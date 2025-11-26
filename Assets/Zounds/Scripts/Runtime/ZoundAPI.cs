using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Zounds {

    public static class ZoundAPI {

#if UNITY_EDITOR
        internal static System.Action<Klip> onEditorAPIKlipCreated;
        internal static System.Action<Zequence> onEditorAPIZequenceCreated;
        internal static System.Action<Zequence, Zound, bool> onEditorAPIZequenceAddZound;
        internal static System.Action onSetAllTabsDirty;
#endif

        public static Klip CreateKlip(AudioClip audioClip, string name = null) {
            AssetReference assetRef = GetClipAssetReference(audioClip);

            if (assetRef.RuntimeKeyIsValid()) {
                return CreateNewKlip(name == null ? audioClip.name : name, assetRef, audioClip.length);
            }
            else {
                Debug.Log("Error creating a Klip: Audio clip " + audioClip.name + " is not an addressable.", audioClip);
                return null;
            }
        }

        public static Klip CreateKlip(AssetReference audioClipReference, string name = null) {
            AudioClip audioClip = GetAudioClipFromAssetReference(audioClipReference);
            if (audioClip != null) {
                return CreateNewKlip(name == null ? audioClip.name : name, audioClipReference, audioClip.length);
            }
            else {
                Debug.Log("Error creating a Klip: Invalid audio clip reference.");
                return null;
            }
        }

        public static Zequence CreateZequence(string name) {
            var newZequence = new Zequence(ZoundLibrary.GetUniqueZoundId());
            newZequence.name = ZoundDictionary.EnsureUniqueZoundName(name);

            var zoundsProject = ZoundsProject.Instance;

            bool editorAdd;
#if UNITY_EDITOR
            editorAdd = true;
#else
            editorAdd = false;
#endif

            if (!editorAdd) {
                var zoundKey = ZoundDictionary.ZoundNameToKey(newZequence.name);
                if (ZoundDictionary.zoundDictionary.ContainsKey(zoundKey)) {
                    ZoundDictionary.zoundDictionary.Remove(zoundKey);
                }

                var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                zoundLibrary.zequences.Add(newZequence);
                zoundLibrary.zequences = zoundLibrary.zequences.OrderBy(it => it.name).ToList();

                //if (Application.isPlaying) {
                if (ZoundEngine.IsInitialized()) {
                    ZoundDictionary.ValidateZoundRuntime(newZequence);
                }
                //}
            }
#if UNITY_EDITOR
            else {
                UnityEditor.Undo.RecordObject(zoundsProject, "create new zequence");
                onEditorAPIZequenceCreated?.Invoke(newZequence);
                UnityEditor.EditorUtility.SetDirty(zoundsProject);
            }
#endif

            return newZequence;
        }

        public static void AddSharedZoundToZequence(Zound childZoundToAdd, Zequence existingZequence) {
            bool editorAdd;
#if UNITY_EDITOR
            editorAdd = true;
#else
            editorAdd = false;
#endif

            if (!editorAdd) {
                var zoundsProject = ZoundsProject.Instance;
                var newEntry = new CompositeZound.ZoundEntry();
                newEntry.zoundId = childZoundToAdd.id;
                newEntry.local = false;
                existingZequence.zoundEntries.Add(newEntry);
            }
#if UNITY_EDITOR
            else {
                onEditorAPIZequenceAddZound?.Invoke(existingZequence, childZoundToAdd, false);
            }
#endif
        }

        public static Klip CreateLocalKlip(Zequence existingZequence, AudioClip audioClip, string name = null) {
            AssetReference assetRef = GetClipAssetReference(audioClip);

            if (assetRef.RuntimeKeyIsValid()) {
                return CreateNewLocalKlip(existingZequence, name == null ? audioClip.name : name, assetRef, audioClip.length);
            }
            else {
                Debug.Log("Error creating a Klip: Audio clip " + audioClip.name + " is not an addressable.", audioClip);
                return null;
            }
        }

        public static Klip CreateLocalKlip(Zequence existingZequence, AssetReference audioClipReference, string name = null) {
            AudioClip audioClip = GetAudioClipFromAssetReference(audioClipReference);
            if (audioClip != null) {
                return CreateNewLocalKlip(existingZequence,name == null ? audioClip.name : name, audioClipReference, audioClip.length);
            }
            else {
                Debug.Log("Error creating a Klip: Invalid audio clip reference.");
                return null;
            }
        }

        public static Zequence CreateLocalZequence(Zequence existingZequence, string name) {
            var newZequence = new Zequence(ZoundLibrary.GetUniqueZoundId());
            newZequence.name = ZoundDictionary.EnsureUniqueZoundName(name);

            var zoundsProject = ZoundsProject.Instance;

            bool editorAdd;
#if UNITY_EDITOR
            editorAdd = true;
#else
            editorAdd = false;
#endif

            if (!editorAdd) {
                newZequence.parentId = existingZequence.id;
                existingZequence.localZequences.Add(new CompositeZound.LocalZequence(newZequence));
                var newEntry = new CompositeZound.ZoundEntry();
                newEntry.zoundId = newZequence.id;
                newEntry.local = true;
                existingZequence.zoundEntries.Add(newEntry);
            }
#if UNITY_EDITOR
            else {
                UnityEditor.Undo.RecordObject(zoundsProject, "add local zound entry");
                newZequence.parentId = existingZequence.id;
                existingZequence.localZequences.Add(new CompositeZound.LocalZequence(newZequence));
                UnityEditor.EditorUtility.SetDirty(zoundsProject);
                onEditorAPIZequenceAddZound?.Invoke(existingZequence, newZequence, true);
            }
#endif

            return newZequence;
        }

        public static void AddTagToZound(Zound zound, string tagName, string tagValue = null) {
            var zoundsProject = ZoundsProject.Instance;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(zoundsProject, "add tag");
#endif
            var zoundLibrary = zoundsProject.zoundLibrary;
            var projectTags = zoundLibrary.tags;
            var zoundTags = projectTags.Where(tag => zound.tags.Contains(tag.id));

            ZoundLibrary.Tag existingTag = null;
            foreach (var t in zoundTags) {
                var split = t.name.Split(':');
                if (split[0] == tagName) {
                    existingTag = t;
                    break;
                }
            }

            if (existingTag != null) {
                zound.tags.Remove(existingTag.id);
            }

            if (!string.IsNullOrWhiteSpace(tagValue)) {
                tagName += ":" + tagValue;
            }
            if (!zoundLibrary.TryGetTag(tagName, out var tagToAdd)) {
                tagToAdd = zoundLibrary.CreateNewTag(tagName);
            }
            zound.tags.Add(tagToAdd.id);
            zoundLibrary.RemoveUnusedTags();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(zoundsProject);
#endif
        }

        public static bool DoesZoundExist(string zoundName) {
            return ZoundDictionary.TryGetZoundByName(zoundName, out _);
        }

        public static List<Zound> GetAllZounds() {
            bool editorAdd;
#if UNITY_EDITOR
            editorAdd = true;
#else
            editorAdd = false;
#endif
            if (!editorAdd || Application.isPlaying) {
                return ZoundDictionary.zoundDictionary.Values.ToList();
            }
            else {
#if UNITY_EDITOR
                var zounds = ZoundsProject.Instance.zoundLibrary.GetAllZounds();
                zounds.AddRange(ZoundDictionary.editorAudioClipZoundsCache);
                return zounds;
#endif
            }
        }

        public static Zound GetZound(string zoundName) {
            return ZoundDictionary.GetZoundByName(zoundName);
        }

        public static IEnumerable<string> GetAllTags() {
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            return zoundLibrary.tags.Select(t => t.name);
        }

        public static IEnumerable<string> GetTagsInZound(string zoundName) {
            var zound = GetZound(zoundName);
            if (zound == null) {
                Debug.LogError("Zound doesn't exist: " + zoundName);
                return null;
            }
            else return GetTagsInZound(zound);
        }

        public static IEnumerable<string> GetTagsInZound(Zound zound) {
            var projectTags = ZoundsProject.Instance.zoundLibrary.tags;
            var zoundTags = projectTags.Where(tag => zound.tags.Contains(tag.id));
            return zoundTags.Select(t => t.name);
        }

        public static List<Zound> GetAllZoundsByTag(string tagName) {
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            var result = new List<Zound>();
            if (ZoundsProject.Instance.zoundLibrary.TryGetTag(tagName, out var tag)) {
                result.AddRange(zoundLibrary.FindAllZounds(z => z.tags.Contains(tag.id)));
            }

            // add other kvp tags which key match the specified tag
            var nameSplit = tagName.Split(':');
            if (nameSplit.Length == 1) {
                string keyTag = nameSplit[0];
                foreach (var otherTag in zoundLibrary.tags) {
                    if (tag != null && otherTag == tag) continue;
                    var otherNameSplit = otherTag.name.Split(':');
                    if (otherNameSplit.Length > 1 && otherNameSplit[0] == keyTag) {
                        result.AddRange(zoundLibrary.FindAllZounds(z => z.tags.Contains(otherTag.id)));
                    }
                }
            }

            return result;
        }

        public static List<Zound> GetAllZoundsByTags(params string[] tagNames) {
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;

            var result = zoundLibrary.FindAllZounds(zound => {
                bool include = true;
                foreach (var tagName in tagNames) {
                    if (ZoundsProject.Instance.zoundLibrary.TryGetTag(tagName, out var tag)) {
                        if (zound.tags.Contains(tag.id)) continue;
                    }

                    // add other kvp tags which key match the specified tag
                    var nameSplit = tagName.Split(':');
                    if (nameSplit.Length == 1) {
                        bool found = false;
                        string keyTag = nameSplit[0];
                        foreach (var otherTag in zoundLibrary.tags) {
                            if (found) break;
                            if (tag != null && otherTag == tag) continue;
                            var otherNameSplit = otherTag.name.Split(':');
                            if (otherNameSplit.Length > 1 && otherNameSplit[0] == keyTag) {
                                if (zound.tags.Contains(otherTag.id)) {
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (found) continue;
                    }
                    include = false;
                    break;
                }
                return include;
            });

            return result;
        }




        private static AssetReference GetClipAssetReference(AudioClip audioClip) {
            AssetReference assetRef;
            if (Application.isPlaying) {
                assetRef = ZoundDictionary.FindAudioClipAssetReference(audioClip);
                if (assetRef == null) {
#if UNITY_EDITOR
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(audioClip);
                    string guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
                    assetRef = new AssetReference(guid);
#endif
                }
                if (assetRef == null || !assetRef.RuntimeKeyIsValid()) {
                    Debug.LogError("Can't find addressable AssetReference for audio clip " + audioClip.name + ", or ZoundEngine is not initialized yet.");
                    return null;
                }
            }
            else {
#if UNITY_EDITOR
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(audioClip);
                string guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
                assetRef = new AssetReference(guid);
#endif
            }
            return assetRef;
        }

        private static AudioClip GetAudioClipFromAssetReference(AssetReference audioClipReference) {
            AudioClip audioClip;
            if (Application.isPlaying) {
                audioClip = ZoundDictionary.GetOrLoadClip(audioClipReference);
            }
            else {
#if UNITY_EDITOR
                audioClip = audioClipReference.editorAsset as AudioClip;
#endif
            }

            return audioClip;
        }

        private static Klip CreateNewKlip(string name, AssetReference assetRef, float clipLength) {
            var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());

            newKlip.audioClipRef = assetRef;
            newKlip.name = ZoundDictionary.EnsureUniqueZoundName(name);

            newKlip.trimStart = 0f;
            newKlip.trimEnd = clipLength;
            newKlip.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
            newKlip.pitchEnvelope = new Envelope(Zound.MinPitchRange, Zound.MaxPitchRange);

            //if (Application.isPlaying) {
            if (ZoundEngine.IsInitialized()) {
                ZoundDictionary.ValidateZoundRuntime(newKlip);
            }
            //}

            var zoundsProject = ZoundsProject.Instance;

            bool editorAdd;
#if UNITY_EDITOR
            editorAdd = true;
#else
            editorAdd = false;
#endif

            if (!editorAdd) {
                var zoundKey = ZoundDictionary.ZoundNameToKey(newKlip.name);
                if (ZoundDictionary.zoundDictionary.ContainsKey(zoundKey)) {
                    ZoundDictionary.zoundDictionary.Remove(zoundKey);
                }

                var zoundLibrary = zoundsProject.zoundLibrary;
                zoundLibrary.klips.Add(newKlip);
                zoundLibrary.klips = zoundLibrary.klips.OrderBy(it => it.name).ToList();
            }
#if UNITY_EDITOR
            else {
                UnityEditor.Undo.RecordObject(zoundsProject, "create new klip");
                onEditorAPIKlipCreated?.Invoke(newKlip);
                UnityEditor.EditorUtility.SetDirty(zoundsProject);
            }
#endif

            return newKlip;
        }

        private static Klip CreateNewLocalKlip(Zequence existingZequence, string name, AssetReference assetRef, float clipLength) {
            var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());

            newKlip.audioClipRef = assetRef;
            newKlip.name = ZoundDictionary.EnsureUniqueZoundName(name);

            newKlip.trimStart = 0f;
            newKlip.trimEnd = clipLength;
            newKlip.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
            newKlip.pitchEnvelope = new Envelope(Zound.MinPitchRange, Zound.MaxPitchRange);

            //if (Application.isPlaying) {
            if (ZoundEngine.IsInitialized()) {
                ZoundDictionary.ValidateZoundRuntime(newKlip);
            }
            //}

            var zoundsProject = ZoundsProject.Instance;

            bool editorAdd;
#if UNITY_EDITOR
            editorAdd = true;
#else
            editorAdd = false;
#endif

            if (!editorAdd) {
                newKlip.parentId = existingZequence.id;
                existingZequence.localKlips.Add(newKlip);
                var newEntry = new CompositeZound.ZoundEntry();
                newEntry.zoundId = newKlip.id;
                newEntry.local = true;
                existingZequence.zoundEntries.Add(newEntry);
            }
#if UNITY_EDITOR
            else {
                UnityEditor.Undo.RecordObject(zoundsProject, "add local zound entry");
                newKlip.parentId = existingZequence.id;
                existingZequence.localKlips.Add(newKlip);
                UnityEditor.EditorUtility.SetDirty(zoundsProject);
                onEditorAPIZequenceAddZound?.Invoke(existingZequence, newKlip, true);
            }
#endif

            return newKlip;
        }

    }

}
