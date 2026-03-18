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
            //public bool showAudioClips = false;
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
            public string themesFolderPath => "Assets/ZoundsData/Themes";

            public EditorStyle editorStyle = new EditorStyle();

            public void ApplyTheme(ZoundsTheme theme) {
                editorStyle.playerHeadColor = theme.playerHeadColor;
                editorStyle.playerHeadThickness = theme.playerHeadThickness;
                editorStyle.klipWaveformBGColor = theme.klipWaveformBGColor;
                editorStyle.zequenceWaveformBGColor = theme.zequenceWaveformBGColor;
                editorStyle.volumeEnvelopeColor = theme.volumeEnvelopeColor;
                editorStyle.volumeEnvelopeThickness = theme.volumeEnvelopeThickness;
                editorStyle.pitchEnvelopeColor = theme.pitchEnvelopeColor;
                editorStyle.pitchEnvelopeThickness = theme.pitchEnvelopeThickness;
                editorStyle.trimHandleColor = theme.trimHandleColor;
                editorStyle.trimHandleThickness = theme.trimHandleThickness;
                editorStyle.waveformColor = theme.waveformColor;
                editorStyle.renderedWaveformColor = theme.renderedWaveformColor;
                editorStyle.renderedWaveformBGColor = theme.renderedWaveformBGColor;
                editorStyle.renderedPlayerHeadColor = theme.renderedPlayerHeadColor;
                editorStyle.trimAreaColor = theme.trimAreaColor;
                editorStyle.selectedEnvelopeLineColor = theme.selectedEnvelopeLineColor;
                editorStyle.selectedEnvelopeHandleColor = theme.selectedEnvelopeHandleColor;
            }

            public ZoundsTheme ExtractTheme() {
                return new ZoundsTheme {
                    playerHeadColor = editorStyle.playerHeadColor,
                    playerHeadThickness = editorStyle.playerHeadThickness,
                    klipWaveformBGColor = editorStyle.klipWaveformBGColor,
                    zequenceWaveformBGColor = editorStyle.zequenceWaveformBGColor,
                    volumeEnvelopeColor = editorStyle.volumeEnvelopeColor,
                    volumeEnvelopeThickness = editorStyle.volumeEnvelopeThickness,
                    pitchEnvelopeColor = editorStyle.pitchEnvelopeColor,
                    pitchEnvelopeThickness = editorStyle.pitchEnvelopeThickness,
                    trimHandleColor = editorStyle.trimHandleColor,
                    trimHandleThickness = editorStyle.trimHandleThickness,
                    waveformColor = editorStyle.waveformColor,
                    renderedWaveformColor = editorStyle.renderedWaveformColor,
                    renderedWaveformBGColor = editorStyle.renderedWaveformBGColor,
                    renderedPlayerHeadColor = editorStyle.renderedPlayerHeadColor,
                    trimAreaColor = editorStyle.trimAreaColor,
                    selectedEnvelopeLineColor = editorStyle.selectedEnvelopeLineColor,
                    selectedEnvelopeHandleColor = editorStyle.selectedEnvelopeHandleColor
                };
            }

            [System.Serializable]
            public class EditorStyle {
                public Color playerHeadColor = new Color(0.1f, 0.1f, 0.9f, 0.75f);
                public float playerHeadThickness = 1.5f;
                public Color klipWaveformBGColor = new Color32(252, 192, 7, 255);
                public Color zequenceWaveformBGColor = new Color32(172, 227, 222, 255);
                public Color volumeEnvelopeColor = new Color(0.1f, 0.7f, 0.1f);
                public float volumeEnvelopeThickness = 1.5f;
                public Color pitchEnvelopeColor = new Color(0.9f, 0.2f, 0.1f);
                public float pitchEnvelopeThickness = 1.5f;
                public Color trimHandleColor = Color.white;
                public float trimHandleThickness = 2.0f;
                public Color waveformColor = Color.black;
                public Color renderedWaveformColor = Color.black;
                public Color renderedWaveformBGColor = new Color32(200, 200, 200, 255);
                public Color renderedPlayerHeadColor = new Color(0.9f, 0.1f, 0.1f, 0.8f);
                public Color trimAreaColor = new Color(0f, 0f, 0f, 0.5f);
                public Color selectedEnvelopeLineColor = new Color(0.1f, 0.7f, 0.9f);
                public Color selectedEnvelopeHandleColor = new Color(0.1f, 0.75f, 0.85f);
                public bool autoRender = false;
                public bool alertOnClosing = true;
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

        /// <summary>
        /// Wipes all in-memory project data and resets the singleton to a clean default state.
        /// Call this whenever the project JSON reference is cleared or becomes invalid.
        /// </summary>
        public static void ResetToDefault() {
            var inst = Instance;
            inst.browserSettings = new BrowserSettings();
            inst.projectSettings = new ProjectSettings();
            inst.zoundLibrary = new ZoundLibrary();
            inst.zoundRoutings = new ZoundRoutings();
            isJSONLoaded = false;
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
            EnsureDirectoryExists(instance.projectSettings.themesFolderPath);
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