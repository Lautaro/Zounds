using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    [CustomEditor(typeof(ZoundsProject))]
    public class ZoundsProjectEditor : Editor {

        public override void OnInspectorGUI() {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("Open Zounds Window")) {
                    ZoundsWindow.OpenWindow();
                }
                GUILayout.Space(10f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            // For debuggin purpose.
            // Might need to hide this for production.
            //bool prevGUIEnabled = GUI.enabled;
            //GUI.enabled = false;
            base.OnInspectorGUI();
            //GUI.enabled = prevGUIEnabled;
        }

    }


    [InitializeOnLoad]
    public static class ZoundsProjectInitialization {
        static ZoundsProjectInitialization() {
#if !ADDRESSABLES_INSTALLED
            Debug.LogError("Zounds Dependency: Addressables package should be installed. Minimum version: 1.18.19");
#endif
            ZoundsFilter.RefreshFolders();
            AutoLoadJSONProject();
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

    }

}