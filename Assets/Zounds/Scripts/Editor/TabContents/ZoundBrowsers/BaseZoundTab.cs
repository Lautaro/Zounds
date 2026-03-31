using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Audio;
using static Zounds.ZoundsWindowProperties.ZoundTabProperties;

namespace Zounds {


    public class BaseZoundTab<TZound> : TabContent where TZound : Zound {

        internal const float inspectorHeight = 39f;

        // ── Layout spacing constants ───────────────────────────────────────────
        // Row layout: Edit <A> M/S <B> ZoundButton <B> Route/Convert/Dup/Del
        //   GAP_A = gap between separate button groups (edit↔MS, right group↔name).
        //   GAP_B = gap between the name button and its immediate neighbours (MS and right group).
        internal const float ROW_HEIGHT          = 24f;  // Height of each zound row.
        internal const float ROW_BUTTON_WIDTH    = 40f;  // Width of each icon button (edit, M, S, route, dup, del, convert).
        internal const float ROW_VERTICAL_GAP    = 10f;   // Vertical space between rows in single-column mode.
        internal const float MULTICOLUMN_H_GAP   = 2f;   // Horizontal gap between buttons in multi-column mode.
        internal const float MULTICOLUMN_V_GAP   = 2f;   // Vertical gap between rows in multi-column mode.
        internal const float TOOLBAR_BUTTON_GAP  = 10f;  // Space between buttons in the quick-controls toolbar.
        internal const float ZoundButton_Spacing = 15f;   // Gap between the name button and adjacent buttons (M/S and right group).

        // ── ZUI-driven spacing — reads named scales from the active style sheet at draw time ──
        // MUTE_SOLO_GAP: small gap within a button group (M↔S, Route↔Dup↔Del).
        internal static float MUTE_SOLO_GAP {
            get {
                var sheet = ZUI.ActiveSheet;
                return (sheet?.horizontalSpacing ?? 8f) * (sheet?.FindSpacingScale("H Btns Medium") ?? 1f);
            }
        }
        // ZoundItem_spacing: large gap between separate button groups (Edit↔MS, MS↔Name, Name↔Fields, Fields↔RightGroup).
        internal static float ZoundItem_spacing {
            get {
                var sheet = ZUI.ActiveSheet;
                return (sheet?.horizontalSpacing ?? 8f) * (sheet?.FindSpacingScale("H Btns Big") ?? 1f);
            }
        }

        // Aliases for call sites that use the old names — map onto the two-gap model.
        internal const float LEFT_BUTTONS_TO_NAME_GAP  = ZoundButton_Spacing;   // M/S → name button
        internal static float INSPECTOR_TO_REMOVE_GAP => ZoundItem_spacing;   // inspector fields → right button group
        internal const float NAME_TO_INSPECTOR_GAP     = ZoundButton_Spacing;   // name button → inspector fields

        private Zound selectedZound;
        private Vector2 scrollPos;
        internal AnimFloat inspectorAnimFloat = new AnimFloat(0f);
        internal ZoundBrowserEditor<TZound> zoundBrowserEditor;
        private GUIContent zoundButtonContent = new GUIContent();
        private GUIContent tempContent = new GUIContent();

        private GUIContent icon_addNew;
        private GUIContent[] icon_columns;

        private GUIContent filterLabel = new GUIContent("Filter:");

        private ZoundBrowserFilterEngine filterEngine = new ZoundBrowserFilterEngine();
        internal List<Zound> filterCache {
            get => filterEngine.filterCache;
            set => filterEngine.filterCache = value;
        }
        private List<KeyValuePair<string, List<Zound>>> groupCache => filterEngine.groupCache;

        protected virtual int zoundTabPropertyIndex => 0;

        protected ZoundsWindowProperties.ZoundTabProperties zoundTabProperties {
            get {
                return ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
            }
        }

        public virtual List<TZound> zounds {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        public virtual List<Zound> zoundsToDisplay => zounds.Select(z => (Zound)z).ToList();

        public Zound zoundToRemove { get; set; } = null;
        public Zound zoundToDuplicate { get; set; } = null;

        public BaseZoundTab() {
            inspectorAnimFloat.value = 0f;
            inspectorAnimFloat.target = 0f;
            inspectorAnimFloat.speed = 4;
            inspectorAnimFloat.valueChanged.RemoveAllListeners();
            inspectorAnimFloat.valueChanged.AddListener(ZoundsWindow.RepaintWindow);
            zoundBrowserEditor = new ZoundBrowserEditor<TZound>(this);

            icon_addNew = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/add-new"), "Add new item.");
            icon_columns = new GUIContent[] {
                new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/multicolumn"), "Grid mode"),
                new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/singlecolumn"), "List mode")
            };
        }

        public override void OnTabOpened() {
            ClearFocus();
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            // ── Entry point for all zound browser tab rendering. Called every frame by ZoundsWindow.
            // Layout order (top to bottom):
            //   1. Search bar + Master Volume slider   (always-on-top header controls)
            //   2. Quick Controls toolbar              (Add, Stop All, MS Clean, Mute Sel, Solo Sel, Types, Tags, References, Group By, Column Mode)
            //   3. Zound list                          (either multicolumn grid or singlecolumn rows, in a scrollview)
            //   4. Deferred mutations                  (remove/duplicate applied safely after iteration ends)

            SerializedProperty zoundLibrary = serializedObject.FindProperty("zoundLibrary");

            // Build the filtered+grouped zound list once per frame.
            // filterCache is invalidated whenever the library changes (add/remove/rename).
            List<Zound> filteredZounds = GetFilteredZounds();
            filteredZounds = EvaluateGroup(filteredZounds);

            var settings = ZoundsProject.Instance.browserSettings;

            // ──────────────────────────────────────────────────────────────────────────
            // SECTION 1 — Search bar + Master Volume
            // These are always visible at the top, above the quick-controls toolbar.
            // Visibility of each control is gated by BrowserSettings booleans.
            // When the window is wide enough (≥420px) both controls share one row.
            // Below that threshold each control gets its own row with extra spacing.
            // ──────────────────────────────────────────────────────────────────────────
            const float SECTION1_BREAK_WIDTH = 420f;
            bool section1Wide = contentRect.width >= SECTION1_BREAK_WIDTH;
            bool showBoth     = settings.showSearch && settings.showMasterVolume;
            bool sideBy       = section1Wide || !showBoth;

            ZUI.RowSpace();

            if (sideBy) GUILayout.BeginHorizontal();

            // ── Search field ───────────────────────────────────────────────────────
            // Filters the zound list in real time. Shows a grey "Search..." ghost
            // when empty and unfocused. The X button clears all active filters.
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

                        // Ghost text — drawn as an overlay label when the field is empty and unfocused.
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
                            ClearFocus();
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            // Spacing between controls: horizontal gap when side-by-side, vertical when stacked.
            if (showBoth)
            {
                if (sideBy) GUILayout.Space(8f);
                else        GUILayout.Space(6f);
            }

            // ── Master Volume slider ───────────────────────────────────────────────
            // Edits projectSettings.editorVolume (or playerVolume at runtime).
            // Displayed as 0–100% even though the underlying value is 0–1.
            if (settings.showMasterVolume) {
                GUILayout.BeginHorizontal();
                {
                    var prevLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 70f;
                    var projectSettings = ZoundsProject.Instance.projectSettings;
                    var masterVol = Application.isPlaying ? projectSettings.playerVolume : projectSettings.editorVolume;

                    // Format volume as 0-100%
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

            // ──────────────────────────────────────────────────────────────────────────
            // SECTION 2 — Quick Controls toolbar
            // A horizontal row of action buttons and filter dropdowns. Each button is
            // gated by a showXxx boolean in BrowserSettings so it can be hidden.
            // The toolbarAny flag ensures TOOLBAR_BUTTON_GAP is only added between
            // visible buttons — never before the first one or after the last.
            // Order: Add | Stop All | MS Clean | Mute Sel | Solo Sel |
            //        Types | Tags | References | Group By | Column Mode
            // ──────────────────────────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(5f);
                // Tracks whether any toolbar button has been drawn yet, so we can
                // conditionally insert spacing between buttons without leading/trailing gaps.
                bool toolbarAny = false;

                // Precompute first/last visible button for corner mask assignment.
                // Order: Add, StopAll, MSClean, MuteSel, SoloSel, Types, Tags, References, GroupBy, ColumnMode
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

                // ── Add new zound button ───────────────────────────────────────────────
                if (settings.showAddZound) {
                    tbIdx = 0;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button(icon_addNew, ZUI.Style.Confirm, TbMask(), GUILayout.Width(30f), GUILayout.Height(30f)) && Event.current.button == 0) {
                        HandleAddNew();
                        filterCache = null;
                    }
                }

                // ── Stop All — stops every currently playing ZoundToken ───────────────
                if (settings.showStopAll) {
                    tbIdx = 1;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Stop All", ZUI.Style.Default, TbMask(), GUILayout.Width(60f), GUILayout.Height(30f))) {
                        ZoundEngine.StopAllZounds();
                    }
                }

                // ── MS Clean — clears all mute/solo flags across the entire library ────
                if (settings.showMSClean) {
                    tbIdx = 2;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("MS Clean", ZUI.Style.Default, TbMask(), GUILayout.Width(65f), GUILayout.Height(30f))) {
                        ZoundsWindow.ModifyZoundsProject("clean mute/solo", () => {
                            ZoundsProject.Instance.zoundLibrary.ForEachZound(z => {
                                z.mute = false;
                                z.solo = false;
                            });
                            ZoundsProject.Instance.zoundLibrary.soloStatusNeedsUpdate = true;
                        });
                    }
                }

                // ── Mute Sel — mutes every zound currently visible in the filtered list ─
                if (settings.showMuteSel) {
                    tbIdx = 3;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Mute Sel", ZUI.Style.Default, TbMask(), GUILayout.Width(65f), GUILayout.Height(30f))) {
                        ZoundsWindow.ModifyZoundsProject("mute selected", () => {
                            foreach (var z in filteredZounds) {
                                if (z is Klip || z is Zequence) {
                                    z.mute = true;
                                }
                            }
                        });
                    }
                }

                // ── Solo Sel — solos every zound currently visible in the filtered list ─
                if (settings.showSoloSel) {
                    tbIdx = 4;
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Solo Sel", ZUI.Style.Default, TbMask(), GUILayout.Width(65f), GUILayout.Height(30f))) {
                        ZoundsWindow.ModifyZoundsProject("solo selected", () => {
                            foreach (var z in filteredZounds) {
                                if (z is Klip || z is Zequence) {
                                    z.solo = true;
                                }
                            }
                            ZoundsProject.Instance.zoundLibrary.soloStatusNeedsUpdate = true;
                        });
                    }
                }

                // ── Types filter dropdown — shows only Klip / Zequence / AudioClip / Missing ──
                // Can be rendered as a dropdown button (default) or inline toggles (typesInlineToggle).
                if (settings.showTypes) {
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (zoundTabProperties.selectedTypes.HasFlag(ZoundType.Everything)) {
                        zoundTabProperties.selectedTypes = ZoundType.None;
                    }

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

                            GenericMenuPopup.Show(
                                menu,
                                "Select Types",
                                Event.current.mousePosition,
                                new List<string>(),
                                "",
                                null,
                                null, 3, true,
                                ZoundsEditorPresets.Instance.typesPresets
                                );
                        }
                    }
                }

                // ── Tags filter dropdown — shows only zounds that have the selected tags ──
                // Tag names with a colon (e.g. "sfx:footstep") get a collapsible key-group
                // header in the popup so you can multi-select the whole category at once.
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
                                        if ((bool)selected) {
                                            if (!selectedTags.Contains(keyTag)) selectedTags.Add(keyTag);
                                        }
                                        else {
                                            selectedTags.RemoveAll(t => t == keyTag);
                                        }
                                        EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                        zoundTabProperties.dirty = true;
                                    }, on2);
                                }
                            }
                            menu.AddItem(new GUIContent(tagName), on, selected => {
                                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected tags");
                                if ((bool)selected) {
                                    if (!selectedTags.Contains(tagName)) selectedTags.Add(tagName);
                                }
                                else {
                                    selectedTags.RemoveAll(t => t == tagName);
                                }
                                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                zoundTabProperties.dirty = true;
                            }, on);
                        }

                        TagMenuPopup.ShowTagMenu(
                            menu,
                            "Select Tags",
                            Event.current.mousePosition,
                            new List<string>(),
                            tagsSearchText,
                            newSearch => tagsSearchText = newSearch,
                            null, 3, true,
                            ZoundsEditorPresets.Instance.tagsPresets);
                    }
                }

                // ── References filter dropdown — shows only zounds that reference the selected zounds ──
                // Useful for finding all Zequences that contain a specific Klip.
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
                            string displayName = z.name;
                            menu.AddItem(new GUIContent(displayName), on, selected => {
                                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected zounds");
                                if ((bool)selected) {
                                    if (!selectedReferences.Contains(zoundId)) selectedReferences.Add(zoundId);
                                }
                                else {
                                    selectedReferences.RemoveAll(id => id == zoundId);
                                }
                                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                zoundTabProperties.dirty = true;
                            }, on);
                        });

                        GenericMenuPopup.Show(
                            menu,
                            "Select References",
                            Event.current.mousePosition,
                            new List<string>(),
                            referencesSearchText,
                            newSearch => referencesSearchText = newSearch,
                            null, 3, true,
                            ZoundsEditorPresets.Instance.referencesPresets);
                    }
                }

                // ── Group By dropdown — groups the visible zound list by Tags, Type, MixerGroup, etc. ──
                // When active the list is split into labelled sections.
                // See EvaluateGroup() for the grouping logic.
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

                // ── Column Mode toggle — switches between list mode (rows) and grid mode ──
                // Uses a Unity Toolbar with two icon options (grid icon = index 0, list = index 1).
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

                // Hook for subclasses to append extra toolbar buttons after the column-mode toggle.
                OnAfterDrawColumnMode();

                GUILayout.Space(3f);
            }
            GUILayout.EndHorizontal();

            ZUI.RowSpace();

            // Resolve the selected zound's index in the filtered list.
            // selectedIndex == -1 means nothing is selected (no inspector panel shown in multicolumn).
            int selectedIndex = -1;
            if (selectedZound != null) {
                selectedIndex = filteredZounds.IndexOf(selectedZound);
            }

            // ──────────────────────────────────────────────────────────────────────────
            // SECTION 3 — Zound list (scrollable)
            // Dispatches to either the list mode or grid mode renderer.
            //   List mode: each zound is a full-width row with edit/MS/name/inspector/right-buttons.
            //   Grid mode: each zound is a name-only button arranged in a grid; right-click
            //              expands an inspector panel below its row.
            // ──────────────────────────────────────────────────────────────────────────
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

            // ──────────────────────────────────────────────────────────────────────────
            // SECTION 4 — Deferred mutations (remove / duplicate)
            // Zound removal and duplication are NOT done inline during iteration because
            // that would modify the list while it's being drawn. Instead, DrawRemoveButton
            // and DrawSinglecolumnRow set zoundToRemove / zoundToDuplicate (via DrawRemoveButton), and
            // we execute those operations here, safely after all rendering is done.
            // ──────────────────────────────────────────────────────────────────────────
            if (zoundToRemove != null) {
                ZoundsWindow.ModifyZoundsProject("remove zound", () => {
                    AudioAssetUtility.RemoveZound(zoundToRemove);
                    if (zoundToRemove is Klip) {
                        ZoundsAssetPostProcessor.RefreshAudioClipsCache();
                    }
                    filterCache = null;
                });
                zoundToRemove = null;
            }
            if (zoundToDuplicate != null) {
                ZoundsWindow.ModifyZoundsProject("duplicate zound", () => {
                    var duplicatedZound = AudioAssetUtility.DuplicateZound(zoundToDuplicate) as TZound;
                    if (duplicatedZound != null) {
                        SelectZound(duplicatedZound);
                    }
                    filterCache = null;
                });
                zoundToDuplicate = null;
            }
        }

        /// <summary>
        /// Override in subclasses to append extra buttons to the end of the quick-controls toolbar,
        /// right after the Column Mode toggle. Called every frame inside the toolbar's BeginHorizontal.
        /// </summary>
        protected virtual void OnAfterDrawColumnMode() {

        }

        // ──────────────────────────────────────────────────────────────────────────
        // GROUPING
        // EvaluateGroup runs once per GroupBy change and rebuilds groupCache — a list
        // of (groupLabel, members) pairs. The rest of the draw code iterates groupCache
        // when it's populated, or falls back to the flat filteredZounds list.
        // Cache is invalidated by setting filterCache = null (e.g. on library changes).
        // ──────────────────────────────────────────────────────────────────────────
        private List<Zound> EvaluateGroup(List<Zound> filteredZounds) {
            return filterEngine.EvaluateGroup(filteredZounds, zoundTabProperties);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GRID MODE RENDERING
        // In grid mode the zound list is drawn as a grid of name-only buttons.
        // Each button is ROW_HEIGHT tall and itemWidth wide (BrowserSettings.itemWidth).
        // Right-clicking a button selects it and reveals an inspector panel (drawn by
        // ZoundBrowserEditor.DrawMulticolumn) that expands below the row.
        //
        // Three layout sub-modes exist (controlled by BrowserSettings.buttonSizeMode):
        //   Fixed  — all buttons the same fixed itemWidth; inspector shown below the row.
        //   Min    — buttons auto-sized to text but never smaller than itemWidth; flow layout.
        //   Auto   — buttons auto-sized to text; flow layout, no minimum width.
        //
        // Call graph:
        //   DrawZoundsMulticolumn
        //     ├─ ZoundGridItemView.DrawFixedRow  (Fixed mode — draws one grid row of columnCount buttons)
        //     └─ ZoundGridItemView.DrawFlowRow   (Auto/Min mode — wraps buttons across horizontal lines)
        //          └─ ZoundGridItemView.DrawButton    (draws a single zound button + handles events)
        // ══════════════════════════════════════════════════════════════════════════
        #region MULTICOLUMN
        /// <summary>
        /// Recalculates the animated inspector height for the selected zound.
        /// Called each frame in Fixed mode so the tags area can expand if the tag text wraps.
        /// The AnimFloat smoothly animates the panel open/closed.
        /// </summary>
        internal void UpdateInspectorHeight(Zound selected) {
            float lastTagsWidth = zoundBrowserEditor.GetLastTagsWidth();
            if (lastTagsWidth > 0f) {
                tempContent.text = GetZoundTagsString(selected);
                float newHeight = zoundBrowserEditor.GetTagsLabelStyle().CalcHeight(tempContent, lastTagsWidth);
                float oldVal = inspectorAnimFloat.target;
                inspectorAnimFloat.target = Mathf.Max(inspectorHeight, newHeight);
                if (inspectorAnimFloat.target != oldVal) {
                    inspectorAnimFloat.value = inspectorAnimFloat.target;
                }
            }
        }



        /// <summary>
        /// Top-level multicolumn draw method. Wraps everything in a scrollview and dispatches to
        /// either the Fixed-mode grid renderer (ZoundGridItemView.DrawFixedRow) or the Auto/Min
        /// flow renderer (ZoundGridItemView.DrawFlowRow). Also handles group headers when GroupBy is active.
        ///
        /// In Fixed mode the inspector panel (ZoundBrowserEditor.DrawMulticolumn) is inserted
        /// below the row that contains the selected zound, animated via inspectorAnimFloat.
        /// </summary>
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
                                        if (index < filteredZounds.Count && filteredZounds[index] == selectedZound) {
                                            selectedIndex = index;
                                        }
                                    }
                                }
                                if (!firstGroupRow) GUILayout.Space(MULTICOLUMN_V_GAP);
                                firstGroupRow = false;
                                bool isRowSelected = selectedIndex >= zoundIndex && selectedIndex < zoundIndex + colCount;
                                ZoundGridItemView.DrawFixedRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth, this);
                                memberCount -= columnCount;
                                if (isRowSelected) {
                                    zoundBrowserEditor.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                                }
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
                // Auto or Min mode: flow layout
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



        #endregion MULTICOLUMN

        // ══════════════════════════════════════════════════════════════════════════
        // LIST MODE RENDERING
        // In list mode each zound gets its own full-width row drawn by DrawListRow.
        // Both single-row and two-row variants share the same zone structure:
        //
        //   [Edit] [M/S]  |  [ZoundButton] [Rout][Dup][Del]  |  [Tags]
        //                 |  [Name] [Vol] [Pitch] [Chance]    |
        //
        // The second middle row only appears when the window is too narrow to fit
        // all fields on one line next to the ZoundButton (multipleRows == true).
        //
        // Call graph:
        //   DrawZoundsSinglecolumn
        //     └─ DrawSinglecolumnRow           (computes rects, allocates GUILayout space, draws everything)
        //          └─ ZoundBrowserEditor.DrawZoundSinglecolumn (draws edit/MS/inspector/right-buttons)
        // ══════════════════════════════════════════════════════════════════════════
        #region SINGLECOLUMN
        /// <summary>
        /// Top-level list mode draw method. Wraps the list in a scrollview and calls
        /// DrawSinglecolumnRow for each visible zound. Handles group headers when GroupBy is active.
        /// In Auto/Min size modes, itemWidth is calculated once from the widest name so all rows
        /// have a consistent name button width.
        /// </summary>
        // Computes the settings-derived portion of ZoundListRowLayout.
        // itemWidth must already be resolved (Auto/Min measurement done by caller).
        // Rect-derived fields are left at default — they are filled per-row in DrawSinglecolumnRow.
        private ZoundListRowLayout ComputeListRowLayout(float itemWidth, ZoundsProject.BrowserSettings browserSettings) {
            var layout = new ZoundListRowLayout();
            layout.itemWidth = itemWidth;

            float buttonWidth = ROW_BUTTON_WIDTH;

            // Right-group width
            layout.removeRectWidth = 0f;
            if (browserSettings.showRouting)   layout.removeRectWidth += buttonWidth;
            if (browserSettings.showDuplicate) layout.removeRectWidth += buttonWidth;
            if (browserSettings.showRemove)    layout.removeRectWidth += buttonWidth;

            // Left-button widths (single-row estimate; multipleRows may change muteSoloRectWidth)
            layout.editRectWidth = browserSettings.showOpenEditor ? buttonWidth : 0f;
            bool bothMS = browserSettings.showMute && browserSettings.showSolo;
            layout.muteSoloWidthSingle = (browserSettings.showMute || browserSettings.showSolo)
                                         ? (bothMS ? 24f + MUTE_SOLO_GAP + 24f : 24f) : 0f;
            float editToMSGapEst = (layout.editRectWidth > 0 && layout.muteSoloWidthSingle > 0) ? ZoundItem_spacing : 0f;
            layout.leftTotalEst = layout.editRectWidth + editToMSGapEst + layout.muteSoloWidthSingle + LEFT_BUTTONS_TO_NAME_GAP;

            // Minimum inspector width needed to stay single-row
            layout.minInspectorWidth = 0f;
            if (browserSettings.showNameField) layout.minInspectorWidth += 120f;
            if (browserSettings.showVolume)    layout.minInspectorWidth += 120f;
            if (browserSettings.showPitch)     layout.minInspectorWidth += 120f;
            if (browserSettings.showChance)    layout.minInspectorWidth += 120f;

            // Tags zone (same formula used for estimate and final; compute once)
            // Actual pixel value is rowRect-dependent so we store the multiplier constraint here
            // and resolve it per-row. We store 0 when tags are hidden so callers can check cheaply.
            // Note: actual tagsZoneWidth is Mathf.Min(180f, rowRect.width * 0.25f) — resolved per-row.
            layout.tagsZoneWidth = browserSettings.showTags ? 1f : 0f; // 1 = "enabled", resolved per-row
            layout.tagsGap       = browserSettings.showTags ? ZoundItem_spacing : 0f;

            // Preserve lastValidSize from previous frame (struct copy keeps it)
            layout.lastValidSize = _listRowLayout.lastValidSize;

            return layout;
        }

        private void DrawZoundsSinglecolumn(Vector2 contentSize, int selectedIndex, List<Zound> filteredZounds) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            var sizeMode = browserSettings.buttonSizeMode;
            float itemWidth = browserSettings.itemWidth;

            if (sizeMode != ZoundsProject.BrowserSettings.ButtonSizeMode.Fixed) {
                // Use the ZoundBtn label style so padding is included in the measured width.
                var btnStyle = ZUI.GetButtonStyle(ZUI.Style.ZoundBtn);
                float maxW = 0f;
                foreach (var z in filteredZounds) {
                    zoundButtonContent.text = z.name;
                    maxW = Mathf.Max(maxW, btnStyle.CalcSize(zoundButtonContent).x);
                }
                if (sizeMode == ZoundsProject.BrowserSettings.ButtonSizeMode.Min) {
                    maxW = Mathf.Max(maxW, itemWidth);
                }
                itemWidth = maxW;
            }

            // Compute the settings-derived layout once — identical for every row in this pass.
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
                            if (filteredZounds[i] == selectedZound) {
                                selectedIndex = i;
                            }
                            DrawSinglecolumnRow(filteredZounds, selectedIndex, i);
                            if (i < filteredZounds.Count - 1) {
                                ZUI.RowSpace(0.5f);
                            }
                            i++;
                        }
                    }
                }
                else {
                    for (int i = 0; i < filteredZounds.Count; i++) {
                        DrawSinglecolumnRow(filteredZounds, selectedIndex, i);
                        if (i < filteredZounds.Count - 1) {
                            ZUI.RowSpace(0.5f);
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        // ── ZoundListRowLayout ────────────────────────────────────────────────────
        // Holds all geometry for a single-column row draw pass.
        // Settings-derived fields are computed once before the row loop in
        // DrawZoundsSinglecolumn (they are identical for every row in a pass).
        // Rect-derived fields are resolved per-row inside DrawSinglecolumnRow once
        // GUILayoutUtility.GetRect has returned the actual row origin.
        // lastValidSize persists across frames as a struct field (not recreated per frame)
        // so it survives the Layout→Repaint cycle where GetRect may return zero-size.
        internal struct ZoundListRowLayout {
            // ── Settings-derived (computed before the loop) ───────────────────────
            public float itemWidth;
            public float editRectWidth;
            public float muteSoloWidthSingle;   // M/S width before multipleRows is known
            public float removeRectWidth;
            public float minInspectorWidth;
            public float tagsZoneWidth;         // final tags zone (same formula used twice; computed once)
            public float tagsGap;
            public float leftTotalEst;          // used to decide multipleRows

            // ── Frame-persistent cache ────────────────────────────────────────────
            public Vector2 lastValidSize;       // survives Layout event returning zero-size rects

            // ── Rect-derived (resolved per-row after GetRect) ─────────────────────
            public bool  multipleRows;
            public float muteSoloRectWidth;     // final, after multipleRows decision
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

        // Single layout instance — struct field on the class so lastValidSize persists.
        private ZoundListRowLayout _listRowLayout;

        /// <summary>
        /// Draws one full singlecolumn row for the zound at filteredList[currentIndex].
        ///
        /// Single-row layout (window wide enough):
        ///   [Edit][M/S] | [ZoundButton] | [Name][Vol][Pitch][Chance][Tags] | [Rout][Conv][Dup][Del]
        ///
        /// Two-row layout (multipleRows == true, window too narrow for all fields):
        ///   Left zone  (full height): Edit + M/S stacked
        ///   Middle row 1: [ZoundButton] [Rout] [Conv] [Dup] [Del]
        ///   Middle row 2: [Name] [Vol] [Pitch] [Chance]
        ///   Right zone (full height, tags only): [Tags]
        /// </summary>
        protected void DrawSinglecolumnRow(List<Zound> filteredList, int selectedIndex, int currentIndex) {
            var currentZound    = filteredList[currentIndex];
            var browserSettings = ZoundsProject.Instance.browserSettings;
            ref var layout      = ref _listRowLayout;

            // ── Resolve rect-dependent fields ─────────────────────────────────────
            // GetRect gives us the row origin; everything below depends on it.

            Rect rowRect;
            try { rowRect = GUILayoutUtility.GetRect(1, ROW_HEIGHT, GUILayout.ExpandWidth(true)); }
            catch { rowRect = new Rect(); }
            if (rowRect.width  > 1f) layout.lastValidSize.x = rowRect.width;
            if (rowRect.height > 1f) layout.lastValidSize.y = rowRect.height;
            rowRect.size = layout.lastValidSize;
            layout.rowRect = rowRect;

            // Missing zounds are always single-row — they show only name + add + delete.
            bool isMissingZoundEarly = !(currentZound is ClipZound) && currentZound.id == 0;

            // Tags zone: resolve actual pixel width now that we have rowRect.width.
            float tagsZoneWidth = layout.tagsZoneWidth > 0f ? Mathf.Min(180f, rowRect.width * 0.25f) : 0f;
            float tagsGap       = tagsZoneWidth > 0f ? layout.tagsGap : 0f;
            layout.tagsZoneWidth = tagsZoneWidth;
            layout.tagsGap       = tagsGap;

            // Decide single-row vs two-row.
            float tagsEstWidth       = tagsZoneWidth;
            float availableForFields = rowRect.width - layout.leftTotalEst - layout.itemWidth
                                       - layout.removeRectWidth - tagsEstWidth
                                       - (tagsEstWidth > 0f ? ZoundItem_spacing : 0f)
                                       - ZoundItem_spacing * 2f;
            layout.multipleRows = !isMissingZoundEarly && availableForFields < layout.minInspectorWidth;

            layout.muteSoloRectWidth = layout.multipleRows
                ? (browserSettings.showMute || browserSettings.showSolo ? 24f : 0f)
                : layout.muteSoloWidthSingle;
            layout.editToMSGap    = (layout.editRectWidth > 0 && layout.muteSoloRectWidth > 0) ? ZoundItem_spacing : 0f;
            layout.leftButtonsWidth = layout.editRectWidth + layout.editToMSGap + layout.muteSoloRectWidth;
            layout.leftGap          = layout.leftButtonsWidth > 0 ? LEFT_BUTTONS_TO_NAME_GAP : 0f;

            // middleRight: x-boundary between middle zone and tags zone.
            layout.middleRight = rowRect.xMax - tagsZoneWidth - tagsGap;

            // Always reserve the second row in GUILayout — Unity requires the same layout call count
            // every frame. Zero height when not needed avoids a visible gap while keeping the
            // control count constant even as multipleRows flips on window resize.
            float row2Gap    = layout.multipleRows ? MUTE_SOLO_GAP : 0f;
            float row2Height = layout.multipleRows ? ROW_HEIGHT    : 0f;
            GUILayout.Space(row2Gap);
            Rect row2Rect;
            try { row2Rect = GUILayoutUtility.GetRect(1, row2Height, GUILayout.ExpandWidth(true)); }
            catch { row2Rect = new Rect(rowRect.x, rowRect.yMax + row2Gap, rowRect.width, row2Height); }
            layout.row2Rect = row2Rect;

            if (layout.multipleRows) {
                float row1MiddleX    = rowRect.x + layout.leftButtonsWidth + layout.leftGap;
                float row1RightStart = layout.middleRight - layout.removeRectWidth;
                layout.nameButtonRect  = new Rect(row1MiddleX, rowRect.y, row1RightStart - row1MiddleX - ZoundItem_spacing, rowRect.height);
                layout.removeButtonRect = new Rect(row1RightStart, rowRect.y, layout.removeRectWidth, rowRect.height);
                float fieldsX          = row2Rect.x + layout.leftButtonsWidth + layout.leftGap;
                layout.inspectorRect   = new Rect(fieldsX, row2Rect.y, layout.middleRight - fieldsX, row2Rect.height);
                layout.tagsRect        = new Rect(layout.middleRight + tagsGap, rowRect.y, tagsZoneWidth, row2Rect.yMax - rowRect.y);
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

            // ── Draw ──────────────────────────────────────────────────────────────

            ZoundListItemView.Draw(currentZound, ref layout, zoundBrowserEditor, this);
        }

        #endregion SINGLECOLUMN

        /// <summary>
        /// Builds a comma-separated display string of the zound's tags, e.g. "sfx:footstep, sfx:wood".
        /// Returns "-Untagged-" when the zound has no tags or all tag IDs are stale/missing.
        /// Used by both the tags field in the inspector and the tags height calculation for dynamic row sizing.
        /// </summary>
        public static string GetZoundTagsString(Zound zoundToInspect) {
            string tagsString;
            if (zoundToInspect.tags.Count > 0) {
                var projectTags = ZoundsProject.Instance.zoundLibrary.tags;
                StringBuilder tagsBuilder = new StringBuilder();
                for (int i = 0; i < zoundToInspect.tags.Count; i++) {
                    var tag = projectTags.Find(t => t.id == zoundToInspect.tags[i]);
                    if (tag == null) continue;
                    tagsBuilder.Append(tag.name);
                    if (i < zoundToInspect.tags.Count - 1) {
                        tagsBuilder.Append(", ");
                    }
                }
                tagsString = tagsBuilder.ToString();
                if (string.IsNullOrEmpty(tagsString)) {
                    tagsString = "-Untagged-";
                }
            }
            else {
                tagsString = "-Untagged-";
            }

            return tagsString;
        }

#if ZOUNDS_CONSIDER_FOLDERS
        private string foldersSearchText = "";
#endif
        private string tagsSearchText = "";
        private string referencesSearchText = "";
        private void DrawFilterFields() {
            var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
            GUILayout.BeginVertical();
            //GUILayout.Space(7f);
            var settings = ZoundsProject.Instance.browserSettings;
            if (settings.showSearch) {
                GUILayout.BeginHorizontal();
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 38f;
                EditorGUI.BeginChangeCheck();
                {
                    var newSearchText = EditorGUILayout.TextField(filterLabel, zoundTabProperties.searchText);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                        zoundTabProperties.searchText = newSearchText;
                        zoundTabProperties.dirty = true;
                        EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                    }
                    EditorGUIUtility.labelWidth = labelWidth;
                    if (GUILayout.Button("X", GUILayout.Width(22f)) && Event.current.button == 0) {
                        Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                        zoundTabProperties.ClearFilters();
                        zoundTabProperties.dirty = true;
                        EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                        ClearFocus();
                    }
                }
                GUILayout.EndHorizontal();
            }


            GUILayout.BeginHorizontal();
            {
                if (settings.showSearch) GUILayout.Space(43f);

                // Precompute first/last for corner masks in this filter row.
                bool[] ffVisible = { settings.showTypes && !settings.typesInlineToggle, settings.showTagsFilter, settings.showReferences };
                int ffFirst = System.Array.FindIndex(ffVisible, v => v);
                int ffLast  = System.Array.FindLastIndex(ffVisible, v => v);
                int ffIdx   = -1;
                ZUICornerMask FfMask() {
                    if (ffFirst == ffLast) return ZUICornerMask.All;
                    if (ffIdx == ffFirst)  return ZUICornerMask.Left;
                    if (ffIdx == ffLast)   return ZUICornerMask.Right;
                    return ZUICornerMask.None;
                }

                if (settings.showTypes) {
                    if (zoundTabProperties.selectedTypes.HasFlag(ZoundType.Everything)) {
                        zoundTabProperties.selectedTypes = ZoundType.None;
                    }
                    if (settings.typesInlineToggle) {
                        DrawTypesInlineToggle(zoundTabProperties);
                    }
                    else {
                    bool typesActive2 = zoundTabProperties.selectedTypes != ZoundType.None;
                    ffIdx = 0;
                    if (ZUI.Button("Types", typesActive2 ? ZUI.Style.Active : ZUI.Style.Default, FfMask())) {
                        var menu = new GenericMenu();
                        AddTypeMenuItem(menu, zoundTabProperties, ZoundType.Klip);
                        AddTypeMenuItem(menu, zoundTabProperties, ZoundType.Zequence);
                        AddTypeMenuItem(menu, zoundTabProperties, ZoundType.AudioClip);
                        AddTypeMenuItem(menu, zoundTabProperties, ZoundType.Missing);

                        GenericMenuPopup.Show(
                            menu,
                            "Select Types",
                            Event.current.mousePosition,
                            new List<string>(),
                            "",
                            null,
                            null, 3, true,
                            ZoundsEditorPresets.Instance.typesPresets
                            );
                    }
                    } // end else (dropdown mode)
                }

                if (settings.showTagsFilter) {
                    ffIdx = 1;
                    var selectedTags = zoundTabProperties.selectedTags;
                    bool tagsActive2 = selectedTags.Count > 0;
                    if (ZUI.Button("Tags", tagsActive2 ? ZUI.Style.Active : ZUI.Style.Default, FfMask())) {
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
                                        if ((bool)selected) {
                                            if (!selectedTags.Contains(keyTag)) selectedTags.Add(keyTag);
                                        }
                                        else {
                                            selectedTags.RemoveAll(t => t == keyTag);
                                        }
                                        EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                        zoundTabProperties.dirty = true;
                                    }, on2);
                                }
                            }
                            menu.AddItem(new GUIContent(tagName), on, selected => {
                                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected tags");
                                if ((bool)selected) {
                                    if (!selectedTags.Contains(tagName)) selectedTags.Add(tagName);
                                }
                                else {
                                    selectedTags.RemoveAll(t => t == tagName);
                                }
                                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                zoundTabProperties.dirty = true;
                            }, on);
                        }

                        TagMenuPopup.ShowTagMenu(
                            menu,
                            "Select Tags",
                            Event.current.mousePosition,
                            new List<string>(),
                            tagsSearchText,
                            newSearch => tagsSearchText = newSearch,
                            null, 3, true,
                            ZoundsEditorPresets.Instance.tagsPresets);
                    }
                }

                if (settings.showReferences) {
                    ffIdx = 2;
                    var selectedReferences = zoundTabProperties.selectedReferences;
                    bool refsActive2 = selectedReferences.Count > 0;
                    if (ZUI.Button("References", refsActive2 ? ZUI.Style.Active : ZUI.Style.Default, FfMask())) {
                        var menu = new GenericMenu();
                        ZoundsProject.Instance.zoundLibrary.ForEachZound(z => {
                            int zoundId = z.id;
                            bool on = selectedReferences.Contains(zoundId);
                            string displayName = z.name;
                            menu.AddItem(new GUIContent(displayName), on, selected => {
                                Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected zounds");
                                if ((bool)selected) {
                                    if (!selectedReferences.Contains(zoundId)) selectedReferences.Add(zoundId);
                                }
                                else {
                                    selectedReferences.RemoveAll(id => id == zoundId);
                                }
                                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                zoundTabProperties.dirty = true;
                            }, on);
                        });

                        GenericMenuPopup.Show(
                            menu,
                            "Select References",
                            Event.current.mousePosition,
                            new List<string>(),
                            referencesSearchText,
                            newSearch => referencesSearchText = newSearch,
                            null, 3, true,
                            ZoundsEditorPresets.Instance.referencesPresets);
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

                if (settings.showGroupBy) {
                    GUILayout.BeginVertical(GUILayout.Height(40f));
                    {
                        EditorGUILayout.LabelField("Group By:", GUILayout.Width(88f));
                        var groupBy = (GroupBy)EditorGUILayout.EnumPopup(zoundTabProperties.groupBy, GUILayout.Width(88f));
                        if (groupBy != zoundTabProperties.groupBy) {
                            Undo.RecordObject(ZoundsWindowProperties.Instance, "change group by");
                            zoundTabProperties.groupBy = groupBy;
                            EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                        }
                    }
                    GUILayout.EndVertical();
                }
        }

        private static void DrawTypesInlineToggle(ZoundsWindowProperties.ZoundTabProperties zoundTabProperties) {
            // Claim a flex rect that expands to fill the same share as a Tags/References button.
            // Height is fixed at 30f to match the toolbar row; width expands via ExpandWidth.
            const float innerGap = 2f;
            var totalRect = GUILayoutUtility.GetRect(10f, 30f, GUILayout.ExpandWidth(true));
            float btnW = (totalRect.width  - innerGap) * 0.5f;
            float btnH = (totalRect.height - innerGap) * 0.5f;

            var rects = new Rect[4];
            rects[0] = new Rect(totalRect.x,          totalRect.y,          btnW, btnH); // Klip
            rects[1] = new Rect(totalRect.x + btnW + innerGap, totalRect.y, btnW, btnH); // Zeq
            rects[2] = new Rect(totalRect.x,          totalRect.y + btnH + innerGap, btnW, btnH); // Clip
            rects[3] = new Rect(totalRect.x + btnW + innerGap, totalRect.y + btnH + innerGap, btnW, btnH); // Miss

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
                if ((bool)selected) {
                    zoundTabProperties.selectedTypes |= t;
                }
                else {
                    zoundTabProperties.selectedTypes &= ~t;
                }
                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                zoundTabProperties.dirty = true;
            }, zoundTabProperties.selectedTypes.HasFlag(t));
        }


        public void SelectZound(Zound zound) {
            selectedZound = zound;
            if (zound == null) {
                inspectorAnimFloat.value = inspectorAnimFloat.value;
                inspectorAnimFloat.target = 0f;
                inspectorAnimFloat.speed = 4f;
            }
            else {
                inspectorAnimFloat.value = 0f;
                inspectorAnimFloat.target = inspectorHeight;
                inspectorAnimFloat.speed = 4f;
            }
        }

        protected void SortZounds() {
            zounds = zounds.OrderBy(it => it.name).ToList();
        }

        private List<Zound> GetFilteredZounds() {
            return filterEngine.GetFilteredZounds(zoundsToDisplay, zoundTabProperties);
        }

        protected virtual void HandleAddNew() { Debug.Log("HandleAddNew in this tab is not yet implemented."); }
        public virtual void OpenZoundEditor(Zound zound) { Debug.Log("OpenZoundEditor in this tab is not yet implemented."); }
        protected virtual void ClearFocus() { GUI.FocusControl(null); }

    }

}