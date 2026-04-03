// ZUIColorRef.cs
// A color value with optional palette reference. Replaces the repeated
// (Color + string colorRef + ZUIPaletteSlot) triplet pattern throughout ZUI.

using System;
using UnityEngine;

[Serializable]
public struct ZUIColorRef
{
    public Color          color;
    public string         paletteRef;   // palette entry name; "" = use inline color
    public ZUIPaletteSlot slot;         // which of the palette entry's three colors to use

    public ZUIColorRef(Color color, string paletteRef = "", ZUIPaletteSlot slot = ZUIPaletteSlot.Primary)
    {
        this.color      = color;
        this.paletteRef = paletteRef ?? "";
        this.slot       = slot;
    }

    /// <summary>Resolves the color: returns the palette color if referenced, otherwise the inline color.</summary>
    public Color Resolve()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(paletteRef))
        {
            var p = ZUI.ActiveSheet?.FindPaletteColor(paletteRef);
            if (p != null) return p.Resolve(slot);
        }
#endif
        return color;
    }

    /// <summary>True when this color is backed by a palette reference.</summary>
    public bool IsPaletteRef => !string.IsNullOrEmpty(paletteRef);

    /// <summary>Creates a ZUIColorRef from legacy separate fields.</summary>
    public static ZUIColorRef FromLegacy(Color color, string colorRef, ZUIPaletteSlot colorSlot)
        => new ZUIColorRef(color, colorRef ?? "", colorSlot);

    public override string ToString()
        => IsPaletteRef ? $"[{paletteRef}:{slot}]" : color.ToString();
}
