using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ZUI
{
    // ===== Public API =====

    public static BoxScope Box(string title, ZUIStyle style = ZUIStyle.Default)
    {
        var s = SectionStyleRegistry.Get(style);
        return new BoxScope(title, s);
    }

    public static BoxScope Box(ZUIStyle style = ZUIStyle.Default)
    {
        var s = SectionStyleRegistry.Get(style);
        return new BoxScope(null, s);
    }

    // ===== Scope =====

    public readonly struct BoxScope : IDisposable
    {
        public BoxScope(string title, SectionStyle style)
        {
            EditorGUILayout.BeginVertical(style.GuiStyle);

            if (!string.IsNullOrEmpty(title))
            {
                EditorGUILayout.LabelField(title, style.LabelStyle);
                GUILayout.Space(2);
            }
        }

        public void Dispose()
        {
            EditorGUILayout.EndVertical();
        }
    }

    public static IDisposable AreaBox(Rect rect, string title = null, ZUIStyle style = ZUIStyle.Default)
    {
        GUILayout.BeginArea(rect);

        var box = Box(title, style);

        return new AreaBoxScope(box);
    }

    private readonly struct AreaBoxScope : IDisposable
    {
        private readonly IDisposable _box;

        public AreaBoxScope(IDisposable box)
        {
            _box = box;
        }

        public void Dispose()
        {
            _box.Dispose();          // EndVertical (Box)
            GUILayout.EndArea();     // EndArea
        }
    }

    // ===== Style System =====

    public enum ZUIStyle
    {
        Default,
        Alternative,
        Warning,
        Subtle
    }

    public class SectionStyle
    {
        public GUIStyle GuiStyle;
        public GUIStyle LabelStyle;
    }

    static class SectionStyleRegistry
    {
        static readonly Dictionary<ZUIStyle, SectionStyle> _styles;

        static SectionStyleRegistry()
        {
            _styles = new Dictionary<ZUIStyle, SectionStyle>
            {
                { ZUIStyle.Default, Create(new Color(.8f, 0.8f, 1f, 0.12f)) },
                { ZUIStyle.Alternative, Create(new Color(.8f, 0.8f, 1f, 0.12f)) },
                { ZUIStyle.Warning, Create(new Color(1f, 0.3f, 0.2f, 0.15f)) },
                { ZUIStyle.Subtle,  Create(new Color(1f, 1f, 1f, 0.05f)) }
        };
    }

    public static SectionStyle Get(ZUIStyle key)
    {
        return _styles[key];
    }

    static SectionStyle Create(Color bg)
    {
        var tex = MakeTex(bg);

        var gui = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(4, 4, 4, 4)
        };

        gui.normal.background = tex;

        return new SectionStyle
        {
            GuiStyle = gui,
            LabelStyle = EditorStyles.boldLabel
        };
    }

    static Texture2D MakeTex(Color col)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }
}
}