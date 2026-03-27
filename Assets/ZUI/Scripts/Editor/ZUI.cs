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

    // ===== Style flash =======================================================
    // Flashes an overlay border on every control that uses a named style def.
    // Call StartFlash(name) to begin; controls pick it up each Repaint automatically.

    public enum FlashDefType { Button, Box }

    private static string      _flashStyleName;
    private static FlashDefType _flashDefType;
    private static double      _flashEndTime;
    private static int         _flashCount    = 6;
    private static float       _flashInterval = 0.12f;

    public static void StartFlash(string styleName, FlashDefType type)
    {
        _flashStyleName = styleName;
        _flashDefType   = type;
        _flashEndTime   = EditorApplication.timeSinceStartup + _flashCount * _flashInterval;
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
        // Repaint all editor windows so the flash animates across all ZUI controls
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            w.Repaint();
    }

    internal static void DrawFlashOverlayIfNeeded(Rect rect, string defName, int cornerRadius, FlashDefType type)
    {
        if (string.IsNullOrEmpty(_flashStyleName) || defName != _flashStyleName || type != _flashDefType) return;
        double t = EditorApplication.timeSinceStartup;
        if (t > _flashEndTime) { _flashStyleName = null; return; }

        int phase = (int)((t % (_flashInterval * 2)) / _flashInterval);  // 0 or 1
        var color = phase == 0 ? Color.white : Color.black;

        var prev = GUI.color;
        GUI.color = color;
        // Draw as an inset border (2px) using four thin rects — works regardless of corner radius
        float bw = 2f;
        GUI.DrawTexture(new Rect(rect.x,              rect.y,               rect.width, bw),          EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,              rect.yMax - bw,       rect.width, bw),          EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,              rect.y,               bw,         rect.height), EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - bw,      rect.y,               bw,         rect.height), EditorGUIUtility.whiteTexture);
        GUI.color = prev;
    }

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
