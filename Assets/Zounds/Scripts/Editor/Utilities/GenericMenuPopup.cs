using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    internal class MenuItemNode {
        public GUIContent content;
        public GenericMenu.MenuFunction func;
        public GenericMenu.MenuFunction2 func2;
        public object userData;
        public bool separator;
        public bool on;

        public string name { get; }
        public MenuItemNode parent { get; }

        public List<MenuItemNode> Nodes { get; private set; }

        public MenuItemNode(string p_name = "", MenuItemNode p_parent = null) {
            name = p_name;
            parent = p_parent;
            Nodes = new List<MenuItemNode>();
        }

        public MenuItemNode CreateNode(string p_name) {
            var node = new MenuItemNode(p_name, this);
            Nodes.Add(node);
            return node;
        }

        // TODO Optimize
        public MenuItemNode GetOrCreateNode(string p_name) {
            var node = Nodes.Find(n => n.name == p_name);
            if (node == null) {
                node = CreateNode(p_name);
            }

            return node;
        }

        public List<MenuItemNode> Search(string p_search) {
            var lowerSearch = p_search.ToLower();
            List<MenuItemNode> result = new List<MenuItemNode>();

            string[] searchSplits = ObjectNames.NicifyVariableName(p_search).ToLower().Split(' ');

            foreach (var node in Nodes) {

                if (node.Nodes.Count == 0) {
                    bool found = node.name.ToLower().Contains(lowerSearch);
                    if (!found) {
                        found = node.name.Replace(" ", "").ToLower().Contains(lowerSearch);
                    }
                    if (!found) {
                        string nicifyLowerName = ObjectNames.NicifyVariableName(node.name).ToLower();
                        found = true;
                        for (int i = 0; i < searchSplits.Length; i++) {
                            if (searchSplits[i] == "") continue;
                            if (!nicifyLowerName.Contains(searchSplits[i])) {
                                found = false;
                                break;
                            }
                        }
                    }
                    if (found) {
                        result.Add(node);
                    }
                }

                result.AddRange(node.Search(p_search));
            }

            return result;
        }

        public string GetPath() {
            return parent == null ? "" : parent.GetPath() + "/" + name;
        }

        public void Execute() {
            if (func != null) {
                func?.Invoke();
            }
            else {
                func2?.Invoke(userData);
            }
        }

        public void ExecuteWithSelectionStatus(bool _selected) {
            func2?.Invoke(_selected);
        }
    }

    public class GenericMenuPopup : PopupWindowContent {

        public System.Action<string> onSearchTermChanged;
        public System.Action<object> onRightClicked;

        public static GenericMenuPopup Get(GenericMenu p_menu, string p_title) {
            var popup = new GenericMenuPopup(p_menu, p_title, null);
            return popup;
        }

        public static GenericMenuPopup Show(GenericMenu p_menu, string p_title, Vector2 p_position, List<string> starredPaths, string _searchTerm = "", System.Action<string> _onSearchTermChanged = null, System.Action<object> _onRightClicked = null, int _columnCount = 3, bool _invokeNoneSelected = false) {
            var popup = new GenericMenuPopup(p_menu, p_title, starredPaths, _columnCount, _invokeNoneSelected);
            popup.onSearchTermChanged = _onSearchTermChanged;
            popup._search = _searchTerm;
            popup.resizeToContent = false;
            popup.onRightClicked = _onRightClicked;
            PopupWindow.Show(new Rect(p_position.x, p_position.y, 0, 0), popup);
            return popup;
        }

        private static GUIStyle _labelWhite;
        private static GUIStyle LabelWhite {
            get {
                if (_labelWhite == null) {
                    _labelWhite = new GUIStyle("label");
                    _labelWhite.normal.textColor = Color.white;
                }
                return _labelWhite;
            }
        }

        private static GUIStyle _backStyle;
        public static GUIStyle BackStyle {
            get {
                if (_backStyle == null) {
                    _backStyle = new GUIStyle(GUI.skin.button);
                    _backStyle.alignment = TextAnchor.MiddleLeft;
                    _backStyle.hover.background = Texture2D.grayTexture;
                    _backStyle.normal.textColor = Color.black;
                }

                return _backStyle;
            }
        }

        private static GUIStyle _plusStyle;
        public static GUIStyle PlusStyle {
            get {
                if (_plusStyle == null) {
                    _plusStyle = new GUIStyle();
                    _plusStyle.fontStyle = FontStyle.Bold;
                    _plusStyle.normal.textColor = Color.white;
                    _plusStyle.fontSize = 16;
                }

                return _plusStyle;
            }
        }

        private HashSet<MenuItemNode> selectedNodes = new HashSet<MenuItemNode>();

        private string _title;
        private Vector2 _scrollPosition;
        private MenuItemNode _rootNode;
        private MenuItemNode _currentNode;
        private MenuItemNode _hoverNode;
        private int hoveredIndex;
        private string _search;
        private bool _repaint = false;
        private int _contentHeight;
        private bool _useScroll;
        private List<MenuItemNode> _starredNodes;

        private int columnCount = 3;
        private bool invokeNoneSelected = false;
        public int width = 350; // dynamically set
        public int height = 250;
        public int maxHeight = 250;
        public bool resizeToContent = false;
        public bool showOnStatus = true;
        public bool showSearch = true;
        public bool showTooltip = false;
        public bool showTitle = false;

        private float columnWidth = 50f; // dynamically set
        //private float columnWidth => (width - 24) / (float)columnCount;

        public GenericMenuPopup(GenericMenu p_menu, string p_title, List<string> p_starredPaths, int p_columnCount = 3, bool p_invokeNoneSelected = false) {
            columnCount = p_columnCount;
            invokeNoneSelected = p_invokeNoneSelected;
            _title = p_title;
            showTitle = !string.IsNullOrWhiteSpace(_title);
            _currentNode = _rootNode = GenerateMenuItemNodeTree(p_menu, out columnWidth);
            width = Mathf.CeilToInt(columnWidth * columnCount + 30);
            if (p_starredPaths != null) {
                _starredNodes = new List<MenuItemNode>();
                for (int i = 0; i < p_starredPaths.Count; i++) {
                    string[] split = p_starredPaths[i].Split('/');
                    List<MenuItemNode> items = _currentNode.Search(split[split.Length - 1]);
                    foreach (MenuItemNode item in items) {
                        if (item.name == split[split.Length - 1]) {
                            _starredNodes.Add(item);
                        }
                    }
                }
            }
        }

        public override Vector2 GetWindowSize() {
            return new Vector2(width, height);
        }

        public void Show(float p_x, float p_y) {
            PopupWindow.Show(new Rect(p_x, p_y, 0, 0), this);
        }

        public void Show(Vector2 p_position) {
            PopupWindow.Show(new Rect(p_position.x, p_position.y, 0, 0), this);
        }

        public override void OnGUI(Rect p_rect) {
            if (Event.current.type == EventType.Layout)
                _useScroll = _contentHeight > maxHeight || (!resizeToContent && _contentHeight > height);

            _contentHeight = 0;
            GUIStyle style = new GUIStyle();
            style.normal.background = Texture2D.whiteTexture;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 1);
            //GUI.Box(p_rect, string.Empty, style);
            GUI.color = Color.white;

            if (showTitle) {
                DrawTitle(new Rect(p_rect.x, p_rect.y, p_rect.width, 24));
            }

            if (showSearch) {
                DrawSearch(new Rect(p_rect.x, p_rect.y + (showTitle ? 24 : 0), p_rect.width, 20));
            }

            DrawMenuItems(new Rect(p_rect.x, p_rect.y + (showTitle ? 24 : 0) + (showSearch ? 22 : 0), p_rect.width, p_rect.height - (showTooltip ? 80 : 0) - (showTitle ? 24 : 0) - (showSearch ? 22 : 0) - 20));
            GUI.color = Color.white;

            if (showTooltip) {
                DrawTooltip(new Rect(p_rect.x + 5, p_rect.y + p_rect.height - 78, p_rect.width - 10, 36));
            }

            if (resizeToContent) {
                _contentHeight += 10;
                height = Mathf.Min(_contentHeight, maxHeight);
            }
            EditorGUI.FocusTextInControl("Search");

            if (GUI.Button(new Rect(p_rect.x, p_rect.y + p_rect.height - 20, p_rect.width, 20), invokeNoneSelected? "Update" : "Add Selected Items")) {
                if (invokeNoneSelected) {
                    InvokeWithSelectionStatusRecursive(_rootNode);
                }
                else {
                    var sortedSelected = selectedNodes.OrderBy(_node => _node.content.text);
                    foreach (var node in sortedSelected) {
                        node.Execute();
                    }
                }
                base.editorWindow.Close();
            }
        }

        private void InvokeWithSelectionStatusRecursive(MenuItemNode _node) {
            bool selected = selectedNodes.Contains(_node);
            //Debug.Log("Invoke: " + _node.name + ": " + selected);
            _node.ExecuteWithSelectionStatus(selected);
            foreach (var childNode in _node.Nodes) {
                InvokeWithSelectionStatusRecursive(childNode);
            }
        }

        private void DrawTitle(Rect p_rect) {
            _contentHeight += 24;
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 12;
            style.alignment = TextAnchor.LowerCenter;
            p_rect.y -= 5;
            GUI.Label(p_rect, _title, style);
        }

        private void DrawSearch(Rect p_rect) {
            _contentHeight += 22;

            List<MenuItemNode> nodes;
            List<MenuItemNode> sortedNodes;
            if (_search != null && _search != "") {
                nodes = _rootNode.Search(_search);
                sortedNodes = new List<MenuItemNode>(nodes);
                sortedNodes.Sort((n1, n2) => {
                    string p1 = n1.parent.GetPath();
                    string p2 = n2.parent.GetPath();
                    if (p1 == p2)
                        return n1.name.CompareTo(n2.name);

                    return p1.CompareTo(p2);
                });
            }
            else {
                nodes = _currentNode.Nodes;
                sortedNodes = nodes;
            }

            if (Event.current.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "Search") {
                int currentSearchIndex = sortedNodes.IndexOf(_hoverNode);
                int searchIndex = currentSearchIndex;

                switch (Event.current.keyCode) {
                    case KeyCode.DownArrow:
                        do {
                            searchIndex++;
                            if (searchIndex >= nodes.Count) {
                                searchIndex = 0;
                            }
                            _hoverNode = sortedNodes[searchIndex];
                            if (searchIndex == currentSearchIndex) {
                                break;
                            }
                        } while (_hoverNode.separator);
                        hoveredIndex = nodes.IndexOf(_hoverNode);
                        break;

                    case KeyCode.UpArrow:
                        do {
                            searchIndex--;
                            if (searchIndex < 0) {
                                searchIndex = nodes.Count - 1;
                            }
                            _hoverNode = sortedNodes[searchIndex];
                            if (searchIndex == currentSearchIndex) {
                                break;
                            }
                        } while (_hoverNode.separator);
                        hoveredIndex = nodes.IndexOf(_hoverNode);
                        break;

                    case KeyCode.Return:
                        if (_hoverNode != null) {
                            if (_hoverNode.Nodes.Count == 0) {
                                SelectNode(_hoverNode);
                            }
                            else {
                                hoveredIndex = 0;
                                _currentNode = _hoverNode;
                                if (_currentNode.Nodes.Count > 0) {
                                    _hoverNode = _currentNode.Nodes[hoveredIndex];
                                }
                                _repaint = true;
                            }
                        }
                        break;

                    case KeyCode.Escape:
                        if (_currentNode.parent != null) {
                            _currentNode = _currentNode.parent;
                            hoveredIndex = 0;
                            if (_currentNode.Nodes.Count > 0) {
                                _hoverNode = _currentNode.Nodes[hoveredIndex];
                            }
                            _repaint = true;
                        }
                        break;
                }
            }

            GUI.SetNextControlName("Search");
            string newSearch = GUI.TextField(p_rect, _search);
            if (newSearch != _search) {
                if (newSearch != null & newSearch != "") {
                    nodes = _rootNode.Search(newSearch);
                }
                else {
                    nodes = _currentNode.Nodes;
                }

                if (nodes.Count > 0) {
                    _hoverNode = nodes[0];
                    hoveredIndex = 0;
                }
                else {
                    _hoverNode = null;
                }
                _search = newSearch;
            }
            onSearchTermChanged.Invoke(_search);
        }

        private void DrawTooltip(Rect p_rect) {
            _contentHeight += 60;
            if (_hoverNode == null || _hoverNode.content == null || string.IsNullOrWhiteSpace(_hoverNode.content.tooltip))
                return;

            GUIStyle style = new GUIStyle();
            style.fontSize = 9;
            style.wordWrap = true;
            style.normal.textColor = Color.white;
            GUI.Label(p_rect, _hoverNode.content.tooltip, style);
        }

        private void DrawMenuItems(Rect p_rect) {
            GUILayout.BeginArea(p_rect);
            if (_useScroll) {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
            }

            GUILayout.BeginVertical();

            if (string.IsNullOrEmpty(_search) && _starredNodes != null && _starredNodes.Count > 0) {
                DrawStaredNodes(p_rect);
            }
            if (string.IsNullOrWhiteSpace(_search) || _search.Length < 2) {
                DrawNodeTree(p_rect);
            }
            else {
                DrawNodeSearch(p_rect);
            }

            GUILayout.EndVertical();
            if (_useScroll) {
                EditorGUILayout.EndScrollView();
            }

            GUILayout.EndArea();
        }

        private void DrawStaredNodes(Rect p_rect) {

            string lastPath = "";
            for (int i = 0; i < _starredNodes.Count; i++) {
                string nodePath = _starredNodes[i].parent.GetPath();
                if (nodePath != lastPath) {
                    _contentHeight += 21;
                    GUILayout.Label(nodePath, LabelWhite);
                    lastPath = nodePath;
                }

                _contentHeight += 21;
                DetermineNodeBackgroundColor(_starredNodes[i]);
                GUIStyle style = new GUIStyle();
                style.normal.background = EditorGUIUtility.whiteTexture;
                GUILayout.BeginHorizontal(style);

                if (showOnStatus) {
                    style = new GUIStyle("box");
                    style.normal.background = Texture2D.whiteTexture;
                    GUI.color = _starredNodes[i].on ? new Color(0, .6f, .8f) : new Color(.2f, .2f, .2f);
                    //GUILayout.Box("", style, GUILayout.Width(14), GUILayout.Height(14));
                }

                GUI.color = _hoverNode == _starredNodes[i] ? Color.white : Color.white;
                GUILayout.Label("⋆ " + _starredNodes[i].name, LabelWhite);

                GUILayout.EndHorizontal();

                var nodeRect = GUILayoutUtility.GetLastRect();
                if (Event.current.isMouse) {
                    if (nodeRect.Contains(Event.current.mousePosition)) {
                        hoveredIndex = i;
                        if (Event.current.type == EventType.MouseDown) {
                            if (Event.current.button == 0) {
                                if (_starredNodes[i].Nodes.Count > 0) {
                                    _currentNode = _starredNodes[i];
                                    _repaint = true;
                                }
                                else {
                                    if (onSearchTermChanged != null) {
                                        onSearchTermChanged.Invoke(_search);
                                    }
                                    SelectNode(_starredNodes[i]);
                                }

                                break;
                            }
                            else if (Event.current.button == 1) {
                                HandleRightClick(_starredNodes[i]);
                            }
                        }

                        if (_hoverNode != _starredNodes[i]) {
                            _hoverNode = _starredNodes[i];
                            _repaint = true;
                        }
                    }
                    else if (_hoverNode == _starredNodes[i]) {
                        _hoverNode = null;
                        _repaint = true;
                    }
                }
            }

            if (_starredNodes.Count == 0) {
                GUILayout.Label("No result found for specified search.");
            }
        }

        private void DrawNodeSearch(Rect p_rect) {
            List<MenuItemNode> search = _rootNode.Search(_search);
            search.Sort((n1, n2) => {
                string p1 = n1.parent.GetPath();
                string p2 = n2.parent.GetPath();
                if (p1 == p2)
                    return n1.name.CompareTo(n2.name);

                return p1.CompareTo(p2);
            });

            string lastPath = "";
            for (int i = 0; i < search.Count; i++) {
                string nodePath = search[i].parent.GetPath();
                if (nodePath != lastPath) {
                    _contentHeight += 21;
                    GUILayout.Label(nodePath, LabelWhite);
                    lastPath = nodePath;
                }

                _contentHeight += 21;
                DetermineNodeBackgroundColor(search[i]);
                GUIStyle style = new GUIStyle();
                style.normal.background = EditorGUIUtility.whiteTexture;
                GUILayout.BeginHorizontal(style);

                if (showOnStatus) {
                    style = new GUIStyle("box");
                    style.normal.background = Texture2D.whiteTexture;
                    GUI.color = search[i].on ? new Color(0, .6f, .8f) : new Color(.2f, .2f, .2f);
                    //GUILayout.Box("", style, GUILayout.Width(14), GUILayout.Height(14));
                }

                GUI.color = _hoverNode == search[i] ? Color.white : Color.white;
                GUILayout.Label(search[i].name, LabelWhite);

                GUILayout.EndHorizontal();

                var nodeRect = GUILayoutUtility.GetLastRect();
                if (Event.current.isMouse) {
                    if (nodeRect.Contains(Event.current.mousePosition)) {
                        hoveredIndex = i;
                        if (Event.current.type == EventType.MouseDown) {
                            if (Event.current.button == 0) {
                                if (search[i].Nodes.Count > 0) {
                                    _currentNode = search[i];
                                    _repaint = true;
                                }
                                else {
                                    if (onSearchTermChanged != null) {
                                        onSearchTermChanged.Invoke(_search);
                                    }
                                    SelectNode(search[i]);
                                }

                                break;
                            }
                            else if (Event.current.button == 1) {
                                HandleRightClick(search[i]);
                            }
                        }

                        if (_hoverNode != search[i]) {
                            _hoverNode = search[i];
                            _repaint = true;
                        }
                    }
                    else if (_hoverNode == search[i]) {
                        _hoverNode = null;
                        _repaint = true;
                    }
                }
            }

            if (search.Count == 0) {
                GUILayout.Label("No result found for specified search.");
            }
        }

        private void DrawNodeTree(Rect p_rect) {
            if (_currentNode != _rootNode) {
                _contentHeight += 21;
                if (GUILayout.Button(_currentNode.GetPath(), BackStyle)) {
                    _currentNode = _currentNode.parent;
                }
            }

            int nodeIndex = 0;
            foreach (var node in _currentNode.Nodes) {
                if (node.separator) {
                    GUILayout.Space(4);
                    _contentHeight += 4;
                    continue;
                }

                _contentHeight += 21;

                TryBeginColumnLayout(nodeIndex);

                DetermineNodeBackgroundColor(node, nodeIndex);
                GUIStyle style = new GUIStyle();
                style.normal.background = EditorGUIUtility.whiteTexture;
                GUILayout.BeginHorizontal(style);

                if (showOnStatus) {
                    style = new GUIStyle("box");
                    style.normal.background = Texture2D.whiteTexture;
                    GUI.color = node.on ? new Color(0, .6f, .8f, .5f) : new Color(.2f, .2f, .2f, .2f);
                    //GUILayout.Box("", style, GUILayout.Width(14), GUILayout.Height(14));
                }

                GUI.color = _hoverNode == node ? Color.white : Color.white;
                style = LabelWhite;
                style.fontStyle = node.Nodes.Count > 0 ? FontStyle.Bold : FontStyle.Normal;
                GUILayout.Label(node.name, style, GUILayout.Width(columnWidth));

                GUILayout.EndHorizontal();
                var nodeRect = GUILayoutUtility.GetLastRect();

                TryEndColumnLayout(nodeIndex);
                nodeIndex++;

                if (Event.current.isMouse) {
                    if (nodeRect.Contains(Event.current.mousePosition)) {
                        if (Event.current.type == EventType.MouseDown) {
                            if (Event.current.button == 0) {
                                if (node.Nodes.Count > 0) {
                                    _currentNode = node;
                                    _repaint = true;
                                }
                                else {
                                    if (onSearchTermChanged != null) {
                                        onSearchTermChanged.Invoke(_search);
                                    }
                                    SelectNode(node);
                                }

                                break;
                            }
                            else if (Event.current.button == 1) {
                                HandleRightClick(node);
                            }
                        }

                        if (_hoverNode != node) {
                            _hoverNode = node;
                            _repaint = true;
                        }
                    }
                    else if (_hoverNode == node) {
                        _hoverNode = null;
                        _repaint = true;
                    }
                }

                if (node.Nodes.Count > 0) {
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    GUI.Label(new Rect(lastRect.x + lastRect.width - 16, lastRect.y - 2, 20, 20), "+", PlusStyle);
                }
            }

            int indicesLeft = columnCount - (nodeIndex % columnCount);
            if (indicesLeft == columnCount) indicesLeft = 0;
            for (int i = 0; i < indicesLeft; i++) {
                GUILayout.Label(GUIContent.none, GUILayout.Width(columnWidth));
                nodeIndex++;
            }

            TryEndColumnLayout(nodeIndex);
        }

        private void TryBeginColumnLayout(int nodeIndex) {
            if (columnCount == 1) return;
            if (nodeIndex == 0 || nodeIndex % columnCount == 0) {
                GUILayout.BeginHorizontal();
            }
        }

        private void TryEndColumnLayout(int nodeIndex) {
            if (columnCount == 1) return;
            if (nodeIndex % columnCount == (columnCount - 1)) {
                GUILayout.EndHorizontal();
            }
        }

        private void DetermineNodeBackgroundColor(MenuItemNode node, int nodeIndex = 0) {
            if (selectedNodes.Contains(node)) {
                Color baseColor = new Color(0.4f, 0.4f, 0.8f);
                GUI.color = _hoverNode == node ? baseColor * 1.5f : baseColor;
            }
            else {
                if (_hoverNode == node) {
                    GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                }
                else {
                    if (nodeIndex % 2 == 0) {
                        GUI.color = new Color(0.3f, 0.3f, 0.3f, 1);
                    }
                    else {
                        GUI.color = new Color(0.25f, 0.25f, 0.25f, 1);
                    }
                }
            }
        }

        private void HandleRightClick(MenuItemNode node) {
            onRightClicked?.Invoke(node.userData);
        }

        void OnEditorUpdate() {
            if (_repaint) {
                //    _repaint = false;
                //    base.editorWindow.Repaint();
            }
            base.editorWindow.Repaint();
        }

        // TODO Possible type caching? 
        internal MenuItemNode GenerateMenuItemNodeTree(GenericMenu p_menu, out float maxColumnWidth) {
            maxColumnWidth = 1f;
            MenuItemNode rootNode = new MenuItemNode();
            if (p_menu == null)
                return rootNode;

            var menuItemsField = p_menu.GetType().GetField("menuItems", BindingFlags.Instance | BindingFlags.NonPublic);
            IList menuItems;

            if (menuItemsField == null) {   //Unity 2021.2
                menuItemsField = p_menu.GetType().GetField("m_MenuItems", BindingFlags.Instance | BindingFlags.NonPublic);
                menuItems = menuItemsField.GetValue(p_menu) as IList;
            }

            else { //Older Unity Versions 
                menuItems = menuItemsField.GetValue(p_menu) as ArrayList;

            }

            var labelStyle = EditorStyles.label;

            foreach (var menuItem in menuItems) {
                var menuItemType = menuItem.GetType();
                GUIContent content = (GUIContent)menuItemType.GetField("content").GetValue(menuItem);

                float textWidth = labelStyle.CalcSize(content).x + 2f;
                if (textWidth > maxColumnWidth) maxColumnWidth = textWidth;

                bool separator = (bool)menuItemType.GetField("separator").GetValue(menuItem);
                string path = content.text;
                string[] splitPath = path.Split('/');
                MenuItemNode currentNode = rootNode;
                for (int i = 0; i < splitPath.Length; i++) {
                    currentNode = (i < splitPath.Length - 1)
                        ? currentNode.GetOrCreateNode(splitPath[i])
                        : currentNode.CreateNode(splitPath[i]);
                }

                if (separator) {
                    currentNode.separator = true;
                }
                else {
                    currentNode.content = content;
                    currentNode.func = (GenericMenu.MenuFunction)menuItemType.GetField("func").GetValue(menuItem);
                    currentNode.func2 = (GenericMenu.MenuFunction2)menuItemType.GetField("func2").GetValue(menuItem);
                    currentNode.userData = menuItemType.GetField("userData").GetValue(menuItem);
                    currentNode.on = (bool)menuItemType.GetField("on").GetValue(menuItem);
                    if (currentNode.on) {
                        selectedNodes.Add(currentNode);
                    }
                }
            }

            DeepSortNodes(rootNode);

            return rootNode;
        }

        private static void DeepSortNodes(MenuItemNode _currentNode) {
            List<List<MenuItemNode>> separatedNodes = new List<List<MenuItemNode>>();

            int i = 0;
            separatedNodes.Add(new List<MenuItemNode>());
            foreach (var node in _currentNode.Nodes) {
                separatedNodes[i].Add(node);
                if (node.separator) {
                    separatedNodes.Add(new List<MenuItemNode>());
                    i++;
                }
            }

            _currentNode.Nodes.Clear();
            for (i = 0; i < separatedNodes.Count; i++) {
                separatedNodes[i].Sort((n1, n2) => {
                    if (n1.separator) return 1;
                    if (n1.Nodes.Count == 0 && n2.Nodes.Count > 0) return -1;
                    if (n1.Nodes.Count > 0 && n2.Nodes.Count == 0) return 1;
                    return n1.name.CompareTo(n2.name);
                });
                _currentNode.Nodes.AddRange(separatedNodes[i]);
            }
            foreach (var childNode in _currentNode.Nodes) {
                DeepSortNodes(childNode);
            }
        }

        private void SelectNode(MenuItemNode _selectedNode) {
            //_selectedNode.Execute();
            //base.editorWindow.Close();
            if (selectedNodes.Contains(_selectedNode)) {
                selectedNodes.Remove(_selectedNode);
            }
            else {
                selectedNodes.Add(_selectedNode);
            }
            _repaint = true;
        }

        public override void OnOpen() {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        public override void OnClose() {
            EditorApplication.update -= OnEditorUpdate;
        }
    }
}