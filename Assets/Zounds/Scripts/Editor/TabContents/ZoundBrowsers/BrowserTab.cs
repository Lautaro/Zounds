using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using static Zounds.ZoundsWindowProperties.ZoundTabProperties;
#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace Zounds {

    public class BrowserTab : TabContent {

        // ── Static instance — for external callers (ZoundAPIBridge, RenderZequenceToKlipPopup) ──
        private static BrowserTab instance;
        public static BrowserTab Instance => instance;

        public override string name { get; set; } = "Browser";

        // ── Layout spacing constants ───────────────────────────────────────────
        internal const float inspectorHeight     = 39f;
        internal const float ROW_HEIGHT          = 24f;
        internal const float ROW_BUTTON_WIDTH    = 40f;
        internal const float ROW_VERTICAL_GAP    = 10f;
        internal const float MULTICOLUMN_H_GAP   = 2f;
        internal const float MULTICOLUMN_V_GAP   = 2f;
        internal const float TOOLBAR_BUTTON_GAP  = 10f;
        internal const float ZoundButton_Spacing = 15f;
        internal const float LEFT_BUTTONS_TO_NAME_GAP = ZoundButton_Spacing;
        internal const float NAME_TO_INSPECTOR_GAP    = ZoundButton_Spacing;

        internal static float MUTE_SOLO_GAP {
            get {
                var sheet = ZUI.ActiveSheet;
                return (sheet?.horizontalSpacing ?? 8f) * (sheet?.FindSpacingScale("H Btns Medium") ?? 1f);
            }
        }
        internal static float ZoundItem_spacing {
            get {
                var sheet = ZUI.ActiveSheet;
                return (sheet?.horizontalSpacing ?? 8f) * (sheet?.FindSpacingScale("H Btns Big") ?? 1f);
            }
        }
        internal static float INSPECTOR_TO_REMOVE_GAP => ZoundItem_spacing;

        // ── Browser state ──────────────────────────────────────────────────────
        private Zound selectedZound;
        private Vector2 scrollPos;
        internal ZUI.AnimatedFloat inspectorAnimFloat = new ZUI.AnimatedFloat(0f);
        internal ZoundBrowserEditor<Zound> zoundBrowserEditor;
        private GUIContent zoundButtonContent = new GUIContent();
        private GUIContent tempContent = new GUIContent();

        private Dictionary<Zound, float> _tagRowHeightCache = new Dictionary<Zound, float>();

        private GUIContent icon_addNew;
        private GUIContent[] icon_columns;
        private GUIContent filterLabel = new GUIContent("Filter:");

        private ZoundBrowserFilterEngine filterEngine = new ZoundBrowserFilterEngine();
        internal List<Zound> filterCache {
            get => filterEngine.filterCache;
            set {
                filterEngine.filterCache = value;
                if (value == null) _tagRowHeightCache.Clear();
            }
        }
        private List<KeyValuePair<string, List<Zound>>> groupCache => filterEngine.groupCache;

        protected int zoundTabPropertyIndex => 0;

        protected ZoundsWindowProperties.ZoundTabProperties zoundTabProperties =>
            ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];

        public Zound zoundToRemove { get; set; } = null;
        public Zound zoundToDuplicate { get; set; } = null;

        // ── Settings panel state ───────────────────────────────────────────────
        private bool showSettings = false;
        private int verticalSpace = 10;
        private Vector2 viewPresetsScrollPos;
        private string lastSelectedPresetName;
        private ZoundsEditorPresets.ViewPreset viewPresetToRename;
        private GUIContent tempGUIContent = new GUIContent();

        // ── Settings panel icon labels ──────────────────────────────────────────
        private GUIContent icon_openEditor;
        private GUIContent icon_routingOff;
        private GUIContent icon_duplicate;
        private GUIContent icon_remove;
        private GUIContent icon_settings;
        private GUIContent label_itemWidth       = new GUIContent("Width",       "Width of each element.");
        private GUIContent label_killOnPlay      = new GUIContent("Kill On Play","When previewing a zound, should current playing zounds be killed?");
        private GUIContent label_msOnly          = new GUIContent("M/S Only",    "Only show either muted or solo zounds.");
        private GUIContent label_buttonSizeMode  = new GUIContent("Button Size Mode", "Choose how zound buttons calculate their width.");
        private GUIContent label_showPresetsAlways = new GUIContent("Presets Always", "Should presets be shown even when settings are closed?");

        // ── Add-menu search state ───────────────────────────────────────────────
        private static string addMenuSearchText = "";

        // ── Search / filter state ───────────────────────────────────────────────
        private string tagsSearchText       = "";
        private string referencesSearchText = "";

        // ═══════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════════

        public BrowserTab() {
            instance = this;

            inspectorAnimFloat.SnapTo(0f);
            inspectorAnimFloat.speed = 4f;
            zoundBrowserEditor = new ZoundBrowserEditor<Zound>(this);

            icon_addNew = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/add-new"), "Add new item.");
            icon_columns = new GUIContent[] {
                new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/multicolumn"), "Grid mode"),
                new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/singlecolumn"), "List mode")
            };

            icon_openEditor = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/open-editor"), "Toggle open editor button visibility.");
            icon_routingOff = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/routing-off"), "Toggle manual routing button visibility.");
            icon_duplicate  = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/duplicate"),   "Toggle duplication button visibility.");
            icon_remove     = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/remove"),      "Toggle remove button visibility.");
            icon_settings   = new GUIContent(EditorGUIUtility.IconContent("SettingsIcon").image,        "Toggle browser settings.");
        }

        ~BrowserTab() {
            if (instance == this) instance = null;
        }

        public override void OnTabOpened() {
            GUI.FocusControl(null);
        }

        public void RefreshFilters() {
            filterCache = null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ZOUNDS TO DISPLAY
        // ═══════════════════════════════════════════════════════════════════════

        public List<Zound> zoundsToDisplay {
            get {
                var result = new List<Zound>();
                var zoundsProject  = ZoundsProject.Instance;
                var zoundLibrary   = zoundsProject.zoundLibrary;
                var tabProps       = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
                var selectedTypes  = tabProps.selectedTypes;

                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.Klip)) {
                    result.AddRange(zoundLibrary.klips);
                }
                else {
                    foreach (var kvp in ZoundEngine.CullingGroups)
                        if (kvp.Key is Klip klip && kvp.Value.Count > 0) result.Add(klip);
                }

                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.Zequence)) {
                    result.AddRange(zoundLibrary.zequences);
                }
                else {
                    foreach (var kvp in ZoundEngine.CullingGroups)
                        if (kvp.Key is Zequence zequence && kvp.Value.Count > 0) result.Add(zequence);
                }

                bool audioClipTypeSelected = selectedTypes == ZoundType.None
                    || selectedTypes == ZoundType.Everything
                    || selectedTypes.HasFlag(ZoundType.AudioClip);
                if (audioClipTypeSelected) {
                    if (!zoundsProject.browserSettings.showAudioClips) {
                        zoundsProject.browserSettings.showAudioClips = true;
                        ZoundsAssetPostProcessor.RefreshAudioClipsCache();
                    }
                    var clipZoundsCache = ZoundsAssetPostProcessor.audioClipZoundsCache;
                    if (clipZoundsCache != null) result.AddRange(clipZoundsCache);
                }
                else {
                    foreach (var kvp in ZoundEngine.CullingGroups)
                        if (kvp.Key is ClipZound cz && kvp.Value.Count > 0) result.Add(cz);
                }

                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.Missing)) {
                    foreach (var z in ZoundEngine.MissingZounds.Values) result.Add(z);
                }

                result = result.OrderBy(it => it.name).ToList();
                return result;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ON GUI — settings panel header + browser body
        // ═══════════════════════════════════════════════════════════════════════

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            SerializedProperty browserSettingsProp = serializedObject.FindProperty("browserSettings");

            // ── Settings panel + presets bar ───────────────────────────────────
            DrawSettingsPanel(serializedObject, browserSettingsProp, contentRect);

            // ── Browser body (search, toolbar, list) ───────────────────────────
            DrawBrowserBody(serializedObject, contentRect);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SETTINGS PANEL
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawSettingsPanel(SerializedObject serializedObject, SerializedProperty browserSettings, Rect contentRect) {
            SerializedProperty showVolume    = browserSettings.FindPropertyRelative("showVolume");
            SerializedProperty showPitch     = browserSettings.FindPropertyRelative("showPitch");
            SerializedProperty showChance    = browserSettings.FindPropertyRelative("showChance");
            SerializedProperty showNameField = browserSettings.FindPropertyRelative("showNameField");
            SerializedProperty showTags      = browserSettings.FindPropertyRelative("showTags");
            SerializedProperty showMute      = browserSettings.FindPropertyRelative("showMute");
            SerializedProperty showSolo      = browserSettings.FindPropertyRelative("showSolo");
            SerializedProperty showOpenEditor        = browserSettings.FindPropertyRelative("showOpenEditor");
            SerializedProperty showConvertToZequence = browserSettings.FindPropertyRelative("showConvertToZequence");
            SerializedProperty showRouting   = browserSettings.FindPropertyRelative("showRouting");
            SerializedProperty showDuplicate = browserSettings.FindPropertyRelative("showDuplicate");
            SerializedProperty showRemove    = browserSettings.FindPropertyRelative("showRemove");
            SerializedProperty itemWidth     = browserSettings.FindPropertyRelative("itemWidth");
            SerializedProperty killOnPlay    = browserSettings.FindPropertyRelative("killOnPlay");
            SerializedProperty msOnly        = browserSettings.FindPropertyRelative("msOnly");
            SerializedProperty showAddZound      = browserSettings.FindPropertyRelative("showAddZound");
            SerializedProperty showStopAll       = browserSettings.FindPropertyRelative("showStopAll");
            SerializedProperty showMSClean       = browserSettings.FindPropertyRelative("showMSClean");
            SerializedProperty showMuteSel       = browserSettings.FindPropertyRelative("showMuteSel");
            SerializedProperty showSoloSel       = browserSettings.FindPropertyRelative("showSoloSel");
            SerializedProperty showMasterVolume  = browserSettings.FindPropertyRelative("showMasterVolume");
            SerializedProperty showSearch        = browserSettings.FindPropertyRelative("showSearch");
            SerializedProperty showTypes         = browserSettings.FindPropertyRelative("showTypes");
            SerializedProperty typesInlineToggle = browserSettings.FindPropertyRelative("typesInlineToggle");
            SerializedProperty showTagsFilter    = browserSettings.FindPropertyRelative("showTagsFilter");
            SerializedProperty showGroupBy       = browserSettings.FindPropertyRelative("showGroupBy");
            SerializedProperty showColumnMode    = browserSettings.FindPropertyRelative("showColumnMode");
            SerializedProperty showReferences    = browserSettings.FindPropertyRelative("showReferences");
            SerializedProperty showPresetsAlways = browserSettings.FindPropertyRelative("showPresetsAlways");
            SerializedProperty buttonSizeMode    = browserSettings.FindPropertyRelative("buttonSizeMode");
            SerializedProperty highQualityWaveform = browserSettings.FindPropertyRelative("highQualityWaveform");

            float totalPresetsWidth = 0f;
            tempGUIContent.text = "Default";
            float width = EditorStyles.helpBox.CalcSize(tempGUIContent).x;
            totalPresetsWidth += width;
            foreach (var viewPreset in ZoundsEditorPresets.Instance.viewPresets) {
                tempGUIContent.text = viewPreset.name;
                width = EditorStyles.toolbarButton.CalcSize(tempGUIContent).x;
                totalPresetsWidth += width;
            }

            float presetsHeight = totalPresetsWidth > (contentRect.width - PresetsBarDrawer.presetsLabelWidth - PresetsBarDrawer.savePresetButtonWidth - 4f) ? 32f : 20f;

            string fileName = ZoundsWindow.Instance.projectJSONAsset != null
                ? ZoundsWindow.Instance.projectJSONAsset.name
                : "No Project Loaded";

            using (ZUI.Box()) {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    ZUI.Label(fileName, ZUI.ZTextStyle.Title);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(icon_settings, EditorStyles.label, GUILayout.Width(18f), GUILayout.Height(18f))) {
                        showSettings = !showSettings;
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(verticalSpace);
                if (showSettings) {
                    ZoundsWindow.Instance.DrawJSONProjectField();
                    ZUI.RowSpace();

                    var prevLabelWidth = EditorGUIUtility.labelWidth;

                    DrawSectionHeader("Display Options");
                    GUILayout.BeginHorizontal();
                    {
                        DrawSettingToggle(showVolume,     "Vol",   ZUICornerMask.Left);
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showPitch,      "Pit");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showChance,     "Cha");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showNameField,  "Name");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showTags,       "Tags");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showMute,       "Mute");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showSolo,       "Solo");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showOpenEditor, "Edit");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showConvertToZequence, "Conv");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showRouting,    "Route");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showDuplicate,  "Dup");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showRemove,     "Del",   ZUICornerMask.Right);
                    }
                    GUILayout.EndHorizontal();
                    ZUI.RowSpace();

                    DrawSectionHeader("Global Settings");
                    GUILayout.BeginHorizontal();
                    {
                        EditorGUIUtility.labelWidth = 110f;
                        EditorGUILayout.PropertyField(buttonSizeMode, label_buttonSizeMode, GUILayout.MaxWidth(200f));
                        GUILayout.Space(10f);

                        if (ZoundsProject.Instance.browserSettings.buttonSizeMode != ZoundsProject.BrowserSettings.ButtonSizeMode.Auto) {
                            EditorGUIUtility.labelWidth = 45f;
                            EditorGUILayout.Slider(itemWidth, 38f, 800f, label_itemWidth, GUILayout.MaxWidth(200f));
                            GUILayout.Space(10f);
                        }

                        DrawSettingToggle(killOnPlay, "Kill On Play");
                        GUILayout.Space(10f);

                        bool newMsOnly = ZUI.Toggle(msOnly.boolValue, label_msOnly, ZUI.Style.RichToggle, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
                        if (newMsOnly != msOnly.boolValue) {
                            ZoundsWindow.ModifyZoundsProject("toggle MS only", () => {
                                ZoundsProject.Instance.browserSettings.msOnly = newMsOnly;
                                RefreshFilters();
                            });
                        }
                    }
                    GUILayout.EndHorizontal();
                    ZUI.RowSpace();

                    DrawSectionHeader("Quick Controls Customization");
                    GUILayout.BeginHorizontal();
                    {
                        DrawSettingToggle(showAddZound,     "Add",       ZUICornerMask.Left);
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showStopAll,      "Stop");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showMSClean,      "MS Clr");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showMuteSel,      "Mute Sel");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showSoloSel,      "Solo Sel");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showMasterVolume, "Master Vol");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showSearch,       "Search",    ZUICornerMask.Right);
                    }
                    GUILayout.EndHorizontal();
                    ZUI.RowSpace(0.5f);
                    GUILayout.BeginHorizontal();
                    {
                        DrawSettingToggle(showTypes,        "Types",      ZUICornerMask.Left);
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(typesInlineToggle,"Inline");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showTagsFilter,   "Tags Filter");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showReferences,   "Refs");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showGroupBy,      "Group By");
                        ZUI.HorizontalSpace("H Btns Medium");
                        DrawSettingToggle(showColumnMode,   "Layout",    ZUICornerMask.Right);
                    }
                    GUILayout.EndHorizontal();
                    ZUI.RowSpace();

                    DrawSectionHeader("Waveform");
                    GUILayout.BeginHorizontal();
                    {
                        bool prevHQ = highQualityWaveform.boolValue;
                        DrawSettingToggle(highQualityWaveform, "HQ Wave");
                        if (highQualityWaveform.boolValue != prevHQ)
                            AudioWaveformUtility.ClearCache();
                    }
                    GUILayout.EndHorizontal();
                    ZUI.RowSpace();
                    EditorGUIUtility.labelWidth = prevLabelWidth;
                }
            }

            ZUI.RowSpace();
            var presetsRect = GUILayoutUtility.GetRect(1f, presetsHeight, GUILayout.ExpandWidth(true));
            viewPresetsScrollPos = PresetsBarDrawer.DrawPresets(
                viewPresetsScrollPos, presetsRect, ZoundsEditorPresets.Instance.viewPresets, totalPresetsWidth,
                lastSelectedPresetName, ClearPresetToRename, SavePreset, HandlePresetClick);
        }

        private void DrawSectionHeader(string label) {
            GUILayout.Space(5f);
            ZUI.Label(label, ZUI.ZTextStyle.Subheader);
            GUILayout.Space(2f);
        }

        private static void DrawSettingToggle(SerializedProperty prop, string label, ZUICornerMask cornerMask = ZUICornerMask.None) {
            bool newVal = ZUI.Toggle(prop.boolValue, label, ZUI.Style.RichToggle, cornerMask, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(90f));
            if (newVal != prop.boolValue) prop.boolValue = newVal;
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
                    preset = new ZoundsEditorPresets.ViewPreset() { name = presetName };
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
                if (preset == null) zoundsPresets.ApplyDefaultView();
                else { preset.Apply(); lastSelectedPresetName = presetName; }
                GUI.FocusControl(null);
            }
            else if (evt.button == 1 && preset != null) {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Rename"), false, () => {
                    if (preset != null) {
                        viewPresetToRename = preset;
                        SavePresetPopup.Show(GUIUtility.ScreenToGUIPoint(mousePosInScreen), presetName, SavePreset);
                    }
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Replace with Current View"), false, () => SavePreset(presetName));
                menu.AddItem(new GUIContent("Delete"), false, () => {
                    if (EditorUtility.DisplayDialog("Remove Preset: " + presetName, "Are you sure you want to remove this preset?\n" + presetName, "Remove", "Cancel")) {
                        Undo.RecordObject(zoundsPresets, "delete preset");
                        zoundsPresets.viewPresets.Remove(preset);
                        EditorUtility.SetDirty(zoundsPresets);
                    }
                });
                menu.ShowAsContext();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BROWSER BODY (search bar, toolbar, zound list)
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawBrowserBody(SerializedObject serializedObject, Rect contentRect) {
            SerializedProperty zoundLibrary = serializedObject.FindProperty("zoundLibrary");

            List<Zound> filteredZounds = GetFilteredZounds();
            filteredZounds = EvaluateGroup(filteredZounds);

            var settings = ZoundsProject.Instance.browserSettings;

            const float SECTION1_BREAK_WIDTH = 420f;
            bool section1Wide = contentRect.width >= SECTION1_BREAK_WIDTH;
            bool showBoth     = settings.showSearch && settings.showMasterVolume;
            bool sideBy       = section1Wide || !showBoth;

            ZUI.RowSpace();

            if (sideBy) GUILayout.BeginHorizontal();

            if (settings.showSearch) {
                GUILayout.BeginHorizontal();
                {
                    var labelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 1f;
                    EditorGUI.BeginChangeCheck();
                    {
                        GUI.SetNextControlName("SearchField");
                        var searchStyle = new GUIStyle(EditorStyles.textField);
                        searchStyle.fontSize = 13;
                        var newSearchText = EditorGUILayout.TextField("", zoundTabProperties.searchText, searchStyle, GUILayout.Height(26f));

                        if (string.IsNullOrEmpty(zoundTabProperties.searchText) && GUI.GetNameOfFocusedControl() != "SearchField") {
                            var lastRect = GUILayoutUtility.GetLastRect();
                            var ghostStyle = new GUIStyle(EditorStyles.label);
                            ghostStyle.fontSize = 13;
                            ghostStyle.normal.textColor = Color.gray;
                            ghostStyle.alignment = TextAnchor.MiddleLeft;
                            ghostStyle.padding.left = 5;
                            GUI.Label(lastRect, "Search...", ghostStyle);
                        }

                        if (EditorGUI.EndChangeCheck()) {
                            Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                            zoundTabProperties.searchText = newSearchText;
                            zoundTabProperties.dirty = true;
                            EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                        }
                        EditorGUIUtility.labelWidth = labelWidth;
                        if (GUILayout.Button("X", GUILayout.Width(26f), GUILayout.Height(26f)) && Event.current.button == 0) {
                            Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                            zoundTabProperties.ClearFilters();
                            zoundTabProperties.dirty = true;
                            EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                            GUI.FocusControl(null);
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (showBoth) {
                if (sideBy) GUILayout.Space(8f);
                else        GUILayout.Space(6f);
            }

            if (settings.showMasterVolume) {
                GUILayout.BeginHorizontal();
                {
                    var prevLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 70f;
                    var projectSettings = ZoundsProject.Instance.projectSettings;
                    var masterVol = Application.isPlaying ? projectSettings.playerVolume : projectSettings.editorVolume;
                    string volLabel = string.Format("Vol {0,3}%", Mathf.RoundToInt(masterVol * 100f));
                    EditorGUI.BeginChangeCheck();
                    float volInPercent = masterVol * 100f;
                    volInPercent = ZUI.Slider(volInPercent, 0f, 100f, volLabel, ZUI.SliderStyle.BigSlider, null, GUILayout.Height(26f), GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck()) {
                        masterVol = volInPercent / 100f;
                        Undo.RecordObject(ZoundsProject.Instance, "change master volume");
                        if (Application.isPlaying) projectSettings.playerVolume = masterVol;
                        else projectSettings.editorVolume = masterVol;
                        EditorUtility.SetDirty(ZoundsProject.Instance);
                    }
                    EditorGUIUtility.labelWidth = prevLabelWidth;
                }
                GUILayout.EndHorizontal();
            }

            if (sideBy) GUILayout.EndHorizontal();

            ZUI.RowSpace();

            // ── Quick Controls toolbar ─────────────────────────────────────────
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(5f);
                bool toolbarAny = false;

                bool[] tbVisible = {
                    settings.showAddZound, settings.showStopAll, settings.showMSClean,
                    settings.showMuteSel,  settings.showSoloSel, settings.showTypes,
                    settings.showTagsFilter, settings.showReferences, settings.showGroupBy, settings.showColumnMode
                };
                int tbFirst = System.Array.FindIndex(tbVisible, v => v);
                int tbLast  = System.Array.FindLastIndex(tbVisible, v => v);
                int tbIdx   = -1;
                ZUICornerMask TbMask() {
                    if (tbFirst == tbLast) return ZUICornerMask.All;
                    if (tbIdx == tbFirst)  return ZUICornerMask.Left;
                    if (tbIdx == tbLast)   return ZUICornerMask.Right;
                    return ZUICornerMask.None;
                }

                if (settings.showAddZound) {
                    tbIdx = 0;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button(icon_addNew, ZUI.Style.Confirm, TbMask(), GUILayout.Width(30f), GUILayout.Height(30f)) && Event.current.button == 0) {
                        HandleAddNew();
                        filterCache = null;
                    }
                }

                if (settings.showStopAll) {
                    tbIdx = 1;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Stop All", ZUI.Style.Default, TbMask(), GUILayout.Width(60f), GUILayout.Height(30f))) {
                        ZoundEngine.StopAllZounds();
                    }
                }

                if (settings.showMSClean) {
                    tbIdx = 2;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("MS Clean", ZUI.Style.Default, TbMask(), GUILayout.Width(65f), GUILayout.Height(30f))) {
                        ZoundsWindow.ModifyZoundsProject("clean mute/solo", () => {
                            ZoundsProject.Instance.zoundLibrary.ForEachZound(z => { z.mute = false; z.solo = false; });
                            ZoundsProject.Instance.zoundLibrary.soloStatusNeedsUpdate = true;
                        });
                    }
                }

                if (settings.showMuteSel) {
                    tbIdx = 3;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Mute Sel", ZUI.Style.Default, TbMask(), GUILayout.Width(65f), GUILayout.Height(30f))) {
                        ZoundsWindow.ModifyZoundsProject("mute selected", () => {
                            foreach (var z in filteredZounds)
                                if (z is Klip || z is Zequence) z.mute = true;
                        });
                    }
                }

                if (settings.showSoloSel) {
                    tbIdx = 4;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Solo Sel", ZUI.Style.Default, TbMask(), GUILayout.Width(65f), GUILayout.Height(30f))) {
                        ZoundsWindow.ModifyZoundsProject("solo selected", () => {
                            foreach (var z in filteredZounds)
                                if (z is Klip || z is Zequence) z.solo = true;
                            ZoundsProject.Instance.zoundLibrary.soloStatusNeedsUpdate = true;
                        });
                    }
                }

                if (settings.showTypes) {
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (zoundTabProperties.selectedTypes.HasFlag(ZoundType.Everything))
                        zoundTabProperties.selectedTypes = ZoundType.None;

                    if (settings.typesInlineToggle) {
                        DrawTypesInlineToggle(zoundTabProperties);
                    }
                    else {
                        bool typesActive = zoundTabProperties.selectedTypes != ZoundType.None;
                        tbIdx = 5;
                        if (ZUI.Button("Types", typesActive ? ZUI.Style.Active : ZUI.Style.Default, TbMask(), GUILayout.Height(30f))) {
                            var menu = new GenericMenu();
                            AddTypeMenuItem(menu, zoundTabProperties, ZoundType.Klip);
                            AddTypeMenuItem(menu, zoundTabProperties, ZoundType.Zequence);
                            AddTypeMenuItem(menu, zoundTabProperties, ZoundType.AudioClip);
                            AddTypeMenuItem(menu, zoundTabProperties, ZoundType.Missing);
                            GenericMenuPopup.Show(menu, "Select Types", Event.current.mousePosition, new List<string>(), "", null, null, 3, true, ZoundsEditorPresets.Instance.typesPresets);
                        }
                    }
                }

                if (settings.showTagsFilter) {
                    tbIdx = 6;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    var selectedTags = zoundTabProperties.selectedTags;
                    bool tagsActive = selectedTags.Count > 0;
                    if (ZUI.Button("Tags", tagsActive ? ZUI.Style.Active : ZUI.Style.Default, TbMask(), GUILayout.Height(30f))) {
                        var menu = new GenericMenu();
                        var allTags = ZoundsProject.Instance.zoundLibrary.tags;
                        var addedKeyTags = new HashSet<string>();
                        foreach (var tag in allTags) {
                            string tagName = tag.name;
                            bool on = selectedTags.Contains(tagName);
                            var nameSplit = tagName.Split(':');
                            if (nameSplit.Length > 1) {
                                string keyTag = nameSplit[0];
                                if (!addedKeyTags.Contains(keyTag)) {
                                    addedKeyTags.Add(keyTag);
                                    bool on2 = selectedTags.Contains(keyTag);
                                    menu.AddItem(new GUIContent(keyTag), on2, selected => {
                                        Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected tags");
                                        if ((bool)selected) { if (!selectedTags.Contains(keyTag)) selectedTags.Add(keyTag); }
                                        else selectedTags.RemoveAll(t => t == keyTag);
                                        EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                        zoundTabProperties.dirty = true;
                                    }, on2);
                                }
                            }
                            menu.AddItem(new GUIContent(tagName), on, selected => {
                                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected tags");
                                if ((bool)selected) { if (!selectedTags.Contains(tagName)) selectedTags.Add(tagName); }
                                else selectedTags.RemoveAll(t => t == tagName);
                                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                zoundTabProperties.dirty = true;
                            }, on);
                        }
                        TagMenuPopup.ShowTagMenu(menu, "Select Tags", Event.current.mousePosition, new List<string>(), tagsSearchText, newSearch => tagsSearchText = newSearch, null, 3, true, ZoundsEditorPresets.Instance.tagsPresets);
                    }
                }

                if (settings.showReferences) {
                    tbIdx = 7;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    var selectedReferences = zoundTabProperties.selectedReferences;
                    bool refsActive = selectedReferences.Count > 0;
                    if (ZUI.Button("References", refsActive ? ZUI.Style.Active : ZUI.Style.Default, TbMask(), GUILayout.Height(30f))) {
                        var menu = new GenericMenu();
                        ZoundsProject.Instance.zoundLibrary.ForEachZound(z => {
                            int zoundId = z.id;
                            bool on = selectedReferences.Contains(zoundId);
                            menu.AddItem(new GUIContent(z.name), on, selected => {
                                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected zounds");
                                if ((bool)selected) { if (!selectedReferences.Contains(zoundId)) selectedReferences.Add(zoundId); }
                                else selectedReferences.RemoveAll(id => id == zoundId);
                                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                zoundTabProperties.dirty = true;
                            }, on);
                        });
                        GenericMenuPopup.Show(menu, "Select References", Event.current.mousePosition, new List<string>(), referencesSearchText, newSearch => referencesSearchText = newSearch, null, 3, true, ZoundsEditorPresets.Instance.referencesPresets);
                    }
                }

                if (settings.showGroupBy) {
                    tbIdx = 8;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    GUILayout.BeginHorizontal(GUILayout.Width(100f), GUILayout.Height(30f));
                    {
                        string currentGroupLabel = zoundTabProperties.groupBy == GroupBy.None ? "No Grouping" : zoundTabProperties.groupBy.ToString();
                        bool groupActive = zoundTabProperties.groupBy != GroupBy.None;
                        if (ZUI.Button(currentGroupLabel, groupActive ? ZUI.Style.Active : ZUI.Style.Default, TbMask(), GUILayout.Height(30f))) {
                            var menu = new GenericMenu();
                            foreach (GroupBy groupBy in System.Enum.GetValues(typeof(GroupBy))) {
                                string menuLabel = groupBy == GroupBy.None ? "No Grouping" : groupBy.ToString();
                                menu.AddItem(new GUIContent(menuLabel), zoundTabProperties.groupBy == groupBy, selected => {
                                    Undo.RecordObject(ZoundsWindowProperties.Instance, "change group by");
                                    zoundTabProperties.groupBy = (GroupBy)selected;
                                    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                    zoundTabProperties.dirty = true;
                                }, groupBy);
                            }
                            menu.ShowAsContext();
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                if (settings.showColumnMode) {
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    int currentColumn = ZoundsProject.Instance.browserSettings.multicolumn ? 0 : 1;
                    int newColumnMode = GUILayout.Toolbar(currentColumn, icon_columns, GUILayout.Width(60f), GUILayout.Height(30f));
                    if (newColumnMode != currentColumn) {
                        ZoundsWindow.ModifyZoundsProject("toggle column view", () => {
                            ZoundsProject.Instance.browserSettings.multicolumn = newColumnMode == 0;
                        });
                    }
                }

                GUILayout.Space(3f);
            }
            GUILayout.EndHorizontal();

            ZUI.RowSpace();

            int selectedIndex = selectedZound != null ? filteredZounds.IndexOf(selectedZound) : -1;

            ZUI.RowSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Space(5f);
            if (ZoundsProject.Instance.browserSettings.multicolumn) {
                DrawZoundsMulticolumn(contentRect.size, selectedIndex, filteredZounds);
            }
            else {
                DrawZoundsSinglecolumn(contentRect.size, selectedIndex, filteredZounds);
            }
            GUILayout.Space(5f);
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            // Deferred mutations
            if (zoundToRemove != null) {
                ZoundsWindow.ModifyZoundsProject("remove zound", () => {
                    AudioAssetUtility.RemoveZound(zoundToRemove);
                    if (zoundToRemove is Klip) ZoundsAssetPostProcessor.RefreshAudioClipsCache();
                    filterCache = null;
                });
                zoundToRemove = null;
            }
            if (zoundToDuplicate != null) {
                ZoundsWindow.ModifyZoundsProject("duplicate zound", () => {
                    var duplicatedZound = AudioAssetUtility.DuplicateZound(zoundToDuplicate) as Zound;
                    if (duplicatedZound != null) SelectZound(duplicatedZound);
                    filterCache = null;
                });
                zoundToDuplicate = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ADD NEW / OPEN EDITOR
        // ═══════════════════════════════════════════════════════════════════════

        private void HandleAddNew() {
            OpenAddNewZoundMenu();
        }

        public static void OpenAddNewZoundMenu(string nameOverride = null) {
            var mousePosition = Event.current.mousePosition;
            var genericMenu = new GenericMenu();
            genericMenu.AddItem(new GUIContent("Klip"), false, () => {
                OpenCreateNewKlipDialog(mousePosition, OnKlipAdded, addMenuSearchText, text => addMenuSearchText = text, nameOverride);
            });
            genericMenu.AddItem(new GUIContent("Zequence"), false, () => {
                ZoundsWindow.ModifyZoundsProject("add new zequence", () => {
                    var newZequence = new Zequence(ZoundLibrary.GetUniqueZoundId());
                    newZequence.name = string.IsNullOrEmpty(nameOverride)
                        ? ZoundDictionary.EnsureUniqueZoundName("New Zequence")
                        : nameOverride;
                    OnZequenceAdded(newZequence);
                }, true);
            });
            genericMenu.ShowAsContext();
        }

        public static void OnZequenceAdded(Zequence newZequence) {
            var zoundKey = ZoundDictionary.ZoundNameToKey(newZequence.name);
            var existingClipZound = ZoundsAssetPostProcessor.audioClipZoundsCache.Find(z => ZoundDictionary.ZoundNameToKey(z.name) == zoundKey);
            if (existingClipZound != null) ZoundsAssetPostProcessor.audioClipZoundsCache.Remove(existingClipZound);
            if (Application.isPlaying && ZoundEngine.Instance.zoundDictionary.ContainsKey(zoundKey))
                ZoundEngine.Instance.zoundDictionary.Remove(zoundKey);
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            zoundLibrary.zequences.Add(newZequence);
            zoundLibrary.zequences = zoundLibrary.zequences.OrderBy(it => it.name).ToList();
            if (instance != null) {
                instance.SelectZound(newZequence);
                instance.filterCache = null;
            }
            if (ZoundEngine.IsInitialized()) ZoundDictionary.ValidateZoundRuntime(newZequence);
        }

        public static void OnKlipAdded(Klip newKlip) {
            var zoundKey = ZoundDictionary.ZoundNameToKey(newKlip.name);
            var existingClipZound = ZoundsAssetPostProcessor.audioClipZoundsCache.Find(z => ZoundDictionary.ZoundNameToKey(z.name) == zoundKey);
            if (existingClipZound != null) ZoundsAssetPostProcessor.audioClipZoundsCache.Remove(existingClipZound);
            if (Application.isPlaying && ZoundEngine.Instance.zoundDictionary.ContainsKey(zoundKey))
                ZoundEngine.Instance.zoundDictionary.Remove(zoundKey);
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            zoundLibrary.klips.Add(newKlip);
            zoundLibrary.klips = zoundLibrary.klips.OrderBy(it => it.name).ToList();
            if (instance != null) {
                instance.SelectZound(newKlip);
                instance.filterCache = null;
            }
        }

        public static void OpenCreateNewKlipDialog(Vector3 mousePosition, System.Action<Klip> onKlipAdded, string searchText, System.Action<string> onSearchTextChanged, string nameOverride = null) {
            var genericMenu = new GenericMenu();
#if ADDRESSABLES_INSTALLED
            AudioAssetUtility.FindAllAudioReferencesInWorkspace(out var libraryAudioRefs, out var workAudioRefs, out var sourcesAudioRefs, out var _);
            foreach (var audioRef in libraryAudioRefs) AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "", nameOverride);
            foreach (var audioRef in workAudioRefs)    AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "", nameOverride);
            foreach (var audioRef in sourcesAudioRefs) AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Sources/", nameOverride);
#endif
            GenericMenuPopup.Show(genericMenu, "Add New Klip(s)", mousePosition, new List<string>(), searchText, newSearch => onSearchTextChanged?.Invoke(newSearch), userData => PlayAudioClip(userData), 3, false, null, (updateFilter) => DrawFolderFilterButtons(updateFilter));
        }

        public void OpenZoundEditor(Zound zound) {
            if (zound == null) return;
            if (zound is ClipZound clipZound) {
                if (EditorUtility.DisplayDialog("Convert to Klip: " + zound.name, "In order for this audio clip to be editable, it must be converted into a Klip. Convert this into a Klip?\n" + zound.name, "Convert", "Cancel")) {
                    ConvertClipToKlip(clipZound);
                }
            }
            else if (zound is Klip klip)          KlipEditorWindow.OpenWindow(klip);
            else if (zound is Zequence zequence)   ZequenceEditorWindow.OpenWindow(zequence);
            else                                   KlipEditorWindow.OpenWindow(zound as Klip);
        }

        internal void ConvertClipToKlip(ClipZound clipZound) {
            ZoundsAssetPostProcessor.audioClipZoundsCache.Remove(clipZound);
            ZoundsWindow.ModifyZoundsProject("convert to klip", () => {
                var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());
                newKlip.audioClipRef = AudioRenderUtility.GetAudioReference(clipZound.audioClip);
                newKlip.name = clipZound.name;
                newKlip.trimStart = 0f;
                newKlip.trimEnd = clipZound.audioClip.length;
                newKlip.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                newKlip.pitchEnvelope  = new Envelope(Zound.MinPitchRange,  Zound.MaxPitchRange);
                if (ZoundEngine.IsInitialized()) ZoundDictionary.ValidateZoundRuntime(newKlip);
                OnKlipAdded(newKlip);
                filterCache = null;
            }, true);
        }

        internal void ConvertKlipToZequence(Klip klip) {
            ZoundsWindow.ModifyZoundsProject("convert to zequence", () => {
                ZoundsProject.Instance.zoundLibrary.klips.Remove(klip);
                var existingID = klip.id;
                var newZeq = new Zequence(existingID);
                newZeq.name = klip.name;
                klip.id = ZoundLibrary.GetUniqueZoundId();
                klip.parentId = newZeq.id;
                newZeq.localKlips.Add(klip);
                var newEntry = new CompositeZound.ZoundEntry();
                newEntry.zoundId = klip.id;
                newEntry.local = true;
                newZeq.zoundEntries.Add(newEntry);
                OnZequenceAdded(newZeq);
                filterCache = null;
            }, true);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FILTER + GROUP
        // ═══════════════════════════════════════════════════════════════════════

        private List<Zound> GetFilteredZounds() {
            return filterEngine.GetFilteredZounds(zoundsToDisplay, zoundTabProperties);
        }

        private List<Zound> EvaluateGroup(List<Zound> filteredZounds) {
            return filterEngine.EvaluateGroup(filteredZounds, zoundTabProperties);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SELECTION
        // ═══════════════════════════════════════════════════════════════════════

        public void SelectZound(Zound zound) {
            selectedZound = zound;
            inspectorAnimFloat.speed = 4f;
            if (zound == null) {
                inspectorAnimFloat.SetTarget(0f);
            }
            else {
                inspectorAnimFloat.SnapTo(0f);
                inspectorAnimFloat.SetTarget(inspectorHeight);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TAGS STRING UTILITY
        // ═══════════════════════════════════════════════════════════════════════

        public static string GetZoundTagsString(Zound zoundToInspect) {
            if (zoundToInspect.tags.Count > 0) {
                var projectTags = ZoundsProject.Instance.zoundLibrary.tags;
                var sb = new StringBuilder();
                for (int i = 0; i < zoundToInspect.tags.Count; i++) {
                    var tag = projectTags.Find(t => t.id == zoundToInspect.tags[i]);
                    if (tag == null) continue;
                    sb.Append(tag.name);
                    if (i < zoundToInspect.tags.Count - 1) sb.Append(", ");
                }
                string s = sb.ToString();
                return string.IsNullOrEmpty(s) ? "-Untagged-" : s;
            }
            return "-Untagged-";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MULTICOLUMN (GRID MODE)
        // ═══════════════════════════════════════════════════════════════════════

        internal void UpdateInspectorHeight(Zound selected) {
            float lastTagsWidth = zoundBrowserEditor.GetLastTagsWidth();
            if (lastTagsWidth > 0f) {
                tempContent.text = GetZoundTagsString(selected);
                float newHeight = zoundBrowserEditor.GetTagsLabelStyle().CalcHeight(tempContent, lastTagsWidth);
                float newTarget = Mathf.Max(inspectorHeight, newHeight);
                if (!Mathf.Approximately(newTarget, inspectorAnimFloat.target)) {
                    inspectorAnimFloat.SnapTo(newTarget);
                }
            }
        }

        private void DrawZoundsMulticolumn(Vector2 contentSize, int selectedIndex, List<Zound> filteredZounds) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            var sizeMode = browserSettings.buttonSizeMode;
            float itemWidth = browserSettings.itemWidth;

            if (sizeMode == ZoundsProject.BrowserSettings.ButtonSizeMode.Fixed) {
                if (itemWidth > contentSize.x - 8f) itemWidth = contentSize.x - 8f;
                int columnCount = Mathf.FloorToInt(contentSize.x / itemWidth);
                int rowCount = Mathf.CeilToInt(filteredZounds.Count / (float)columnCount);
                int zoundIndex = 0;
                int inspectorRowIndex = selectedIndex < 0 ? -1 : Mathf.FloorToInt(selectedIndex / (float)columnCount);

                scrollPos = GUILayout.BeginScrollView(scrollPos);
                {
                    if (groupCache != null && groupCache.Count > 0) {
                        foreach (var kvp in groupCache) {
                            EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
                            int memberCount = kvp.Value.Count;
                            bool firstGroupRow = true;
                            while (memberCount > 0) {
                                int colCount = memberCount > columnCount ? columnCount : memberCount;
                                if (zoundTabProperties.groupBy == GroupBy.Tags) {
                                    for (int i = 0; i < colCount; i++) {
                                        int index = zoundIndex + i;
                                        if (index < filteredZounds.Count && filteredZounds[index] == selectedZound)
                                            selectedIndex = index;
                                    }
                                }
                                if (!firstGroupRow) GUILayout.Space(MULTICOLUMN_V_GAP);
                                firstGroupRow = false;
                                bool isRowSelected = selectedIndex >= zoundIndex && selectedIndex < zoundIndex + colCount;
                                ZoundGridItemView.DrawFixedRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth, this);
                                memberCount -= columnCount;
                                if (isRowSelected)
                                    zoundBrowserEditor.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                            }
                        }
                    }
                    else {
                        for (int i = 0; i < rowCount; i++) {
                            if (i > 0) GUILayout.Space(MULTICOLUMN_V_GAP);
                            ZoundGridItemView.DrawFixedRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth, this);
                            if (selectedIndex >= 0 && inspectorRowIndex == i) {
                                UpdateInspectorHeight(filteredZounds[selectedIndex]);
                                zoundBrowserEditor.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                            }
                        }
                    }
                }
                GUILayout.EndScrollView();
            }
            else {
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                {
                    int zoundIndex = 0;
                    if (groupCache != null && groupCache.Count > 0) {
                        foreach (var kvp in groupCache) {
                            EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
                            ZoundGridItemView.DrawFlowRow(kvp.Value, selectedIndex, ref zoundIndex, contentSize.x, this);
                        }
                    }
                    else {
                        ZoundGridItemView.DrawFlowRow(filteredZounds, selectedIndex, ref zoundIndex, contentSize.x, this);
                    }
                }
                GUILayout.EndScrollView();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SINGLECOLUMN (LIST MODE)
        // ═══════════════════════════════════════════════════════════════════════

        private ZoundListRowLayout ComputeListRowLayout(float itemWidth, ZoundsProject.BrowserSettings browserSettings) {
            var layout = new ZoundListRowLayout();
            layout.itemWidth = itemWidth;

            float buttonWidth = ROW_BUTTON_WIDTH;
            layout.removeRectWidth = 0f;
            if (browserSettings.showRouting)   layout.removeRectWidth += buttonWidth;
            if (browserSettings.showDuplicate) layout.removeRectWidth += buttonWidth;
            if (browserSettings.showRemove)    layout.removeRectWidth += buttonWidth;

            layout.editRectWidth = browserSettings.showOpenEditor ? buttonWidth : 0f;
            bool bothMS = browserSettings.showMute && browserSettings.showSolo;
            layout.muteSoloWidthSingle = (browserSettings.showMute || browserSettings.showSolo)
                ? (bothMS ? 24f + MUTE_SOLO_GAP + 24f : 24f) : 0f;
            float editToMSGapEst = (layout.editRectWidth > 0 && layout.muteSoloWidthSingle > 0) ? ZoundItem_spacing : 0f;
            layout.leftTotalEst = layout.editRectWidth + editToMSGapEst + layout.muteSoloWidthSingle + LEFT_BUTTONS_TO_NAME_GAP;

            layout.minInspectorWidth = 0f;
            if (browserSettings.showNameField) layout.minInspectorWidth += 120f;
            if (browserSettings.showVolume)    layout.minInspectorWidth += 120f;
            if (browserSettings.showPitch)     layout.minInspectorWidth += 120f;
            if (browserSettings.showChance)    layout.minInspectorWidth += 120f;

            layout.tagsZoneWidth = browserSettings.showTags ? 1f : 0f;
            layout.tagsGap       = browserSettings.showTags ? ZoundItem_spacing : 0f;
            layout.lastValidSize = _listRowLayout.lastValidSize;
            return layout;
        }

        private void DrawZoundsSinglecolumn(Vector2 contentSize, int selectedIndex, List<Zound> filteredZounds) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            var sizeMode = browserSettings.buttonSizeMode;
            float itemWidth = browserSettings.itemWidth;

            if (sizeMode != ZoundsProject.BrowserSettings.ButtonSizeMode.Fixed) {
                var btnStyle = ZUI.GetButtonStyle(ZUI.Style.ZoundBtn);
                float maxW = 0f;
                foreach (var z in filteredZounds) {
                    zoundButtonContent.text = z.name;
                    maxW = Mathf.Max(maxW, btnStyle.CalcSize(zoundButtonContent).x);
                }
                if (sizeMode == ZoundsProject.BrowserSettings.ButtonSizeMode.Min)
                    maxW = Mathf.Max(maxW, itemWidth);
                itemWidth = maxW;
            }

            _listRowLayout = ComputeListRowLayout(itemWidth, browserSettings);

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            {
                GUILayout.Space(1f);
                if (groupCache != null && groupCache.Count > 0) {
                    int i = 0;
                    foreach (var kvp in groupCache) {
                        EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
                        foreach (var z in kvp.Value) {
                            if (i >= filteredZounds.Count) break;
                            if (filteredZounds[i] == selectedZound) selectedIndex = i;
                            DrawSinglecolumnRow(filteredZounds, selectedIndex, i);
                            if (i < filteredZounds.Count - 1) ZUI.RowSpace(0.5f);
                            i++;
                        }
                    }
                }
                else {
                    for (int i = 0; i < filteredZounds.Count; i++) {
                        DrawSinglecolumnRow(filteredZounds, selectedIndex, i);
                        if (i < filteredZounds.Count - 1) ZUI.RowSpace(0.5f);
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        internal struct ZoundListRowLayout {
            public float itemWidth;
            public float editRectWidth;
            public float muteSoloWidthSingle;
            public float removeRectWidth;
            public float minInspectorWidth;
            public float tagsZoneWidth;
            public float tagsGap;
            public float leftTotalEst;
            public Vector2 lastValidSize;
            public bool  multipleRows;
            public float muteSoloRectWidth;
            public float editToMSGap;
            public float leftButtonsWidth;
            public float leftGap;
            public float middleRight;
            public Rect  editButtonRect;
            public Rect  muteSoloRect;
            public Rect  nameButtonRect;
            public Rect  inspectorRect;
            public Rect  removeButtonRect;
            public Rect  tagsRect;
            public Rect  row2Rect;
            public Rect  itemAreaRect;
            public Rect  rowRect;
        }

        private ZoundListRowLayout _listRowLayout;

        protected void DrawSinglecolumnRow(List<Zound> filteredList, int selectedIndex, int currentIndex) {
            var currentZound    = filteredList[currentIndex];
            var browserSettings = ZoundsProject.Instance.browserSettings;
            ref var layout      = ref _listRowLayout;

            bool isMissingZoundEarly = !(currentZound is ClipZound) && currentZound.id == 0;

            float rowHeight = ROW_HEIGHT;
            float tagHeight = ROW_HEIGHT;
            float tagsZoneWidth = 0f;
            if (layout.tagsZoneWidth > 0f && !isMissingZoundEarly) {
                var tagsStyle = zoundBrowserEditor.GetTagsLabelStyle();
                if (tagsStyle != null) {
                    tempContent.text = GetZoundTagsString(currentZound);
                    float lastKnownRowWidth = layout.lastValidSize.x > 1f ? layout.lastValidSize.x : 400f;
                    float maxZoneWidth      = Mathf.Min(180f, lastKnownRowWidth * 0.25f);
                    float naturalWidth      = tagsStyle.CalcSize(tempContent).x;
                    tagsZoneWidth           = Mathf.Min(naturalWidth, maxZoneWidth);
                    tagHeight               = tagsStyle.CalcHeight(tempContent, tagsZoneWidth);
                    rowHeight               = Mathf.Max(ROW_HEIGHT, tagHeight);
                    _tagRowHeightCache[currentZound] = rowHeight;
                }
            }
            if (_tagRowHeightCache.TryGetValue(currentZound, out float cachedHeight)) {
                rowHeight = cachedHeight;
                tagHeight = cachedHeight;
            }

            bool tagsOverflow = rowHeight > ROW_HEIGHT + 1f;

            Rect rowRect;
            try { rowRect = GUILayoutUtility.GetRect(1, rowHeight, GUILayout.ExpandWidth(true)); }
            catch { rowRect = new Rect(); }
            if (rowRect.width  > 1f) layout.lastValidSize.x = rowRect.width;
            rowRect.width  = layout.lastValidSize.x;
            rowRect.height = tagsOverflow ? ROW_HEIGHT : (rowHeight > 1f ? rowHeight : ROW_HEIGHT);
            layout.rowRect = rowRect;

            if (layout.tagsZoneWidth > 0f && !isMissingZoundEarly) {
                var tagsStyle = zoundBrowserEditor.GetTagsLabelStyle();
                if (tagsStyle != null) {
                    float maxZoneWidth = Mathf.Min(180f, rowRect.width * 0.25f);
                    tagsZoneWidth      = Mathf.Min(tagsZoneWidth, maxZoneWidth);
                }
            }
            float tagsGap = tagsZoneWidth > 0f ? layout.tagsGap : 0f;
            layout.tagsZoneWidth = tagsZoneWidth;
            layout.tagsGap       = tagsGap;

            float tagsEstWidth       = tagsZoneWidth;
            float availableForFields = rowRect.width - layout.leftTotalEst - layout.itemWidth
                                       - layout.removeRectWidth - tagsEstWidth
                                       - (tagsEstWidth > 0f ? ZoundItem_spacing : 0f)
                                       - ZoundItem_spacing * 2f;
            layout.multipleRows = !isMissingZoundEarly && (availableForFields < layout.minInspectorWidth || tagsOverflow);

            layout.muteSoloRectWidth = layout.multipleRows
                ? (browserSettings.showMute || browserSettings.showSolo ? 24f : 0f)
                : layout.muteSoloWidthSingle;
            layout.editToMSGap      = (layout.editRectWidth > 0 && layout.muteSoloRectWidth > 0) ? ZoundItem_spacing : 0f;
            layout.leftButtonsWidth = layout.editRectWidth + layout.editToMSGap + layout.muteSoloRectWidth;
            layout.leftGap          = layout.leftButtonsWidth > 0 ? LEFT_BUTTONS_TO_NAME_GAP : 0f;
            layout.middleRight      = rowRect.xMax - tagsZoneWidth - tagsGap;

            float row2Gap    = layout.multipleRows ? MUTE_SOLO_GAP : 0f;
            float row2Height = layout.multipleRows ? ROW_HEIGHT    : 0f;
            GUILayout.Space(row2Gap);
            Rect row2Rect;
            try { row2Rect = GUILayoutUtility.GetRect(1, row2Height, GUILayout.ExpandWidth(true)); }
            catch { row2Rect = new Rect(rowRect.x, rowRect.yMax + row2Gap, rowRect.width, row2Height); }
            if (layout.multipleRows && tagsOverflow) {
                row2Rect.y = rowRect.y + ROW_HEIGHT + row2Gap;
            }
            layout.row2Rect = row2Rect;

            if (layout.multipleRows) {
                float row1MiddleX    = rowRect.x + layout.leftButtonsWidth + layout.leftGap;
                float row1RightStart = layout.middleRight - layout.removeRectWidth;
                layout.nameButtonRect   = new Rect(row1MiddleX, rowRect.y, row1RightStart - row1MiddleX - ZoundItem_spacing, ROW_HEIGHT);
                layout.removeButtonRect = new Rect(row1RightStart, rowRect.y, layout.removeRectWidth, ROW_HEIGHT);
                float fieldsX           = row2Rect.x + layout.leftButtonsWidth + layout.leftGap;
                layout.inspectorRect    = new Rect(fieldsX, row2Rect.y, layout.middleRight - fieldsX, row2Rect.height);
                layout.tagsRect         = new Rect(layout.middleRight + tagsGap, rowRect.y, tagsZoneWidth, row2Rect.yMax - rowRect.y);
            }
            else {
                layout.row2Rect = Rect.zero;
                float row1MiddleX    = rowRect.x + layout.leftButtonsWidth + layout.leftGap;
                float row1RightStart = layout.middleRight - layout.removeRectWidth;
                layout.nameButtonRect  = new Rect(row1MiddleX, rowRect.y, layout.itemWidth, rowRect.height);
                float fieldsX          = layout.nameButtonRect.xMax + ZoundItem_spacing;
                float fieldsWidth      = row1RightStart - fieldsX - ZoundItem_spacing;
                layout.inspectorRect   = new Rect(fieldsX, rowRect.y, Mathf.Max(0f, fieldsWidth), rowRect.height);
                layout.removeButtonRect = new Rect(row1RightStart, rowRect.y, layout.removeRectWidth, rowRect.height);
                layout.tagsRect        = tagsZoneWidth > 0
                    ? new Rect(layout.middleRight + tagsGap, rowRect.y, tagsZoneWidth, rowRect.height)
                    : Rect.zero;
            }

            float leftHeight       = layout.multipleRows ? (row2Rect.yMax - rowRect.y) : rowRect.height;
            layout.editButtonRect  = new Rect(rowRect.x, rowRect.y, layout.editRectWidth, leftHeight);
            layout.muteSoloRect    = new Rect(layout.editButtonRect.xMax + layout.editToMSGap, rowRect.y, layout.muteSoloRectWidth, leftHeight);
            layout.itemAreaRect    = layout.multipleRows
                ? new Rect(rowRect.x, rowRect.y, rowRect.width, row2Rect.yMax - rowRect.y)
                : rowRect;

            ZoundListItemView.Draw(currentZound, ref layout, zoundBrowserEditor, this);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TYPE FILTER HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private static void DrawTypesInlineToggle(ZoundsWindowProperties.ZoundTabProperties zoundTabProperties) {
            const float innerGap = 2f;
            var totalRect = GUILayoutUtility.GetRect(10f, 30f, GUILayout.ExpandWidth(true));
            float btnW = (totalRect.width  - innerGap) * 0.5f;
            float btnH = (totalRect.height - innerGap) * 0.5f;
            var rects = new Rect[4];
            rects[0] = new Rect(totalRect.x,                   totalRect.y,                   btnW, btnH);
            rects[1] = new Rect(totalRect.x + btnW + innerGap, totalRect.y,                   btnW, btnH);
            rects[2] = new Rect(totalRect.x,                   totalRect.y + btnH + innerGap, btnW, btnH);
            rects[3] = new Rect(totalRect.x + btnW + innerGap, totalRect.y + btnH + innerGap, btnW, btnH);
            DrawTypeToggleButton(zoundTabProperties, ZoundType.Klip,      "K", rects[0]);
            DrawTypeToggleButton(zoundTabProperties, ZoundType.Zequence,  "Z", rects[1]);
            DrawTypeToggleButton(zoundTabProperties, ZoundType.AudioClip, "C", rects[2]);
            DrawTypeToggleButton(zoundTabProperties, ZoundType.Missing,   "M", rects[3]);
        }

        private static void DrawTypeToggleButton(ZoundsWindowProperties.ZoundTabProperties props, ZoundType type, string label, Rect rect) {
            bool on = props.selectedTypes.HasFlag(type);
            if (ZUI.Toggle(rect, on, label) != on) {
                Undo.RecordObject(ZoundsWindowProperties.Instance, "toggle type filter");
                if (on) props.selectedTypes &= ~type;
                else    props.selectedTypes |= type;
                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                props.dirty = true;
            }
        }

        private static void AddTypeMenuItem(GenericMenu menu, ZoundsWindowProperties.ZoundTabProperties zoundTabProperties, ZoundType type) {
            var t = type;
            menu.AddItem(new GUIContent(type.ToString()), zoundTabProperties.selectedTypes.HasFlag(t), selected => {
                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected types");
                if ((bool)selected) zoundTabProperties.selectedTypes |= t;
                else                zoundTabProperties.selectedTypes &= ~t;
                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                zoundTabProperties.dirty = true;
            }, zoundTabProperties.selectedTypes.HasFlag(t));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // KLIP CREATION HELPERS (used by OpenCreateNewKlipDialog)
        // ═══════════════════════════════════════════════════════════════════════

#if ADDRESSABLES_INSTALLED
        private static void AddAudioRefToGenericMenu(System.Action<Klip> onKlipAdded, GenericMenu genericMenu, AssetReferenceT<AudioClip> audioRef, string parentPath, string nameOverride) {
            var clipName = audioRef.editorAsset.name;
            string assetPath = AssetDatabase.GetAssetPath(audioRef.editorAsset);
            var projectSettings = ZoundsProject.Instance.projectSettings;
            string relativePath = "";
            if (!string.IsNullOrEmpty(projectSettings.libraryFolderPath) && assetPath.StartsWith(projectSettings.libraryFolderPath)) {
                relativePath = assetPath.Replace(projectSettings.libraryFolderPath, "").Replace("\\", "/");
                if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
                int lastSlash = relativePath.LastIndexOf('/');
                relativePath = lastSlash != -1 ? relativePath.Substring(0, lastSlash + 1) : "";
            }
            else if (!string.IsNullOrEmpty(projectSettings.sourcesFolderPath) && assetPath.StartsWith(projectSettings.sourcesFolderPath)) {
                string subPath = assetPath.Replace(projectSettings.sourcesFolderPath, "").Replace("\\", "/");
                if (subPath.StartsWith("/")) subPath = subPath.Substring(1);
                int lastSlash = subPath.LastIndexOf('/');
                string subFolder = lastSlash != -1 ? subPath.Substring(0, lastSlash + 1) : "";
                relativePath = "Sources/" + subFolder;
            }
            else if (!string.IsNullOrEmpty(parentPath)) {
                relativePath = parentPath;
            }

            genericMenu.AddItem(new GUIContent(relativePath + clipName), false, userData => {
                ZoundsWindow.ModifyZoundsProject("add new klips", () => {
                    var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());
                    string ap = AssetDatabase.GetAssetPath(audioRef.editorAsset);
                    if (ap.StartsWith(projectSettings.workFolderPath)) {
                        string newPath = ap.Replace(projectSettings.workFolderPath, projectSettings.sourcesFolderPath);
                        newPath = Path.ChangeExtension(newPath, ".Copy.wav");
                        newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                        var reloadedAudio = AudioRenderUtility.SaveAudio(audioRef.editorAsset, newPath);
                        newKlip.audioClipRef = AudioRenderUtility.GetAudioReference(reloadedAudio);
                        newKlip.name = ZoundDictionary.EnsureUniqueZoundName(newKlip.audioClipRef.editorAsset.name);
                    }
                    else {
                        newKlip.audioClipRef = audioRef;
                        newKlip.name = ZoundDictionary.EnsureUniqueZoundName(clipName);
                    }
                    if (!string.IsNullOrEmpty(nameOverride)) newKlip.name = nameOverride;
                    newKlip.trimStart = 0f;
                    newKlip.trimEnd = audioRef.editorAsset.length;
                    newKlip.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                    newKlip.pitchEnvelope  = new Envelope(Zound.MinPitchRange,  Zound.MaxPitchRange);
                    if (ZoundEngine.IsInitialized()) ZoundDictionary.ValidateZoundRuntime(newKlip);
                    onKlipAdded?.Invoke(newKlip);
                }, true);
            }, audioRef.editorAsset);
        }
#endif

        private static void PlayAudioClip(object userData) {
            if (userData is AudioClip audioClip) AudioPreviewUtility.PlayPreviewClip(audioClip);
        }

        private static void DrawFolderFilterButtons(System.Action<string, bool> updateFilter) {
            var projectSettings = ZoundsProject.Instance.projectSettings;
            string libraryPath = projectSettings.libraryFolderPath;
            string sourcesPath = projectSettings.sourcesFolderPath;

            var allFolders = new List<string>();
            if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath))
                allFolders.AddRange(Directory.GetDirectories(libraryPath, "*", SearchOption.AllDirectories));
            if (!string.IsNullOrEmpty(sourcesPath) && Directory.Exists(sourcesPath))
                allFolders.AddRange(Directory.GetDirectories(sourcesPath, "*", SearchOption.AllDirectories));

            string defaultRoot = "Assets/GameData/ZoundsData";
            if (allFolders.Count == 0 && Directory.Exists(defaultRoot))
                allFolders.AddRange(Directory.GetDirectories(defaultRoot, "*", SearchOption.AllDirectories));

            if (allFolders.Count == 0) return;

            Color libraryColor = new Color(0.7f, 0.9f, 0.7f);
            Color sourcesColor = new Color(0.7f, 0.8f, 1.0f);
            Color defaultColor = GUI.color;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                float viewWidth = EditorGUIUtility.currentViewWidth - 30f;
                float currentX  = 0f;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Folders:", EditorStyles.miniLabel, GUILayout.Width(50));
                currentX += 55f;

                if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                    updateFilter?.Invoke("", true);
                }
                currentX += 45f;

                var uniqueNames = new HashSet<string>();
                foreach (string folderPath in allFolders) {
                    string folderName = Path.GetFileName(folderPath);
                    if (uniqueNames.Contains(folderName)) continue;
                    uniqueNames.Add(folderName);

                    bool isLibrary = !string.IsNullOrEmpty(libraryPath) && folderPath.StartsWith(libraryPath);
                    float buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent(folderName)).x + 4f;

                    if (currentX + buttonWidth > viewWidth) {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(55f);
                        currentX = 55f;
                    }

                    GUI.color = isLibrary ? libraryColor : sourcesColor;
                    if (GUILayout.Button(folderName, EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                        string relative = "";
                        if (isLibrary) relative = folderPath.Replace(libraryPath, "");
                        else if (!string.IsNullOrEmpty(sourcesPath) && folderPath.StartsWith(sourcesPath)) relative = folderPath.Replace(sourcesPath, "");
                        else relative = folderPath.Replace(defaultRoot, "");
                        relative = relative.Replace("\\", "/").ToLower();
                        if (relative.StartsWith("/")) relative = relative.Substring(1);
                        if (!string.IsNullOrEmpty(relative) && !relative.EndsWith("/")) relative += "/";
                        updateFilter?.Invoke(relative, true);
                    }
                    GUI.color = defaultColor;
                    currentX += buttonWidth;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
    }
}
