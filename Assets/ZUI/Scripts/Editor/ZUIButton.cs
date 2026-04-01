// ZUIButton.cs

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // When true, DrawManualButton renders with cornerRadius = 0 regardless of the def.
    // Used by the Style Editor preview to simulate older Unity / no-rounding fallback.
    public static bool SimulateLegacyCorners = false;

    // ===== Style name constants ===============================================
    // Use these for compile-time safety. Custom stylesheet styles use raw strings.

    public static class Style
    {
        public const string Default     = "Default";
        public const string Confirm     = "Confirm";
        public const string Danger      = "Danger";
        public const string Subtle      = "Subtle";
        public const string Active      = "Active";
        public const string Alternative = "Alternative";
        public const string Cancel      = "Cancel";
        public const string ZoundBtn         = "ZoundBtn";      // Zound browser main name button
        public const string ZoundBtnFlat     = "ZoundBtnFlat";  // Zound browser secondary buttons
        public const string ZoundBtnFlatToggle = "ZoundBtnFlatToggle"; // M/S toggles
        public const string RichToggle       = "RichToggle";    // Standard toggle style
        public const string RichButton       = "RichButton";    // Standard button style
    }


    // ===== Button API — string style name =====================================
    // When a sheet is loaded: routes to the sheet's def via name lookup + manual draw.
    // When no sheet: falls back to ButtonStyleRegistry with baked GUIStyle textures.

    public static bool Button(string label, string style = Style.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def     = sheet.FindButton(style);
            bool icoOnly = IsIconOnly(new GUIContent(label), def, null);
            var rect    = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(iconOnly: icoOnly), options);
            return DrawManualButton(rect, new GUIContent(label), def, debugStyle: style);
        }
        return GUILayout.Button(label, ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(string label, string style, ZUICornerMask cornerMask, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def     = sheet.FindButton(style);
            bool icoOnly = IsIconOnly(new GUIContent(label), def, null);
            var rect    = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(iconOnly: icoOnly), options);
            return DrawManualButton(rect, new GUIContent(label), def, debugStyle: style, cornerMask: cornerMask);
        }
        return GUILayout.Button(label, ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(GUIContent content, string style = Style.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def     = sheet.FindButton(style);
            bool icoOnly = IsIconOnly(content, def, null);
            var rect    = GUILayoutUtility.GetRect(content, def.GetLabelStyle(iconOnly: icoOnly), options);
            return DrawManualButton(rect, content, def, debugStyle: style);
        }
        return GUILayout.Button(content, ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(GUIContent content, string style, ZUICornerMask cornerMask, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def     = sheet.FindButton(style);
            bool icoOnly = IsIconOnly(content, def, null);
            var rect    = GUILayoutUtility.GetRect(content, def.GetLabelStyle(iconOnly: icoOnly), options);
            return DrawManualButton(rect, content, def, debugStyle: style, cornerMask: cornerMask);
        }
        return GUILayout.Button(content, ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(Rect rect, string label, string style = Style.Default,
                              ZUICornerMask cornerMask = ZUICornerMask.None)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualButton(rect, new GUIContent(label), sheet.FindButton(style), debugStyle: style, cornerMask: cornerMask);
        return GUI.Button(rect, label, ButtonStyleRegistry.Get(style));
    }

    public static bool Button(Rect rect, GUIContent content, string style = Style.Default,
                              ZUICornerMask cornerMask = ZUICornerMask.None)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualButton(rect, content, sheet.FindButton(style), debugStyle: style, cornerMask: cornerMask);
        return GUI.Button(rect, content, ButtonStyleRegistry.Get(style));
    }

    // ===== Button API — with explicit icon override ===========================
    // Icon and placement override whatever is stored in the def.

    public static bool Button(string label, Texture2D icon, ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                              string style = Style.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def     = sheet.FindButton(style);
            bool icoOnly = string.IsNullOrEmpty(label);
            var rect    = GUILayoutUtility.GetRect(MakeContent(label, icon, placement), def.GetLabelStyle(iconOnly: icoOnly), options);
            return DrawManualButton(rect, new GUIContent(label), def, icon, placement, style);
        }
        return GUILayout.Button(new GUIContent(label, icon), ButtonStyleRegistry.Get(style), options);
    }

    public static bool Button(Rect rect, string label, Texture2D icon, ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                              string style = Style.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualButton(rect, new GUIContent(label), sheet.FindButton(style), icon, placement, style);
        return GUI.Button(rect, new GUIContent(label, icon), ButtonStyleRegistry.Get(style));
    }

    // ===== Button API — string icon ID (looked up from ZUI.ActiveSheet.iconLibrary) ===

    public static bool Button(string label, string iconId,
                              ZIconPlacement placement,
                              string style = Style.Default,
                              params GUILayoutOption[] options)
        => Button(label, ZUI.FindIcon(iconId), placement, style, options);

    public static bool Button(Rect rect, string label, string iconId,
                              ZIconPlacement placement,
                              string style = Style.Default)
        => Button(rect, label, ZUI.FindIcon(iconId), placement, style);

    // ===== Button API — ZUIButtonDef (named style def, manual draw) ===========

    public static bool Button(string label, ZUIButtonDef def, params GUILayoutOption[] options)
    {
        bool icoOnly = IsIconOnly(new GUIContent(label), def, null);
        var rect = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(iconOnly: icoOnly), options);
        return DrawManualButton(rect, new GUIContent(label), def);
    }

    public static bool Button(GUIContent content, ZUIButtonDef def, params GUILayoutOption[] options)
    {
        bool icoOnly = IsIconOnly(content, def, null);
        var rect = GUILayoutUtility.GetRect(content, def.GetLabelStyle(iconOnly: icoOnly), options);
        return DrawManualButton(rect, content, def);
    }

    public static bool Button(string label, ZUIButtonDef def, ZUICornerMask cornerMask, params GUILayoutOption[] options)
    {
        bool icoOnly = IsIconOnly(new GUIContent(label), def, null);
        var rect = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(iconOnly: icoOnly), options);
        return DrawManualButton(rect, new GUIContent(label), def, cornerMask: cornerMask);
    }

    public static bool Button(GUIContent content, ZUIButtonDef def, ZUICornerMask cornerMask, params GUILayoutOption[] options)
    {
        bool icoOnly = IsIconOnly(content, def, null);
        var rect = GUILayoutUtility.GetRect(content, def.GetLabelStyle(iconOnly: icoOnly), options);
        return DrawManualButton(rect, content, def, cornerMask: cornerMask);
    }

    public static bool Button(Rect rect, string label, ZUIButtonDef def, ZUICornerMask cornerMask = ZUICornerMask.None)
        => DrawManualButton(rect, new GUIContent(label), def, cornerMask: cornerMask);

    public static bool Button(Rect rect, GUIContent content, ZUIButtonDef def, ZUICornerMask cornerMask = ZUICornerMask.None)
        => DrawManualButton(rect, content, def, cornerMask: cornerMask);

    // ── Content helper ────────────────────────────────────────────────────────
    // Used by GUILayoutUtility.GetRect to include icon dimensions in layout sizing.

    static GUIContent MakeContent(string label, ZUIButtonDef def)
        => new GUIContent(label);

    static GUIContent MakeContent(string label, Texture2D icon, ZIconPlacement placement)
        => icon != null ? new GUIContent(label, icon) : new GUIContent(label);

    // Returns true when a button will render as icon-only (no visible text).
    static bool IsIconOnly(GUIContent content, ZUIButtonDef def, Texture2D iconOverride)
    {
        bool hasIcon = iconOverride != null || content.image != null;
        return hasIcon && string.IsNullOrEmpty(content.text);
    }

    // ── Manual button draw ────────────────────────────────────────────────────
    // Hover tracked via rect + mousePosition (requires wantsMouseMove — ZUIWindow sets this).
    // Active state tracked via GUIUtility.hotControl.
    // SimulateLegacyCorners forces cornerRadius = 0 for the preview fallback toggle.
    // icon/placement override whatever is stored in the def when explicitly passed.

    static bool DrawManualButton(Rect rect, GUIContent content, ZUIButtonDef def,
                                 Texture2D icon = null, ZIconPlacement placement = ZIconPlacement.LeftOfLabel,
                                 string debugStyle = null,
                                 ZUICornerMask cornerMask = ZUICornerMask.None)
    {
        // ── Style debug ───────────────────────────────────────────────────────
        if (CheckDebugContextClick(rect))
        {
            CollectButtonDebugInfo(def, debugStyle ?? def.name, rect);
            return false;
        }

        bool iconOnly = IsIconOnly(content, def, icon);

        if (!GUI.enabled)
        {
            if (Event.current.type == EventType.Repaint)
            {
                int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
                var prev = GUI.color;
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.4f);
                DrawVisualWithMask(rect, def, ZUIButtonDrawState.Normal, r, cornerMask);
                DrawButtonLabel(rect, content, def.GetLabelStyle(ZUIButtonDrawState.Normal, iconOnly), icon, placement, def, def.GetNormalText());
                GUI.color = prev;
            }
            return false;
        }

        int id = GUIUtility.GetControlID(FocusType.Passive, rect);
        var ev = Event.current;
        bool isHover = rect.Contains(ev.mousePosition);
        bool isActive = GUIUtility.hotControl == id;
        bool clicked = false;

        // Drive hover/click tween state
        ZUI.TweenNotifyHover(id, isHover, def);
        ZUI.TweenNotifyActive(id, isActive, def);

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (isHover && ev.button == 0)
                {
                    GUIUtility.hotControl = id;
                    ev.Use();
                }
                else if (isHover && ev.button == 1 && !StyleDebugMode)
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
                    if (isHover)
                        clicked = true;
                }
                break;
        }

        if (ev.type == EventType.Repaint)
        {
            int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
            var s = isActive ? ZUIButtonDrawState.Active : (isHover ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal);

            if (def.hoverAnimEnabled || def.clickAnimEnabled)
            {
                float hoverT = ZUI.TweenGetHoverT(id);
                float clickT = ZUI.TweenGetClickT(id);

                if (clickT > 0f && def.clickAnimEnabled)
                {
                    // Active animation — lerp Hover→Active
                    def.DrawVisualLerped(rect, ZUIButtonDrawState.Hover, ZUIButtonDrawState.Active,
                                         clickT, r, cornerMask);
                }
                else if (hoverT > 0f && def.hoverAnimEnabled)
                {
                    // Hover animation — lerp Normal→Hover
                    def.DrawVisualLerped(rect, ZUIButtonDrawState.Normal, ZUIButtonDrawState.Hover,
                                         hoverT, r, cornerMask);
                }
                else
                {
                    DrawVisualWithMask(rect, def, ZUIButtonDrawState.Normal, r, cornerMask);
                }
            }
            else
            {
                DrawVisualWithMask(rect, def, s, r, cornerMask);
            }

            ZUI.DrawFlashOverlayIfNeeded(rect, def.name, r, ZUI.FlashDefType.Button);
            DrawButtonLabel(rect, content, def.GetLabelStyle(s, iconOnly), icon, placement, def, def.GetText(s));
        }

        return clicked;
    }

    // Draws button visual with optional per-call corner mask override.
    static void DrawVisualWithMask(Rect rect, ZUIButtonDef def, ZUIButtonDrawState state, int cornerRadius, ZUICornerMask mask)
    {
        if (cornerRadius == 0)
        {
            def.DrawVisual(rect, state, cornerRadius);
            return;
        }

        // Apply corner mask (None uses def's default per-corner flags)
        var (tl, tr, bl, br) = ResolveCornerMask(def, mask);
        def.DrawVisualWithCorners(rect, state, cornerRadius, tl, tr, bl, br);
    }

    // Draws the button label, handling icon placement when an icon is present.
    internal static void DrawButtonLabel(Rect rect, GUIContent content, GUIStyle labelStyle,
                                         Texture2D icon, ZIconPlacement placement, ZUIButtonDef def,
                                         ZUITextDef textDef = null)
    {
        // Resolve icon: explicit override → content.image (GUIContent-embedded texture)
        var drawIcon = icon ?? content.image as Texture2D;
        if (drawIcon == null)
        {
            DrawLabel(rect, content, labelStyle, textDef);
            return;
        }

        // Strip the image from content so Unity doesn't double-render it alongside our manual draw.
        var drawContent = drawIcon == content.image as Texture2D
            ? new GUIContent(content.text, content.tooltip)
            : content;

        var drawPlacement = placement;
        bool iconOnly = string.IsNullOrEmpty(content.text);
        // Icon-only: fill the button rect minus iconPadH/V inset. Icon+text: use the configured iconSize.
        float ipadH = def != null ? def.iconPadH : 3;
        float ipadV = def != null ? def.iconPadV : 3;
        float sz    = iconOnly ? Mathf.Min(rect.width - ipadH * 2f, rect.height - ipadV * 2f) : 14f;
        float pad   = iconOnly ? ipadH : 4f;

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

    // ===== GetButtonStyle — returns the GUIStyle for a style name =============
    // Useful for GUILayoutUtility.GetRect sizing when using ZUI buttons.

    public static GUIStyle GetButtonStyle(string style = Style.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def = sheet.FindButton(style);
            if (def != null) return def.GetLabelStyle();
        }
        return ButtonStyleRegistry.Get(style);
    }

    // ===== ButtonStyleRegistry ===============================================
    // Fallback when no sheet is loaded. Lazy init — NOT a static constructor.

    static class ButtonStyleRegistry
    {
        static Dictionary<string, GUIStyle> _styles;

        public static GUIStyle Get(string key)
        {
            if (_styles == null || _styles[Style.Default].normal.background == null)
                Build();
            if (_styles.TryGetValue(key, out var s)) return s;
            return _styles[Style.Default];
        }

        static void Build()
        {
            _styles = new Dictionary<string, GUIStyle>
            {
                { Style.Default,     Make(new Color(.22f, .22f, .26f, 1f), new Color(.30f, .30f, .36f, 1f), new Color(.16f, .16f, .20f, 1f), new Color(.88f, .88f, .88f, 1f)) },
                { Style.Confirm,     Make(new Color(.14f, .34f, .14f, 1f), new Color(.18f, .44f, .18f, 1f), new Color(.10f, .24f, .10f, 1f), new Color(.72f, 1f,   .72f, 1f)) },
                { Style.Danger,      Make(new Color(.40f, .12f, .10f, 1f), new Color(.54f, .16f, .13f, 1f), new Color(.28f, .08f, .07f, 1f), new Color(1f,   .72f, .70f, 1f)) },
                { Style.Subtle,      Make(new Color(.20f, .20f, .20f, .30f), new Color(.30f, .30f, .30f, .45f), new Color(.14f, .14f, .14f, .40f), new Color(.65f, .65f, .65f, 1f)) },
                { Style.Active,      Make(new Color(.20f, .38f, .55f, 1f), new Color(.25f, .46f, .65f, 1f), new Color(.14f, .28f, .42f, 1f), new Color(.75f, .92f, 1f,   1f)) },
                { Style.Alternative, Make(new Color(.55f, .38f, .10f, 1f), new Color(.68f, .48f, .14f, 1f), new Color(.40f, .28f, .08f, 1f), new Color(1f,   .88f, .55f, 1f)) },
                { Style.Cancel,      Make(new Color(.38f, .15f, .15f, 1f), new Color(.48f, .20f, .20f, 1f), new Color(.28f, .10f, .10f, 1f), new Color(.95f, .70f, .70f, 1f)) },
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
