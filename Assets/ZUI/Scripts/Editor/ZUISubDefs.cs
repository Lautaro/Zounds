// ZUISubDefs.cs
// Reusable sub-definition structs shared between ZUIBoxDef and ZUIButtonDef.

using System;
using UnityEngine;

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
            // Do NOT allocate via 'new' here — Unity forbids heap allocation in OnAfterDeserialize.
            // gradient is already instantiated by field initializer; just overwrite its values.
            gradient.colorA     = colorA;
            gradient.colorB     = colorB;
            gradient.isGradient = isGradient;
            gradient.angle      = gradientAngle;
            gradient.colorARef  = colorARef;
            gradient.colorASlot = colorASlot;
            gradient.colorBRef  = colorBRef;
            gradient.colorBSlot = colorBSlot;
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
public class ZUIDropShadowDef
{
    public bool    enabled = false;
    public Vector2 offset  = new Vector2(3f, 3f);
    public Color   color   = new Color(0f, 0f, 0f, 0.35f);

    public string         colorRef  = "";
    public ZUIPaletteSlot colorSlot = ZUIPaletteSlot.Primary;

    public Color GetResolvedColor()
    {
        if (!string.IsNullOrEmpty(colorRef))
        {
            var p = ZUI.ActiveSheet?.FindPaletteColor(colorRef);
            if (p != null) return p.Resolve(colorSlot);
        }
        return color;
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
