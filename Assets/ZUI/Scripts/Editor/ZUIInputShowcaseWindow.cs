// ZUIInputShowcaseWindow.cs
// Demonstrates custom-styled text input and slider via Unity IMGUI GUIStyle.
// Shows what is possible: colored track, big thumb, outlined text field.
// Open via: Tools / ZUI Input Showcase

using UnityEditor;
using UnityEngine;

public class ZUIInputShowcaseWindow : ZUIWindow
{
    [MenuItem("Tools/ZUI Input Showcase")]
    public static void Open() => GetWindow<ZUIInputShowcaseWindow>("Input Showcase");

    // ── State ─────────────────────────────────────────────────────────────────

    private Vector2 _scroll;
    private string  _textA = "Edit me";
    private string  _textB = "Outlined field";
    private float   _sliderA = 0.5f;
    private float   _sliderB = 0.3f;
    private float   _sliderC = 0.7f;

    // ── Cached styles (built once, reused) ────────────────────────────────────

    private GUIStyle _trackStyle;
    private GUIStyle _thumbStyle;
    private GUIStyle _trackStyle2;
    private GUIStyle _thumbStyle2;
    private GUIStyle _trackStyle3;
    private GUIStyle _thumbStyle3;
    private GUIStyle _textFieldStyleA;
    private GUIStyle _textFieldStyleB;

    private Texture2D _texTrackA;
    private Texture2D _texThumbA;
    private Texture2D _texTrackB;
    private Texture2D _texThumbB;
    private Texture2D _texTrackC;
    private Texture2D _texThumbC;
    private Texture2D _texFieldBorderA;
    private Texture2D _texFieldBgA;
    private Texture2D _texFieldBorderB;

    // ── GUI ───────────────────────────────────────────────────────────────────

    protected override void OnZUI()
    {
        EnsureStyles();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(10f);

        using (ZUI.Box("Custom Slider Styles", ZUI.ZUIStyle.Default))
        {
            GUILayout.Space(4f);
            DrawSliderDemo();
        }

        GUILayout.Space(8f);

        using (ZUI.Box("Custom Text Field Styles", ZUI.ZUIStyle.Default))
        {
            GUILayout.Space(4f);
            DrawTextFieldDemo();
        }

        GUILayout.Space(10f);
        EditorGUILayout.EndScrollView();
    }

    // ── Slider demos ──────────────────────────────────────────────────────────

    void DrawSliderDemo()
    {
        // Slider A: thick colored track + round thumb
        EditorGUILayout.LabelField("Thick track (16px), round thumb, teal/purple", EditorStyles.miniLabel);
        var rectA = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        var trackRectA = new Rect(rectA.x, rectA.center.y - 8f, rectA.width, 16f);
        _sliderA = GUI.HorizontalSlider(trackRectA, _sliderA, 0f, 1f, _trackStyle, _thumbStyle);

        GUILayout.Space(10f);

        // Slider B: thin flat track + wide flat thumb, warm orange
        EditorGUILayout.LabelField("Flat track (4px), wide thumb (40×20px), orange", EditorStyles.miniLabel);
        var rectB = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        var trackRectB = new Rect(rectB.x, rectB.center.y - 2f, rectB.width, 4f);
        _sliderB = GUI.HorizontalSlider(trackRectB, _sliderB, 0f, 1f, _trackStyle2, _thumbStyle2);

        GUILayout.Space(10f);

        // Slider C: gradient-colored track simulation, compact thumb
        EditorGUILayout.LabelField("Segmented track with value fill (manual draw)", EditorStyles.miniLabel);
        var rectC = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
        DrawValueFilledSlider(rectC, ref _sliderC, new Color(0.15f, 0.35f, 0.65f, 1f), new Color(0.55f, 0.15f, 0.75f, 1f));
    }

    // Draws a slider where the left portion of the track is filled to show the value.
    void DrawValueFilledSlider(Rect rect, ref float value, Color fillColor, Color emptyColor)
    {
        float trackH    = 8f;
        float thumbW    = 14f;
        float thumbH    = 20f;
        var   trackRect = new Rect(rect.x, rect.center.y - trackH * 0.5f, rect.width, trackH);
        float fillW     = value * trackRect.width;

        if (Event.current.type == EventType.Repaint)
        {
            // Empty track
            EditorGUI.DrawRect(trackRect, emptyColor);
            // Filled portion
            EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.y, fillW, trackH), fillColor);
        }

        // Thumb drawn on top via invisible slider + manual thumb rect
        value = GUI.HorizontalSlider(trackRect, value, 0f, 1f, GUIStyle.none, GUIStyle.none);

        if (Event.current.type == EventType.Repaint)
        {
            float thumbX = trackRect.x + fillW - thumbW * 0.5f;
            var thumbRect = new Rect(thumbX, rect.center.y - thumbH * 0.5f, thumbW, thumbH);
            EditorGUI.DrawRect(thumbRect, Color.white);
            EditorGUI.DrawRect(new Rect(thumbRect.x + 1, thumbRect.y + 1, thumbRect.width - 2, thumbRect.height - 2), fillColor);
        }
    }

    // ── Text field demos ──────────────────────────────────────────────────────

    void DrawTextFieldDemo()
    {
        // Field A: colored background, no border, larger font
        EditorGUILayout.LabelField("Dark bg, large font (14px), no border", EditorStyles.miniLabel);
        _textA = EditorGUI.TextField(GUILayoutUtility.GetRect(1f, 26f, GUILayout.ExpandWidth(true)), _textA, _textFieldStyleA);

        GUILayout.Space(8f);

        // Field B: transparent background, colored outline, accent text color
        EditorGUILayout.LabelField("Outlined field — transparent bg, 2px colored border, accent text", EditorStyles.miniLabel);
        _textB = EditorGUI.TextField(GUILayoutUtility.GetRect(1f, 26f, GUILayout.ExpandWidth(true)), _textB, _textFieldStyleB);

        GUILayout.Space(8f);

        // Comparison: default EditorStyles.textField
        EditorGUILayout.LabelField("Default EditorStyles.textField for comparison", EditorStyles.miniLabel);
        EditorGUILayout.TextField("Default style");
    }

    // ── Style construction ────────────────────────────────────────────────────

    void EnsureStyles()
    {
        if (_trackStyle != null) return;

        // ── Slider A: thick teal track + round purple thumb ───────────────────
        _texTrackA = MakeTex(1, 1, new Color(0.10f, 0.35f, 0.40f, 1f));
        _texThumbA = MakeRoundTex(20, new Color(0.60f, 0.25f, 0.80f, 1f));

        _trackStyle = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = 16f,
        };
        _trackStyle.normal.background = _texTrackA;

        _thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth  = 20f,
            fixedHeight = 20f,
        };
        _thumbStyle.normal.background  = _texThumbA;
        _thumbStyle.active.background  = _texThumbA;
        _thumbStyle.focused.background = _texThumbA;
        _thumbStyle.hover.background   = _texThumbA;

        // ── Slider B: flat orange track + wide flat thumb ─────────────────────
        _texTrackB = MakeTex(1, 1, new Color(0.25f, 0.12f, 0.05f, 1f));
        _texThumbB = MakeTex(1, 1, new Color(0.90f, 0.50f, 0.10f, 1f));

        _trackStyle2 = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = 4f,
        };
        _trackStyle2.normal.background = _texTrackB;
        _trackStyle2.border = new RectOffset(0, 0, 0, 0);

        _thumbStyle2 = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth  = 40f,
            fixedHeight = 20f,
        };
        _thumbStyle2.normal.background  = _texThumbB;
        _thumbStyle2.active.background  = _texThumbB;
        _thumbStyle2.focused.background = _texThumbB;
        _thumbStyle2.hover.background   = _texThumbB;

        // ── Text field A: dark bg, large font ─────────────────────────────────
        _texFieldBgA = MakeTex(1, 1, new Color(0.10f, 0.10f, 0.14f, 1f));

        _textFieldStyleA = new GUIStyle(EditorStyles.textField)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Normal,
        };
        _textFieldStyleA.normal.background = _texFieldBgA;
        _textFieldStyleA.focused.background = _texFieldBgA;
        _textFieldStyleA.normal.textColor  = new Color(0.80f, 0.95f, 1.00f, 1f);
        _textFieldStyleA.focused.textColor = new Color(1.00f, 1.00f, 1.00f, 1f);
        _textFieldStyleA.padding = new RectOffset(6, 6, 4, 4);
        _textFieldStyleA.border  = new RectOffset(0, 0, 0, 0);

        // ── Text field B: outlined, transparent bg ────────────────────────────
        // Unity doesn't support CSS-like border-only backgrounds directly.
        // We simulate it by building a bordered texture (2px border, transparent fill).
        _texFieldBorderB = MakeBorderedTex(64, 26, new Color(0.30f, 0.75f, 0.90f, 1f), 2, new Color(0.08f, 0.10f, 0.14f, 1f));

        _textFieldStyleB = new GUIStyle(EditorStyles.textField)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Normal,
        };
        _textFieldStyleB.normal.background  = _texFieldBorderB;
        _textFieldStyleB.focused.background = _texFieldBorderB;
        _textFieldStyleB.normal.textColor   = new Color(0.30f, 0.90f, 1.00f, 1f);
        _textFieldStyleB.focused.textColor  = Color.white;
        _textFieldStyleB.padding = new RectOffset(8, 6, 5, 4);
        _textFieldStyleB.border  = new RectOffset(3, 3, 3, 3);
    }

    // ── Texture helpers ───────────────────────────────────────────────────────

    static Texture2D MakeTex(int w, int h, Color col)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        t.SetPixels(pix);
        t.Apply();
        return t;
    }

    // Creates a circular-ish texture by clearing corners.
    static Texture2D MakeRoundTex(int size, Color col)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(r - dist);
            t.SetPixel(x, y, new Color(col.r, col.g, col.b, alpha));
        }
        t.Apply();
        return t;
    }

    // Creates a texture with a solid border and a fill in the center.
    static Texture2D MakeBorderedTex(int w, int h, Color border, int bw, Color fill)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool isBorder = x < bw || x >= w - bw || y < bw || y >= h - bw;
            t.SetPixel(x, y, isBorder ? border : fill);
        }
        t.Apply();
        return t;
    }
}
