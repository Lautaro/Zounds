// ZUIStyleDef.cs

using System;
using UnityEngine;

// ── Text Style ────────────────────────────────────────────────────────────────

[Serializable]
public class ZUITextDef
{
    public Color     color     = new Color(.88f, .88f, .88f, 1f);
    public int       fontSize  = 0;    // 0 = inherit from skin
    public FontStyle fontStyle = FontStyle.Normal;

    public bool    shadowEnabled = false;
    public Vector2 shadowOffset  = new Vector2(1f, 1f);
    public Color   shadowColor   = new Color(0f, 0f, 0f, 0.6f);

    public string         colorRef       = "";
    public ZUIPaletteSlot colorSlot      = ZUIPaletteSlot.Primary;
    public string         shadowColorRef = "";
    public ZUIPaletteSlot shadowColorSlot = ZUIPaletteSlot.Primary;

    public Color GetResolvedColor()
    {
        if (!string.IsNullOrEmpty(colorRef)) { var p = ZUI.ActiveSheet?.FindPaletteColor(colorRef); if (p != null) return p.Resolve(colorSlot); }
        return color;
    }
    public Color GetResolvedShadowColor()
    {
        if (!string.IsNullOrEmpty(shadowColorRef)) { var p = ZUI.ActiveSheet?.FindPaletteColor(shadowColorRef); if (p != null) return p.Resolve(shadowColorSlot); }
        return shadowColor;
    }

    public ZUITextDef() { }
    public ZUITextDef(Color color) { this.color = color; }

    public void Apply(GUIStyle s)
    {
        s.normal.textColor = s.hover.textColor = s.active.textColor = GetResolvedColor();
        s.fontStyle = fontStyle;
        if (fontSize > 0) s.fontSize = fontSize;
    }
}

// ── Box Style ─────────────────────────────────────────────────────────────────
// Background drawn via ZUIGradient.DrawRect — supports solid or gradient fill.
// GUIStyle carries only padding/margin so the layout engine can size the group.

[Serializable]
public class ZUIBoxDef
{
    public string      name             = "New Box Style";
    public ZUIGradient background       = new ZUIGradient(new Color(.20f, .20f, .24f, 1f));
    public ZUITextDef  titleText        = new ZUITextDef(new Color(.90f, .90f, .90f, 1f));
    public ZUITextDef  contentText      = new ZUITextDef(new Color(.80f, .80f, .80f, 1f));
    public UnityEngine.Texture2D titleIcon     = null;
    public int                   titleIconSize = 14;
    public Color       borderColor      = new Color(1f, 1f, 1f, 0.06f);
    public Color       borderColorEnd   = new Color(0f, 0f, 0f, 0.10f);  // bottom+right when isBorderGradient
    public bool        isBorderGradient = false;
    public float       borderWidth      = 1f;
    public int         cornerRadius     = 0;
    // Per-corner rounding — all true by default (all corners round when cornerRadius > 0).
    // Set any to false to make that corner flat while the others remain rounded.
    public bool        roundTL = true;   // top-left
    public bool        roundTR = true;   // top-right
    public bool        roundBL = true;   // bottom-left
    public bool        roundBR = true;   // bottom-right
    public int         padH             = 8;
    public int         padV             = 6;
    public int         marginH          = 4;
    public int         marginV          = 4;

    // Optional: use a named ZUITextStyleDef from the sheet instead of the inline def.
    public string      titleTextStyleId   = "";
    public string      contentTextStyleId = "";

    public bool    bgShadowEnabled = false;
    public Vector2 bgShadowOffset  = new Vector2(3f, 3f);
    public Color   bgShadowColor   = new Color(0f, 0f, 0f, 0.35f);

    public string         borderColorRef      = "";
    public ZUIPaletteSlot borderColorSlot     = ZUIPaletteSlot.Primary;
    public string         borderColorEndRef   = "";
    public ZUIPaletteSlot borderColorEndSlot  = ZUIPaletteSlot.Primary;
    public string         bgShadowColorRef    = "";
    public ZUIPaletteSlot bgShadowColorSlot   = ZUIPaletteSlot.Primary;

    // Global override flags
    public bool useGlobalBorder      = false;
    public bool useGlobalPadding     = false;
    public bool useGlobalShape       = false;
    public bool useGlobalBackground  = false;
    public bool useGlobalTitleText   = false;
    public bool useGlobalContentText = false;

    // Backward compat — routes through titleText
    public Color labelColor { get => titleText.color; set => titleText.color = value; }

    // ── Constructors ──────────────────────────────────────────────────────────

    public ZUIBoxDef() { }

    public ZUIBoxDef(string name, Color bgColor, Color labelColor)
    {
        this.name       = name;
        background      = new ZUIGradient(bgColor);
        this.labelColor = labelColor;
    }

    public ZUIBoxDef(string name, Color bgColor, Color labelColor,
                     Color borderColor, float borderWidth, int padH, int padV)
    {
        this.name        = name;
        background       = new ZUIGradient(bgColor);
        this.labelColor  = labelColor;
        this.borderColor = borderColor;
        borderColorEnd   = new Color(0f, 0f, 0f, 0.10f);
        this.borderWidth = borderWidth;
        this.padH        = padH;
        this.padV        = padV;
    }

    public ZUIBoxDef(string name, ZUIGradient background, Color labelColor,
                     Color borderColor, float borderWidth, int padH, int padV)
    {
        this.name        = name;
        this.background  = background;
        this.labelColor  = labelColor;
        this.borderColor = borderColor;
        borderColorEnd   = new Color(0f, 0f, 0f, 0.10f);
        this.borderWidth = borderWidth;
        this.padH        = padH;
        this.padV        = padV;
    }

    // ── Resolved values ───────────────────────────────────────────────────────

    public int GetResolvedCornerRadius()
    {
#if UNITY_EDITOR
        if (useGlobalShape)
        {
            var g = ZUI.ActiveSheet?.globalBox;
            if (g != null) return g.cornerRadius;
        }
#endif
        return cornerRadius;
    }

    // Returns a Vector4(TL, TR, BL, BR) with each component set to r or 0
    // based on the per-corner flags. Global shape overrides use all-round.
    public Vector4 GetCornerVector(float r)
    {
#if UNITY_EDITOR
        if (useGlobalShape)
            return new Vector4(r, r, r, r);
#endif
        return new Vector4(
            roundTL ? r : 0f,
            roundTR ? r : 0f,
            roundBL ? r : 0f,
            roundBR ? r : 0f);
    }


    // ── Global-aware resolved getters ─────────────────────────────────────────

    public ZUIGradient GetResolvedBackground()
    {
#if UNITY_EDITOR
        if (useGlobalBackground) { var g = ZUI.ActiveSheet?.globalBox; if (g != null) return g.background; }
#endif
        return background;
    }

    public ZUITextDef GetResolvedTitleText()
    {
#if UNITY_EDITOR
        if (useGlobalTitleText) { var g = ZUI.ActiveSheet?.globalBox; if (g != null) return g.titleText; }
        if (!string.IsNullOrEmpty(titleTextStyleId)) { var s = ZUI.ActiveSheet?.FindText(titleTextStyleId); if (s != null) return s.text; }
#endif
        return titleText;
    }

    public ZUITextDef GetResolvedContentText()
    {
#if UNITY_EDITOR
        if (useGlobalContentText) { var g = ZUI.ActiveSheet?.globalBox; if (g != null) return g.contentText; }
        if (!string.IsNullOrEmpty(contentTextStyleId)) { var s = ZUI.ActiveSheet?.FindText(contentTextStyleId); if (s != null) return s.text; }
#endif
        return contentText;
    }

    // ── Layout GUIStyle ───────────────────────────────────────────────────────

    [NonSerialized] private GUIStyle _layoutStyle;

    public GUIStyle GetLayoutStyle()
    {
        if (_layoutStyle != null) return _layoutStyle;

        int pH = padH, pV = padV;
#if UNITY_EDITOR
        if (useGlobalPadding)
        {
            var g = ZUI.ActiveSheet?.globalBox;
            if (g != null) { pH = g.padH; pV = g.padV; }
        }
#endif
        _layoutStyle = new GUIStyle(GUIStyle.none)
        {
            padding = new RectOffset(pH, pH, pV, pV),
            margin  = new RectOffset(marginH, marginH, marginV, marginV),
        };
        return _layoutStyle;
    }

    [NonSerialized] private Texture2D _borderGradTex;
    [NonSerialized] private int       _borderGradHash;

    Texture2D GetOrBuildBorderGradTex(Color bc1, Color bc2)
    {
        unchecked
        {
            int h = bc1.GetHashCode() * 397 ^ bc2.GetHashCode();
            if (_borderGradTex != null && _borderGradHash == h) return _borderGradTex;
            _borderGradHash = h;
        }
        const int size = 32;
        _borderGradTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        float ax = Mathf.Cos(135f * Mathf.Deg2Rad);
        float ay = Mathf.Sin(135f * Mathf.Deg2Rad);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f) / size - 0.5f;
            float ny = (y + 0.5f) / size - 0.5f;
            float t  = Mathf.Clamp01(nx * ax + ny * ay + 0.5f);
            _borderGradTex.SetPixel(x, y, Color.Lerp(bc1, bc2, t));
        }
        _borderGradTex.Apply();
        return _borderGradTex;
    }

    public void Invalidate() { _layoutStyle = null; _contentStyle = null; _borderGradTex = null; background.Invalidate(); }

    // ── Content text GUIStyle ─────────────────────────────────────────────────

    [NonSerialized] private GUIStyle _contentStyle;

    public GUIStyle GetContentStyle()
    {
        if (_contentStyle != null) return _contentStyle;
        _contentStyle = new GUIStyle(UnityEditor.EditorStyles.label);
        GetResolvedContentText().Apply(_contentStyle);
        _contentStyle.wordWrap = true;
        return _contentStyle;
    }

    // ── Background draw ───────────────────────────────────────────────────────

    public void DrawBackground(Rect rect)
    {
#if UNITY_EDITOR
        // ── Style debug ───────────────────────────────────────────────────────
        if (ZUI.CheckDebugBoxClick(rect))
        {
            var debugStyle = ZUI._pendingBoxStyleSet ? ZUI._pendingBoxStyle : ZUI.ZUIStyle.Default;
            ZUI._pendingBoxStyleSet = false;
            ZUI.CollectBoxDebugInfo(this, debugStyle, rect);
        }
        else ZUI._pendingBoxStyleSet = false;

        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
        if (rect.width <= 1f) return;

        Color resolvedBgShadow = !string.IsNullOrEmpty(bgShadowColorRef)
            ? (ZUI.ActiveSheet?.FindPaletteColor(bgShadowColorRef) is var _sp && _sp != null ? _sp.Resolve(bgShadowColorSlot) : bgShadowColor)
            : bgShadowColor;
        int cr = GetResolvedCornerRadius();

        if (bgShadowEnabled && resolvedBgShadow.a > 0f)
        {
            var sr = new Rect(rect.x + bgShadowOffset.x, rect.y + bgShadowOffset.y, rect.width, rect.height);
#if UNITY_2021_2_OR_NEWER
            if (cr > 0)
            {
                float r = Mathf.Min(cr, sr.width * 0.5f, sr.height * 0.5f);
                GUI.DrawTexture(sr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, resolvedBgShadow, Vector4.zero, new Vector4(r, r, r, r));
            }
            else
#endif
            UnityEditor.EditorGUI.DrawRect(sr, resolvedBgShadow);
        }

        Color bc1raw = !string.IsNullOrEmpty(borderColorRef)
            ? (ZUI.ActiveSheet?.FindPaletteColor(borderColorRef) is var _bp1 && _bp1 != null ? _bp1.Resolve(borderColorSlot) : borderColor)
            : borderColor;
        Color bc2raw = !string.IsNullOrEmpty(borderColorEndRef)
            ? (ZUI.ActiveSheet?.FindPaletteColor(borderColorEndRef) is var _bp2 && _bp2 != null ? _bp2.Resolve(borderColorEndSlot) : borderColorEnd)
            : borderColorEnd;
        Color  bc1 = bc1raw, bc2 = bc2raw;
        float  bw  = borderWidth;
        bool   bg2 = isBorderGradient;
        if (useGlobalBorder)
        {
            var g = ZUI.ActiveSheet?.globalBox;
            if (g != null) { bc1 = g.borderColor; bc2 = g.borderColorEnd; bw = g.borderWidth; bg2 = g.isBorderGradient; }
        }

#if UNITY_2021_2_OR_NEWER
        if (cr > 0 && bw > 0f && bc1.a > 0f)
        {
            float r     = Mathf.Min(cr, rect.width * 0.5f, rect.height * 0.5f);
            var   crVec = GetCornerVector(r);
            if (bg2 && (bc2.a > 0f || bc1 != bc2))
            {
                var borderTex = GetOrBuildBorderGradTex(bc1, bc2);
                GUI.DrawTexture(rect, borderTex, ScaleMode.StretchToFill, true, 0f, Color.white, Vector4.zero, crVec);
            }
            else
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, bc1, Vector4.zero, crVec);
            }
            var   inner = new Rect(rect.x + bw, rect.y + bw, rect.width - bw * 2f, rect.height - bw * 2f);
            float ir    = Mathf.Max(0f, r - bw);
            GetResolvedBackground().DrawRect(inner, GetCornerVector(ir));
            return;
        }
#endif

        GetResolvedBackground().DrawRect(rect, GetCornerVector(cr));

        if (bw > 0f)
        {
            Color top    = bc1;
            Color bottom = bg2 ? bc2 : bc1;
            Color left   = bc1;
            Color right  = bg2 ? bc2 : bc1;

            float b = bw;
            if (top.a    > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, b),           top);
            if (bottom.a > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - b, rect.width, b),           bottom);
            if (left.a   > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        b,          rect.height), left);
            if (right.a  > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.xMax - b, rect.y,        b,          rect.height), right);
        }

        ZUI.DrawFlashOverlayIfNeeded(rect, name, cr, ZUI.FlashDefType.Box);
#endif
    }
}

// ── Button draw-state enum ────────────────────────────────────────────────────

public enum ZUIButtonDrawState { Normal, Hover, Active }

// ── Icon placement ────────────────────────────────────────────────────────────

public enum ZIconPlacement
{
    LeftOfLabel,    // icon sits immediately left of the text, both centred together
    RightOfLabel,   // icon sits immediately right of the text, both centred together
    LeftEdge,       // icon pinned to the left edge; text centred in remaining space
    RightEdge,      // icon pinned to the right edge; text centred in remaining space
}

// ── Button Style ──────────────────────────────────────────────────────────────

[Serializable]
public class ZUIButtonDef
{
    public string      name     = "New Button Style";

    // ── Icon ──────────────────────────────────────────────────────────────────
    public UnityEngine.Texture2D icon          = null;
    public ZIconPlacement        iconPlacement = ZIconPlacement.LeftOfLabel;
    public int                   iconSize      = 14;

    // ── Normal state ──────────────────────────────────────────────────────────
    public ZUIGradient normal   = new ZUIGradient(new Color(.22f, .22f, .26f, 1f));
    public ZUITextDef  text     = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public Color       borderColor      = new Color(1f, 1f, 1f, 0f);
    public Color       borderColorEnd   = new Color(0f, 0f, 0f, 0.10f);
    public bool        isBorderGradient = false;
    public float       borderWidth      = 0f;
    public int         cornerRadius     = 0;
    // Per-corner rounding — all true by default (all corners round when cornerRadius > 0).
    public bool        roundTL = true;
    public bool        roundTR = true;
    public bool        roundBL = true;
    public bool        roundBR = true;
    public int         padH     = 10;   // padding for text buttons and icon+text buttons
    public int         padV     = 3;
    public int         iconPadH = 3;   // padding for icon-only buttons (no label)
    public int         iconPadV = 3;

    // Optional: use a named ZUITextStyleDef from the sheet instead of the inline def.
    public string      textStyleId       = "";
    public string      hoverTextStyleId  = "";
    public string      activeTextStyleId = "";

    public bool    bgShadowEnabled = false;
    public Vector2 bgShadowOffset  = new Vector2(2f, 2f);
    public Color   bgShadowColor   = new Color(0f, 0f, 0f, 0.4f);

    public string         borderColorRef           = "";
    public ZUIPaletteSlot borderColorSlot          = ZUIPaletteSlot.Primary;
    public string         borderColorEndRef        = "";
    public ZUIPaletteSlot borderColorEndSlot       = ZUIPaletteSlot.Primary;
    public string         hoverBorderColorRef      = "";
    public ZUIPaletteSlot hoverBorderColorSlot     = ZUIPaletteSlot.Primary;
    public string         hoverBorderColorEndRef   = "";
    public ZUIPaletteSlot hoverBorderColorEndSlot  = ZUIPaletteSlot.Primary;
    public string         activeBorderColorRef     = "";
    public ZUIPaletteSlot activeBorderColorSlot    = ZUIPaletteSlot.Primary;
    public string         activeBorderColorEndRef  = "";
    public ZUIPaletteSlot activeBorderColorEndSlot = ZUIPaletteSlot.Primary;
    public string         bgShadowColorRef         = "";
    public ZUIPaletteSlot bgShadowColorSlot        = ZUIPaletteSlot.Primary;

    // ── Hover state ───────────────────────────────────────────────────────────
    // Default overrides = true so existing assets that already have hover/active values keep them.
    public bool        hoverBgOverride      = true;
    public ZUIGradient hover                = new ZUIGradient(new Color(.30f, .30f, .36f, 1f));
    public bool        hoverTextOverride    = false;
    public ZUITextDef  hoverText            = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public bool        hoverBorderOverride  = false;
    public Color       hoverBorderColor     = new Color(1f, 1f, 1f, 0f);
    public Color       hoverBorderColorEnd  = new Color(0f, 0f, 0f, 0.10f);
    public bool        hoverIsBorderGrad    = false;
    public float       hoverBorderWidth     = 0f;

    // ── Active state ──────────────────────────────────────────────────────────
    public bool        activeBgOverride     = true;
    public ZUIGradient active               = new ZUIGradient(new Color(.16f, .16f, .20f, 1f));
    public bool        activeTextOverride   = false;
    public ZUITextDef  activeText           = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public bool        activeBorderOverride = false;
    public Color       activeBorderColor    = new Color(1f, 1f, 1f, 0f);
    public Color       activeBorderColorEnd = new Color(0f, 0f, 0f, 0.10f);
    public bool        activeIsBorderGrad   = false;
    public float       activeBorderWidth    = 0f;

    // ── Global override flags ─────────────────────────────────────────────────
    public bool useGlobalShape      = false;
    public bool useGlobalPadding    = false;
    public bool useGlobalBorder     = false;
    public bool useGlobalBackground = false;
    public bool useGlobalText       = false;

    // Backward compat — routes through text
    public Color textColor { get => text.color; set => text.color = value; }

    // ── Constructors ──────────────────────────────────────────────────────────

    public ZUIButtonDef() { }

    public ZUIButtonDef(string name, Color normalBg, Color hoverBg, Color activeBg, Color textColor)
    {
        this.name      = name;
        normal         = new ZUIGradient(normalBg);
        hover          = new ZUIGradient(hoverBg);
        active         = new ZUIGradient(activeBg);
        this.textColor = textColor;
    }

    public ZUIButtonDef(string name, ZUIGradient normal, ZUIGradient hover, ZUIGradient active,
                        Color textColor, int cornerRadius = 0)
    {
        this.name         = name;
        this.normal       = normal;
        this.hover        = hover;
        this.active       = active;
        this.textColor    = textColor;
        this.cornerRadius = cornerRadius;
    }

    // ── Resolved values ───────────────────────────────────────────────────────

    public int GetResolvedCornerRadius()
    {
#if UNITY_EDITOR
        if (useGlobalShape)
        {
            var g = ZUI.ActiveSheet?.globalButton;
            if (g != null) return g.cornerRadius;
        }
#endif
        return cornerRadius;
    }

    public Vector4 GetCornerVector(float r)
    {
#if UNITY_EDITOR
        if (useGlobalShape)
            return new Vector4(r, r, r, r);
#endif
        return new Vector4(
            roundTL ? r : 0f,
            roundTR ? r : 0f,
            roundBL ? r : 0f,
            roundBR ? r : 0f);
    }

    // ── State-resolved getters ────────────────────────────────────────────────
    // Each resolves its value from the inheritance chain: Normal → Hover → Active.

    public ZUIGradient GetNormalGradient()
    {
#if UNITY_EDITOR
        if (useGlobalBackground) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) return g.normal; }
#endif
        return normal;
    }

    public ZUITextDef GetNormalText()
    {
#if UNITY_EDITOR
        if (useGlobalText) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) return g.text; }
        if (!string.IsNullOrEmpty(textStyleId)) { var s = ZUI.ActiveSheet?.FindText(textStyleId); if (s != null) return s.text; }
#endif
        return text;
    }

    public ZUIGradient GetHoverGradient()  => hoverBgOverride  ? hover  : GetNormalGradient();
    public ZUIGradient GetActiveGradient() => activeBgOverride ? active : GetHoverGradient();
    public ZUIGradient GetGradient(ZUIButtonDrawState s) =>
        s == ZUIButtonDrawState.Active ? GetActiveGradient() :
        s == ZUIButtonDrawState.Hover  ? GetHoverGradient() : GetNormalGradient();

    public ZUITextDef GetHoverText()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(hoverTextStyleId)) { var s = ZUI.ActiveSheet?.FindText(hoverTextStyleId); if (s != null) return s.text; }
#endif
        return hoverTextOverride ? hoverText : GetNormalText();
    }

    public ZUITextDef GetActiveText()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(activeTextStyleId)) { var s = ZUI.ActiveSheet?.FindText(activeTextStyleId); if (s != null) return s.text; }
#endif
        return activeTextOverride ? activeText : GetHoverText();
    }
    public ZUITextDef GetText(ZUIButtonDrawState s) =>
        s == ZUIButtonDrawState.Active ? GetActiveText() :
        s == ZUIButtonDrawState.Hover  ? GetHoverText() : GetNormalText();

    // Returns (c1, c2, dual, width) for each state — colors resolved through palette if refs are set.
    public (Color c1, Color c2, bool dual, float w) GetNormalBorder()
    {
        Color c1 = ResolveRef(borderColorRef,     borderColorSlot,     borderColor);
        Color c2 = ResolveRef(borderColorEndRef,  borderColorEndSlot,  borderColorEnd);
        return (c1, c2, isBorderGradient, borderWidth);
    }
    public (Color c1, Color c2, bool dual, float w) GetHoverBorder()
    {
        if (!hoverBorderOverride) return GetNormalBorder();
        Color c1 = ResolveRef(hoverBorderColorRef,    hoverBorderColorSlot,    hoverBorderColor);
        Color c2 = ResolveRef(hoverBorderColorEndRef, hoverBorderColorEndSlot, hoverBorderColorEnd);
        return (c1, c2, hoverIsBorderGrad, hoverBorderWidth);
    }
    public (Color c1, Color c2, bool dual, float w) GetActiveBorder()
    {
        if (!activeBorderOverride) return GetHoverBorder();
        Color c1 = ResolveRef(activeBorderColorRef,    activeBorderColorSlot,    activeBorderColor);
        Color c2 = ResolveRef(activeBorderColorEndRef, activeBorderColorEndSlot, activeBorderColorEnd);
        return (c1, c2, activeIsBorderGrad, activeBorderWidth);
    }

    static Color ResolveRef(string refName, ZUIPaletteSlot slot, Color fallback)
    {
        if (string.IsNullOrEmpty(refName)) return fallback;
        var p = ZUI.ActiveSheet?.FindPaletteColor(refName);
        return p != null ? p.Resolve(slot) : fallback;
    }
    public (Color c1, Color c2, bool dual, float w) GetBorder(ZUIButtonDrawState s) =>
        s == ZUIButtonDrawState.Active ? GetActiveBorder() :
        s == ZUIButtonDrawState.Hover  ? GetHoverBorder() : GetNormalBorder();

    // ── Border gradient texture (rounded-corner split-border) ─────────────────

    [NonSerialized] private Texture2D _borderGradTex;
    [NonSerialized] private int       _borderGradHash;

    Texture2D GetOrBuildBorderGradTex(Color bc1, Color bc2)
    {
        unchecked
        {
            int h = bc1.GetHashCode() * 397 ^ bc2.GetHashCode();
            if (_borderGradTex != null && _borderGradHash == h) return _borderGradTex;
            _borderGradHash = h;
        }
        const int  size = 32;
        _borderGradTex = new Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false)
        {
            filterMode = UnityEngine.FilterMode.Bilinear,
            wrapMode   = UnityEngine.TextureWrapMode.Clamp,
        };
        // 135° = top-left → bottom-right (bc1 top-left, bc2 bottom-right)
        float ax = Mathf.Cos(135f * Mathf.Deg2Rad);
        float ay = Mathf.Sin(135f * Mathf.Deg2Rad);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f) / size - 0.5f;
            float ny = (y + 0.5f) / size - 0.5f;
            float t  = Mathf.Clamp01(nx * ax + ny * ay + 0.5f);
            _borderGradTex.SetPixel(x, y, Color.Lerp(bc1, bc2, t));
        }
        _borderGradTex.Apply();
        return _borderGradTex;
    }

    // ── Visual draw (fill + border, order-correct) ────────────────────────────
    // Used by DrawManualButton. Handles both rounded and flat cases.
    // Rounded with border: outer rounded rect = border colour, inner = fill.
    // Flat or no border: fill first, then 4-edge flat border rects on top.

    public void DrawVisual(Rect rect, ZUIButtonDrawState state, int cornerRadius)
    {
#if UNITY_EDITOR
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;

        Color resolvedBtnShadow = ResolveRef(bgShadowColorRef, bgShadowColorSlot, bgShadowColor);
        if (bgShadowEnabled && resolvedBtnShadow.a > 0f)
        {
            var sr = new Rect(rect.x + bgShadowOffset.x, rect.y + bgShadowOffset.y, rect.width, rect.height);
            int cr = GetResolvedCornerRadius();
#if UNITY_2021_2_OR_NEWER
            if (cr > 0)
            {
                float r = Mathf.Min(cr, sr.width * 0.5f, sr.height * 0.5f);
                GUI.DrawTexture(sr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, resolvedBtnShadow, Vector4.zero, new Vector4(r, r, r, r));
            }
            else
#endif
            UnityEditor.EditorGUI.DrawRect(sr, resolvedBtnShadow);
        }

        ZUIGradient fill = GetGradient(state);

        var (bc1raw, bc2raw, bg2raw, bwraw) = GetBorder(state);
        Color  bc1 = bc1raw, bc2 = bc2raw;
        float  bw  = bwraw;
        bool   bg2 = bg2raw;
        if (useGlobalBorder)
        {
            var g = ZUI.ActiveSheet?.globalButton;
            if (g != null) { bc1 = g.borderColor; bc2 = g.borderColorEnd; bw = g.borderWidth; bg2 = g.isBorderGradient; }
        }

#if UNITY_2021_2_OR_NEWER
        if (cornerRadius > 0 && bw > 0f && bc1.a > 0f)
        {
            float r     = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
            var   crVec = GetCornerVector(r);
            if (bg2 && (bc2.a > 0f || bc1 != bc2))
            {
                var borderTex = GetOrBuildBorderGradTex(bc1, bc2);
                GUI.DrawTexture(rect, borderTex, ScaleMode.StretchToFill, true, 0f, Color.white, Vector4.zero, crVec);
            }
            else
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, bc1, Vector4.zero, crVec);
            }
            var   inner = new Rect(rect.x + bw, rect.y + bw, rect.width - bw * 2f, rect.height - bw * 2f);
            float ir    = Mathf.Max(0f, r - bw);
            fill.DrawRect(inner, GetCornerVector(ir));
            return;
        }
#endif

        fill.DrawRect(rect, GetCornerVector(cornerRadius));

        if (bw > 0f)
        {
            Color top    = bc1;
            Color bottom = bg2 ? bc2 : bc1;
            Color left   = bc1;
            Color right  = bg2 ? bc2 : bc1;
            float b = bw;
            if (top.a    > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, b),           top);
            if (bottom.a > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - b, rect.width, b),           bottom);
            if (left.a   > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        b,          rect.height), left);
            if (right.a  > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.xMax - b, rect.y,        b,          rect.height), right);
        }
#endif
    }

    // ── Border draw (standalone, flat only) ───────────────────────────────────

    public void DrawBorder(Rect rect)
    {
#if UNITY_EDITOR
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;

        var (bc1raw, bc2raw, bg2raw, bwraw) = GetNormalBorder();
        Color  bc1 = bc1raw, bc2 = bc2raw;
        float  bw  = bwraw;
        bool   bg2 = bg2raw;
        if (useGlobalBorder)
        {
            var g = ZUI.ActiveSheet?.globalButton;
            if (g != null) { bc1 = g.borderColor; bc2 = g.borderColorEnd; bw = g.borderWidth; bg2 = g.isBorderGradient; }
        }

        if (bw <= 0f) return;

        Color top    = bc1;
        Color bottom = bg2 ? bc2 : bc1;
        Color left   = bc1;
        Color right  = bg2 ? bc2 : bc1;

        float b = bw;
        if (top.a    > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, b),           top);
        if (bottom.a > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - b, rect.width, b),           bottom);
        if (left.a   > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        b,          rect.height), left);
        if (right.a  > 0f) UnityEditor.EditorGUI.DrawRect(new Rect(rect.xMax - b, rect.y,        b,          rect.height), right);
#endif
    }

    // ── GUIStyle for legacy ZButtonStyle path ─────────────────────────────────

    [NonSerialized] private GUIStyle _style;

    public GUIStyle GetStyle()
    {
        if (_style == null || _style.normal.background == null)
            _style = BuildStyle();
        return _style;
    }

    // ── Label style for manual-draw button path ───────────────────────────────

    [NonSerialized] private GUIStyle _labelStyle;
    [NonSerialized] private GUIStyle _hoverLabelStyle;
    [NonSerialized] private GUIStyle _activeLabelStyle;
    [NonSerialized] private GUIStyle _iconLabelStyle;
    [NonSerialized] private GUIStyle _iconHoverLabelStyle;
    [NonSerialized] private GUIStyle _iconActiveLabelStyle;

    // iconOnly = true  → uses iconPadH/iconPadV (tight padding for icon-only buttons)
    // iconOnly = false → uses padH/padV (wider padding for text buttons and icon+text buttons)
    public GUIStyle GetLabelStyle(ZUIButtonDrawState state = ZUIButtonDrawState.Normal, bool iconOnly = false)
    {
        if (iconOnly)
        {
            // Check icon-mode cache for this state
            GUIStyle existing = state == ZUIButtonDrawState.Hover  ? _iconHoverLabelStyle
                              : state == ZUIButtonDrawState.Active ? _iconActiveLabelStyle
                              :                                      _iconLabelStyle;
            if (existing != null) return existing;

            int pH = iconPadH, pV = iconPadV;
#if UNITY_EDITOR
            if (useGlobalPadding) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) { pH = g.iconPadH; pV = g.iconPadV; } }
#endif
            var built = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(pH, pH, pV, pV) };
#if UNITY_EDITOR
            built.fontSize = UnityEditor.EditorStyles.miniButton.fontSize;
            built.font     = UnityEditor.EditorStyles.miniButton.font;
#endif
            GetText(state).Apply(built);
            if (state == ZUIButtonDrawState.Hover)  _iconHoverLabelStyle  = built;
            else if (state == ZUIButtonDrawState.Active) _iconActiveLabelStyle = built;
            else                                         _iconLabelStyle       = built;
            return built;
        }

        // Text / icon+text mode — original cache
        GUIStyle cached = state == ZUIButtonDrawState.Hover  ? _hoverLabelStyle
                        : state == ZUIButtonDrawState.Active ? _activeLabelStyle
                        :                                      _labelStyle;
        if (cached != null) return cached;

        int tpH = padH, tpV = padV;
#if UNITY_EDITOR
        if (useGlobalPadding) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) { tpH = g.padH; tpV = g.padV; } }
#endif
        var s = new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleCenter,
            padding   = new RectOffset(tpH, tpH, tpV, tpV),
        };
#if UNITY_EDITOR
        s.fontSize = UnityEditor.EditorStyles.miniButton.fontSize;
        s.font     = UnityEditor.EditorStyles.miniButton.font;
#endif
        GetText(state).Apply(s);
        if (state == ZUIButtonDrawState.Hover)       _hoverLabelStyle  = s;
        else if (state == ZUIButtonDrawState.Active) _activeLabelStyle = s;
        else                                         _labelStyle       = s;
        return s;
    }

    public void Invalidate()
    {
        _style                = null;
        _labelStyle           = null;
        _hoverLabelStyle      = null;
        _activeLabelStyle     = null;
        _iconLabelStyle       = null;
        _iconHoverLabelStyle  = null;
        _iconActiveLabelStyle = null;
        _borderGradTex        = null;
        normal.Invalidate();
        hover.Invalidate();
        active.Invalidate();
    }

    // ── GUIStyle builder ──────────────────────────────────────────────────────

    private GUIStyle BuildStyle()
    {
        var s = new GUIStyle(GUIStyle.none);

        SetState(s.normal,  normal.GetOrBuildTexture(), text.GetResolvedColor());
        SetState(s.hover,   hover.GetOrBuildTexture(),  text.GetResolvedColor());
        SetState(s.active,  active.GetOrBuildTexture(), text.GetResolvedColor());
        SetState(s.focused, hover.GetOrBuildTexture(),  text.GetResolvedColor());

        s.border    = new RectOffset(0, 0, 0, 0);
        s.padding   = new RectOffset(padH, padH, padV, padV);
        s.alignment = TextAnchor.MiddleCenter;
        text.Apply(s);
#if UNITY_EDITOR
        s.fontSize = UnityEditor.EditorStyles.miniButton.fontSize;
        s.font     = UnityEditor.EditorStyles.miniButton.font;
#endif
        if (text.fontSize > 0) s.fontSize = text.fontSize;
        return s;
    }

    static void SetState(GUIStyleState state, Texture2D tex, Color textColor)
    {
        state.background        = tex;
        state.scaledBackgrounds = new Texture2D[] { tex };
        state.textColor         = textColor;
    }
}

// ── Named Text Style ──────────────────────────────────────────────────────────

[Serializable]
public class ZUITextStyleDef
{
    public string     name = "New Text Style";
    public ZUITextDef text = new ZUITextDef();

    [NonSerialized] private GUIStyle _style;

    public GUIStyle GetStyle()
    {
        if (_style == null)
        {
#if UNITY_EDITOR
            _style = new GUIStyle(UnityEditor.EditorStyles.label);
#else
            _style = new GUIStyle(GUIStyle.none);
#endif
            _style.wordWrap = true;
        }
        // Always re-apply color so palette reference changes are picked up immediately.
        text.Apply(_style);
        return _style;
    }

    public void Invalidate() { _style = null; }
}
