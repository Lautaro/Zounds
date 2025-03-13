using UnityEngine;

namespace Zounds {

    public class ZoundsWindowProperties : ScriptableObject {

        private static ZoundsWindowProperties instance;
        public static ZoundsWindowProperties Instance {
            get {
                if (instance == null) {
                    instance = Resources.Load<ZoundsWindowProperties>("ZoundsWindowProperties");
                }
                return instance;
            }
        }

        [HideInInspector] public int selectedMainTab = 0;
        [HideInInspector] public int selectedZoundTab = 0;
        [HideInInspector] public ZoundTabProperties[] zoundTabProperties = new ZoundTabProperties[4]{
            new ZoundTabProperties(),
            new ZoundTabProperties(),
            new ZoundTabProperties(),
            new ZoundTabProperties(),
        };

        [System.Serializable]
        public class ZoundTabProperties {
            public string searchText;
        }

    }

}
