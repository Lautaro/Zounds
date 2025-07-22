using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class ZoundBrowserTab : TabContent {

        public override string name => "Browser";

        private TabViewIMGUI zoundTabView;

        #region LABELS
        private GUIContent label_showVolume = new GUIContent("V", "Toggle volume visibility.");
        private GUIContent label_showPitch = new GUIContent("P", "Toggle pitch visibility.");
        private GUIContent label_showChance = new GUIContent("C", "Toggle chance visibility.");
        private GUIContent label_itemWidth = new GUIContent("Width", "Width of each element.");
        private GUIContent label_showNameField = new GUIContent("Name", "Toggle name editor field visibility.");
        private GUIContent label_showTags = new GUIContent("Tags", "Toggle tags visibility.");
        private GUIContent label_killOnPlay = new GUIContent("Kill On Play", "When previewing a zound, should current playing zounds be killed?");
        #endregion LABELS

        public ZoundBrowserTab() {
            zoundTabView = new TabViewIMGUI(new TabContent[] {
                new KlipsTab(),
                new ZequencesTab(),
                new MuzicsTab(),
            });
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            SerializedProperty browserSettings = serializedObject.FindProperty("browserSettings");
            SerializedProperty showVolume       = browserSettings.FindPropertyRelative("showVolume");
            SerializedProperty showPitch        = browserSettings.FindPropertyRelative("showPitch");
            SerializedProperty showChance       = browserSettings.FindPropertyRelative("showChance");
            SerializedProperty itemWidth        = browserSettings.FindPropertyRelative("itemWidth");
            SerializedProperty showNameField    = browserSettings.FindPropertyRelative("showNameField");
            SerializedProperty showTags         = browserSettings.FindPropertyRelative("showTags");
            SerializedProperty killOnPlay       = browserSettings.FindPropertyRelative("killOnPlay");

            float topMargin = ZoundsProject.useJSON? 43f : 27f;
            float sideMargin = 5f;
            float settingsHeight = 28f;
            var settingsRect = new Rect(sideMargin, topMargin, contentRect.size.x - 2f*sideMargin, settingsHeight);
            // draw browser settings background
            EditorGUI.HelpBox(settingsRect, null, MessageType.None);
            // add padding
            settingsRect.x += 4f;
            settingsRect.y += 4f;
            settingsRect.width -= 8f;
            settingsRect.height -= 8f;
            GUILayout.BeginArea(settingsRect);
            GUILayout.BeginHorizontal();
            {
                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 12f;
                EditorGUILayout.PropertyField(showVolume, label_showVolume, GUILayout.MaxWidth(34f));
                EditorGUILayout.PropertyField(showPitch, label_showPitch, GUILayout.MaxWidth(34f));
                EditorGUILayout.PropertyField(showChance, label_showChance, GUILayout.MaxWidth(34f));
                EditorGUIUtility.labelWidth = 38f;
                EditorGUILayout.Slider(itemWidth, 38f, 800f, label_itemWidth);
                EditorGUILayout.PropertyField(showNameField, label_showNameField, GUILayout.MaxWidth(55f));
                EditorGUIUtility.labelWidth = 35f;
                EditorGUILayout.PropertyField(showTags, label_showTags, GUILayout.MaxWidth(55f));
                EditorGUIUtility.labelWidth = 70f;
                EditorGUILayout.PropertyField(killOnPlay, label_killOnPlay, GUILayout.MaxWidth(90f));
                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float yOffset = topMargin + settingsHeight + 8f;
            var contentSize = contentRect.size;
            var zoundRect = new Rect(2f*sideMargin, yOffset, contentSize.x - 4f*sideMargin, contentSize.y - yOffset - 10f);

            // draw zound browser background
            EditorGUI.HelpBox(new Rect(zoundRect.x, zoundRect.y + 18f, zoundRect.width, zoundRect.height - 16f), null, MessageType.None);

            // draw header background
            GUI.Box(new Rect(zoundRect.x, zoundRect.y + 18f, zoundRect.width, 54f), GUIContent.none);

            GUILayout.BeginArea(zoundRect);
            {
                int selectedZoundTab = ZoundsWindowProperties.Instance.selectedZoundTab;
                int tempZoundTab = zoundTabView.DrawLayout(selectedZoundTab, serializedObject, zoundRect);
                if (tempZoundTab != selectedZoundTab) {
                    // make selected zound tab undo-able
                    Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected zound tab");
                    ZoundsWindowProperties.Instance.selectedZoundTab = tempZoundTab;
                    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                }
            }
            GUILayout.EndArea();
        }

    }

}
