// ZUIText.cs

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ── Box context stack ──────────────────────────────────────────────────────
    // BoxScope pushes/pops so that ZUI.Label() can inherit the box's content style.

    static readonly Stack<ZUIBoxDef> _boxStack = new Stack<ZUIBoxDef>();

    internal static void PushBoxContext(ZUIBoxDef def) => _boxStack.Push(def);

    internal static void PopBoxContext()
    {
        if (_boxStack.Count > 0) _boxStack.Pop();
    }

    static ZUIBoxDef CurrentBoxDef => _boxStack.Count > 0 ? _boxStack.Peek() : null;

    // ── Shadow-aware label draw ────────────────────────────────────────────────
    // Draws shadow pass first (offset + contentColor tint), then main pass.

    internal static void DrawLabel(Rect rect, GUIContent content, GUIStyle style, ZUITextDef textDef)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (textDef != null && textDef.shadowEnabled && textDef.shadowColor.a > 0f)
        {
            var sr = new Rect(rect.x + textDef.shadowOffset.x, rect.y + textDef.shadowOffset.y,
                              rect.width, rect.height);
            var shadowStyle = new GUIStyle(style);
            Color sc = textDef.GetResolvedShadowColor();
            shadowStyle.normal.textColor = sc;
            shadowStyle.Draw(sr, content, false, false, false, false);
        }
        style.Draw(rect, content, false, false, false, false);
    }

    // ── Label API — inherits box content style when called inside a BoxScope ───

    public static void Label(string text, params GUILayoutOption[] options)
    {
        var boxDef = CurrentBoxDef;
        if (boxDef != null)
        {
            DrawLayoutLabel(new GUIContent(text), boxDef.GetResolvedContentText(),
                            boxDef.GetContentStyle(), options);
            return;
        }
        GUILayout.Label(text, options);
    }

    public static GUIStyle GetTextStyle(ZTextStyle style)
    {
        var sheet = ActiveSheet;
        if (sheet != null)
        {
            var def = sheet.FindText(style.ToString());
            if (def != null) return def.GetStyle();
        }
        return TextStyleRegistry.Get(style);
    }

    public static void Label(string text, ZTextStyle style, params GUILayoutOption[] options)
    {
        var sheet = ActiveSheet;
        if (sheet != null)
        {
            var def = sheet.FindText(style.ToString());
            if (def != null)
            {
                DrawLayoutLabel(new GUIContent(text), def.text, def.GetStyle(), options, style, def);
                return;
            }
        }
        DrawLayoutLabel(new GUIContent(text), null, TextStyleRegistry.Get(style), options, style, null);
    }

    public static void Label(string text, ZUITextStyleDef def, params GUILayoutOption[] options)
        => DrawLayoutLabel(new GUIContent(text), def.text, def.GetStyle(), options);

    public static void Label(Rect rect, string text)
    {
        var boxDef = CurrentBoxDef;
        DrawLabel(rect, new GUIContent(text),
                  boxDef?.GetContentStyle() ?? EditorStyles.label,
                  boxDef?.GetResolvedContentText());
    }

    public static void Label(Rect rect, string text, ZTextStyle style)
    {
        var sheet = ActiveSheet;
        ZUITextDef textDef = null;
        GUIStyle guiStyle;
        ZUITextStyleDef styleDef = null;
        if (sheet != null)
        {
            styleDef = sheet.FindText(style.ToString());
            if (styleDef != null) { textDef = styleDef.text; guiStyle = styleDef.GetStyle(); }
            else guiStyle = TextStyleRegistry.Get(style);
        }
        else guiStyle = TextStyleRegistry.Get(style);
        if (CheckDebugContextClick(rect)) { CollectTextDebugInfo(styleDef, style, textDef, rect); return; }
        DrawLabel(rect, new GUIContent(text), guiStyle, textDef);
    }

    public static void Label(Rect rect, string text, ZUITextStyleDef def)
        => DrawLabel(rect, new GUIContent(text), def.GetStyle(), def.text);

    static void DrawLayoutLabel(GUIContent content, ZUITextDef textDef, GUIStyle style,
                                GUILayoutOption[] options,
                                ZTextStyle debugStyle = ZTextStyle.Default, ZUITextStyleDef debugDef = null)
    {
        var rect = GUILayoutUtility.GetRect(content, style, options);
        if (CheckDebugContextClick(rect))
        {
            if (debugDef != null) CollectTextDebugInfo(debugDef, debugStyle, rect);
            else CollectTextDebugInfo(textDef, debugStyle, rect);
            return;
        }
        DrawLabel(rect, content, style, textDef);
    }

    // ── ZTextStyle enum ────────────────────────────────────────────────────────

    public enum ZTextStyle
    {
        Default,
        Title,
        Header,
        Subheader,
        Small,
        Subtle,
        Accent,
    }

    // ── TextStyleRegistry ─────────────────────────────────────────────────────
    // Fallback when no sheet is loaded. Lazy init.

    static class TextStyleRegistry
    {
        static Dictionary<ZTextStyle, GUIStyle> _styles;

        public static GUIStyle Get(ZTextStyle key)
        {
            if (_styles == null) Build();
            if (_styles.TryGetValue(key, out var s)) return s;
            return _styles[ZTextStyle.Default];
        }

        static void Build()
        {
            _styles = new Dictionary<ZTextStyle, GUIStyle>
            {
                { ZTextStyle.Default,   Make(new Color(.88f, .88f, .88f, 1f), 0,  FontStyle.Normal) },
                { ZTextStyle.Title,     MakeTitle()                                                  },
                { ZTextStyle.Header,    Make(new Color(.95f, .95f, .95f, 1f), 14, FontStyle.Bold)   },
                { ZTextStyle.Subheader, Make(new Color(.90f, .90f, .90f, 1f), 0,  FontStyle.Bold)   },
                { ZTextStyle.Small,     Make(new Color(.70f, .70f, .70f, 1f), 9,  FontStyle.Normal) },
                { ZTextStyle.Subtle,    Make(new Color(.55f, .55f, .55f, 1f), 0,  FontStyle.Normal) },
                { ZTextStyle.Accent,    Make(new Color(.70f, .88f, 1f,   1f), 0,  FontStyle.Normal) },
            };
        }

        static GUIStyle Make(Color color, int fontSize, FontStyle fontStyle)
        {
            var s = new GUIStyle(EditorStyles.label) { wordWrap = true };
            s.normal.textColor = color;
            s.fontStyle = fontStyle;
            if (fontSize > 0) s.fontSize = fontSize;
            return s;
        }

        static GUIStyle MakeTitle()
        {
            var s = new GUIStyle(EditorStyles.label)
            {
                wordWrap  = false,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize  = 18,
            };
            s.normal.textColor = new Color(.98f, .98f, .98f, 1f);
            return s;
        }
    }
}
