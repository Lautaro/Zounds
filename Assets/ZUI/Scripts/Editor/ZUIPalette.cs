// ZUIPalette.cs
using System;
using UnityEngine;

// Which of a palette entry's colors to use when resolving a reference.
// Values 0-2 are the original manual slots. Values 3+ are auto-palette only.
public enum ZUIPaletteSlot
{
    Primary,    // 0 — base color (always available)
    Highlight,  // 1 — lighter companion (manual or auto → Light)
    Shade,      // 2 — darker companion  (manual or auto → Dark)
    Lightest,   // 3 — auto-palette only
    Light,      // 4 — auto-palette only (same as Highlight when auto)
    Dark,       // 5 — auto-palette only (same as Shade when auto)
    Darkest,    // 6 — auto-palette only
    Muted,      // 7 — auto-palette only
    Vivid,      // 8 — auto-palette only
}

[Serializable]
public class ZUIPaletteColor
{
    public string name      = "New Color";
    public Color  color     = Color.white;                            // primary
    public Color  highlight = new Color(0.85f, 0.85f, 0.85f, 1f);   // lighter companion
    public Color  shade     = new Color(0.1f,  0.1f,  0.1f,  1f);   // darker companion

    // ── Auto-palette (opt-in) ────────────────────────────────────────────────
    public bool  autoPalette;                                         // false = manual (default)
    [Range(0.05f, 0.5f)]
    public float lightnessSpread   = 0.2f;                            // how far lightest/darkest deviate
    [Range(0.05f, 0.5f)]
    public float saturationSpread  = 0.15f;                           // how far muted/vivid deviate

    // Cached auto-generated colors (non-serialized — rebuilt on demand)
    [NonSerialized] private Color[] _autoCache;
    [NonSerialized] private Color   _autoCacheBaseColor;
    [NonSerialized] private float   _autoCacheLSpread;
    [NonSerialized] private float   _autoCacheSSpread;

    void EnsureAutoCache()
    {
        if (_autoCache != null
            && _autoCacheBaseColor == color
            && Mathf.Approximately(_autoCacheLSpread, lightnessSpread)
            && Mathf.Approximately(_autoCacheSSpread, saturationSpread))
            return;

        _autoCache = ZUIColorHSL.GenerateAutoPalette(color, lightnessSpread, saturationSpread);
        _autoCacheBaseColor = color;
        _autoCacheLSpread   = lightnessSpread;
        _autoCacheSSpread   = saturationSpread;
    }

    /// <summary>Resolves a color by slot. Auto-palette entries compute derived colors on demand.</summary>
    public Color Resolve(ZUIPaletteSlot slot)
    {
        if (!autoPalette)
        {
            // Manual mode — original behavior, new slots fall back gracefully
            return slot switch
            {
                ZUIPaletteSlot.Highlight => highlight,
                ZUIPaletteSlot.Light     => highlight,
                ZUIPaletteSlot.Shade     => shade,
                ZUIPaletteSlot.Dark      => shade,
                ZUIPaletteSlot.Lightest  => highlight,
                ZUIPaletteSlot.Darkest   => shade,
                ZUIPaletteSlot.Muted     => color,
                ZUIPaletteSlot.Vivid     => color,
                _                        => color,
            };
        }

        // Auto-palette mode
        EnsureAutoCache();
        return slot switch
        {
            ZUIPaletteSlot.Lightest  => _autoCache[0],
            ZUIPaletteSlot.Light     => _autoCache[1],
            ZUIPaletteSlot.Highlight => _autoCache[1],  // Highlight maps to Light
            ZUIPaletteSlot.Dark      => _autoCache[2],
            ZUIPaletteSlot.Shade     => _autoCache[2],  // Shade maps to Dark
            ZUIPaletteSlot.Darkest   => _autoCache[3],
            ZUIPaletteSlot.Muted     => _autoCache[4],
            ZUIPaletteSlot.Vivid     => _autoCache[5],
            _                        => color,
        };
    }

    /// <summary>Forces the auto-cache to rebuild on next access.</summary>
    public void InvalidateAutoCache() => _autoCache = null;
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
