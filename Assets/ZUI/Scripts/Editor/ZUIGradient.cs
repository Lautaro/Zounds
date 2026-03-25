// ZUIGradient.cs
// Serialisable 1-D colour gradient used by ZUI style defs.
// Single-colour mode (isGradient = false) or gradient mode with bias and angle.
// DrawRect() renders with optional rounded corners (Unity 2021.2+ only; falls back silently).
// Pixel-edge mode: each active edge draws colorA → colorB over pixelLength pixels, then solid colorB.
// Adjacent active edges blend seamlessly via 2-D corner textures.

using System;
using UnityEngine;

[Flags]
public enum ZUIPixelEdges
{
    None   = 0,
    Left   = 1,
    Right  = 2,
    Bottom = 4,
    Top    = 8,
}

[Serializable]
public class ZUIGradient
{
    public bool  isGradient = false;
    public bool  isRadial   = false;   // radial gradient (colorA = centre, colorB = edge); only when isGradient
    public Color colorA     = new Color(.3f, .3f, .3f, 1f);
    public Color colorB     = new Color(.1f, .1f, .1f, 1f);   // used only when isGradient
    public float bias       = 0.5f;   // 0–1; 0.5 = linear; controls transition curve
    public float angle      = 90f;    // degrees; 0 = left→right, 90 = bottom→top (linear mode only)

    public string          colorARef  = "";                         // palette entry name; empty = use colorA directly
    public ZUIPaletteSlot  colorASlot = ZUIPaletteSlot.Primary;    // which of the entry's three colors to use
    public string          colorBRef  = "";                         // palette entry name; empty = use colorB directly
    public ZUIPaletteSlot  colorBSlot = ZUIPaletteSlot.Primary;    // special: if colorBRef empty + Shade/Highlight + colorARef set → use colorA entry's companion

    public Color GetColorA()
    {
        if (!string.IsNullOrEmpty(colorARef))
        {
            var p = ZUI.ActiveSheet?.FindPaletteColor(colorARef);
            if (p != null) return p.Resolve(colorASlot);
        }
        return colorA;
    }

    public Color GetColorB()
    {
        if (!string.IsNullOrEmpty(colorBRef))
        {
            var p = ZUI.ActiveSheet?.FindPaletteColor(colorBRef);
            if (p != null) return p.Resolve(colorBSlot);
        }
        // Special case: colorBRef empty + non-Primary slot + colorARef set
        // → use colorA's palette entry's companion color.
        // This is the "BG1 as self-contained gradient" mode.
        if (colorBSlot != ZUIPaletteSlot.Primary && !string.IsNullOrEmpty(colorARef))
        {
            var p = ZUI.ActiveSheet?.FindPaletteColor(colorARef);
            if (p != null) return p.Resolve(colorBSlot);
        }
        return colorB;
    }

    // Pixel-edge mode: each active edge draws colorA → colorB over pixelLength pixels,
    // then solid colorB fills the rest. cornerRadius is ignored in pixel mode.
    public bool          usePixelLength = false;
    public int           pixelLength    = 32;
    public ZUIPixelEdges pixelEdges     = ZUIPixelEdges.Bottom;

    // ── Constructors ─────────────────────────────────────────────────────────────

    public ZUIGradient() { }

    public ZUIGradient(Color solid) { colorA = solid; }

    public ZUIGradient(Color a, Color b, float angle = 90f, float bias = 0.5f)
    {
        isGradient  = true;
        colorA      = a;
        colorB      = b;
        this.angle  = angle;
        this.bias   = bias;
    }

    // ── Clone ─────────────────────────────────────────────────────────────────────

    public ZUIGradient Clone() => new ZUIGradient
    {
        isGradient     = isGradient,
        isRadial       = isRadial,
        colorA         = colorA,
        colorB         = colorB,
        bias           = bias,
        angle          = angle,
        usePixelLength = usePixelLength,
        pixelLength    = pixelLength,
        pixelEdges     = pixelEdges,
        colorARef  = colorARef,
        colorASlot = colorASlot,
        colorBRef  = colorBRef,
        colorBSlot = colorBSlot,
    };

    // ── Cache ─────────────────────────────────────────────────────────────────────

    [NonSerialized] private Texture2D _tex;
    [NonSerialized] private int       _texHash;

    // Pixel-edge strips (one per direction) + corner blend textures.
    // All 8 share the same hash; rebuilt together whenever the gradient params change.
    [NonSerialized] private Texture2D _texPL, _texPR, _texPB, _texPT;
    [NonSerialized] private Texture2D _texCornerBL, _texCornerBR, _texCornerTL, _texCornerTR;
    [NonSerialized] private int       _pixelStripHash;

    public void Invalidate()
    {
        _tex = null;
        _texPL = _texPR = _texPB = _texPT = null;
        _texCornerBL = _texCornerBR = _texCornerTL = _texCornerTR = null;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────────

    public void DrawRect(Rect rect, float cornerRadius = 0f)
    {
#if UNITY_EDITOR
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
        if (rect.width <= 1f) return;

        if (!isGradient)
        {
#if UNITY_2021_2_OR_NEWER
            if (cornerRadius > 0f)
            {
                float r = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                    true, 0f, GetColorA(), Vector4.zero, new Vector4(r, r, r, r));
                return;
            }
#endif
            UnityEditor.EditorGUI.DrawRect(rect, GetColorA());
            return;
        }

        // ── Pixel-edge mode ───────────────────────────────────────────────────
        if (usePixelLength && pixelLength > 0 && pixelEdges != ZUIPixelEdges.None)
        {
            DrawPixelMultiEdge(rect);
            return;
        }

        // ── Normal gradient ───────────────────────────────────────────────────
        var tex = GetOrBuildTexture();
#if UNITY_2021_2_OR_NEWER
        if (cornerRadius > 0f)
        {
            float r = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill,
                true, 0f, Color.white, Vector4.zero, new Vector4(r, r, r, r));
            return;
        }
#endif
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
#endif
    }

    // ── Pixel multi-edge ──────────────────────────────────────────────────────────
    // Each edge's strip is drawn only in the non-corner region.
    // Each active corner where two edges meet gets a 2-D blend texture so neither
    // strip cuts across the other.

    void DrawPixelMultiEdge(Rect rect)
    {
        UnityEditor.EditorGUI.DrawRect(rect, GetColorB());

        EnsurePixelStrips();

        bool hasL = (pixelEdges & ZUIPixelEdges.Left)   != 0;
        bool hasR = (pixelEdges & ZUIPixelEdges.Right)  != 0;
        bool hasB = (pixelEdges & ZUIPixelEdges.Bottom) != 0;
        bool hasT = (pixelEdges & ZUIPixelEdges.Top)    != 0;

        float gw = Mathf.Min(pixelLength, rect.width);
        float gh = Mathf.Min(pixelLength, rect.height);

        // ── Edge strips — clipped to exclude corner areas ─────────────────────

        // Left / Right strips: full height minus any active top/bottom corner strips
        float hTop = rect.y    + (hasT ? gh : 0f);
        float hBot = rect.yMax - (hasB ? gh : 0f);
        float hH   = hBot - hTop;
        if (hH > 0f)
        {
            if (hasL) GUI.DrawTexture(new Rect(rect.x,         hTop, gw, hH), _texPL, ScaleMode.StretchToFill, true);
            if (hasR) GUI.DrawTexture(new Rect(rect.xMax - gw, hTop, gw, hH), _texPR, ScaleMode.StretchToFill, true);
        }

        // Bottom / Top strips: full width minus any active left/right corner strips
        float vLeft  = rect.x    + (hasL ? gw : 0f);
        float vRight = rect.xMax - (hasR ? gw : 0f);
        float vW     = vRight - vLeft;
        if (vW > 0f)
        {
            if (hasB) GUI.DrawTexture(new Rect(vLeft, rect.yMax - gh, vW, gh), _texPB, ScaleMode.StretchToFill, true);
            if (hasT) GUI.DrawTexture(new Rect(vLeft, rect.y,         vW, gh), _texPT, ScaleMode.StretchToFill, true);
        }

        // ── Corners — 2-D blend textures ──────────────────────────────────────
        if (hasL && hasB) GUI.DrawTexture(new Rect(rect.x,         rect.yMax - gh, gw, gh), _texCornerBL, ScaleMode.StretchToFill, true);
        if (hasR && hasB) GUI.DrawTexture(new Rect(rect.xMax - gw, rect.yMax - gh, gw, gh), _texCornerBR, ScaleMode.StretchToFill, true);
        if (hasL && hasT) GUI.DrawTexture(new Rect(rect.x,         rect.y,         gw, gh), _texCornerTL, ScaleMode.StretchToFill, true);
        if (hasR && hasT) GUI.DrawTexture(new Rect(rect.xMax - gw, rect.y,         gw, gh), _texCornerTR, ScaleMode.StretchToFill, true);
    }

    // Builds 4 edge strips + 4 corner blend textures.
    // All share the same hash; rebuilt together whenever gradient params change.
    void EnsurePixelStrips()
    {
        int h = ComputePixelStripHash();
        if (_pixelStripHash == h && _texPL != null) return;
        _pixelStripHash = h;

        _texPL = BuildStrip(pixelLength, 1, true);   // Left:   colorA on left  → colorB on right
        _texPR = BuildStrip(pixelLength, 1, false);  // Right:  colorB on left  → colorA on right
        _texPB = BuildStrip(1, pixelLength, true);   // Bottom: colorA at bottom → colorB at top
        _texPT = BuildStrip(1, pixelLength, false);  // Top:    colorB at bottom → colorA at top

        // mirrorX flips horizontal axis (true = colorA is on the right of the corner).
        // mirrorY flips vertical axis  (true = colorA is at the top of the corner).
        _texCornerBL = BuildCornerTex(false, false);  // Bottom-Left
        _texCornerBR = BuildCornerTex(true,  false);  // Bottom-Right
        _texCornerTL = BuildCornerTex(false, true);   // Top-Left
        _texCornerTR = BuildCornerTex(true,  true);   // Top-Right
    }

    // 2-D corner texture: each pixel gets min(tHorizontal, tVertical) so both adjacent
    // edge gradients converge smoothly. The corner itself is solid colorA; both gradients
    // fade away from it toward colorB.
    Texture2D BuildCornerTex(bool mirrorX, bool mirrorY)
    {
        int size = Mathf.Max(pixelLength, 1);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        float maxIdx = Mathf.Max(size - 1, 1);
        Color ca = GetColorA(), cb = GetColorB();
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float tx = (float)x / maxIdx;
            float ty = (float)y / maxIdx;
            if (mirrorX) tx = 1f - tx;
            if (mirrorY) ty = 1f - ty;
            float t = Mathf.Min(ApplyBias(tx), ApplyBias(ty));
            tex.SetPixel(x, y, Color.Lerp(ca, cb, t));
        }
        tex.Apply();
        return tex;
    }

    int ComputePixelStripHash()
    {
        unchecked
        {
            int h = 99991;
            h = h * 397 ^ GetColorA().GetHashCode();
            h = h * 397 ^ GetColorB().GetHashCode();
            h = h * 397 ^ bias.GetHashCode();
            h = h * 397 ^ pixelLength;
            return h;
        }
    }

    // ── Normal gradient texture ───────────────────────────────────────────────────

    public Texture2D GetOrBuildTexture()
    {
        int h = ComputeHash();
        if (_tex != null && _texHash == h) return _tex;
        _tex     = BuildTexture();
        _texHash = h;
        return _tex;
    }

    Texture2D BuildTexture()
    {
        if (!isGradient)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, GetColorA());
            t.Apply();
            return t;
        }

        if (isRadial) return BuildRadialTexture();

        float rad = angle * Mathf.Deg2Rad;
        float ax  = Mathf.Cos(rad);
        float ay  = Mathf.Sin(rad);

        if (Mathf.Abs(ay) < 0.001f) return BuildStrip(32, 1, ax >= 0f);  // horizontal
        if (Mathf.Abs(ax) < 0.001f) return BuildStrip(1, 32, ay >= 0f);  // vertical
        return Build2D(32, 32, ax, ay);                                    // diagonal
    }

    Texture2D BuildRadialTexture()
    {
        // Use a square texture; distance is normalised so t=1 at the inscribed
        // circle edge (half the texture width), not at the diagonal corner.
        // When stretched onto a wide button the gradient becomes elliptical, but
        // it still completes (reaches colorB) at the midpoint of each edge rather
        // than only at the four corners — which matches the rounded-corner shape.
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        float half = (size - 1) * 0.5f;
        Color ca = GetColorA(), cb = GetColorB();
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Normalise each axis to [-1, 1] independently, then use the
            // Chebyshev (max) norm so the gradient reaches colorB exactly at
            // the nearest edge midpoint in both X and Y directions.
            float dx = Mathf.Abs((x - half) / half);
            float dy = Mathf.Abs((y - half) / half);
            float t  = Mathf.Clamp01(Mathf.Max(dx, dy));
            tex.SetPixel(x, y, Color.Lerp(ca, cb, ApplyBias(t)));
        }
        tex.Apply();
        return tex;
    }

    Texture2D BuildStrip(int w, int h, bool forward)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        int len = Mathf.Max(w, h);
        Color ca = GetColorA(), cb = GetColorB();
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Mathf.Max(len - 1, 1);
            if (!forward) t = 1f - t;
            Color c = Color.Lerp(ca, cb, ApplyBias(t));
            if (w > 1) tex.SetPixel(i, 0, c);
            else       tex.SetPixel(0, i, c);
        }
        tex.Apply();
        return tex;
    }

    Texture2D Build2D(int w, int h, float ax, float ay)
    {
        float len = Mathf.Sqrt(ax * ax + ay * ay);
        ax /= len; ay /= len;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        Color ca = GetColorA(), cb = GetColorB();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x + 0.5f) / w - 0.5f;
            float ny = (y + 0.5f) / h - 0.5f;
            float t  = Mathf.Clamp01(nx * ax + ny * ay + 0.5f);
            tex.SetPixel(x, y, Color.Lerp(ca, cb, ApplyBias(t)));
        }
        tex.Apply();
        return tex;
    }

    float ApplyBias(float t)
    {
        if (Mathf.Approximately(bias, 0.5f)) return t;
        float b = Mathf.Clamp(bias, 0.001f, 0.999f);
        return Mathf.Pow(t, Mathf.Log(b) / Mathf.Log(0.5f));
    }

    int ComputeHash()
    {
        unchecked
        {
            int h = isGradient ? 1231 : 1237;
            h = h * 397 ^ GetColorA().GetHashCode();
            if (!isGradient) return h;
            h = h * 397 ^ GetColorB().GetHashCode();
            h = h * 397 ^ bias.GetHashCode();
            if (isRadial) { h = h * 397 ^ 9901; return h; }
            h = h * 397 ^ angle.GetHashCode();
            if (usePixelLength) { h = h * 397 ^ pixelLength; h = h * 397 ^ (int)pixelEdges; }
            return h;
        }
    }
}
