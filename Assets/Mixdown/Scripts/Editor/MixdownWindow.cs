// MixdownWindow.cs
// Mock tool window to test multi-sheet ZUI coexistence alongside Zounds.

using UnityEditor;
using UnityEngine;

public class MixdownWindow : ZUIWindow
{
    [MenuItem("Tools/Open Mixdown")]
    static void Open() => GetWindow<MixdownWindow>("Mixdown");

    protected override bool UseEditorSheet => false;
    protected override string ConsumerSheetName => "Mixdown";

    float _slider1 = 0.5f, _slider2 = 0.75f;
    int _tab;
    Vector2 _scroll;
    string _textField = "Hello Mixdown";

    protected override void OnZUI()
    {
        using (ZUI.Box("Mixdown Tool", ZUI.ZUIStyle.Default))
        {
            // Tab bar
            GUILayout.BeginHorizontal();
            _tab = GUILayout.Toolbar(_tab, new[] { "Mix", "Master", "Export" }, EditorStyles.miniButton);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace();

            _scroll = GUILayout.BeginScrollView(_scroll);

            // Section 1: Channel Strip
            using (ZUI.Box("Channel Strip", ZUI.ZUIStyle.Alternative))
            {
                GUILayout.BeginHorizontal();
                ZUI.Label("Volume", GUILayout.Width(60f));
                _slider1 = EditorGUILayout.Slider(_slider1, 0f, 1f);
                GUILayout.EndHorizontal();

                ZUI.VerticalSpace(0.5f);

                GUILayout.BeginHorizontal();
                ZUI.Label("Pan", GUILayout.Width(60f));
                _slider2 = EditorGUILayout.Slider(_slider2, -1f, 1f);
                GUILayout.EndHorizontal();

                ZUI.VerticalSpace(0.5f);

                GUILayout.BeginHorizontal();
                ZUI.Label("Name", GUILayout.Width(60f));
                _textField = EditorGUILayout.TextField(_textField);
                GUILayout.EndHorizontal();
            }

            ZUI.VerticalSpace();

            // Section 2: Actions
            using (ZUI.Box("Actions", ZUI.ZUIStyle.Default))
            {
                GUILayout.BeginHorizontal();
                if (ZUI.Button("Render", ZUI.Style.Confirm)) Debug.Log("[Mixdown] Render clicked");
                if (ZUI.Button("Cancel", ZUI.Style.Danger))  Debug.Log("[Mixdown] Cancel clicked");
                if (ZUI.Button("Settings", ZUI.Style.Default)) Debug.Log("[Mixdown] Settings clicked");
                GUILayout.EndHorizontal();
            }

            ZUI.VerticalSpace();

            // Section 3: Info
            using (ZUI.Box("Output", ZUI.ZUIStyle.Alternative))
            {
                ZUI.Label("Status: Ready");
                ZUI.Label($"Volume: {_slider1:P0}  Pan: {_slider2:F2}");
                ZUI.Label($"Active Sheet: {ZUI.ActiveSheet?.name ?? "null"}");

                ZUI.VerticalSpace(0.5f);

                ZUI.Label("Registered consumer sheets:");
                var names = ZUI.GetRegisteredConsumerNames();
                foreach (var name in names)
                {
                    var sheet = ZUI.GetConsumerSheet(name);
                    ZUI.Label($"  {name}: {(sheet != null ? sheet.name : "—")}");
                }
            }

            GUILayout.EndScrollView();
        }
    }
}
