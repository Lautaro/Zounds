// MixdownWindow.cs
// Mock tool window to test multi-sheet ZUI coexistence alongside Zounds.

using UnityEditor;
using UnityEngine;

public class MixdownWindow : ZUIWindow
{
    [MenuItem("Tools/Open Mixdown")]
    static void Open() => GetWindow<MixdownWindow>("Mixdown");

    protected override string ConsumerSheetName => "Mixdown";

    // State
    int _tab;
    Vector2 _scroll;
    float _masterVol = 0.85f, _masterPan;
    float _ch1Vol = 0.7f, _ch1Pan = -0.3f;
    float _ch2Vol = 0.5f, _ch2Pan = 0.4f;
    float _ch3Vol = 0.9f, _ch3Pan;
    bool _ch1Mute, _ch2Mute, _ch3Mute;
    bool _ch1Solo, _ch2Solo, _ch3Solo;
    bool _reverbEnabled = true, _compEnabled;
    float _reverbMix = 0.3f, _reverbDecay = 1.5f;
    float _compThresh = -12f, _compRatio = 4f;
    string _projectName = "My Mixdown Project";
    bool _autoSave = true, _highQuality = true, _normalize;
    int _sampleRate = 1; // 0=44.1, 1=48, 2=96
    int _bitDepth;       // 0=16, 1=24, 2=32

    protected override void OnZUI()
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        // ═══════════════════════════════════════════════════════════════
        // Header
        // ═══════════════════════════════════════════════════════════════
        using (ZUI.Box("Mixdown Studio", ZUI.ZUIStyle.Default))
        {
            GUILayout.BeginHorizontal();
            _tab = GUILayout.Toolbar(_tab, new[] { "Mixer", "Effects", "Export", "Settings" }, EditorStyles.miniButton);
            GUILayout.EndHorizontal();
        }

        ZUI.VerticalSpace();

        // ═══════════════════════════════════════════════════════════════
        if (_tab == 0) DrawMixerTab();
        else if (_tab == 1) DrawEffectsTab();
        else if (_tab == 2) DrawExportTab();
        else DrawSettingsTab();

        GUILayout.EndScrollView();
    }

    // ─── Mixer Tab ──────────────────────────────────────────────────
    void DrawMixerTab()
    {
        // Master channel
        using (ZUI.Box("Master Bus", "Alternative"))
        {
            GUILayout.BeginHorizontal();
            ZUI.Label("Volume");
            _masterVol = EditorGUILayout.Slider(_masterVol, 0f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ZUI.Label("Pan");
            _masterPan = EditorGUILayout.Slider(_masterPan, -1f, 1f);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace(0.5f);

            // Master meter (fake)
            GUILayout.BeginHorizontal();
            ZUI.Label("Level", GUILayout.Width(40f));
            var meterRect = GUILayoutUtility.GetRect(0f, 12f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(meterRect, new Color(0.1f, 0.1f, 0.1f));
                float level = _masterVol * 0.8f;
                EditorGUI.DrawRect(new Rect(meterRect.x, meterRect.y, meterRect.width * level, meterRect.height),
                    level > 0.7f ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 0.9f, 0.4f));
            }
            GUILayout.EndHorizontal();
        }

        ZUI.VerticalSpace();

        // Channel strips
        DrawChannelStrip("Drums",  ref _ch1Vol, ref _ch1Pan, ref _ch1Mute, ref _ch1Solo);
        ZUI.VerticalSpace(0.5f);
        DrawChannelStrip("Bass",   ref _ch2Vol, ref _ch2Pan, ref _ch2Mute, ref _ch2Solo);
        ZUI.VerticalSpace(0.5f);
        DrawChannelStrip("Synth",  ref _ch3Vol, ref _ch3Pan, ref _ch3Mute, ref _ch3Solo);

        ZUI.VerticalSpace();

        // Transport
        using (ZUI.Box(null, "Subtle"))
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (ZUI.Button("⏮", ZUI.Style.RichButton, GUILayout.Width(32f))) { }
            if (ZUI.Button("▶ Play", ZUI.Style.RichButton, ZUI.Tint.Confirm, GUILayout.Width(80f))) Debug.Log("[Mixdown] Play");
            if (ZUI.Button("⏹ Stop", ZUI.Style.RichButton, ZUI.Tint.Danger,  GUILayout.Width(80f))) Debug.Log("[Mixdown] Stop");
            if (ZUI.Button("⏭", ZUI.Style.RichButton, GUILayout.Width(32f))) { }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    void DrawChannelStrip(string name, ref float vol, ref float pan, ref bool mute, ref bool solo)
    {
        using (ZUI.Box(name, ZUI.ZUIStyle.Default))
        {
            GUILayout.BeginHorizontal();

            // M/S toggles with grouped corners
            mute = ZUI.Toggle(mute, "M", ZUI.Style.RichButton, ZUI.Tint.Danger, ZUICornerMask.Left, GUILayout.Width(24f));
            solo = ZUI.Toggle(solo, "S", ZUI.Style.Active, ZUICornerMask.Right, GUILayout.Width(24f));

            GUILayout.Space(8f);
            ZUI.Label("Vol", GUILayout.Width(24f));
            vol = EditorGUILayout.Slider(vol, 0f, 1f);

            ZUI.Label("Pan", GUILayout.Width(24f));
            pan = EditorGUILayout.Slider(pan, -1f, 1f);

            GUILayout.EndHorizontal();
        }
    }

    // ─── Effects Tab ────────────────────────────────────────────────
    void DrawEffectsTab()
    {
        using (ZUI.Box("Reverb", "Info"))
        {
            GUILayout.BeginHorizontal();
            _reverbEnabled = ZUI.Toggle(_reverbEnabled, _reverbEnabled ? "ON" : "OFF",
                ZUI.Style.RichButton, _reverbEnabled ? ZUI.Tint.Confirm : null, GUILayout.Width(50f));
            ZUI.Label("Space Reverb", ZUI.ZTextStyle.Accent);
            GUILayout.EndHorizontal();

            if (_reverbEnabled)
            {
                ZUI.VerticalSpace(0.5f);
                GUILayout.BeginHorizontal();
                ZUI.Label("Mix", GUILayout.Width(50f));
                _reverbMix = EditorGUILayout.Slider(_reverbMix, 0f, 1f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                ZUI.Label("Decay", GUILayout.Width(50f));
                _reverbDecay = EditorGUILayout.Slider(_reverbDecay, 0.1f, 10f);
                GUILayout.EndHorizontal();
            }
        }

        ZUI.VerticalSpace();

        using (ZUI.Box("Compressor", "Warning"))
        {
            GUILayout.BeginHorizontal();
            _compEnabled = ZUI.Toggle(_compEnabled, _compEnabled ? "ON" : "OFF",
                ZUI.Style.RichButton, _compEnabled ? ZUI.Tint.Confirm : null, GUILayout.Width(50f));
            ZUI.Label("Bus Compressor", ZUI.ZTextStyle.Accent);
            GUILayout.EndHorizontal();

            if (_compEnabled)
            {
                ZUI.VerticalSpace(0.5f);
                GUILayout.BeginHorizontal();
                ZUI.Label("Threshold", GUILayout.Width(60f));
                _compThresh = EditorGUILayout.Slider(_compThresh, -40f, 0f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                ZUI.Label("Ratio", GUILayout.Width(60f));
                _compRatio = EditorGUILayout.Slider(_compRatio, 1f, 20f);
                GUILayout.EndHorizontal();
            }
        }

        ZUI.VerticalSpace();

        // Quick actions
        using (ZUI.Box(null, "Subtle"))
        {
            GUILayout.BeginHorizontal();
            if (ZUI.Button("Bypass All", ZUI.Style.RichButton, ZUI.Tint.Danger, GUILayout.Width(100f))) Debug.Log("[Mixdown] Bypass");
            if (ZUI.Button("Reset",      ZUI.Style.RichButton, ZUI.Tint.Danger, GUILayout.Width(60f)))
            {
                _reverbMix = 0.3f; _reverbDecay = 1.5f;
                _compThresh = -12f; _compRatio = 4f;
            }
            GUILayout.FlexibleSpace();
            ZUI.Label("2 effects loaded", ZUI.ZTextStyle.Subtle);
            GUILayout.EndHorizontal();
        }
    }

    // ─── Export Tab ──────────────────────────────────────────────────
    void DrawExportTab()
    {
        using (ZUI.Box("Export Settings", "Alternative"))
        {
            GUILayout.BeginHorizontal();
            ZUI.Label("Format", GUILayout.Width(70f));
            GUILayout.Toolbar(0, new[] { "WAV", "OGG", "MP3" }, EditorStyles.miniButton);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace(0.5f);

            GUILayout.BeginHorizontal();
            ZUI.Label("Sample Rate", GUILayout.Width(70f));
            _sampleRate = GUILayout.Toolbar(_sampleRate, new[] { "44.1k", "48k", "96k" }, EditorStyles.miniButton);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace(0.5f);

            GUILayout.BeginHorizontal();
            ZUI.Label("Bit Depth", GUILayout.Width(70f));
            _bitDepth = GUILayout.Toolbar(_bitDepth, new[] { "16-bit", "24-bit", "32-bit" }, EditorStyles.miniButton);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace(0.5f);

            GUILayout.BeginHorizontal();
            _normalize = ZUI.Toggle(_normalize, "Normalize", ZUI.Style.Default, GUILayout.Width(80f));
            _highQuality = ZUI.Toggle(_highQuality, "HQ", ZUI.Style.Active, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
        }

        ZUI.VerticalSpace();

        using (ZUI.Box("Output", ZUI.ZUIStyle.Default))
        {
            ZUI.Label("Ready to export");
            ZUI.Label($"Master: {_masterVol:P0} | Channels: 3 | Effects: {(_reverbEnabled ? 1 : 0) + (_compEnabled ? 1 : 0)}", ZUI.ZTextStyle.Small);

            ZUI.VerticalSpace();

            GUILayout.BeginHorizontal();
            if (ZUI.Button("Export", ZUI.Style.RichButton, ZUI.Tint.Confirm, GUILayout.Width(120f), GUILayout.Height(28f)))
                Debug.Log("[Mixdown] Exporting...");
            if (ZUI.Button("Export All", ZUI.Style.Alternative, GUILayout.Width(120f), GUILayout.Height(28f)))
                Debug.Log("[Mixdown] Exporting all...");
            GUILayout.EndHorizontal();
        }
    }

    // ─── Settings Tab ───────────────────────────────────────────────
    void DrawSettingsTab()
    {
        using (ZUI.Box("Project", "Alternative"))
        {
            GUILayout.BeginHorizontal();
            ZUI.Label("Name", GUILayout.Width(60f));
            _projectName = EditorGUILayout.TextField(_projectName);
            GUILayout.EndHorizontal();

            ZUI.VerticalSpace(0.5f);

            GUILayout.BeginHorizontal();
            _autoSave = ZUI.Toggle(_autoSave, "Auto Save", ZUI.Style.Default, GUILayout.Width(80f));
            GUILayout.EndHorizontal();
        }

        ZUI.VerticalSpace();

        using (ZUI.Box("System Info", "Subtle"))
        {
            ZUI.Label($"Active Sheet: {ZUI.ActiveSheet?.name ?? "null"}");
            ZUI.Label($"Consumer: Mixdown", ZUI.ZTextStyle.Subtle);

            ZUI.VerticalSpace(0.5f);

            ZUI.Label("Registered Sheets:", ZUI.ZTextStyle.Subheader);
            var names = ZUI.GetRegisteredConsumerNames();
            foreach (var n in names)
            {
                var sheet = ZUI.GetConsumerSheet(n);
                ZUI.Label($"  {n}: {(sheet != null ? sheet.name : "—")}", ZUI.ZTextStyle.Small);
            }
        }

        ZUI.VerticalSpace();

        using (ZUI.Box("Danger Zone", "Warning"))
        {
            ZUI.Label("These actions cannot be undone.", ZUI.ZTextStyle.Small);
            ZUI.VerticalSpace(0.5f);
            GUILayout.BeginHorizontal();
            if (ZUI.Button("Reset All Settings", ZUI.Style.RichButton, ZUI.Tint.Danger, GUILayout.Width(140f)))
                Debug.Log("[Mixdown] Reset!");
            if (ZUI.Button("Clear Cache",        ZUI.Style.RichButton, ZUI.Tint.Danger, GUILayout.Width(100f)))
                Debug.Log("[Mixdown] Cache cleared");
            GUILayout.EndHorizontal();
        }
    }
}
