using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class TagsEditorWindow : EditorWindow {

        private Zound zoundToEdit;

        public static void OpenWindow(Zound zound) {
            var window = GetWindow<TagsEditorWindow>();
            window.zoundToEdit = zound;
            window.titleContent.text = zound.name + " - Tags Editor";
            window.Show();
        }

        private void OnEnable() {
            if (zoundToEdit == null) {
                titleContent.text = "Tags Editor";
            }
            else {
                titleContent.text = zoundToEdit.name + " - Tags Editor";
            }
            minSize = new Vector2(200f, 150f);
        }

        private void OnGUI() {
            EditorGUILayout.LabelField("Simulation only. Tags Editor will be worked on the next task.");
            GUILayout.Space(20f);
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            var projectTags = zoundLibrary.tags;
            foreach (var tagId in zoundToEdit.tags) {
                var tag = projectTags.Find(t => t.id == tagId);
                if (tag != null) {
                    EditorGUILayout.LabelField(tag.name);
                }
            }
            if (GUILayout.Button("Create New Tag")) {
                var newTag = zoundLibrary.CreateNewTag(GenerateRandomString());
                zoundToEdit.tags.Add(newTag.id);
            }
        }

        private static string GenerateRandomString(int length = 10) {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            System.Random random = new System.Random();
            char[] stringChars = new char[length];

            for (int i = 0; i < length; i++) {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

    }

}