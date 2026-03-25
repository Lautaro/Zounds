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
            SerializedProperty showTagsFilter = browserSettings.FindPropertyRelative("showTagsFilter");
            SerializedProperty showGroupBy = browserSettings.FindPropertyRelative("showGroupBy");
            SerializedProperty showColumnMode = browserSettings.FindPropertyRelative("showColumnMode");
            SerializedProperty showReferences = browserSettings.FindPropertyRelative("showReferences");
            SerializedProperty showPresetsAlways = browserSettings.FindPropertyRelative("showPresetsAlways");
            SerializedProperty buttonSizeMode = browserSettings.FindPropertyRelative("buttonSizeMode");

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

            // Title Bar Constants
            const float TITLE_BAR_HEIGHT = 32f;
            const float SIDE_MARGIN = 5f;
            const float TOP_MARGIN = 30f;

            // 1. Calculate dynamic height
            float settingsHeight = 0f;
            if (showSettings)
            {
                // JSON Field + Presets + Labels + Padding + Display Row + Global Row (2) + Customization Rows (2)
                settingsHeight = presetsHeight + 250f;
            }
            else if (ZoundsProject.Instance.browserSettings.showPresetsAlways)
            {
                settingsHeight = presetsHeight + 10f;
            }

            // 2. The Header Area (Always visible)
            float totalHeaderHeight = TITLE_BAR_HEIGHT + settingsHeight;
            var headerRect = new Rect(SIDE_MARGIN, TOP_MARGIN, contentRect.width - 2f * SIDE_MARGIN, totalHeaderHeight);

            Color prev = GUI.color;
            //GUI.color = settingsAreaTint; // tint
            // 3. Draw Title and Cog (Persistent)
            string fileName = ZoundsWindow.Instance.projectJSONAsset != null
                ? ZoundsWindow.Instance.projectJSONAsset.name
                : "No Project Loaded";

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15
            };

            
            GUILayout.BeginArea(headerRect);
            {
                using (ZUI.Box())
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(fileName, titleStyle);
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(icon_settings, EditorStyles.label, GUILayout.Width(18f), GUILayout.Height(18f)))
                        {
                            showSettings = !showSettings;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(6f);


                    // 4. Draw Header Content (Presets/Settings)
                    if (settingsHeight > 0)
                    {
                        var areaRect = headerRect;
                        areaRect.y += TITLE_BAR_HEIGHT;
                        areaRect.height -= TITLE_BAR_HEIGHT;
                        areaRect.x += 4f;
                        areaRect.width -= 8f;


                        var presetsRect = GUILayoutUtility.GetRect(1f, presetsHeight, GUILayout.ExpandWidth(true));
                        viewPresetsScrollPos = PresetsBarDrawer.DrawPresets(
                            viewPresetsScrollPos, presetsRect, ZoundsEditorPresets.Instance.viewPresets, totalPresetsWidth, lastSelectedPresetName, ClearPresetToRename, SavePreset, HandlePresetClick);

                        GUILayout.Space(verticalSpace * 2);
                        //showSettings = true;
                        if (showSettings)
                        {
                            ZoundsWindow.Instance.DrawJSONProjectField();
                            GUILayout.Space(verticalSpace);

                            var prevLabelWidth = EditorGUIUtility.labelWidth;

                            // Display Options
                            DrawSectionHeader("Display Options");
                            GUILayout.BeginHorizontal();
                            {
                                EditorGUIUtility.labelWidth = 12f;
                                EditorGUILayout.PropertyField(showVolume, label_showVolume, GUILayout.MaxWidth(34f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showPitch, label_showPitch, GUILayout.MaxWidth(34f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showChance, label_showChance, GUILayout.MaxWidth(34f));
                                GUILayout.Space(5f);
                                EditorGUIUtility.labelWidth = 38f;
                                EditorGUILayout.PropertyField(showNameField, label_showNameField, GUILayout.MaxWidth(58f));
                                GUILayout.Space(5f);
                                EditorGUIUtility.labelWidth = 35f;
                                EditorGUILayout.PropertyField(showTags, label_showTags, GUILayout.MaxWidth(55f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showMute, label_showMute, GUILayout.MaxWidth(55f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showSolo, label_showSolo, GUILayout.MaxWidth(55f));
                                GUILayout.Space(5f);

                                var prevIconSize = EditorGUIUtility.GetIconSize();
                                EditorGUIUtility.SetIconSize(new Vector2(16f, 16f));
                                EditorGUIUtility.labelWidth = 20f;
                                EditorGUILayout.PropertyField(showOpenEditor, icon_openEditor, GUILayout.MaxWidth(40f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showRouting, icon_routingOff, GUILayout.MaxWidth(40f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showDuplicate, icon_duplicate, GUILayout.MaxWidth(40f));
                                GUILayout.Space(5f);
                                EditorGUILayout.PropertyField(showRemove, icon_remove, GUILayout.MaxWidth(40f));
                                EditorGUIUtility.SetIconSize(prevIconSize);
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

                                EditorGUIUtility.labelWidth = 70f;
                                EditorGUILayout.PropertyField(killOnPlay, label_killOnPlay, GUILayout.MaxWidth(90f));
                                GUILayout.Space(10f);

                                var guiColor = GUI.color;
                                if (msOnly.boolValue) GUI.color = new Color(0f, 1f, 0.6f, 1f);
                                if (GUILayout.Button(label_msOnly, EditorStyles.miniButton, GUILayout.MaxWidth(65f)))
                                {
                                    ZoundsWindow.ModifyZoundsProject("toggle MS only", () =>
                                    {
                                        ZoundsProject.Instance.browserSettings.msOnly = !ZoundsProject.Instance.browserSettings.msOnly;
                                        RefreshFilters();
                                    });
                                }
                                GUI.color = guiColor;
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(verticalSpace);

                            // Quick Controls Customization
                            DrawSectionHeader("Quick Controls Customization");
                            GUILayout.BeginHorizontal();
                            {
                                EditorGUIUtility.labelWidth = 28f;
                                EditorGUILayout.PropertyField(showAddZound, label_showAddZound, GUILayout.MaxWidth(50f));
                                EditorGUILayout.PropertyField(showStopAll, label_showStopAll, GUILayout.MaxWidth(55f));
                                EditorGUIUtility.labelWidth = 55f;
                                EditorGUILayout.PropertyField(showMSClean, label_showMSClean, GUILayout.MaxWidth(75f));
                                EditorGUILayout.PropertyField(showMuteSel, label_showMuteSel, GUILayout.MaxWidth(75f));
                                EditorGUILayout.PropertyField(showSoloSel, label_showSoloSel, GUILayout.MaxWidth(75f));
                                EditorGUIUtility.labelWidth = 65f;
                                EditorGUILayout.PropertyField(showMasterVolume, label_showMasterVolume, GUILayout.MaxWidth(85f));
                                EditorGUIUtility.labelWidth = 85f;
                                EditorGUILayout.PropertyField(showPresetsAlways, label_showPresetsAlways, GUILayout.MaxWidth(105f));
                                EditorGUIUtility.labelWidth = 45f;
                                EditorGUILayout.PropertyField(showSearch, label_showSearch, GUILayout.MaxWidth(65f));
                                GUILayout.EndHorizontal();
                                GUILayout.BeginHorizontal();
                                EditorGUIUtility.labelWidth = 40f;
                                EditorGUILayout.PropertyField(showTypes, label_showTypes, GUILayout.MaxWidth(60f));
                                EditorGUIUtility.labelWidth = 65f;
                                EditorGUILayout.PropertyField(showTagsFilter, label_showTagsFilter, GUILayout.MaxWidth(85f));
                                EditorGUIUtility.labelWidth = 28f;
                                EditorGUILayout.PropertyField(showReferences, label_showReferences, GUILayout.MaxWidth(48f));
                                EditorGUIUtility.labelWidth = 60f;
                                EditorGUILayout.PropertyField(showGroupBy, label_showGroupBy, GUILayout.MaxWidth(80f));
                                EditorGUIUtility.labelWidth = 45f;
                                EditorGUILayout.PropertyField(showColumnMode, label_showColumnMode, GUILayout.MaxWidth(65f));
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(verticalSpace);
                            EditorGUIUtility.labelWidth = prevLabelWidth;
                        }
                    }
                }
            }
            GUILayout.EndArea();
          
            float yOffset = headerRect.yMax + 5f;
            var zoundRect = new Rect(SIDE_MARGIN, yOffset, contentRect.width - 2f * SIDE_MARGIN, contentRect.height - yOffset - 10f);


            // draw zound browser background
            EditorGUI.HelpBox(new Rect(zoundRect.x, zoundRect.y, zoundRect.width, zoundRect.height + 2f), null, MessageType.None);

            // draw header background
            GUI.Box(new Rect(zoundRect.x, zoundRect.y, zoundRect.width, 24f), GUIContent.none);


            GUILayout.BeginArea(zoundRect);
            {
                zoundTabView.DrawLayout(0, serializedObject, zoundRect);
            }
            GUILayout.EndArea();
        }

        private void DrawSectionHeader(string label)
        {
            GUILayout.Space(5f);
            var rect = GUILayoutUtility.GetRect(1f, 18f);
            var labelWidth = EditorStyles.miniBoldLabel.CalcSize(new GUIContent(label)).x;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, labelWidth, rect.height), label, EditorStyles.miniBoldLabel);
            var lineRect = new Rect(rect.x + labelWidth + 5f, rect.y + rect.height / 2f, rect.width - labelWidth - 5f, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            GUILayout.Space(2f);
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
