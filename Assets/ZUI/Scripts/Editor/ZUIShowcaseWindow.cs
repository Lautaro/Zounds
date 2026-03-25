// ZUIShowcaseWindow.cs
// Open via:  Tools / ZUI Showcase
// Styles are live-driven by the active ZUIStyleSheetAsset.
// Open Tools / ZUI Style Editor alongside this window to see changes in real time.

using UnityEditor;
using UnityEngine;

public class ZUIShowcaseWindow : ZUIWindow
{
    [MenuItem("Tools/ZUI Showcase")]
    public static void Open() => GetWindow<ZUIShowcaseWindow>("ZUI Showcase");

    // ── State ─────────────────────────────────────────────────────────────────

    private Vector2 _scroll;
    private string  _lastPressed = "—";
    private bool    _toggleA;
    private bool    _toggleB     = true;
    private bool    _guiEnabled  = true;

    // ── OnZUI ─────────────────────────────────────────────────────────────────

    protected override void OnZUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(8f);

        DrawButtonSection();
        GUILayout.Space(6f);
        DrawBoxSection();
        GUILayout.Space(6f);
        DrawCompositionSection();
        GUILayout.Space(8f);

        EditorGUILayout.EndScrollView();
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    void DrawButtonSection()
    {
        SectionHeader("Buttons");

        using (ZUI.Box("All Styles"))
        {
            GUILayout.BeginHorizontal();
            if (ZUI.Button("Default", ZUI.ZButtonStyle.Default)) Pressed("Default");
            if (ZUI.Button("Confirm", ZUI.ZButtonStyle.Confirm)) Pressed("Confirm");
            if (ZUI.Button("Danger",  ZUI.ZButtonStyle.Danger))  Pressed("Danger");
            if (ZUI.Button("Subtle",  ZUI.ZButtonStyle.Subtle))  Pressed("Subtle");
            if (ZUI.Button("Active",  ZUI.ZButtonStyle.Active))  Pressed("Active");
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);

        using (ZUI.Box("Toggle Pattern"))
        {
            GUILayout.BeginHorizontal();
            var styleA = _toggleA ? ZUI.ZButtonStyle.Active : ZUI.ZButtonStyle.Default;
            if (ZUI.Button("Option A", styleA, GUILayout.Width(80f)))
                { _toggleA = !_toggleA; Pressed($"Toggle A → {_toggleA}"); }
            var styleB = _toggleB ? ZUI.ZButtonStyle.Active : ZUI.ZButtonStyle.Default;
            if (ZUI.Button("Option B", styleB, GUILayout.Width(80f)))
                { _toggleB = !_toggleB; Pressed($"Toggle B → {_toggleB}"); }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);

        using (ZUI.Box("Disabled"))
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enabled:", GUILayout.Width(55f));
            _guiEnabled = EditorGUILayout.Toggle(_guiEnabled, GUILayout.Width(16f));
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            var prev = GUI.enabled;
            GUI.enabled = _guiEnabled;
            GUILayout.BeginHorizontal();
            if (ZUI.Button("Default", ZUI.ZButtonStyle.Default)) Pressed("Default");
            if (ZUI.Button("Confirm", ZUI.ZButtonStyle.Confirm)) Pressed("Confirm");
            if (ZUI.Button("Danger",  ZUI.ZButtonStyle.Danger))  Pressed("Danger");
            GUILayout.EndHorizontal();
            GUI.enabled = prev;
        }

        GUILayout.Space(4f);

        using (ZUI.Box("Last Pressed", ZUI.ZUIStyle.Subtle))
        {
            EditorGUILayout.LabelField(_lastPressed, EditorStyles.boldLabel);
        }
    }

    // ── Boxes ─────────────────────────────────────────────────────────────────

    void DrawBoxSection()
    {
        SectionHeader("Boxes");

        GUILayout.BeginHorizontal();
        using (ZUI.Box("Default",     ZUI.ZUIStyle.Default))     { PlaceholderContent(); }
        using (ZUI.Box("Alternative", ZUI.ZUIStyle.Alternative)) { PlaceholderContent(); }
        using (ZUI.Box("Warning",     ZUI.ZUIStyle.Warning))     { PlaceholderContent(); }
        using (ZUI.Box("Subtle",      ZUI.ZUIStyle.Subtle))      { PlaceholderContent(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);

        using (ZUI.Box("Nesting", ZUI.ZUIStyle.Default))
        {
            GUILayout.BeginHorizontal();
            using (ZUI.Box("Inner A", ZUI.ZUIStyle.Subtle))
            {
                EditorGUILayout.LabelField("Content A");
                ZUI.Button("Action", ZUI.ZButtonStyle.Confirm);
            }
            using (ZUI.Box("Inner B", ZUI.ZUIStyle.Alternative))
            {
                EditorGUILayout.LabelField("Content B");
                ZUI.Button("Remove", ZUI.ZButtonStyle.Danger);
            }
            GUILayout.EndHorizontal();
        }
    }

    // ── Composition ───────────────────────────────────────────────────────────

    void DrawCompositionSection()
    {
        SectionHeader("Composition");

        using (ZUI.Box("Toolbar", ZUI.ZUIStyle.Default))
        {
            GUILayout.BeginHorizontal();
            if (ZUI.Button("Render", ZUI.ZButtonStyle.Confirm, GUILayout.Width(65f))) Pressed("Render");
            if (ZUI.Button("Remove", ZUI.ZButtonStyle.Danger,  GUILayout.Width(65f))) Pressed("Remove");
            GUILayout.FlexibleSpace();
            if (ZUI.Button("Select", ZUI.ZButtonStyle.Subtle, GUILayout.Width(65f))) Pressed("Select");
            if (ZUI.Button("Play",   ZUI.ZButtonStyle.Active, GUILayout.Width(65f))) Pressed("Play");
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);

        using (ZUI.Box("Rect-based buttons"))
        {
            var area = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
            float w = 80f, pad = 6f;
            var r = new Rect(area.x, area.y + 4f, w, 20f);
            if (ZUI.Button(r, "Default", ZUI.ZButtonStyle.Default)) Pressed("Rect Default");
            r.x += w + pad;
            if (ZUI.Button(r, "Confirm", ZUI.ZButtonStyle.Confirm)) Pressed("Rect Confirm");
            r.x += w + pad;
            if (ZUI.Button(r, "Danger",  ZUI.ZButtonStyle.Danger))  Pressed("Rect Danger");
        }

        GUILayout.Space(4f);

        var boxRect = GUILayoutUtility.GetRect(1f, 52f, GUILayout.ExpandWidth(true));
        using (ZUI.AreaBox(boxRect, "AreaBox", ZUI.ZUIStyle.Alternative))
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Fixed-rect area box", GUILayout.ExpandWidth(true));
            ZUI.Button("Ok",     ZUI.ZButtonStyle.Confirm, GUILayout.Width(50f));
            ZUI.Button("Cancel", ZUI.ZButtonStyle.Subtle,  GUILayout.Width(55f));
            GUILayout.EndHorizontal();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Pressed(string label) { _lastPressed = label; Repaint(); }

    static void SectionHeader(string title)
    {
        var rect = GUILayoutUtility.GetRect(1f, EditorGUIUtility.singleLineHeight + 2f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.12f, .12f, .14f, 1f));
        EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y + 1f, rect.width, rect.height),
            title, EditorStyles.boldLabel);
        GUILayout.Space(4f);
    }

    static void PlaceholderContent()
    {
        EditorGUILayout.LabelField("Label", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Label", EditorStyles.miniLabel);
    }
}
