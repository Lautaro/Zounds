using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using static Zounds.TagBrowserTab;

namespace Zounds {

    public class RoutingTab : TabContent {

        public override string name => "Routing";

        private Vector2 scrollPos;

        public override void OnTabOpened() {
            var zoundRoutings = ZoundsProject.Instance.zoundRoutings;
            var projectSettings = ZoundsProject.Instance.projectSettings;
            ScanFolderRoutings(zoundRoutings, projectSettings.userFolderPath);
            ScanFolderRoutings(zoundRoutings, projectSettings.sourceFolderPath);
            ScanFolderRoutings(zoundRoutings, projectSettings.systemFolderPath + "/WorkFiles");

            ScanTagRoutings(zoundRoutings);
        }

        private static void ScanFolderRoutings(ZoundRoutings zoundRoutings, string folderPath) {
            var subFolders = AssetDatabase.GetSubFolders(folderPath);
            foreach (var subFolder in subFolders) {
                string relativePath = subFolder.Replace(folderPath + "/", "");
                var routing = zoundRoutings.folderRoutings.Find(r => r.relativePath == relativePath);
                if (routing == null) {
                    routing = new ZoundRoutings.FolderRouting() {
                        relativePath = relativePath
                    };
                    zoundRoutings.folderRoutings.Add(routing);
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            }
        }

        private void ScanTagRoutings(ZoundRoutings zoundRoutings) {
            var tagGroups = TagBrowserTab.ExtractTagGroups();
            foreach (var kvp in tagGroups) {
                int tagId = kvp.Key;
                var tagGroup = kvp.Value;
                var tagRouting = zoundRoutings.tagRoutings.Find(r => r.tagId == tagId);
                if (tagRouting == null) {
                    tagRouting = new ZoundRoutings.TagRouting() {
                        tagId = tagId
                    };
                    zoundRoutings.tagRoutings.Add(tagRouting);
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            }
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            contentRect.x += 10f;
            contentRect.y += 30f;
            contentRect.width -= 20f;
            contentRect.height -= 40f;
            GUILayout.BeginArea(contentRect);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            var zoundsProject = ZoundsProject.Instance;
            var zoundRoutings = zoundsProject.zoundRoutings;

            EditorGUILayout.LabelField("Folder Routings", EditorStyles.boldLabel);
            GUILayout.Space(2f);
            foreach (var folderRouting in zoundRoutings.folderRoutings) {
                EditorGUI.BeginChangeCheck();
                var mixerGroup = EditorGUILayout.ObjectField(folderRouting.relativePath, folderRouting.mixerGroup, typeof(AudioMixerGroup), false) as AudioMixerGroup;
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(zoundsProject, "change folder mixer group");
                    folderRouting.mixerGroup = mixerGroup;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }

            GUILayout.Space(10f);

            EditorGUILayout.LabelField("Tag Routings", EditorStyles.boldLabel);
            GUILayout.Space(2f);
            foreach (var tagRouting in zoundRoutings.tagRoutings) {
                EditorGUI.BeginChangeCheck();
                var tag = zoundsProject.zoundLibrary.tags.Find(t => t.id == tagRouting.tagId);
                if (tag == null) continue;
                var mixerGroup = EditorGUILayout.ObjectField(tag.name, tagRouting.mixerGroup, typeof(AudioMixerGroup), false) as AudioMixerGroup;
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(zoundsProject, "change tag mixer group");
                    tagRouting.mixerGroup = mixerGroup;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

    }

}
