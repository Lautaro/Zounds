// ZUI.cs

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ===== Active Style Sheet =================================================

    static ZUIStyleSheetAsset _activeSheet;

    // Fallback path used when EditorPrefs has no entry (e.g. first launch or branch switch).
    internal static string k_DefaultSheetPath => ZUIInstallPath + "/ZUIStyleSheet.asset";

    // ===== ZUI Internal Editor Sheet ==========================================
    // Used by ZUI's own editor windows (style editor, asset browser, etc.)
    // Separate from ActiveSheet so editing a consumer sheet doesn't break the editor.
    internal static string k_EditorSheetPath => ZUIInstallPath + "/SystemAssets/ZUIEditorSheet.asset";
    static ZUIStyleSheetAsset _editorSheet;

    // ===== ZUI Install Path (auto-detected) ==================================
    static string _zuiInstallPath;
    /// <summary>The Assets-relative path where ZUI is installed (e.g. "Assets/ZUI" or "Assets/Plugins/ZUI").</summary>
    public static string ZUIInstallPath
    {
        get
        {
            if (_zuiInstallPath != null) return _zuiInstallPath;
            // Find this script's own path and derive the ZUI root from it
            var guids = AssetDatabase.FindAssets("t:MonoScript ZUI");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Look for the ZUI.cs that's in a Scripts/Editor folder
                if (path.EndsWith("/ZUI.cs") && path.Contains("/Scripts/Editor/"))
                {
                    // path = "Assets/.../ZUI/Scripts/Editor/ZUI.cs"
                    // We want "Assets/.../ZUI"
                    int idx = path.IndexOf("/Scripts/Editor/ZUI.cs");
                    if (idx > 0)
                    {
                        _zuiInstallPath = path.Substring(0, idx);
                        return _zuiInstallPath;
                    }
                }
            }
            // Fallback
            _zuiInstallPath = "Assets/ZUI";
            return _zuiInstallPath;
        }
    }

    // ===== Consumer Sheet Registry ============================================
    // Tools register their sheets at startup. Each window uses UseSheet("ToolName")
    // to activate its sheet for the duration of its OnGUI.
    static readonly Dictionary<string, ZUIStyleSheetAsset> _consumerSheets = new();

    /// <summary>Register a named consumer sheet. Call from [InitializeOnLoad] or OnEnable.</summary>
    public static void RegisterConsumerSheet(string name, ZUIStyleSheetAsset sheet)
    {
        if (sheet == null || string.IsNullOrEmpty(name)) return;
        _consumerSheets[name] = sheet;
    }

    /// <summary>Unregister a consumer sheet.</summary>
    public static void UnregisterConsumerSheet(string name)
    {
        if (!string.IsNullOrEmpty(name)) _consumerSheets.Remove(name);
    }

    /// <summary>Returns the names of all registered consumer sheets.</summary>
    public static string[] GetRegisteredConsumerNames()
    {
        var names = new string[_consumerSheets.Count];
        _consumerSheets.Keys.CopyTo(names, 0);
        return names;
    }

    /// <summary>Get a registered consumer sheet by name. Returns null if not found.</summary>
    public static ZUIStyleSheetAsset GetConsumerSheet(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        _consumerSheets.TryGetValue(name, out var sheet);
        return sheet;
    }

    /// <summary>
    /// Activates a named consumer sheet for the duration of the returned scope.
    /// Usage: using (ZUI.UseSheet("Zounds")) { /* draw */ }
    /// </summary>
    public static SheetScope UseSheet(string consumerName)
    {
        var sheet = GetConsumerSheet(consumerName);
        return new SheetScope(sheet ?? ActiveSheet);
    }

    /// <summary>
    /// Activates a specific sheet for the duration of the returned scope.
    /// </summary>
    public static SheetScope UseSheet(ZUIStyleSheetAsset sheet)
    {
        return new SheetScope(sheet ?? ActiveSheet);
    }

    public readonly struct SheetScope : IDisposable
    {
        private readonly ZUIStyleSheetAsset _previous;
        public SheetScope(ZUIStyleSheetAsset sheet)
        {
            _previous = _activeSheet;
            _activeSheet = sheet;
        }
        public void Dispose() => _activeSheet = _previous;
    }

    /// <summary>
    /// The internal style sheet used by ZUI's own editor windows.
    /// Auto-creates with defaults if the asset doesn't exist.
    /// </summary>
    public static ZUIStyleSheetAsset EditorSheet
    {
        get
        {
            if (_editorSheet != null) return _editorSheet;
            _editorSheet = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(k_EditorSheetPath);
            if (_editorSheet == null)
                _editorSheet = CreateEditorSheet();
            return _editorSheet;
        }
    }

    /// <summary>Creates the ZUI editor sheet with hardcoded emergency defaults.</summary>
    [MenuItem("Tools/ZUI/Create Editor Sheet")]
    static void ForceCreateEditorSheet()
    {
        _editorSheet = null;
        var sheet = EditorSheet; // triggers auto-create
        if (sheet != null)
            Selection.activeObject = sheet; // select in project
    }

    static ZUIStyleSheetAsset CreateEditorSheet()
    {
        // Ensure directory exists (recursive)
        string dir = System.IO.Path.GetDirectoryName(k_EditorSheetPath).Replace('\\', '/');
        EnsureFolderExists(dir);

        var sheet = ScriptableObject.CreateInstance<ZUIStyleSheetAsset>();
        sheet.EnsureDefaults();

        // Editor-specific palette
        sheet.palette.Add(new ZUIPaletteColor { name = "EditorBg",      color = new Color(.18f, .18f, .22f, 1f), highlight = new Color(.24f, .24f, .30f, 1f), shade = new Color(.12f, .12f, .15f, 1f) });
        sheet.palette.Add(new ZUIPaletteColor { name = "EditorAccent",   color = new Color(.35f, .55f, .85f, 1f), highlight = new Color(.50f, .70f, 1f, 1f),   shade = new Color(.20f, .35f, .55f, 1f) });
        sheet.palette.Add(new ZUIPaletteColor { name = "EditorText",     color = new Color(.85f, .85f, .88f, 1f), highlight = new Color(1f, 1f, 1f, 1f),       shade = new Color(.55f, .55f, .60f, 1f) });

        // Editor icon aliases — map semantic names to Phosphor icons in SystemAssets
        string sysIcons = ZUIAssetLibrary.k_SystemIconsPath;
        sheet.iconAliases = new System.Collections.Generic.List<ZUIAssetAlias>
        {
            // Style list controls
            new ZUIAssetAlias("move-up",       sysIcons + "/arrow-up.png"),
            new ZUIAssetAlias("move-down",     sysIcons + "/arrow-down.png"),
            new ZUIAssetAlias("flash",         sysIcons + "/star.png"),
            new ZUIAssetAlias("duplicate",     sysIcons + "/copy.png"),
            new ZUIAssetAlias("copy",          sysIcons + "/copy-simple.png"),
            new ZUIAssetAlias("paste",         sysIcons + "/clipboard-text.png"),
            new ZUIAssetAlias("delete",        sysIcons + "/trash.png"),
            // Toolbar
            new ZUIAssetAlias("add",           sysIcons + "/plus-circle.png"),
            new ZUIAssetAlias("remove",        sysIcons + "/minus-circle.png"),
            // Section toggles
            new ZUIAssetAlias("expand",        sysIcons + "/caret-down.png"),
            new ZUIAssetAlias("collapse",      sysIcons + "/caret-right.png"),
            // Preview
            new ZUIAssetAlias("eye",           sysIcons + "/eye.png"),
            new ZUIAssetAlias("eye-closed",    sysIcons + "/eye-closed.png"),
            // Settings
            new ZUIAssetAlias("settings",      sysIcons + "/sliders-horizontal.png"),
            new ZUIAssetAlias("palette",       sysIcons + "/palette.png"),
            new ZUIAssetAlias("folder",        sysIcons + "/folder-simple.png"),
            // Misc
            new ZUIAssetAlias("sort-asc",      sysIcons + "/sort-ascending.png"),
            new ZUIAssetAlias("sort-desc",     sysIcons + "/sort-descending.png"),
            new ZUIAssetAlias("search",        sysIcons + "/list-magnifying-glass.png"),
            new ZUIAssetAlias("warning",       sysIcons + "/warning.png"),
            // Corner toggles (angle icon rotated per corner)
            new ZUIAssetAlias("corner-tl",     sysIcons + "/angle.png",   0f),
            new ZUIAssetAlias("corner-tr",     sysIcons + "/angle.png",  90f),
            new ZUIAssetAlias("corner-br",     sysIcons + "/angle.png", 180f),
            new ZUIAssetAlias("corner-bl",     sysIcons + "/angle.png", 270f),
        };

        AssetDatabase.CreateAsset(sheet, k_EditorSheetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ZUI] Created editor style sheet at {k_EditorSheetPath} with {sheet.iconAliases.Count} icon aliases");
        return sheet;
    }

    /// <summary>Recursively creates asset folders if they don't exist.</summary>
    public static void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    /// <summary>Sets the default active sheet. Use from bootstrap code to establish the fallback sheet.</summary>
    public static void SetDefaultActiveSheet(ZUIStyleSheetAsset sheet)
    {
        if (sheet != null) _activeSheet = sheet;
    }

    public static ZUIStyleSheetAsset ActiveSheet
    {
        get
        {
            if (_activeSheet != null) return _activeSheet;
            var path = EditorPrefs.GetString("ZUIStyleEditor_LastSheet", "");
            if (string.IsNullOrEmpty(path)) path = k_DefaultSheetPath;
            _activeSheet = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(path);
            return _activeSheet;
        }
        internal set => _activeSheet = value;
    }

    // ===== Skin API ==========================================================

    /// <summary>Returns the names of all skins on the active sheet.</summary>
    public static string[] GetSkinNames() => ActiveSheet?.GetSkinNames() ?? new string[0];

    /// <summary>Returns the name of the active skin, or null if no skin is active.</summary>
    public static string ActiveSkinName => ActiveSheet?.ActiveSkin?.name;

    /// <summary>Sets the active skin by name. Pass null or "" to clear (use base palette).</summary>
    public static void SetActiveSkin(string skinName)
    {
        var sheet = ActiveSheet;
        if (sheet == null) return;
        sheet.SetActiveSkin(skinName);
        InvalidateAllStyles();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(sheet);
#endif
    }

    /// <summary>Invalidates all cached styles so palette color changes are reflected.</summary>
    public static void InvalidateAllStyles()
    {
        var sheet = ActiveSheet;
        if (sheet == null) return;
        foreach (var b in sheet.buttons) b.Invalidate();
        foreach (var b in sheet.boxes)   b.Invalidate();
        foreach (var s in sheet.sliders) s.Invalidate();
        foreach (var t in sheet.textStyles) t.Invalidate();
        if (sheet.globalButton != null) sheet.globalButton.Invalidate();
        if (sheet.globalBox != null)    sheet.globalBox.Invalidate();
    }

    // ===== Icon library ======================================================

    /// <summary>Resolves an icon by alias name, system path, or filename.</summary>
    /// <summary>Resolves an icon by alias name, system path, or filename.</summary>
    public static Texture2D FindIcon(string id) => ZUIAssetLibrary.FindIcon(id);

    /// <summary>Resolves an icon by name, also returning alias rotation.</summary>
    public static Texture2D FindIcon(string id, out float rotation) => ZUIAssetLibrary.FindIcon(id, out rotation);

    /// <summary>Resolves a font by alias name, system path, or filename. Respects skin overrides.</summary>
    public static Font FindFont(string name) => ZUIAssetLibrary.FindFont(name);

    /// <summary>Returns the resolved default font (sheet default → ZUI default → Unity default).</summary>
    public static Font DefaultFont => ZUIAssetLibrary.ResolveDefaultFont();

    // ===== Palette color lookup ==============================================

    /// <summary>
    /// Returns a palette color by name. The sheet's FindPaletteColor handles
    /// skin-first resolution internally.
    /// </summary>
    public static ZUIPaletteColor FindPaletteColor(string name)
        => ActiveSheet?.FindPaletteColor(name);

    /// <summary>
    /// Returns a palette color from the active sheet by name and slot.
    /// Falls back to <paramref name="fallback"/> when no sheet is loaded or the entry is not found.
    /// Usage: ZUI.PaletteColor("Warning", ZUIPaletteSlot.Primary, new Color(...))
    /// </summary>
    public static Color PaletteColor(string name, ZUIPaletteSlot slot, Color fallback)
    {
        var entry = FindPaletteColor(name);
        return entry != null ? entry.Resolve(slot) : fallback;
    }

    // ===== Style flash =======================================================
    // Flashes an overlay border on every control that uses a named style def.
    // Call StartFlash(name) to begin; controls pick it up each Repaint automatically.

    public enum FlashDefType { Button, Box, Slider }

    private static string       _flashStyleName;
    private static FlashDefType _flashDefType;
    private static double       _flashEndTime;

    // Flash params read from the active sheet; these are the compile-time fallbacks.
    private const int   k_FallbackFlashCount    = 8;
    private const float k_FallbackFlashInterval = 0.12f;

    private static float ActiveFlashInterval => ActiveSheet?.flashInterval ?? k_FallbackFlashInterval;
    private static int   ActiveFlashCount    => ActiveSheet?.flashCount    ?? k_FallbackFlashCount;

    public static void StartFlash(string styleName, FlashDefType type)
    {
        _flashStyleName = styleName;
        _flashDefType   = type;
        _flashEndTime   = EditorApplication.timeSinceStartup + ActiveFlashCount * ActiveFlashInterval;
        EnsureAnimUpdateRunning();
    }

    internal static void DrawFlashOverlayIfNeeded(Rect rect, string defName, int cornerRadius, FlashDefType type)
    {
        if (string.IsNullOrEmpty(_flashStyleName) || defName != _flashStyleName || type != _flashDefType) return;
        double t = EditorApplication.timeSinceStartup;
        if (t > _flashEndTime) { _flashStyleName = null; return; }

        float interval = ActiveFlashInterval;
        int phase = (int)((t % (interval * 2)) / interval);
        var color = phase == 0 ? Color.white : Color.black;

        var prev = GUI.color;
        GUI.color = color;
        float bw = 2f;
        GUI.DrawTexture(new Rect(rect.x,              rect.y,               rect.width, bw),          EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,              rect.yMax - bw,       rect.width, bw),          EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,              rect.y,               bw,         rect.height), EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - bw,      rect.y,               bw,         rect.height), EditorGUIUtility.whiteTexture);
        GUI.color = prev;
    }

    // ===== Spacing ===========================================================

    private const float k_FallbackVerticalSpacing   = 6f;
    private const float k_FallbackHorizontalSpacing = 8f;

    private static bool   _spaceFlashActive;
    private static double _spaceFlashEndTime;

    /// <summary>Triggers the animated spacing overlay on every ZUI.VerticalSpace() / HorizontalSpace() call.</summary>
    public static void StartVerticalSpaceFlash()
    {
        _spaceFlashActive  = true;
        _spaceFlashEndTime = EditorApplication.timeSinceStartup + ActiveFlashCount * ActiveFlashInterval;
        EnsureAnimUpdateRunning();
    }

    // Called after GUILayout.Space() — uses GetLastRect() to draw on top of the
    // already-consumed space, so zero extra layout height is added.
    private static void DrawSpaceOverlay(float scale, string scaleName, bool isHorizontal)
    {
        if (!_spaceFlashActive) return;
        double t = EditorApplication.timeSinceStartup;
        if (t > _spaceFlashEndTime) { _spaceFlashActive = false; return; }
        if (Event.current.type != EventType.Repaint) return;

        var spaceRect  = GUILayoutUtility.GetLastRect();
        float interval = ActiveFlashInterval;
        int   phase    = (int)((t % (interval * 2)) / interval);
        var lineColor  = phase == 0 ? Color.white : Color.black;
        var labelColor = phase == 0 ? Color.black  : Color.white;

        float px = isHorizontal ? spaceRect.width : spaceRect.height;
        string label = scaleName != null
            ? $"\"{scaleName}\" ×{scale:F2}  ({px:F0}px)"
            : scale == 1f
                ? $"×1  ({px:F0}px)"
                : $"×{scale:F2}  ({px:F0}px)";

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = labelColor },
            fontStyle = FontStyle.Bold,
            fontSize  = 9,
            padding   = new RectOffset(2, 2, 0, 0),
        };
        Vector2 labelSize = labelStyle.CalcSize(new GUIContent(label));

        var prev = GUI.color;
        if (isHorizontal)
        {
            float lineW = 2f;
            float lineX = spaceRect.x + (spaceRect.width - lineW) * 0.5f;
            GUI.color = lineColor;
            GUI.DrawTexture(new Rect(lineX, spaceRect.y, lineW, spaceRect.height), EditorGUIUtility.whiteTexture);
            float labelX = lineX - labelSize.x * 0.5f;
            float labelY = spaceRect.y + (spaceRect.height - labelSize.y) * 0.5f;
            GUI.DrawTexture(new Rect(labelX - 2f, labelY, labelSize.x + 4f, labelSize.y), EditorGUIUtility.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(labelX, labelY, labelSize.x, labelSize.y), label, labelStyle);
        }
        else
        {
            float lineH = 2f;
            float lineY = spaceRect.y + (spaceRect.height - lineH) * 0.5f;
            GUI.color = lineColor;
            GUI.DrawTexture(new Rect(spaceRect.x, lineY, spaceRect.width, lineH), EditorGUIUtility.whiteTexture);
            float labelX = spaceRect.x + spaceRect.width * 0.5f - labelSize.x * 0.5f;
            float labelY = lineY - labelSize.y;
            GUI.DrawTexture(new Rect(labelX - 2f, labelY, labelSize.x + 4f, labelSize.y), EditorGUIUtility.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(labelX, labelY, labelSize.x, labelSize.y), label, labelStyle);
        }
        GUI.color = prev;
    }

    // ── Vertical ─────────────────────────────────────────────────────────────

    /// <summary>Inserts the standard vertical gap, as configured in the active ZUI Style Sheet.</summary>
    public static void VerticalSpace()
    {
        GUILayout.Space(ActiveSheet?.verticalSpacing ?? k_FallbackVerticalSpacing);
        DrawSpaceOverlay(1f, null, false);
    }

    /// <summary>Inserts a scaled multiple of the standard vertical spacing.</summary>
    public static void VerticalSpace(float scale)
    {
        GUILayout.Space((ActiveSheet?.verticalSpacing ?? k_FallbackVerticalSpacing) * scale);
        DrawSpaceOverlay(scale, null, false);
    }

    /// <summary>Inserts vertical spacing using a named scale from the active style sheet.</summary>
    public static void VerticalSpace(string scaleName)
    {
        float scale = ActiveSheet?.FindSpacingScale(scaleName) ?? 1f;
        GUILayout.Space((ActiveSheet?.verticalSpacing ?? k_FallbackVerticalSpacing) * scale);
        DrawSpaceOverlay(scale, scaleName, false);
    }

    // ── Horizontal ───────────────────────────────────────────────────────────

    /// <summary>Inserts the standard horizontal gap, as configured in the active ZUI Style Sheet.</summary>
    public static void HorizontalSpace()
    {
        GUILayout.Space(ActiveSheet?.horizontalSpacing ?? k_FallbackHorizontalSpacing);
        DrawSpaceOverlay(1f, null, true);
    }

    /// <summary>Inserts a scaled multiple of the standard horizontal spacing.</summary>
    public static void HorizontalSpace(float scale)
    {
        GUILayout.Space((ActiveSheet?.horizontalSpacing ?? k_FallbackHorizontalSpacing) * scale);
        DrawSpaceOverlay(scale, null, true);
    }

    /// <summary>Inserts horizontal spacing using a named scale from the active style sheet.</summary>
    public static void HorizontalSpace(string scaleName)
    {
        float scale = ActiveSheet?.FindSpacingScale(scaleName) ?? 1f;
        GUILayout.Space((ActiveSheet?.horizontalSpacing ?? k_FallbackHorizontalSpacing) * scale);
        DrawSpaceOverlay(scale, scaleName, true);
    }

    // ── Label width helpers ─────────────────────────────────────────────────
    const float k_FallbackLabelWide   = 82f;
    const float k_FallbackLabelNarrow = 36f;
    const float k_FallbackInputMin    = 56f;

    public static float LabelWidthWide   => ActiveSheet?.labelWidthWide   ?? k_FallbackLabelWide;
    public static float LabelWidthNarrow => ActiveSheet?.labelWidthNarrow ?? k_FallbackLabelNarrow;
    public static float InputFieldMinWidth => ActiveSheet?.inputFieldMinWidth ?? k_FallbackInputMin;

    public static GUILayoutOption LabelWide()   => GUILayout.Width(LabelWidthWide);
    public static GUILayoutOption LabelNarrow() => GUILayout.Width(LabelWidthNarrow);
    public static GUILayoutOption InputMin()    => GUILayout.MinWidth(InputFieldMinWidth);

    // ── Backwards-compatible aliases ──────────────────────────────────────────
    /// <inheritdoc cref="VerticalSpace()"/>
    public static void RowSpace() => VerticalSpace();
    /// <inheritdoc cref="VerticalSpace(float)"/>
    public static void RowSpace(float scale) => VerticalSpace(scale);

    // ===== Box API — ZUIStyle (enum-keyed) ====================================

    public static BoxScope Box(string title, ZUIStyle style = ZUIStyle.Default)
    {
        var sheet = ActiveSheet;
        if (sheet != null)
        {
            var def = sheet.FindBox(style.ToString());
            if (def != null) { _pendingBoxStyle = style; _pendingBoxStyleSet = true; return new BoxScope(title, def); }
        }
        return new BoxScope(title, SectionStyleRegistry.Get(style));
    }

    public static BoxScope Box(ZUIStyle style = ZUIStyle.Default)
    {
        var sheet = ActiveSheet;
        if (sheet != null)
        {
            var def = sheet.FindBox(style.ToString());
            if (def != null) { _pendingBoxStyle = style; _pendingBoxStyleSet = true; return new BoxScope(null, def); }
        }
        return new BoxScope(null, SectionStyleRegistry.Get(style));
    }

    // ===== Box API — ZUIBoxDef (named style def) ==============================

    public static BoxScope Box(ZUIBoxDef def)           => new BoxScope(null,  def);
    public static BoxScope Box(string title, ZUIBoxDef def) => new BoxScope(title, def);

    // ===== Box API — named string style =======================================
    // Looks up a ZUIBoxDef by name from the active sheet.

    /// <summary>
    /// Opens a ZUI box using a named box style from the active style sheet.
    /// Falls back to "Default" when the name is not found.
    /// </summary>
    public static BoxScope Box(string title, string styleName)
    {
        var def = ActiveSheet?.FindBox(styleName);
        if (def != null) return new BoxScope(title, def);
        return new BoxScope(title, SectionStyleRegistry.Get(ZUIStyle.Default));
    }

    /// <inheritdoc cref="Box(string, string)"/>
    public static BoxScope BoxNamed(string styleName)
    {
        var def = ActiveSheet?.FindBox(styleName);
        if (def != null) return new BoxScope(null, def);
        return new BoxScope(null, SectionStyleRegistry.Get(ZUIStyle.Default));
    }

    // ===== AreaBox ============================================================

    public static IDisposable AreaBox(Rect rect, string title = null, ZUIStyle style = ZUIStyle.Default)
    {
        GUILayout.BeginArea(rect);
        return new AreaBoxScope(Box(title, style));
    }

    public static IDisposable AreaBox(Rect rect, ZUIBoxDef def)
    {
        GUILayout.BeginArea(rect);
        return new AreaBoxScope(Box(def));
    }

    public static IDisposable AreaBox(Rect rect, string title, ZUIBoxDef def)
    {
        GUILayout.BeginArea(rect);
        return new AreaBoxScope(Box(title, def));
    }

    // ===== BoxScope ===========================================================

    public readonly struct BoxScope : IDisposable
    {
        // ZUIStyle path — existing SectionStyle
        public readonly GUIStyle ContentStyle;
        private readonly bool    _hasContext;

        public BoxScope(string title, SectionStyle style)
        {
            ContentStyle = null;
            _hasContext  = false;
            EditorGUILayout.BeginVertical(style.GuiStyle);

            if (!string.IsNullOrEmpty(title))
            {
                EditorGUILayout.LabelField(title, style.LabelStyle);
                GUILayout.Space(2);
            }
        }

        // ZUIBoxDef path — DrawRect background, no texture

        public BoxScope(string title, ZUIBoxDef def)
        {
            ContentStyle = def.GetContentStyle();
            _hasContext  = true;
            ZUI.PushBoxContext(def);
            var rect = EditorGUILayout.BeginVertical(def.GetLayoutStyle());
            def.DrawBackground(rect);

            if (!string.IsNullOrEmpty(title))
            {
                var ls = new GUIStyle(EditorStyles.boldLabel);
                def.GetResolvedTitleText().Apply(ls);
                EditorGUILayout.LabelField(title, ls);
                GUILayout.Space(2);
            }
        }

        public void Dispose()
        {
            EditorGUILayout.EndVertical();
            if (_hasContext) ZUI.PopBoxContext();
        }
    }

    // ===== FoldoutBox API =====================================================
    //
    // A ZUI.Box whose content area animates in/out via AnimatedFoldout.
    // The box header (title + background) always draws; the body folds.
    //
    // Usage:
    //   using (var box = ZUI.FoldoutBox("Title", "StyleName", isOpen))
    //   {
    //       if (box.visible)
    //       {
    //           // draw content
    //       }
    //   }
    //
    // No field declaration or unique key needed — managed internally.

    private static readonly Dictionary<string, AnimatedFoldout> _foldoutBoxCache
        = new Dictionary<string, AnimatedFoldout>();

    /// <summary>
    /// Opens a ZUI box with animated foldout content, using a named style from the active sheet.
    /// The header always draws; the content animates in/out based on <paramref name="open"/>.
    /// </summary>
    public static FoldoutBoxScope FoldoutBox(string title, string styleName, bool open)
    {
        var def = ActiveSheet?.FindBox(styleName);
        if (def == null) def = ActiveSheet?.FindBox("Default");
        return new FoldoutBoxScope(title, def, open, styleName);
    }

    /// <summary>
    /// Opens a ZUI box with animated foldout content, using a ZUIBoxDef directly.
    /// </summary>
    public static FoldoutBoxScope FoldoutBox(string title, ZUIBoxDef def, bool open, string key = null)
    {
        return new FoldoutBoxScope(title, def, open, key ?? title ?? "FoldoutBox");
    }

    /// <summary>
    /// Opens a ZUI box with animated foldout content, using a ZUIStyle enum.
    /// </summary>
    public static FoldoutBoxScope FoldoutBox(string title, ZUIStyle style, bool open)
    {
        var sheet = ActiveSheet;
        ZUIBoxDef def = null;
        if (sheet != null) def = sheet.FindBox(style.ToString());
        return new FoldoutBoxScope(title, def, open, style.ToString());
    }

    public struct FoldoutBoxScope : IDisposable
    {
        /// <summary>True when the content area should be drawn.</summary>
        public readonly bool visible;

        private readonly bool _hasBoxDef;
        private readonly bool _boxOpened;
        private readonly FoldoutScope _foldoutScope;

        public FoldoutBoxScope(string title, ZUIBoxDef def, bool open, string key)
        {
            // Get or create the cached AnimatedFoldout for this key.
            string foldoutKey = "FoldoutBox_" + key;
            if (!_foldoutBoxCache.TryGetValue(foldoutKey, out var foldout))
            {
                foldout = new AnimatedFoldout(foldoutKey);
                _foldoutBoxCache[foldoutKey] = foldout;
            }

            _hasBoxDef = def != null;

            // Foldout wraps the box — the entire box animates in/out.
            _foldoutScope = foldout.Begin(open);
            visible = _foldoutScope.visible;

            // Only open the box shell when the foldout has content to show.
            _boxOpened = _foldoutScope.visible;
            if (_boxOpened)
            {
                if (_hasBoxDef)
                {
                    PushBoxContext(def);
                    var rect = EditorGUILayout.BeginVertical(def.GetLayoutStyle());
                    def.DrawBackground(rect);

                    if (!string.IsNullOrEmpty(title))
                    {
                        var ls = new GUIStyle(EditorStyles.boldLabel);
                        def.GetResolvedTitleText().Apply(ls);
                        EditorGUILayout.LabelField(title, ls);
                        GUILayout.Space(2);
                    }
                }
                else
                {
                    EditorGUILayout.BeginVertical();
                    if (!string.IsNullOrEmpty(title))
                    {
                        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                        GUILayout.Space(2);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_boxOpened)
            {
                EditorGUILayout.EndVertical();
                if (_hasBoxDef) PopBoxContext();
            }
            _foldoutScope.Dispose();
        }
    }

    // ===== AreaBoxScope =======================================================

    private readonly struct AreaBoxScope : IDisposable
    {
        private readonly IDisposable _box;

        public AreaBoxScope(IDisposable box) { _box = box; }

        public void Dispose()
        {
            _box.Dispose();
            GUILayout.EndArea();
        }
    }

    // ===== ZUIStyle enum ======================================================

    public enum ZUIStyle
    {
        Default,
        Alternative,
        Warning,
        Subtle,
        Info,
        Alternative2,
        Alternate,
    }

    // Translates a ZUICornerMask into (roundTL, roundTR, roundBL, roundBR).
    // When mask is None, falls back to the def's own per-corner flags.
    internal static (bool tl, bool tr, bool bl, bool br) ResolveCornerMask(ZUIButtonDef def, ZUICornerMask mask)
    {
        switch (mask)
        {
            case ZUICornerMask.All:    return (true,  true,  true,  true);
            case ZUICornerMask.Left:   return (true,  false, true,  false);
            case ZUICornerMask.Right:  return (false, true,  false, true);
            case ZUICornerMask.Top:    return (true,  true,  false, false);
            case ZUICornerMask.Bottom: return (false, false, true,  true);
            case ZUICornerMask.Square: return (false, false, false, false);
            default:                   return def.GetResolvedCornerFlags();
        }
    }

    // Converts resolved corner booleans + radius into a Vector4 for GPU draw.
    // Unity GUI.DrawTexture borderRadius order: x=TL, y=TR, z=BR, w=BL
    internal static Vector4 CornerMaskToVector(bool tl, bool tr, bool bl, bool br, float r)
        => new Vector4(tl ? r : 0f, tr ? r : 0f, br ? r : 0f, bl ? r : 0f);

    // ===== SectionStyle =======================================================

    public class SectionStyle
    {
        public GUIStyle GuiStyle;
        public GUIStyle LabelStyle;
    }

    // ===== SectionStyleRegistry ===============================================
    // Lazy init — NOT a static constructor.
    // Static constructors run once and their textures are destroyed on domain
    // reload while the dictionary shell survives, leaving dangling references.

    static class SectionStyleRegistry
    {
        static Dictionary<ZUIStyle, SectionStyle> _styles;

        public static SectionStyle Get(ZUIStyle key)
        {
            if (_styles == null) Build();
            if (_styles.TryGetValue(key, out var s)) return s;
            return _styles[ZUIStyle.Default];
        }

        static void Build()
        {
            _styles = new Dictionary<ZUIStyle, SectionStyle>
            {
                { ZUIStyle.Default,      Create(new Color(.8f,  0.8f, 1f,   0.12f)) },
                { ZUIStyle.Alternative,  Create(new Color(.8f,  0.8f, 1f,   0.12f)) },
                { ZUIStyle.Warning,      Create(new Color(1f,   0.3f, 0.2f, 0.15f)) },
                { ZUIStyle.Subtle,       Create(new Color(1f,   1f,   1f,   0.05f)) },
                { ZUIStyle.Info,         Create(new Color(0.2f, 0.6f, 1f,   0.15f)) },
                { ZUIStyle.Alternative2, Create(new Color(0.6f, 0.8f, 0.4f, 0.12f)) },
                { ZUIStyle.Alternate,    Create(new Color(0.8f, 0.5f, 1f,   0.12f)) },
            };
        }

        static SectionStyle Create(Color bg)
        {
            var tex = MakeTex(bg);

            var gui = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin  = new RectOffset(4, 4, 4, 4),
            };
            gui.normal.background = tex;

            return new SectionStyle
            {
                GuiStyle   = gui,
                LabelStyle = EditorStyles.boldLabel,
            };
        }

        internal static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}

// ===== ZUICornerMask — per-call corner rounding override =====================
// Top-level enum so call sites can use it without ZUI. qualifier.
// Pass to ZUI.Button/ZUI.Toggle overloads to override which corners are rounded
// without needing a separate style sheet entry per variant.
// None = use the def's own roundTL/TR/BL/BR settings (no override).

public enum ZUICornerMask
{
    None,    // no override — use the def's roundTL/TR/BL/BR
    All,     // all four corners
    Left,    // TL + BL  (left end of a button group)
    Right,   // TR + BR  (right end of a button group)
    Top,     // TL + TR  (top of a vertical button group)
    Bottom,  // BL + BR  (bottom of a vertical button group)
    Square,  // all corners square (no rounding)
}
