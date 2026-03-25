using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace Zounds {

    /// <summary>
    /// Tab drawer for Klips tab in Zound Browser.
    /// </summary>
    public class KlipsTab : BaseZoundTab<Klip> {

        private static KlipsTab instance;
        public static KlipsTab Instance => instance;

        public KlipsTab() : base() {
            instance = this;
        }

        // Store previous search keywords when click '+ Add New' button.
        private string addMenuSearchText = "";

        public override string name => "Klips";

        protected override int zoundTabPropertyIndex => 0;

        public override List<Klip> zounds {
            get => ZoundsProject.Instance.zoundLibrary.klips;
            set => ZoundsProject.Instance.zoundLibrary.klips = value;
        }

        public override List<Zound> zoundsToDisplay {
            get {
                var result = base.zoundsToDisplay;
                bool needsReorder = false;

                if (true/*ZoundsProject.Instance.browserSettings.showAudioClips*/) {
                    result.AddRange(ZoundsAssetPostProcessor.audioClipZoundsCache);
                    needsReorder = true;
                }
                //else {
                //    var cullingGroups = ZoundEngine.CullingGroups;
                //    foreach (var kvp in cullingGroups) {
                //        if (kvp.Key is ClipZound clipZound && kvp.Value.Count > 0) {
                //            result.Add(clipZound);
                //        }
                //    }
                //}

                var missingZounds = ZoundEngine.MissingZounds;
                foreach (var z in missingZounds.Values) {
                    result.Add(z);
                }
                if (missingZounds.Count > 0) needsReorder = true;

                if (needsReorder) {
                    result = result.OrderBy(it => it.name).ToList();
                }

                return result;
            }
        }

        protected override void HandleAddNew() {
            OpenCreateNewKlipDialog(OnKlipAdded, addMenuSearchText, text => addMenuSearchText = text);
        }

        private void OnKlipAdded(Klip newKlip) {
            zounds.Add(newKlip);
            SortZounds();
            SelectZound(newKlip);
            filterCache = null;
        }

        public static void OpenCreateNewKlipDialog(Action<Klip> onKlipAdded, string searchText, Action<string> onSearchTextChanged) {
            Debug.Log("[ZoundsDebug] OpenCreateNewKlipDialog called");
            var genericMenu = new GenericMenu();
#if ADDRESSABLES_INSTALLED
            AudioAssetUtility.FindAllAudioReferencesInWorkspace(out var libraryAudioRefs, out var workAudioRefs, out var sourcesAudioRefs, out var _);
            foreach (var audioRef in libraryAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "");
            }
            // Removed workAudioRefs loop to prevent using rendered workfiles as Klip sources.
            foreach (var audioRef in sourcesAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Sources/");
            }
#endif

            GenericMenuPopup.Show(
                genericMenu,
                "Add New Klip(s)",
                Event.current.mousePosition,
                new List<string>(),
                searchText,
                newSearch => onSearchTextChanged?.Invoke(newSearch),
                userData => PlayAudioClip(userData),
                3, false, null, (updateFilter) => DrawFolderFilterButtons(updateFilter));
        }

        private static void AddAudioRefToGenericMenu(Action<Klip> onKlipAdded, GenericMenu genericMenu, AssetReferenceT<AudioClip> audioRef, string parentPath) {
            var clipName = audioRef.editorAsset.name;
            // Generate full path including folder hierarchy
            string assetPath = AssetDatabase.GetAssetPath(audioRef.editorAsset);
            var projectSettings = ZoundsProject.Instance.projectSettings;
            string relativePath = "";

            if (assetPath.StartsWith(projectSettings.libraryFolderPath)) {
                relativePath = assetPath.Replace(projectSettings.libraryFolderPath, "").Replace("\\", "/");
            } 
            else if (assetPath.StartsWith(projectSettings.sourcesFolderPath)) {
                relativePath = "Sources/" + assetPath.Replace(projectSettings.sourcesFolderPath, "").Replace("\\", "/");
            }
            else if (!string.IsNullOrEmpty(parentPath)) {
                relativePath = parentPath;
            }

            if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
            int lastSlash = relativePath.LastIndexOf('/');
            if (lastSlash != -1) {
                relativePath = relativePath.Substring(0, lastSlash + 1);
            } else {
                relativePath = "";
            }

            // Debug.Log($"[ZoundsTrace] KlipsTab.AddAudioRefToGenericMenu: clip={clipName}, relativePath={relativePath}, assetPath={assetPath}");
            // Debug.Log($"[ZoundsTrace] MENU ITEM CLICKED: {clipName} at {assetPath}");


            genericMenu.AddItem(new GUIContent(relativePath + clipName), false, userData => {
                Debug.Log($"[ZoundsTrace] MENU ITEM CLICKED: {clipName} at {assetPath}");

                ZoundsWindow.ModifyZoundsProject("add new klips", () => {
                    var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());

                    var projectSettings = ZoundsProject.Instance.projectSettings;
                    string assetPath = AssetDatabase.GetAssetPath(audioRef.editorAsset);
            if (assetPath.StartsWith(projectSettings.workFolderPath)) {
                        // copy to Sources path if the clip is a rendered zound
                        string newPath = assetPath.Replace(projectSettings.workFolderPath, projectSettings.sourcesFolderPath);
                        newPath = Path.ChangeExtension(newPath, ".Copy.wav");
                        newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                        var reloadedAudio = AudioRenderUtility.SaveAudio(audioRef.editorAsset, newPath);
                        newKlip.audioClipRef = AudioRenderUtility.GetAudioReference(reloadedAudio);
                        newKlip.name = ZoundDictionary.EnsureUniqueZoundName(newKlip.audioClipRef.editorAsset.name);
                    }
                    else {
                        newKlip.audioClipRef = audioRef;
                        newKlip.name = ZoundDictionary.EnsureUniqueZoundName(clipName);
                    }

                    newKlip.trimStart = 0f;
                    newKlip.trimEnd = audioRef.editorAsset.length;
                    newKlip.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                    newKlip.pitchEnvelope = new Envelope(Zound.MinPitchRange, Zound.MaxPitchRange);

                    //if (Application.isPlaying) {
                    if (ZoundEngine.IsInitialized()) {
                        ZoundDictionary.ValidateZoundRuntime(newKlip);
                    }
                    //}

                    onKlipAdded?.Invoke(newKlip);
                }, true);
            }, audioRef.editorAsset);
        }

        private static void PlayAudioClip(object userData) {
            if (userData is AudioClip audioClip) {
                AudioPreviewUtility.PlayPreviewClip(audioClip);
            }
        }


        /// <summary>
        /// Draws a row of filter buttons for subfolders in the UserFiles directory.
        /// </summary>
        private static void DrawFolderFilterButtons(System.Action<string, bool> updateFilter) {
            Debug.Log("[ZoundsDebug] KlipsTab.DrawFolderFilterButtons called");
            var projectSettings = ZoundsProject.Instance.projectSettings;
            string libraryPath = projectSettings.libraryFolderPath;
            string sourcesPath = projectSettings.sourcesFolderPath;
            
            var allFolders = new List<string>();
            if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath)) {
                allFolders.AddRange(Directory.GetDirectories(libraryPath, "*", SearchOption.AllDirectories));
            }
            if (!string.IsNullOrEmpty(sourcesPath) && Directory.Exists(sourcesPath)) {
                allFolders.AddRange(Directory.GetDirectories(sourcesPath, "*", SearchOption.AllDirectories));
            }

            // Also check the default path in case library/sources paths are empty or pointing to subdirectories
            string defaultRoot = "Assets/GameData/ZoundsData";
            if (allFolders.Count == 0 && Directory.Exists(defaultRoot)) {
                allFolders.AddRange(Directory.GetDirectories(defaultRoot, "*", SearchOption.AllDirectories));
            }

            if (allFolders.Count == 0) {
                Debug.Log($"[ZoundsDebug] No folders found to draw filter buttons at {libraryPath}, {sourcesPath}, or {defaultRoot}.");
                return;
            }

            Debug.Log($"[ZoundsDebug] Drawing folder filter buttons for {allFolders.Count} folders. libraryPath: {libraryPath}, sourcesPath: {sourcesPath}");

            // Define colors for Library and Sources
            Color libraryColor = new Color(0.7f, 0.9f, 0.7f); // Light green
            Color sourcesColor = new Color(0.7f, 0.8f, 1.0f); // Light blue
            Color defaultColor = GUI.color;

            // Draw the folder filter buttons
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // We use a flexible layout that calculates widths more accurately
                float viewWidth = EditorGUIUtility.currentViewWidth - 30f;
                float currentX = 0f;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Folders:", EditorStyles.miniLabel, GUILayout.Width(50));
                currentX += 55f;

                // "All" button
                if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                    updateFilter?.Invoke("", true);
                }
                currentX += 45f;

                var uniqueNames = new HashSet<string>();
                foreach (string folderPath in allFolders) {
                    string folderName = Path.GetFileName(folderPath);
                    if (uniqueNames.Contains(folderName)) continue;
                    uniqueNames.Add(folderName);

                    bool isLibrary = folderPath.StartsWith(libraryPath);
                    
                    // Calculate button size
                    float buttonWidth = EditorStyles.miniButton.CalcSize(new GUIContent(folderName)).x + 4f;

                    if (currentX + buttonWidth > viewWidth) {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(55f); // Align with label
                        currentX = 55f;
                    }

                    GUI.color = isLibrary ? libraryColor : sourcesColor;
                    if (GUILayout.Button(folderName, EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                        string relative = isLibrary ? folderPath.Replace(libraryPath, "") : folderPath.Replace(sourcesPath, "");
                        
                        relative = relative.Replace("\\", "/").ToLower();
                        // Remove leading slash if any
                        if (relative.StartsWith("/")) relative = relative.Substring(1);
                        // Ensure trailing slash for consistent hierarchy matching
                        if (!string.IsNullOrEmpty(relative) && !relative.EndsWith("/")) relative += "/";
                        
                        Debug.Log($"[ZoundsDebug] KlipsTab: Filtering by folder: '{relative}'");
                        updateFilter?.Invoke(relative, true);
                    }
                    GUI.color = defaultColor;
                    currentX += buttonWidth;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        public override void OpenZoundEditor(Zound zound) {
            if (zound == null) return;
            
            if (zound is ClipZound clipZound) {
                if (EditorUtility.DisplayDialog("Convert to Klip: " + zound.name, "In order for this audio clip to be editable, it must be converted into a Klip. Convert this into a Klip?\n" + zound.name, "Convert", "Cancel")) {
                    ConvertClipToKlip(clipZound);
                }
            }
            else if (zound is Klip klip) {
                KlipEditorWindow.OpenWindow(klip);
            }
            else {
                KlipEditorWindow.OpenWindow(zound as Klip);
            }
        }

        internal void ConvertClipToKlip(ClipZound clipZound) {
            ZoundsAssetPostProcessor.audioClipZoundsCache.Remove(clipZound);
            ZoundsWindow.ModifyZoundsProject("convert to klip", () => {
                var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());

                newKlip.audioClipRef = AudioRenderUtility.GetAudioReference(clipZound.audioClip);
                newKlip.name = clipZound.name;

                newKlip.trimStart = 0f;
                newKlip.trimEnd = clipZound.audioClip.length;
                newKlip.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                newKlip.pitchEnvelope = new Envelope(Zound.MinPitchRange, Zound.MaxPitchRange);

                //if (Application.isPlaying) {
                if (ZoundEngine.IsInitialized()) {
                    ZoundDictionary.ValidateZoundRuntime(newKlip);
                }
                //}

                OnKlipAdded(newKlip);
                filterCache = null;
            }, true);
        }

        //protected override void OnAfterDrawColumnMode() {
        //    var labelWidth = EditorGUIUtility.labelWidth;
        //    EditorGUIUtility.labelWidth = 100f;
        //    EditorGUI.BeginChangeCheck();
        //    bool showAudioClips = EditorGUILayout.Toggle("Show AudioClips", ZoundsProject.Instance.browserSettings.showAudioClips);
        //    if (EditorGUI.EndChangeCheck()) {
        //        Undo.RecordObject(ZoundsProject.Instance, "toggle show AudioClips");
        //        ZoundsProject.Instance.browserSettings.showAudioClips = showAudioClips;
        //        EditorUtility.SetDirty(ZoundsProject.Instance);
        //        var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];
        //        zoundTabProperties.dirty = true; 
        //    }
        //    EditorGUIUtility.labelWidth = labelWidth;
        //}

    }

}
