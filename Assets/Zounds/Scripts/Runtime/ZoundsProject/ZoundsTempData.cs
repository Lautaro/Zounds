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
                        instance = CreateInstance<ZoundsTempData>();
                        UnityEditor.AssetDatabase.CreateAsset(instance, ZoundsProject.Instance.projectSettings.systemFolderPath + "/Resources/ZoundsTempData.asset");
                    }
                }
                return instance;
            }
        }

        [HideInInspector] public string preservedJSONProject;
        [HideInInspector] public bool zoundsProjectDirty;
#endif

    }
}
