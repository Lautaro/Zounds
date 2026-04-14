// ZUIAutoColorTestWindow.cs
// Test window for the autocolor palette system.
// Creates its own stylesheet with base colors and autocolors for visual testing.
// Open via: Tools > ZUI > Autocolor Test

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class ZUIAutoColorTestWindow : ZUIWindow
{
    const string k_ConsumerName = "AutocolorTest";
    const string k_SheetPath = "Assets/ZUI/SystemAssets/ZUIAutoColorTestSheet.asset";

    protected override string ConsumerSheetName => k_ConsumerName;

    [MenuItem("Tools/ZUI/Autocolor Test")]
    static void Open() => GetWindow<ZUIAutoColorTestWindow>("Autocolor Test");

    Vector2 _scroll;
    bool _toggleA = true;
    bool _toggleB;
    int _radioSel;
    float _sliderVal = 0.5f;

    protected override void OnZUIEnable()
    {
        EnsureSheet();
    }

    protected override void OnZUI()
    {
        var sheet = ZUI.GetConsumerSheet(k_ConsumerName);
        if (sheet == null)
        {
            EditorGUILayout.HelpBox("Autocolor Test sheet not found. Close and reopen this window.", MessageType.Warning);
            if (GUILayout.Button("Create Sheet"))
                EnsureSheet();
            return;
        }

        _scroll = GUILayout.BeginScrollView(_scroll);

        // Header
        EditorGUILayout.LabelField("Autocolor Test Window", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Visual test bed for the autocolor palette system.", EditorStyles.wordWrappedMiniLabel);
        ZUI.VerticalSpace("V Section Rows");

        // ── Palette info ──────────────────────────────────────────────────────
        using (ZUI.BoxNamed("Default"))
        {
            EditorGUILayout.LabelField("Palette Entries", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Control Gap");

            if (sheet.palette != null)
            {
                foreach (var entry in sheet.palette)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.name, EditorStyles.miniLabel, GUILayout.Width(80f));

                    // Base color swatch
                    var baseRect = GUILayoutUtility.GetRect(24f, 16f, GUILayout.Width(24f));
                    if (Event.current.type == EventType.Repaint)
                        EditorGUI.DrawRect(baseRect, entry.color);

                    // Autocolor swatches
                    if (entry.autoColors != null)
                    {
                        GUILayout.Space(4f);
                        foreach (var ac in entry.autoColors)
                        {
                            Color resolved = ac.Resolve(entry.color);
                            var acRect = GUILayoutUtility.GetRect(20f, 16f, GUILayout.Width(20f));
                            if (Event.current.type == EventType.Repaint)
                            {
                                EditorGUI.DrawRect(acRect, resolved);
                                // Tooltip-style name
                            }
                            GUILayout.Space(1f);
                        }
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
        }

        ZUI.VerticalSpace("V Section Rows");

        // ── Buttons ───────────────────────────────────────────────────────────
        using (ZUI.BoxNamed("Default"))
        {
            EditorGUILayout.LabelField("Buttons", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Control Gap");

            GUILayout.BeginHorizontal();
            ZUI.Button("Primary", "Default");
            ZUI.Button("Action", "Default");
            ZUI.Button("Subtle", "Default");
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");

            GUILayout.BeginHorizontal();
            _toggleA = ZUI.Toggle(_toggleA, "Toggle On", "Toggle");
            _toggleB = ZUI.Toggle(_toggleB, "Toggle Off", "Toggle");
            GUILayout.EndHorizontal();
        }

        ZUI.VerticalSpace("V Section Rows");

        // ── Boxes ─────────────────────────────────────────────────────────────
        using (ZUI.BoxNamed("Default"))
        {
            EditorGUILayout.LabelField("Nested Boxes", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Control Gap");

            using (ZUI.BoxNamed("Default"))
            {
                EditorGUILayout.LabelField("Level 1 — Default box");

                using (ZUI.BoxNamed("Default"))
                {
                    EditorGUILayout.LabelField("Level 2 — Nested");
                    ZUI.VerticalSpace("V Control Gap");

                    GUILayout.BeginHorizontal();
                    ZUI.Button("Nested Button", "Default");
                    _toggleA = ZUI.Toggle(_toggleA, "Nested Toggle", "Toggle");
                    GUILayout.EndHorizontal();
                }
            }
        }

        ZUI.VerticalSpace("V Section Rows");

        // ── Sliders ───────────────────────────────────────────────────────────
        using (ZUI.BoxNamed("Default"))
        {
            EditorGUILayout.LabelField("Sliders", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Control Gap");
            _sliderVal = ZUI.Slider(_sliderVal, 0f, 1f, "Value", "Default");
        }

        ZUI.VerticalSpace("V Section Rows");

        // ── Autocolor resolution test ─────────────────────────────────────────
        using (ZUI.BoxNamed("Default"))
        {
            EditorGUILayout.LabelField("Autocolor Resolution Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Shows all autocolor-derived colors from each palette entry.", EditorStyles.wordWrappedMiniLabel);
            ZUI.VerticalSpace("V Control Gap");

            if (sheet.palette != null)
            {
                foreach (var entry in sheet.palette)
                {
                    if (entry.autoColors == null || entry.autoColors.Count == 0)
                        continue;

                    EditorGUILayout.LabelField(entry.name, EditorStyles.miniLabel);
                    GUILayout.BeginHorizontal();

                    // Base swatch
                    GUILayout.BeginVertical(GUILayout.Width(40f));
                    var bRect = GUILayoutUtility.GetRect(36f, 36f, GUILayout.Width(36f), GUILayout.Height(36f));
                    if (Event.current.type == EventType.Repaint)
                        EditorGUI.DrawRect(bRect, entry.color);
                    EditorGUILayout.LabelField("Base", EditorStyles.miniLabel, GUILayout.Width(36f));
                    GUILayout.EndVertical();

                    GUILayout.Space(4f);

                    // Autocolor swatches
                    foreach (var ac in entry.autoColors)
                    {
                        Color resolved = ac.Resolve(entry.color);
                        GUILayout.BeginVertical(GUILayout.Width(40f));
                        var acRect = GUILayoutUtility.GetRect(36f, 36f, GUILayout.Width(36f), GUILayout.Height(36f));
                        if (Event.current.type == EventType.Repaint)
                            EditorGUI.DrawRect(acRect, resolved);
                        var nameStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            fontSize = 8,
                            clipping = TextClipping.Clip,
                        };
                        EditorGUILayout.LabelField(ac.name, nameStyle, GUILayout.Width(36f));
                        GUILayout.EndVertical();
                        GUILayout.Space(2f);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    ZUI.VerticalSpace("V Control Gap");
                }
            }
        }

        GUILayout.EndScrollView();
    }

    // ── Sheet creation ───────────────────────────────────────────────────────

    void EnsureSheet()
    {
        // Check if already registered
        if (ZUI.GetConsumerSheet(k_ConsumerName) != null)
            return;

        // Check if asset exists on disk
        var existing = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(k_SheetPath);
        if (existing != null)
        {
            ZUI.RegisterConsumerSheet(k_ConsumerName, existing);
            return;
        }

        // Create sheet asset
        var sheet = ScriptableObject.CreateInstance<ZUIStyleSheetAsset>();
        sheet.consumerName = k_ConsumerName;

        // Seed with base palette colors and autocolors
        sheet.palette = new List<ZUIPaletteColor>();

        // Base color 1: Surface (dark blue-grey)
        var surface = new ZUIPaletteColor
        {
            name = "Surface",
            color = new Color(0.15f, 0.18f, 0.25f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Light",  hueMod = 0f,    satMod = -0.3f, valMod = 0.4f },
                new ZUIAutoColor { name = "Dark",   hueMod = 0f,    satMod = 0.1f,  valMod = -0.5f },
                new ZUIAutoColor { name = "Muted",  hueMod = 0f,    satMod = -0.6f, valMod = 0.1f },
            },
        };

        // Base color 2: Accent (teal)
        var accent = new ZUIPaletteColor
        {
            name = "Accent",
            color = new Color(0.2f, 0.7f, 0.65f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Bright", hueMod = 0.05f,  satMod = 0.3f,  valMod = 0.3f },
                new ZUIAutoColor { name = "Dim",    hueMod = -0.05f, satMod = -0.4f, valMod = -0.3f },
                new ZUIAutoColor { name = "Warm",   hueMod = -0.3f,  satMod = 0f,    valMod = 0f },
            },
        };

        // Base color 3: Text (off-white)
        var text = new ZUIPaletteColor
        {
            name = "Text",
            color = new Color(0.9f, 0.9f, 0.92f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Secondary", hueMod = 0f, satMod = 0f, valMod = -0.35f },
                new ZUIAutoColor { name = "Disabled",  hueMod = 0f, satMod = 0f, valMod = -0.55f },
            },
        };

        sheet.palette.Add(surface);
        sheet.palette.Add(accent);
        sheet.palette.Add(text);

        // Add default styles — Toggle and TabButton fall back to Default if missing
        var btn = new ZUIButtonDef();
        btn.name = "Default";
        var toggle = new ZUIButtonDef();
        toggle.name = "Toggle";
        var box = new ZUIBoxDef();
        box.name = "Default";
        var slider = new ZUISliderDef();
        slider.name = "Default";

        sheet.buttons = new List<ZUIButtonDef> { btn, toggle };
        sheet.boxes = new List<ZUIBoxDef> { box };
        sheet.sliders = new List<ZUISliderDef> { slider };
        sheet.textStyles = new List<ZUITextStyleDef>();

        // Ensure directory exists
        var dir = System.IO.Path.GetDirectoryName(k_SheetPath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        AssetDatabase.CreateAsset(sheet, k_SheetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ZUI.RegisterConsumerSheet(k_ConsumerName, sheet);
        Debug.Log($"[ZUI] Created Autocolor Test sheet at {k_SheetPath}");
    }
}
