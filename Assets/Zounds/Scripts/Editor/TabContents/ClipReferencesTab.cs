using System.Linq;

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Zounds {

    public class ClipReferencesTab : TabContent {
        // v2.1.5 - Forced recompile for detection fix


        internal static bool needsRefresh = false;

        public override string name { get; set; } = "Clip References";

        private Dictionary<string, ClipGroup> clipGroups = null;
        private Vector2 scrollPos;
        private double lastUpdateTime;

        // The tab should stay visible as long as we have any identified issues in our list.
        // Even if we stage a fix (assign a clip), the issue remains in the list until "Confirm" is pressed.
        public override bool isVisible => clipGroups != null && clipGroups.Count > 0;
        public override Color headerColor => isVisible ? new Color(1f, 0.4f, 0.4f) : Color.white;

        public override void Update() {
            // Check for external refresh signal
            if (needsRefresh) {
                needsRefresh = false;
                clipGroups = ExtractClipGroups();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            // If we have a staged clip, do NOT auto-refresh or we lose the staging state and the tab might vanish
            bool hasStagedClip = clipGroups != null && clipGroups.Values.Any(g => g.audioClip != null);
            if (hasStagedClip) return;

            if (EditorApplication.timeSinceStartup - lastUpdateTime > 2.0) {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                
                // Only refresh background if we're not currently looking at the tab
                if (ZoundsWindow.MainTabView?.GetTab<ClipReferencesTab>(0) != this || clipGroups == null) {
                    clipGroups = ExtractClipGroups();
                }
            }
        }

        public override void OnTabOpened() {
            clipGroups = ExtractClipGroups();
        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            if (clipGroups == null) {
                clipGroups = ExtractClipGroups();
            }

            GUILayout.BeginVertical();
            {
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                {
                    GUILayout.Space(10f);
                    EditorGUILayout.LabelField("Missing References:", EditorStyles.boldLabel);
                    GUILayout.Space(10f);

                    int drawnCount = DrawClipGroups(200f, true);
                    
                    if (drawnCount == 0) {
                        GUILayout.Space(20f);
                        EditorGUILayout.LabelField("There is no missing audio clip reference.", EditorStyles.centeredGreyMiniLabel);
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private int DrawClipGroups(float labelWidth, bool drawMissing) {
            int drawnCount = 0;
            
            // We use a list to iterate to avoid modification exceptions if needed, 
            // though clipGroups shouldn't change during Draw.
            var keys = clipGroups.Keys.ToList();
            foreach (var key in keys) {
                var clipGroup = clipGroups[key];
                drawnCount++;

                GUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    var groupedByZound = clipGroup.zoundPathPairs
                        .GroupBy(p => p.zound)
                        .Select(g => new { Path = g.First().path });

                    foreach (var group in groupedByZound) {
                        EditorGUILayout.LabelField(group.Path, EditorStyles.boldLabel);
                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField("Missing:", EditorStyles.miniLabel, GUILayout.Width(50f));
                        GUI.color = new Color(1f, 0.4f, 0.4f);
                        string displayFileName = System.IO.Path.GetFileName(clipGroup.missingKey);
                        if (string.IsNullOrEmpty(displayFileName)) displayFileName = clipGroup.missingKey;
                        EditorGUILayout.LabelField(displayFileName, EditorStyles.miniLabel);
                        GUI.color = Color.white;
                    }
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(5f);

                    // Confirm Button - Disabled if no clip is selected
                    EditorGUI.BeginDisabledGroup(clipGroup.audioClip == null);
                    {
                        var originalBg = GUI.backgroundColor;
                        if (clipGroup.audioClip != null) GUI.backgroundColor = Color.green;
                        
                        if (GUILayout.Button("CONFIRM FIX", GUILayout.Height(30f))) {
                            string newPath = AssetDatabase.GetAssetPath(clipGroup.audioClip);
                            string newGuid = AssetDatabase.AssetPathToGUID(newPath);
                            
                            ZoundsWindow.ModifyAndSaveZoundsProject("replace audio clip", () => {
                                var clipRef = new UnityEngine.AddressableAssets.AssetReference(newGuid);
                                foreach (var pathPair in clipGroup.zoundPathPairs) {
                                    if (pathPair.zound is Klip klip) {
                                        if (pathPair.isSource) {
                                            klip.audioClipRef = clipRef;
                                            klip.audioClipPath = newPath;
                                            klip.needsRender = true;
                                        } else {
                                            klip.renderedClipRef = clipRef;
                                            klip.renderedClipPath = newPath;
                                        }
                                        if (Application.isPlaying && ZoundEngine.IsInitialized()) {
                                            ZoundDictionary.ValidateZoundRuntime(klip);
                                        }
                                    }
                                }
                            }, true);

                            #if ADDRESSABLES_INSTALLED
                            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                            if (settings != null) {
                                var entry = settings.CreateOrMoveEntry(newGuid, settings.DefaultGroup);
                                if (entry != null) entry.address = newPath;
                            }
                            #endif

                            needsRefresh = true;
                        }
                        GUI.backgroundColor = originalBg;
                    }
                    EditorGUI.EndDisabledGroup();

                    GUILayout.Space(5f);

                    GUILayout.BeginHorizontal();
                    {
                        EditorGUI.BeginChangeCheck();
                        var selectedClip = EditorGUILayout.ObjectField("Select New Clip:", clipGroup.audioClip, typeof(AudioClip), false) as AudioClip;
                        if (EditorGUI.EndChangeCheck()) {
                            clipGroup.audioClip = selectedClip;
                        }

                        if (clipGroup.audioClip != null) {
                            if (GUILayout.Button("Cancel", GUILayout.Width(60f))) {
                                clipGroup.audioClip = null;
                                needsRefresh = true;
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.Space(10f);
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
            if (zound == null) return;
            
            bool isMissing = false;

            // CRITICAL CHECK: Does the reference point to a valid file?
            if (clipRef == null || string.IsNullOrEmpty(clipRef.AssetGUID)) {
                isMissing = true;
            } else {
                try {
                    // Check if the asset exists. Note: RuntimeKeyIsValid can be misleading in Editor.
                    if (clipRef.editorAsset == null) {
                        isMissing = true;
                    }
                } catch {
                    isMissing = true;
                }
            }

            // REVERSED LOGIC: If it's healthy, we ignore it.
            if (!isMissing) {
                return;
            }

            // Special Case: Rendered clips (don't show in red tab if we can just re-render)
            if (!isSource && zound is Klip klip) {
                if (klip.audioClipRef != null && !string.IsNullOrEmpty(klip.audioClipRef.AssetGUID)) {
                    try {
                        if (klip.audioClipRef.editorAsset != null) {
                            klip.needsRender = true;
                            return; 
                        }
                    } catch { }
                }
            }

            // Identify the file string key for grouping
            string key = !string.IsNullOrEmpty(clipPath) ? clipPath : 
                         (clipRef != null && !string.IsNullOrEmpty(clipRef.AssetGUID) ? clipRef.AssetGUID : 
                         (isSource ? "[No Source]" : "[No Render]"));
            
            if (!result.TryGetValue(key, out var clipGroup)) {
                clipGroup = new ClipGroup() {
                    audioClip = null,
                    zoundPathPairs = new List<ClipGroup.ZoundPathPair>(),
                    missingKey = key
                };
                result.Add(key, clipGroup);
            }
            clipGroup.zoundPathPairs.Add(new ClipGroup.ZoundPathPair() {
                path = parentPath + zound.name,
                zound = zound,
                isSource = isSource
            });
        }

        public class ClipGroup {
            public AudioClip audioClip;
            public List<ZoundPathPair> zoundPathPairs = new List<ZoundPathPair>();
            public string missingKey;

            public class ZoundPathPair {
                public string path;
                public Zound zound;
                public bool isSource;
            }
        }

    }

}
