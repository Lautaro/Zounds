using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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

        public static List<AssetReferenceT<AudioClip>> FindAllAudioReferencesInWorkspace() {
            var projectSettings = ZoundsProject.Instance.projectSettings;
            var sourcePath = projectSettings.sourceFolderPath;
            var userPath = projectSettings.userFolderPath;
            var workPath = projectSettings.workFolderPath;

            var result = new List<AssetReferenceT<AudioClip>>();
            result.AddRange(FindAllAudioReferencesInFolder(sourcePath));
            result.AddRange(FindAllAudioReferencesInFolder(userPath));
            result.AddRange(FindAllAudioReferencesInFolder(workPath));

            return result;
        }
#endif

        public static bool DisplayZoundRemoveDialog(Zound zoundToRemove) {
            List<Zound> affectedZounds = GetDependentZounds(zoundToRemove);

            string affectedZoundsString;
            if (affectedZounds.Count == 0) {
                affectedZoundsString = "No other zound affected.";
            }
            else if (affectedZounds.Count == 1) {
                affectedZoundsString = "1 zound affected:\n" + affectedZounds[0].name;
            }
            else {
                affectedZoundsString = affectedZounds.Count + " zounds affected:\n";
                foreach (var zound in affectedZounds) {
                    affectedZoundsString += "- " + zound.name + "\n";
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
            else if (zoundToRemove is Randomizer randomizer) {
                library.randomizers.Remove(randomizer);
            }

            List<Zound> affectedZounds = GetDependentZounds(zoundToRemove);
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
            else if (zoundToDuplicate is Randomizer randomizer) {
                result = new Randomizer(ZoundLibrary.GetUniqueZoundId(), randomizer);
                library.randomizers.Add((Randomizer)result);
            }

            return result;
        }

        private static List<Zound> GetDependentZounds(Zound zoundToRemove) {
            var library = ZoundsProject.Instance.zoundLibrary;
            List<Zound> affectedZounds = new List<Zound>();
            affectedZounds.AddRange(library.klips.FindAll(z => z.HasDependency(zoundToRemove)));
            affectedZounds.AddRange(library.zequences.FindAll(z => z.HasDependency(zoundToRemove)));
            affectedZounds.AddRange(library.muzics.FindAll(z => z.HasDependency(zoundToRemove)));
            affectedZounds.AddRange(library.randomizers.FindAll(z => z.HasDependency(zoundToRemove)));
            return affectedZounds;
        }

    }

}