// ZUIShowcaseWindow.cs
// Demonstrates ZUI multi-selection controls and configurable label widths.
// Open via: Tools > ZUI > Control Showcase

using UnityEditor;
using UnityEngine;

public class ZUIShowcaseWindow : ZUIWindow
{
    protected override string ConsumerSheetName => "Showcase";

    const string k_SheetPath = "Assets/ZUI/SystemAssets/ZUIShowcaseSheet.asset";

    [MenuItem("Tools/ZUI/Zhowcase")]
    static void Open() => GetWindow<ZUIShowcaseWindow>("Zhowcase");

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
    ZUIColorRef _pickerCustom = new ZUIColorRef(new Color(0.3f, 0.6f, 1f));
    ZUIColorRef _pickerPalette = new ZUIColorRef(Color.cyan, "EditorAccent", ZUIPaletteSlot.Primary);
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

    protected override void OnZUI()
    {
        string[] tabs = { "Buttons", "Sliders", "Color", "Layout", "Forms", "Fill", "Row" };
        _showcaseTab = ZUI.MiniRadio(_showcaseTab, tabs, "TabButton");
        ZUI.VerticalSpace("V Control Gap");

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
                ZUI.HorizontalSpace("H Control Gap");
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
        using (ZUI.Box("Toggle"))
        {
            GUILayout.BeginHorizontal();
            _toggleA = ZUI.Toggle(_toggleA, "Feature A", "Toggle");
            ZUI.HorizontalSpace("H Control Gap");
            _toggleB = ZUI.Toggle(_toggleB, "Feature B", "Toggle");
            GUILayout.EndHorizontal();
        }
        ZUI.VerticalSpace("V Control Gap");
        using (ZUI.Box("CycleButton"))
        {
            _cycleMode = ZUI.CycleButton(_cycleMode, _modeLabels, "Toggle", GUILayout.Width(cycleW));
        }
        ZUI.VerticalSpace("V Control Gap");
        using (ZUI.Box("CycleArrows"))
        {
            _cycleArrowMode = ZUI.CycleArrows(_cycleArrowMode, _modeLabels, "Toggle", "", GUILayout.Width(arrowW));
        }
        GUILayout.EndVertical();

        ZUI.HorizontalSpace("H Control Gap");

        // Right column
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (ZUI.Box("MiniRadio"))
        {
            _miniRadioH1 = ZUI.MiniRadio(_miniRadioH1, _qualityLabels, "Toggle", shaped: true);
        }
        ZUI.VerticalSpace("V Control Gap");
        using (ZUI.Box("MiniRadio (Vertical)"))
        {
            _miniRadioV = ZUI.MiniRadioVertical(_miniRadioV, _qualityLabels, "Toggle", shaped: true);
        }
        ZUI.VerticalSpace("V Control Gap");
        using (ZUI.Box("MicroRadio"))
        {
            _microRadioSel = ZUI.MicroRadio(_microRadioSel, _microRadioLabels, "Toggle", wrap: true);
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    // ── New Controls ────────────────────────────────────────────────────────

    void DrawSection_NewControls()
    {
        using (ZUI.Box("ZUI.ColorPicker"))
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.MaxWidth(280f));
            EditorGUILayout.LabelField("Custom color", EditorStyles.miniLabel);
            ZUI.ColorPicker(ref _pickerCustom);
            GUILayout.EndVertical();

            ZUI.HorizontalSpace("H Control Gap");

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
        using (ZUI.Box("Configurable Label Widths"))
        {
            EditorGUILayout.LabelField("Wide labels (descriptive)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Corner Radius", ZUI.RowLabelStyle, ZUI.LabelWide());
            _sliderA = ZUI.Slider(_sliderA, 0f, 1f, "", "SmallSlider");
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Track Height", ZUI.RowLabelStyle, ZUI.LabelWide());
            _sliderB = ZUI.Slider(_sliderB, 0f, 360f, "", "SmallSlider");
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");
            EditorGUILayout.LabelField("Narrow labels (compact rows)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ang", ZUI.RowLabelStyle, ZUI.LabelNarrow());
            _sliderB = ZUI.Slider(_sliderB, 0f, 360f, "", "SmallSlider");
            ZUI.HorizontalSpace("H Control Gap");
            EditorGUILayout.LabelField("Crv", ZUI.RowLabelStyle, ZUI.LabelNarrow());
            _sliderC = ZUI.Slider(_sliderC, 0f, 1f, "", "SmallSlider");
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
            GUILayout.BeginHorizontal();

            // Left: labeled rows + conditional
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            using (ZUI.Box("Labeled Rows"))
            {
                var enabled = ZUI.Toggle(() => _formEnabled, v => _formEnabled = v);
                var blur    = ZUI.Slider(() => _formBlur, v => _formBlur = v, 0f, 20f);
                var passes  = ZUI.IntSlider(() => _formPasses, v => _formPasses = v, 1, 20);

                var form = ZUI.Form();
                form.Add("Enabled", enabled);
                form.Add("Blur Radius", blur);
                if (_formEnabled)
                    form.Add("Passes", passes);
                form.Draw();
            }
            GUILayout.EndVertical();

            ZUI.HorizontalSpace("H Control Gap");

            // Right: multi-control row
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            using (ZUI.Box("Multi-Control Row"))
            {
                var xField = ZUI.FloatField(() => _formOffsetX, v => _formOffsetX = v, 60f);
                var yField = ZUI.FloatField(() => _formOffsetY, v => _formOffsetY = v, 60f);
                var blend  = ZUI.CycleButton(() => _formBlendMode, v => _formBlendMode = v, _modeLabels);

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
        GUILayout.BeginHorizontal();

        // Left column: Standard + MicroSlider
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (ZUI.Box("Standard"))
        {
            _sliderB = ZUI.Slider(_sliderB, 0f, 360f, "Angle", "SmallSlider");
        }
        ZUI.VerticalSpace("V Control Gap");
        using (ZUI.Box("MicroSlider"))
        {
            _microSliderA = ZUI.MicroSlider(_microSliderA, 0f, 100f, "Volume", ZUI.SliderStyle.Default, false, null, GUILayout.Width(120f));
        }
        ZUI.VerticalSpace("V Control Gap");
        using (ZUI.Box("Stacked"))
        {
            _stackedVal = ZUI.SliderStacked(_stackedVal, 0f, 24f, "Radius", "SmallSlider");
        }
        GUILayout.EndVertical();

        ZUI.HorizontalSpace("H Control Gap");

        // Right column: 2D Slider
        GUILayout.BeginVertical(GUILayout.Width(120f));
        using (ZUI.Box("2D Slider"))
        {
            _slider2D = ZUI.Slider2D(_slider2D, new Vector2(-10f, -10f), new Vector2(10f, 10f),
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
        using (ZUI.Box("Solid Fill"))
        {
            ZUI.Fill(_fillSolid, allowGradient: true);
        }
        GUILayout.EndVertical();

        ZUI.HorizontalSpace("H Control Gap");

        // Right: gradient fill
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        using (ZUI.Box("Gradient Fill"))
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
        using (ZUI.Box("Zound Browser Row (ZUI.HRow)"))
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

            ZUI.VerticalSpace("V Control Gap");

            // Row 2: sliders + tag indicator
            using (var row = ZUI.HRow())
            {
                _rowVol = row.MicroSlider(_rowVol, 0f, 1f, "Vol", GUILayout.Width(80f));
                _rowPitch = row.MicroSlider(_rowPitch, 0.5f, 2f, "Pitch", GUILayout.Width(80f));
                row.Flexible();
                row.Label("Tags", GUILayout.Width(30f));
            }
        }

        ZUI.VerticalSpace("V Control Gap");

        // Example 2: Simple toolbar
        using (ZUI.Box("Toolbar (ZUI.HRow)"))
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

        ZUI.VerticalSpace("V Control Gap");

        // Example 3: compared to manual rects
        using (ZUI.Box("Same layout, no rects, no manual x-tracking"))
        {
            EditorGUILayout.LabelField("The rows above use ZUI.HRow() — no rect math needed.", EditorStyles.wordWrappedMiniLabel);
        }
    }

}
