using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class TagBrowserTab : TabContent {

        public override string name => "Tag Browser";

        private Dictionary<string, TagGroup> tagGroups = null;
        private Vector2 scrollPos;

        public override void OnTabOpened() {
            tagGroups = ExtractTagGroups();
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            if (tagGroups == null) {
                tagGroups = ExtractTagGroups();
            }

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

        public static Dictionary<string, TagGroup> ExtractTagGroups() {
            var result = new Dictionary<string, TagGroup>();
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            HashSet<string> keyTags = new HashSet<string>();
            foreach (var tag in zoundLibrary.tags) {
                var nameSplit = tag.name.Split(':');
                if (nameSplit.Length > 1) {
                    keyTags.Add(nameSplit[0]);
                }
                else {
                    keyTags.Add(tag.name);
                }
            }
            foreach (var keyTag in keyTags) {
                var zounds = ZoundsFilter.GetZoundsByTag(keyTag);
                result.Add(keyTag, new TagGroup() {
                    tag = keyTag,
                    zounds = zounds
                });
            }
            return result;
        }

        public class TagGroup {
            public string tag;
            public bool foldout;
            public List<Zound> zounds = new List<Zound>();
        }

    }

}
