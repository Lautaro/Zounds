using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zounds {

    public class ZoundsWindow : EditorWindow, IHasCustomMenu {

        private static ZoundsWindow instance;

        [MenuItem("Tools/Zounds")]
        public static void OpenWindow() {
            var window = GetWindow<ZoundsWindow>();
            window.Show();
        }

        private SerializedObject projectSO;
        private TabViewIMGUI mainTabView;

        private PlayModeStateChange editorState;

        private void OnEnable() {
            instance = this;
            wantsMouseMove = true;
            autoRepaintOnSceneChange = true;
#if UNITY_2020_1_OR_NEWER
            Undo.undoRedoPerformed += PerformUndoRedo;
#endif

            titleContent.text = "Zounds";
            minSize = new Vector2(414f, 151f);
            var zoundsProject = ZoundsProject.Instance;
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

        private void OnDisable() {
            EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
#if UNITY_2020_1_OR_NEWER
            Undo.undoRedoPerformed -= PerformUndoRedo;
#endif
        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange stateChange) {
            editorState = stateChange;
            if (editorState == PlayModeStateChange.EnteredEditMode || editorState == PlayModeStateChange.EnteredPlayMode) {
                Repaint();
            }
        }

        private void OnGUI() {
            if (editorState == PlayModeStateChange.ExitingEditMode || editorState == PlayModeStateChange.ExitingPlayMode) {
                return;
            }
            projectSO.Update();
            
            int selectedMainTab = ZoundsWindowProperties.Instance.selectedMainTab;
            int tempMainTab = mainTabView.DrawLayout(selectedMainTab, projectSO, new Rect(Vector2.zero, position.size));
            if (tempMainTab != selectedMainTab) {
                // make selected main tab undo-able
                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected main tab");
                ZoundsWindowProperties.Instance.selectedMainTab = tempMainTab;
                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
            }

            HandleEvents(Event.current);
            projectSO.ApplyModifiedProperties();
        }

        private void HandleEvents(Event _event) {
#if !UNITY_2020_1_OR_NEWER
            if (_event.type == EventType.ValidateCommand) {
                if (_event.commandName == "UndoRedoPerformed") {
                    PerformUndoRedo();
                }
            }
#endif
        }

        private void PerformUndoRedo() {
            ZoundsWindowProperties.DirtyAll();
            // repaint immediately when user undo/redo to make experience feels more fluid
            Repaint();
        }

        public static void RepaintWindow() {
            if (instance != null) {
                instance.Repaint();
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

    }

}
