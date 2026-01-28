using UnityEngine;

namespace Zounds {
    public class ZoundsTempData : ScriptableObject {

        private static ZoundsTempData instance;
        public static ZoundsTempData Instance {
            get {
                if (instance == null) {
                    instance = Resources.Load<ZoundsTempData>("ZoundsTempData");
                    if (instance == null) {
                        Debug.Log("Create");
                        instance = CreateInstance<ZoundsTempData>();
                        UnityEditor.AssetDatabase.CreateAsset(instance, ZoundsProject.Instance.projectSettings.systemFolderPath + "/Resources/ZoundsTempData.asset");
                    }
                }
                return instance;
            }
        }

        [HideInInspector] public string preservedJSONProject;
        [HideInInspector] public bool zoundsProjectDirty;

    }
}
