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
    protected override string RootBoxStyle => "Window";

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
        using (ZUI.BoxNamed("Card"))
        {
            EditorGUILayout.LabelField("Buttons", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Control Gap");

            GUILayout.BeginHorizontal();
            ZUI.Button("Default", "Default");
            ZUI.Button("Action", "Action");
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace("V Control Gap");

            GUILayout.BeginHorizontal();
            _toggleA = ZUI.Toggle(_toggleA, "Toggle On", "Toggle");
            _toggleB = ZUI.Toggle(_toggleB, "Toggle Off", "Toggle");
            GUILayout.EndHorizontal();
        }

        ZUI.VerticalSpace("V Section Rows");

        // ── Cards ─────────────────────────────────────────────────────────────
        using (ZUI.BoxNamed("Card"))
        {
            EditorGUILayout.LabelField("Card Box", EditorStyles.boldLabel);
            ZUI.VerticalSpace("V Control Gap");

            using (ZUI.BoxNamed("Inset"))
            {
                EditorGUILayout.LabelField("Inset box inside Card");
                ZUI.VerticalSpace("V Control Gap");

                GUILayout.BeginHorizontal();
                ZUI.Button("Inset Button", "Default");
                ZUI.Button("Inset Action", "Action");
                GUILayout.EndHorizontal();
            }

            ZUI.VerticalSpace("V Control Gap");

            using (ZUI.BoxNamed("Inset"))
            {
                EditorGUILayout.LabelField("Another inset section");
                _toggleA = ZUI.Toggle(_toggleA, "Inset Toggle", "Toggle");
            }
        }

        ZUI.VerticalSpace("V Section Rows");

        // ── Sliders ───────────────────────────────────────────────────────────
        using (ZUI.BoxNamed("Card"))
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

        // Base color 1: Surface (deep navy)
        var surface = new ZUIPaletteColor
        {
            name = "Surface",
            color = new Color(0.12f, 0.14f, 0.22f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Raised",  hueMod = 0f,    satMod = -0.2f, valMod = 0.25f },
                new ZUIAutoColor { name = "Sunken",  hueMod = 0f,    satMod = 0.15f, valMod = -0.4f },
                new ZUIAutoColor { name = "Border",  hueMod = 0f,    satMod = -0.3f, valMod = 0.5f },
                new ZUIAutoColor { name = "Hover",   hueMod = 0.05f, satMod = 0.1f,  valMod = 0.35f },
            },
        };

        // Base color 2: Accent (warm amber-gold)
        var accent = new ZUIPaletteColor
        {
            name = "Accent",
            color = new Color(0.9f, 0.65f, 0.2f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Bright", hueMod = 0.05f,  satMod = 0.2f,  valMod = 0.3f },
                new ZUIAutoColor { name = "Deep",   hueMod = -0.05f, satMod = 0.2f,  valMod = -0.4f },
                new ZUIAutoColor { name = "Soft",   hueMod = 0.1f,   satMod = -0.5f, valMod = 0.15f },
            },
        };

        // Base color 3: Accent 2 (cool teal)
        var accent2 = new ZUIPaletteColor
        {
            name = "Cool",
            color = new Color(0.2f, 0.65f, 0.7f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Bright", hueMod = 0.05f,  satMod = 0.3f,  valMod = 0.3f },
                new ZUIAutoColor { name = "Dim",    hueMod = -0.05f, satMod = -0.3f, valMod = -0.35f },
            },
        };

        // Base color 4: Text (warm off-white)
        var text = new ZUIPaletteColor
        {
            name = "Text",
            color = new Color(0.92f, 0.9f, 0.85f, 1f),
            autoColors = new List<ZUIAutoColor>
            {
                new ZUIAutoColor { name = "Secondary", hueMod = 0f, satMod = 0f, valMod = -0.3f },
                new ZUIAutoColor { name = "Disabled",  hueMod = 0f, satMod = 0f, valMod = -0.55f },
                new ZUIAutoColor { name = "Inverse",   hueMod = 0f, satMod = 0f, valMod = -0.85f },
            },
        };

        sheet.palette.Add(surface);
        sheet.palette.Add(accent);
        sheet.palette.Add(accent2);
        sheet.palette.Add(text);

        // Styles
        var btn = new ZUIButtonDef();
        btn.name = "Default";
        var btnAction = new ZUIButtonDef();
        btnAction.name = "Action";
        var toggle = new ZUIButtonDef();
        toggle.name = "Toggle";

        var boxDefault = new ZUIBoxDef();
        boxDefault.name = "Default";
        var boxCard = new ZUIBoxDef();
        boxCard.name = "Card";
        var boxInset = new ZUIBoxDef();
        boxInset.name = "Inset";
        var boxWindow = new ZUIBoxDef();
        boxWindow.name = "Window";

        var slider = new ZUISliderDef();
        slider.name = "Default";

        sheet.buttons = new List<ZUIButtonDef> { btn, btnAction, toggle };
        sheet.boxes = new List<ZUIBoxDef> { boxDefault, boxCard, boxInset, boxWindow };
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
