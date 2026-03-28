using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Audio;
using static Zounds.ZoundsWindowProperties.ZoundTabProperties;

namespace Zounds {

    internal static class ZoundsEditorColors {
        internal static Color flashColorStart = new Color(0.5f, 0.5f, 0.8f, 1f);
        internal static Color flashColorEnd = new Color(0.7f, 0.7f, 0.9f, 1f);
        internal static Color flashColorStartSelected = new Color(0.7f, 0.7f, 0.9f, 1f);
        internal static Color flashColorEndSelected = new Color(0.9f, 0.9f, 1f, 1f);
        internal static Color flashColorStartMuted = new Color(0.8f, 0.5f, 0.5f, 1f);
        internal static Color flashColorEndMuted = new Color(0.9f, 0.7f, 0.7f, 1f);
        internal static Color clipFlashColorStartSelected = new Color(0f, 0.7f, 0.9f, 1f);
        internal static Color clipFlashColorEndSelected = new Color(0f, 0.9f, 1f, 1f);
        internal static Color clipFlashColorStart = new Color(0f, 0.5f, 0.8f, 1f);
        internal static Color clipFlashColorEnd = new Color(0f, 0.7f, 0.9f, 1f);
    }

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
        internal const float MUTE_SOLO_GAP       = 5f;   // Gap between M and S buttons.
        internal const float ZoundItem_spacing   = 10f;   // Gap between separate button groups in a zound item.
        internal const float ZoundButton_Spacing = 15f;   // Gap between the name button and adjacent buttons (M/S and right group).

        // Aliases for call sites that use the old names — map onto the two-gap model.
        internal const float LEFT_BUTTONS_TO_NAME_GAP  = ZoundButton_Spacing;   // M/S → name button
        internal const float INSPECTOR_TO_REMOVE_GAP   = ZoundItem_spacing;   // inspector fields → right button group
        internal const float NAME_TO_INSPECTOR_GAP     = ZoundButton_Spacing;   // name button → inspector fields

        private Zound selectedZound;
        private Vector2 scrollPos;
        protected AnimFloat inspectorAnimFloat = new AnimFloat(0f);
        private ZoundInspector<TZound> zoundInspector;
        private GUIContent zoundButtonContent = new GUIContent();
        private GUIContent tempContent = new GUIContent();

        private GUIContent icon_addNew;
        private GUIContent[] icon_columns;

        private GUIContent filterLabel = new GUIContent("Filter:");

        internal List<Zound> filterCache = null;
        private GroupBy prevGroupBy;
        private List<KeyValuePair<string, List<Zound>>> groupCache = null;

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
            zoundInspector = new ZoundInspector<TZound>(this);

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
            // ──────────────────────────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical();
                {
                    // ── Search field ───────────────────────────────────────────────────
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
                                var newSearchText = EditorGUILayout.TextField("", zoundTabProperties.searchText, GUILayout.Height(22f));

                                // Ghost text — drawn as an overlay label when the field is empty and unfocused.
                                if (string.IsNullOrEmpty(zoundTabProperties.searchText) && GUI.GetNameOfFocusedControl() != "SearchField") {
                                    var lastRect = GUILayoutUtility.GetLastRect();
                                    var ghostStyle = new GUIStyle(EditorStyles.label);
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
                                if (GUILayout.Button("X", GUILayout.Width(22f), GUILayout.Height(22f)) && Event.current.button == 0) {
                                    Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                                    zoundTabProperties.ClearFilters();
                                    zoundTabProperties.dirty = true;
                                    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                                    ClearFocus();
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }

                    // ── Master Volume slider ───────────────────────────────────────────
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
                            // Slider still uses 0-1 but we display and let user input 0-100
                            float volInPercent = masterVol * 100f;
                            volInPercent = EditorGUILayout.Slider(volLabel, volInPercent, 0f, 100f, GUILayout.ExpandWidth(true));
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
                        GUILayout.Space(8f);
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

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

                // ── Add new zound button ───────────────────────────────────────────────
                if (settings.showAddZound) {
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button(icon_addNew, ZUI.Style.Confirm, GUILayout.Width(30f), GUILayout.Height(30f)) && Event.current.button == 0) {
                        HandleAddNew();
                        filterCache = null;
                    }
                }

                // ── Stop All — stops every currently playing ZoundToken ───────────────
                if (settings.showStopAll) {
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Stop All", ZUI.Style.Default, GUILayout.Width(60f), GUILayout.Height(30f))) {
                        ZoundEngine.StopAllZounds();
                    }
                }

                // ── MS Clean — clears all mute/solo flags across the entire library ────
                if (settings.showMSClean) {
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("MS Clean", ZUI.Style.Default, GUILayout.Width(65f), GUILayout.Height(30f))) {
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
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Mute Sel", ZUI.Style.Default, GUILayout.Width(65f), GUILayout.Height(30f))) {
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
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    if (ZUI.Button("Solo Sel", ZUI.Style.Default, GUILayout.Width(65f), GUILayout.Height(30f))) {
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
                        if (ZUI.Button("Types", typesActive ? ZUI.Style.Active : ZUI.Style.Default, GUILayout.Height(30f))) {
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
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    var selectedTags = zoundTabProperties.selectedTags;
                    bool tagsActive = selectedTags.Count > 0;
                    if (ZUI.Button("Tags", tagsActive ? ZUI.Style.Active : ZUI.Style.Default, GUILayout.Height(30f))) {
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
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    var selectedReferences = zoundTabProperties.selectedReferences;
                    bool refsActive = selectedReferences.Count > 0;
                    if (ZUI.Button("References", refsActive ? ZUI.Style.Active : ZUI.Style.Default, GUILayout.Height(30f))) {
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
                    if (toolbarAny) GUILayout.Space(TOOLBAR_BUTTON_GAP);
                    toolbarAny = true;
                    GUILayout.BeginHorizontal(GUILayout.Width(100f), GUILayout.Height(30f));
                    {
                        string currentGroupLabel = zoundTabProperties.groupBy == GroupBy.None ? "No Grouping" : zoundTabProperties.groupBy.ToString();
                        bool groupActive = zoundTabProperties.groupBy != GroupBy.None;
                        if (ZUI.Button(currentGroupLabel, groupActive ? ZUI.Style.Active : ZUI.Style.Default, GUILayout.Height(30f))) {
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

            GUILayout.Space(5f);

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
            GUILayout.Space(5f);
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
        // Cache is invalidated by setting groupCache = null (e.g. on library changes).
        // ──────────────────────────────────────────────────────────────────────────
        private List<Zound> EvaluateGroup(List<Zound> filteredZounds) {
            var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
            if (groupCache == null || prevGroupBy != zoundTabProperties.groupBy) {
                prevGroupBy = zoundTabProperties.groupBy;
                groupCache = new List<KeyValuePair<string, List<Zound>>>();
                if (prevGroupBy == GroupBy.None) {
                    filterCache = null;
                    filteredZounds = GetFilteredZounds();
                    groupCache = new List<KeyValuePair<string, List<Zound>>>();
                }
#if ZOUNDS_CONSIDER_FOLDERS
                else if (prevGroupBy == GroupBy.Folder) {
                    var groupTemp = new Dictionary<string, List<TZound>>();
                    var zoundsCopy = new List<TZound>();
                    zoundsCopy.AddRange(filteredZounds);
                    string[] folders = ZoundsFilter.GetFolders();
                    foreach (var folder in folders) {
                        var clips = ZoundsFilter.GetClipsAtFolder(folder);
                        var arr = zoundsCopy.ToArray();
                        foreach (var z in arr) {
                            if (IsClipContainedInZound(clips, z)) {
                                if (!groupTemp.TryGetValue(folder, out var members)) {
                                    members = new List<TZound>();
                                    groupTemp.Add(folder, members);
                                }
                                members.Add(z);
                                zoundsCopy.Remove(z);
                            }
                        }
                    }
                    if (zoundsCopy.Count > 0) {
                        if (!groupTemp.TryGetValue("[No Folder]", out var members)) {
                            members = new List<TZound>();
                            groupTemp.Add("[No Folder]", members);
                        }
                        foreach (var z in zoundsCopy) {
                            members.Add(z);
                        }
                    }
                    var sortedKeys = groupTemp.Keys.OrderBy(k => k);
                    foreach (var key in sortedKeys) {
                        var members = groupTemp[key].Distinct().ToList();
                        groupCache.Add(new KeyValuePair<string, List<TZound>>(key, members));
                    }
                    filterCache = new List<TZound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                    filteredZounds = filterCache;
                }
#endif

                else if (prevGroupBy == GroupBy.Tags) {
                    var groupTemp = new Dictionary<string, Dictionary<int, List<Zound>>>();
                    var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                    foreach (var z in filterCache) {
                        if (z.tags == null || z.tags.Count == 0) {
                            if (!groupTemp.TryGetValue("-Untagged-", out var members)) {
                                members = new Dictionary<int, List<Zound>>();
                                groupTemp.Add("-Untagged-", members);
                            }
                            if (!members.TryGetValue(0, out var sorted)) {
                                sorted = new List<Zound>();
                                members.Add(0, sorted);
                            }
                            sorted.Add(z);
                        }
                        else {
                            foreach (var tagId in z.tags) {
                                if (zoundLibrary.TryGetTag(tagId, out var tag)) {
                                    string tagName = tag.name;
                                    var splits = tagName.Split(':');
                                    if (splits.Length > 1) {
                                        tagName = splits[0];
                                    }
                                    if (!groupTemp.TryGetValue(tagName, out var members)) {
                                        members = new Dictionary<int, List<Zound>>();
                                        groupTemp.Add(tagName, members);
                                    }
                                    if (!members.TryGetValue(tagId, out var sorted)) {
                                        sorted = new List<Zound>();
                                        members.Add(tagId, sorted);
                                    }
                                    sorted.Add(z);
                                }
                            }
                        }
                    }
                    var sortedKeys = groupTemp.Keys.OrderBy(k => k);
                    foreach (var key in sortedKeys) {
                        var members = groupTemp[key].Distinct().ToList();
                        var sortedMembers = new List<Zound>();
                        foreach (var kvp in members) {
                            sortedMembers.AddRange(kvp.Value);
                        }
                        groupCache.Add(new KeyValuePair<string, List<Zound>>(key, sortedMembers));
                    }
                    filterCache = new List<Zound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                    filteredZounds = filterCache;
                }

                else if (prevGroupBy == GroupBy.References) {
                    var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                    var referenceCount = new Dictionary<Zound, int>();
                    foreach (var z in filterCache) {
                        if (referenceCount.ContainsKey(z)) continue;
                        referenceCount.Add(z, 0);
                    }
                    var uniqueZounds = referenceCount.Keys.ToArray();
                    foreach (var z in uniqueZounds) {
                        zoundLibrary.ForEachZound(otherZound => {
                            if (otherZound.HasDirectDependency(z) || otherZound.HasNestedDependency(z)) {
                                referenceCount[z]++;
                            }
                        });
                    }
                    int[] sortedCount = referenceCount.Values.Distinct().OrderByDescending(c => c).ToArray();
                    foreach (var count in sortedCount) {
                        var zoundMembers = new List<Zound>();
                        foreach (var kvp in referenceCount) {
                            if (kvp.Value != count) continue;
                            zoundMembers.Add(kvp.Key);
                        }
                        zoundMembers = zoundMembers.Distinct().ToList();
                        groupCache.Add(new KeyValuePair<string, List<Zound>>(count.ToString(), zoundMembers));
                    }
                    filterCache = new List<Zound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                    filteredZounds = filterCache;
                }

                else if (prevGroupBy == GroupBy.MixerGroup) {
                    var zoundsProject = ZoundsProject.Instance;
                    var zoundLibrary = zoundsProject.zoundLibrary;
                    var zoundRoutings = zoundsProject.zoundRoutings;
                    var referenceCount = new Dictionary<Zound, int>();

                    var zoundsByMixerGroupName = new Dictionary<string, List<Zound>>();
                    var unroutedZounds = new List<Zound>();

                    foreach (var z in filterCache) {
                        if (z.manuallySetMixerGroupRef != null && z.editor_hasManuallySetRouting) {
                            string mixerGroupName = z.manuallySetMixerGroupRef.SubObjectName;
                            if (!zoundsByMixerGroupName.ContainsKey(mixerGroupName)) {
                                zoundsByMixerGroupName.Add(mixerGroupName, new List<Zound>());
                            }
                            zoundsByMixerGroupName[mixerGroupName].Add(z);
                            continue;
                        }
                        var matchingRule = zoundRoutings.FindMatchingRoutingRule(z);
                        if (matchingRule != null && matchingRule.mixerGroupRef != null) {
                            string mixerGroupName = matchingRule.mixerGroupRef.SubObjectName;
                            if (!zoundsByMixerGroupName.ContainsKey(mixerGroupName)) {
                                zoundsByMixerGroupName.Add(mixerGroupName, new List<Zound>());
                            }
                            zoundsByMixerGroupName[mixerGroupName].Add(z);
                        }
                        else {
                            unroutedZounds.Add(z);
                        }
                    };

                    string[] sortedMixerGroupNames = zoundsByMixerGroupName.Keys.OrderBy(n => n).ToArray();
                    foreach (var mixerGroupName in sortedMixerGroupNames) {
                        var zoundMembers = zoundsByMixerGroupName[mixerGroupName].OrderBy(z => z.name).ToList();
                        groupCache.Add(new KeyValuePair<string, List<Zound>>(mixerGroupName, zoundMembers));
                    }
                    groupCache.Add(new KeyValuePair<string, List<Zound>>("-Unrouted-", unroutedZounds));
                    filterCache = new List<Zound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                    filteredZounds = filterCache;
                }

                else if (prevGroupBy == GroupBy.Type) {
                    var zoundsProject = ZoundsProject.Instance;
                    var zoundLibrary = zoundsProject.zoundLibrary;
                    var zoundRoutings = zoundsProject.zoundRoutings;

                    var audioClipList = new List<Zound>();
                    var klipList = new List<Zound>();
                    var missingList = new List<Zound>();
                    var zequenceList = new List<Zound>();

                    foreach (var z in filterCache) {
                        if (z is ClipZound) audioClipList.Add(z);
                        else if (z is Klip) klipList.Add(z);
                        else if (z is Zequence) zequenceList.Add(z);
                        else missingList.Add(z);
                    };

                    if (audioClipList.Count > 0)
                        groupCache.Add(new KeyValuePair<string, List<Zound>>("AudioClip", audioClipList));
                    if (klipList.Count > 0)
                        groupCache.Add(new KeyValuePair<string, List<Zound>>("Klip", klipList));
                    if (zequenceList.Count > 0)
                        groupCache.Add(new KeyValuePair<string, List<Zound>>("Zequence", zequenceList));
                    if (missingList.Count > 0)
                        groupCache.Add(new KeyValuePair<string, List<Zound>>("Missing", missingList));
                    filterCache = new List<Zound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                    filteredZounds = filterCache;
                }
            }

            return filteredZounds;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GRID MODE RENDERING
        // In grid mode the zound list is drawn as a grid of name-only buttons.
        // Each button is ROW_HEIGHT tall and itemWidth wide (BrowserSettings.itemWidth).
        // Right-clicking a button selects it and reveals an inspector panel (drawn by
        // ZoundInspector.DrawMulticolumn) that expands below the row.
        //
        // Three layout sub-modes exist (controlled by BrowserSettings.buttonSizeMode):
        //   Fixed  — all buttons the same fixed itemWidth; inspector shown below the row.
        //   Min    — buttons auto-sized to text but never smaller than itemWidth; flow layout.
        //   Auto   — buttons auto-sized to text; flow layout, no minimum width.
        //
        // Call graph:
        //   DrawZoundsMulticolumn
        //     ├─ DrawMulticolumnRow        (Fixed mode — draws one grid row of columnCount buttons)
        //     └─ DrawFlowRow              (Auto/Min mode — wraps buttons across horizontal lines)
        //          └─ HandleZoundButtonMulticolumn  (draws a single zound button + handles events)
        // ══════════════════════════════════════════════════════════════════════════
        #region MULTICOLUMN
        /// <summary>
        /// Recalculates the animated inspector height for the selected zound.
        /// Called each frame in Fixed mode so the tags area can expand if the tag text wraps.
        /// The AnimFloat smoothly animates the panel open/closed.
        /// </summary>
        private void UpdateInspectorHeight(Zound selected) {
            float lastTagsWidth = zoundInspector.GetLastTagsWidth();
            if (lastTagsWidth > 0f) {
                tempContent.text = GetZoundTagsString(selected);
                float newHeight = zoundInspector.GetTagsLabelStyle().CalcHeight(tempContent, lastTagsWidth);
                float oldVal = inspectorAnimFloat.target;
                inspectorAnimFloat.target = Mathf.Max(inspectorHeight, newHeight);
                if (inspectorAnimFloat.target != oldVal) {
                    inspectorAnimFloat.value = inspectorAnimFloat.target;
                }
            }
        }

        /// <summary>
        /// Draws a wrapping flow-layout row for Auto/Min button size modes.
        /// Buttons are laid out left-to-right; when the next button would overflow maxWidth
        /// a new GUILayout horizontal row is started. Each button delegates to
        /// HandleZoundButtonMulticolumn for rendering and input handling.
        /// totalIndex is passed by ref so the caller's global index stays in sync.
        /// </summary>
        private void DrawFlowRow(List<Zound> zounds, int selectedIndex, ref int totalIndex, float maxWidth) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            var sizeMode = browserSettings.buttonSizeMode;
            float minWidth = browserSettings.itemWidth;
            var btnStyle = ZUI.GetButtonStyle(ZUI.Style.ZoundBtn);

            float currentX = 0;
            bool rowStarted = false;
            bool inspectorPending = false; // draw inspector after closing the current horizontal row

            for (int i = 0; i < zounds.Count; i++) {
                var zound = zounds[i];
                zoundButtonContent.text = zound.name;
                float requiredWidth = btnStyle.CalcSize(zoundButtonContent).x;

                if (sizeMode == ZoundsProject.BrowserSettings.ButtonSizeMode.Min) {
                    requiredWidth = Mathf.Max(requiredWidth, minWidth);
                }

                if (!rowStarted) {
                    GUILayout.BeginHorizontal();
                    rowStarted = true;
                    currentX = 0;
                }

                if (currentX + requiredWidth > maxWidth - 40f && currentX > 0) {
                    GUILayout.EndHorizontal();
                    rowStarted = false;
                    if (inspectorPending) {
                        int localIdx = selectedIndex - (totalIndex - i);
                        if (localIdx >= 0 && localIdx < zounds.Count) {
                            UpdateInspectorHeight(zounds[localIdx]);
                            zoundInspector.DrawMulticolumn(zounds[localIdx], inspectorAnimFloat.value);
                        }
                        inspectorPending = false;
                    }
                    GUILayout.Space(MULTICOLUMN_V_GAP);
                    GUILayout.BeginHorizontal();
                    rowStarted = true;
                    currentX = 0;
                }

                int currentIndex = totalIndex;
                bool hasAnyInstancePlaying = TryGetAnyInstanceToken(zound, out var token);
                bool isClipZoundG = zound.IsClipOrLocalZound();
                UpdateZoundButtonPulse(zound, isClipZoundG, hasAnyInstancePlaying, token);
                if (!isClipZoundG && zound.id == 0) { /* missing zound — box handles its own style */ }
                else if (zound is Klip klipG && (klipG.audioClipRef == null || !klipG.audioClipRef.RuntimeKeyIsValid() || klipG.audioClipRef.editorAsset == null)) GUI.color = new Color(1f, 0.4f, 0f, 1f);
                else if (hasAnyInstancePlaying) {
                    if      (token.state == ZoundToken.State.Paused)   GUI.color = new Color(0.9f, 0.5f, 0.9f, 1f);
                    else if (token.audioSource.volume < Mathf.Epsilon) GUI.color = new Color(0.9f, 0.5f, 0.1f, 1f);
                    else if (selectedIndex == currentIndex) GUI.color = isClipZoundG ? ZoundsEditorColors.clipFlashColorStartSelected : ZoundsEditorColors.flashColorStartSelected;
                    else if (isClipZoundG)                              GUI.color = ZoundsEditorColors.clipFlashColorStart;
                }
                else if (selectedIndex == currentIndex) GUI.color = isClipZoundG ? Color.cyan : ZoundsEditorColors.flashColorStartSelected;
                else if (isClipZoundG)                  GUI.color = Color.cyan;
                if (currentX > 0) GUILayout.Space(MULTICOLUMN_H_GAP);
                HandleZoundButtonMulticolumn(zounds, selectedIndex, i, requiredWidth, token, Event.current);
                GUI.color = Color.white;

                if (selectedIndex == currentIndex) inspectorPending = true;

                currentX += requiredWidth + MULTICOLUMN_H_GAP;
                totalIndex++;
            }

            if (rowStarted) {
                GUILayout.EndHorizontal();
                if (inspectorPending && selectedIndex >= 0 && selectedIndex < totalIndex) {
                    int localIdx = selectedIndex - (totalIndex - zounds.Count);
                    if (localIdx >= 0 && localIdx < zounds.Count) {
                        UpdateInspectorHeight(zounds[localIdx]);
                        zoundInspector.DrawMulticolumn(zounds[localIdx], inspectorAnimFloat.value);
                    }
                }
            }
        }

        /// <summary>
        /// Sets GUI.color before drawing a zound button to communicate its state at a glance.
        /// Priority order (highest wins):
        ///   Red       — missing zound (id == 0, not a ClipZound)
        ///   Orange    — Klip with a missing/invalid audio clip reference
        ///   Pulsing   — currently playing (animates between two colours via a yoyo lerp)
        ///   Selected  — right-click-selected, not playing
        ///   Cyan      — ClipZound (raw AudioClip, not a Klip)
        ///   Default   — white (no tint)
        /// Caller must reset GUI.color = Color.white after drawing.
        /// </summary>
        // Returns a stable pulse key for a zound button. Uses object identity (not zound.id,
        // which is 0 for all ClipZounds and therefore not unique).
        private static string ZoundPulseKey(Zound zound)
            => "zound:btn:" + RuntimeHelpers.GetHashCode(zound);

        // Starts or stops the ZUI pulse for a zound button based on current token state.
        // Call once per repaint before drawing the button. DrawPulse is called after.
        private static void UpdateZoundButtonPulse(Zound zound, bool isClipZound, bool hasToken, ZoundToken token) {
            string key = ZoundPulseKey(zound);
            if (hasToken && token.state != ZoundToken.State.Paused && token.audioSource.volume >= Mathf.Epsilon) {
                if (!ZUI.IsPulsing(key)) {
                    // Fill: dim multiply tint so controls remain readable.
                    // Border: bright version of the same color for a crisp outline.
                    var baseColor = isClipZound
                        ? ZoundsEditorColors.clipFlashColorEnd
                        : token.audioSource.mute
                            ? ZoundsEditorColors.flashColorEndMuted
                            : ZoundsEditorColors.flashColorEnd;
                    var fillColor   = new Color(baseColor.r, baseColor.g, baseColor.b, 0.25f);
                    var borderColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                    ZUI.StartPulse(key, new ZUI.PulseParams {
                        color         = fillColor,
                        borderColor   = borderColor,
                        mode          = ZUI.PulseMode.FillAndBorder,
                        blend         = ZUI.PulseBlend.Alpha,
                        cycleDuration = 0.5f,
                        totalDuration = float.PositiveInfinity,
                        borderWidth   = 3f,
                    });
                }
            }
            else {
                ZUI.StopPulse(key);
            }
        }


        /// <summary>
        /// Top-level multicolumn draw method. Wraps everything in a scrollview and dispatches to
        /// either the Fixed-mode grid renderer (DrawMulticolumnRow) or the Auto/Min flow renderer
        /// (DrawFlowRow). Also handles group headers when GroupBy is active.
        ///
        /// In Fixed mode the inspector panel (ZoundInspector.DrawMulticolumn) is inserted
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
            int inspectorRowIndex;
            if (selectedIndex < 0) {
                inspectorRowIndex = -1;
            }
            else {
                inspectorRowIndex = Mathf.FloorToInt(selectedIndex / (float)columnCount);
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            {
                if (groupCache != null && groupCache.Count > 0) {
                    foreach (var kvp in groupCache) {
                        EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
                        int memberCount = kvp.Value.Count;
                        bool firstGroupRow = true;
                        while (memberCount > 0) {
                            int colCount = memberCount > columnCount ? columnCount : memberCount;
                            if (prevGroupBy == GroupBy.Tags) {
                                // Exception, because this one supports zounds to exist in multiple tag groups.
                                for (int i=0; i<colCount; i++) {
                                    int index = zoundIndex + i;
                                    if (index < filteredZounds.Count && filteredZounds[index] == selectedZound) {
                                        selectedIndex = index;
                                    }
                                }
                            }
                            if (!firstGroupRow) GUILayout.Space(MULTICOLUMN_V_GAP);
                            firstGroupRow = false;
                            bool isRowSelected = selectedIndex >= zoundIndex && selectedIndex < zoundIndex + colCount;
                            DrawMulticolumnRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth);
                            memberCount -= columnCount;
                            if (isRowSelected) {
                                zoundInspector.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                            }
                        }
                    }
                }
                else {
                    for (int i = 0; i < rowCount; i++) {
                        if (i > 0) GUILayout.Space(MULTICOLUMN_V_GAP);
                        DrawMulticolumnRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth);
                        if (selectedIndex >= 0 && inspectorRowIndex == i) {
                            UpdateInspectorHeight(filteredZounds[selectedIndex]);
                            zoundInspector.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
            }
            else {
                // Auto or Min mode: Use Flow Layout
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                {
                    int zoundIndex = 0;
                    if (groupCache != null && groupCache.Count > 0) {
                        foreach (var kvp in groupCache) {
                            EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
                            DrawFlowRow(kvp.Value, selectedIndex, ref zoundIndex, contentSize.x);
                        }
                    }
                    else {
                        DrawFlowRow(filteredZounds, selectedIndex, ref zoundIndex, contentSize.x);
                    }
                }
                GUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// Draws one horizontal grid row in Fixed multicolumn mode.
        /// Iterates columnCount slots; empty trailing slots are drawn as invisible placeholders
        /// so the grid stays aligned when the last row isn't full.
        /// currentIndex is passed by ref and advances by columnCount on return.
        /// FlexibleSpace is added at both ends to horizontally centre the grid in the window.
        /// </summary>
        protected void DrawMulticolumnRow(List<Zound> filteredList, int selectedIndex, ref int currentIndex, int columnCount, float itemWidth) {
            GUILayout.BeginHorizontal();

            var col = GUI.color;
            var evt = Event.current;

            GUILayout.FlexibleSpace(); // center item list by adding space in the start and end.
            {
                for (int i = 0; i < columnCount; i++) {
                    if (i > 0) GUILayout.Space(MULTICOLUMN_H_GAP);
                    if (currentIndex >= filteredList.Count) {
                        GUILayoutUtility.GetRect(itemWidth, ROW_HEIGHT, GUIStyle.none, GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth), GUILayout.Height(ROW_HEIGHT));
                    }
                    else {
                        bool hasAnyInstancePlaying = TryGetAnyInstanceToken(filteredList[currentIndex], out var token);

                        bool isClipZound = filteredList[currentIndex].IsClipOrLocalZound();
                        bool isKlipIssue = filteredList[currentIndex] is Klip klip && 
                                           (klip.audioClipRef == null || !klip.audioClipRef.RuntimeKeyIsValid() || klip.audioClipRef.editorAsset == null);

                        if (!isClipZound && filteredList[currentIndex].id == 0) {
                            // missing zound — box draws its own style, no color tint needed
                        }
                        else if (isKlipIssue) {
                            GUI.color = new Color(1f, 0.4f, 0f, 1f); // Orange for data issues
                        }
                        else {
                            var zound = filteredList[currentIndex];
                            UpdateZoundButtonPulse(zound, isClipZound, hasAnyInstancePlaying, token);

                            if (selectedIndex == currentIndex) {
                                if (hasAnyInstancePlaying) {
                                    if      (token.state == ZoundToken.State.Paused)      GUI.color = new Color(0.9f, 0.5f, 0.9f, 1f);
                                    else if (token.audioSource.volume < Mathf.Epsilon)    GUI.color = new Color(0.9f, 0.5f, 0.1f, 1f);
                                    else GUI.color = isClipZound ? ZoundsEditorColors.clipFlashColorStartSelected : ZoundsEditorColors.flashColorStartSelected;
                                }
                                else {
                                    GUI.color = isClipZound ? Color.cyan : ZoundsEditorColors.flashColorStartSelected;
                                }
                            }
                            else {
                                if (hasAnyInstancePlaying) {
                                    if      (token.state == ZoundToken.State.Paused)      GUI.color = new Color(0.9f, 0.5f, 0.9f, 1f);
                                    else if (token.audioSource.volume < Mathf.Epsilon)    GUI.color = new Color(0.9f, 0.5f, 0.1f, 1f);
                                    else if (isClipZound)                                  GUI.color = ZoundsEditorColors.clipFlashColorStart;
                                }
                                else if (isClipZound) GUI.color = Color.cyan;
                            }
                        }
                        HandleZoundButtonMulticolumn(filteredList, selectedIndex, currentIndex, itemWidth, token, evt);
                        GUI.color = col;
                    }
                    currentIndex++;
                }
            }
            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a single zound name button in multicolumn mode and handles all mouse interactions.
        /// Mouse button behaviour:
        ///   Left click           — play the zound (or copy name to clipboard if Alt held, or open InfoView if Ctrl held)
        ///   Right click          — select/deselect the zound (shows/hides the inspector panel)
        ///   Middle click         — copy name to clipboard
        /// A white highlight texture is drawn behind the button when a ZoundToken is playing.
        /// </summary>
        private void HandleZoundButtonMulticolumn(List<Zound> filteredList, int selectedIndex, int currentIndex, float itemWidth, ZoundToken token, Event evt) {
            var currentZound = filteredList[currentIndex];
            var zoundName = currentZound.name;
            zoundButtonContent.text = zoundName;
            zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open configuration panel. Middle click or Alt left click to copy the name to clipboard.";

            var nameRect = GUILayoutUtility.GetRect(itemWidth, ROW_HEIGHT, ZUI.GetButtonStyle(ZUI.Style.ZoundBtn), GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth));

            ZUI.DrawPulse(ZoundPulseKey(currentZound), nameRect);
            DrawMuteSoloBackground(nameRect, currentZound);

            if (token != null) {
                if (token.isChildZound) {
                    if (!token.isDelayFinished) {
                        var highlightRect = new Rect(nameRect.x - 1f, nameRect.y - 1f, nameRect.width + 2.5f, nameRect.height + 2f);
                        var guiColor = GUI.color;
                        GUI.color = Color.yellow;
                        GUI.DrawTexture(highlightRect, EditorGUIUtility.whiteTexture);
                        GUI.color = guiColor;
                    }
                }
                else {
                    var highlightRect = new Rect(nameRect.x - 1f, nameRect.y - 1f, nameRect.width + 2.5f, nameRect.height + 2f);
                    GUI.DrawTexture(highlightRect, EditorGUIUtility.whiteTexture);
                }
            }

            bool isMissingZound = !(currentZound is ClipZound) && currentZound.id == 0;

            float pulseIG = ZUI.GetPulseIntensity(ZoundPulseKey(currentZound));
            var   prevColorG = GUI.color;
            if (pulseIG > 0f) GUI.color = Color.Lerp(GUI.color, Color.black, pulseIG * 0.6f);

            if (isMissingZound) {
                // Draw MissingZound box style instead of a button.
                // Reset GUI.color so the box and label draw with their own colors unaffected.
                GUI.color = prevColorG;
                var missingBoxDef = ZUI.ActiveSheet?.FindBox("MissingZound");
                if (missingBoxDef != null) {
                    missingBoxDef.DrawBackground(nameRect);
                    if (evt.type == EventType.Repaint) {
                        var labelStyle = new GUIStyle(EditorStyles.label);
                        missingBoxDef.GetResolvedTitleText().Apply(labelStyle);
                        labelStyle.alignment = TextAnchor.MiddleCenter;
                        labelStyle.clipping  = TextClipping.Clip;
                        GUI.Label(nameRect, zoundName, labelStyle);
                    }
                } else {
                    if (evt.type == EventType.Repaint) {
                        var fallbackStyle = new GUIStyle(EditorStyles.label);
                        fallbackStyle.normal.textColor = new Color(0.8f, 0.4f, 0.4f, 1f);
                        fallbackStyle.alignment = TextAnchor.MiddleCenter;
                        GUI.Label(nameRect, zoundName, fallbackStyle);
                    }
                }
                // Left click expands inspector (same as right-click does for normal zounds)
                if (evt.type == EventType.MouseDown && evt.button == 0 && nameRect.Contains(evt.mousePosition)) {
                    if (selectedIndex == currentIndex) SelectZound(null);
                    else SelectZound(filteredList[currentIndex]);
                    GUI.FocusControl(null);
                    evt.Use();
                }
            } else {
                if (ZUI.Button(nameRect, zoundButtonContent, ZUI.Style.ZoundBtn)) {
                    if (evt.button == 0) {
                        if (evt.alt) {
                            CopyToClipboard(zoundName);
                        }
                        else {
                            if (evt.control) {
                                InfoViewWindow.OpenWindow(currentZound);
                            }
                            else {
                                var browserSettings = ZoundsProject.Instance.browserSettings;
                                if (browserSettings.killOnPlay) {
                                    ZoundEngine.StopAllZounds();
                                }
                                ZoundEngine.PlayZound(currentZound);
                            }
                        }
                    }
                    else if (evt.button == 1) {
                        if (selectedIndex == currentIndex) SelectZound(null);
                        else SelectZound(filteredList[currentIndex]);
                    }
                    else if (evt.button == 2) {
                        CopyToClipboard(zoundName);
                    }
                    GUI.FocusControl(null);
                }
            }
            GUI.color = prevColorG;

            DrawMuteSoloIndicator(nameRect, currentZound);
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
        //          └─ ZoundInspector.DrawZoundSinglecolumn (draws edit/MS/inspector/right-buttons)
        // ══════════════════════════════════════════════════════════════════════════
        #region SINGLECOLUMN
        /// <summary>
        /// Top-level list mode draw method. Wraps the list in a scrollview and calls
        /// DrawSinglecolumnRow for each visible zound. Handles group headers when GroupBy is active.
        /// In Auto/Min size modes, itemWidth is calculated once from the widest name so all rows
        /// have a consistent name button width.
        /// </summary>
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
                            DrawSinglecolumnRow(filteredZounds, selectedIndex, i, itemWidth);
                            if (i < filteredZounds.Count - 1) {
                                GUILayout.Space(ROW_VERTICAL_GAP);
                            }
                            i++;
                        }
                    }
                }
                else {
                    for (int i = 0; i < filteredZounds.Count; i++) {
                        DrawSinglecolumnRow(filteredZounds, selectedIndex, i, itemWidth);
                        if (i < filteredZounds.Count - 1) {
                            try {
                                GUILayout.Space(ROW_VERTICAL_GAP);
                            }
                            catch { }
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        // lastValidSize caches the most recent non-zero rowRect size.
        // Unity sometimes returns zero-size rects during the layout event; the cache
        // ensures the repaint event uses sensible values even when layout is skipped.
        private Vector2 lastValidSize;

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
        protected void DrawSinglecolumnRow(List<Zound> filteredList, int selectedIndex, int currentIndex, float itemWidth) {
            var currentZound = filteredList[currentIndex];
            var browserSettings = ZoundsProject.Instance.browserSettings;
            var evt = Event.current;

            // ── Step 1: measure minimum inspector width needed for single-row ──────
            float minInspectorWidth = 0f;
            if (browserSettings.showNameField) minInspectorWidth += 120f;
            if (browserSettings.showVolume)    minInspectorWidth += 120f;
            if (browserSettings.showPitch)     minInspectorWidth += 120f;
            if (browserSettings.showChance)    minInspectorWidth += 120f;
            // Tags excluded: in two-row mode they move to their own zone.

            // ── Step 2: reserve rows in GUILayout ─────────────────────────────────
            Rect rowRect;
            try { rowRect = GUILayoutUtility.GetRect(1, ROW_HEIGHT, GUILayout.ExpandWidth(true)); }
            catch { rowRect = new Rect(); }
            if (rowRect.width  > 1f) lastValidSize.x = rowRect.width;
            if (rowRect.height > 1f) lastValidSize.y = rowRect.height;
            rowRect.size = lastValidSize;

            // ── Step 3: compute right-group width ─────────────────────────────────
            // The convert-to-Zequence button is NOT shown in list mode (conversion is done via the Klip editor).
            // We deliberately exclude it from the right-group width so Klips and Zeqs get identical layouts.
            float buttonWidth = ROW_BUTTON_WIDTH;
            float removeRectWidth = 0f;
            if (browserSettings.showRouting)   removeRectWidth += buttonWidth;
            if (browserSettings.showDuplicate) removeRectWidth += buttonWidth;
            if (browserSettings.showRemove)    removeRectWidth += buttonWidth;

            // ── Step 4: compute left-button widths ────────────────────────────────
            float editRectWidth = browserSettings.showOpenEditor ? buttonWidth : 0f;
            // In single-row mode M/S are side by side; in two-row mode stacked in 24px.
            // We estimate single-row first; multipleRows may flip this to 24f below.
            bool bothMS = browserSettings.showMute && browserSettings.showSolo;
            float muteSoloWidthSingle = (browserSettings.showMute || browserSettings.showSolo)
                                        ? (bothMS ? 24f + MUTE_SOLO_GAP + 24f : 24f) : 0f;
            float editToMSGap = (editRectWidth > 0 && muteSoloWidthSingle > 0) ? ZoundItem_spacing : 0f;
            float leftTotalEst = editRectWidth + editToMSGap + muteSoloWidthSingle + LEFT_BUTTONS_TO_NAME_GAP;

            // ── Step 5: decide single-row vs two-row layout ───────────────────────
            // Missing zounds are always single-row — they show only name + add + delete.
            bool isMissingZoundEarly = !(currentZound is ClipZound) && currentZound.id == 0;

            // Tags zone width estimate (clamped at 25% of row width, max 180px).
            // Tags are always in their own zone so we must subtract them from available space.
            float tagsEstWidth = browserSettings.showTags ? Mathf.Min(180f, rowRect.width * 0.25f) : 0f;
            float tagsEstGap   = tagsEstWidth > 0 ? ZoundItem_spacing : 0f;
            // Available width for fields = total row - left zone - name button - right group - tags zone - gaps.
            float availableForFields = rowRect.width - leftTotalEst - itemWidth - removeRectWidth
                                       - tagsEstWidth - tagsEstGap - ZoundItem_spacing * 2f; // 2 gaps around fields
            bool multipleRows = !isMissingZoundEarly && availableForFields < minInspectorWidth;

            float muteSoloRectWidth = multipleRows ? (browserSettings.showMute || browserSettings.showSolo ? 24f : 0f) : muteSoloWidthSingle;
            editToMSGap = (editRectWidth > 0 && muteSoloRectWidth > 0) ? ZoundItem_spacing : 0f;
            float leftButtonsWidth = editRectWidth + editToMSGap + muteSoloRectWidth;
            float leftGap = leftButtonsWidth > 0 ? LEFT_BUTTONS_TO_NAME_GAP : 0f;

            // ── Step 6: reserve GUILayout space and compute all rects ─────────────
            // Both single-row and two-row modes share the same zone structure:
            //   Left: Edit + M/S  |  Middle: ZoundButton + actions (row1) / fields (row2)  |  Right: Tags
            // The gap between row1 and row2 in two-row mode matches MUTE_SOLO_GAP for visual consistency.
            Rect nameButtonRect, inspectorRect, row2Rect, tagsRect;
            Rect removeButtonRect;

            // Tags zone is always shown at the right side (same width for both modes).
            float tagsZoneWidth = browserSettings.showTags ? Mathf.Min(180f, rowRect.width * 0.25f) : 0f;
            float tagsGap       = tagsZoneWidth > 0 ? ZoundItem_spacing : 0f;
            // middleRight is the x-boundary between the middle zone and the tags zone.
            float middleRight = rowRect.xMax - tagsZoneWidth - tagsGap;

            // Always reserve the second row in GUILayout — Unity requires the same layout call count
            // every frame. Using zero height when not needed avoids any visible gap while keeping
            // the control count constant even as multipleRows flips on window resize.
            float row2Gap    = multipleRows ? MUTE_SOLO_GAP : 0f;
            float row2Height = multipleRows ? ROW_HEIGHT    : 0f;
            GUILayout.Space(row2Gap);
            try { row2Rect = GUILayoutUtility.GetRect(1, row2Height, GUILayout.ExpandWidth(true)); }
            catch { row2Rect = new Rect(rowRect.x, rowRect.yMax + row2Gap, rowRect.width, row2Height); }

            if (multipleRows) {

                // Row 1 middle: ZoundButton + right-group side by side.
                float row1MiddleX    = rowRect.x + leftButtonsWidth + leftGap;
                // Gap between ZoundButton and first action button equals ZoundItem_spacing (same as between actions).
                float row1RightStart = middleRight - removeRectWidth;
                nameButtonRect  = new Rect(row1MiddleX, rowRect.y, row1RightStart - row1MiddleX - ZoundItem_spacing, rowRect.height);
                removeButtonRect = new Rect(row1RightStart, rowRect.y, removeRectWidth, rowRect.height);

                // Row 2 middle: fields fill the middle zone.
                float fieldsX = row2Rect.x + leftButtonsWidth + leftGap;
                inspectorRect = new Rect(fieldsX, row2Rect.y, middleRight - fieldsX, row2Rect.height);

                // Tags zone spans both rows (from row1.y to row2.yMax).
                tagsRect = new Rect(middleRight + tagsGap, rowRect.y,
                                    tagsZoneWidth, row2Rect.yMax - rowRect.y);
            }
            else {
                row2Rect = Rect.zero;
                // Single row: same zone layout — ZoundButton (fixed width) | fields | actions | tags.
                float row1MiddleX    = rowRect.x + leftButtonsWidth + leftGap;
                float row1RightStart = middleRight - removeRectWidth;
                nameButtonRect  = new Rect(row1MiddleX, rowRect.y, itemWidth, rowRect.height);
                // Fields fill between ZoundButton and actions, with ZoundItem_spacing gaps on both sides.
                float fieldsX     = nameButtonRect.xMax + ZoundItem_spacing;
                float fieldsWidth = row1RightStart - fieldsX - ZoundItem_spacing;
                inspectorRect    = new Rect(fieldsX, rowRect.y, Mathf.Max(0f, fieldsWidth), rowRect.height);
                removeButtonRect = new Rect(row1RightStart, rowRect.y, removeRectWidth, rowRect.height);
                // Tags zone at the right end (same position as two-row mode).
                tagsRect = tagsZoneWidth > 0
                    ? new Rect(middleRight + tagsGap, rowRect.y, tagsZoneWidth, rowRect.height)
                    : Rect.zero;
            }

            // Left-side buttons span the full height of both rows when multipleRows.
            float leftHeight = multipleRows ? (row2Rect.yMax - rowRect.y) : rowRect.height;
            Rect editButtonRect = new Rect(rowRect.x,                         rowRect.y, editRectWidth,    leftHeight);
            Rect muteSoloRect   = new Rect(editButtonRect.xMax + editToMSGap, rowRect.y, muteSoloRectWidth, leftHeight);

            // ── Step 8: draw — flat sequential calls, no layout nesting ──────────

            // Full item area (both rows when multipleRows) — used for pulse background and indicator.
            var itemAreaRect = multipleRows
                ? new Rect(rowRect.x, rowRect.y, rowRect.width, row2Rect.yMax - rowRect.y)
                : rowRect;

            // Background layers drawn first so they sit behind all controls.
            bool isClipZound    = currentZound.IsClipOrLocalZound();
            bool isMissingZound = !isClipZound && currentZound.id == 0;
            if (!isMissingZound) {
                TryGetAnyInstanceToken(currentZound, out var tokenPre);
                UpdateZoundButtonPulse(currentZound, isClipZound, tokenPre != null, tokenPre);
                ZUI.DrawPulse(ZoundPulseKey(currentZound), itemAreaRect);
            }
            DrawMuteSoloBackground(itemAreaRect, currentZound);

            // Name button: colour-coded to show play state, mute, etc. (not applied to missing zounds).
            var guiColor = GUI.color;

            if (!isMissingZound) {
                TryGetAnyInstanceToken(currentZound, out var token);
                bool hasToken = token != null;

                if (hasToken) {
                    if      (token.state == ZoundToken.State.Paused)   GUI.color = new Color(0.9f, 0.5f, 0.9f, 1f);
                    else if (token.audioSource.volume < Mathf.Epsilon) GUI.color = new Color(0.9f, 0.5f, 0.1f, 1f);
                    else    GUI.color = isClipZound ? ZoundsEditorColors.clipFlashColorStart : ZoundsEditorColors.flashColorStart;
                }
                else if (isClipZound) {
                    GUI.color = Color.cyan;
                }
            }

            var zoundName = currentZound.name;

            if (isMissingZound) {
                // Box spans the full middle zone: from after the edit button to before the remove group.
                float missingBoxLeft  = editButtonRect.xMax;
                float missingBoxRight = removeRectWidth > 0 ? removeButtonRect.x - ZoundItem_spacing : rowRect.xMax;
                var missingBoxRect = new Rect(missingBoxLeft, nameButtonRect.y, missingBoxRight - missingBoxLeft, nameButtonRect.height);
                var missingBoxDef = ZUI.ActiveSheet?.FindBox("MissingZound");
                if (missingBoxDef != null) {
                    missingBoxDef.DrawBackground(missingBoxRect);
                    if (Event.current.type == EventType.Repaint) {
                        var labelStyle = new GUIStyle(EditorStyles.label);
                        missingBoxDef.GetResolvedTitleText().Apply(labelStyle);
                        labelStyle.alignment = TextAnchor.MiddleCenter;
                        labelStyle.clipping  = TextClipping.Clip;
                        GUI.Label(missingBoxRect, zoundName, labelStyle);
                    }
                }
                else {
                    // Fallback: plain dim label when style not yet defined in sheet.
                    if (Event.current.type == EventType.Repaint) {
                        var fallbackStyle = new GUIStyle(EditorStyles.label);
                        fallbackStyle.normal.textColor = new Color(0.8f, 0.4f, 0.4f, 1f);
                        fallbackStyle.alignment = TextAnchor.MiddleCenter;
                        GUI.Label(missingBoxRect, zoundName, fallbackStyle);
                    }
                }
            }
            else {
                zoundButtonContent.text    = zoundName;
                zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open edit mode. Middle click or Alt left click to copy the name to clipboard.";

                // Darken text in sync with the pulse so it stays readable against the brightening bg.
                float pulseI = ZUI.GetPulseIntensity(ZoundPulseKey(currentZound));
                if (pulseI > 0f) GUI.color = Color.Lerp(guiColor, Color.black, pulseI * 0.6f);

                if (ZUI.Button(nameButtonRect, zoundButtonContent, ZUI.Style.ZoundBtn)) {
                    if (evt.button == 0) {
                        if (evt.alt) {
                            CopyToClipboard(zoundName);
                        }
                        else {
                            if (evt.control) { InfoViewWindow.OpenWindow(currentZound); }
                            else {
                                if (browserSettings.killOnPlay) ZoundEngine.StopAllZounds();
                                ZoundEngine.PlayZound(currentZound);
                            }
                        }
                    }
                    else if (evt.button == 1) { OpenZoundEditor(currentZound); }
                    else if (evt.button == 2) { CopyToClipboard(zoundName); }
                    GUI.FocusControl(null);
                }
            }

            GUI.color = guiColor;

            // Edit button / M/S buttons / inspector fields / right-group buttons.
            // In two-row mode, tagsRect is non-zero and tags are drawn in the dedicated right zone.
            zoundInspector.DrawZoundSinglecolumn(editButtonRect, muteSoloRect, removeButtonRect, inspectorRect, currentZound, tagsRect);

            DrawMuteSoloIndicator(itemAreaRect, currentZound);
        }

        /// <summary>
        /// Draws a thin coloured bar at the top edge of the row to show mute/solo state at a glance —
        /// red for muted, green for soloed. Called after the main row contents so it renders on top.
        /// For Zequences, also draws a yellow bar at the bottom edge if any local (nested) mute/solo is active.
        /// This is separate from the M/S buttons — it shows the state even in multicolumn mode where
        /// no M/S buttons are visible.
        /// </summary>
        // Pass 1 — call BEFORE drawing controls: fills the background tint.
        private static void DrawMuteSoloBackground(Rect rowRect, Zound currentZound) {
            if (!currentZound.mute && !currentZound.solo) return;
            var guiColor   = GUI.color;
            var stateColor = currentZound.mute
                ? ZUI.PaletteColor("Warning", ZUIPaletteSlot.Primary, new Color(0.8f, 0.2f, 0.2f, 1f))
                : ZUI.PaletteColor("Confirm",  ZUIPaletteSlot.Primary, new Color(0f,   0.7f, 0.2f, 1f));
            GUI.color = stateColor;
            GUI.DrawTexture(rowRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
        }

        // Pass 2 — call AFTER drawing controls: draws the top border stripe and Zequence bottom stripe.
        private static void DrawMuteSoloIndicator(Rect rowRect, Zound currentZound) {
            var guiColor = GUI.color;

            if (currentZound.mute || currentZound.solo) {
                var stateColor = currentZound.mute
                    ? ZUI.PaletteColor("Warning", ZUIPaletteSlot.Primary, new Color(0.8f, 0.2f, 0.2f, 1f))
                    : ZUI.PaletteColor("Confirm",  ZUIPaletteSlot.Primary, new Color(0f,   0.7f, 0.2f, 1f));
                GUI.color = stateColor;
                GUI.DrawTexture(new Rect(rowRect.x + 1f, rowRect.y, rowRect.width - 2f, 2f), EditorGUIUtility.whiteTexture);
            }

            if (currentZound is Zequence zeq && zeq.HasLocalMuteOrSoloEntry()) {
                GUI.color = new Color(1f, 1f, 0f, 1f);
                GUI.DrawTexture(new Rect(rowRect.x + 1f, rowRect.yMax - 1.5f, rowRect.width - 2f, 1.5f), EditorGUIUtility.whiteTexture);
            }

            GUI.color = guiColor;
        }


        /// <summary>
        /// Finds the best ZoundToken to represent the current play state of a zound.
        /// Prefers a token where isDelayFinished == true (the audio has actually started).
        /// Falls back to the first found token (still in its pre-delay phase) so a highlight
        /// can be shown for queued/delayed zounds too.
        /// Returns true if any fully-started token exists; token is always set if any exists.
        /// </summary>
        private static bool TryGetAnyInstanceToken(Zound currentZound, out ZoundToken token) {
            token = null;
            bool hasAnyInstancePlaying = false;
            ZoundToken firstFoundToken = null;
            if (ZoundEngine.CullingGroups.TryGetValue(currentZound, out var cullingGroup)) {
                foreach (var t in cullingGroup) {
                    if (firstFoundToken == null) firstFoundToken = t;
                    if (t.isDelayFinished) {
                        token = t;
                        hasAnyInstancePlaying = true;
                        break;
                    }
                }
            }

            if (!hasAnyInstancePlaying) token = firstFoundToken;

            return hasAnyInstancePlaying;
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

                if (settings.showTypes) {
                    if (zoundTabProperties.selectedTypes.HasFlag(ZoundType.Everything)) {
                        zoundTabProperties.selectedTypes = ZoundType.None;
                    }
                    if (settings.typesInlineToggle) {
                        DrawTypesInlineToggle(zoundTabProperties);
                    }
                    else {
                    bool typesActive2 = zoundTabProperties.selectedTypes != ZoundType.None;
                    if (ZUI.Button("Types", typesActive2 ? ZUI.Style.Active : ZUI.Style.Default)) {
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
                    var selectedTags = zoundTabProperties.selectedTags;
                    bool tagsActive2 = selectedTags.Count > 0;
                    if (ZUI.Button("Tags", tagsActive2 ? ZUI.Style.Active : ZUI.Style.Default)) {
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
                    var selectedReferences = zoundTabProperties.selectedReferences;
                    bool refsActive2 = selectedReferences.Count > 0;
                    if (ZUI.Button("References", refsActive2 ? ZUI.Style.Active : ZUI.Style.Default)) {
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

        private static void CopyToClipboard(string zoundName) {
            GUIUtility.systemCopyBuffer = zoundName;
            Debug.Log("Copied to clipboard: " + zoundName);
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

        private int playedClipZoundCount = 0;
        private int missingZoundCount = 0;

        private List<Zound> GetFilteredZounds() {
            var tabProperties = zoundTabProperties;
            if (filterCache != null && !tabProperties.dirty) {
                int currentMissingZoundCount = ZoundEngine.MissingZounds.Count;
                int currentPlayedClipZoundCount = 0;

                if (Application.isPlaying) {
                    var cullingGroups = ZoundEngine.CullingGroups;
                    foreach (var kvp in cullingGroups) {
                        if (kvp.Key is ClipZound clipZound && kvp.Value.Count > 0) {
                            currentPlayedClipZoundCount++;
                        }
                    }
                }
                if (currentMissingZoundCount == missingZoundCount && currentPlayedClipZoundCount == playedClipZoundCount) {
                    return filterCache;
                }
                else {
                    missingZoundCount = currentMissingZoundCount;
                    playedClipZoundCount = currentPlayedClipZoundCount;
                }
            }

            tabProperties.dirty = false;
            groupCache = null;

            var zoundList = zoundsToDisplay;

            filterCache = new List<Zound>();
            if (string.IsNullOrEmpty(tabProperties.searchText)) {
                foreach (var obj in zoundList) {
                    filterCache.Add(obj);
                }
            }
            else {
                string[] searchSplits = ObjectNames.NicifyVariableName(tabProperties.searchText).ToLower().Split(' ');

                for (int i = 0; i < zoundList.Count; i++) {
                    var zoundName = zoundList[i].name;
                    bool found = zoundName.ToLower().Contains(tabProperties.searchText.ToLower());
                    if (!found) {
                        found = true;
                        string nicifyLowerName = ObjectNames.NicifyVariableName(zoundName).ToLower();
                        for (int j = 0; j < searchSplits.Length; j++) {
                            if (searchSplits[j] == "") continue;
                            if (!nicifyLowerName.Contains(searchSplits[j])) {
                                found = false;
                                break;
                            }
                        }
                    }
                    if (found) {
                        filterCache.Add(zoundList[i]);
                    }
                }
            }

            if (tabProperties.selectedFolders.Count > 0) {
                List<AudioClip> clips = new List<AudioClip>();
                foreach (var folder in tabProperties.selectedFolders) {
                    clips.AddRange(ZoundsFilter.GetClipsAtFolder(folder));
                }
                var arr = filterCache.ToArray();
                foreach (TZound z in arr) {
                    if (!IsClipContainedInZound(clips, z)) {
                        filterCache.Remove(z);
                    }
                }
            }

            if (tabProperties.selectedTags.Count > 0) {
                var zoundsWithTag = new List<Zound>();
                foreach (var tagId in tabProperties.selectedTags) {
                    zoundsWithTag.AddRange(ZoundsFilter.GetZoundsByTag(tagId));
                }
                zoundsWithTag = zoundsWithTag.Distinct().ToList();
                var arr = filterCache.ToArray();
                foreach (TZound z in arr) {
                    if (!zoundsWithTag.Contains(z)) {
                        filterCache.Remove(z);
                    }
                }
            }

            if (tabProperties.selectedReferences.Count > 0) {
                var dependencies = new List<Zound>();
                foreach (var zoundId in tabProperties.selectedReferences) {
                    if (ZoundDictionary.TryGetZoundById(zoundId, out var zoundReference)) {
                        dependencies.AddRange(zoundReference.GetDependencies());
                    }
                }
                dependencies = dependencies.Distinct().ToList();
                var arr = filterCache.ToArray();
                foreach (TZound z in arr) {
                    if (!dependencies.Contains(z)) {
                        filterCache.Remove(z);
                    }
                }
            }

            filterCache = filterCache.Distinct().ToList();

            if (ZoundsProject.Instance.browserSettings.msOnly) {
                filterCache.RemoveAll(z => !z.mute && !z.solo);
            }

            return filterCache;
        }

        private static bool IsClipContainedInZound(List<AudioClip> clips, Zound z) {
            if (z is Klip klip) {
                var clip = klip.GetAudioClipReference().editorAsset as AudioClip;
                if (clip != null && clips.Contains(clip)) {
                    return true;
                }
                return false;
            }
            if (z is Zequence zequence) {
                foreach (var entry in zequence.zoundEntries) {
                    if (zequence.TryGetEntryZound(entry, out var childZound)) {
                        if (IsClipContainedInZound(clips, childZound)) return true;
                    }
                }
                return false;
            }
            if (z is Muzic muzic) {
                Debug.LogError("Folder filter not implemented yet for Muzic");
                return false;
            }
            return false;
        }

        protected virtual void HandleAddNew() { Debug.Log("HandleAddNew in this tab is not yet implemented."); }
        public virtual void OpenZoundEditor(Zound zound) { Debug.Log("OpenZoundEditor in this tab is not yet implemented."); }
        protected virtual void ClearFocus() { GUI.FocusControl(null); }

    }

}