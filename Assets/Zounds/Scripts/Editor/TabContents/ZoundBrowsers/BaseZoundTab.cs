using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using static Zounds.ZoundsWindowProperties.ZoundTabProperties;

namespace Zounds {

    public class BaseZoundTab<TZound> : TabContent where TZound : Zound {

        protected const float inspectorHeight = 39f;

        private TZound selectedZound;
        private Vector2 scrollPos;
        protected AnimFloat inspectorAnimFloat = new AnimFloat(0f);
        private ZoundInspector<TZound> zoundInspector;
        private GUIContent zoundButtonContent = new GUIContent();

        private GUIContent icon_addNew;
        private GUIContent[] icon_columns;

        private GUIContent filterLabel = new GUIContent("Filter:");

        private List<TZound> filterCache = null;
        private GroupBy prevGroupBy;
        private List<KeyValuePair<string, List<TZound>>> groupCache = null;

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

        public TZound zoundToRemove { get; set; } = null;
        public TZound zoundToDuplicate { get; set; } = null;

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
                GUILayout.Space(3f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            int selectedIndex = -1;

            List<TZound> filteredZounds = GetFilteredZounds();

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
                });
                zoundToRemove = null;
            }
            if (zoundToDuplicate != null) {
                ZoundsWindow.ModifyZoundsProject("duplicate zound", () => {
                    var duplicatedZound = AudioAssetUtility.DuplicateZound(zoundToDuplicate) as TZound;
                    if (duplicatedZound != null) {
                        SortZounds();
                        SelectZound(duplicatedZound);
                    }
                });
                zoundToDuplicate = null;
            }
        }

        private List<TZound> EvaluateGroup(List<TZound> filteredZounds) {
            var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
            if (groupCache == null || prevGroupBy != zoundTabProperties.groupBy) {
                prevGroupBy = zoundTabProperties.groupBy;
                groupCache = new List<KeyValuePair<string, List<TZound>>>();
                if (prevGroupBy == GroupBy.None) {
                    filterCache = null;
                    filteredZounds = GetFilteredZounds();
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
                }
#endif
                else if (prevGroupBy == GroupBy.Tags) {
                    var groupTemp = new Dictionary<string, Dictionary<int, List<TZound>>>();
                    var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                    foreach (var z in filterCache) {
                        if (z.tags == null || z.tags.Count == 0) {
                            if (!groupTemp.TryGetValue("-Untagged-", out var members)) {
                                members = new Dictionary<int, List<TZound>>();
                                groupTemp.Add("-Untagged-", members);
                            }
                            if (!members.TryGetValue(0, out var sorted)) {
                                sorted = new List<TZound>();
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
                                        members = new Dictionary<int, List<TZound>>();
                                        groupTemp.Add(tagName, members);
                                    }
                                    if (!members.TryGetValue(tagId, out var sorted)) {
                                        sorted = new List<TZound>();
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
                        var sortedMembers = new List<TZound>();
                        foreach (var kvp in members) {
                            sortedMembers.AddRange(kvp.Value);
                        }
                        groupCache.Add(new KeyValuePair<string, List<TZound>>(key, sortedMembers));
                    }
                    filterCache = new List<TZound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                }
                else if (prevGroupBy == GroupBy.References) {
                    var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                    var referenceCount = new Dictionary<TZound, int>();
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
                        var zoundMembers = new List<TZound>();
                        foreach (var kvp in referenceCount) {
                            if (kvp.Value != count) continue;
                            zoundMembers.Add(kvp.Key);
                        }
                        zoundMembers = zoundMembers.Distinct().ToList();
                        groupCache.Add(new KeyValuePair<string, List<TZound>>(count.ToString(), zoundMembers));
                    }
                    filterCache = new List<TZound>();
                    foreach (var members in groupCache) {
                        filterCache.AddRange(members.Value);
                    }
                }
            }

            return filteredZounds;
        }

        #region MULTICOLUMN
        private void DrawZoundsMulticolumn(Vector2 contentSize, int selectedIndex, List<TZound> filteredZounds) {
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
        protected void DrawMulticolumnRow(List<TZound> filteredList, int selectedIndex, ref int currentIndex, int columnCount, float itemWidth) {
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

                        if (selectedIndex == currentIndex) {
                            if (hasAnyInstancePlaying) {
                                var colorStart = new Color(0.7f, 0.7f, 0.9f, 1f);
                                var colorEnd = new Color(0.9f, 0.9f, 1f, 1f);
                                float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                                t = 4 * t * (1 - t); // yoyo interpolation
                                GUI.color = Color.Lerp(colorStart, colorEnd, t);
                                ZoundsWindow.RepaintWindow();
                            }
                            else {
                                GUI.color = new Color(0.7f, 0.7f, 0.9f, 1f);
                            }
                        }
                        else {
                            if (hasAnyInstancePlaying) {
                                var colorStart = new Color(0.5f, 0.5f, 0.8f, 1f);
                                var colorEnd = new Color(0.7f, 0.7f, 0.9f, 1f);
                                float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                                t = 4 * t * (1 - t); // yoyo interpolation
                                GUI.color = Color.Lerp(colorStart, colorEnd, t);
                                ZoundsWindow.RepaintWindow();
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

        private void HandleZoundButtonMulticolumn(List<TZound> filteredList, int selectedIndex, int currentIndex, float itemWidth, ZoundToken token, Event evt) {
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

            if (GUI.Button(nameRect, zoundButtonContent)) {
            //if (GUILayout.Button(zoundButtonContent, GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth))) {
                if (evt.button == 0) {
                    if (evt.alt) {
                        CopyToClipboard(zoundName);
                    }
                    else if (evt.control) {
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
        }
        #endregion MULTICOLUMN

        #region SINGLECOLUMN
        private void DrawZoundsSinglecolumn(Vector2 contentSize, int selectedIndex, List<TZound> filteredZounds) {
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
        protected void DrawSinglecolumnRow(List<TZound> filteredList, int selectedIndex, int currentIndex, float itemWidth) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            float minInspectorWidth = 0f;
            if (browserSettings.showNameField) minInspectorWidth += 170f;
            if (browserSettings.showVolume) minInspectorWidth += 170f;
            if (browserSettings.showPitch) minInspectorWidth += 170f;
            if (browserSettings.showChance) minInspectorWidth += 170f;
            if (browserSettings.showTags) minInspectorWidth += 170f;

            Rect editButtonRect, removeButtonRect, nameButtonRect, inspectorRect;

            GUILayout.BeginVertical();
            var rowRect = GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            if (rowRect.height > 1f) {
                lastValidSize = rowRect.size;
            }
            else {
                rowRect.size = lastValidSize;
            }

            float buttonWidth = 30f;
            float removeRectWidth = buttonWidth * 2f;

            if (rowRect.width - itemWidth - removeRectWidth - 4f < minInspectorWidth) {
                nameButtonRect = rowRect;
                nameButtonRect.x += buttonWidth + 4f;
                nameButtonRect.width -= (buttonWidth + 4f + removeRectWidth + 4f);
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
                inspectorRect.x += buttonWidth + 4f;
                inspectorRect.width -= buttonWidth + 4f + removeRectWidth;
            }
            else {
                nameButtonRect = new Rect(rowRect.x + buttonWidth + 4f, rowRect.y, itemWidth, rowRect.height);
                inspectorRect = new Rect(nameButtonRect.xMax + 4f, rowRect.y, rowRect.width - itemWidth - buttonWidth - 4f - removeRectWidth - 4f - 4f, rowRect.height);
            }
            editButtonRect = new Rect(rowRect.x, rowRect.y, buttonWidth, inspectorRect.yMax - rowRect.y);
            removeButtonRect = new Rect(rowRect.xMax - removeRectWidth, rowRect.y, removeRectWidth, inspectorRect.yMax - rowRect.y);
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();

            var col = GUI.color;
            var evt = Event.current;

            HandleZoundButtonSinglecolumn(editButtonRect, removeButtonRect, nameButtonRect, inspectorRect, filteredList, currentIndex, itemWidth, evt);

            GUILayout.EndHorizontal();
        }

        private void HandleZoundButtonSinglecolumn(Rect editButtonRect, Rect removeButtonRect, Rect nameButtonRect, Rect inspectorRect, List<TZound> filteredList, int currentIndex, float itemWidth, Event evt) {
            var currentZound = filteredList[currentIndex];

            var guiColor = GUI.color;
            if (TryGetAnyInstanceToken(currentZound, out var token)) {

                if (!token.isChildZound) {
                    var highlightRect = new Rect(nameButtonRect.x - 1f, nameButtonRect.y - 1f, nameButtonRect.width + 2.5f, nameButtonRect.height + 2.5f);
                    GUI.DrawTexture(highlightRect, EditorGUIUtility.whiteTexture);
                }

                var colorStart = new Color(0.5f, 0.5f, 0.8f, 1f);
                var colorEnd = new Color(0.7f, 0.7f, 0.9f, 1f);
                float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                t = 4 * t * (1 - t); // yoyo interpolation
                GUI.color = Color.Lerp(colorStart, colorEnd, t);
                ZoundsWindow.RepaintWindow();
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

            var zoundName = currentZound.name;
            zoundButtonContent.text = zoundName;
            zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open edit mode. Middle click or Alt left click to copy the name to clipboard.";
            if (GUI.Button(nameButtonRect, zoundButtonContent)) {
                if (evt.button == 0) {
                    if (evt.alt) {
                        CopyToClipboard(zoundName);
                    }
                    else if (evt.control) {
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
                else if (evt.button == 1) {
                    OpenZoundEditor(currentZound);
                }
                else if (evt.button == 2) {
                    CopyToClipboard(zoundName);
                }
                GUI.FocusControl(null);
            }

            GUI.color = guiColor;

            zoundInspector.DrawSinglecolumn(editButtonRect, removeButtonRect, inspectorRect, currentZound);
        }

        private static bool TryGetAnyInstanceToken(TZound currentZound, out ZoundToken token) {
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

        public static string GetZoundTagsString(TZound zoundToInspect) {
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
                EditorGUILayout.LabelField("Group By:", GUILayout.Width(84f));
                var groupBy = (GroupBy)EditorGUILayout.EnumPopup(zoundTabProperties.groupBy, GUILayout.Width(84f));
                if (groupBy != zoundTabProperties.groupBy) {
                    Undo.RecordObject(ZoundsWindowProperties.Instance, "change group by");
                    zoundTabProperties.groupBy = groupBy;
                    EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                }
            }
            GUILayout.EndVertical();
        }

        private static void CopyToClipboard(string zoundName) {
            GUIUtility.systemCopyBuffer = zoundName;
            Debug.Log("Copied to clipboard: " + zoundName);
        }

        protected void SelectZound(TZound zound) {
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

        private List<TZound> GetFilteredZounds() {
            var tabProperties = zoundTabProperties;
            if (filterCache != null && !tabProperties.dirty) return filterCache;
            tabProperties.dirty = false;
            groupCache = null;

            filterCache = new List<TZound>();
            if (string.IsNullOrEmpty(tabProperties.searchText)) {
                foreach (var obj in zounds) {
                    filterCache.Add(obj);
                }
            }
            else {
                string[] searchSplits = ObjectNames.NicifyVariableName(tabProperties.searchText).ToLower().Split(' ');

                for (int i = 0; i < zounds.Count; i++) {
                    var zoundName = zounds[i].name;
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
                        filterCache.Add(zounds[i]);
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
            if (z is Randomizer randomizer) {
                Debug.LogError("Folder filter not implemented yet for Randomizer");
                return false;
            }
            return false;
        }

        protected virtual void HandleAddNew() { Debug.Log("HandleAddNew in this tab is not yet implemented."); }
        public virtual void OpenZoundEditor(TZound zound) { Debug.Log("OpenZoundEditor in this tab is not yet implemented."); }
        protected virtual void ClearFocus() { GUI.FocusControl(null); }

    }

}