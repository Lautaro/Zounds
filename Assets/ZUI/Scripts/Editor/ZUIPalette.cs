// ZUIPalette.cs
using System;
using UnityEngine;

// Which of a palette entry's three colors to use when resolving a reference.
public enum ZUIPaletteSlot { Primary, Highlight, Shade }

[Serializable]
public class ZUIPaletteColor
{
    public string name      = "New Color";
    public Color  color     = Color.white;                            // primary
    public Color  highlight = new Color(0.85f, 0.85f, 0.85f, 1f);   // lighter companion
    public Color  shade     = new Color(0.1f,  0.1f,  0.1f,  1f);   // darker companion

    public Color Resolve(ZUIPaletteSlot slot) => slot switch
    {
        ZUIPaletteSlot.Highlight => highlight,
        ZUIPaletteSlot.Shade     => shade,
        _                        => color,
    };
}

/// <summary>
/// A named palette override set. Skins are embedded in ZUIStyleSheetAsset and
/// override palette colors without changing the style structure.
/// </summary>
[Serializable]
public class ZUISkin
{
    public string name = "New Skin";
    public System.Collections.Generic.List<ZUIPaletteColor> palette = new System.Collections.Generic.List<ZUIPaletteColor>();
    public System.Collections.Generic.List<ZUIFontOverride> fontOverrides = new System.Collections.Generic.List<ZUIFontOverride>();
}
