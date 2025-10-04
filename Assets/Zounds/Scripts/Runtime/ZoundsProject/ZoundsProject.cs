using UnityEngine;

namespace Zounds {

    public class ZoundsProject : ScriptableObject {

        public static bool useJSON = true;
        internal static bool isJSONLoaded = false;

        public BrowserSettings browserSettings = new BrowserSettings();
        public ProjectSettings projectSettings = new ProjectSettings();
        public ZoundLibrary zoundLibrary = new ZoundLibrary();
        public ZoundRoutings zoundRoutings = new ZoundRoutings();

        [System.Serializable]
        public class BrowserSettings {
            public bool multicolumn = false;
            public bool showVolume = true;
            public bool showPitch = true;
            public bool showChance = true;
            public float itemWidth = 300f;
            public bool showNameField = true;
            public bool showTags = true;
            public bool killOnPlay = false;
            public bool showAudioClips = false;
            public bool msOnly = false; // only show either muted or solo
        }

        [System.Serializable]
        public class ProjectSettings {
            public float playerVolume = 1f;
            public float systemVolumeModifier = 1f;
            public float editorVolume = 1f;
            public string systemFolderPath = "Assets/ZoundsData/SystemFiles";
            public string userFolderPath = "Assets/ZoundsData/UserFiles";
            public string sourceFolderPath = "Assets/ZoundsData/SourceFiles";

            public float cooldownDuration = 0.1f;
            public int maxPlayedZoundInstances = 10;
            public float cullFadeDuration = 0.4f;

            public string workFolderPath => systemFolderPath + "/WorkFiles";

            public EditorStyle editorStyle = new EditorStyle();

            [System.Serializable]
            public class EditorStyle {
                public Color playerHeadColor = new Color(0.1f, 0.1f, 0.9f, 0.75f);
                public Color klipWaveformBGColor = new Color32(252, 192, 7, 255);
                public Color zequenceWaveformBGColor = new Color32(172, 227, 222, 255);
                public Color volumeEnvelopeColor = new Color(0.1f, 0.7f, 0.1f);
                public Color pitchEnvelopeColor = new Color(0.9f, 0.2f, 0.1f);
                public Color selectedEnvelopeLineColor = new Color(0.1f, 0.7f, 0.9f);
                public Color selectedEnvelopeHandleColor = new Color(0.1f, 0.75f, 0.85f);
            }

        }

        private static ZoundsProject instance;

        public static ZoundsProject Instance {
            get {
                if (instance == null) {
                    if (useJSON) {
                        instance = CreateInstance<ZoundsProject>();
                        instance.hideFlags = HideFlags.DontSave;
                    }
                    else {
                        instance = Resources.Load<ZoundsProject>("ZoundsProject");
                        if (instance == null) {
                            instance = CreateInstance<ZoundsProject>();
#if UNITY_EDITOR
                            GenerateDefaultFiles();
#endif
                            Debug.Log("ZoundsProject has been created.", instance);
                        }
                    }
                }
                return instance;
            }
        }

        public static void LoadFromJSON(TextAsset jsonTextAsset) {
            LoadFromJSON(jsonTextAsset.text);
        }

        public static void LoadFromJSON(string jsonContent) {
            ProjectSerializer deserialized;
            try {
                deserialized = JsonUtility.FromJson<ProjectSerializer>(jsonContent);
            }
            catch {
                Debug.LogError("Invalid Json Content: " + jsonContent);
                deserialized = null;
            }
            if (deserialized == null) return;
            var inst = Instance;
            inst.browserSettings = deserialized.browserSettings;
            inst.projectSettings = deserialized.projectSettings;
            inst.zoundLibrary = deserialized.zoundLibrary;
            inst.zoundRoutings = deserialized.zoundRoutings;
#if UNITY_EDITOR
            GenerateDefaultFiles();
#endif
            isJSONLoaded = true;
        }

#if UNITY_EDITOR
        internal static void GenerateDefaultFiles() {
            EnsureDirectoryExists(instance.projectSettings.systemFolderPath);
            EnsureDirectoryExists(instance.projectSettings.workFolderPath);
            EnsureDirectoryExists(instance.projectSettings.systemFolderPath + "/Resources");
            EnsureDirectoryExists(instance.projectSettings.userFolderPath);
            EnsureDirectoryExists(instance.projectSettings.sourceFolderPath);

            if (!useJSON) {
                UnityEditor.AssetDatabase.CreateAsset(instance, instance.projectSettings.systemFolderPath + "/Resources/ZoundsProject.asset");
            }

            UnityEditor.AssetDatabase.Refresh();
        }

        public static void EnsureDirectoryExists(string path) {
            if (!path.StartsWith("Assets") && !path.StartsWith("Packages")) {
                Debug.LogError("Path must start with 'Assets' or 'Packages'.");
                return;
            }

            string[] folders = path.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++) {
                string newPath = $"{currentPath}/{folders[i]}";

                if (!UnityEditor.AssetDatabase.IsValidFolder(newPath)) {
                    UnityEditor.AssetDatabase.CreateFolder(currentPath, folders[i]);
                }

                currentPath = newPath;
            }
        }
#endif

        [System.Serializable]
        internal class ProjectSerializer {
            public BrowserSettings browserSettings = new BrowserSettings();
            public ProjectSettings projectSettings = new ProjectSettings();
            public ZoundLibrary zoundLibrary = new ZoundLibrary();
            public ZoundRoutings zoundRoutings = new ZoundRoutings();
        }
    }

}