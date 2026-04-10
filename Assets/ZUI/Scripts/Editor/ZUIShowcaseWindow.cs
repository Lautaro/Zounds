// ZUIShowcaseWindow.cs
// Demonstrates ZUI multi-selection controls and configurable label widths.
// Open via: Tools > ZUI > Control Showcase

using UnityEditor;
using UnityEngine;

public class ZUIShowcaseWindow : ZUIWindow
{
    protected override string ConsumerSheetName => "Showcase";

    const string k_SheetPath = "Assets/ZUI/SystemAssets/ZUIShowcaseSheet.asset";

    [MenuItem("Tools/ZUI/Control Showcase")]
    static void Open() => GetWindow<ZUIShowcaseWindow>("ZUI Showcase");

    protected override void OnZUIEnable()
    {
        EnsureShowcaseSheet();
    }

    void EnsureShowcaseSheet()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(k_SheetPath);
        if (existing != null)
        {
            ZUI.RegisterConsumerSheet("Showcase", existing);
            return;
        }
        // Copy from editor sheet
        var editorSheet = ZUI.EditorSheet;
        if (editorSheet == null) return;
        string json = EditorJsonUtility.ToJson(editorSheet, false);
        var showcase = ScriptableObject.CreateInstance<ZUIStyleSheetAsset>();
        EditorJsonUtility.FromJsonOverwrite(json, showcase);
        string dir = System.IO.Path.GetDirectoryName(k_SheetPath);
        if (!AssetDatabase.IsValidFolder(dir))
            System.IO.Directory.CreateDirectory(dir);
        AssetDatabase.CreateAsset(showcase, k_SheetPath);
        AssetDatabase.SaveAssets();
        ZUI.RegisterConsumerSheet("Showcase", showcase);
        Debug.Log($"[ZUI] Created Showcase sheet at {k_SheetPath}");
    }

    // ── State ────────────────────────────────────────────────────────────────
    Vector2 _scroll;

    int _cycleMode;
    int _cycleArrowMode;
    int _miniRadioH1;
    int _miniRadioH2;
    int _miniRadioH3;
    int _miniRadioV;
    int _cycleShape;
    int _cycleArrowShape;
    int _cycleEase;

    float _sliderA = 0.5f;
    float _sliderB = 45f;
    float _sliderC = 0.75f;
    bool _toggleA = true;
    bool _toggleB;
    bool _toggleC;
    int _toolbarSel;

    // Form showcase state
    float _formRadius = 4f;
    float _formBlur = 2f;
    int _formPasses = 5;
    Color _formColor = new Color(0.3f, 0.6f, 1f, 0.5f);
    float _formOffsetX = 3f;
    float _formOffsetY = 3f;
    bool _formEnabled = true;
    int _formBlendMode;
    float _formOpacity = 0.8f;
    float _formScale = 1f;
    bool _formInner;

    string[] _modeLabels  = { "Linear", "Radial", "Fixed" };
    string[] _shapeLabels = { "Ellipse", "Square", "Diamond", "Star" };
    string[] _easeLabels  = { "Linear", "EaseIn", "EaseOut", "EaseInOut", "Bounce" };
    string[] _qualityLabels = { "Low", "Medium", "High", "Ultra" };
    string[] _tabLabels   = { "Controls", "Layout", "Sliders" };
    int _showcaseTab;

    // New controls showcase state
    int _microRadioSel;
    float _microSliderA = 50f;
    float _microSliderB = 0.75f;
    ZUIColorRef _paletteColor = new ZUIColorRef(Color.cyan);
    ZUIPaletteColorControl _paletteCtrl;
    Vector2 _slider2D = Vector2.zero;
    Vector2 _slider2DLarge = new Vector2(0.5f, 0.5f);
    string[] _microRadioLabels = { "Option1", "Option2", "Option3", "Option4", "Option5" };

    // Blocks demo state
    float _blocksSliderA = 0.5f;
    float _blocksSliderB = 0.3f;
    float _blocksSliderC = 75f;
    bool _blocksToggle = true;
    Vector2 _blocksOffset = Vector2.zero;
    int _blocksAlign;
    [System.NonSerialized] string[] _alignLabels = { "Top", "Center", "Bottom", "Spread", "Even" };

    // Stacked slider demo
    float _stackedVal = 8f;

    protected override void OnZUI()
    {
        string[] tabs = { "Controls", "Layout", "Sliders" };
        _showcaseTab = ZUI.MiniRadio(_showcaseTab, tabs);
        ZUI.VerticalSpace("V Control Gap");

        _scroll = GUILayout.BeginScrollView(_scroll);

        switch (_showcaseTab)
        {
            case 0: // Controls
                DrawSection_MultiSelect();
                ZUI.VerticalSpace("V Control Gap");
                DrawSection_NewControls();
                break;
            case 1: // Layout
                DrawSection_Forms();
                ZUI.VerticalSpace("V Control Gap");
                DrawSection_Blocks();
                ZUI.VerticalSpace("V Control Gap");
                DrawSection_LabelWidths();
                ZUI.VerticalSpace("V Control Gap");
                DrawSection_MixedControls();
                break;
            case 2: // Sliders
                DrawSection_Sliders();
                break;
        }

        GUILayout.EndScrollView();
    }

    // ── Multi-Selection Controls ─────────────────────────────────────────────

    /// <summary>Measures the widest CycleButton width across multiple label arrays.</summary>
    float MeasureCycleWidth(string style, params string[][] labelSets)
    {
        var sheet = ZUI.ActiveSheet;
        var def = sheet?.FindButton(style);
        var labelStyle = def?.GetLabelStyle() ?? EditorStyles.miniButton;
        float padH = def != null ? def.padding.PadLeft + def.padding.PadRight + 4f : 12f;
        float maxW = 0f;
        foreach (var labels in labelSets)
            foreach (var label in labels)
            {
                float w = labelStyle.CalcSize(new GUIContent(label)).x + padH;
                if (w > maxW) maxW = w;
            }
        return maxW;
    }

    void DrawSection_MultiSelect()
    {
        using (ZUI.Box("Multi-Selection Controls"))
        {
            // Measure widest across ALL cycle button label sets for uniform width
            float cycleW = MeasureCycleWidth("Toggle", _modeLabels, _shapeLabels, _easeLabels, _qualityLabels);
            float arrowW = cycleW + 42f; // arrows add ~42px (20px per arrow + gaps)

            var form = ZUI.Form();

            // CycleButton
            form.Header("CycleButton");
            form.Add("Mode",       () => _cycleMode  = ZUI.CycleButton(_cycleMode,  _modeLabels,  "Toggle", GUILayout.Width(cycleW)));
            form.Add("Shape",      () => _cycleShape = ZUI.CycleButton(_cycleShape, _shapeLabels, "Toggle", GUILayout.Width(cycleW)));
            form.Add("Ease Curve", () => _cycleEase  = ZUI.CycleButton(_cycleEase,  _easeLabels,  "Toggle", GUILayout.Width(cycleW)));
            form.Gap();

            // CycleArrows
            form.Header("CycleArrows");
            form.Add("Mode",  () => _cycleArrowMode = ZUI.CycleArrows(_cycleArrowMode, _modeLabels, "Toggle", "", GUILayout.Width(arrowW)));
            form.Add("Shape", () => _cycleArrowShape  = ZUI.CycleArrows(_cycleArrowShape, _shapeLabels, "Toggle", "", GUILayout.Width(arrowW)));
            form.Gap();

            // MiniRadio horizontal (shaped)
            form.Header("MiniRadio (Horizontal, Shaped)");
            form.Add("Quality", () => _miniRadioH1 = ZUI.MiniRadio(_miniRadioH1, _qualityLabels, "Toggle", shaped: true));
            form.Add("Ease",    () => _miniRadioH2 = ZUI.MiniRadio(_miniRadioH2, _easeLabels, "Toggle", shaped: true));
            form.Gap();

            // MiniRadio vertical (shaped)
            form.Header("MiniRadio (Vertical, Shaped)");
            form.Add("Quality", () => _miniRadioV = ZUI.MiniRadioVertical(_miniRadioV, _qualityLabels, "Toggle", shaped: true));

            form.Draw();
        }
    }

    // ── New Controls ────────────────────────────────────────────────────────

    void DrawSection_NewControls()
    {
        using (ZUI.Box("New Controls"))
        {
            // MicroRadio
            EditorGUILayout.LabelField("MicroRadio", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            GUILayout.BeginHorizontal();
            _microRadioSel = ZUI.MicroRadio(_microRadioSel, _microRadioLabels, "Toggle", wrap: true);
            ZUI.HorizontalSpace("H Control Gap");
            float ms = ZUI.MicroRadioAnimDuration * 1000f;
            ms = ZUI.MicroSlider(ms, 100f, 1000f, "Speed", ZUI.SliderStyle.Default, false, null, GUILayout.Width(100f));
            ZUI.MicroRadioAnimDuration = ms / 1000f;
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");

            // MicroSlider
            EditorGUILayout.LabelField("MicroSlider", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            float msW = 120f; // shared fixed width for both micro sliders
            _microSliderA = ZUI.MicroSlider(_microSliderA, 0f, 100f, "Volume", ZUI.SliderStyle.Default, false, null, GUILayout.Width(msW));
            ZUI.VerticalSpace("V Section Rows");
            _microSliderB = ZUI.MicroSlider(_microSliderB, 0f, 1f, "Amount", ZUI.SliderStyle.Default, true, null, GUILayout.Width(msW));

            ZUI.VerticalSpace("V Control Gap");

            // Palette Color
            EditorGUILayout.LabelField("Palette Color Picker", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            if (_paletteCtrl == null)
                _paletteCtrl = ZUI.PaletteColorField(() => _paletteColor, v => _paletteColor = v);
            var form = ZUI.Form();
            form.Add("Color", _paletteCtrl);
            form.Draw();

        }
    }

    // ── Label Width Showcase ─────────────────────────────────────────────────

    void DrawSection_LabelWidths()
    {
        using (ZUI.Box("Configurable Label Widths"))
        {
            EditorGUILayout.LabelField("Wide labels (descriptive)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Corner Radius", ZUI.LabelWide());
            _sliderA = EditorGUILayout.Slider(_sliderA, 0f, 1f);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Track Height", ZUI.LabelWide());
            _sliderB = EditorGUILayout.Slider(_sliderB, 0f, 360f);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");
            EditorGUILayout.LabelField("Narrow labels (compact rows)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ang", ZUI.LabelNarrow());
            _sliderB = EditorGUILayout.Slider(_sliderB, 0f, 360f);
            ZUI.HorizontalSpace("H Control Gap");
            EditorGUILayout.LabelField("Crv", ZUI.LabelNarrow());
            _sliderC = EditorGUILayout.Slider(_sliderC, 0f, 1f);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Section Rows");

            // Show current values from sheet
            ZUI.VerticalSpace("V Control Gap");
            var sheet = ZUI.ActiveSheet;
            if (sheet != null)
            {
                EditorGUILayout.LabelField($"Sheet values:  Wide={sheet.labelWidthWide:F0}  Narrow={sheet.labelWidthNarrow:F0}  InputMin={sheet.inputFieldMinWidth:F0}", EditorStyles.miniLabel);
            }
        }
    }

    // ── Form Builder ─────────────────────────────────────────────────────────

    void DrawSection_Forms()
    {
        using (ZUI.Box("ZUI.Form — Declarative Layout"))
        {
            EditorGUILayout.LabelField("Build typed controls, compose into forms, Draw() once.", EditorStyles.wordWrappedMiniLabel);
            ZUI.VerticalSpace("V Section Rows");

            // ── Example 1: Typed controls — no lambdas for simple values ─
            var enabled = ZUI.Toggle(() => _formEnabled, v => _formEnabled = v);
            var color   = ZUI.ColorField(() => _formColor, v => _formColor = v);
            var blur    = ZUI.Slider(() => _formBlur, v => _formBlur = v, 0f, 20f);
            var passes  = ZUI.IntSlider(() => _formPasses, v => _formPasses = v, 1, 20);
            var inner   = ZUI.Toggle(() => _formInner, v => _formInner = v);

            var form1 = ZUI.Form();
            form1.Add("Enabled", enabled);
            form1.Add("Color", color);
            form1.Add("Blur Radius", blur);
            form1.Add("Blur Passes", passes);
            if (_formBlur > 0)
                form1.Add("Inner Glow", inner);
            form1.Draw();

            ZUI.VerticalSpace("V Control Gap");

            // ── Example 2: Multi-control rows with typed controls ────────
            EditorGUILayout.LabelField("Multi-control rows", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            var xField = ZUI.FloatField(() => _formOffsetX, v => _formOffsetX = v, 60f);
            var yField = ZUI.FloatField(() => _formOffsetY, v => _formOffsetY = v, 60f);
            var radius = ZUI.Slider(() => _formRadius, v => _formRadius = v, 0f, 24f);
            var opacity = ZUI.Slider(() => _formOpacity, v => _formOpacity = v, 0f, 1f);
            var blend  = ZUI.CycleButton(() => _formBlendMode, v => _formBlendMode = v, _modeLabels);

            var offsetRow = ZUI.Row("Offset").Add(xField).Add(yField);

            var form2 = ZUI.Form();
            form2.Add("Corner Radius", radius);
            form2.Add("Opacity", opacity);
            form2.Add(offsetRow);
            form2.Add("Blend Mode", blend);
            form2.Draw();

            ZUI.VerticalSpace("V Control Gap");

            // ── Example 3: Conditional + mixed (typed + lambda) ──────────
            EditorGUILayout.LabelField("Conditional rows (toggle Enabled above)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            var scale = ZUI.Slider(() => _formScale, v => _formScale = v, 0.1f, 4f);
            var intensity = ZUI.Slider(() => _formOpacity, v => _formOpacity = v, 0f, 1f);
            var quality = ZUI.IntSlider(() => _formPasses, v => _formPasses = v, 1, 20);

            var form3 = ZUI.Form();
            form3.Header("Always Visible");
            form3.Add("Blend Mode", blend);
            form3.Add("Scale", scale);
            form3.Gap();
            if (_formEnabled)
            {
                form3.Header("Enabled Section");
                form3.Add("Intensity", intensity);
                form3.Add("Quality", quality);

                // Multi-control row: typed toggle + typed cycle
                var active = ZUI.Toggle(() => _toggleC, v => _toggleC = v, "Active", "Toggle");
                var shape  = ZUI.CycleButton(() => _cycleShape, v => _cycleShape = v, _shapeLabels);
                var effectRow = ZUI.Row("Effect").Add(active).Add(shape);
                form3.Add(effectRow);
            }
            form3.Draw();

            ZUI.VerticalSpace("V Control Gap");

            // ── Example 4: Style editor pattern ─────────────────────────
            EditorGUILayout.LabelField("Style editor pattern (shadow fields)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            var shadowColor = ZUI.ColorField(() => _formColor, v => _formColor = v);
            var shadowX = ZUI.FloatField(() => _formOffsetX, v => _formOffsetX = v, 48f);
            var shadowY = ZUI.FloatField(() => _formOffsetY, v => _formOffsetY = v, 48f);
            var shadowBlur = ZUI.Slider(() => _formBlur, v => _formBlur = v, 0f, 20f);
            var shadowPasses = ZUI.IntSlider(() => _formPasses, v => _formPasses = v, 1, 20);

            var shadowRow = ZUI.Row("Shadow").Add(shadowColor).Add(shadowX).Add(shadowY);

            var shadowForm = ZUI.Form();
            shadowForm.Add(shadowRow);
            shadowForm.Add("Blur", shadowBlur);
            if (_formBlur > 0)
                shadowForm.Add("Passes", shadowPasses);

            shadowForm.Draw();
        }
    }

    // ── Mixed Controls ───────────────────────────────────────────────────────

    void DrawSection_MixedControls()
    {
        using (ZUI.Box("Mixed Layout"))
        {
            // Toolbar row
            _toolbarSel = ZUI.MiniRadio(_toolbarSel, _tabLabels);
            ZUI.VerticalSpace("V Section Rows");

            if (_toolbarSel == 0)
            {
                // Toggles + cycle on same row
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Enable", ZUI.LabelWide());
                _toggleA = ZUI.Toggle(_toggleA, "Feature A", "Toggle");
                ZUI.HorizontalSpace("H Control Gap");
                _toggleB = ZUI.Toggle(_toggleB, "Feature B", "Toggle");
                GUILayout.EndHorizontal();

                ZUI.VerticalSpace("V Section Rows");

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Blend Mode", ZUI.LabelWide());
                _cycleMode = ZUI.CycleButton(_cycleMode, _modeLabels);
                ZUI.HorizontalSpace("H Control Gap");
                EditorGUILayout.LabelField("Shape", ZUI.LabelNarrow());
                _cycleShape = ZUI.CycleButton(_cycleShape, _shapeLabels);
                GUILayout.EndHorizontal();
            }
            else if (_toolbarSel == 1)
            {
                EditorGUILayout.LabelField("Label width comparison", EditorStyles.boldLabel);
                ZUI.VerticalSpace("V Section Rows");

                // Same control with wide vs narrow label
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Opacity", ZUI.LabelWide());
                _sliderC = EditorGUILayout.Slider(_sliderC, 0f, 1f);
                GUILayout.EndHorizontal();

                ZUI.VerticalSpace("V Section Rows");

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Op", ZUI.LabelNarrow());
                _sliderC = EditorGUILayout.Slider(_sliderC, 0f, 1f);
                ZUI.HorizontalSpace("H Control Gap");
                EditorGUILayout.LabelField("Val", ZUI.LabelNarrow());
                _sliderA = EditorGUILayout.Slider(_sliderA, 0f, 1f);
                GUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("ZUI.Slider with configurable widths", EditorStyles.boldLabel);
                ZUI.VerticalSpace("V Section Rows");

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Angle", ZUI.LabelWide());
                _sliderB = ZUI.Slider(_sliderB, 0f, 360f, "", "SmallSlider");
                GUILayout.EndHorizontal();

                ZUI.VerticalSpace("V Section Rows");

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Amount", ZUI.LabelWide());
                _sliderA = ZUI.Slider(_sliderA, 0f, 1f, "", "SmallSlider");
                GUILayout.EndHorizontal();

                ZUI.VerticalSpace("V Section Rows");

                // Compact row with narrow labels
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("X", ZUI.LabelNarrow());
                _sliderA = ZUI.Slider(_sliderA, 0f, 1f, "", "SmallSlider");
                ZUI.HorizontalSpace("H Control Gap");
                EditorGUILayout.LabelField("Y", ZUI.LabelNarrow());
                _sliderC = ZUI.Slider(_sliderC, 0f, 1f, "", "SmallSlider");
                GUILayout.EndHorizontal();
            }
        }
    }

    // ── Blocks (height-matched cells) ───────────────────────────────────────

    void DrawSection_Blocks()
    {
        using (ZUI.Box("ZUI.Blocks — Height-Matched Cells"))
        {
            // Alignment selector
            EditorGUILayout.LabelField("Cell alignment for right column:", EditorStyles.miniLabel);
            ZUI.VerticalSpace("V Section Rows");
            _blocksAlign = ZUI.MiniRadio(_blocksAlign, _alignLabels);
            ZUI.VerticalSpace("V Control Gap");

            ZUIAlign align = (ZUIAlign)_blocksAlign;

            // Demo: 2D slider + stacked MicroSliders + toggle
            using (var blocks = ZUI.Blocks("showcase_blocks_demo"))
            {
                using (blocks.Cell(ZUIAlign.Top))
                {
                    _blocksOffset = ZUI.Slider2D(_blocksOffset,
                        new Vector2(-10f, -10f), new Vector2(10f, 10f),
                        size: 80f, labelX: "X", labelY: "Y",
                        defaultValue: Vector2.zero);
                }
                using (blocks.Cell(align, GUILayout.Width(100f)))
                {
                    _blocksSliderA = ZUI.MicroSlider(_blocksSliderA, 0f, 1f, "Amount");
                    if (align == ZUIAlign.Spread || align == ZUIAlign.Even) GUILayout.FlexibleSpace();
                    else ZUI.VerticalSpace("V Control Gap");
                    _blocksSliderC = ZUI.MicroSlider(_blocksSliderC, 0f, 100f, "Volume");
                }
                using (blocks.Cell(align))
                {
                    _blocksToggle = ZUI.Toggle(_blocksToggle, "Enabled", "Toggle");
                    if (align == ZUIAlign.Spread || align == ZUIAlign.Even) GUILayout.FlexibleSpace();
                    else ZUI.VerticalSpace("V Control Gap");
                    _blocksSliderB = ZUI.MicroSlider(_blocksSliderB, 0f, 1f, "Mix");
                }
            }
        }
    }

    // ── Sliders ─────────────────────────────────────────────────────────────

    void DrawSection_Sliders()
    {
        using (ZUI.Box("Slider Variants"))
        {
            // Regular slider
            EditorGUILayout.LabelField("Standard Slider", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Angle", ZUI.LabelWide());
            _sliderB = ZUI.Slider(_sliderB, 0f, 360f, "", "SmallSlider");
            GUILayout.EndHorizontal();
            ZUI.VerticalSpace("V Section Rows");
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Amount", ZUI.LabelWide());
            _sliderA = ZUI.Slider(_sliderA, 0f, 1f, "", "SmallSlider");
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");

            // MicroSlider
            EditorGUILayout.LabelField("MicroSlider", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            float msW = 120f;
            _microSliderA = ZUI.MicroSlider(_microSliderA, 0f, 100f, "Volume", ZUI.SliderStyle.Default, false, null, GUILayout.Width(msW));
            ZUI.VerticalSpace("V Section Rows");
            _microSliderB = ZUI.MicroSlider(_microSliderB, 0f, 1f, "Amount", ZUI.SliderStyle.Default, true, null, GUILayout.Width(msW));

            ZUI.VerticalSpace("V Control Gap");

            // Stacked slider
            EditorGUILayout.LabelField("Stacked Slider (label+value / track)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            GUILayout.BeginHorizontal();
            _stackedVal = ZUI.SliderStacked(_stackedVal, 0f, 24f, "Radius", "SmallSlider");
            ZUI.HorizontalSpace();
            EditorGUILayout.LabelField("← Track matches label+field width", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");

            // 2D Slider
            EditorGUILayout.LabelField("2D Slider (XY Pad)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");
            GUILayout.BeginHorizontal();
            _slider2D = ZUI.Slider2D(_slider2D, new Vector2(-10f, -10f), new Vector2(10f, 10f),
                size: 100f, labelX: "X", labelY: "Y", defaultValue: Vector2.zero);
            ZUI.HorizontalSpace("H Control Gap");
            _slider2DLarge = ZUI.Slider2D(_slider2DLarge, Vector2.zero, Vector2.one,
                size: 80f, labelX: "Pan", labelY: "Tilt", defaultValue: new Vector2(0.5f, 0.5f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
