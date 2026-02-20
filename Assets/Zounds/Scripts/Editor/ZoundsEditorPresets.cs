using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Zounds.ZoundsWindowProperties.ZoundTabProperties;

namespace Zounds {
    public class ZoundsEditorPresets : ScriptableObject {

        private static ZoundsEditorPresets instance;
        public static ZoundsEditorPresets Instance {
            get {
                if (instance == null) {
                    instance = Resources.Load<ZoundsEditorPresets>("ZoundsEditorPresets");
                    if (instance == null) {
                        var systemPath = ZoundsProject.Instance.projectSettings.systemFolderPath;
                        EnsureFolderPathExists(systemPath + "/Resources");
                        instance = CreateInstance<ZoundsEditorPresets>();
                        UnityEditor.AssetDatabase.CreateAsset(instance, systemPath + "/Resources/ZoundsEditorPresets.asset");
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

        internal void ApplyDefaultView() {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[0];
            Undo.RecordObject(ZoundsWindowProperties.Instance, "apply preset");
            ZoundsWindow.ModifyZoundsProject("apply preset", () => {
                browserSettings.multicolumn = false;
                browserSettings.showVolume = true;
                browserSettings.showPitch = true;
                browserSettings.showChance = true;
                browserSettings.itemWidth = 300f;
                browserSettings.showNameField = true;
                browserSettings.showTags = true;
            });
            zoundTabProperties.selectedTypes = ZoundType.None;
            zoundTabProperties.selectedFolders.Clear();
            zoundTabProperties.selectedTags.Clear();
            zoundTabProperties.selectedReferences.Clear();
            zoundTabProperties.groupBy = GroupBy.None;
            EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
            ZoundsWindow.RepaintWindow();
        }

        public List<ViewPreset> viewPresets = new List<ViewPreset>();
        public List<NameListPreset> typesPresets = new List<NameListPreset>();
        public List<NameListPreset> tagsPresets = new List<NameListPreset>();
        public List<NameListPreset> referencesPresets = new List<NameListPreset>();

        public interface IPreset {
            public string name { get; set; }
        }

        [System.Serializable]
        public class ViewPreset : IPreset {
            [SerializeField] string m_name = "New Preset";

            public string name { get => m_name; set => m_name = value; }
            public bool multicolumn = false;
            public bool showVolume = true;
            public bool showPitch = true;
            public bool showChance = true;
            public float itemWidth = 300f;
            public bool showNameField = true;
            public bool showTags = true;

            public ZoundType selectedTypes;
            public List<string> selectedFolders = new List<string>();
            public List<string> selectedTags = new List<string>();
            public List<int> selectedReferences = new List<int>(); // zound ids
            public GroupBy groupBy = GroupBy.None;

            public void SetFromCurrentSettings() {
                var browserSettings = ZoundsProject.Instance.browserSettings;
                var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[0];

                multicolumn = browserSettings.multicolumn;
                showVolume = browserSettings.showVolume;
                showPitch = browserSettings.showPitch;
                showChance = browserSettings.showChance;
                itemWidth = browserSettings.itemWidth;
                showNameField = browserSettings.showNameField;
                showTags = browserSettings.showTags;
                selectedTypes = zoundTabProperties.selectedTypes;
                selectedFolders = zoundTabProperties.selectedFolders.ToList();
                selectedTags = zoundTabProperties.selectedTags.ToList();
                selectedReferences = zoundTabProperties.selectedReferences.ToList();
                groupBy = zoundTabProperties.groupBy;
            }

            internal void Apply() {
                var browserSettings = ZoundsProject.Instance.browserSettings;
                var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[0];
                Undo.RecordObject(ZoundsWindowProperties.Instance, "apply preset");
                ZoundsWindow.ModifyZoundsProject("apply preset", () => {
                    browserSettings.multicolumn = multicolumn;
                    browserSettings.showVolume = showVolume;
                    browserSettings.showPitch = showPitch;
                    browserSettings.showChance = showChance;
                    browserSettings.itemWidth = itemWidth;
                    browserSettings.showNameField = showNameField;
                    browserSettings.showTags = showTags;
                });
                zoundTabProperties.selectedTypes = selectedTypes;
                zoundTabProperties.selectedFolders = selectedFolders.ToList();
                zoundTabProperties.selectedTags = selectedTags.ToList();
                zoundTabProperties.selectedReferences = selectedReferences.ToList();
                zoundTabProperties.groupBy = groupBy;
                EditorUtility.SetDirty(ZoundsWindowProperties.Instance);
                ZoundsWindow.RepaintWindow();
            }
        }

        [System.Serializable]
        public class NameListPreset : IPreset {
            [SerializeField] string m_name = "New Preset";

            public string name { get => m_name; set => m_name = value; }
            public List<string> names = new List<string>();
        }

    }
}
