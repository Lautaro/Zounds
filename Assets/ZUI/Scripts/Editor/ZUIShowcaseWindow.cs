// ZUIShowcaseWindow.cs
// Demonstrates ZUI multi-selection controls and configurable label widths.
// Open via: Tools > ZUI > Control Showcase

using UnityEditor;
using UnityEngine;

public class ZUIShowcaseWindow : ZUIWindow
{
    // Connects to the Zhowcase style sheet via its consumerName.
    // The sheet asset lives at Assets/ZUI/SystemAssets/ZUIShowcaseSheet.asset
    // with consumerName = "Zhowcase". Auto-discovery registers it at domain reload.
    protected override string ConsumerSheetName => "Zhowcase";

    [MenuItem("Tools/ZUI/Zhowcase")]
    static void Open() => GetWindow<ZUIShowcaseWindow>("Zhowcase");

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
    ZUIColorRef _pickerCustom = new ZUIColorRef(new Color(0.3f, 0.6f, 1f));
    ZUIColorRef _pickerPalette = new ZUIColorRef(Color.cyan, "EditorAccent");
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

    // Fill demo
    // Row demo
    bool _rowMute;
    bool _rowSolo;
    float _rowVol = 0.8f;
    float _rowPitch = 1f;

    ZUIGradient _fillSolid = new ZUIGradient(new Color(0.3f, 0.6f, 1f));
    ZUIGradient _fillGradient = new ZUIGradient(new Color(0.2f, 0.4f, 0.8f), new Color(0.8f, 0.2f, 0.4f));

    // Envelope demo state
    System.Collections.Generic.List<ZUIEnvelopePoint> _envA;
    System.Collections.Generic.List<ZUIEnvelopePoint> _envB;
    System.Collections.Generic.List<ZUIEnvelopePoint> _envC;
    ZUIEnvelopeRuntime _envARt;
    ZUIEnvelopeRuntime _envBRt;
    ZUIEnvelopeRuntime _envCRt;
    ZUIEnvelopeDef _envDemoDef;
    ZUIColorRef _envACurve = new ZUIColorRef(new Color(0.4f, 0.8f, 1f));
    ZUIColorRef _envBCurve = new ZUIColorRef(new Color(0.9f, 0.6f, 0.3f));
    ZUIColorRef _envCCurve = new ZUIColorRef(new Color(0.6f, 0.9f, 0.4f));

    protected override void OnZUI()
    {
        string[] tabs = { "Buttons", "Sliders", "Color", "Layout", "Forms", "Fill", "Row", "Envelope" };
        _showcaseTab = this.MiniRadio(_showcaseTab, tabs, "TabButton");
        this.VerticalSpace("V Control Gap");

        _scroll = GUILayout.BeginScrollView(_scroll);

        switch (_showcaseTab)
        {
            case 0: // Buttons — toggles, radio, cycle
                DrawSection_Buttons();
                break;
            case 1: // Sliders — all slider variants
                DrawSection_Sliders();
                break;
            case 2: // Color — color pickers
                DrawSection_NewControls();
                break;
            case 3: // Layout — blocks, label widths
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawSection_Blocks();
                GUILayout.EndVertical();
                this.HorizontalSpace("H Control Gap");
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawSection_LabelWidths();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                break;
            case 4: // Forms
                DrawSection_Forms();
                break;
            case 5: // Fill
                DrawSection_Fill();
                break;
            case 6: // Row
                DrawSection_Row();
                break;
            case 7: // Envelope
                DrawSection_Envelope();
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

    void DrawSection_Buttons()
    {
        float cycleW = MeasureCycleWidth("Toggle", _modeLabels, _shapeLabels, _easeLabels, _qualityLabels);
        float arrowW = cycleW + 42f;

        GUILayout.BeginHorizontal();

        // Left column
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (this.Box("Toggle"))
        {
            GUILayout.BeginHorizontal();
            _toggleA = this.Toggle(_toggleA, "Feature A", "Toggle");
            this.HorizontalSpace("H Control Gap");
            _toggleB = this.Toggle(_toggleB, "Feature B", "Toggle");
            GUILayout.EndHorizontal();
        }
        this.VerticalSpace("V Control Gap");
        using (this.Box("CycleButton"))
        {
            _cycleMode = this.CycleButton(_cycleMode, _modeLabels, "Toggle", GUILayout.Width(cycleW));
        }
        this.VerticalSpace("V Control Gap");
        using (this.Box("CycleArrows"))
        {
            _cycleArrowMode = this.CycleArrows(_cycleArrowMode, _modeLabels, "Toggle", "", GUILayout.Width(arrowW));
        }
        GUILayout.EndVertical();

        this.HorizontalSpace("H Control Gap");

        // Right column
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (this.Box("MiniRadio"))
        {
            _miniRadioH1 = this.MiniRadio(_miniRadioH1, _qualityLabels, "Toggle", shaped: true);
        }
        this.VerticalSpace("V Control Gap");
        using (this.Box("MiniRadio (Vertical)"))
        {
            _miniRadioV = this.MiniRadioVertical(_miniRadioV, _qualityLabels, "Toggle", shaped: true);
        }
        this.VerticalSpace("V Control Gap");
        using (this.Box("MicroRadio"))
        {
            _microRadioSel = this.MicroRadio(_microRadioSel, _microRadioLabels, "Toggle", wrap: true);
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    // ── New Controls ────────────────────────────────────────────────────────

    void DrawSection_NewControls()
    {
        using (this.Box("ZUI.ColorPicker"))
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.MaxWidth(280f));
            EditorGUILayout.LabelField("Custom color", EditorStyles.miniLabel);
            ZUI.ColorPicker(ref _pickerCustom);
            GUILayout.EndVertical();

            this.HorizontalSpace("H Control Gap");

            GUILayout.BeginVertical(GUILayout.MaxWidth(280f));
            EditorGUILayout.LabelField("Palette color", EditorStyles.miniLabel);
            ZUI.ColorPicker(ref _pickerPalette);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    // ── Label Width Showcase ─────────────────────────────────────────────────

    void DrawSection_LabelWidths()
    {
        using (this.Box("Configurable Label Widths"))
        {
            EditorGUILayout.LabelField("Wide labels (descriptive)", EditorStyles.boldLabel);
            this.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Corner Radius", ZUI.RowLabelStyle, ZUI.LabelWide());
            _sliderA = this.Slider(_sliderA, 0f, 1f, "", "SmallSlider");
            GUILayout.EndHorizontal();

            this.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Track Height", ZUI.RowLabelStyle, ZUI.LabelWide());
            _sliderB = this.Slider(_sliderB, 0f, 360f, "", "SmallSlider");
            GUILayout.EndHorizontal();

            this.VerticalSpace("V Control Gap");
            EditorGUILayout.LabelField("Narrow labels (compact rows)", EditorStyles.boldLabel);
            this.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ang", ZUI.RowLabelStyle, ZUI.LabelNarrow());
            _sliderB = this.Slider(_sliderB, 0f, 360f, "", "SmallSlider");
            this.HorizontalSpace("H Control Gap");
            EditorGUILayout.LabelField("Crv", ZUI.RowLabelStyle, ZUI.LabelNarrow());
            _sliderC = this.Slider(_sliderC, 0f, 1f, "", "SmallSlider");
            GUILayout.EndHorizontal();

            this.VerticalSpace("V Section Rows");

            // Show current values from sheet
            this.VerticalSpace("V Control Gap");
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
        using (this.Box("ZUI.Form — Declarative Layout"))
        {
            GUILayout.BeginHorizontal();

            // Left: labeled rows + conditional
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            using (this.Box("Labeled Rows"))
            {
                var enabled = this.Toggle(() => _formEnabled, v => _formEnabled = v);
                var blur    = this.Slider(() => _formBlur, v => _formBlur = v, 0f, 20f);
                var passes  = ZUI.IntSlider(() => _formPasses, v => _formPasses = v, 1, 20);

                var form = ZUI.Form();
                form.Add("Enabled", enabled);
                form.Add("Blur Radius", blur);
                if (_formEnabled)
                    form.Add("Passes", passes);
                form.Draw();
            }
            GUILayout.EndVertical();

            this.HorizontalSpace("H Control Gap");

            // Right: multi-control row
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            using (this.Box("Multi-Control Row"))
            {
                var xField = ZUI.FloatField(() => _formOffsetX, v => _formOffsetX = v, 60f);
                var yField = ZUI.FloatField(() => _formOffsetY, v => _formOffsetY = v, 60f);
                var blend  = this.CycleButton(() => _formBlendMode, v => _formBlendMode = v, _modeLabels);

                var form = ZUI.Form();
                form.Add(ZUI.Row("Offset").Add(xField).Add(yField));
                form.Add("Blend Mode", blend);
                form.Draw();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
    }

    // ── Blocks (height-matched cells) ───────────────────────────────────────

    void DrawSection_Blocks()
    {
        using (this.Box("ZUI.Blocks — Height-Matched Cells"))
        {
            // Alignment selector
            EditorGUILayout.LabelField("Cell alignment for right column:", EditorStyles.miniLabel);
            this.VerticalSpace("V Section Rows");
            _blocksAlign = this.MiniRadio(_blocksAlign, _alignLabels);
            this.VerticalSpace("V Control Gap");

            ZUIAlign align = (ZUIAlign)_blocksAlign;

            // Demo: 2D slider + stacked MicroSliders + toggle
            using (var blocks = this.Blocks("showcase_blocks_demo"))
            {
                using (blocks.Cell(ZUIAlign.Top))
                {
                    _blocksOffset = this.Slider2D(_blocksOffset,
                        new Vector2(-10f, -10f), new Vector2(10f, 10f),
                        size: 80f, labelX: "X", labelY: "Y",
                        defaultValue: Vector2.zero);
                }
                using (blocks.Cell(align, GUILayout.Width(100f)))
                {
                    _blocksSliderA = this.MicroSlider(_blocksSliderA, 0f, 1f, "Amount");
                    if (align == ZUIAlign.Spread || align == ZUIAlign.Even) GUILayout.FlexibleSpace();
                    else this.VerticalSpace("V Control Gap");
                    _blocksSliderC = this.MicroSlider(_blocksSliderC, 0f, 100f, "Volume");
                }
                using (blocks.Cell(align))
                {
                    _blocksToggle = this.Toggle(_blocksToggle, "Enabled", "Toggle");
                    if (align == ZUIAlign.Spread || align == ZUIAlign.Even) GUILayout.FlexibleSpace();
                    else this.VerticalSpace("V Control Gap");
                    _blocksSliderB = this.MicroSlider(_blocksSliderB, 0f, 1f, "Mix");
                }
            }
        }
    }

    // ── Sliders ─────────────────────────────────────────────────────────────

    void DrawSection_Sliders()
    {
        GUILayout.BeginHorizontal();

        // Left column: Standard + MicroSlider
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (this.Box("Standard"))
        {
            _sliderB = this.Slider(_sliderB, 0f, 360f, "Angle", "SmallSlider");
        }
        this.VerticalSpace("V Control Gap");
        using (this.Box("MicroSlider"))
        {
            _microSliderA = this.MicroSlider(_microSliderA, 0f, 100f, "Volume", ZUI.SliderStyle.Default, false, null, GUILayout.Width(120f));
        }
        this.VerticalSpace("V Control Gap");
        using (this.Box("Stacked"))
        {
            _stackedVal = this.SliderStacked(_stackedVal, 0f, 24f, "Radius", "SmallSlider");
        }
        GUILayout.EndVertical();

        this.HorizontalSpace("H Control Gap");

        // Right column: 2D Slider
        GUILayout.BeginVertical(GUILayout.Width(120f));
        using (this.Box("2D Slider"))
        {
            _slider2D = this.Slider2D(_slider2D, new Vector2(-10f, -10f), new Vector2(10f, 10f),
                size: 100f, labelX: "X", labelY: "Y", defaultValue: Vector2.zero);
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    // ── Fill ────────────────────────────────────────────────────────────────

    void DrawSection_Fill()
    {
        GUILayout.BeginHorizontal();

        // Left: solid color fill
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (this.Box("Solid Fill"))
        {
            ZUI.Fill(_fillSolid, allowGradient: true);
        }
        GUILayout.EndVertical();

        this.HorizontalSpace("H Control Gap");

        // Right: gradient fill
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (this.Box("Gradient Fill"))
        {
            ZUI.Fill(_fillGradient, onOpenStopEditor: rect =>
            {
                // For the showcase, just log — no popup wired up
                Debug.Log($"[Showcase] Stop editor requested at {rect}");
            });
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    // ── Row ─────────────────────────────────────────────────────────────────

    void DrawSection_Row()
    {
        // Example 1: Zound-like browser row
        using (this.Box("Zound Browser Row (ZUI.HRow)"))
        {
            // Row 1: controls + name
            using (var row = ZUI.HRow())
            {
                row.Button(new GUIContent("E", "Edit"), "Toggle", GUILayout.Width(24f));
                row.Toggle(ref _rowMute, "M", "Toggle", GUILayout.Width(24f));
                row.Toggle(ref _rowSolo, "S", "Toggle", GUILayout.Width(24f));
                row.Flexible();
                row.Button("Knight Attack", "Default");
                row.Flexible();
                row.Button(new GUIContent("\u00d7", "Remove"), "Toggle", GUILayout.Width(24f));
            }

            this.VerticalSpace("V Control Gap");

            // Row 2: sliders + tag indicator
            using (var row = ZUI.HRow())
            {
                _rowVol = row.MicroSlider(_rowVol, 0f, 1f, "Vol", GUILayout.Width(80f));
                _rowPitch = row.MicroSlider(_rowPitch, 0.5f, 2f, "Pitch", GUILayout.Width(80f));
                row.Flexible();
                row.Label("Tags", GUILayout.Width(30f));
            }
        }

        this.VerticalSpace("V Control Gap");

        // Example 2: Simple toolbar
        using (this.Box("Toolbar (ZUI.HRow)"))
        {
            using (var row = ZUI.HRow())
            {
                row.Button("New", "Toggle");
                row.Button("Open", "Toggle");
                row.Button("Save", "Toggle");
                row.Flexible();
                row.Label("Ready");
            }
        }

        this.VerticalSpace("V Control Gap");

        // Example 3: compared to manual rects
        using (this.Box("Same layout, no rects, no manual x-tracking"))
        {
            EditorGUILayout.LabelField("The rows above use ZUI.HRow() — no rect math needed.", EditorStyles.wordWrappedMiniLabel);
        }
    }

    // ── Envelope ────────────────────────────────────────────────────────────

    void EnsureEnvelopeState()
    {
        if (_envDemoDef == null) _envDemoDef = new ZUIEnvelopeDef { name = "Showcase" };

        if (_envA == null)
        {
            _envA = new System.Collections.Generic.List<ZUIEnvelopePoint>
            {
                new ZUIEnvelopePoint(0f,    1f),
                new ZUIEnvelopePoint(0.25f, 0.6f),
                new ZUIEnvelopePoint(0.6f,  0.3f),
                new ZUIEnvelopePoint(1f,    0f),
            };
            _envARt = new ZUIEnvelopeRuntime { showGrid = true, showValueLabels = true };
        }
        if (_envB == null)
        {
            _envB = new System.Collections.Generic.List<ZUIEnvelopePoint>
            {
                new ZUIEnvelopePoint(0f,   0f),
                new ZUIEnvelopePoint(0.3f, 0.9f),
                new ZUIEnvelopePoint(0.7f, 0.4f),
                new ZUIEnvelopePoint(1f,   0.7f),
            };
            _envBRt = new ZUIEnvelopeRuntime
            {
                showGrid = true, loopEnabled = true,
                loopStart = 0.25f, loopEnd = 0.8f,
                anchorsLocked = true,
            };
        }
        if (_envC == null)
        {
            _envC = new System.Collections.Generic.List<ZUIEnvelopePoint>
            {
                new ZUIEnvelopePoint(0f,   0.2f, 1f, ZUIEnvelopeEditState.NotEditable),
                new ZUIEnvelopePoint(0.2f, 0.8f, 1f, ZUIEnvelopeEditState.YEditable),
                new ZUIEnvelopePoint(0.5f, 0.5f, 1f, ZUIEnvelopeEditState.Editable),
                new ZUIEnvelopePoint(0.8f, 0.7f, 1f, ZUIEnvelopeEditState.XEditable),
                new ZUIEnvelopePoint(1f,   0.3f, 1f, ZUIEnvelopeEditState.NotEditable),
            };
            _envCRt = new ZUIEnvelopeRuntime { showGrid = true };
        }
    }

    void DrawSection_Envelope()
    {
        EnsureEnvelopeState();

        using (this.Box("ZUI.Envelope — basic"))
        {
            this.Envelope(_envA, _envACurve, _envDemoDef, _envARt, 200f, 120f, 1);
            EditorGUILayout.LabelField("Hover curve to preview an add point • click to add • double-click point to remove • drag point to move • Shift+LMB line to move segment • Shift+RMB line/point to curve",
                                      EditorStyles.wordWrappedMiniLabel);
        }

        this.VerticalSpace("V Control Gap");

        using (this.Box("ZUI.Envelope — anchors locked + loop markers"))
        {
            this.Envelope(_envB, _envBCurve, _envDemoDef, _envBRt, 200f, 120f, 2);

            using (var row = ZUI.HRow())
            {
                _envBRt.anchorsLocked = row.Toggle(_envBRt.anchorsLocked, "Lock anchors");
                _envBRt.loopEnabled   = row.Toggle(_envBRt.loopEnabled,   "Loop");
                _envBRt.loopStart     = row.MicroSlider(_envBRt.loopStart, 0f, _envBRt.loopEnd, "Start", GUILayout.Width(120f));
                _envBRt.loopEnd       = row.MicroSlider(_envBRt.loopEnd,   _envBRt.loopStart, 1f, "End",   GUILayout.Width(120f));
                row.Flexible();
            }
            EditorGUILayout.LabelField("End caps render as NotEditable when Lock anchors is on. Drag the green vertical markers with LMB; hold RMB on either marker or inside the band to drag both together (loop gap preserved).",
                                      EditorStyles.wordWrappedMiniLabel);
        }

        this.VerticalSpace("V Control Gap");

        using (this.Box("ZUI.Envelope — mixed per-point edit states"))
        {
            this.Envelope(_envC, _envCCurve, _envDemoDef, _envCRt, 200f, 120f, 3);
            EditorGUILayout.LabelField("Points left→right: NotEditable, YEditable, Editable, XEditable, NotEditable. Each has its own handle visual.",
                                      EditorStyles.wordWrappedMiniLabel);
        }
    }

}
