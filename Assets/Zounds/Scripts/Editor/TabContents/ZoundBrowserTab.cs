using System;
using UnityEditor;
using UnityEngine;

namespace Zounds
{

    public class ZoundBrowserTab : TabContent
    {
        public override string name { get; set; } = "Browser";

        private TabViewIMGUI zoundTabView;
        private Vector2 viewPresetsScrollPos;
        private string lastSelectedPresetName;
        private ZoundsEditorPresets.ViewPreset viewPresetToRename;
        private GUIContent tempGUIContent = new GUIContent();
        private bool showSettings = false;
        private int verticalSpace = 10;
        #region LABELS
        private GUIContent label_showVolume = new GUIContent("V", "Toggle volume visibility.");
        private GUIContent label_showPitch = new GUIContent("P", "Toggle pitch visibility.");
        private GUIContent label_showChance = new GUIContent("C", "Toggle chance visibility.");
        private GUIContent label_itemWidth = new GUIContent("Width", "Width of each element.");
        private GUIContent label_showNameField = new GUIContent("Name", "Toggle name editor field visibility.");
        private GUIContent label_showTags = new GUIContent("Tags", "Toggle tags visibility.");
        private GUIContent label_showMute = new GUIContent("Mute", "Toggle mute visibility.");
        private GUIContent label_showSolo = new GUIContent("Solo", "Toggle solo visibility.");
        private GUIContent icon_openEditor;
        private GUIContent icon_routingOff;
        private GUIContent icon_duplicate;
        private GUIContent icon_remove;
        private GUIContent icon_settings;
        private GUIContent label_killOnPlay = new GUIContent("Kill On Play", "When previewing a zound, should current playing zounds be killed?");
        private GUIContent label_msOnly = new GUIContent("M/S Only", "Only show either muted or solo zounds.");

        // Quick Controls Customization labels
        private GUIContent label_showAddZound = new GUIContent("Add", "Toggle 'Add Zound/Klip' visibility.");
        private GUIContent label_showStopAll = new GUIContent("Stop", "Toggle 'Stop All' visibility.");
        private GUIContent label_showMSClean = new GUIContent("MS Clear", "Toggle 'MS Clear' visibility.");
        private GUIContent label_showMuteSel = new GUIContent("Mute Sel", "Toggle 'Mute Selection' visibility.");
        private GUIContent label_showSoloSel = new GUIContent("Solo Sel", "Toggle 'Solo Selection' visibility.");
        private GUIContent label_showMasterVolume = new GUIContent("Master Vol", "Toggle 'Master Volume' visibility.");
        private GUIContent label_showSearch = new GUIContent("Search", "Toggle 'Search Filter' visibility.");
        private GUIContent label_showTypes = new GUIContent("Types", "Toggle 'Types Filter' visibility.");
        private GUIContent label_typesInlineToggle = new GUIContent("Inline Types", "Show type toggles inline instead of a dropdown menu.");
        private GUIContent label_showTagsFilter = new GUIContent("Tags Filter", "Toggle 'Tags Filter' visibility.");
        private GUIContent label_showGroupBy = new GUIContent("Group By", "Toggle 'Group By' visibility.");
        private GUIContent label_showColumnMode = new GUIContent("Layout", "Toggle 'Multi/Single-column' visibility.");
        private GUIContent label_showReferences = new GUIContent("Refs", "Toggle 'References' visibility.");
        private GUIContent label_showPresetsAlways = new GUIContent("Presets Always", "Should presets be shown even when settings are closed?");
        private GUIContent label_buttonSizeMode = new GUIContent("Button Size Mode", "Choose how zound buttons calculate their width.");
        #endregion LABELS

        public ZoundBrowserTab()
        {
            zoundTabView = new TabViewIMGUI(new TabContent[] {
                new ConsolidatedTab()
            });

            icon_openEditor = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/open-editor"), "Toggle open editor button visibility.");
            icon_routingOff = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/routing-off"), "Toggle manual routing button visibility.");
            icon_duplicate = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/duplicate"), "Toggle duplication button visibility.");
            icon_remove = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/remove"), "Toggle remove button visibility.");
            icon_settings = new GUIContent(EditorGUIUtility.IconContent("SettingsIcon").image, "Toggle browser settings.");
        }

        public void RefreshFilters()
        {
            zoundTabView.GetTab<ConsolidatedTab>(0).filterCache = null;
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect)
        {
            SerializedProperty browserSettings = serializedObject.FindProperty("browserSettings");

            // Display Options (Item Drawing)
            SerializedProperty showVolume = browserSettings.FindPropertyRelative("showVolume");
            SerializedProperty showPitch = browserSettings.FindPropertyRelative("showPitch");
            SerializedProperty showChance = browserSettings.FindPropertyRelative("showChance");
            SerializedProperty showNameField = browserSettings.FindPropertyRelative("showNameField");
            SerializedProperty showTags = browserSettings.FindPropertyRelative("showTags");
            SerializedProperty showMute = browserSettings.FindPropertyRelative("showMute");
            SerializedProperty showSolo = browserSettings.FindPropertyRelative("showSolo");
            SerializedProperty showOpenEditor = browserSettings.FindPropertyRelative("showOpenEditor");
            SerializedProperty showConvertToZequence = browserSettings.FindPropertyRelative("showConvertToZequence");
            SerializedProperty showRouting = browserSettings.FindPropertyRelative("showRouting");
            SerializedProperty showDuplicate = browserSettings.FindPropertyRelative("showDuplicate");
            SerializedProperty showRemove = browserSettings.FindPropertyRelative("showRemove");

            // Global Settings
            SerializedProperty itemWidth = browserSettings.FindPropertyRelative("itemWidth");
            SerializedProperty killOnPlay = browserSettings.FindPropertyRelative("killOnPlay");
            SerializedProperty msOnly = browserSettings.FindPropertyRelative("msOnly");

            // Quick Controls visibility
            SerializedProperty showAddZound = browserSettings.FindPropertyRelative("showAddZound");
            SerializedProperty showStopAll = browserSettings.FindPropertyRelative("showStopAll");
            SerializedProperty showMSClean = browserSettings.FindPropertyRelative("showMSClean");
            SerializedProperty showMuteSel = browserSettings.FindPropertyRelative("showMuteSel");
            SerializedProperty showSoloSel = browserSettings.FindPropertyRelative("showSoloSel");
            SerializedProperty showMasterVolume = browserSettings.FindPropertyRelative("showMasterVolume");
            SerializedProperty showSearch = browserSettings.FindPropertyRelative("showSearch");
            SerializedProperty showTypes = browserSettings.FindPropertyRelative("showTypes");
            SerializedProperty typesInlineToggle = browserSettings.FindPropertyRelative("typesInlineToggle");
            SerializedProperty showTagsFilter = browserSettings.FindPropertyRelative("showTagsFilter");
            SerializedProperty showGroupBy = browserSettings.FindPropertyRelative("showGroupBy");
            SerializedProperty showColumnMode = browserSettings.FindPropertyRelative("showColumnMode");
            SerializedProperty showReferences = browserSettings.FindPropertyRelative("showReferences");
            SerializedProperty showPresetsAlways = browserSettings.FindPropertyRelative("showPresetsAlways");
            SerializedProperty buttonSizeMode = browserSettings.FindPropertyRelative("buttonSizeMode");
            SerializedProperty highQualityWaveform = browserSettings.FindPropertyRelative("highQualityWaveform");

            float totalPresetsWidth = 0f;
            tempGUIContent.text = "Default";
            float width = EditorStyles.helpBox.CalcSize(tempGUIContent).x;
            totalPresetsWidth += width;
            foreach (var viewPreset in ZoundsEditorPresets.Instance.viewPresets)
            {
                tempGUIContent.text = viewPreset.name;
                width = EditorStyles.toolbarButton.CalcSize(tempGUIContent).x;
                totalPresetsWidth += width;
            }

            float presetsHeight =
                totalPresetsWidth > (contentRect.width - PresetsBarDrawer.presetsLabelWidth - PresetsBarDrawer.savePresetButtonWidth - 4f) ?
                32f : 20f;

            string fileName = ZoundsWindow.Instance.projectJSONAsset != null
                ? ZoundsWindow.Instance.projectJSONAsset.name
                : "No Project Loaded";

            using (ZUI.Box())
            {
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();
                        ZUI.Label(fileName, ZUI.ZTextStyle.Title);
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(icon_settings, EditorStyles.label, GUILayout.Width(18f), GUILayout.Height(18f)))
                        {
                            showSettings = !showSettings;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    var presetsRect = GUILayoutUtility.GetRect(1f, presetsHeight, GUILayout.ExpandWidth(true));
                    viewPresetsScrollPos = PresetsBarDrawer.DrawPresets(
                        viewPresetsScrollPos, presetsRect, ZoundsEditorPresets.Instance.viewPresets, totalPresetsWidth, lastSelectedPresetName, ClearPresetToRename, SavePreset, HandlePresetClick);

                    GUILayout.Space(verticalSpace * 2);
                    if (showSettings)
                    {
                            ZoundsWindow.Instance.DrawJSONProjectField();
                            GUILayout.Space(verticalSpace);

                            var prevLabelWidth = EditorGUIUtility.labelWidth;

                            // Display Options — ZToggle buttons for compact boolean row
                            DrawSectionHeader("Display Options");
                            GUILayout.BeginHorizontal();
                            {
                                DrawSettingToggle(showVolume,     "Vol",   ZUICornerMask.Left);
                                GUILayout.Space(3f);
                                DrawSettingToggle(showPitch,      "Pit");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showChance,     "Cha");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showNameField,  "Name");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showTags,       "Tags");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showMute,       "Mute");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showSolo,       "Solo");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showOpenEditor, "Edit");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showConvertToZequence, "Conv");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showRouting,    "Route");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showDuplicate,  "Dup");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showRemove,     "Del",   ZUICornerMask.Right);
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(verticalSpace);

                            // Global Settings
                            DrawSectionHeader("Global Settings");
                            GUILayout.BeginHorizontal();
                            {
                                EditorGUIUtility.labelWidth = 110f;
                                EditorGUILayout.PropertyField(buttonSizeMode, label_buttonSizeMode, GUILayout.MaxWidth(200f));
                                GUILayout.Space(10f);

                                if (ZoundsProject.Instance.browserSettings.buttonSizeMode != ZoundsProject.BrowserSettings.ButtonSizeMode.Auto)
                                {
                                    EditorGUIUtility.labelWidth = 45f;
                                    EditorGUILayout.Slider(itemWidth, 38f, 800f, label_itemWidth, GUILayout.MaxWidth(200f));
                                    GUILayout.Space(10f);
                                }

                                DrawSettingToggle(killOnPlay, "Kill On Play");
                                GUILayout.Space(10f);

                                bool newMsOnly = ZUI.Toggle(msOnly.boolValue, label_msOnly, ZUI.Style.RichToggle, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
                                if (newMsOnly != msOnly.boolValue)
                                {
                                    ZoundsWindow.ModifyZoundsProject("toggle MS only", () =>
                                    {
                                        ZoundsProject.Instance.browserSettings.msOnly = newMsOnly;
                                        RefreshFilters();
                                    });
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(verticalSpace);

                            // Quick Controls Customization
                            DrawSectionHeader("Quick Controls Customization");
                            GUILayout.BeginHorizontal();
                            {
                                DrawSettingToggle(showAddZound,     "Add",        ZUICornerMask.Left);
                                GUILayout.Space(3f);
                                DrawSettingToggle(showStopAll,      "Stop");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showMSClean,      "MS Clr");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showMuteSel,      "Mute Sel");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showSoloSel,      "Solo Sel");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showMasterVolume, "Master Vol");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showPresetsAlways,"Presets");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showSearch,       "Search",     ZUICornerMask.Right);
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(3f);
                            GUILayout.BeginHorizontal();
                            {
                                DrawSettingToggle(showTypes,        "Types",      ZUICornerMask.Left);
                                GUILayout.Space(3f);
                                DrawSettingToggle(typesInlineToggle,"Inline");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showTagsFilter,   "Tags Filter");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showReferences,   "Refs");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showGroupBy,      "Group By");
                                GUILayout.Space(3f);
                                DrawSettingToggle(showColumnMode,   "Layout",     ZUICornerMask.Right);
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(verticalSpace);

                            // Waveform Quality
                            DrawSectionHeader("Waveform");
                            GUILayout.BeginHorizontal();
                            {
                                bool prevHQ = highQualityWaveform.boolValue;
                                DrawSettingToggle(highQualityWaveform, "HQ Wave");
                                if (highQualityWaveform.boolValue != prevHQ)
                                    AudioWaveformUtility.ClearCache();
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(verticalSpace);
                            EditorGUIUtility.labelWidth = prevLabelWidth;
                    }
            }

            // Zound browser flows directly below the header box.
            zoundTabView.DrawLayout(0, serializedObject, contentRect);
        }

        private void DrawSectionHeader(string label)
        {
            GUILayout.Space(5f);
            ZUI.Label(label, ZUI.ZTextStyle.Subheader);
            GUILayout.Space(2f);
        }

        private static void DrawSettingToggle(SerializedProperty prop, string label, ZUICornerMask cornerMask = ZUICornerMask.None)
        {
            bool newVal = ZUI.Toggle(prop.boolValue, label, ZUI.Style.RichToggle, cornerMask, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(90f));
            if (newVal != prop.boolValue)
                prop.boolValue = newVal;
        }


        private void ClearPresetToRename()
        {
            viewPresetToRename = null;
        }

        private void SavePreset(string presetName)
        {
            var zoundsPresets = ZoundsEditorPresets.Instance;
            Undo.RecordObject(zoundsPresets, "save preset");
            ZoundsEditorPresets.ViewPreset preset;
            if (viewPresetToRename == null)
            {
                preset = zoundsPresets.viewPresets.Find(p => p.name == presetName);
                if (preset == null)
                {
                    preset = new ZoundsEditorPresets.ViewPreset()
                    {
                        name = presetName
                    };
                    zoundsPresets.viewPresets.Add(preset);
                }
            }
            else
            {
                preset = viewPresetToRename;
                preset.name = presetName;
                viewPresetToRename = null;
            }

            preset.SetFromCurrentSettings();

            lastSelectedPresetName = preset.name;
            EditorUtility.SetDirty(zoundsPresets);
        }

        private void HandlePresetClick(string presetName)
        {
            var evt = Event.current;
            var mousePosInScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
            var zoundsPresets = ZoundsEditorPresets.Instance;
            var preset = zoundsPresets.viewPresets.Find(p => p.name == presetName);

            if (evt.button == 0)
            {
                if (preset == null)
                {
                    zoundsPresets.ApplyDefaultView();
                }
                else
                {
                    preset.Apply();
                    lastSelectedPresetName = presetName;
                }
                GUI.FocusControl(null);
            }
            else if (evt.button == 1)
            {
                if (preset != null)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Rename"), false, () =>
                    {
                        if (preset != null)
                        {
                            viewPresetToRename = preset;
                            SavePresetPopup.Show(GUIUtility.ScreenToGUIPoint(mousePosInScreen), presetName, SavePreset);
                        }
                    });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Replace with Current View"), false, () =>
                    {
                        SavePreset(presetName);
                    });
                    menu.AddItem(new GUIContent("Delete"), false, () =>
                    {
                        if (EditorUtility.DisplayDialog("Remove Preset: " + presetName, "Are you sure you want to remove this preset?\n" + presetName, "Remove", "Cancel"))
                        {
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
