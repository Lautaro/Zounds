using UnityEngine;

namespace Zounds {
    internal class ZoundsTempData : ScriptableObject {

#if UNITY_EDITOR
        private static ZoundsTempData instance;
        public static ZoundsTempData Instance {
            get {
                if (instance == null) {
                    instance = Resources.Load<ZoundsTempData>("ZoundsTempData");
                    if (instance == null) {
                        var systemPath = ZoundsProject.Instance.projectSettings.systemFolderPath;
                        EnsureFolderPathExists(systemPath + "/Resources");
                        instance = CreateInstance<ZoundsTempData>();
                        UnityEditor.AssetDatabase.CreateAsset(instance, systemPath + "/Resources/ZoundsTempData.asset");
                    }
                }
                return instance;
            }
        }

        private static void EnsureFolderPathExists(string folderPath) {
            if (string.IsNullOrEmpty(folderPath))
                return;

            folderPath = folderPath.Replace("\\", "/");

            string[] parts = folderPath.Split('/');
            string currentPath = "";

            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i];
                string parentPath = string.IsNullOrEmpty(currentPath) ? "Assets" : currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;

                if (!UnityEditor.AssetDatabase.IsValidFolder(currentPath)) {
                    UnityEditor.AssetDatabase.CreateFolder(parentPath, part);
                }
            }
        }

        [HideInInspector] public string preservedJSONProject;
        [HideInInspector] public bool zoundsProjectDirty;
#endif

    }
}
