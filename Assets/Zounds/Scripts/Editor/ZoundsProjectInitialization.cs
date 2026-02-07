using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds
{
    [InitializeOnLoad]
    public static class ZoundsProjectInitialization {
        static ZoundsProjectInitialization() {
#if !ADDRESSABLES_INSTALLED
            Debug.LogError("Zounds Dependency: Addressables package should be installed. Minimum version: 1.18.19");
#endif
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ZoundsFilter.RefreshFolders();

            // disable auto loading to achieve identical editor & build behaviour
            //if (IsPreservedJSONProjectAvailable()) {
            //    RegisterJSONProjectRestorationEvent();
            //}
            //else {
            //    AutoLoadJSONProject();
            //}

            // instead, we load last opened project only when the user do so
            if (IsPreservedJSONProjectAvailable()) {
                AssignLastOpenedProject();
            }
            ZoundEngine.onLoadLastOpenedProject += RestorePreservedJSONProject;
        }

        private static void AutoLoadJSONProject() {
            var zoundsProject = ZoundsProject.Instance;

            if (ZoundsProject.useJSON) {
                string projectJsonPath = GetZoundsProjectPath();
                if (!string.IsNullOrEmpty(projectJsonPath)) {
                    var projectJSONAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(projectJsonPath);
                    if (projectJSONAsset != null) {
                        ReloadJSONProject(projectJSONAsset);
                    }
                }
            }

        }

        private static void ReloadJSONProject(TextAsset projectJSONAsset) {
            if (!ZoundsProject.isJSONLoaded) {
                if (projectJSONAsset != null) {
                    ZoundsProject.LoadFromJSON(projectJSONAsset);
                }
            }
        }

        public static string GetSettingsPath() {
            string assetsPath = Application.dataPath;
            string projectRoot = Path.GetDirectoryName(assetsPath);
            string projectSettingsPath = Path.Combine(projectRoot, "ProjectSettings");
            string targetFile = Path.Combine(projectSettingsPath, "ZoundsProjectPath.txt");

            if (!Directory.Exists(projectSettingsPath)) {
                Directory.CreateDirectory(projectSettingsPath);
            }

            return targetFile;
        }

        public static string GetZoundsProjectPath() {
            string targetFile = GetSettingsPath();
            string content;

            if (!File.Exists(targetFile)) {
                content = "";
                File.WriteAllText(targetFile, content);
            }
            else {
                content = File.ReadAllText(targetFile);
            }

            return content;
        }

        public static void SetZoundsProjectPath(string path) {
            string targetFile = GetSettingsPath();
            File.WriteAllText(targetFile, path);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange) {
            if (stateChange == PlayModeStateChange.ExitingEditMode) {
                ZoundsPreprocessBuild.CopyDefaultZoundsProject();
                PreserveJSONProjectBeforePlaying();
            }
            // Restoration is no longer called in EnteredPlayMode since it's invoked after all Start methods frame
            //else if (stateChange == PlayModeStateChange.EnteredPlayMode) {
            //    RestorePreservedJSONProject();
            //}
        }

        private static void PreserveJSONProjectBeforePlaying() {
            var tempData = ZoundsTempData.Instance;
            if (ZoundsProject.isJSONLoaded) {
                tempData.preservedJSONProject = ZoundsWindow.StringifyToJSON();
            }
            else {
                tempData.preservedJSONProject = null;
            }
            tempData.zoundsProjectDirty = ZoundsWindow.zoundsProjectDirty;
            AssignLastOpenedProject();
        }

        private static void AssignLastOpenedProject() {
            if (!IsPreservedJSONProjectAvailable()) {
                ZoundEngine.editorLastOpenedProject = null;
                return;
            }
            string projectJsonPath = GetZoundsProjectPath();
            if (string.IsNullOrEmpty(projectJsonPath)) {
                ZoundEngine.editorLastOpenedProject = null;
            }
            else {
                var projectJSONAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(projectJsonPath);
                ZoundEngine.editorLastOpenedProject = projectJSONAsset;
            }
        }

        private static void RestorePreservedJSONProject() {
            var tempData = ZoundsTempData.Instance;
            ZoundsProject.LoadFromJSON(tempData.preservedJSONProject);
            tempData.preservedJSONProject = null;
            ZoundsWindow.zoundsProjectDirty = tempData.zoundsProjectDirty;
            tempData.zoundsProjectDirty = false;
        }

        private static bool IsPreservedJSONProjectAvailable() {
            return !string.IsNullOrWhiteSpace(ZoundsTempData.Instance.preservedJSONProject);
        }

    }

}
