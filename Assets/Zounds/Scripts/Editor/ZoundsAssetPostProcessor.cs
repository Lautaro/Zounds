#if ADDRESSABLES_INSTALLED
using System.Collections.Generic;
using System.Linq;
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

            bool dirty = EnsureUniqueAudioClipNames(addressableSettings, importedAssets, movedAssets);

            var projectSettings = ZoundsProject.Instance.projectSettings;
            List<string> modifiedAssets = null;

            foreach (var assetPath in importedAssets) {
                if (ProcessAsset(projectSettings, addressableSettings, assetPath, true)) {
                    if (modifiedAssets == null) modifiedAssets = new List<string>();
                    modifiedAssets.Add(assetPath);
                    dirty = true;
                }
            }
            foreach (var assetPath in movedAssets) {
                if (ProcessAsset(projectSettings, addressableSettings, assetPath, false)) {
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

        private static bool ProcessAsset(ZoundsProject.ProjectSettings projectSettings, AddressableAssetSettings addressableSettings, string assetPath, bool isNew) {
            if (!assetPath.StartsWith(projectSettings.userFolderPath) &&
                !assetPath.StartsWith(projectSettings.sourceFolderPath) &&
                !assetPath.StartsWith(projectSettings.workFolderPath)) {
                return false;
            }

            if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(AudioClip)) {
                return false;
            }


            if (Application.isPlaying) {
                if (ZoundEngine.IsInitialized()) {
                    var clipAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (isNew) {
                        var clipZound = new ClipZound(clipAsset);
                        ZoundDictionary.ValidateZoundRuntime(clipZound);
                    }
                    else {
                        var clipZound = ZoundDictionary.FindClipZoundByAudioClip(clipAsset);
                        if (clipZound != null) {
                            clipZound.name = clipAsset.name;
                            ZoundDictionary.ValidateZoundRuntime(clipZound);
                        }
                    }
                }
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

        private static bool EnsureUniqueAudioClipNames(AddressableAssetSettings addressableSettings, string[] importedAssets, string[] movedAssets) {
            var assets = new List<AddressableAssetEntry>();
            addressableSettings.GetAllAssets(assets, false);

            var reservedZoundKeys = new HashSet<string>();

            string userFolderPath = ZoundsProject.Instance.projectSettings.userFolderPath;
            foreach (var asset in assets) {
                if (asset.address.StartsWith(userFolderPath)) {
                    if (importedAssets.Contains(asset.address) || movedAssets.Contains(asset.address)) {
                        continue;
                    }
                    string normalizedAddress = asset.address.Replace('\\', '/');
                    string assetName = System.IO.Path.GetFileNameWithoutExtension(normalizedAddress);
                    string zoundKey = ZoundDictionary.ZoundNameToKey(assetName);
                    reservedZoundKeys.Add(zoundKey);
                }
            }

            var allPaths = new List<string>();
            allPaths.AddRange(importedAssets);
            allPaths.AddRange(movedAssets);

            bool dirty = false;
            foreach (var assetPath in allPaths) {
                string normalizedAddress = assetPath.Replace('\\', '/');
                string assetName = System.IO.Path.GetFileNameWithoutExtension(normalizedAddress);
                string zoundKey = ZoundDictionary.ZoundNameToKey(assetName);
                if (reservedZoundKeys.Contains(zoundKey)) {
                    int iterator = 0;
                    string uniqueZoundKey;

                    do {
                        iterator++;
                        uniqueZoundKey = zoundKey + iterator;
                    } while (reservedZoundKeys.Contains(uniqueZoundKey));

                    assetName += " " + iterator;
                    reservedZoundKeys.Add(uniqueZoundKey);
                    dirty = true;
                    var errorMessage = AssetDatabase.RenameAsset(assetPath, assetName);
                    if (!string.IsNullOrEmpty(errorMessage)) {
                        //Debug.LogError(errorMessage + ": " + assetPath);
                    }

                    if (Application.isPlaying) {
                        if (ZoundEngine.IsInitialized()) {
                            if (ZoundDictionary.TryGetZoundByName(zoundKey, out var zound)) {
                                zound.name = assetName;
                                ZoundDictionary.ValidateZoundRuntime(zound);
                            }
                        }
                    }

                }
            }

            return dirty;
        }

    }

}
#endif