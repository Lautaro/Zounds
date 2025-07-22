using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System;


#if ADDRESSABLES_INSTALLED
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets;
#endif

namespace Zounds {

    public static class AudioAssetUtility {

#if ADDRESSABLES_INSTALLED
        public static List<AssetReferenceT<AudioClip>> FindAllAudioReferencesInFolder(string folderPath) {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
            List<AssetReferenceT<AudioClip>> references = new List<AssetReferenceT<AudioClip>>();

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) {
                Debug.LogError("Addressable Asset Settings not found!");
                return references;
            }

            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AddressableAssetEntry entry = settings.FindAssetEntry(guid);

                if (entry != null) { // check if the AudioClip is actually Addressable
                    AssetReferenceT<AudioClip> reference = new AssetReferenceT<AudioClip>(guid);
                    references.Add(reference);
                }
            }

            return references;
        }

        public static void FindAllAudioReferencesInWorkspace(out List<AssetReferenceT<AudioClip>> userAudioRefs, out List<AssetReferenceT<AudioClip>> workAudioRefs, out List<AssetReferenceT<AudioClip>> sourceAudioRefs) {
            var projectSettings = ZoundsProject.Instance.projectSettings;
            var sourcePath = projectSettings.sourceFolderPath;
            var userPath = projectSettings.userFolderPath;
            var workPath = projectSettings.workFolderPath;

            sourceAudioRefs = FindAllAudioReferencesInFolder(sourcePath);
            userAudioRefs = FindAllAudioReferencesInFolder(userPath);
            workAudioRefs = FindAllAudioReferencesInFolder(workPath);
        }
#endif

        public static bool DisplayZoundRemoveDialog(Zound zoundToRemove) {
            List<Zound> directlyAffectedZounds = GetDirectZoundReferences(zoundToRemove);
            List<Zound> nestedAffectedZounds = GetNestedZoundReferences(zoundToRemove);

            int affectedCount = directlyAffectedZounds.Count + nestedAffectedZounds.Count;

            string affectedZoundsString;
            if (affectedCount == 0) {
                affectedZoundsString = "No other zound affected.";
            }
            else {
                if (affectedCount == 1) {
                    affectedZoundsString = "1 zound affected:\n";
                }
                else {
                    affectedZoundsString = directlyAffectedZounds.Count + " zounds affected:\n";
                }
                foreach (var zound in directlyAffectedZounds) {
                    affectedZoundsString += "- " + zound.name + " (Direct)\n";
                }
                foreach (var zound in nestedAffectedZounds) {
                    affectedZoundsString += "- " + zound.name + " (Nested)\n";
                }
            }

            return EditorUtility.DisplayDialog("Remove Zound", string.Format("Are you sure you want to remove this zound?\n> {0}\n\n{1}", zoundToRemove.name, affectedZoundsString), "Remove", "Cancel");
        }

        public static void RemoveZound(Zound zoundToRemove) {
            var library = ZoundsProject.Instance.zoundLibrary;
            if (zoundToRemove is Klip klip) {
                library.klips.Remove(klip);
            }
            else if (zoundToRemove is Zequence zequence) {
                library.zequences.Remove(zequence);
            }
            else if (zoundToRemove is Muzic muzic) {
                library.muzics.Remove(muzic);
            }

            List<Zound> affectedZounds = GetDirectZoundReferences(zoundToRemove);
            foreach (var zound in affectedZounds) {
                zound.RemoveDependency(zoundToRemove);
            }
        }

        public static Zound DuplicateZound(Zound zoundToDuplicate) {
            var library = ZoundsProject.Instance.zoundLibrary;
            Zound result = null;

            if (zoundToDuplicate is Klip klip) {
                result = new Klip(ZoundLibrary.GetUniqueZoundId(), klip);
                library.klips.Add((Klip)result);
            }
            else if (zoundToDuplicate is Zequence zequence) {
                result = new Zequence(ZoundLibrary.GetUniqueZoundId(), zequence);
                library.zequences.Add((Zequence)result);
            }
            else if (zoundToDuplicate is Muzic muzic) {
                result = new Muzic(ZoundLibrary.GetUniqueZoundId(), muzic);
                library.muzics.Add((Muzic)result);
            }

            return result;
        }

        public static List<Zound> GetDirectZoundReferences(Zound zoundToRemove) {
            var library = ZoundsProject.Instance.zoundLibrary;
            return library.FindAllZounds(z => z.HasDirectDependency(zoundToRemove));
        }

        public static List<Zound> GetNestedZoundReferences(Zound zoundToRemove) {
            var library = ZoundsProject.Instance.zoundLibrary;
            return library.FindAllZounds(z => z.HasNestedDependency(zoundToRemove));
        }

    }

}