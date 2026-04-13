#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateHH2TestScene {

    [MenuItem("Zounds/Create HH2 Test Scene")]
    public static void Create() {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var go = new GameObject("HH2 Build Test");
        var test = go.AddComponent<HH2BuildTest>();

        // Assign the HH2 Zounds JSON
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/HH2/HH2 Zounds.json");
        if (json != null) {
            test.zoundsProjectJson = json;
        } else {
            Debug.LogWarning("[Zounds] Could not find Assets/HH2/HH2 Zounds.json — assign manually in the inspector.");
        }

        // Ensure AudioListener exists
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<AudioListener>() == null) {
            cam.gameObject.AddComponent<AudioListener>();
        }

        string path = "Assets/HH2/Test Scene/HH2TestScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();
        Debug.Log($"[Zounds] Created HH2 test scene at {path}");

        // Select the test object so the JSON field is visible in inspector
        Selection.activeGameObject = go;
    }
}
#endif
