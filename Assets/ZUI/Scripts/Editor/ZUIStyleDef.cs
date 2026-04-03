// ZUIStyleDef.cs

using System;
using UnityEngine;
using UnityEngine.Serialization;

// ── Text Style ────────────────────────────────────────────────────────────────

[Serializable]
public class ZUITextDef : ISerializationCallbackReceiver
{
    // Text color — when gradientEnabled, color+colorB form a horizontal per-character gradient.
    public ZUIColorRef color        = new ZUIColorRef(new Color(.88f, .88f, .88f, 1f));
    public bool        gradientEnabled = false;
    public ZUIColorRef colorB       = new ZUIColorRef(new Color(.50f, .70f, 1f, 1f));
    public int         fontSize     = 0;    // 0 = inherit from skin
    public FontStyle   fontStyle    = FontStyle.Normal;

    public bool        shadowEnabled = false;
    public Vector2     shadowOffset  = new Vector2(1f, 1f);
    public ZUIColorRef shadowColor   = new ZUIColorRef(new Color(0f, 0f, 0f, 0.6f));

    public bool        outlineEnabled = false;
    public ZUIColorRef outlineColor   = new ZUIColorRef(new Color(0f, 0f, 0f, 0.8f));
    public int         outlineWidth   = 1;
    public int         outlinePasses  = 4;  // 4 or 8

    // ── Legacy fields (pre-ZUIColorRef) ──────────────────────────────────────
    [HideInInspector] public int _textDefVersion = 0;
    [HideInInspector][FormerlySerializedAs("color")]          public Color          _legacyColor          = new Color(.88f, .88f, .88f, 1f);
    [HideInInspector][FormerlySerializedAs("colorB")]         public Color          _legacyColorB         = new Color(.50f, .70f, 1f, 1f);
    [HideInInspector][FormerlySerializedAs("shadowColor")]    public Color          _legacyShadowColor    = new Color(0f, 0f, 0f, 0.6f);
    [HideInInspector][FormerlySerializedAs("outlineColor")]   public Color          _legacyOutlineColor   = new Color(0f, 0f, 0f, 0.8f);
    [HideInInspector][FormerlySerializedAs("colorRef")]       public string         _legacyColorRef       = "";
    [HideInInspector][FormerlySerializedAs("colorSlot")]      public ZUIPaletteSlot _legacyColorSlot      = ZUIPaletteSlot.Primary;
    [HideInInspector][FormerlySerializedAs("colorBRef")]      public string         _legacyColorBRef      = "";
    [HideInInspector][FormerlySerializedAs("colorBSlot")]     public ZUIPaletteSlot _legacyColorBSlot     = ZUIPaletteSlot.Primary;
    [HideInInspector][FormerlySerializedAs("shadowColorRef")] public string         _legacyShadowColorRef = "";
    [HideInInspector][FormerlySerializedAs("shadowColorSlot")]public ZUIPaletteSlot _legacyShadowColorSlot = ZUIPaletteSlot.Primary;
    [HideInInspector][FormerlySerializedAs("outlineColorRef")]public string         _legacyOutlineColorRef = "";
    [HideInInspector][FormerlySerializedAs("outlineColorSlot")]public ZUIPaletteSlot _legacyOutlineColorSlot = ZUIPaletteSlot.Primary;

    public Color GetResolvedColor()      => color.Resolve();
    public Color GetResolvedColorB()     => colorB.Resolve();
    public Color GetResolvedShadowColor()  => shadowColor.Resolve();
    public Color GetResolvedOutlineColor() => outlineColor.Resolve();

    public ZUITextDef() { }
    public ZUITextDef(Color c) { color = new ZUIColorRef(c); _textDefVersion = 2; }

    public void Apply(GUIStyle s)
    {
        s.normal.textColor = s.hover.textColor = s.active.textColor = GetResolvedColor();
        s.fontStyle = fontStyle;
        if (fontSize > 0) s.fontSize = fontSize;
    }

    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        if (_textDefVersion < 2)
        {
            bool legacyHasData = _legacyColor != default ||
                                 !string.IsNullOrEmpty(_legacyColorRef) ||
                                 _legacyShadowColor != default ||
                                 !string.IsNullOrEmpty(_legacyShadowColorRef);
            if (legacyHasData)
            {
                color        = ZUIColorRef.FromLegacy(_legacyColor,        _legacyColorRef,        _legacyColorSlot);
                colorB       = ZUIColorRef.FromLegacy(_legacyColorB,       _legacyColorBRef,       _legacyColorBSlot);
                shadowColor  = ZUIColorRef.FromLegacy(_legacyShadowColor,  _legacyShadowColorRef,  _legacyShadowColorSlot);
                outlineColor = ZUIColorRef.FromLegacy(_legacyOutlineColor, _legacyOutlineColorRef, _legacyOutlineColorSlot);
            }
            _textDefVersion = 2;
        }
    }
}

// ── Box Style ─────────────────────────────────────────────────────────────────
// Background drawn via ZUIGradient.DrawRect — supports solid or gradient fill.
// GUIStyle carries only padding/margin so the layout engine can size the group.

[Serializable]
public class ZUIBoxDef : ISerializationCallbackReceiver
{
    public string      name             = "New Box Style";
    public ZUIGradient background       = new ZUIGradient(new Color(.20f, .20f, .24f, 1f));
    public ZUITextDef  titleText        = new ZUITextDef(new Color(.90f, .90f, .90f, 1f));
    public ZUITextDef  contentText      = new ZUITextDef(new Color(.80f, .80f, .80f, 1f));
    public UnityEngine.Texture2D titleIcon     = null;
    public int                   titleIconSize = 14;

    public ZUIBorderDef    border    = new ZUIBorderDef();
    public ZUIDropShadowDef bgShadow = new ZUIDropShadowDef();
    public ZUIShapeDef     shape     = new ZUIShapeDef();

    public int         padH    = 8;
    public int         padV    = 6;
    public int         marginH = 4;
    public int         marginV = 4;

    // Optional: use a named ZUITextStyleDef from the sheet instead of the inline def.
    public string      titleTextStyleId   = "";
    public string      contentTextStyleId = "";

    // Global override flags
    public bool useGlobalBorder      = false;
    public bool useGlobalPadding     = false;
    public bool useGlobalShape       = false;
    public bool useGlobalBackground  = false;
    public bool useGlobalTitleText   = false;
    public bool useGlobalContentText = false;

    // Backward compat — routes through titleText
    public Color labelColor { get => titleText.color.color; set => titleText.color = new ZUIColorRef(value); }

    // ── Serialization migration ───────────────────────────────────────────────
    // Version 0 = old flat-field layout; version 1 = struct layout.
    // Fields prefixed _legacy_ are kept only for migration and hidden in the inspector.

    [HideInInspector] public int _defVersion = 0;

    [HideInInspector] public Color  _legacyBorderColor      = new Color(1f, 1f, 1f, 0.06f);
    [HideInInspector] public Color  _legacyBorderColorEnd   = new Color(0f, 0f, 0f, 0.10f);
    [HideInInspector] public bool   _legacyIsBorderGradient = false;
    [HideInInspector] public float  _legacyBorderGradientAngle = 135f;
    [HideInInspector] public float  _legacyBorderWidth      = 1f;
    [HideInInspector] public string _legacyBorderColorRef      = "";
    [HideInInspector] public int    _legacyBorderColorSlot     = 0;
    [HideInInspector] public string _legacyBorderColorEndRef   = "";
    [HideInInspector] public int    _legacyBorderColorEndSlot  = 0;

    [HideInInspector] public bool    _legacyBgShadowEnabled  = false;
    [HideInInspector] public Vector2 _legacyBgShadowOffset   = new Vector2(3f, 3f);
    [HideInInspector] public Color   _legacyBgShadowColor    = new Color(0f, 0f, 0f, 0.35f);
    [HideInInspector] public string  _legacyBgShadowColorRef = "";
    [HideInInspector] public int     _legacyBgShadowColorSlot = 0;

    [HideInInspector] public int  _legacyCornerRadius = 0;
    [HideInInspector] public bool _legacyRoundTL = true;
    [HideInInspector] public bool _legacyRoundTR = true;
    [HideInInspector] public bool _legacyRoundBL = true;
    [HideInInspector] public bool _legacyRoundBR = true;

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (_defVersion == 0)
        {
            // Migrate flat border fields into ZUIBorderDef's own legacy fields
            // so its own OnAfterDeserialize can pick them up.
            border.colorA        = _legacyBorderColor;
            border.colorB        = _legacyBorderColorEnd;
            border.isGradient    = _legacyIsBorderGradient;
            border.gradientAngle = _legacyBorderGradientAngle;
            border.width         = _legacyBorderWidth;
            border.colorARef     = _legacyBorderColorRef;
            border.colorASlot    = (ZUIPaletteSlot)_legacyBorderColorSlot;
            border.colorBRef     = _legacyBorderColorEndRef;
            border.colorBSlot    = (ZUIPaletteSlot)_legacyBorderColorEndSlot;
            border._borderDefVersion = 0; // force ZUIBorderDef migration
            border.OnAfterDeserialize();

            bgShadow.enabled = _legacyBgShadowEnabled;
            bgShadow.offset  = _legacyBgShadowOffset;
            bgShadow.tint    = ZUIColorRef.FromLegacy(_legacyBgShadowColor, _legacyBgShadowColorRef, (ZUIPaletteSlot)_legacyBgShadowColorSlot);
            bgShadow._shadowDefVersion = 2;

            shape.cornerRadius = _legacyCornerRadius;
            shape.roundTL      = _legacyRoundTL;
            shape.roundTR      = _legacyRoundTR;
            shape.roundBL      = _legacyRoundBL;
            shape.roundBR      = _legacyRoundBR;

            _defVersion = 1;
        }
    }

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
        border           = new ZUIBorderDef(borderColor, borderWidth);
        this.padH        = padH;
        this.padV        = padV;
        _defVersion      = 1;
    }

    public ZUIBoxDef(string name, ZUIGradient background, Color labelColor,
                     Color borderColor, float borderWidth, int padH, int padV)
    {
        this.name        = name;
        this.background  = background;
        this.labelColor  = labelColor;
        border           = new ZUIBorderDef(borderColor, borderWidth);
        this.padH        = padH;
        this.padV        = padV;
        _defVersion      = 1;
    }

    // ── Resolved values ───────────────────────────────────────────────────────

    public int GetResolvedCornerRadius()
    {
#if UNITY_EDITOR
        if (useGlobalShape)
        {
            var g = ZUI.ActiveSheet?.globalBox;
            if (g != null) return g.shape.cornerRadius;
        }
#endif
        return shape.cornerRadius;
    }

    public Vector4 GetCornerVector(float r)
    {
#if UNITY_EDITOR
        if (useGlobalShape)
            return new Vector4(r, r, r, r);
#endif
        return shape.GetCornerVector(r);
    }

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

    ZUIBorderDef GetResolvedBorder()
    {
        if (useGlobalBorder)
        {
            var g = ZUI.ActiveSheet?.globalBox;
            if (g != null) return g.border;
        }
        return border;
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

    public void Invalidate() { _layoutStyle = null; _contentStyle = null; background.Invalidate(); border.gradient.Invalidate(); }

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
        if (ZUI.CheckDebugBoxClick(rect))
        {
            var debugStyle = ZUI._pendingBoxStyleSet ? ZUI._pendingBoxStyle : ZUI.ZUIStyle.Default;
            ZUI._pendingBoxStyleSet = false;
            ZUI.CollectBoxDebugInfo(this, debugStyle, rect);
        }
        else ZUI._pendingBoxStyleSet = false;

        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
        if (rect.width <= 1f) return;

        var resolvedBorder = GetResolvedBorder();
        Color bc1 = resolvedBorder.gradient.GetColorA();
        Color bc2 = resolvedBorder.gradient.GetColorB();
        float bw  = resolvedBorder.width;
        bool  bg2 = resolvedBorder.gradient.isGradient;

        var resolvedShadow = bgShadow;
        Color shadowColor  = resolvedShadow.GetResolvedColor();
        int   cr           = GetResolvedCornerRadius();

        if (resolvedShadow.enabled && shadowColor.a > 0f)
        {
            var sr = new Rect(rect.x + resolvedShadow.offset.x, rect.y + resolvedShadow.offset.y, rect.width, rect.height);
#if UNITY_2021_2_OR_NEWER
            if (cr > 0)
            {
                float r = Mathf.Min(cr, sr.width * 0.5f, sr.height * 0.5f);
                GUI.DrawTexture(sr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, shadowColor, Vector4.zero, new Vector4(r, r, r, r));
            }
            else
#endif
            UnityEditor.EditorGUI.DrawRect(sr, shadowColor);
        }

#if UNITY_2021_2_OR_NEWER
        if (cr > 0 && bw > 0f && bc1.a > 0f)
        {
            float r     = Mathf.Min(cr, rect.width * 0.5f, rect.height * 0.5f);
            var   crVec = GetCornerVector(r);
            resolvedBorder.gradient.DrawRect(rect, crVec);
            var   inner = new Rect(rect.x + bw, rect.y + bw, rect.width - bw * 2f, rect.height - bw * 2f);
            float ir    = Mathf.Max(0f, r - bw);
            GetResolvedBackground().DrawRect(inner, GetCornerVector(ir));
            return;
        }
#endif

        GetResolvedBackground().DrawRect(rect, GetCornerVector(cr));

        if (bw > 0f && bc1.a > 0f)
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
    LeftOfLabel,
    RightOfLabel,
    LeftEdge,
    RightEdge,
}

// ── Button Style ──────────────────────────────────────────────────────────────

[Serializable]
public class ZUIButtonDef : ISerializationCallbackReceiver
{
    public string      name     = "New Button Style";

    // ── Normal state ──────────────────────────────────────────────────────────
    public ZUIGradient   normal = new ZUIGradient(new Color(.22f, .22f, .26f, 1f));
    public ZUITextDef    text   = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public ZUIBorderDef  border = new ZUIBorderDef(new Color(1f, 1f, 1f, 0f), 0f);

    public int  cornerRadius = 0;
    public bool roundTL = true, roundTR = true, roundBL = true, roundBR = true;
    public int  padH     = 10;
    public int  padV     = 3;
    public int  iconPadH = 3;
    public int  iconPadV = 3;

    // Optional: use a named ZUITextStyleDef from the sheet instead of the inline def.
    public string textStyleId       = "";
    public string hoverTextStyleId  = "";
    public string activeTextStyleId = "";

    public ZUIDropShadowDef bgShadow = new ZUIDropShadowDef { offset = new Vector2(2f, 2f), tint = new ZUIColorRef(new Color(0f, 0f, 0f, 0.4f)) };

    // ── Hover state ───────────────────────────────────────────────────────────
    public bool        hoverBgOverride     = true;
    public ZUIGradient hover               = new ZUIGradient(new Color(.30f, .30f, .36f, 1f));
    public bool        hoverTextOverride   = false;
    public ZUITextDef  hoverText           = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public bool        hoverBorderOverride = false;
    public ZUIBorderDef hoverBorder        = new ZUIBorderDef(new Color(1f, 1f, 1f, 0f), 0f);

    // ── Active state ──────────────────────────────────────────────────────────
    public bool        activeBgOverride     = true;
    public ZUIGradient active               = new ZUIGradient(new Color(.16f, .16f, .20f, 1f));
    public bool        activeTextOverride   = false;
    public ZUITextDef  activeText           = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public bool        activeBorderOverride = false;
    public ZUIBorderDef activeBorder        = new ZUIBorderDef(new Color(1f, 1f, 1f, 0f), 0f);

    // ── Global override flags ─────────────────────────────────────────────────
    public bool useGlobalShape      = false;
    public bool useGlobalPadding    = false;
    public bool useGlobalBorder     = false;
    public bool useGlobalBackground = false;
    public bool useGlobalText       = false;

    // ── Hover / click animations ──────────────────────────────────────────────
    public bool  hoverAnimEnabled   = false;
    public float hoverInDuration    = 0.12f;
    public float hoverOutDuration   = 0.20f;
    public bool  clickAnimEnabled   = false;
    public float clickInDuration    = 0.06f;
    public float clickOutDuration   = 0.20f;

    public bool previewAsToggle = false;

    // Backward compat
    public Color textColor { get => text.color.color; set => text.color = new ZUIColorRef(value); }

    // ── Serialization migration ───────────────────────────────────────────────

    [HideInInspector] public int _defVersion = 0;

    // Normal border legacy
    [HideInInspector] public Color  _legacyBorderColor          = new Color(1f, 1f, 1f, 0f);
    [HideInInspector] public Color  _legacyBorderColorEnd       = new Color(0f, 0f, 0f, 0.10f);
    [HideInInspector] public bool   _legacyIsBorderGradient     = false;
    [HideInInspector] public float  _legacyBorderGradientAngle  = 135f;
    [HideInInspector] public float  _legacyBorderWidth          = 0f;
    [HideInInspector] public string _legacyBorderColorRef       = "";
    [HideInInspector] public int    _legacyBorderColorSlot      = 0;
    [HideInInspector] public string _legacyBorderColorEndRef    = "";
    [HideInInspector] public int    _legacyBorderColorEndSlot   = 0;
    // Hover border legacy
    [HideInInspector] public Color  _legacyHoverBorderColor        = new Color(1f, 1f, 1f, 0f);
    [HideInInspector] public Color  _legacyHoverBorderColorEnd     = new Color(0f, 0f, 0f, 0.10f);
    [HideInInspector] public bool   _legacyHoverIsBorderGrad       = false;
    [HideInInspector] public float  _legacyHoverBorderWidth        = 0f;
    [HideInInspector] public string _legacyHoverBorderColorRef     = "";
    [HideInInspector] public int    _legacyHoverBorderColorSlot    = 0;
    [HideInInspector] public string _legacyHoverBorderColorEndRef  = "";
    [HideInInspector] public int    _legacyHoverBorderColorEndSlot = 0;
    // Active border legacy
    [HideInInspector] public Color  _legacyActiveBorderColor        = new Color(1f, 1f, 1f, 0f);
    [HideInInspector] public Color  _legacyActiveBorderColorEnd     = new Color(0f, 0f, 0f, 0.10f);
    [HideInInspector] public bool   _legacyActiveIsBorderGrad       = false;
    [HideInInspector] public float  _legacyActiveBorderWidth        = 0f;
    [HideInInspector] public string _legacyActiveBorderColorRef     = "";
    [HideInInspector] public int    _legacyActiveBorderColorSlot    = 0;
    [HideInInspector] public string _legacyActiveBorderColorEndRef  = "";
    [HideInInspector] public int    _legacyActiveBorderColorEndSlot = 0;
    // Shadow legacy
    [HideInInspector] public bool    _legacyBgShadowEnabled   = false;
    [HideInInspector] public Vector2 _legacyBgShadowOffset    = new Vector2(2f, 2f);
    [HideInInspector] public Color   _legacyBgShadowColor     = new Color(0f, 0f, 0f, 0.4f);
    [HideInInspector] public string  _legacyBgShadowColorRef  = "";
    [HideInInspector] public int     _legacyBgShadowColorSlot = 0;

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (_defVersion == 0)
        {
            // Populate ZUIBorderDef legacy fields so its own migration runs correctly.
            void MigrateBorder(ZUIBorderDef b, Color ca, Color cb, bool grad, float gradAngle, float w,
                               string caRef, int caSlot, string cbRef, int cbSlot)
            {
                b.colorA = ca; b.colorB = cb; b.isGradient = grad; b.gradientAngle = gradAngle;
                b.width = w; b.colorARef = caRef; b.colorASlot = (ZUIPaletteSlot)caSlot;
                b.colorBRef = cbRef; b.colorBSlot = (ZUIPaletteSlot)cbSlot;
                b._borderDefVersion = 0;
                b.OnAfterDeserialize();
            }

            MigrateBorder(border,
                _legacyBorderColor, _legacyBorderColorEnd, _legacyIsBorderGradient, _legacyBorderGradientAngle,
                _legacyBorderWidth, _legacyBorderColorRef, _legacyBorderColorSlot,
                _legacyBorderColorEndRef, _legacyBorderColorEndSlot);

            MigrateBorder(hoverBorder,
                _legacyHoverBorderColor, _legacyHoverBorderColorEnd, _legacyHoverIsBorderGrad, 135f,
                _legacyHoverBorderWidth, _legacyHoverBorderColorRef, _legacyHoverBorderColorSlot,
                _legacyHoverBorderColorEndRef, _legacyHoverBorderColorEndSlot);

            MigrateBorder(activeBorder,
                _legacyActiveBorderColor, _legacyActiveBorderColorEnd, _legacyActiveIsBorderGrad, 135f,
                _legacyActiveBorderWidth, _legacyActiveBorderColorRef, _legacyActiveBorderColorSlot,
                _legacyActiveBorderColorEndRef, _legacyActiveBorderColorEndSlot);

            bgShadow.enabled = _legacyBgShadowEnabled;
            bgShadow.offset  = _legacyBgShadowOffset;
            bgShadow.tint    = ZUIColorRef.FromLegacy(_legacyBgShadowColor, _legacyBgShadowColorRef, (ZUIPaletteSlot)_legacyBgShadowColorSlot);
            bgShadow._shadowDefVersion = 2;

            _defVersion = 1;
        }
    }

    // ── Constructors ──────────────────────────────────────────────────────────

    public ZUIButtonDef() { }

    public ZUIButtonDef(string name, Color normalBg, Color hoverBg, Color activeBg, Color textColor)
    {
        this.name      = name;
        normal         = new ZUIGradient(normalBg);
        hover          = new ZUIGradient(hoverBg);
        active         = new ZUIGradient(activeBg);
        this.textColor = textColor;
        _defVersion    = 1;
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
        _defVersion       = 1;
    }

    // ── Resolved values ───────────────────────────────────────────────────────

    public int GetResolvedCornerRadius()
    {
#if UNITY_EDITOR
        if (useGlobalShape) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) return g.cornerRadius; }
#endif
        return cornerRadius;
    }

    /// <summary>Returns the resolved per-corner rounding flags, respecting useGlobalShape.</summary>
    public (bool tl, bool tr, bool bl, bool br) GetResolvedCornerFlags()
    {
#if UNITY_EDITOR
        if (useGlobalShape)
        {
            var g = ZUI.ActiveSheet?.globalButton;
            if (g != null) return (g.roundTL, g.roundTR, g.roundBL, g.roundBR);
        }
#endif
        return (roundTL, roundTR, roundBL, roundBR);
    }

    public Vector4 GetCornerVector(float r)
    {
        var (tl, tr, bl, br) = GetResolvedCornerFlags();
        return new Vector4(tl ? r : 0f, tr ? r : 0f, br ? r : 0f, bl ? r : 0f);
    }

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

    public ZUIBorderDef GetNormalBorder()
    {
        if (useGlobalBorder) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) return g.border; }
        return border;
    }

    public ZUIBorderDef GetHoverBorder()  => hoverBorderOverride  ? hoverBorder  : GetNormalBorder();
    public ZUIBorderDef GetActiveBorder() => activeBorderOverride ? activeBorder : GetHoverBorder();

    public ZUIBorderDef GetBorder(ZUIButtonDrawState s) =>
        s == ZUIButtonDrawState.Active ? GetActiveBorder() :
        s == ZUIButtonDrawState.Hover  ? GetHoverBorder() : GetNormalBorder();

    // ── Visual draw ───────────────────────────────────────────────────────────

    void DrawVisualInternal(Rect rect, ZUIButtonDrawState state, int cornerRadius,
                            Vector4 crVec)
    {
#if UNITY_EDITOR
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;

        Color shadowColor = bgShadow.GetResolvedColor();
        if (bgShadow.enabled && shadowColor.a > 0f)
        {
            var sr = new Rect(rect.x + bgShadow.offset.x, rect.y + bgShadow.offset.y, rect.width, rect.height);
            int cr2 = GetResolvedCornerRadius();
#if UNITY_2021_2_OR_NEWER
            if (cr2 > 0)
            {
                float r2 = Mathf.Min(cr2, sr.width * 0.5f, sr.height * 0.5f);
                GUI.DrawTexture(sr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, shadowColor, Vector4.zero, new Vector4(r2, r2, r2, r2));
            }
            else
#endif
            UnityEditor.EditorGUI.DrawRect(sr, shadowColor);
        }

        ZUIGradient fill   = GetGradient(state);
        ZUIBorderDef bDef  = GetBorder(state);
        Color  bc1 = bDef.gradient.GetColorA();
        float  bw  = bDef.width;

#if UNITY_2021_2_OR_NEWER
        if (cornerRadius > 0 && bw > 0f && bc1.a > 0f)
        {
            float r     = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
            var   cVec  = crVec == default ? new Vector4(r, r, r, r) : crVec;
            bDef.gradient.DrawRect(rect, cVec);
            var   inner = new Rect(rect.x + bw, rect.y + bw, rect.width - bw * 2f, rect.height - bw * 2f);
            float ir    = Mathf.Max(0f, r - bw);
            var   iVec  = crVec == default
                ? new Vector4(ir, ir, ir, ir)
                : new Vector4(crVec.x > 0 ? ir : 0, crVec.y > 0 ? ir : 0, crVec.z > 0 ? ir : 0, crVec.w > 0 ? ir : 0);
            fill.DrawRect(inner, iVec);
            return;
        }
#endif

        fill.DrawRect(rect, crVec);

        if (bw > 0f && bc1.a > 0f)
        {
            // Fallback flat border using gradient's colorA on all sides (no rounded support here)
            float b = bw;
            UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, b),           bc1);
            UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - b, rect.width, b),           bc1);
            UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        b,          rect.height), bc1);
            UnityEditor.EditorGUI.DrawRect(new Rect(rect.xMax - b, rect.y,        b,          rect.height), bc1);
        }
#endif
    }

    public void DrawVisual(Rect rect, ZUIButtonDrawState state, int cornerRadius)
        => DrawVisualInternal(rect, state, cornerRadius, GetCornerVector(Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f)));

    public void DrawVisualWithCorners(Rect rect, ZUIButtonDrawState state, int cornerRadius,
                                      bool roundTL, bool roundTR, bool roundBL, bool roundBR)
    {
        float r    = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
        var   cVec = new Vector4(roundTL ? r : 0f, roundTR ? r : 0f, roundBR ? r : 0f, roundBL ? r : 0f);
        DrawVisualInternal(rect, state, cornerRadius, cVec);
    }

    /// <summary>
    /// Draws the button visual with the fill gradient lerped between two states.
    /// Used by the tween system when hoverAnimEnabled is true so the gradient
    /// (including angle) smoothly transitions rather than snapping.
    /// t=0 shows <paramref name="from"/>, t=1 shows <paramref name="to"/>.
    /// </summary>
    public void DrawVisualLerped(Rect rect, ZUIButtonDrawState from, ZUIButtonDrawState to,
                                  float t, int cornerRadius, ZUICornerMask cornerMask = ZUICornerMask.None)
    {
#if UNITY_EDITOR
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;

        var fromGrad = GetGradient(from);
        var toGrad   = GetGradient(to);
        var fill     = ZUIGradient.Lerp(fromGrad, toGrad, t);

        // Border: lerp width and colorA between the two states
        var fromBorder = GetBorder(from);
        var toBorder   = GetBorder(to);
        float bw = Mathf.Lerp(fromBorder.width, toBorder.width, t);
        Color bc = Color.Lerp(fromBorder.gradient.GetColorA(), toBorder.gradient.GetColorA(), t);

        float r = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);

        bool tl, tr, bl, br;
        if (cornerMask == ZUICornerMask.None) { (tl, tr, bl, br) = GetResolvedCornerFlags(); }
        else { (tl, tr, bl, br) = ZUI.ResolveCornerMask(this, cornerMask); }

        var cVec = new Vector4(tl ? r : 0f, tr ? r : 0f, br ? r : 0f, bl ? r : 0f);

#if UNITY_2021_2_OR_NEWER
        if (cornerRadius > 0 && bw > 0f && bc.a > 0f)
        {
            new ZUIGradient(bc).DrawRect(rect, cVec);
            var inner = new Rect(rect.x + bw, rect.y + bw, rect.width - bw * 2f, rect.height - bw * 2f);
            float ir  = Mathf.Max(0f, r - bw);
            fill.DrawRect(inner, new Vector4(tl ? ir : 0f, tr ? ir : 0f, br ? ir : 0f, bl ? ir : 0f));
            return;
        }
#endif
        fill.DrawRect(rect, cVec);
#endif
    }

    // ── Border draw (standalone, flat only) ───────────────────────────────────

    public void DrawBorder(Rect rect)
    {
#if UNITY_EDITOR
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
        var bDef = GetNormalBorder();
        float bw = bDef.width;
        if (bw <= 0f) return;
        Color bc1 = bDef.gradient.GetColorA();
        if (bc1.a <= 0f) return;
        float b = bw;
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, b),           bc1);
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - b, rect.width, b),           bc1);
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        b,          rect.height), bc1);
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.xMax - b, rect.y,        b,          rect.height), bc1);
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

    // ── Label style ───────────────────────────────────────────────────────────

    [NonSerialized] private GUIStyle _labelStyle;
    [NonSerialized] private GUIStyle _hoverLabelStyle;
    [NonSerialized] private GUIStyle _activeLabelStyle;
    [NonSerialized] private GUIStyle _iconLabelStyle;
    [NonSerialized] private GUIStyle _iconHoverLabelStyle;
    [NonSerialized] private GUIStyle _iconActiveLabelStyle;

    public GUIStyle GetLabelStyle(ZUIButtonDrawState state = ZUIButtonDrawState.Normal, bool iconOnly = false)
    {
        if (iconOnly)
        {
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
            if (state == ZUIButtonDrawState.Hover)       _iconHoverLabelStyle  = built;
            else if (state == ZUIButtonDrawState.Active) _iconActiveLabelStyle = built;
            else                                         _iconLabelStyle       = built;
            return built;
        }

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
        normal.Invalidate();
        hover.Invalidate();
        active.Invalidate();
        border.gradient.Invalidate();
        hoverBorder.gradient.Invalidate();
        activeBorder.gradient.Invalidate();
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
        text.Apply(_style);
        return _style;
    }

    public void Invalidate() { _style = null; }
}

// ── Slider Label Position ─────────────────────────────────────────────────────

public enum ZUILabelPosition  { Inline, Above, Below }
public enum ZUILabelAlignment { Left, Center, Right }

// ── Slider Style ──────────────────────────────────────────────────────────────

[Serializable]
public class ZUISliderDef
{
    public string name = "New Slider Style";

    public ZUIBoxDef    track         = new ZUIBoxDef("Track",
                                            new Color(.14f, .14f, .18f, 1f),
                                            new Color(.88f, .88f, .88f, 1f),
                                            new Color(1f, 1f, 1f, .08f), 1f, 0, 0);
    public ZUIBoxDef    trackFill     = new ZUIBoxDef("TrackFill",
                                            new Color(.20f, .38f, .55f, 1f),
                                            new Color(.88f, .88f, .88f, 1f),
                                            new Color(.30f, .60f, 1f, .30f), 1f, 0, 0);
    public float        trackHeight   = 6f;

    public ZUIButtonDef thumb         = new ZUIButtonDef("Thumb",
                                            new Color(.30f, .54f, .78f, 1f),
                                            new Color(.40f, .64f, .90f, 1f),
                                            new Color(.20f, .40f, .62f, 1f),
                                            new Color(.92f, .96f, 1f,   1f));
    public ZUIButtonDef thumbMax      = null;
    public float        thumbWidth    = 12f;
    public float        thumbHeight   = 20f;

    public ZUITextDef        labelText      = new ZUITextDef(new Color(.78f, .78f, .82f, 1f));
    public float             labelWidth     = 0f;
    public ZUILabelPosition  labelPosition  = ZUILabelPosition.Inline;
    public ZUILabelAlignment labelAlignment = ZUILabelAlignment.Left;

    public ZUITextDef   valueText      = new ZUITextDef(new Color(.88f, .88f, .88f, 1f));
    public float        valueWidth     = 40f;
    public bool         showValueField = true;

    [NonSerialized] private GUIStyle _labelStyle;
    [NonSerialized] private GUIStyle _valueStyle;

    public GUIStyle GetLabelStyle()
    {
#if UNITY_EDITOR
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(UnityEditor.EditorStyles.label);
            _labelStyle.clipping = TextClipping.Clip;
        }
        labelText.Apply(_labelStyle);
#endif
        return _labelStyle ?? GUIStyle.none;
    }

    public GUIStyle GetValueStyle()
    {
#if UNITY_EDITOR
        if (_valueStyle == null)
        {
            _valueStyle = new GUIStyle(UnityEditor.EditorStyles.numberField);
            _valueStyle.alignment = TextAnchor.MiddleRight;
        }
        valueText.Apply(_valueStyle);
#endif
        return _valueStyle ?? GUIStyle.none;
    }

    public void Invalidate()
    {
        _labelStyle = null;
        _valueStyle = null;
        track?.Invalidate();
        trackFill?.Invalidate();
        thumb?.Invalidate();
        thumbMax?.Invalidate();
    }
}
