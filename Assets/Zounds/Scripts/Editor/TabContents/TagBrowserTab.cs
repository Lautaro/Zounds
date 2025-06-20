using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class TagBrowserTab : TabContent {

        public override string name => "Tag Browser";

        private Dictionary<int, TagGroup> tagGroups = new Dictionary<int, TagGroup>();
        private Vector2 scrollPos;

        public override void OnTabOpened() {
            tagGroups = ExtractTagGroups();
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            contentRect.x += 10f;
            contentRect.y += 30f;
            contentRect.width -= 20f;
            contentRect.height -= 40f;
            GUILayout.BeginArea(contentRect);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            foreach (var tagGroup in tagGroups.Values) {
                tagGroup.foldout = EditorGUILayout.Foldout(tagGroup.foldout, tagGroup.tag, true);
                if (tagGroup.foldout) {
                    EditorGUI.indentLevel++;
                    foreach (var zound in tagGroup.zounds) {
                        EditorGUILayout.LabelField(string.Format("{0} ({1})", zound.name, zound.GetType().Name));
                    }
                    EditorGUI.indentLevel--;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public static Dictionary<int, TagGroup> ExtractTagGroups() {
            var result = new Dictionary<int, TagGroup>();
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            zoundLibrary.ForEachZound(zound => {
                foreach (var tagId in zound.tags) {
                    if (!result.TryGetValue(tagId, out var tagGroup)) {
                        var tag = ZoundsProject.Instance.zoundLibrary.tags.Find(t => t.id == tagId);
                        if (tag == null) continue;
                        tagGroup = new TagGroup() { tag = tag.name };
                        result.Add(tagId, tagGroup);
                    }
                    tagGroup.zounds.Add(zound);
                }
            });
            return result;
        }

        public class TagGroup {
            public string tag;
            public bool foldout;
            public List<Zound> zounds = new List<Zound>();
        }

    }

}
