#if ADDRESSABLES_INSTALLED
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Zounds {

    /// <summary>
    /// Editor utility that finds and removes orphaned audio files in WorkFiles and ZoundFiles.
    /// An orphan is any file whose GUID is not referenced by any Klip or Zequence in the library.
    /// </summary>
    public static class OrphanCleaner {

        /// <summary>
        /// Returns a list of asset paths in WorkFiles and ZoundFiles that are not referenced
        /// by any Klip or Zequence in the current ZoundsProject library.
        /// </summary>
        public static List<string> FindOrphans() {
            var projectSettings = ZoundsProject.Instance.projectSettings;
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;

            // Collect all audio files in WorkFiles and ZoundFiles
            string[] searchFolders = new[] { projectSettings.workFolderPath, projectSettings.zoundFilesFolderPath };
            string[] allGuids = AssetDatabase.FindAssets("t:AudioClip", searchFolders);

            // Collect all referenced GUIDs from the library
            var referencedGuids = new HashSet<string>();
            zoundLibrary.ForEachZound(z => {
                if (z is Klip klip) {
                    if (klip.audioClipRef != null && !string.IsNullOrEmpty(klip.audioClipRef.AssetGUID))
                        referencedGuids.Add(klip.audioClipRef.AssetGUID);
                    if (klip.renderedClipRef != null && !string.IsNullOrEmpty(klip.renderedClipRef.AssetGUID))
                        referencedGuids.Add(klip.renderedClipRef.AssetGUID);
                    if (!string.IsNullOrEmpty(klip.audioClipPath))
                        referencedGuids.Add(AssetDatabase.AssetPathToGUID(klip.audioClipPath));
                    if (!string.IsNullOrEmpty(klip.renderedClipPath))
                        referencedGuids.Add(AssetDatabase.AssetPathToGUID(klip.renderedClipPath));
                }
                else if (z is Zequence zequence) {
                    if (zequence.renderedClipRef != null && !string.IsNullOrEmpty(zequence.renderedClipRef.AssetGUID))
                        referencedGuids.Add(zequence.renderedClipRef.AssetGUID);
                    if (!string.IsNullOrEmpty(zequence.renderedClipPath))
                        referencedGuids.Add(AssetDatabase.AssetPathToGUID(zequence.renderedClipPath));
                }
                return false;
            });

            var orphans = new List<string>();
            foreach (string guid in allGuids) {
                if (!referencedGuids.Contains(guid)) {
                    orphans.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            return orphans;
        }

        /// <summary>
        /// Deletes the specified orphan files from disk and removes them from Addressables.
        /// </summary>
        public static void DeleteOrphans(List<string> orphanPaths) {
            if (orphanPaths == null || orphanPaths.Count == 0) return;

            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;

            foreach (string path in orphanPaths) {
                string guid = AssetDatabase.AssetPathToGUID(path);

                // Remove from Addressables if registered
                if (addressableSettings != null) {
                    var entry = addressableSettings.FindAssetEntry(guid);
                    if (entry != null) {
                        addressableSettings.RemoveAssetEntry(guid);
                    }
                }

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[Zounds] Deleted orphaned file: {path}");
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Zounds/Clean Up Orphaned Files")]
        private static void CleanUpOrphanedFiles() {
            var orphans = FindOrphans();

            if (orphans.Count == 0) {
                EditorUtility.DisplayDialog("Zounds Orphan Cleaner", "No orphaned files found in WorkFiles or ZoundFiles.", "OK");
                return;
            }

            string orphanList = string.Join("\n", orphans);
            bool confirmed = EditorUtility.DisplayDialog(
                "Zounds Orphan Cleaner",
                $"Found {orphans.Count} orphaned file(s):\n\n{orphanList}\n\nDelete these files?",
                "Delete",
                "Cancel");

            if (confirmed) {
                DeleteOrphans(orphans);
                EditorUtility.DisplayDialog("Zounds Orphan Cleaner", $"Deleted {orphans.Count} orphaned file(s).", "OK");
            }
        }
    }

}
#endif
