// ZUIShowcase2Window.cs
// A mixed-UI mockup: standard Unity IMGUI interleaved with ZUI controls.
// Open via: Tools / ZUI Showcase 2

using UnityEditor;
using UnityEngine;

public class ZUIShowcase2Window : ZUIWindow
{
    [MenuItem("Tools/ZUI Showcase 2")]
    public static void Open() => GetWindow<ZUIShowcase2Window>("ZUI Showcase 2");

    private void OnEnable() => wantsMouseMove = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private Vector2 _scroll;
    private string  _searchText  = "";
    private bool    _showAdvanced;
    private float   _threshold   = 0.5f;
    private int     _selectedMode = 1;
    private bool    _autoRefresh  = true;
    private string  _statusMsg   = "Ready";
    private Color   _tintColor   = Color.white;

    private readonly string[] _modeNames = { "Fast", "Balanced", "Quality" };

    // ── GUI ───────────────────────────────────────────────────────────────────

    protected override void OnZUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(8f);

        DrawToolbar();
        GUILayout.Space(6f);
        DrawMainContent();
        GUILayout.Space(6f);
        DrawStatusBar();

        GUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
    }

    // ── Toolbar — standard Unity IMGUI ────────────────────────────────────────

    void DrawToolbar()
    {
        // Plain IMGUI toolbar — no ZUI
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Asset Processor", EditorStyles.boldLabel, GUILayout.Width(120f));
        GUILayout.FlexibleSpace();
        _searchText = GUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(160f));
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            _searchText = "";
        GUILayout.EndHorizontal();
    }

    // ── Main content — mix of ZUI boxes/buttons and plain IMGUI ──────────────

    void DrawMainContent()
    {
        GUILayout.BeginHorizontal();

        // Left column: ZUI box containing standard IMGUI fields
        DrawSettingsColumn();

        GUILayout.Space(6f);

        // Right column: standard Group containing ZUI buttons
        DrawActionsColumn();

        GUILayout.EndHorizontal();
    }

    void DrawSettingsColumn()
    {
        // ZUI Box wrapping regular IMGUI controls
        using (var box = ZUI.Box("Settings", ZUI.ZUIStyle.Default))
        {
            EditorGUILayout.LabelField("Processing Mode");
            _selectedMode = GUILayout.SelectionGrid(_selectedMode, _modeNames, 3, EditorStyles.miniButton);

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Threshold");
            _threshold = EditorGUILayout.Slider(_threshold, 0f, 1f);

            GUILayout.Space(4f);
            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
            _tintColor   = EditorGUILayout.ColorField("Tint", _tintColor);

            GUILayout.Space(6f);

            // ZUI button inside a ZUI box
            if (ZUI.Button("Apply Settings", ZUI.Style.Confirm))
                _statusMsg = "Settings applied.";

            GUILayout.Space(2f);

            // Standard Unity button inside a ZUI box
            if (GUILayout.Button("Reset to Defaults", EditorStyles.miniButton))
            {
                _threshold    = 0.5f;
                _selectedMode = 1;
                _autoRefresh  = true;
                _statusMsg    = "Reset to defaults.";
            }
        }
    }

    void DrawActionsColumn()
    {
        GUILayout.BeginVertical();

        // Standard IMGUI group label — NOT a ZUI box
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        GUILayout.Space(2f);

        // ZUI buttons inside a plain vertical layout
        GUILayout.BeginHorizontal();
        if (ZUI.Button("Scan",    ZUI.Style.Default)) _statusMsg = "Scanning assets...";
        if (ZUI.Button("Process", ZUI.Style.Active))  _statusMsg = "Processing...";
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (ZUI.Button("Export",  ZUI.Style.Confirm)) _statusMsg = "Exporting...";
        if (ZUI.Button("Delete",  ZUI.Style.Danger))  _statusMsg = "Deleted.";
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);

        // ZUI info box containing plain IMGUI fields
        using (ZUI.Box("Advanced", ZUI.ZUIStyle.Info))
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Options");
            if (_showAdvanced)
            {
                EditorGUILayout.IntField("Batch Size", 64);
                EditorGUILayout.Toggle("Dry Run", false);
                GUILayout.Space(2f);
                // ZUI button inside ZUI box
                if (ZUI.Button("Run Advanced", ZUI.Style.Alternative))
                    _statusMsg = "Advanced run started.";
            }
        }

        GUILayout.Space(6f);

        // Warning box — a ZUI box with no ZUI buttons inside
        using (ZUI.Box("Caution", ZUI.ZUIStyle.Warning))
        {
            EditorGUILayout.LabelField("This will modify imported assets.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            // One ZUI, one standard
            if (ZUI.Button("Confirm", ZUI.Style.Danger))
                _statusMsg = "Confirmed destructive action.";
            if (GUILayout.Button("Cancel", EditorStyles.miniButton))
                _statusMsg = "Cancelled.";
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    // ── Status bar — standard IMGUI ───────────────────────────────────────────

    void DrawStatusBar()
    {
        // Plain EditorGUI status strip — no ZUI
        var rect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));
        EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 60f, 14f),
            _statusMsg, EditorStyles.miniLabel);
        if (GUI.Button(new Rect(rect.xMax - 54f, rect.y + 3f, 50f, 16f), "Clear Log", EditorStyles.miniButton))
            _statusMsg = "—";
    }



}
