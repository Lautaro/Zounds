using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace Zounds {

    public static class ZoundDictionary {

        private static Dictionary<string, Zound> zoundDictionary = new Dictionary<string, Zound>();
        private static Dictionary<int, Zound> zoundDictionaryById = new Dictionary<int, Zound>();
        private static Dictionary<AssetReference, AudioClip> loadedClips = new Dictionary<AssetReference, AudioClip>();
        private static Dictionary<string, AudioClip> loadedUserClips = new Dictionary<string, AudioClip>();

#if ADDRESSABLES_INSTALLED
        internal static void Initialize() {
            if (!Application.isPlaying) {
                Debug.LogError("Can't initialize ZoundDictionary during edit mode.");
                return;
            }
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

                            if (!loadedUserClips.TryGetValue(resLocation.PrimaryKey, out AudioClip clip)) {
                                var handle = Addressables.LoadAssetAsync<AudioClip>(resLocation.PrimaryKey);
                                handle.Completed += ao => {
                                    var clipZound = new ClipZound(ao.Result);
                                    zoundDictionary.Add(zoundKey, clipZound);
                                };
                                handle.WaitForCompletion();
                            }
                        }
                    }
                }
            }
            AddZoundsToDictionary(clipZounds);
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

                            if (!loadedUserClips.TryGetValue(resLocation.PrimaryKey, out AudioClip clip)) {
                                var handle = Addressables.LoadAssetAsync<AudioClip>(resLocation.PrimaryKey);
                                handle.Completed += ao => {
                                    var clipZound = new ClipZound(ao.Result);
                                    zoundDictionary.Add(zoundKey, clipZound);
                                };
                                tasks.Add(handle.Task);
                            }
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
                zound = library.klips.Find(klip => klip.id == zoundId);
                if (zound == null) zound = library.zequences.Find(zequence => zequence.id == zoundId);
                if (zound == null) zound = library.muzics.Find(muzic => muzic.id == zoundId);
                if (zound == null) zound = library.randomizers.Find(randomizer => randomizer.id == zoundId);
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
            return null;
        }

        public static string ZoundNameToKey(string zoundName) {
            return zoundName.ToLower()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");
        }

        public static string EnsureUniqueZoundName(string zoundName) {
            var zoundsProject = ZoundsProject.Instance;
            var library = zoundsProject.zoundLibrary;
            string key = ZoundNameToKey(zoundName);
            string currentKey = key;
            int iteration = 0;
            bool isUnique = false;
            while (true) {
                isUnique = library.klips.Find(z => ZoundNameToKey(z.name) == currentKey) == null &&
                           library.zequences.Find(z => ZoundNameToKey(z.name) == currentKey) == null &&
                           library.muzics.Find(z => ZoundNameToKey(z.name) == currentKey) == null &&
                           library.randomizers.Find(z => ZoundNameToKey(z.name) == currentKey) == null;
                if (isUnique) break;
                iteration++;
                currentKey = key + iteration.ToString();
            }

            if (iteration == 0) return zoundName;
            else return zoundName + " " + iteration;
        }

        public static void ValidateZoundRuntime(Zound zoundToValidate = null) {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            string key = ZoundNameToKey(zoundToValidate.name);
            bool handled = false;
            foreach (var kvp in zoundDictionary) {
                if (zoundToValidate == kvp.Value) {
                    zoundDictionary.Remove(kvp.Key);
                    if (zoundDictionary.ContainsKey(key)) {
                        Debug.LogError("Multiple zounds with the same key exist: " + key);
                    }
                    else {
                        zoundDictionary.Add(key, zoundToValidate);
                    }
                    handled = true;
                    break;
                }
            }
            if (!handled) {
                if (zoundDictionary.ContainsKey(key)) {
                    Debug.LogError("Multiple zounds with the same key exist: " + key);
                    return;
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

        private static void InitZoundsDictionary() {
            if (zoundDictionary == null || zoundDictionary.Count == 0) {
                zoundDictionary = new Dictionary<string, Zound>();
                var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                AddZoundsToDictionary(zoundLibrary.klips);
                AddZoundsToDictionary(zoundLibrary.zequences);
                AddZoundsToDictionary(zoundLibrary.muzics);
                AddZoundsToDictionary(zoundLibrary.randomizers);
            }
            if (zoundDictionaryById == null || zoundDictionaryById.Count == 0) {
                zoundDictionaryById = new Dictionary<int, Zound>();
                var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                AddZoundsToDictionaryById(zoundLibrary.klips);
                AddZoundsToDictionaryById(zoundLibrary.zequences);
                AddZoundsToDictionaryById(zoundLibrary.muzics);
                AddZoundsToDictionaryById(zoundLibrary.randomizers);
            }
        }

        private static void AddZoundsToDictionary<TZound>(List<TZound> zounds) where TZound : Zound {
            foreach (var zound in zounds) {
                string key = ZoundNameToKey(zound.name);
                if (zoundDictionary.ContainsKey(key)) {
                    Debug.LogError("Multiple zounds with the same key exist: " + key);
                    continue;
                }
                zoundDictionary.Add(key, zound);
            }
        }

        private static void AddZoundsToDictionaryById<TZound>(List<TZound> zounds) where TZound : Zound {
            foreach (var zound in zounds) {
                if (zoundDictionaryById.ContainsKey(zound.id)) {
                    Debug.LogError("Multiple zounds with the same id exist: " + zound.id + "(" + zound.name + " & " + zoundDictionaryById[zound.id].name + ")");
                    continue;
                }
                zoundDictionaryById.Add(zound.id, zound);
            }
        }

    }

}
