using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class ZoundsWindow : EditorWindow, IHasCustomMenu {

        private static ZoundsWindow instance;

        public static string setFocusNextFrame = null;

        [MenuItem("Tools/Zounds")]
        public static void OpenWindow() {
            var window = GetWindow<ZoundsWindow>();
            window.Show();
        }

        private SerializedObject projectSO;
        private TabViewIMGUI mainTabView;

        private PlayModeStateChange editorState;

        [SerializeField] private TextAsset projectJSONAsset;

        private void OnEnable() {
            instance = this;
            wantsMouseMove = true;
            autoRepaintOnSceneChange = true;
            Undo.undoRedoPerformed += PerformUndoRedo;

            var zoundsProject = ZoundsProject.Instance;

            if (ZoundsProject.useJSON) {
                string projectJsonPath = ZoundsProjectInitialization.GetZoundsProjectPath();
                if (!string.IsNullOrEmpty(projectJsonPath)) {
                    projectJSONAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(projectJsonPath);
                    if (projectJSONAsset != null) {
                        ReloadJSONProject();
                    }
                }
            }

            titleContent.text = "Zounds";
            minSize = new Vector2(414f, 151f);
            zoundsProject.zoundLibrary.Validate();
            projectSO = new SerializedObject(zoundsProject);

            mainTabView = new TabViewIMGUI(new TabContent[] {
                new ZoundBrowserTab(),
                new TagBrowserTab(),
                new RoutingTab(),
                new ProjectSettingsTab(),
            });

            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private void ReloadJSONProject(bool forceReload = false) {
            if (!ZoundsProject.isJSONLoaded || forceReload) {
                if (projectJSONAsset != null) {
                    ZoundsProject.LoadFromJSON(projectJSONAsset);
                    if (forceReload) {
                        projectSO = new SerializedObject(ZoundsProject.Instance);
                    }
                }
            }
        }

        private void OnDisable() {
            EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            Undo.undoRedoPerformed -= PerformUndoRedo;
        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange stateChange) {
            editorState = stateChange;
            if (editorState == PlayModeStateChange.EnteredEditMode || editorState == PlayModeStateChange.EnteredPlayMode) {
                Repaint();
            }
        }

        private void OnGUI() {
            if (setFocusNextFrame != null) {
                setFocusNextFrame = null;
                GUI.FocusControl(setFocusNextFrame);
            }
            if (editorState == PlayModeStateChange.ExitingEditMode || editorState == PlayModeStateChange.ExitingPlayMode) {
                return;
            }
            if (projectSO.targetObject == null) {
                //ReloadJSONProject(true);
            }
            projectSO.Update();
            
            DrawJSONProjectField();

            if (!ZoundsProject.useJSON || ZoundsProject.isJSONLoaded) {
                var mainTabViewport = new Rect(new Vector2(0, EditorGUIUtility.singleLineHeight), position.size);
                mainTabViewport.height -= mainTabViewport.y;
                int selectedMainTab = ZoundsWindowProperties.Instance.selectedMainTab;
                int tempMainTab = mainTabView.DrawLayout(selectedMainTab, projectSO, mainTabViewport);
                if (tempMainTab != selectedMainTab) {
                    // make selected main tab undo-able
                    Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected main tab");
                    ZoundsWindowProperties.Instance.selectedMainTab = tempMainTab;
                    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                }
            }
            else {
                GUILayout.FlexibleSpace();
            }

            projectSO.ApplyModifiedProperties();
        }

        private void DrawJSONProjectField() {
            if (!ZoundsProject.useJSON) return;
            GUILayout.BeginHorizontal();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 80f;
            EditorGUI.BeginChangeCheck();
            var newTarget = EditorGUILayout.ObjectField("Project JSON", projectJSONAsset, typeof(TextAsset), false) as TextAsset;
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(this, "change project resource path");
                projectJSONAsset = newTarget;
                string assetPath = AssetDatabase.GetAssetPath(projectJSONAsset);
                ZoundsProjectInitialization.SetZoundsProjectPath(assetPath);
                EditorUtility.SetDirty(this);
            }
            EditorGUIUtility.labelWidth = labelWidth;
            var guiEnabled = GUI.enabled;
            if (GUILayout.Button("Create New", GUILayout.Width(85f))) {
                string uniquePath = AssetDatabase.GenerateUniqueAssetPath("Assets/ZoundsProject.json");
                SaveToJSON(uniquePath, new ZoundsProject.ProjectSerializer());
                ZoundsProject.GenerateDefaultFiles();
                ZoundsProject.LoadFromJSON(projectJSONAsset);
            }
            GUI.enabled = guiEnabled && !ReferenceEquals(projectJSONAsset, null);
            if (GUILayout.Button("Load", GUILayout.Width(60f))) {
                if (projectJSONAsset == null) {
                    EditorUtility.DisplayDialog("Load Zounds Project Failed", "File not found: " + projectJSONAsset, "Close");
                }
                else {
                    ZoundsProject.LoadFromJSON(projectJSONAsset);
                    mainTabView.GetTab<ZoundBrowserTab>(0).RefreshFilters();
                    Repaint();
                }
            }
            if (GUILayout.Button("Save", GUILayout.Width(60f))) {
                SaveToJSON();
            }
            GUI.enabled = guiEnabled;
            GUILayout.EndHorizontal();
        }

        private void PerformUndoRedo() {
            ZoundsAssetPostProcessor.RefreshAudioClipsCache();
            string assetPath;
            if (projectJSONAsset != null) assetPath = AssetDatabase.GetAssetPath(projectJSONAsset);
            else assetPath = "";
            ZoundsProjectInitialization.SetZoundsProjectPath(assetPath);
            ZoundsWindowProperties.DirtyAll();
            // repaint immediately when user undo/redo to make experience feels more fluid
            Repaint();
        }

        public static void RepaintWindow() {
            if (instance != null) {
                var zoundBrowserTab = instance.mainTabView.GetTab<ZoundBrowserTab>(0);
                zoundBrowserTab.RefreshFilters();
                instance.Repaint();
            }
        }

        public static void PingWindow() {
            if (instance == null) {
                OpenWindow();
            }
            else {
                instance.ShowTab();
            }
        }

        // Implemention for IHasCustomMenu to add menu toggle in top right window menu
        public void AddItemsToMenu(GenericMenu menu) {
            menu.AddItem(new GUIContent("Multicolumn Zounds Browser"), ZoundsProject.Instance.browserSettings.multicolumn, ToggleColumnView);
        }

        private void ToggleColumnView() {
            Undo.RecordObject(ZoundsProject.Instance, "toggle column view");
            ZoundsProject.Instance.browserSettings.multicolumn = !ZoundsProject.Instance.browserSettings.multicolumn;
            EditorUtility.SetDirty(ZoundsProject.Instance);
        }

        public static void ModifyZoundsProject(string undoMessage, System.Action action, bool repaintWindow = false) {
            Undo.RecordObject(ZoundsProject.Instance, undoMessage);
            action.Invoke();
            EditorUtility.SetDirty(ZoundsProject.Instance);
            //ClearFocus();
            if (repaintWindow) {
                RepaintWindow();
            }
        }

        private void SaveToJSON() {
            if (projectJSONAsset == null) return;
            string assetPath = AssetDatabase.GetAssetPath(projectJSONAsset);

            var zoundsProject = ZoundsProject.Instance;
            var serializer = new ZoundsProject.ProjectSerializer() {
                browserSettings = zoundsProject.browserSettings,
                projectSettings = zoundsProject.projectSettings,
                zoundLibrary = zoundsProject.zoundLibrary,
                zoundRoutings = zoundsProject.zoundRoutings
            };
            SaveToJSON(assetPath, serializer);
        }

        private void SaveToJSON(string assetPath, ZoundsProject.ProjectSerializer serializer) {
            string fullJSONPath = Path.Combine(
                Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
                assetPath
            );
            string content = JsonUtility.ToJson(serializer, true);
            File.WriteAllText(fullJSONPath, content);
            AssetDatabase.Refresh();
            projectJSONAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }


    }

}
