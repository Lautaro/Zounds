// ZUIIconBrowserWindow.cs
// Browses all built-in Unity editor icons loaded from the internal editor AssetBundle.
// Open via Tools → ZUI Icon Browser.
// Click an icon to see its name, size, and copy-ready code snippets.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class ZUIIconBrowserWindow : EditorWindow
{
    [MenuItem("Tools/ZUI Icon Browser")]
    static void Open()
    {
        var w = GetWindow<ZUIIconBrowserWindow>("Icon Browser");
        w.minSize = new Vector2(400f, 300f);
        w.wantsMouseMove = true;
    }

    // ── Icon entry ────────────────────────────────────────────────────────────

    struct IconEntry
    {
        public string bundlePath;   // full path in the editor bundle
        public string iconName;     // filename without extension — key for IconContent
        public string searchKey;    // lower-case for fast filtering
        public Texture2D tex;       // null until lazy-loaded
    }

    // ── State ─────────────────────────────────────────────────────────────────

    List<IconEntry> _all    = new List<IconEntry>();
    List<int>       _view   = new List<int>();   // indices into _all passing current filter
    AssetBundle     _bundle;
    bool            _ready;

    string  _search      = "";
    string  _lastSearch;
    bool    _showDark    = true;   // show d_ (dark-skin) variants
    bool    _show2x      = false;  // show @2x (high-DPI) variants

    Vector2 _scroll;
    int     _selected    = -1;     // index into _all
    int     _cellPx      = 80;     // total cell size including label

    // cached derived state
    int _lastCols      = -1;
    int _lastCellPx    = -1;
    int _lastViewCount = -1;

    // ── Layout constants ──────────────────────────────────────────────────────

    const int k_ToolbarH  = 38;
    const int k_DetailH   = 110;
    const int k_CellGap   = 4;
    const int k_LabelH    = 14;
    const int k_IconPad   = 5;
    const int k_MinCellPx = 50;
    const int k_MaxCellPx = 140;

    // ── Cached GUIStyles (built once) ─────────────────────────────────────────

    GUIStyle _cellLabelStyle;
    GUIStyle _infoKeyStyle;
    GUIStyle _infoValStyle;
    GUIStyle _snippetStyle;
    GUIStyle _countStyle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (!_ready) LoadAll();
        BuildStyles();
    }

    void BuildStyles()
    {
        _cellLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            clipping  = TextClipping.Clip,
            fontSize  = 8,
        };
        _infoKeyStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.55f, 0.55f, 0.60f, 1f) },
        };
        _infoValStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.88f, 0.88f, 0.88f, 1f) },
            wordWrap = false,
        };
        _snippetStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.60f, 0.85f, 1.00f, 1f) },
            wordWrap = false,
        };
        _countStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
    }

    // ── Load icons from the editor asset bundle ───────────────────────────────

    void LoadAll()
    {
        _ready = true;
        _all.Clear();

        var method = typeof(EditorGUIUtility)
            .GetMethod("GetEditorAssetBundle", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogWarning("[ZUI Icon Browser] GetEditorAssetBundle not found via reflection.");
            return;
        }

        _bundle = method.Invoke(null, null) as AssetBundle;
        if (_bundle == null)
        {
            Debug.LogWarning("[ZUI Icon Browser] Editor asset bundle is null.");
            return;
        }

        foreach (var path in _bundle.GetAllAssetNames())
        {
            if (!path.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)) continue;
            if (!path.EndsWith(".png",   StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;

            string iconName = System.IO.Path.GetFileNameWithoutExtension(path);
            _all.Add(new IconEntry
            {
                bundlePath = path,
                iconName   = iconName,
                searchKey  = iconName.ToLowerInvariant(),
            });
        }

        _all.Sort((a, b) => string.Compare(a.iconName, b.iconName, StringComparison.OrdinalIgnoreCase));
        ApplyFilter();
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    void ApplyFilter()
    {
        _view.Clear();
        string q = _search.ToLowerInvariant();

        for (int i = 0; i < _all.Count; i++)
        {
            var e = _all[i];
            if (!_showDark && e.searchKey.StartsWith("d_")) continue;
            if (!_show2x   && e.searchKey.Contains("@2x"))  continue;
            if (!string.IsNullOrEmpty(q) && !e.searchKey.Contains(q)) continue;
            _view.Add(i);
        }

        _lastSearch  = _search;
        _selected    = -1;
        _scroll      = Vector2.zero;
        Repaint();
    }

    // ── Lazy texture load ─────────────────────────────────────────────────────

    Texture2D GetTexture(int allIdx)
    {
        var e = _all[allIdx];
        if (e.tex != null) return e.tex;
        if (_bundle == null) return null;

        // Try direct bundle load first, then fall back to IconContent
        var tex = _bundle.LoadAsset<Texture2D>(e.bundlePath);
        if (tex == null)
        {
            var gc = EditorGUIUtility.IconContent(e.iconName);
            tex = gc?.image as Texture2D;
        }

        e.tex     = tex;
        _all[allIdx] = e;
        return tex;
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (_cellLabelStyle == null) BuildStyles();
        // After domain reload Unity deserializes _view (List<int>) but _all comes back
        // empty because IconEntry contains Texture2D. Detect and reload.
        if (_ready && _all.Count == 0) _ready = false;
        if (!_ready) LoadAll();
        if (_search != _lastSearch) ApplyFilter();

        DrawToolbar();

        float usedH  = k_ToolbarH + (_selected >= 0 ? k_DetailH : 0f);
        var gridRect = new Rect(0, k_ToolbarH, position.width, position.height - usedH);
        DrawGrid(gridRect);

        if (_selected >= 0)
            DrawDetail(new Rect(0, position.height - k_DetailH, position.width, k_DetailH));
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    void DrawToolbar()
    {
        var r = new Rect(0, 0, position.width, k_ToolbarH);
        EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.20f, 1f));
        EditorGUI.DrawRect(new Rect(0, k_ToolbarH - 1f, position.width, 1f), new Color(0f, 0f, 0f, 0.4f));

        float x = 8f;
        float y = (k_ToolbarH - 18f) * 0.5f;

        // Search label + field
        EditorGUI.LabelField(new Rect(x, y + 2f, 46f, 14f), "Search:", _infoKeyStyle);
        x += 48f;
        var newSearch = EditorGUI.TextField(new Rect(x, y, 190f, 18f), _search);
        if (newSearch != _search) _search = newSearch;
        x += 193f;

        // Clear button
        if (!string.IsNullOrEmpty(_search))
        {
            if (GUI.Button(new Rect(x, y, 18f, 18f), "×", EditorStyles.miniButton)) _search = "";
            x += 22f;
        }

        // Count
        string countTxt = _all.Count > 0
            ? $"{_view.Count} / {_all.Count}"
            : "No bundle";
        EditorGUI.LabelField(new Rect(x, y + 2f, 80f, 14f), countTxt, _countStyle);
        x += 84f;

        // Dark skin toggle
        bool newDark = GUI.Toggle(new Rect(x, y + 2f, 14f, 14f), _showDark, GUIContent.none);
        EditorGUI.LabelField(new Rect(x + 16f, y + 2f, 42f, 14f), "d_ skin", _infoKeyStyle);
        if (newDark != _showDark) { _showDark = newDark; ApplyFilter(); }
        x += 62f;

        // @2x toggle
        bool new2x = GUI.Toggle(new Rect(x, y + 2f, 14f, 14f), _show2x, GUIContent.none);
        EditorGUI.LabelField(new Rect(x + 16f, y + 2f, 36f, 14f), "@2x", _infoKeyStyle);
        if (new2x != _show2x) { _show2x = new2x; ApplyFilter(); }
        x += 56f;

        // Size slider
        EditorGUI.LabelField(new Rect(x, y + 2f, 30f, 14f), "Size:", _infoKeyStyle);
        int newCell = (int)GUI.HorizontalSlider(new Rect(x + 34f, y + 6f, 80f, 12f), _cellPx, k_MinCellPx, k_MaxCellPx);
        EditorGUI.LabelField(new Rect(x + 118f, y + 2f, 28f, 14f), newCell.ToString(), _infoKeyStyle);
        if (newCell != _cellPx) { _cellPx = newCell; Repaint(); }
    }

    // ── Grid ──────────────────────────────────────────────────────────────────

    void DrawGrid(Rect viewRect)
    {
        if (_view.Count == 0)
        {
            var noRect = new Rect(viewRect.x, viewRect.y + 30f, viewRect.width, 20f);
            EditorGUI.LabelField(noRect, _all.Count == 0 ? "Bundle not loaded." : "No icons match.", _countStyle);
            return;
        }

        int cellTotal = _cellPx + k_CellGap;
        int cols      = Mathf.Max(1, (int)(viewRect.width / cellTotal));
        int rows      = (_view.Count + cols - 1) / cols;
        float totalH  = rows * cellTotal + k_CellGap;

        var contentRect = new Rect(0, 0, viewRect.width, totalH);
        _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect, false, false);

        // Visible row range — only draw (and load) what's on screen
        float scrollY    = _scroll.y;
        int   firstRow   = Mathf.Max(0, (int)(scrollY / cellTotal) - 1);
        int   lastRow    = Mathf.Min(rows - 1, (int)((scrollY + viewRect.height) / cellTotal) + 1);
        int   iconDispSz = _cellPx - k_LabelH - k_IconPad * 2;

        var ev = Event.current;

        for (int row = firstRow; row <= lastRow; row++)
        for (int col = 0; col < cols; col++)
        {
            int viewIdx = row * cols + col;
            if (viewIdx >= _view.Count) break;

            int  allIdx  = _view[viewIdx];
            if (allIdx >= _all.Count) continue;  // stale index — _all rebuilt mid-frame
            var  entry   = _all[allIdx];
            float cx     = col * cellTotal + k_CellGap;
            float cy     = row * cellTotal + k_CellGap;
            var  cellR   = new Rect(cx, cy, _cellPx, _cellPx);

            bool isSel  = _selected == allIdx;
            bool isHov  = cellR.Contains(ev.mousePosition);

            // Background
            if (isSel)
                EditorGUI.DrawRect(cellR, new Color(0.22f, 0.44f, 0.72f, 1f));
            else if (isHov && ev.type == EventType.MouseMove)
                Repaint();

            if (!isSel && isHov)
                EditorGUI.DrawRect(cellR, new Color(1f, 1f, 1f, 0.07f));

            // Click
            if (ev.type == EventType.MouseDown && ev.button == 0 && cellR.Contains(ev.mousePosition))
            {
                _selected = allIdx;
                ev.Use();
                Repaint();
            }

            // Icon texture
            var tex = GetTexture(allIdx);
            if (tex != null)
            {
                var iconR = new Rect(
                    cellR.x + (cellR.width  - iconDispSz) * 0.5f,
                    cellR.y + k_IconPad,
                    iconDispSz, iconDispSz);
                GUI.DrawTexture(iconR, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // Placeholder while loading / genuinely missing
                var ph = new Rect(cellR.x + (_cellPx - 12) * 0.5f, cellR.y + k_IconPad + (iconDispSz - 12) * 0.5f, 12f, 12f);
                EditorGUI.DrawRect(ph, new Color(1f, 1f, 1f, 0.08f));
            }

            // Name label
            var lblR = new Rect(cellR.x + 1f, cellR.yMax - k_LabelH - 2f, cellR.width - 2f, k_LabelH);
            EditorGUI.LabelField(lblR, entry.iconName, _cellLabelStyle);
        }

        GUI.EndScrollView();
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    void DrawDetail(Rect r)
    {
        if (_selected < 0 || _selected >= _all.Count) return;
        var e   = _all[_selected];
        var tex = GetTexture(_selected);

        // Panel background + top border
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.16f, 1f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), new Color(1f, 1f, 1f, 0.12f));

        const float pad      = 10f;
        const float previewW = 80f;
        const float keyW     = 76f;
        const float lh       = 15f;
        const float btnW     = 168f;
        const float btnH     = 19f;

        // ── Large preview ──────────────────────────────────────────────────────
        float ps   = Mathf.Min(previewW, r.height - pad * 2f);
        var   preR = new Rect(r.x + pad, r.y + (r.height - ps) * 0.5f, ps, ps);

        // Checker pattern
        DrawCheckerboard(preR, 8);
        if (tex != null) GUI.DrawTexture(preR, tex, ScaleMode.ScaleToFit, true);

        // ── Info rows ─────────────────────────────────────────────────────────
        float ix = r.x + previewW + pad * 2f;
        float iw = r.width - ix - btnW - pad * 2f;
        float iy = r.y + pad;

        Row(ref iy, ix, keyW, iw, lh, "Icon name:",   e.iconName,    _infoValStyle);
        Row(ref iy, ix, keyW, iw, lh, "Bundle path:", e.bundlePath,  _infoValStyle);
        if (tex != null)
            Row(ref iy, ix, keyW, iw, lh, "Dimensions:", $"{tex.width} × {tex.height} px", _infoValStyle);

        iy += 2f;
        Row(ref iy, ix, keyW, iw, lh, "IconContent:", $"EditorGUIUtility.IconContent(\"{e.iconName}\")", _snippetStyle);
        Row(ref iy, ix, keyW, iw, lh, "FindTexture:", $"EditorGUIUtility.FindTexture(\"{e.iconName}\")", _snippetStyle);

        // ── Copy buttons ──────────────────────────────────────────────────────
        float bx = r.xMax - btnW - pad;
        float by = r.y + pad;

        CopyBtn(ref by, bx, btnW, btnH, "Copy icon name",         e.iconName);
        CopyBtn(ref by, bx, btnW, btnH, "Copy IconContent( ) call",
            $"EditorGUIUtility.IconContent(\"{e.iconName}\")");
        CopyBtn(ref by, bx, btnW, btnH, "Copy FindTexture( ) call",
            $"EditorGUIUtility.FindTexture(\"{e.iconName}\")");
        CopyBtn(ref by, bx, btnW, btnH, "Copy bundle path",       e.bundlePath);
    }

    void Row(ref float y, float x, float keyW, float valW, float h,
             string key, string val, GUIStyle valStyle)
    {
        EditorGUI.LabelField(new Rect(x,          y, keyW,  h), key, _infoKeyStyle);
        EditorGUI.LabelField(new Rect(x + keyW,   y, valW,  h), val, valStyle);
        y += h + 1f;
    }

    void CopyBtn(ref float y, float x, float w, float h, string label, string value)
    {
        if (GUI.Button(new Rect(x, y, w, h), label, EditorStyles.miniButton))
            GUIUtility.systemCopyBuffer = value;
        y += h + 3f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void DrawCheckerboard(Rect r, float squareSize)
    {
        if (Event.current.type != EventType.Repaint) return;
        var dark  = new Color(0.22f, 0.22f, 0.22f, 1f);
        var light = new Color(0.32f, 0.32f, 0.32f, 1f);
        int cols  = Mathf.CeilToInt(r.width  / squareSize);
        int rows  = Mathf.CeilToInt(r.height / squareSize);
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            var sq = new Rect(
                r.x + col * squareSize,
                r.y + row * squareSize,
                squareSize, squareSize);
            sq.xMax = Mathf.Min(sq.xMax, r.xMax);
            sq.yMax = Mathf.Min(sq.yMax, r.yMax);
            EditorGUI.DrawRect(sq, (row + col) % 2 == 0 ? dark : light);
        }
    }
}
