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

                if (entry != null) // check if the AudioClip is actually Addressable
                {
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
            var workPath = projectSettings.systemFolderPath + "/WorkFiles";

            var result = new List<AssetReferenceT<AudioClip>>();
            result.AddRange(FindAllAudioReferencesInFolder(sourcePath));
            result.AddRange(FindAllAudioReferencesInFolder(userPath));
            result.AddRange(FindAllAudioReferencesInFolder(workPath));

            return result;
        }
#endif

    }

}