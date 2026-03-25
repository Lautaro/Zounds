// ZUIAssetBrowserWindow.cs
// Browse a folder of images (PNG/JPG/SVG/etc.), assign string IDs, convert SVGs to PNG,
// and register them in a ZUIIconLibraryAsset for use in ZUI buttons and boxes.
//
// SVG conversion requires the com.unity.vectorgraphics package.
// Without it, any SVG that Unity already imported as a Sprite is still convertible.
//
// Open via  Tools → ZUI Asset Browser.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class ZUIAssetBrowserWindow : EditorWindow
{
    [MenuItem("Tools/ZUI Asset Browser")]
    static void Open()
    {
        var w = GetWindow<ZUIAssetBrowserWindow>("Asset Browser");
        w.minSize       = new Vector2(560f, 380f);
        w.wantsMouseMove = true;
    }

    // ── File entry ────────────────────────────────────────────────────────────

    struct FileEntry
    {
        public string    assetPath;      // "Assets/Icons/star.svg"
        public string    displayName;    // "star"
        public string    ext;            // ".svg"
        public string    searchKey;      // lower-case
        public bool      isSvg;
        public string    convertedPath;  // asset path of generated PNG, or null
        public Texture2D thumb;          // null until lazy-loaded
    }

    // ── State ─────────────────────────────────────────────────────────────────

    List<FileEntry>     _files        = new List<FileEntry>();
    List<int>           _view         = new List<int>();
    bool                _scanned;
    ZUIIconLibraryAsset _library;
    string              _sourceFolder = "Assets";
    string              _search       = "";
    string              _lastSearch;
    int                 _filterMode;    // 0=All  1=In Library  2=Not in library
    int                 _cellPx        = 80;
    Vector2             _gridScroll;
    int                 _selected      = -1;
    string              _pendingId     = "";
    string              _statusMsg;
    bool                _statusOk;

    // ── Constants ─────────────────────────────────────────────────────────────

    const int    k_ToolbarH  = 38;
    const int    k_DetailW   = 262;
    const int    k_CellGap   = 4;
    const int    k_LabelH    = 14;
    const int    k_IconPad   = 5;
    const int    k_MinCell   = 48;
    const int    k_MaxCell   = 128;
    const string k_PrefFolder  = "ZUIAssetBrowser_Folder";
    const string k_PrefLibrary = "ZUIAssetBrowser_Library";

    static readonly HashSet<string> k_Exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".gif", ".psd", ".webp", ".svg" };

    // ── Styles ────────────────────────────────────────────────────────────────

    GUIStyle _cellLabel;
    GUIStyle _keyStyle;
    GUIStyle _valStyle;
    GUIStyle _badgeStyle;
    GUIStyle _warnStyle;
    GUIStyle _okStyle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _sourceFolder = EditorPrefs.GetString(k_PrefFolder, "Assets");
        var lp = EditorPrefs.GetString(k_PrefLibrary, "");
        if (!string.IsNullOrEmpty(lp))
            _library = AssetDatabase.LoadAssetAtPath<ZUIIconLibraryAsset>(lp);
        // Auto-link to sheet's library when nothing stored
        if (_library == null)
            _library = ZUI.ActiveSheet?.iconLibrary;
        BuildStyles();
    }

    void BuildStyles()
    {
        _cellLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            { clipping = TextClipping.Clip, fontSize = 8 };
        _keyStyle  = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.55f, 0.55f, 0.60f) } };
        _valStyle  = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.88f, 0.88f, 0.88f) }, wordWrap = false };
        _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.40f, 0.85f, 0.55f) },
              fontSize = 7, alignment = TextAnchor.LowerCenter };
        _warnStyle = new GUIStyle(EditorStyles.miniLabel)
            { wordWrap = true, normal = { textColor = new Color(0.85f, 0.65f, 0.30f) } };
        _okStyle   = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.40f, 0.90f, 0.50f) } };
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    void Scan()
    {
        _scanned  = true;
        _selected = -1;
        _files.Clear();

        string abs = ToAbs(_sourceFolder);
        if (!Directory.Exists(abs)) { ApplyFilter(); return; }

        foreach (var file in Directory.GetFiles(abs, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (!k_Exts.Contains(ext)) continue;

            string ap = ToAsset(file);
            if (ap == null) continue;

            bool   svg  = ext == ".svg";
            string name = Path.GetFileNameWithoutExtension(file);

            // Look for a converted PNG that was previously generated
            string convPath = null;
            if (svg)
            {
                string c = Path.GetDirectoryName(ap).Replace('\\', '/') + "/Generated/" + name + ".png";
                if (File.Exists(ToAbs(c))) convPath = c;
            }

            _files.Add(new FileEntry
            {
                assetPath     = ap,
                displayName   = name,
                ext           = ext,
                searchKey     = name.ToLowerInvariant(),
                isSvg         = svg,
                convertedPath = convPath,
            });
        }

        _files.Sort((a, b) => string.Compare(a.searchKey, b.searchKey, StringComparison.Ordinal));
        ApplyFilter();
    }

    void ApplyFilter()
    {
        _view.Clear();
        string q = _search.ToLowerInvariant();
        for (int i = 0; i < _files.Count; i++)
        {
            var f = _files[i];
            if (!string.IsNullOrEmpty(q) && !f.searchKey.Contains(q)) continue;
            if (_filterMode == 1 && _library?.FindBySource(f.assetPath) == null) continue;
            if (_filterMode == 2 && _library?.FindBySource(f.assetPath) != null) continue;
            _view.Add(i);
        }
        _lastSearch = _search;
        Repaint();
    }

    // ── Texture lazy-load ─────────────────────────────────────────────────────

    Texture2D GetThumb(int idx)
    {
        var f = _files[idx];
        if (f.thumb != null) return f.thumb;

        Texture2D tex;
        if (f.isSvg)
        {
            tex = f.convertedPath != null
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(f.convertedPath)
                : null;
            tex ??= TryAnyTexture(f.assetPath);
        }
        else
        {
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f.assetPath);
        }

        f.thumb     = tex;
        _files[idx] = f;
        return tex;
    }

    static Texture2D TryAnyTexture(string ap)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(ap);
        if (t != null) return t;
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(ap);
        if (s != null) return s.texture;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ap))
        {
            if (a is Texture2D tx) return tx;
            if (a is Sprite sp)    return sp.texture;
        }
        return null;
    }

    // ── SVG conversion ────────────────────────────────────────────────────────

    static bool HasVG =>
        Type.GetType("Unity.VectorGraphics.VectorUtils, Unity.VectorGraphics") != null;

    bool DoConvert(int idx, int size)
    {
        var    f      = _files[idx];
        string outDir = Path.GetDirectoryName(f.assetPath).Replace('\\', '/') + "/Generated";
        string outAp  = outDir + "/" + f.displayName + ".png";

        Directory.CreateDirectory(ToAbs(outDir));

        Texture2D baked = HasVG ? RasterizeVG(f.assetPath, size) : null;
        baked ??= BakeSprite(f.assetPath, size);

        if (baked == null) return false;

        File.WriteAllBytes(ToAbs(outAp), baked.EncodeToPNG());
        AssetDatabase.ImportAsset(outAp);

        f.convertedPath = outAp;
        f.thumb         = null;
        _files[idx]     = f;
        return true;
    }

    // Bake from a Unity-imported Sprite (works with Vector Graphics or sprite atlases)
    static Texture2D BakeSprite(string ap, int size)
    {
        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(ap);
        return spr != null ? BlitReadable(spr.texture, size) : null;
    }

    // Rasterize via Unity.VectorGraphics package using reflection
    static Texture2D RasterizeVG(string ap, int size)
    {
        try
        {
            var parserT = Type.GetType("Unity.VectorGraphics.SVGParser, Unity.VectorGraphics");
            var utilsT  = Type.GetType("Unity.VectorGraphics.VectorUtils, Unity.VectorGraphics");
            if (parserT == null || utilsT == null) return null;

            // SVGParser.ImportSVG(TextReader)
            var importFn = parserT.GetMethod("ImportSVG",
                BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(TextReader) }, null);
            if (importFn == null) return null;

            using var reader    = new StringReader(File.ReadAllText(ToAbs(ap)));
            var       sceneInfo = importFn.Invoke(null, new object[] { reader });
            var       siType    = sceneInfo.GetType();
            var       scene     = siType.GetField("Scene")?.GetValue(sceneInfo)
                               ?? siType.GetProperty("Scene")?.GetValue(sceneInfo);
            if (scene == null) return null;

            // TessellationOptions
            var optsT = Type.GetType("Unity.VectorGraphics.VectorUtils+TessellationOptions, Unity.VectorGraphics");
            if (optsT == null) return null;
            var opts = Activator.CreateInstance(optsT);
            optsT.GetField("MaxCordDeviation")?.SetValue(opts, 0.05f);
            optsT.GetField("MaxTangentAngle")?.SetValue(opts, 0.1f);
            optsT.GetField("SamplingStepSize")?.SetValue(opts, 0.01f);

            // VectorUtils.TessellateScene(Scene, TessellationOptions)
            var tessSceneFn = utilsT.GetMethod("TessellateScene",
                BindingFlags.Static | BindingFlags.Public, null,
                new[] { scene.GetType(), optsT }, null);
            if (tessSceneFn == null) return null;
            var geoms = tessSceneFn.Invoke(null, new[] { scene, opts });

            // VectorUtils.BuildSprite — pick first matching overload
            var buildFn = Array.Find(utilsT.GetMethods(BindingFlags.Static | BindingFlags.Public),
                m => m.Name == "BuildSprite");
            if (buildFn == null) return null;

            var bp   = buildFn.GetParameters();
            var bArgs = new object[bp.Length];
            bArgs[0] = geoms;
            for (int i = 1; i < bp.Length; i++)
                bArgs[i] = bp[i].HasDefaultValue ? bp[i].DefaultValue
                          : bp[i].ParameterType.IsValueType
                              ? Activator.CreateInstance(bp[i].ParameterType)
                              : null;
            // Try to set a reasonable pixelsPerUnit
            for (int i = 1; i < bp.Length; i++)
                if (bp[i].ParameterType == typeof(float)) { bArgs[i] = 100f; break; }

            var sprite = buildFn.Invoke(null, bArgs) as Sprite;
            if (sprite == null) return null;

            // VectorUtils.RenderSpriteToTexture2D
            var renderFn = Array.Find(utilsT.GetMethods(BindingFlags.Static | BindingFlags.Public),
                m => m.Name == "RenderSpriteToTexture2D");
            if (renderFn != null)
            {
                var rp   = renderFn.GetParameters();
                var rArgs = new object[rp.Length];
                rArgs[0] = sprite;
                if (rp.Length > 1) rArgs[1] = size;
                if (rp.Length > 2) rArgs[2] = size;
                for (int i = 3; i < rp.Length; i++)
                    rArgs[i] = rp[i].HasDefaultValue ? rp[i].DefaultValue : null;
                return renderFn.Invoke(null, rArgs) as Texture2D;
            }

            return BlitReadable(sprite.texture, size);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ZUI Asset Browser] SVG rasterize error: {ex.Message}");
            return null;
        }
    }

    static Texture2D BlitReadable(Texture2D src, int size)
    {
        if (src == null) return null;
        var rt   = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
        dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        dst.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return dst;
    }

    // ── Path helpers ──────────────────────────────────────────────────────────

    static string ToAbs(string assetPath)
    {
        var data = Application.dataPath.Replace('\\', '/');
        var root = data.Substring(0, data.Length - "Assets".Length); // ends with "/"
        return root + assetPath.Replace('\\', '/');
    }

    static string ToAsset(string absPath)
    {
        var data = Application.dataPath.Replace('\\', '/');
        var full = absPath.Replace('\\', '/');
        if (!full.StartsWith(data)) return null;
        return "Assets" + full.Substring(data.Length);
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (_cellLabel == null) BuildStyles();
        if (!_scanned)           Scan();
        if (_search != _lastSearch) ApplyFilter();

        DrawToolbar();

        var body = new Rect(0, k_ToolbarH, position.width, position.height - k_ToolbarH);

        if (_selected >= 0 && _selected < _files.Count)
        {
            float dw = Mathf.Min(k_DetailW, body.width * 0.38f);
            DrawGrid(new Rect(body.x, body.y, body.width - dw, body.height));
            DrawDetail(new Rect(body.xMax - dw, body.y, dw, body.height));
        }
        else
        {
            DrawGrid(body);
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    void DrawToolbar()
    {
        var r = new Rect(0, 0, position.width, k_ToolbarH);
        EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.20f));
        EditorGUI.DrawRect(new Rect(0, k_ToolbarH - 1f, position.width, 1f), new Color(0, 0, 0, 0.4f));

        float x = 8f, y = (k_ToolbarH - 18f) * 0.5f;

        // Library picker
        EditorGUI.LabelField(new Rect(x, y + 2, 46, 14), "Library:", _keyStyle);
        x += 48;
        var newLib = EditorGUI.ObjectField(new Rect(x, y, 140, 18), _library,
            typeof(ZUIIconLibraryAsset), false) as ZUIIconLibraryAsset;
        if (newLib != _library)
        {
            _library = newLib;
            EditorPrefs.SetString(k_PrefLibrary, newLib != null ? AssetDatabase.GetAssetPath(newLib) : "");
            ApplyFilter();
        }
        x += 144;

        // Folder path display + picker button + scan
        EditorGUI.LabelField(new Rect(x, y + 2, 40, 14), "Folder:", _keyStyle);
        x += 42;
        float fw = Mathf.Clamp(position.width * 0.20f, 80f, 200f);
        string folderLabel = _sourceFolder.Length > 30 ? "…" + _sourceFolder[(_sourceFolder.Length - 28)..] : _sourceFolder;
        EditorGUI.LabelField(new Rect(x, y + 2, fw, 14), folderLabel, _valStyle);
        x += fw + 2;

        if (GUI.Button(new Rect(x, y, 22, 18), "…", EditorStyles.miniButton))
        {
            string picked = EditorUtility.OpenFolderPanel("Select Source Folder", ToAbs(_sourceFolder), "");
            if (!string.IsNullOrEmpty(picked))
            {
                _sourceFolder = ToAsset(picked + "/_") is { } ap
                    ? ap.Substring(0, ap.Length - 2)   // strip the dummy "/_" suffix
                    : picked;
                EditorPrefs.SetString(k_PrefFolder, _sourceFolder);
                _scanned = false;
            }
        }
        x += 26;

        if (GUI.Button(new Rect(x, y, 36, 18), "Scan", EditorStyles.miniButton))
        {
            // Flush thumbnails and re-scan
            for (int i = 0; i < _files.Count; i++)
            { var f = _files[i]; f.thumb = null; _files[i] = f; }
            _scanned = false;
        }
        x += 40;

        // Search
        var ns = EditorGUI.TextField(new Rect(x, y, 110, 18), _search);
        if (ns != _search) _search = ns;
        x += 112;
        if (!string.IsNullOrEmpty(_search) && GUI.Button(new Rect(x, y, 18, 18), "×", EditorStyles.miniButton))
            _search = "";
        x += 22;

        // Filter tabs
        int nf = GUI.Toolbar(new Rect(x, y, 134, 18), _filterMode,
            new[] { "All", "In Lib", "Not in Lib" }, EditorStyles.miniButton);
        if (nf != _filterMode) { _filterMode = nf; ApplyFilter(); }
        x += 138;

        EditorGUI.LabelField(new Rect(x, y + 2, 72, 14), $"{_view.Count} / {_files.Count}", _keyStyle);

        // Size slider — right-aligned
        float sx = position.width - 112f;
        EditorGUI.LabelField(new Rect(sx, y + 2, 30, 14), "Size:", _keyStyle);
        int nc = (int)GUI.HorizontalSlider(new Rect(sx + 32, y + 6, 70, 12), _cellPx, k_MinCell, k_MaxCell);
        if (nc != _cellPx) { _cellPx = nc; Repaint(); }
    }

    // ── Grid ──────────────────────────────────────────────────────────────────

    void DrawGrid(Rect view)
    {
        if (!_scanned || _files.Count == 0)
        {
            EditorGUI.LabelField(new Rect(view.x, view.y + 30, view.width, 20),
                _scanned ? "No image files found in the selected folder."
                         : "Press Scan to load files from the folder.",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }
        if (_view.Count == 0)
        {
            EditorGUI.LabelField(new Rect(view.x, view.y + 30, view.width, 20),
                "No files match the current filter.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        int cellTotal = _cellPx + k_CellGap;
        int cols      = Mathf.Max(1, (int)(view.width / cellTotal));
        int rows      = (_view.Count + cols - 1) / cols;
        int iconSz    = _cellPx - k_LabelH - k_IconPad * 2;

        _gridScroll = GUI.BeginScrollView(view, _gridScroll,
            new Rect(0, 0, view.width, rows * cellTotal + k_CellGap), false, false);

        int firstRow = Mathf.Max(0, (int)(_gridScroll.y / cellTotal) - 1);
        int lastRow  = Mathf.Min(rows - 1, (int)((_gridScroll.y + view.height) / cellTotal) + 1);
        var ev       = Event.current;

        for (int row = firstRow; row <= lastRow; row++)
        for (int col = 0; col < cols; col++)
        {
            int vi = row * cols + col;
            if (vi >= _view.Count) break;
            int fi = _view[vi];
            if (fi >= _files.Count) continue;

            var  f     = _files[fi];
            var  cellR = new Rect(col * cellTotal + k_CellGap, row * cellTotal + k_CellGap, _cellPx, _cellPx);
            bool isSel = _selected == fi;
            bool isHov = cellR.Contains(ev.mousePosition);

            if (isSel)      EditorGUI.DrawRect(cellR, new Color(0.22f, 0.44f, 0.72f));
            else if (isHov) EditorGUI.DrawRect(cellR, new Color(1, 1, 1, 0.07f));

            // Green left stripe = in library
            var entry = _library?.FindBySource(f.assetPath);
            if (entry != null)
                EditorGUI.DrawRect(new Rect(cellR.x, cellR.y, 3, cellR.height), new Color(0.35f, 0.80f, 0.50f));

            // Click to select
            if (ev.type == EventType.MouseDown && ev.button == 0 && isHov)
            {
                _selected  = fi;
                _pendingId = entry?.id ?? f.displayName;
                _statusMsg = null;
                ev.Use(); Repaint();
            }

            // Thumbnail
            var iconR = new Rect(cellR.x + (cellR.width - iconSz) * 0.5f, cellR.y + k_IconPad, iconSz, iconSz);
            var thumb = GetThumb(fi);

            if (thumb != null)
            {
                DrawChecker(iconR, 6);
                GUI.DrawTexture(iconR, thumb, ScaleMode.ScaleToFit, true);
            }
            else
            {
                EditorGUI.DrawRect(iconR, new Color(0.16f, 0.16f, 0.20f));
                EditorGUI.LabelField(iconR, f.ext.TrimStart('.').ToUpper(), EditorStyles.centeredGreyMiniLabel);
            }

            // SVG badge (orange pill top-left)
            if (f.isSvg)
            {
                var br = new Rect(cellR.x + 2, cellR.y + 2, 24, 10);
                EditorGUI.DrawRect(br, new Color(0.85f, 0.55f, 0.20f, 0.88f));
                EditorGUI.LabelField(br, "SVG", _badgeStyle);
            }

            // Name label at bottom of cell
            EditorGUI.LabelField(
                new Rect(cellR.x + 1, cellR.yMax - k_LabelH - 2, cellR.width - 2, k_LabelH),
                f.displayName, _cellLabel);

            // Library ID badge below name
            if (entry != null)
                EditorGUI.LabelField(
                    new Rect(cellR.x + 1, cellR.yMax - 9, cellR.width - 2, 9),
                    entry.id, _badgeStyle);

            if (isHov && ev.type == EventType.MouseMove) Repaint();
        }

        GUI.EndScrollView();
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    void DrawDetail(Rect r)
    {
        if (_selected < 0 || _selected >= _files.Count) return;
        var f     = _files[_selected];
        var entry = _library?.FindBySource(f.assetPath);

        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.17f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), new Color(1, 1, 1, 0.08f));

        const float pad = 10f, kw = 52f, lh = 14f;
        float x = r.x + pad, w = r.width - pad * 2f, y = r.y + pad;

        // ── Large preview ─────────────────────────────────────────────────────
        float ps   = Mathf.Min(w, 150f);
        var   preR = new Rect(r.x + (r.width - ps) * 0.5f, y, ps, ps);
        DrawChecker(preR, 10);
        var thumb = GetThumb(_selected);
        if (thumb != null)
            GUI.DrawTexture(preR, thumb, ScaleMode.ScaleToFit, true);
        else
        {
            EditorGUI.DrawRect(preR, new Color(0.15f, 0.15f, 0.18f));
            EditorGUI.LabelField(preR,
                f.isSvg ? "No preview\n(convert first)" : "Could not load",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
        }
        y += ps + 8f;

        // ── Info rows ─────────────────────────────────────────────────────────
        Row(ref y, x, kw, w - kw, lh, "Name:", f.displayName + f.ext);
        if (thumb != null)
            Row(ref y, x, kw, w - kw, lh, "Size:", $"{thumb.width} × {thumb.height} px");
        y += 4f;

        // ── SVG controls ──────────────────────────────────────────────────────
        if (f.isSvg)
        {
            EditorGUI.DrawRect(new Rect(x - 4, y, w + 8, 1), new Color(1, 1, 1, 0.06f));
            y += 6f;

            if (f.convertedPath != null)
            {
                EditorGUI.LabelField(new Rect(x, y, w, lh),
                    "✓ " + Path.GetFileName(f.convertedPath), _okStyle);
                y += lh + 2f;
            }

            bool canConvert = HasVG || AssetDatabase.LoadAssetAtPath<Sprite>(f.assetPath) != null;
            if (canConvert)
            {
                string btnLabel = f.convertedPath != null ? "Re-convert (256 px)" : "Convert SVG → PNG  (256 px)";
                if (GUI.Button(new Rect(x, y, w, 22), btnLabel, EditorStyles.miniButton))
                {
                    _statusMsg = null;
                    bool ok = DoConvert(_selected, 256);
                    _statusOk  = ok;
                    _statusMsg = ok ? "✓ Converted." : (HasVG ? "Conversion failed — check the SVG file."
                                                                : "Install com.unity.vectorgraphics.");
                }
            }
            else
            {
                EditorGUI.LabelField(new Rect(x, y, w, 28),
                    "Install com.unity.vectorgraphics to enable SVG conversion.", _warnStyle);
            }
            y += 26f;

            if (_statusMsg != null)
            {
                EditorGUI.LabelField(new Rect(x, y, w, 18), _statusMsg, _statusOk ? _okStyle : _warnStyle);
                y += 20f;
            }

            y += 4f;
        }

        // ── Library section ───────────────────────────────────────────────────
        EditorGUI.DrawRect(new Rect(x - 4, y, w + 8, 1), new Color(1, 1, 1, 0.06f));
        y += 6f;

        EditorGUI.LabelField(new Rect(x, y, w, lh), "Icon ID:", _keyStyle);
        y += lh + 2f;
        _pendingId = EditorGUI.TextField(new Rect(x, y, w, 18), _pendingId);
        y += 22f;

        // Determine what texture goes into the library entry
        Texture2D toStore = f.isSvg
            ? (f.convertedPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(f.convertedPath) : null)
            : AssetDatabase.LoadAssetAtPath<Texture2D>(f.assetPath);

        bool hasId  = !string.IsNullOrWhiteSpace(_pendingId);
        bool hasLib = _library != null;
        bool hasTex = toStore != null;

        // Add / Update button
        GUI.enabled = hasId && hasLib && hasTex;
        if (GUI.Button(new Rect(x, y, w, 22), entry != null ? "Update in Library" : "Add to Library",
            EditorStyles.miniButton) && _library != null)
        {
            _library.Upsert(_pendingId, toStore, f.assetPath);
            AssetDatabase.SaveAssets();
            ApplyFilter(); Repaint();
        }
        y += 26f;
        GUI.enabled = true;

        if (f.isSvg && !hasTex && hasId)
        {
            EditorGUI.LabelField(new Rect(x, y, w, 28),
                "Convert the SVG to PNG first before adding to library.", _warnStyle);
            y += 32f;
        }

        // Remove button
        GUI.enabled = entry != null && hasLib;
        if (GUI.Button(new Rect(x, y, w, 20), "Remove from Library", EditorStyles.miniButton)
            && _library != null)
        {
            _library.RemoveBySource(f.assetPath);
            AssetDatabase.SaveAssets();
            ApplyFilter(); Repaint();
        }
        y += 24f;
        GUI.enabled = true;

        if (!hasLib)
        {
            EditorGUI.LabelField(new Rect(x, y, w, 28),
                "Assign a ZUIIconLibraryAsset in the toolbar.", _warnStyle);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Row(ref float y, float x, float kw, float vw, float lh, string k, string v)
    {
        EditorGUI.LabelField(new Rect(x,      y, kw,  lh), k, _keyStyle);
        EditorGUI.LabelField(new Rect(x + kw, y, vw,  lh), v, _valStyle);
        y += lh + 1f;
    }

    static void DrawChecker(Rect r, float sq)
    {
        if (Event.current.type != EventType.Repaint) return;
        var d = new Color(0.22f, 0.22f, 0.22f);
        var l = new Color(0.30f, 0.30f, 0.30f);
        int rows = Mathf.CeilToInt(r.height / sq), cols = Mathf.CeilToInt(r.width / sq);
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            var s = new Rect(r.x + col * sq, r.y + row * sq, sq, sq);
            s.xMax = Mathf.Min(s.xMax, r.xMax);
            s.yMax = Mathf.Min(s.yMax, r.yMax);
            EditorGUI.DrawRect(s, (row + col) % 2 == 0 ? d : l);
        }
    }
}
