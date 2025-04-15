using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

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
                GUILayout.Space(4);
                if (GUILayout.Button("Stop All", GUILayout.Width(60f), GUILayout.Height(30f))) {
                    ZoundEngine.StopAllZounds();
                }
                GUILayout.Space(5f);
                DrawSearchField();

                GUILayout.Space(5f);
                int currentColumn = ZoundsProject.Instance.browserSettings.multicolumn ? 0 : 1;
                int newColumnMode = GUILayout.Toolbar(currentColumn, icon_columns, GUILayout.Width(60f), GUILayout.Height(30f));
                if (newColumnMode != currentColumn) {
                    ModifyZoundsProject("toggle column view", () => {
                        ZoundsProject.Instance.browserSettings.multicolumn = newColumnMode == 0;
                    });
                }
                GUILayout.Space(5f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            int selectedIndex = -1;

            List<TZound> filteredZounds = GetFilteredZounds();
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
                ModifyZoundsProject("remove zound", () => {
                    AudioAssetUtility.RemoveZound(zoundToRemove);
                });
                zoundToRemove = null;
            }
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
                for (int i = 0; i < rowCount; i++) {
                    DrawMulticolumnRow(filteredZounds, selectedIndex, ref zoundIndex, columnCount, itemWidth);
                    if (selectedIndex >= 0 && inspectorRowIndex == i) {
                        zoundInspector.DrawMulticolumn(filteredZounds[selectedIndex], inspectorAnimFloat.value);
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
                        if (selectedIndex == currentIndex) {
                            if (ZoundEngine.CullingGroups.TryGetValue(filteredList[currentIndex], out var cullingGroup) && cullingGroup.Count > 0) {
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
                            if (ZoundEngine.CullingGroups.TryGetValue(filteredList[currentIndex], out var cullingGroup) && cullingGroup.Count > 0) {
                                var colorStart = new Color(0.5f, 0.5f, 0.8f, 1f);
                                var colorEnd = new Color(0.7f, 0.7f, 0.9f, 1f);
                                float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                                t = 4 * t * (1 - t); // yoyo interpolation
                                GUI.color = Color.Lerp(colorStart, colorEnd, t);
                                ZoundsWindow.RepaintWindow();
                            }
                        }
                        HandleZoundButtonMulticolumn(filteredList, selectedIndex, currentIndex, itemWidth, evt);
                        GUI.color = col;
                    }
                    currentIndex++;
                }
            }
            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }

        private void HandleZoundButtonMulticolumn(List<TZound> filteredList, int selectedIndex, int currentIndex, float itemWidth, Event evt) {
            var currentZound = filteredList[currentIndex];
            var zoundName = currentZound.name;
            zoundButtonContent.text = zoundName;
            zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open configuration panel. Middle click or Alt left click to copy the name to clipboard.";
            if (GUILayout.Button(zoundButtonContent, GUILayout.MinWidth(itemWidth), GUILayout.MaxWidth(itemWidth))) {
                if (evt.button == 0) {
                    if (evt.alt) {
                        CopyToClipboard(zoundName);
                    }
                    else {
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
                for (int i = 0; i < filteredZounds.Count; i++) {
                    DrawSinglecolumnRow(filteredZounds, selectedIndex, i, itemWidth);
                    if (i < filteredZounds.Count - 1) {
                        GUILayout.Space(4f);
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
            if (rowRect.width - itemWidth - 34f < minInspectorWidth) {
                nameButtonRect = rowRect;
                nameButtonRect.x += 34f;
                nameButtonRect.width -= 34f + 34f;
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
                inspectorRect.x += 34f;
                inspectorRect.width -= 34f + 30f;
            }
            else {
                nameButtonRect = new Rect(rowRect.x + 34f, rowRect.y, itemWidth, rowRect.height);
                inspectorRect = new Rect(nameButtonRect.xMax + 4f, rowRect.y, rowRect.width - itemWidth - 34f - 34f - 4f, rowRect.height);
            }
            editButtonRect = new Rect(rowRect.x, rowRect.y, 30f, inspectorRect.yMax - rowRect.y);
            removeButtonRect = new Rect(rowRect.xMax - 30f, rowRect.y, 30f, inspectorRect.yMax - rowRect.y);
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
            if (ZoundEngine.CullingGroups.TryGetValue(currentZound, out var cullingGroup) && cullingGroup.Count > 0) {
                var colorStart = new Color(0.5f, 0.5f, 0.8f, 1f);
                var colorEnd = new Color(0.7f, 0.7f, 0.9f, 1f);
                float t = (Time.realtimeSinceStartup % 0.5f) / 0.5f;
                t = 4 * t * (1 - t); // yoyo interpolation
                GUI.color = Color.Lerp(colorStart, colorEnd, t);
                ZoundsWindow.RepaintWindow();
            }

            var zoundName = currentZound.name;
            zoundButtonContent.text = zoundName;
            zoundButtonContent.tooltip = zoundName + ": Left click to play. Right click to open edit mode. Middle click or Alt left click to copy the name to clipboard.";
            if (GUI.Button(nameButtonRect, zoundButtonContent)) {
                if (evt.button == 0) {
                    if (evt.alt) {
                        CopyToClipboard(zoundName);
                    }
                    else {
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

        public void ModifyZoundsProject(string undoMessage, System.Action action, bool repaintWindow = false) {
            Undo.RecordObject(ZoundsProject.Instance, undoMessage);
            action.Invoke();
            EditorUtility.SetDirty(ZoundsProject.Instance);
            ClearFocus();
            if (repaintWindow) {
                ZoundsWindow.RepaintWindow();
            }
        }

        private void DrawSearchField() {
            var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
            //GUILayout.BeginVertical();
            //GUILayout.Space(7f);
            //GUILayout.BeginHorizontal();

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 55f;
            EditorGUI.BeginChangeCheck();
            var newSearchText = EditorGUILayout.TextField(new GUIContent("Search:"), zoundTabProperties.searchText);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                zoundTabProperties.searchText = newSearchText;
            }
            EditorGUIUtility.labelWidth = labelWidth;
            if (GUILayout.Button("Clear", GUILayout.Width(50f)) && Event.current.button == 0) {
                Undo.RecordObject(ZoundsWindowProperties.Instance, "change search text");
                zoundTabProperties.searchText = "";
                ClearFocus();
            }

            //GUILayout.EndHorizontal();
            //GUILayout.EndVertical();
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

            var filteredList = new List<TZound>();
            if (string.IsNullOrEmpty(tabProperties.searchText)) {
                foreach (var obj in zounds) {
                    filteredList.Add(obj);
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
                        filteredList.Add(zounds[i]);
                    }
                }
            }
            return filteredList;
        }

        protected virtual void HandleAddNew() { Debug.Log("HandleAddNew in this tab is not yet implemented."); }
        public virtual void OpenZoundEditor(TZound zound) { Debug.Log("OpenZoundEditor in this tab is not yet implemented."); }
        protected virtual void ClearFocus() { GUI.FocusControl(null); }

    }

}