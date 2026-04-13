using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zounds;

/// <summary>
/// Runtime build test UI for HH2 Zounds project.
/// Shows a volume slider, a grid of all configured Zounds (click to play),
/// and tracks which Zounds fail to produce audio.
/// Uses only the public Zounds API — no internal access.
/// </summary>
public class HH2BuildTest : MonoBehaviour {

    [Tooltip("Drag the HH2 Zounds JSON file here. In a build, falls back to StreamingAssets.")]
    public TextAsset zoundsProjectJson;

    private Vector2 zoundScrollPos;
    private Vector2 resultScrollPos;
    private float masterVolume = 1f;
    private string playResult = "";
    private float playResultTime;

    private List<ZoundInfo> allZounds = new List<ZoundInfo>();
    private bool initialized;

    private enum Tab { Zounds, TestAll }
    private Tab activeTab = Tab.Zounds;
    private GUIStyle richLabel;

    // Test state
    private bool testRunning;
    private int testIndex;
    private float testNextTime;
    private List<string> testResults = new List<string>();
    private int testPassed;
    private int testFailed;

    struct ZoundInfo {
        public string name;
        public Zound zound;
        public string type; // "Klip" or "Zeq"
    }

    void Start() {
        if (zoundsProjectJson != null) {
            ZoundEngine.Initialize(zoundsProjectJson);
        } else {
            // In a build, the pre-build hook copies the JSON to StreamingAssets
            ZoundEngine.Initialize();
        }
        initialized = ZoundEngine.IsInitialized();

        if (!initialized) {
            Debug.LogError("[HH2BuildTest] ZoundEngine failed to initialize.");
            return;
        }

        // Collect all top-level Zounds from the library
        var library = ZoundsProject.Instance.zoundLibrary;
        library.ForEachZound(z => {
            if (z.parentId != 0) return;
            if (z is Klip) {
                allZounds.Add(new ZoundInfo { name = z.name, zound = z, type = "K" });
            } else if (z is Zequence) {
                allZounds.Add(new ZoundInfo { name = z.name, zound = z, type = "Z" });
            }
        });
        allZounds = allZounds.OrderBy(z => z.name).ToList();

        Debug.Log($"[HH2BuildTest] {allZounds.Count} Zounds loaded.");
    }

    void Update() {
        if (testRunning && testIndex < allZounds.Count && Time.time >= testNextTime) {
            var info = allZounds[testIndex];
            var token = ZoundEngine.PlayZound(info.zound);
            if (token != null) {
                testResults.Add($"<color=#88FF88>OK</color>  [{info.type}] {info.name}");
                testPassed++;
            } else {
                testResults.Add($"<color=#FF6666>FAIL</color>  [{info.type}] {info.name}");
                testFailed++;
            }
            testIndex++;
            testNextTime = Time.time + 0.15f;

            if (testIndex >= allZounds.Count) {
                testRunning = false;
                testResults.Add("");
                testResults.Add($"Done: {testPassed} passed, {testFailed} failed out of {allZounds.Count}");
            }
        }
    }

    void OnGUI() {
        if (!initialized) {
            GUILayout.Label("ZoundEngine failed to initialize. Check console.", GUI.skin.box);
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        // Header
        GUILayout.Label($"HH2 Build Test — {allZounds.Count} Zounds", GUI.skin.box);
        GUILayout.Space(4);

        // Volume slider
        GUILayout.BeginHorizontal();
        GUILayout.Label("Volume:", GUILayout.Width(55));
        masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f, GUILayout.Width(200));
        GUILayout.Label($"{masterVolume:F2}", GUILayout.Width(40));
        AudioListener.volume = masterVolume;
        GUILayout.FlexibleSpace();

        // Stop all
        if (GUILayout.Button("Stop All", GUILayout.Width(70))) {
            ZoundEngine.StopAllZounds();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Play result
        if (!string.IsNullOrEmpty(playResult) && Time.time - playResultTime < 3f) {
            GUILayout.Label(playResult);
        }

        GUILayout.Space(4);

        // Tab bar
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(activeTab == Tab.Zounds, $"Zounds ({allZounds.Count})", GUI.skin.button))
            activeTab = Tab.Zounds;
        if (GUILayout.Toggle(activeTab == Tab.TestAll, "Test All", GUI.skin.button))
            activeTab = Tab.TestAll;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        switch (activeTab) {
            case Tab.Zounds: DrawZoundsGrid(); break;
            case Tab.TestAll: DrawTestAll(); break;
        }

        GUILayout.EndArea();
    }

    void DrawZoundsGrid() {
        zoundScrollPos = GUILayout.BeginScrollView(zoundScrollPos);

        int columns = Mathf.Max(1, (Screen.width - 40) / 170);
        int col = 0;
        GUILayout.BeginHorizontal();

        foreach (var info in allZounds) {
            if (GUILayout.Button($"[{info.type}] {info.name}", GUILayout.Width(160), GUILayout.Height(28))) {
                var token = ZoundEngine.PlayZound(info.zound);
                playResult = token != null ? $"Playing: {info.name}" : $"FAILED: {info.name} — no audio";
                playResultTime = Time.time;
            }

            col++;
            if (col >= columns) {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                col = 0;
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();
    }

    void DrawTestAll() {
        if (!testRunning && testResults.Count == 0) {
            GUILayout.Label("Plays every Zound one by one and reports which ones produce audio.");
            GUILayout.Space(8);
            if (GUILayout.Button("Run Test", GUILayout.Height(30), GUILayout.Width(120))) {
                testRunning = true;
                testIndex = 0;
                testNextTime = Time.time;
                testResults.Clear();
                testPassed = 0;
                testFailed = 0;
            }
        } else {
            if (testRunning) {
                GUILayout.Label($"Testing... {testIndex}/{allZounds.Count}");
            } else {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Results: {testPassed} passed, {testFailed} failed");
                if (GUILayout.Button("Run Again", GUILayout.Width(80))) {
                    testRunning = true;
                    testIndex = 0;
                    testNextTime = Time.time;
                    testResults.Clear();
                    testPassed = 0;
                    testFailed = 0;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);

            if (richLabel == null) {
                richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            }

            resultScrollPos = GUILayout.BeginScrollView(resultScrollPos);
            foreach (string line in testResults) {
                GUILayout.Label(line, richLabel);
            }
            GUILayout.EndScrollView();
        }
    }
}
