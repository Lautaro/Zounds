using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace Zounds {

    public static class ZoundDictionary {

        internal static Dictionary<string, Zound> zoundDictionary = new Dictionary<string, Zound>();
        private static Dictionary<int, Zound> zoundDictionaryById = new Dictionary<int, Zound>();
        private static Dictionary<AssetReference, AudioClip> loadedClips = new Dictionary<AssetReference, AudioClip>();

        internal static Dictionary<AudioClip, string> runtimeClipFolders = new Dictionary<AudioClip, string>();

#if UNITY_EDITOR
        internal static List<ClipZound> editorAudioClipZoundsCache;
#endif

#if ADDRESSABLES_INSTALLED
        internal static void Initialize() {
            if (!Application.isPlaying) {
                Debug.LogError("Can't initialize ZoundDictionary during edit mode.");
                return;
            }
            zoundDictionary.Clear();
            runtimeClipFolders.Clear();
            InitZoundsDictionary();
            var ao = Addressables.InitializeAsync();
            ao.WaitForCompletion();
            foreach (var zound in zoundDictionary.Values) {
                if (zound is IZoundAudioClip zoundAudioClip) {
                    var clipRef = zoundAudioClip.GetAudioClipReference();
                    GetOrLoadClip(clipRef);
                }
            }
            InitUserAudioClips();
        }

        internal static async Task InitializeAsync() {
            if (!Application.isPlaying) {
                Debug.LogError("Can't initialize ZoundDictionary during edit mode.");
                return;
            }
            zoundDictionary.Clear();
            runtimeClipFolders.Clear();
            InitZoundsDictionary();
            await Addressables.InitializeAsync().Task;
            var tasks = new List<Task>();
            foreach (var zound in zoundDictionary.Values) {
                if (zound is IZoundAudioClip zoundAudioClip) {
                    var clipRef = zoundAudioClip.GetAudioClipReference();
                    if (!GetOrLoadClipAsync(tasks, zound, clipRef)) {
                        continue;
                    }
                }
            }
            await Task.WhenAll(tasks);
            await InitUserAudioClipsAsync();
        }

        public static AudioClip GetOrLoadClip(AssetReference clipRef) {
#if UNITY_EDITOR
            if (!Application.isPlaying) return clipRef.editorAsset as AudioClip;
#endif
            if (!loadedClips.TryGetValue(clipRef, out AudioClip clip)) {
                if (clipRef.IsValid()) {
                    clip = clipRef.Asset as AudioClip;
                    loadedClips.Add(clipRef, clip);
                }
                else {
                    if (clipRef.RuntimeKeyIsValid()) {
                        var handle = clipRef.LoadAssetAsync<AudioClip>();
                        clip = handle.WaitForCompletion();
                    }
                    else {
                        Debug.LogError("Invalid AudioClip asset reference.");
                        return null;
                    }
                }
            }
            return clip;
        }

        private static bool GetOrLoadClipAsync(List<Task> tasks, Zound zound, AssetReference clipRef) {
            if (!loadedClips.TryGetValue(clipRef, out AudioClip clip)) {
                if (clipRef.IsValid()) {
                    clip = clipRef.Asset as AudioClip;
                    loadedClips.Add(clipRef, clip);
                    return true;
                }
                else {
                    if (clipRef.RuntimeKeyIsValid()) {
                        var handle = clipRef.LoadAssetAsync<AudioClip>();
                        tasks.Add(handle.Task);
                        return true;
                    }
                    else {
                        if (zound != null) {
                            Debug.LogError("Invalid AudioClip asset reference at zound: " + zound.name);
                        }
                        return false;
                    }
                }
            }
            return false;
        }

        private static void InitUserAudioClips() {
            string userFolderPath = ZoundsProject.Instance.projectSettings.userFolderPath;
            var visitedLocations = new HashSet<string>();

            var clipZounds = new List<ClipZound>();
            foreach (var loc in Addressables.ResourceLocators) {
                foreach (var key in loc.Keys) {
                    if (!loc.Locate(key, typeof(object), out var resourceLocations))
                        continue;
                    foreach (var resLocation in resourceLocations) {
                        if (resLocation.PrimaryKey.StartsWith(userFolderPath)) {
                            string normalizedAddress = resLocation.PrimaryKey.Replace('\\', '/');
                            if (visitedLocations.Contains(normalizedAddress)) continue;
                            visitedLocations.Add(normalizedAddress);
                            string assetName = System.IO.Path.GetFileNameWithoutExtension(normalizedAddress);
                            string zoundKey = ZoundNameToKey(assetName);
                            if (zoundDictionary.ContainsKey(zoundKey)) {
                                continue;
                            }

                            string primaryKey = resLocation.PrimaryKey;
                            var handle = Addressables.LoadAssetAsync<AudioClip>(primaryKey);
                            handle.Completed += ao => {
#if UNITY_EDITOR
                                ClipZound clipZound = null;
                                if (editorAudioClipZoundsCache != null) {
                                    clipZound = editorAudioClipZoundsCache.Find(c => c.name == ao.Result.name);
                                }
                                if (clipZound == null) {
                                    Debug.LogError("New Zound is created from AudioClip " + ao.Result.name + " which somehow didn't present in the AudioClip cache.");
                                    clipZound = new ClipZound(ao.Result, primaryKey);
                                }
#else
                                var clipZound = new ClipZound(ao.Result, primaryKey);
#endif
                                zoundDictionary.Add(zoundKey, clipZound);
                            };
                            handle.WaitForCompletion();
                        }
                    }
                }
            }
            foreach (var clipZound in clipZounds) {
                AddZoundToKeysDictionary(clipZound);
            }
        }

        private static async Task InitUserAudioClipsAsync() {
            string userFolderPath = ZoundsProject.Instance.projectSettings.userFolderPath;
            var tasks = new List<Task>();
            var visitedLocations = new HashSet<string>();
            foreach (var loc in Addressables.ResourceLocators) {
                foreach (var key in loc.Keys) {
                    if (!loc.Locate(key, typeof(object), out var resourceLocations))
                        continue;
                    foreach (var resLocation in resourceLocations) {
                        if (resLocation.PrimaryKey.StartsWith(userFolderPath)) {
                            string normalizedAddress = resLocation.PrimaryKey.Replace('\\', '/');
                            if (visitedLocations.Contains(normalizedAddress)) continue;
                            visitedLocations.Add(normalizedAddress);

                            string assetName = System.IO.Path.GetFileNameWithoutExtension(normalizedAddress);
                            string zoundKey = ZoundNameToKey(assetName);
                            if (zoundDictionary.ContainsKey(zoundKey)) {
                                continue;
                            }
                            var handle = Addressables.LoadAssetAsync<AudioClip>(resLocation.PrimaryKey);
                            handle.Completed += ao => {
#if UNITY_EDITOR
                                ClipZound clipZound = null;
                                if (editorAudioClipZoundsCache != null) {
                                    clipZound = editorAudioClipZoundsCache.Find(c => c.name == ao.Result.name);
                                }
                                if (clipZound == null) {
                                    Debug.LogError("New Zound is created from AudioClip " + ao.Result.name + " which somehow didn't present in the AudioClip cache.");
                                    clipZound = new ClipZound(ao.Result, resLocation.PrimaryKey);
                                }
#else
                                var clipZound = new ClipZound(ao.Result, resLocation.PrimaryKey);
#endif
                                zoundDictionary.Add(zoundKey, clipZound);
                            };
                            tasks.Add(handle.Task);
                        }
                    }
                }
            }
            await Task.WhenAll(tasks);
        }

#endif

        public static bool TryGetZoundById(int zoundId, out Zound zound) {
            zound = GetZoundById(zoundId);
            return zound != null;
        }

        public static Zound GetZoundById(int zoundId) {
            if (zoundDictionaryById.TryGetValue(zoundId, out Zound zound)) {
                return zound;
            }
            else {
                var library = ZoundsProject.Instance.zoundLibrary;
                zound = library.FindZound(z => z.id == zoundId);
                if (zound != null) {
                    zoundDictionaryById.Add(zoundId, zound);
                    return zound;
                }
            }
            return null;
        }

        public static bool TryGetZoundByName(string zoundName, out Zound zound) {
            zound = GetZoundByName(zoundName);
            return zound != null;
        }

        public static Zound GetZoundByName(string zoundName) {
            string key = ZoundNameToKey(zoundName);
            if (zoundDictionary.TryGetValue(key, out Zound zound)) {
                return zound;
            }
            else {
                var library = ZoundsProject.Instance.zoundLibrary;
                zound = library.FindZound(z => ZoundNameToKey(z.name) == key);
                if (zound != null) {
                    zoundDictionary.Add(key, zound);
                    return zound;
                }
            }
            return null;
        }

        public static string ZoundNameToKey(string zoundName) {
            return zoundName.ToLower()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");
        }

        public static string EnsureUniqueZoundName(string zoundName, Zound zoundToIgnore = null) {
            var zoundsProject = ZoundsProject.Instance;
            var library = zoundsProject.zoundLibrary;
            string key = ZoundNameToKey(zoundName);
            string currentKey = key;
            int iteration = 0;
            bool isUnique = false;

            bool hasDuplicateNumber = false;
            bool nameHasDuplicateNumber = Regex.IsMatch(zoundName, @"\(\d+\)$");

            while (true) {
                isUnique = library.FindZound(z => z != zoundToIgnore && ZoundNameToKey(z.name) == currentKey) == null;
                // Don't search in local zounds
                //if (isUnique) {
                //    bool foundDuplicate = false;
                //    foreach (var zequence in library.zequences) {
                //        if (zequence.localKlips.Find(k => k != zoundToIgnore && ZoundNameToKey(k.name) == currentKey) != null) {
                //            foundDuplicate = true;
                //        }
                //        if (zequence.localZequences.Find(lr => lr.zequence != zoundToIgnore && ZoundNameToKey(lr.zequence.name) == currentKey) != null) {
                //            foundDuplicate = true;
                //        }
                //        if (foundDuplicate) break;
                //        foreach (var localZequence in zequence.localZequences) {
                //            if (localZequence.zequence.localKlips.Find(k => k != zoundToIgnore && ZoundNameToKey(k.name) == currentKey) != null) {
                //                foundDuplicate = true;
                //                break;
                //            }
                //        }
                //        if (foundDuplicate) break;
                //    }
                //    if (foundDuplicate) isUnique = false;
                //}
                if (isUnique) break;
                iteration++;
                hasDuplicateNumber = Regex.IsMatch(currentKey, @"\(\d+\)$");
                if (hasDuplicateNumber) {
                    currentKey = Regex.Replace(currentKey, @"\(\d+\)$", $"({iteration})");
                }
                else {
                    currentKey = key + "(" + iteration.ToString() + ")";
                }
            }

            if (iteration == 0) return zoundName;
            else {
                if (nameHasDuplicateNumber) return Regex.Replace(zoundName, @"\(\d+\)$", $"({iteration})");
                return zoundName + " (" + iteration + ")";
            }
        }

        public static void ValidateZoundRuntime(Zound zoundToValidate = null) {
//#if UNITY_EDITOR
//            if (!Application.isPlaying) return;
//#endif
            string key = ZoundNameToKey(zoundToValidate.name);
            bool handled = false;
            foreach (var kvp in zoundDictionary) {
                if (zoundToValidate == kvp.Value) {
                    zoundDictionary.Remove(kvp.Key);
                    if (zoundDictionary.TryGetValue(key, out var existingZound)) {
                        if (existingZound is ClipZound clipZound) {
                            zoundDictionary.Remove(key);
                            zoundDictionary.Add(key, zoundToValidate);
                        }
                        else {
                            Debug.LogError("Multiple zounds with the same key exist: " + key);
                        }
                    }
                    else {
                        zoundDictionary.Add(key, zoundToValidate);
                    }
                    handled = true;
                    break;
                }
            }
            if (!handled) {
                if (zoundDictionary.TryGetValue(key, out var existingZound)) {
                    if (existingZound is ClipZound clipZound) {
                        zoundDictionary.Remove(key);
                    }
                    else {
                        //Debug.LogError("Multiple zounds with the same key exist: " + key);
                        return;
                    }
                }
                zoundDictionary.Add(key, zoundToValidate);
                if (zoundToValidate is IZoundAudioClip zoundAudioClip) {
                    var clipRef = zoundAudioClip.GetAudioClipReference();
                    GetOrLoadClip(clipRef);
                }
            }
        }

        internal static ClipZound FindClipZoundByAudioClip(AudioClip audioClip) {
            foreach (var zound in zoundDictionary.Values) {
                if (zound is ClipZound clipZound) {
                    if (clipZound.audioClip == audioClip) {
                        return clipZound;
                    }
                }
            }
            return null;
        }

        internal static AssetReference FindAudioClipAssetReference(AudioClip audioClip) {
            foreach (var kvp in loadedClips) {
                if (kvp.Value == audioClip) return kvp.Key;
            }
            return null;
        }

        private static void InitZoundsDictionary() {
            if (zoundDictionary == null || zoundDictionary.Count == 0) {
                zoundDictionary = new Dictionary<string, Zound>();
                var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                zoundLibrary.ForEachZound(AddZoundToKeysDictionary);
            }
            if (zoundDictionaryById == null || zoundDictionaryById.Count == 0) {
                zoundDictionaryById = new Dictionary<int, Zound>();
                var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                zoundLibrary.ForEachZound(zound => {
                    if (zoundDictionaryById.ContainsKey(zound.id)) {
                        Debug.LogError("Multiple zounds with the same id exist: " + zound.id + "(" + zound.name + " & " + zoundDictionaryById[zound.id].name + ")");
                    }
                    else {
                        zoundDictionaryById.Add(zound.id, zound);
                    }
                });
            }
        }

        private static void AddZoundToKeysDictionary(Zound zound) {
            string key = ZoundNameToKey(zound.name);
            if (zoundDictionary.TryGetValue(key, out var existingZound)) {
                if (existingZound is ClipZound clipZound) {
                    zoundDictionary.Remove(key);
                    zoundDictionary.Add(key, zound);
                }
                else {
                    Debug.LogError("Multiple zounds with the same key exist: " + key);
                }
            }
            else {
                zoundDictionary.Add(key, zound);
            }
        }
    }

}
