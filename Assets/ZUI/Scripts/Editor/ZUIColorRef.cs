// ZUIColorRef.cs
// A color value with optional palette reference. Replaces the repeated
// (Color + string colorRef + ZUIPaletteSlot) triplet pattern throughout ZUI.

using System;
using UnityEngine;

[Serializable]
public struct ZUIColorRef
{
    public Color          color;
    public string         paletteRef;      // palette entry name; "" = use inline color
    public ZUIPaletteSlot slot;            // which of the palette entry's slot colors to use
    public string         autoColorRef;    // autocolor name within the palette entry; "" = use slot

    public ZUIColorRef(Color color, string paletteRef = "", ZUIPaletteSlot slot = ZUIPaletteSlot.Primary, string autoColorRef = "")
    {
        this.color        = color;
        this.paletteRef   = paletteRef ?? "";
        this.slot         = slot;
        this.autoColorRef = autoColorRef ?? "";
    }

    /// <summary>Resolves the color: checks active skin, then sheet palette, then inline color.</summary>
    public Color Resolve()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(paletteRef))
        {
            var p = ZUI.FindPaletteColor(paletteRef);
            if (p != null)
            {
                // Autocolor takes priority over slot
                string acRef = autoColorRef;
                if (!string.IsNullOrEmpty(acRef) && p.autoColors != null)
                {
                    var ac = p.autoColors.Find(a => a.name == acRef);
                    if (ac != null) return ac.Resolve(p.color);
                }
                return p.Resolve(slot);
            }
        }
#endif
        return color;
    }

    /// <summary>True when this color is backed by a palette reference.</summary>
    public bool IsPaletteRef => !string.IsNullOrEmpty(paletteRef);

    /// <summary>True when this references a named autocolor (not a slot).</summary>
    public bool IsAutoColorRef => IsPaletteRef && !string.IsNullOrEmpty(autoColorRef);

    /// <summary>Creates a ZUIColorRef from legacy separate fields.</summary>
    public static ZUIColorRef FromLegacy(Color color, string colorRef, ZUIPaletteSlot colorSlot)
        => new ZUIColorRef(color, colorRef ?? "", colorSlot);

    public override string ToString()
        => IsAutoColorRef ? $"[{paletteRef}:{autoColorRef}]"
         : IsPaletteRef   ? $"[{paletteRef}:{slot}]"
         : color.ToString();
}
