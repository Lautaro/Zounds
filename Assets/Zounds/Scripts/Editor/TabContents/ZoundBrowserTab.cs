using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class ZoundBrowserTab : TabContent {

        public override string name => "Browser";

        private TabViewIMGUI zoundTabView;
        private Vector2 viewPresetsScrollPos;

        private string lastSelectedPresetName;
        private ZoundsEditorPresets.ViewPreset viewPresetToRename;
        private GUIContent tempGUIContent = new GUIContent();

        #region LABELS
        private GUIContent label_showVolume = new GUIContent("V", "Toggle volume visibility.");
        private GUIContent label_showPitch = new GUIContent("P", "Toggle pitch visibility.");
        private GUIContent label_showChance = new GUIContent("C", "Toggle chance visibility.");
        private GUIContent label_itemWidth = new GUIContent("Width", "Width of each element.");
        private GUIContent label_showNameField = new GUIContent("Name", "Toggle name editor field visibility.");
        private GUIContent label_showTags = new GUIContent("Tags", "Toggle tags visibility.");
        private GUIContent label_killOnPlay = new GUIContent("Kill On Play", "When previewing a zound, should current playing zounds be killed?");
        private GUIContent label_msOnly = new GUIContent("M/S Only", "Only show either muted or solo zounds.");
        #endregion LABELS

        public ZoundBrowserTab() {
            zoundTabView = new TabViewIMGUI(new TabContent[] {
                //new KlipsTab(),
                //new ZequencesTab(),
                //new MuzicsTab(),
                new ConsolidatedTab()
            });
        }

        public void RefreshFilters() {
            //zoundTabView.GetTab<KlipsTab>(0).filterCache = null;
            //zoundTabView.GetTab<ZequencesTab>(1).filterCache = null;
            //zoundTabView.GetTab<MuzicsTab>(2).filterCache = null;
            zoundTabView.GetTab<ConsolidatedTab>(0).filterCache = null;
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            SerializedProperty browserSettings = serializedObject.FindProperty("browserSettings");
            SerializedProperty showVolume = browserSettings.FindPropertyRelative("showVolume");
            SerializedProperty showPitch = browserSettings.FindPropertyRelative("showPitch");
            SerializedProperty showChance = browserSettings.FindPropertyRelative("showChance");
            SerializedProperty itemWidth = browserSettings.FindPropertyRelative("itemWidth");
            SerializedProperty showNameField = browserSettings.FindPropertyRelative("showNameField");
            SerializedProperty showTags = browserSettings.FindPropertyRelative("showTags");
            SerializedProperty killOnPlay = browserSettings.FindPropertyRelative("killOnPlay");
            SerializedProperty msOnly = browserSettings.FindPropertyRelative("msOnly");


            float totalPresetsWidth = 0f;
            tempGUIContent.text = "Default";
            float width = EditorStyles.helpBox.CalcSize(tempGUIContent).x;
            totalPresetsWidth += width;
            foreach (var viewPreset in ZoundsEditorPresets.Instance.viewPresets) {
                tempGUIContent.text = viewPreset.name;
                width = EditorStyles.toolbarButton.CalcSize(tempGUIContent).x;
                totalPresetsWidth += width;
            }

            float presetsHeight = 
                totalPresetsWidth > (contentRect.width - PresetsBarDrawer.presetsLabelWidth - PresetsBarDrawer.savePresetButtonWidth - 4f) ? 
                32f : 20f;


            float topMargin = ZoundsProject.useJSON ? 43f : 27f;
            float sideMargin = 5f;
            float settingsHeight = 28f + presetsHeight + 4f;
            var settingsRect = new Rect(sideMargin, topMargin, contentRect.size.x - 2f * sideMargin, settingsHeight);
            // draw browser settings background
            EditorGUI.HelpBox(settingsRect, null, MessageType.None);
            // add padding
            settingsRect.x += 4f;
            settingsRect.y += 4f;
            settingsRect.width -= 8f;
            settingsRect.height -= 8f;
            GUILayout.BeginArea(settingsRect);


            var presetsRect = GUILayoutUtility.GetRect(1f, presetsHeight, GUILayout.ExpandWidth(true));
            viewPresetsScrollPos = PresetsBarDrawer.DrawPresets(
                viewPresetsScrollPos, presetsRect, ZoundsEditorPresets.Instance.viewPresets, totalPresetsWidth, lastSelectedPresetName, ClearPresetToRename, SavePreset, HandlePresetClick);

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
                var guiColor = GUI.color;
                if (msOnly.boolValue) {
                    GUI.color = new Color(0f, 1f, 0.6f, 1f);
                }
                if (GUILayout.Button(label_msOnly, EditorStyles.miniButton, GUILayout.MaxWidth(65f))) {
                    //msOnly.boolValue = !msOnly.boolValue;
                    // use direct method because we need to use this toggle status upon refreshing filters
                    ZoundsWindow.ModifyZoundsProject("toggle MS only", () => {
                        var browserSettings = ZoundsProject.Instance.browserSettings;
                        browserSettings.msOnly = !browserSettings.msOnly;
                        RefreshFilters();
                    });
                }
                GUI.color = guiColor;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float yOffset = topMargin + settingsHeight + 8f;
            var contentSize = contentRect.size;
            var zoundRect = new Rect(2f * sideMargin, yOffset, contentSize.x - 4f * sideMargin, contentSize.y - yOffset - 10f);

            //float tabSelectionOffset = 18f;
            float tabSelectionOffset = 0f;

            // draw zound browser background
            EditorGUI.HelpBox(new Rect(zoundRect.x, zoundRect.y + tabSelectionOffset, zoundRect.width, zoundRect.height - tabSelectionOffset + 2f), null, MessageType.None);

            // draw header background
            GUI.Box(new Rect(zoundRect.x, zoundRect.y + tabSelectionOffset, zoundRect.width, 54f), GUIContent.none);

            GUILayout.BeginArea(zoundRect);
            {
                //int selectedZoundTab = ZoundsWindowProperties.Instance.selectedZoundTab;
                //int tempZoundTab = zoundTabView.DrawLayout(selectedZoundTab, serializedObject, zoundRect);
                //if (tempZoundTab != selectedZoundTab) {
                //    // make selected zound tab undo-able
                //    Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected zound tab");
                //    ZoundsWindowProperties.Instance.selectedZoundTab = tempZoundTab;
                //    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                //}
                zoundTabView.DrawLayout(0, serializedObject, zoundRect);
            }
            GUILayout.EndArea();
        }

        private void ClearPresetToRename() {
            viewPresetToRename = null;
        }

        private void SavePreset(string presetName) {
            var zoundsPresets = ZoundsEditorPresets.Instance;
            Undo.RecordObject(zoundsPresets, "save preset");
            ZoundsEditorPresets.ViewPreset preset;
            if (viewPresetToRename == null) {
                preset = zoundsPresets.viewPresets.Find(p => p.name == presetName);
                if (preset == null) {
                    preset = new ZoundsEditorPresets.ViewPreset() {
                        name = presetName
                    };
                    zoundsPresets.viewPresets.Add(preset);
                }
            }
            else {
                preset = viewPresetToRename;
                preset.name = presetName;
                viewPresetToRename = null;
            }

            preset.SetFromCurrentSettings();

            lastSelectedPresetName = preset.name;
            EditorUtility.SetDirty(zoundsPresets);
        }

        private void HandlePresetClick(string presetName) {
            var evt = Event.current;
            var mousePosInScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
            var zoundsPresets = ZoundsEditorPresets.Instance;
            var preset = zoundsPresets.viewPresets.Find(p => p.name == presetName);

            if (evt.button == 0) {
                if (preset == null) {
                    zoundsPresets.ApplyDefaultView();
                }
                else {
                    preset.Apply();
                    lastSelectedPresetName = presetName;
                }
                GUI.FocusControl(null);
            }
            else if (evt.button == 1) {
                if (preset != null) {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Rename"), false, () => {
                        if (preset != null) {
                            viewPresetToRename = preset;
                            SavePresetPopup.Show(GUIUtility.ScreenToGUIPoint(mousePosInScreen), presetName, SavePreset);
                        }
                    });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Replace with Current View"), false, () => {
                        SavePreset(presetName);
                    });
                    menu.AddItem(new GUIContent("Delete"), false, () => {
                        if (EditorUtility.DisplayDialog("Remove Preset: " + presetName, "Are you sure you want to remove this preset?\n" + presetName, "Remove", "Cancel")) {
                            var zoundsPresets = ZoundsEditorPresets.Instance;
                            Undo.RecordObject(zoundsPresets, "delete preset");
                            zoundsPresets.viewPresets.Remove(preset);
                            EditorUtility.SetDirty(zoundsPresets);
                        }
                    });
                    menu.ShowAsContext();
                }
            }
        }
        
    }

}
