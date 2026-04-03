// ZUISubDefs.cs
// Reusable sub-definition structs shared between ZUIBoxDef and ZUIButtonDef.

using System;
using UnityEngine;
using UnityEngine.Serialization;

// ── ZUIBorderDef ──────────────────────────────────────────────────────────────
// Encapsulates all border properties: a ZUIGradient for colour + a width.
// Migration from the old flat-field layout is handled via OnAfterDeserialize.

[Serializable]
public class ZUIBorderDef : ISerializationCallbackReceiver
{
    public ZUIGradient gradient = new ZUIGradient(new Color(1f, 1f, 1f, 0.06f));
    public float       width    = 1f;

    // ── Migration from pre-gradient layout ────────────────────────────────────
    [HideInInspector] public int _borderDefVersion = 0;

    [HideInInspector] public Color  colorA        = new Color(1f, 1f, 1f, 0.06f);
    [HideInInspector] public Color  colorB        = new Color(0f, 0f, 0f, 0.10f);
    [HideInInspector] public bool   isGradient    = false;
    [HideInInspector] public float  gradientAngle = 135f;

    [HideInInspector] public string         colorARef  = "";
    [HideInInspector] public ZUIPaletteSlot colorASlot = ZUIPaletteSlot.Primary;
    [HideInInspector] public string         colorBRef  = "";
    [HideInInspector] public ZUIPaletteSlot colorBSlot = ZUIPaletteSlot.Primary;

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (_borderDefVersion == 0)
        {
            // Migrate flat fields into the gradient sub-object.
            gradient.colorA     = ZUIColorRef.FromLegacy(colorA, colorARef, colorASlot);
            gradient.colorB     = ZUIColorRef.FromLegacy(colorB, colorBRef, colorBSlot);
            gradient.isGradient = isGradient;
            gradient.angle      = gradientAngle;
            gradient._gradientDefVersion = 2; // skip gradient's own migration
            _borderDefVersion   = 1;
        }
    }

    // ── Constructors ──────────────────────────────────────────────────────────

    public ZUIBorderDef() { _borderDefVersion = 1; }

    public ZUIBorderDef(Color solidColor, float width = 1f)
    {
        gradient = new ZUIGradient(solidColor);
        this.width = width;
        _borderDefVersion = 1;
    }

    // ── Resolved values (kept for draw-path compatibility) ────────────────────

    public Color GetResolvedA() => gradient.GetColorA();
    public Color GetResolvedB() => gradient.GetColorB();
}

// ── ZUIDropShadowDef ──────────────────────────────────────────────────────────
// Background drop-shadow for boxes and buttons.

[Serializable]
public class ZUIDropShadowDef : ISerializationCallbackReceiver
{
    public bool         enabled = false;
    public Vector2      offset  = new Vector2(3f, 3f);
    public ZUIColorRef  tint    = new ZUIColorRef(new Color(0f, 0f, 0f, 0.35f));

    // ── Legacy fields (pre-ZUIColorRef) ──────────────────────────────────────
    [HideInInspector] public int _shadowDefVersion = 0;
    [HideInInspector][FormerlySerializedAs("color")]     public Color          _legacyColor     = new Color(0f, 0f, 0f, 0.35f);
    [HideInInspector][FormerlySerializedAs("colorRef")]  public string         _legacyColorRef  = "";
    [HideInInspector][FormerlySerializedAs("colorSlot")] public ZUIPaletteSlot _legacyColorSlot = ZUIPaletteSlot.Primary;

    public Color GetResolvedColor() => tint.Resolve();

    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        if (_shadowDefVersion < 2)
        {
            if (_legacyColor != default || !string.IsNullOrEmpty(_legacyColorRef))
                tint = ZUIColorRef.FromLegacy(_legacyColor, _legacyColorRef, _legacyColorSlot);
            _shadowDefVersion = 2;
        }
    }
}

// ── ZUIShapeDef ───────────────────────────────────────────────────────────────
// Corner radius + per-corner rounding flags.

[Serializable]
public class ZUIShapeDef
{
    public int  cornerRadius = 0;
    public bool roundTL      = true;
    public bool roundTR      = true;
    public bool roundBL      = true;
    public bool roundBR      = true;

    public int GetResolvedRadius() => cornerRadius;

    // Returns a Vector4(TL, TR, BR, BL) for GUI.DrawTexture's borderRadius parameter.
    public Vector4 GetCornerVector(float r) => new Vector4(
        roundTL ? r : 0f,
        roundTR ? r : 0f,
        roundBR ? r : 0f,
        roundBL ? r : 0f);
}

// ── ZUIPaddingDef ────────────────────────────────────────────────────────────
// Padding and margin values. Used by both ZUIButtonDef and ZUIBoxDef.
// Fields that don't apply to a particular context (e.g. iconPad on boxes) are
// simply left at 0 — the editor hides them via the showIcon/showMargin flags.

[Serializable]
public class ZUIPaddingDef
{
    public int padH    = 8;
    public int padV    = 6;
    public int iconPadH = 0;
    public int iconPadV = 0;
    public int marginH  = 0;
    public int marginV  = 0;
}
