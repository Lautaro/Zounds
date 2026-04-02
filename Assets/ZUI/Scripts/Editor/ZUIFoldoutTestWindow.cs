// ZUIFoldoutTestWindow.cs
// Test window for developing the foldout/clipping animation system.
// Open via:  Tools / ZUI Foldout Test
//
// Two tests:
//   1. Manual slider — colored bands whose visible height is controlled
//      by a slider. Tests that GUI.BeginGroup clipping works.
//   2. Animated toggle — a box that opens/closes with height animation.

using UnityEditor;
using UnityEngine;

public class ZUIFoldoutTestWindow : ZUIWindow
{
    [MenuItem("Tools/ZUI Foldout Test")]
    public static void Open() => GetWindow<ZUIFoldoutTestWindow>("ZUI Foldout Test");

    // ── Test 1: Manual slider ────────────────────────────────────────────────
    private float _sliderHeight = 100f;

    // ── Test 2: Animated toggle ──────────────────────────────────────────────
    private bool _isOpen = false;
    private ZUI.AnimatedFoldout2 _foldout = new ZUI.AnimatedFoldout2("test_foldout", 1.0f); // 1 second duration

    // ── Scroll ───────────────────────────────────────────────────────────────
    private Vector2 _scroll;

    protected override void OnZUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(8f);

        DrawTest1_ManualSlider();
        GUILayout.Space(16f);
        DrawTest2_AnimatedToggle();
        GUILayout.Space(16f);

        // A label below to verify layout push
        using (ZUI.Box("Content Below"))
        {
            EditorGUILayout.LabelField("This content should be pushed down by the boxes above.");
            EditorGUILayout.LabelField("If clipping works, nothing overlaps this.");
        }

        GUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
    }

    // =====================================================================
    // TEST 1: Manual slider controls visible height of colored bands
    // =====================================================================

    void DrawTest1_ManualSlider()
    {
        SectionHeader("Test 1: Manual Slider Clipping");

        _sliderHeight = EditorGUILayout.Slider("Visible Height", _sliderHeight, 0f, 200f);
        GUILayout.Space(4f);

        // Reserve exactly _sliderHeight in the layout
        Rect clipRect = GUILayoutUtility.GetRect(0f, _sliderHeight, GUILayout.ExpandWidth(true));

        // Draw a border so the reserved area is visible
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(clipRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Clip group: everything inside is clipped to clipRect
            GUI.BeginGroup(clipRect);

            // Draw colored bands at fixed positions (total 200px tall)
            // These are immediate-mode draws, no GUILayout involved
            float bandH = 40f;
            Color[] colors = {
                new Color(0.8f, 0.2f, 0.2f, 1f),  // red
                new Color(0.2f, 0.8f, 0.2f, 1f),  // green
                new Color(0.2f, 0.2f, 0.8f, 1f),  // blue
                new Color(0.8f, 0.8f, 0.2f, 1f),  // yellow
                new Color(0.8f, 0.2f, 0.8f, 1f),  // magenta
            };
            for (int i = 0; i < colors.Length; i++)
            {
                Rect bandRect = new Rect(0f, i * bandH, clipRect.width, bandH);
                EditorGUI.DrawRect(bandRect, colors[i]);

                // Label in each band
                var labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
                GUI.Label(new Rect(10f, i * bandH + 10f, 200f, 20f), $"Band {i + 1} (y={i * bandH}–{(i + 1) * bandH})", labelStyle);
            }

            GUI.EndGroup();
        }
    }

    // =====================================================================
    // TEST 2: Animated toggle with measure-first approach
    // =====================================================================

    void DrawTest2_AnimatedToggle()
    {
        SectionHeader("Test 2: Animated Foldout");

        EditorGUILayout.BeginHorizontal();
        if (ZUI.Button(_isOpen ? "Close" : "Open", ZUI.Style.Default, GUILayout.Width(80f)))
            _isOpen = !_isOpen;
        EditorGUILayout.LabelField($"  state: {_foldout.state}  contentH: {_foldout.contentHeight:F0}  t: {_foldout.AnimT:F2}");
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4f);

        using (var fold = _foldout.Begin(_isOpen))
        {
            if (fold.visible)
            {
                DrawSampleContent("Animated Box");
            }
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    static void DrawSampleContent(string title)
    {
        using (ZUI.Box(title))
        {
            EditorGUILayout.LabelField("Line 1: Some settings here");
            EditorGUILayout.LabelField("Line 2: More controls");

            GUILayout.BeginHorizontal();
            ZUI.Button("Action A", ZUI.Style.Confirm, GUILayout.Width(80f));
            ZUI.Button("Action B", ZUI.Style.Danger, GUILayout.Width(80f));
            ZUI.Button("Action C", ZUI.Style.Subtle, GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Line 3: Toggle options");

            GUILayout.BeginHorizontal();
            ZUI.Button("Opt 1", ZUI.Style.Default, GUILayout.Width(60f));
            ZUI.Button("Opt 2", ZUI.Style.Default, GUILayout.Width(60f));
            ZUI.Button("Opt 3", ZUI.Style.Default, GUILayout.Width(60f));
            ZUI.Button("Opt 4", ZUI.Style.Default, GUILayout.Width(60f));
            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Line 4: End of content");
        }
    }

    static void SectionHeader(string title)
    {
        var rect = GUILayoutUtility.GetRect(1f, EditorGUIUtility.singleLineHeight + 2f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.12f, .12f, .14f, 1f));
        EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y + 1f, rect.width, rect.height),
            title, EditorStyles.boldLabel);
        GUILayout.Space(4f);
    }
}
