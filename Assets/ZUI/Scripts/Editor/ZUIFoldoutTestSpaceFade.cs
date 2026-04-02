// ZUIFoldoutTestSpaceFade.cs
// Test window for the Space+Fade AnimatedFoldout implementation.
// Uses the same AnimatedFoldout from ZUIAnimation.cs that BrowserTab uses.
//
// Open via: Tools / ZUI Foldout Test - Space+Fade

using UnityEditor;
using UnityEngine;

public class ZUIFoldoutTestSpaceFade : ZUIWindow
{
    [MenuItem("Tools/ZUI Foldout Test - Space+Fade")]
    public static void Open() => GetWindow<ZUIFoldoutTestSpaceFade>("Foldout: Space+Fade");

    private bool _isOpen = false;
    private ZUI.AnimatedFoldout _foldout = new ZUI.AnimatedFoldout("spacefade_test");
    private Vector2 _scroll;

    // Toggle states for the ZUI controls
    private bool _togA, _togB, _togC, _togD, _togE, _togF;

    protected override void OnZUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(8f);

        // ── Reproduce the BrowserTab structure: foldout inside ZUI.Box ──
        using (ZUI.Box())
        {
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                ZUI.Label("Space+Fade Test", ZUI.ZTextStyle.Title);
                GUILayout.FlexibleSpace();
                if (ZUI.Button(_isOpen ? "Close" : "Open", ZUI.Style.Default, GUILayout.Width(80f)))
                    _isOpen = !_isOpen;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6f);

            using (var fold = _foldout.Begin(_isOpen))
            {
                if (fold.visible)
                {
                    DrawFoldoutContent();
                }
            }
        }

        GUILayout.Space(8f);

        // Content below to verify push-down
        using (ZUI.Box("Content Below"))
        {
            EditorGUILayout.LabelField("This content should be pushed down by the foldout above.");
            EditorGUILayout.LabelField($"State: {_foldout.state}  Height: {_foldout.contentHeight:F0}");

            GUILayout.Space(4f);
            float spaceMs = _foldout.spaceDuration * 1000f;
            float fadeMs  = _foldout.fadeDuration * 1000f;
            spaceMs = EditorGUILayout.Slider("Space (ms)", spaceMs, 0f, 250f);
            fadeMs  = EditorGUILayout.Slider("Fade (ms)",  fadeMs,  0f, 250f);
            _foldout.spaceDuration = spaceMs / 1000f;
            _foldout.fadeDuration  = fadeMs / 1000f;
        }

        GUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawFoldoutContent()
    {
        DrawSectionHeader("Display Options");
        GUILayout.BeginHorizontal();
        {
            _togA = ZUI.Toggle(_togA, "Vol",  ZUI.Style.RichToggle, ZUICornerMask.Left,  GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
            ZUI.HorizontalSpace("H Btns Medium");
            _togB = ZUI.Toggle(_togB, "Pit",  ZUI.Style.RichToggle, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
            ZUI.HorizontalSpace("H Btns Medium");
            _togC = ZUI.Toggle(_togC, "Cha",  ZUI.Style.RichToggle, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
            ZUI.HorizontalSpace("H Btns Medium");
            _togD = ZUI.Toggle(_togD, "Name", ZUI.Style.RichToggle, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
            ZUI.HorizontalSpace("H Btns Medium");
            _togE = ZUI.Toggle(_togE, "Tags", ZUI.Style.RichToggle, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
            ZUI.HorizontalSpace("H Btns Medium");
            _togF = ZUI.Toggle(_togF, "Mute", ZUI.Style.RichToggle, ZUICornerMask.Right, GUILayout.Height(18f), GUILayout.MinWidth(28f), GUILayout.MaxWidth(65f));
        }
        GUILayout.EndHorizontal();
        ZUI.RowSpace();

        DrawSectionHeader("Actions");
        GUILayout.BeginHorizontal();
        {
            ZUI.Button("Action A", ZUI.Style.Confirm, ZUICornerMask.Left, GUILayout.Width(80f));
            ZUI.Button("Action B", ZUI.Style.Danger,  GUILayout.Width(80f));
            ZUI.Button("Action C", ZUI.Style.Subtle,  ZUICornerMask.Right, GUILayout.Width(80f));
        }
        GUILayout.EndHorizontal();
        ZUI.RowSpace();

        EditorGUILayout.LabelField("End of foldout content.");
    }

    private static void DrawSectionHeader(string label)
    {
        GUILayout.Space(5f);
        ZUI.Label(label, ZUI.ZTextStyle.Subheader);
        GUILayout.Space(2f);
    }
}
