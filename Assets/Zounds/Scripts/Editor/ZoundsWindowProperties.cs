using System.Collections.Generic;
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

        public static void DirtyAll() {
            foreach (var tabProperty in Instance.zoundTabProperties) {
                tabProperty.dirty = true;
            }
        }

        [System.Serializable]
        public class ZoundTabProperties {

            public enum GroupBy {
                None, Tags, References,
#if ZOUNDS_CONSIDER_FOLDERS
                Folder
#endif
            }

            public string searchText;
            public List<string> selectedFolders = new List<string>();
            public List<string> selectedTags = new List<string>();
            public List<int> selectedReferences = new List<int>(); // zound ids
            public GroupBy groupBy = GroupBy.None;

            public bool dirty { get; set; } = false;

            public void ClearFilters() {
                searchText = "";
                selectedFolders.Clear();
                selectedTags.Clear();
                selectedReferences.Clear();
            }

        }

    }

}
