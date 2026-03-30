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
    internal const string k_DefaultSheetPath = "Assets/ZUI/ZUIStyleSheet.asset";

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

    // ===== Icon library ======================================================

    public static Texture2D FindIcon(string id) => ActiveSheet?.iconLibrary?.Find(id);

    // ===== Palette color lookup ==============================================

    /// <summary>
    /// Returns a palette color from the active sheet by name and slot.
    /// Falls back to <paramref name="fallback"/> when no sheet is loaded or the entry is not found.
    /// Usage: ZUI.PaletteColor("Warning", ZUIPaletteSlot.Primary, new Color(...))
    /// </summary>
    public static Color PaletteColor(string name, ZUIPaletteSlot slot, Color fallback)
    {
        var entry = ActiveSheet?.FindPaletteColor(name);
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
        EditorApplication.update -= OnFlashUpdate;
        EditorApplication.update += OnFlashUpdate;
    }

    private static void OnFlashUpdate()
    {
        if (string.IsNullOrEmpty(_flashStyleName) || EditorApplication.timeSinceStartup > _flashEndTime)
        {
            _flashStyleName = null;
            EditorApplication.update -= OnFlashUpdate;
        }
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            w.Repaint();
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

    // ===== Vertical Spacing ==================================================

    private const float k_FallbackVerticalSpacing = 6f;

    private static bool   _verticalSpaceFlashActive;
    private static double _verticalSpaceFlashEndTime;

    /// <summary>Triggers the animated spacing overlay on every ZUI.VerticalSpace() call.</summary>
    public static void StartVerticalSpaceFlash()
    {
        _verticalSpaceFlashActive  = true;
        _verticalSpaceFlashEndTime = EditorApplication.timeSinceStartup + ActiveFlashCount * ActiveFlashInterval;
        EditorApplication.update -= OnVerticalSpaceFlashUpdate;
        EditorApplication.update += OnVerticalSpaceFlashUpdate;
    }

    private static void OnVerticalSpaceFlashUpdate()
    {
        if (!_verticalSpaceFlashActive || EditorApplication.timeSinceStartup > _verticalSpaceFlashEndTime)
        {
            _verticalSpaceFlashActive = false;
            EditorApplication.update -= OnVerticalSpaceFlashUpdate;
        }
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            w.Repaint();
    }

    // Called after GUILayout.Space() — uses GetLastRect() to draw on top of the
    // already-consumed space, so zero extra layout height is added.
    private static void DrawVerticalSpaceOverlay(float scale)
    {
        if (!_verticalSpaceFlashActive) return;
        double t = EditorApplication.timeSinceStartup;
        if (t > _verticalSpaceFlashEndTime) { _verticalSpaceFlashActive = false; return; }
        if (Event.current.type != EventType.Repaint) return;

        var spaceRect  = GUILayoutUtility.GetLastRect();
        float interval = ActiveFlashInterval;
        int   phase    = (int)((t % (interval * 2)) / interval);
        var lineColor  = phase == 0 ? Color.white : Color.black;
        var labelColor = phase == 0 ? Color.black  : Color.white;

        float lineH = 2f;
        float lineY = spaceRect.y + (spaceRect.height - lineH) * 0.5f;

        var prev = GUI.color;

        GUI.color = lineColor;
        GUI.DrawTexture(new Rect(spaceRect.x, lineY, spaceRect.width, lineH), EditorGUIUtility.whiteTexture);

        string label = scale == 1f
            ? $"×1  ({spaceRect.height:F0}px)"
            : $"×{scale:F1}  ({spaceRect.height:F0}px)";

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal    = { textColor = labelColor },
            fontStyle = FontStyle.Bold,
            fontSize  = 9,
            padding   = new RectOffset(2, 2, 0, 0),
        };
        Vector2 labelSize = labelStyle.CalcSize(new GUIContent(label));
        float labelX = spaceRect.x + spaceRect.width * 0.5f - labelSize.x * 0.5f;
        float labelY = lineY - labelSize.y;

        GUI.color = lineColor;
        GUI.DrawTexture(new Rect(labelX - 2f, labelY, labelSize.x + 4f, labelSize.y), EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(labelX, labelY, labelSize.x, labelSize.y), label, labelStyle);

        GUI.color = prev;
    }

    /// <summary>
    /// Inserts the standard vertical gap between rows, as configured in the active ZUI Style Sheet.
    /// </summary>
    public static void VerticalSpace()
    {
        GUILayout.Space(ActiveSheet?.verticalSpacing ?? k_FallbackVerticalSpacing);
        DrawVerticalSpaceOverlay(1f);
    }

    /// <summary>
    /// Inserts a scaled multiple of the standard vertical spacing.
    /// Use 0.5f for tight gaps, 2f for section breaks, etc.
    /// </summary>
    public static void VerticalSpace(float scale)
    {
        GUILayout.Space((ActiveSheet?.verticalSpacing ?? k_FallbackVerticalSpacing) * scale);
        DrawVerticalSpaceOverlay(scale);
    }

    // Backwards-compatible aliases so existing RowSpace() calls don't need to change.
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

                if (def.titleIcon != null)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        float sz = def.titleIconSize;
                        GUILayout.Label(def.titleIcon, GUILayout.Width(sz), GUILayout.Height(sz));
                        GUILayout.Space(2f);
                        EditorGUILayout.LabelField(title, ls);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(title, ls);
                }
                GUILayout.Space(2);
            }
        }

        public void Dispose()
        {
            EditorGUILayout.EndVertical();
            if (_hasContext) ZUI.PopBoxContext();
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
            default:                   return (def.roundTL, def.roundTR, def.roundBL, def.roundBR);
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
}
