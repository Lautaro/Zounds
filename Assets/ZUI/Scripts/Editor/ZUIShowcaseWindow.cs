// ZUIShowcaseWindow.cs
// Demonstrates ZUI multi-selection controls and configurable label widths.
// Open via: Tools > ZUI > Control Showcase

using UnityEditor;
using UnityEngine;

public class ZUIShowcaseWindow : ZUIWindow
{
    protected override bool UseEditorSheet => true;

    [MenuItem("Tools/ZUI/Control Showcase")]
    static void Open() => GetWindow<ZUIShowcaseWindow>("ZUI Showcase");

    // ── State ────────────────────────────────────────────────────────────────
    Vector2 _scroll;

    int _cycleMode;
    int _cycleArrowMode;
    int _miniRadioH;
    int _miniRadioV;
    int _cycleShape;
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

    protected override void OnZUI()
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawSection_MultiSelect();
        ZUI.VerticalSpace("V Control Gap");
        DrawSection_LabelWidths();
        ZUI.VerticalSpace("V Control Gap");
        DrawSection_Forms();
        ZUI.VerticalSpace("V Control Gap");
        DrawSection_MixedControls();

        GUILayout.EndScrollView();
    }

    // ── Multi-Selection Controls ─────────────────────────────────────────────

    void DrawSection_MultiSelect()
    {
        using (ZUI.Box("Multi-Selection Controls"))
        {
            EditorGUILayout.LabelField("CycleButton", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            // CycleButton — basic
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mode", ZUI.LabelWide());
            _cycleMode = ZUI.CycleButton(_cycleMode, _modeLabels);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Section Rows");

            // CycleButton — more options
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shape", ZUI.LabelWide());
            _cycleShape = ZUI.CycleButton(_cycleShape, _shapeLabels);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Section Rows");

            // CycleButton — many options (shows stable width)
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ease Curve", ZUI.LabelWide());
            _cycleEase = ZUI.CycleButton(_cycleEase, _easeLabels);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");
            EditorGUILayout.LabelField("CycleArrows", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            // CycleArrows
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mode", ZUI.LabelWide());
            _cycleArrowMode = ZUI.CycleArrows(_cycleArrowMode, _modeLabels);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");
            EditorGUILayout.LabelField("MiniRadio (Horizontal)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            // MiniRadio horizontal
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quality", ZUI.LabelWide());
            _miniRadioH = ZUI.MiniRadio(_miniRadioH, _qualityLabels);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");
            EditorGUILayout.LabelField("MiniRadio (Vertical)", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Section Rows");

            // MiniRadio vertical
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quality", ZUI.LabelWide());
            _miniRadioV = ZUI.MiniRadioVertical(_miniRadioV, _qualityLabels);
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
}
