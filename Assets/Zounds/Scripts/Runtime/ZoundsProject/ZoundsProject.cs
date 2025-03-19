using UnityEngine;

namespace Zounds {

    public class ZoundsProject : ScriptableObject {

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
        }

        [System.Serializable]
        public class ProjectSettings {
            public float playerVolume           = 1f;
            public float systemVolumeModifier   = 1f;
            public float editorVolume           = 1f;
            public string systemFolderPath  = "Assets/ZoundsData/SystemFiles";
            public string userFolderPath    = "Assets/ZoundsData/UserFiles";
            public string sourceFolderPath  = "Assets/ZoundsData/SourceFiles";

            public float cooldownDuration = 0.1f;
            public int maxPlayedZoundInstances = 10;
        }

        private static ZoundsProject instance;

        public static ZoundsProject Instance {
            get {
                if (instance == null) {
                    instance = Resources.Load<ZoundsProject>("ZoundsProject");
                    if (instance == null) {
                        instance = CreateInstance<ZoundsProject>();
#if UNITY_EDITOR
                        GenerateDefaultFiles();
#endif
                        Debug.Log("ZoundsProject has been created.", instance);
                    }
                }
                return instance;
            }
        }

#if UNITY_EDITOR
        private static void GenerateDefaultFiles() {
            EnsureDirectoryExists(instance.projectSettings.systemFolderPath);
            EnsureDirectoryExists(instance.projectSettings.systemFolderPath + "/WorkFiles");
            EnsureDirectoryExists(instance.projectSettings.systemFolderPath + "/Resources");
            EnsureDirectoryExists(instance.projectSettings.userFolderPath);
            EnsureDirectoryExists(instance.projectSettings.sourceFolderPath);
            UnityEditor.AssetDatabase.CreateAsset(instance, instance.projectSettings.systemFolderPath + "/Resources/ZoundsProject.asset");

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

    }

}