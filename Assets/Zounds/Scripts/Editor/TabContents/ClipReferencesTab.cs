using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Zounds {

    public class ClipReferencesTab : TabContent {

        internal static bool needsRefresh = false;

        public override string name => "Clip References";

        private Dictionary<string, ClipGroup> clipGroups = null;
        private Vector2 scrollPos;

        public override void OnTabOpened() {
            clipGroups = ExtractClipGroups();
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            if (clipGroups == null || needsRefresh) {
                needsRefresh = false;
                clipGroups = ExtractClipGroups();
            }
            bool dirty = false;

            contentRect.x += 10f;
            contentRect.y += 30f;
            contentRect.width -= 20f;
            contentRect.height -= 40f;
            GUILayout.BeginArea(contentRect);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            var fieldWidth = EditorGUIUtility.fieldWidth;
            var labelWidth = EditorGUIUtility.labelWidth;
            float halfWidth = contentRect.width / 2f;
            EditorGUIUtility.fieldWidth = halfWidth;

            EditorGUILayout.LabelField("Missing References:", EditorStyles.boldLabel);
            GUILayout.Space(2f);
            EditorGUI.indentLevel++;
            int drawnCount = DrawClipGroups(halfWidth, ref dirty, true);
            if (drawnCount == 0) {
                EditorGUILayout.LabelField("There is no missing audio clip reference.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUI.indentLevel--;
            GUILayout.Space(10f);

            EditorGUIUtility.fieldWidth = fieldWidth;

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (dirty) {
                clipGroups = null;
            }
        }

        private int DrawClipGroups(float labelWidth, ref bool dirty, bool drawMissing) {
            int drawnCount = 0;
            var guiColor = GUI.color;
            foreach (var kvp in clipGroups) {
                var clipGroup = kvp.Value;
                if (drawMissing && clipGroup.audioClip != null) continue;
                if (!drawMissing && clipGroup.audioClip == null) continue;
                drawnCount++;

                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var newClip = EditorGUILayout.ObjectField(clipGroup.audioClip, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck() && newClip != null) {
                    dirty = true;
                    ZoundsWindow.ModifyZoundsProject("replace audio clip", () => {
                        var assetPath = AssetDatabase.GetAssetPath(newClip);
                        var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                        var clipRef = new AssetReference(assetGuid);
                        foreach (var pathPair in clipGroup.zoundPathPairs) {
                            if (pathPair.zound is Klip klip) {
                                if (pathPair.isSource) {
                                    klip.audioClipRef = clipRef;
                                    klip.audioClipPath = assetPath;
                                }
                                else {
                                    klip.renderedClipRef = clipRef;
                                    klip.renderedClipPath = assetPath;
                                }
                            }
                        }
                    });
                }
                GUILayout.BeginVertical();
                {
                    if (clipGroup.audioClip == null) {
                        GUI.color = new Color(1f, 0.2f, 0.2f);
                        EditorGUILayout.SelectableLabel(kvp.Key, EditorStyles.miniBoldLabel, GUILayout.MaxWidth(labelWidth - 30f), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight));
                        GUI.color = guiColor;
                    }
                    foreach (var pathPair in clipGroup.zoundPathPairs) {
                        string status = pathPair.isSource ? "Source" : "Rendered";
                        GUI.color = pathPair.isSource ? new Color(1f, 0.8f, 0.4f) : Color.cyan;
                        EditorGUILayout.LabelField($"{pathPair.path} ({status})", GUILayout.MaxWidth(labelWidth - 30f));
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);
                var lineRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
                lineRect = EditorGUI.IndentedRect(lineRect);
                GUI.color = Color.gray;
                GUI.DrawTexture(lineRect, EditorGUIUtility.whiteTexture);
                GUI.color = guiColor;
                GUILayout.Space(5f);
            }

            return drawnCount;
        }

        public static Dictionary<string, ClipGroup> ExtractClipGroups() {
            var result = new Dictionary<string, ClipGroup>();
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;

            zoundLibrary.ForEachZound(z => {
                HandleZoundExtraction("", z, result);
            });

            return result;
        }

        private static void HandleZoundExtraction(string parentPath, Zound zound, Dictionary<string, ClipGroup> result) {
            if (zound is Klip klip) {
                RegisterClip(parentPath, zound, result, klip.audioClipRef, klip.audioClipPath, true);
                RegisterClip(parentPath, zound, result, klip.renderedClipRef, klip.renderedClipPath, false);
            }
            else if (zound is Zequence zequence) {
                foreach (var entry in zequence.zoundEntries) {
                    if (!entry.local) continue;
                    if (zequence.TryGetEntryZound(entry, out var childZound)) {
                        HandleZoundExtraction(parentPath + zound.name + " -> ", childZound, result);
                    }
                }
            }
        }

        private static void RegisterClip(string parentPath, Zound zound, Dictionary<string, ClipGroup> result, AssetReference clipRef, string clipPath, bool isSource) {
            if (clipRef != null && clipRef.RuntimeKeyIsValid()) {
                var audioClip = clipRef.editorAsset as AudioClip;
                string guid = audioClip == null ? clipPath : clipRef.AssetGUID;
                if (!result.TryGetValue(guid, out var clipGroup)) {
                    clipGroup = new ClipGroup() {
                        audioClip = audioClip,
                        zoundPathPairs = new List<ClipGroup.ZoundPathPair>()
                    };
                    result.Add(guid, clipGroup);
                }
                clipGroup.zoundPathPairs.Add(new ClipGroup.ZoundPathPair() {
                    path = parentPath + zound.name,
                    zound = zound,
                    isSource = isSource
                });
            }
        }

        public class ClipGroup {
            public AudioClip audioClip;
            public List<ZoundPathPair> zoundPathPairs = new List<ZoundPathPair>();

            public class ZoundPathPair {
                public string path;
                public Zound zound;
                public bool isSource;
            }
        }

    }

}
