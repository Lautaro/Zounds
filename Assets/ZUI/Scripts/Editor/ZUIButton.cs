// ZUIButton.cs

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // When true, DrawManualButton renders with cornerRadius = 0 regardless of the def.
    // Used by the Style Editor preview to simulate older Unity / no-rounding fallback.
    public static bool SimulateLegacyCorners = false;

    // ===== Button API — ZButtonStyle (enum-keyed) ============================
    // When a sheet is loaded: routes to the sheet's def via name lookup + manual draw.
    // When no sheet: falls back to ButtonStyleRegistry with baked GUIStyle textures.

    public static bool Button(string label, ZButtonStyle style = ZButtonStyle.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def  = sheet.FindButton(style.ToString());
            var rect = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(), options);
            return DrawManualButton(rect, new GUIContent(label), def);
        }
        return GUILayout.Button(label, ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(GUIContent content, ZButtonStyle style = ZButtonStyle.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def  = sheet.FindButton(style.ToString());
            var rect = GUILayoutUtility.GetRect(content, def.GetLabelStyle(), options);
            return DrawManualButton(rect, content, def);
        }
        return GUILayout.Button(content, ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(Rect rect, string label, ZButtonStyle style = ZButtonStyle.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualButton(rect, new GUIContent(label), sheet.FindButton(style.ToString()));
        return GUI.Button(rect, label, ButtonStyleRegistry.Get(style));
    }

    public static bool Button(Rect rect, GUIContent content, ZButtonStyle style = ZButtonStyle.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualButton(rect, content, sheet.FindButton(style.ToString()));
        return GUI.Button(rect, content, ButtonStyleRegistry.Get(style));
    }

    // ===== Button API — with explicit icon override ===========================
    // Icon and placement override whatever is stored in the def.

    public static bool Button(string label, Texture2D icon, ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                              ZButtonStyle style = ZButtonStyle.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def  = sheet.FindButton(style.ToString());
            var rect = GUILayoutUtility.GetRect(MakeContent(label, icon, placement), def.GetLabelStyle(), options);
            return DrawManualButton(rect, new GUIContent(label), def, icon, placement);
        }
        return GUILayout.Button(new GUIContent(label, icon), ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(Rect rect, string label, Texture2D icon, ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                              ZButtonStyle style = ZButtonStyle.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualButton(rect, new GUIContent(label), sheet.FindButton(style.ToString()), icon, placement);
        return GUI.Button(rect, new GUIContent(label, icon), ButtonStyleRegistry.Get(style));
    }

    // ===== Button API — string icon ID (looked up from ZUI.ActiveSheet.iconLibrary) ===

    public static bool Button(string label, string iconId,
                              ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                              ZButtonStyle style = ZButtonStyle.Default,
                              params GUILayoutOption[] options)
        => Button(label, ZUI.FindIcon(iconId), placement, style, options);

    public static bool Button(Rect rect, string label, string iconId,
                              ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                              ZButtonStyle style = ZButtonStyle.Default)
        => Button(rect, label, ZUI.FindIcon(iconId), placement, style);

    // ===== Button API — ZUIButtonDef (named style def, manual draw) ===========

    public static bool Button(string label, ZUIButtonDef def, params GUILayoutOption[] options)
    {
        var rect = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(), options);
        return DrawManualButton(rect, new GUIContent(label), def);
    }

    public static bool Button(GUIContent content, ZUIButtonDef def, params GUILayoutOption[] options)
    {
        var rect = GUILayoutUtility.GetRect(content, def.GetLabelStyle(), options);
        return DrawManualButton(rect, content, def);
    }

    public static bool Button(Rect rect, string label, ZUIButtonDef def)
        => DrawManualButton(rect, new GUIContent(label), def);

    public static bool Button(Rect rect, GUIContent content, ZUIButtonDef def)
        => DrawManualButton(rect, content, def);

    // ── Content helper ────────────────────────────────────────────────────────
    // Used by GUILayoutUtility.GetRect to include icon dimensions in layout sizing.

    static GUIContent MakeContent(string label, ZUIButtonDef def)
        => def.icon != null ? new GUIContent(label, def.icon) : new GUIContent(label);

    static GUIContent MakeContent(string label, Texture2D icon, ZIconPlacement placement)
        => icon != null ? new GUIContent(label, icon) : new GUIContent(label);

    // ── Manual button draw ────────────────────────────────────────────────────
    // Hover tracked via rect + mousePosition (requires wantsMouseMove — ZUIWindow sets this).
    // Active state tracked via GUIUtility.hotControl.
    // SimulateLegacyCorners forces cornerRadius = 0 for the preview fallback toggle.
    // icon/placement override whatever is stored in the def when explicitly passed.

    static bool DrawManualButton(Rect rect, GUIContent content, ZUIButtonDef def,
                                 Texture2D icon = null, ZIconPlacement placement = ZIconPlacement.LeftOfLabel)
    {
        if (!GUI.enabled)
        {
            if (Event.current.type == EventType.Repaint)
            {
                int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
                var prev = GUI.color;
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.4f);
                def.DrawVisual(rect, ZUIButtonDrawState.Normal, r);
                DrawButtonLabel(rect, content, def.GetLabelStyle(ZUIButtonDrawState.Normal), icon, placement, def, def.GetNormalText());
                GUI.color = prev;
            }
            return false;
        }

        int id = GUIUtility.GetControlID(FocusType.Passive, rect);
        var ev = Event.current;
        bool isHover = rect.Contains(ev.mousePosition);
        bool isActive = GUIUtility.hotControl == id;
        bool clicked = false;

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (isHover && ev.button == 0)
                {
                    GUIUtility.hotControl = id;
                    ev.Use();
                }
                break;

            case EventType.MouseUp:
                if (isActive)
                {
                    GUIUtility.hotControl = 0;
                    ev.Use();
                    if (isHover) clicked = true;
                }
                break;
        }

        if (ev.type == EventType.Repaint)
        {
            int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
            var s = isActive ? ZUIButtonDrawState.Active : (isHover ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal);
            def.DrawVisual(rect, s, r);
            DrawButtonLabel(rect, content, def.GetLabelStyle(s), icon, placement, def, def.GetText(s));
        }

        return clicked;
    }

    // Draws the button label, handling icon placement when an icon is present.
    // icon parameter wins over def.icon when non-null.
    internal static void DrawButtonLabel(Rect rect, GUIContent content, GUIStyle labelStyle,
                                         Texture2D icon, ZIconPlacement placement, ZUIButtonDef def,
                                         ZUITextDef textDef = null)
    {
        var drawIcon = icon ?? def.icon;
        if (drawIcon == null)
        {
            DrawLabel(rect, content, labelStyle, textDef);
            return;
        }

        var drawPlacement = icon != null ? placement : def.iconPlacement;
        float sz  = def.iconSize;
        float pad = 4f;

        switch (drawPlacement)
        {
            case ZIconPlacement.LeftEdge:
            {
                var iconRect = new Rect(rect.x + pad, rect.y + (rect.height - sz) * 0.5f, sz, sz);
                GUI.DrawTexture(iconRect, drawIcon, ScaleMode.ScaleToFit, true);
                var textRect = new Rect(rect.x + pad + sz + 2f, rect.y, rect.width - pad - sz - 2f, rect.height);
                DrawLabel(textRect, new GUIContent(content.text), labelStyle, textDef);
                break;
            }
            case ZIconPlacement.RightEdge:
            {
                var iconRect = new Rect(rect.xMax - pad - sz, rect.y + (rect.height - sz) * 0.5f, sz, sz);
                GUI.DrawTexture(iconRect, drawIcon, ScaleMode.ScaleToFit, true);
                var textRect = new Rect(rect.x, rect.y, rect.width - pad - sz - 2f, rect.height);
                DrawLabel(textRect, new GUIContent(content.text), labelStyle, textDef);
                break;
            }
            case ZIconPlacement.RightOfLabel:
            {
                var textContent = new GUIContent(content.text);
                var textSize    = labelStyle.CalcSize(textContent);
                float totalW    = textSize.x + 2f + sz;
                float startX    = rect.x + (rect.width - totalW) * 0.5f;
                var textStyle   = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft };
                DrawLabel(new Rect(startX, rect.y, textSize.x, rect.height), textContent, textStyle, textDef);
                GUI.DrawTexture(new Rect(startX + textSize.x + 2f, rect.y + (rect.height - sz) * 0.5f, sz, sz),
                                drawIcon, ScaleMode.ScaleToFit, true);
                break;
            }
            default: // LeftOfLabel
            {
                var textContent = new GUIContent(content.text);
                var textSize    = labelStyle.CalcSize(textContent);
                float totalW    = sz + 2f + textSize.x;
                float startX    = rect.x + (rect.width - totalW) * 0.5f;
                GUI.DrawTexture(new Rect(startX, rect.y + (rect.height - sz) * 0.5f, sz, sz),
                                drawIcon, ScaleMode.ScaleToFit, true);
                var textStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft };
                DrawLabel(new Rect(startX + sz + 2f, rect.y, textSize.x, rect.height), textContent, textStyle, textDef);
                break;
            }
        }
    }

    // ===== ZButtonStyle enum =================================================

    public enum ZButtonStyle
    {
        Default,
        Confirm,
        Danger,
        Subtle,
        Active,
        Alternative,
        Cancel
    }

    // ===== ButtonStyleRegistry ===============================================
    // Fallback when no sheet is loaded. Lazy init — NOT a static constructor.

    static class ButtonStyleRegistry
    {
        static Dictionary<ZButtonStyle, GUIStyle> _styles;

        public static GUIStyle Get(ZButtonStyle key)
        {
            if (_styles == null || _styles[ZButtonStyle.Default].normal.background == null)
                Build();
            if (_styles.TryGetValue(key, out var s)) return s;
            return _styles[ZButtonStyle.Default];
        }

        static void Build()
        {
            _styles = new Dictionary<ZButtonStyle, GUIStyle>
            {
                { ZButtonStyle.Default,     Make(new Color(.22f, .22f, .26f, 1f), new Color(.30f, .30f, .36f, 1f), new Color(.16f, .16f, .20f, 1f), new Color(.88f, .88f, .88f, 1f)) },
                { ZButtonStyle.Confirm,     Make(new Color(.14f, .34f, .14f, 1f), new Color(.18f, .44f, .18f, 1f), new Color(.10f, .24f, .10f, 1f), new Color(.72f, 1f,   .72f, 1f)) },
                { ZButtonStyle.Danger,      Make(new Color(.40f, .12f, .10f, 1f), new Color(.54f, .16f, .13f, 1f), new Color(.28f, .08f, .07f, 1f), new Color(1f,   .72f, .70f, 1f)) },
                { ZButtonStyle.Subtle,      Make(new Color(.20f, .20f, .20f, .30f), new Color(.30f, .30f, .30f, .45f), new Color(.14f, .14f, .14f, .40f), new Color(.65f, .65f, .65f, 1f)) },
                { ZButtonStyle.Active,      Make(new Color(.20f, .38f, .55f, 1f), new Color(.25f, .46f, .65f, 1f), new Color(.14f, .28f, .42f, 1f), new Color(.75f, .92f, 1f,   1f)) },
                { ZButtonStyle.Alternative, Make(new Color(.55f, .38f, .10f, 1f), new Color(.68f, .48f, .14f, 1f), new Color(.40f, .28f, .08f, 1f), new Color(1f,   .88f, .55f, 1f)) },
                { ZButtonStyle.Cancel,      Make(new Color(.38f, .15f, .15f, 1f), new Color(.48f, .20f, .20f, 1f), new Color(.28f, .10f, .10f, 1f), new Color(.95f, .70f, .70f, 1f)) },
            };
        }

        static GUIStyle Make(Color normal, Color hover, Color active, Color textColor)
        {
            var s = new GUIStyle(GUIStyle.none);
            SetState(s.normal,  MakeFlatTex(normal), textColor);
            SetState(s.hover,   MakeFlatTex(hover),  textColor);
            SetState(s.active,  MakeFlatTex(active), textColor);
            SetState(s.focused, MakeFlatTex(hover),  textColor);
            s.border    = new RectOffset(0, 0, 0, 0);
            s.padding   = new RectOffset(10, 10, 3, 4);
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize  = EditorStyles.miniButton.fontSize;
            s.font      = EditorStyles.miniButton.font;
            return s;
        }

        static void SetState(GUIStyleState state, Texture2D tex, Color text)
        {
            state.background        = tex;
            state.scaledBackgrounds = new Texture2D[] { tex };
            state.textColor         = text;
        }

        static Texture2D MakeFlatTex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply(); return t;
        }
    }
}
