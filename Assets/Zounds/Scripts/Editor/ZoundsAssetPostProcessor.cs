#if ADDRESSABLES_INSTALLED
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Zounds {

    public class ZoundsAssetPostProcessor : AssetPostprocessor {

        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;

            if (addressableSettings == null) {
                Debug.LogError("Addressable Asset Settings not found.");
                return;
            }

            var projectSettings = ZoundsProject.Instance.projectSettings;
            List<string> modifiedAssets = null;

            bool dirty = false;
            foreach (var assetPath in importedAssets) {
                if (ProcessAsset(projectSettings, addressableSettings, assetPath)) {
                    if (modifiedAssets == null) modifiedAssets = new List<string>();
                    modifiedAssets.Add(assetPath);
                    dirty = true;
                }
            }
            foreach (var assetPath in movedAssets) {
                if (ProcessAsset(projectSettings, addressableSettings, assetPath)) {
                    if (modifiedAssets == null) modifiedAssets = new List<string>();
                    modifiedAssets.Add(assetPath);
                    dirty = true;
                }
            }

            if (dirty) {
                if (modifiedAssets != null) {
                    string message = string.Format("Updated {0} Addressable Audio Clip(s):\n", modifiedAssets.Count);
                    foreach (var assetPath in modifiedAssets) {
                        message += string.Format("- {0}\n", assetPath);
                    }
                    Debug.Log(message);
                }
                AssetDatabase.SaveAssets();
            }
        }

        private static bool ProcessAsset(ZoundsProject.ProjectSettings projectSettings, AddressableAssetSettings addressableSettings, string assetPath) {
            if (!assetPath.StartsWith(projectSettings.userFolderPath) &&
                !assetPath.StartsWith(projectSettings.sourceFolderPath) &&
                !assetPath.StartsWith(projectSettings.workFolderPath)) {
                return false;
            }

            if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(AudioClip)) {
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            bool dirty = false;

            AddressableAssetEntry entry = addressableSettings.FindAssetEntry(guid);
            if (entry == null) {
                string groupName = "Zounds Default Local Group";
                AddressableAssetGroup group = addressableSettings.FindGroup(groupName);
                if (group == null) {
                    group = addressableSettings.CreateGroup(groupName, false, false, false, null, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
                }
                entry = addressableSettings.CreateOrMoveEntry(guid, group);
                dirty = true;
            }
            else {
                // dont move group
                //if (entry.parentGroup != group) {
                //    addressableSettings.MoveEntry(entry, group);
                //}
            }

            if (entry.address != assetPath) {
                entry.address = assetPath;
                dirty = true;
            }

            if (dirty) {
                addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            }
            return dirty;
        }

    }

}
#endif