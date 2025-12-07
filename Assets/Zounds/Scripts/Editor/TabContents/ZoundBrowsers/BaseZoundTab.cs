using System.Collections.Generic;
using System.Linq;
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

        protected const float inspectorHeight = 39f;

        private Zound selectedZound;
        private Vector2 scrollPos;
        protected AnimFloat inspectorAnimFloat = new AnimFloat(0f);
        private ZoundInspector<TZound> zoundInspector;
        private GUIContent zoundButtonContent = new GUIContent();

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
                new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/multicolumn"), "Multicolumn"),
                new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/singlecolumn"), "Singlecolumn")
            };
        }

        public override void OnTabOpened() {
            ClearFocus();
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            SerializedProperty zoundLibrary = serializedObject.FindProperty("zoundLibrary");

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(5f);
                if (GUILayout.Button(icon_addNew, GUILayout.Width(30f), GUILayout.Height(30f)) && Event.current.button == 0) {
                    HandleAddNew();
                    filterCache = null;
                }
                if (GUILayout.Button("Stop All", GUILayout.Width(60f), GUILayout.Height(30f))) {
                    ZoundEngine.StopAllZounds();
                }

                DrawFilterFields();

                int currentColumn = ZoundsProject.Instance.browserSettings.multicolumn ? 0 : 1;
                int newColumnMode = GUILayout.Toolbar(currentColumn, icon_columns, GUILayout.Width(60f), GUILayout.Height(30f));
                if (newColumnMode != currentColumn) {
                    ZoundsWindow.ModifyZoundsProject("toggle column view", () => {
                        ZoundsProject.Instance.browserSettings.multicolumn = newColumnMode == 0;
                    });
                }

                OnAfterDrawColumnMode();

                GUILayout.Space(3f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            int selectedIndex = -1;

            List<Zound> filteredZounds = GetFilteredZounds();

            filteredZounds = EvaluateGroup(filteredZounds);

            if (selectedZound != null) {
                selectedIndex = filteredZounds.IndexOf(selectedZound);
            }

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

        protected virtual void OnAfterDrawColumnMode() {
            
        }

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

                    zoundLibrary.ForEachZound(z => {
                        if (z.manuallySetMixerGroupRef != null && z.editor_hasManuallySetRouting) {
                            string mixerGroupName = z.manuallySetMixerGroupRef.SubObjectName;
                            if (!zoundsByMixerGroupName.ContainsKey(mixerGroupName)) {
                                zoundsByMixerGroupName.Add(mixerGroupName, new List<Zound>());
                            }
                            zoundsByMixerGroupName[mixerGroupName].Add(z);
                            return;
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
                    });

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
            }

            return filteredZounds;
        }

        #region MULTICOLUMN
        private void DrawZoundsMulticolumn(Vector2 contentSize, int selectedIndex, List<Zound> filteredZounds) {
            float itemWidth = ZoundsProject.Instance.browserSettings.itemWidth;
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
                            bool isRowSelected = selectedIndex >= zoundIndex && selectedIndex < zoundIndex + colCount;
                            DrawMulticolumnRow(filteredZounds, selectedIndex, ref zoundIndex, colCount, itemWidth);
                            memberCount -= columnCount;
                            if (isRowSelected) {
                                zoundInspector.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                            }
                        }
                    }
                }
                else {
                    for (int i = 0; i < rowCount; i++) {
                        DrawMulticolumnRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth);
                        if (selectedIndex >= 0 && inspectorRowIndex == i) {
                            zoundInspector.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        // Zounds row drawer for multicolumn
        protected void DrawMulticolumnRow(List<Zound> filteredList, int selectedIndex, ref int currentIndex, int columnCount, float itemWidth) {
            GUILayout.BeginHorizontal();

            var col = GUI.color;
            var evt = Event.current;

            GUILayout.FlexibleSpace(); // center item list by adding space in the start and end.
            {
                for (int i = 0; i < columnCount; i++) {
                    if (currentIndex >= filteredList.Count) {
                        EditorGUILayout.LabelField(GUIContent.none, GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth));
                    }
                    else {
                        bool hasAnyInstancePlaying = TryGetAnyInstanceToken(filteredList[currentIndex], out var token);

                        bool isClipZound = filteredList[currentIndex] is ClipZound;

                        if (!isClipZound && filteredList[currentIndex].id == 0) {
                            GUI.color = Color.red;
                        }
                        else {
                            if (selectedIndex == currentIndex) {
                                if (hasAnyInstancePlaying) {
                                    var colorStart = isClipZound ? ZoundsEditorColors.clipFlashColorStartSelected : token.audioSource.mute ? ZoundsEditorColors.flashColorStartMuted : ZoundsEditorColors.flashColorStartSelected;
                                    var colorEnd = isClipZound ? ZoundsEditorColors.clipFlashColorEndSelected : token.audioSource.mute ? ZoundsEditorColors.flashColorEndMuted : ZoundsEditorColors.flashColorEndSelected;
                                    float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                                    t = 4 * t * (1 - t); // yoyo interpolation
                                    GUI.color = Color.Lerp(colorStart, colorEnd, t);
                                    ZoundsWindow.RepaintWindow();
                                }
                                else {
                                    if (isClipZound) GUI.color = Color.cyan;
                                }
                            }
                            else {
                                if (hasAnyInstancePlaying) {
                                    var colorStart = isClipZound ? ZoundsEditorColors.clipFlashColorStart : token.audioSource.mute ? ZoundsEditorColors.flashColorStartMuted : ZoundsEditorColors.flashColorStart;
                                    var colorEnd = isClipZound ? ZoundsEditorColors.clipFlashColorEnd : token.audioSource.mute ? ZoundsEditorColors.flashColorEndMuted : ZoundsEditorColors.flashColorEnd;
                                    float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                                    t = 4 * t * (1 - t); // yoyo interpolation
                                    GUI.color = Color.Lerp(colorStart, colorEnd, t);
                                    ZoundsWindow.RepaintWindow();
                                }
                                else {
                                    if (isClipZound) GUI.color = Color.cyan;
                                }
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

        private void HandleZoundButtonMulticolumn(List<Zound> filteredList, int selectedIndex, int currentIndex, float itemWidth, ZoundToken token, Event evt) {
            var currentZound = filteredList[currentIndex];
            var zoundName = currentZound.name;
            zoundButtonContent.text = zoundName;
            zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open configuration panel. Middle click or Alt left click to copy the name to clipboard.";

            var nameRect = GUILayoutUtility.GetRect(itemWidth, EditorGUIUtility.singleLineHeight, GUI.skin.button, GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth));

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

            if (GUI.Button(nameRect, zoundButtonContent)) {
            //if (GUILayout.Button(zoundButtonContent, GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth))) {
                if (evt.button == 0) {
                    if (evt.alt) {
                        CopyToClipboard(zoundName);
                    }
                    else if (!isMissingZound) {
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
                    if (selectedIndex == currentIndex) {
                        SelectZound(null);
                    }
                    else {
                        SelectZound(filteredList[currentIndex]);
                    }
                }
                else if (evt.button == 2) {
                    CopyToClipboard(zoundName);
                }
                GUI.FocusControl(null);
            }

            DrawMuteSoloIndicator(nameRect, currentZound);
        }
        #endregion MULTICOLUMN

        #region SINGLECOLUMN
        private void DrawZoundsSinglecolumn(Vector2 contentSize, int selectedIndex, List<Zound> filteredZounds) {
            float itemWidth = ZoundsProject.Instance.browserSettings.itemWidth;

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
                                GUILayout.Space(4f);
                            }
                            i++;
                        }
                    }
                }
                else {
                    for (int i = 0; i < filteredZounds.Count; i++) {
                        DrawSinglecolumnRow(filteredZounds, selectedIndex, i, itemWidth);
                        if (i < filteredZounds.Count - 1) {
                            GUILayout.Space(4f);
                        }
                    }
                } 
            }
            GUILayout.EndScrollView();
        }

        // Zound drawer for singlecolumn
        private Vector2 lastValidSize;
        protected void DrawSinglecolumnRow(List<Zound> filteredList, int selectedIndex, int currentIndex, float itemWidth) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            float minInspectorWidth = 0f;
            if (browserSettings.showNameField) minInspectorWidth += 170f;
            if (browserSettings.showVolume) minInspectorWidth += 170f;
            if (browserSettings.showPitch) minInspectorWidth += 170f;
            if (browserSettings.showChance) minInspectorWidth += 170f;
            if (browserSettings.showTags) minInspectorWidth += 170f;

            Rect editButtonRect, muteSoloRect, removeButtonRect, nameButtonRect, inspectorRect;

            GUILayout.BeginVertical();
            var rowRect = GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            if (rowRect.height > 1f) {
                lastValidSize = rowRect.size;
            }
            else {
                rowRect.size = lastValidSize;
            }

            float buttonWidth = 30f;
            float editRectWidth = buttonWidth;
            float muteSoloRectWidth = 24f;
            float leftButtonsWidth = editRectWidth + muteSoloRectWidth;
            float removeRectWidth = buttonWidth * 2f;

            if (rowRect.width - itemWidth - removeRectWidth - 4f < minInspectorWidth) {
                nameButtonRect = rowRect;
                nameButtonRect.x += leftButtonsWidth + 4f;
                nameButtonRect.width -= (leftButtonsWidth + 4f + removeRectWidth + 4f);
                try {// workaround for unity's bug
                    GUILayout.Space(2f);
                }
                catch { }
                try {
                    inspectorRect = GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                }
                catch {
                    inspectorRect = nameButtonRect; // workaround for unity's bug
                }
                inspectorRect.x += leftButtonsWidth + 4f;
                inspectorRect.width -= leftButtonsWidth + 4f + removeRectWidth;
            }
            else {
                nameButtonRect = new Rect(rowRect.x + leftButtonsWidth + 4f, rowRect.y, itemWidth, rowRect.height);
                inspectorRect = new Rect(nameButtonRect.xMax + 4f, rowRect.y, rowRect.width - itemWidth - leftButtonsWidth - 4f - removeRectWidth - 4f - 4f, rowRect.height);
            }
            editButtonRect = new Rect(rowRect.x, rowRect.y, editRectWidth, inspectorRect.yMax - rowRect.y);
            muteSoloRect = new Rect(editButtonRect.xMax, editButtonRect.y, muteSoloRectWidth, editButtonRect.height);
            removeButtonRect = new Rect(rowRect.xMax - removeRectWidth, rowRect.y, removeRectWidth, inspectorRect.yMax - rowRect.y);
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();

            var col = GUI.color;
            var evt = Event.current;

            HandleZoundButtonSinglecolumn(editButtonRect, muteSoloRect, removeButtonRect, nameButtonRect, inspectorRect, filteredList, currentIndex, itemWidth, evt);

            GUILayout.EndHorizontal();

            DrawMuteSoloIndicator(rowRect, filteredList[currentIndex]);
        }

        private static void DrawMuteSoloIndicator(Rect rowRect, Zound currentZound) {
            var guiColor = GUI.color;

            if (currentZound.mute || currentZound.solo) {
                var horizontalBar = rowRect;
                horizontalBar.height = 1.5f;
                horizontalBar.x += 1f;
                horizontalBar.width -= 2f;
                GUI.color = currentZound.mute ? new Color(0.8f, 0.2f, 0.2f, 1f) : new Color(0f, 0.7f, 0.2f, 1f);
                GUI.DrawTexture(horizontalBar, EditorGUIUtility.whiteTexture);
            }

            if (currentZound is Zequence zeq && zeq.HasLocalMuteOrSoloEntry()) {
                GUI.color = new Color(0.7f, 0.7f, 0f, 1f);
                var horizontalBar = rowRect;
                horizontalBar.height = 1.5f;
                horizontalBar.x += 1f;
                horizontalBar.width -= 2f;
                horizontalBar.width /= 2f;
                horizontalBar.x += horizontalBar.width;
                GUI.DrawTexture(horizontalBar, EditorGUIUtility.whiteTexture);
            }

            GUI.color = guiColor;
        }

        private void HandleZoundButtonSinglecolumn(Rect editButtonRect, Rect muteSoloRect, Rect removeButtonRect, Rect nameButtonRect, Rect inspectorRect, List<Zound> filteredList, int currentIndex, float itemWidth, Event evt) {
            var currentZound = filteredList[currentIndex];

            bool isClipZound = currentZound is ClipZound;

            var guiColor = GUI.color;
            if (!isClipZound && currentZound.id == 0) {
                GUI.color = Color.red;
            }
            else {
                if (TryGetAnyInstanceToken(currentZound, out var token)) {

                    if (!token.isChildZound) {
                        var highlightRect = new Rect(nameButtonRect.x - 1f, nameButtonRect.y - 1f, nameButtonRect.width + 2.5f, nameButtonRect.height + 2.5f);
                        GUI.DrawTexture(highlightRect, EditorGUIUtility.whiteTexture);
                    }

                    if (token.state == ZoundToken.State.Paused) {
                        GUI.color = new Color(0.9f, 0.5f, 0.9f, 1f);
                    }
                    else {
                        var colorStart = isClipZound ? ZoundsEditorColors.clipFlashColorStart : token.audioSource.mute ? ZoundsEditorColors.flashColorStartMuted : ZoundsEditorColors.flashColorStart;
                        var colorEnd = isClipZound ? ZoundsEditorColors.clipFlashColorEnd : token.audioSource.mute ? ZoundsEditorColors.flashColorEndMuted : ZoundsEditorColors.flashColorEnd;
                        float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                        t = 4 * t * (1 - t); // yoyo interpolation
                        GUI.color = Color.Lerp(colorStart, colorEnd, t);
                        ZoundsWindow.RepaintWindow();
                    }
                }
                else {
                    if (isClipZound) GUI.color = Color.cyan;
                }

                if (token != null) {
                    if (token.isChildZound) {
                        if (!token.isDelayFinished) {
                            var highlightRect = new Rect(nameButtonRect.x - 1f, nameButtonRect.y - 1f, nameButtonRect.width + 2.5f, nameButtonRect.height + 2.5f);
                            var guiColor2 = GUI.color;
                            GUI.color = Color.yellow;
                            GUI.DrawTexture(highlightRect, EditorGUIUtility.whiteTexture);
                            GUI.color = guiColor2;
                        }
                    }
                }

            }

            var zoundName = currentZound.name;
            zoundButtonContent.text = zoundName;
            zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open edit mode. Middle click or Alt left click to copy the name to clipboard.";

            bool isMissingZound = !(currentZound is ClipZound) && currentZound.id == 0;

            if (GUI.Button(nameButtonRect, zoundButtonContent)) {
                if (evt.button == 0) {
                    if (evt.alt) {
                        CopyToClipboard(zoundName);
                    }
                    else if (!isMissingZound) {
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
                else if (evt.button == 1 && !isMissingZound) {
                    OpenZoundEditor(currentZound);
                }
                else if (evt.button == 2) {
                    CopyToClipboard(zoundName);
                }
                GUI.FocusControl(null);
            }

            GUI.color = guiColor;

            zoundInspector.DrawSinglecolumn(editButtonRect, muteSoloRect, removeButtonRect, inspectorRect, currentZound);
        }

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
                if (GUILayout.Button("Clear", GUILayout.Width(50f)) && Event.current.button == 0) {
                    Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                    zoundTabProperties.ClearFilters();
                    zoundTabProperties.dirty = true;
                    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                    ClearFocus();
                }
            }
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(43f);
                var guiColor = GUI.color;

#if ZOUNDS_CONSIDER_FOLDERS
                var selectedFolders = zoundTabProperties.selectedFolders;
                GUI.color = selectedFolders.Count > 0 ? Color.cyan : guiColor;
                if (GUILayout.Button("Folder", EditorStyles.miniButton)) {
                    var menu = new GenericMenu();
                    var allFolders = ZoundsFilter.GetFolders();
                    foreach (var folder in allFolders) {
                        string f = folder;
                        bool on = selectedFolders.Contains(f);
                        string displayName = f.Substring(1, f.Length - 1).Replace('/', '\\');
                        if (string.IsNullOrEmpty(displayName)) displayName = "[Root]";
                        menu.AddItem(new GUIContent(displayName), on, selected => {
                            Undo.RecordObject(ZoundsWindowProperties.Instance, "change selected folders");
                            if ((bool)selected) {
                                if (!selectedFolders.Contains(f)) selectedFolders.Add(f);
                            }
                            else {
                                selectedFolders.RemoveAll(f2 => f2 == f);
                            }
                            EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                            zoundTabProperties.dirty = true;
                        }, on);
                    }

                    GenericMenuPopup.Show(
                        menu,
                        "Select Folders",
                        Event.current.mousePosition,
                        new List<string>(),
                        foldersSearchText,
                        newSearch => foldersSearchText = newSearch,
                        null, 1, true);
                }
#endif

                if (zoundTabProperties.selectedTypes.HasFlag(ZoundType.Everything)) {
                    zoundTabProperties.selectedTypes = ZoundType.None;
                }
                GUI.color = zoundTabProperties.selectedTypes != ZoundType.None ? Color.cyan : guiColor;
                if (GUILayout.Button("Types", EditorStyles.miniButton)) {
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
                        null, 3, true);
                }

                var selectedTags = zoundTabProperties.selectedTags;
                GUI.color = selectedTags.Count > 0 ? Color.cyan : guiColor;
                if (GUILayout.Button("Tags", EditorStyles.miniButton)) {
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

                    GenericMenuPopup.Show(
                        menu,
                        "Select Tags",
                        Event.current.mousePosition,
                        new List<string>(),
                        tagsSearchText,
                        newSearch => tagsSearchText = newSearch,
                        null, 3, true);
                }

                var selectedReferences = zoundTabProperties.selectedReferences;
                GUI.color = selectedReferences.Count > 0 ? Color.cyan : guiColor;
                if (GUILayout.Button("References", EditorStyles.miniButton)) {
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
                        null, 3, true);
                }

                GUI.color = guiColor;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

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