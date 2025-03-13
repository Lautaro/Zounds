using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Zounds {

    public class ZoundsWindow : EditorWindow, IHasCustomMenu {

        [MenuItem("Tools/Zounds")]
        public static void OpenWindow() {
            var window = GetWindow<ZoundsWindow>();
            window.Show();
        }

        private SerializedObject projectSO;
        private TabViewIMGUI mainTabView;

        private void OnEnable() {
            titleContent.text = "Zounds";
            minSize = new Vector2(414f, 151f);
            projectSO = new SerializedObject(ZoundsProject.Instance);

            mainTabView = new TabViewIMGUI(new TabContent[] {
                new ZoundBrowserTab(),
                new TagBrowserTab(),
                new RoutingTab(),
                new ProjectSettingsTab(),
            });
        }

        private void OnGUI() {
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
            if (_event.type == EventType.ValidateCommand) {
                if (_event.commandName == "UndoRedoPerformed") {
                    // repaint immediately when user undo/redo to make experience feels more fluid
                    Repaint();
                }
            }
        }

        public static void RepaintWindow() {
            GetWindow<ZoundsWindow>().Repaint();
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

    }

}
