using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
using static Zounds.ZoundsWindowProperties.ZoundTabProperties;
#endif

namespace Zounds {
    public class AllZoundsTab : BaseZoundTab<Zound> {

        private static AllZoundsTab instance;
        public static AllZoundsTab Instance => instance;

        public AllZoundsTab() : base() {
            instance = this;
        }

        ~AllZoundsTab() {
            if (instance == this) instance = null;
        }

        // Store previous search keywords when click '+ Add New' button.
        private static string addMenuSearchText = "";

        public override string name => "Zounds";

        protected override int zoundTabPropertyIndex => 0;

        public override List<Zound> zoundsToDisplay {
            get {
                var result = new List<Zound>();

                var zoundsProject = ZoundsProject.Instance;
                var zoundLibrary = zoundsProject.zoundLibrary;
                var zoundTabProperties = ZoundsWindowProperties.Instance.zoundTabProperties[zoundTabPropertyIndex];

                var selectedTypes = zoundTabProperties.selectedTypes;

                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.Klip)) {
                    result.AddRange(zoundLibrary.klips);
                }
                else {
                    var cullingGroups = ZoundEngine.CullingGroups;
                    foreach (var kvp in cullingGroups) {
                        if (kvp.Key is Klip klip && kvp.Value.Count > 0) {
                            result.Add(klip);
                        }
                    }
                }

                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.Zequence)) {
                    result.AddRange(zoundLibrary.zequences);
                }
                else {
                    var cullingGroups = ZoundEngine.CullingGroups;
                    foreach (var kvp in cullingGroups) {
                        if (kvp.Key is Zequence zequence && kvp.Value.Count > 0) {
                            result.Add(zequence);
                        }
                    }
                }

                bool audioClipTypeSelected = (selectedTypes == ZoundType.None || selectedTypes == ZoundType.Everything || selectedTypes.HasFlag(ZoundType.AudioClip));
                if (audioClipTypeSelected) {
                    if (!zoundsProject.browserSettings.showAudioClips) {
                        zoundsProject.browserSettings.showAudioClips = true;
                        ZoundsAssetPostProcessor.RefreshAudioClipsCache();
                    }
                    var clipZoundsCache = ZoundsAssetPostProcessor.audioClipZoundsCache;
                    if (clipZoundsCache != null) {
                        result.AddRange(clipZoundsCache);
                    }
                }
                else {
                    var cullingGroups = ZoundEngine.CullingGroups;
                    foreach (var kvp in cullingGroups) {
                        if (kvp.Key is ClipZound clipZound && kvp.Value.Count > 0) {
                            result.Add(clipZound);
                        }
                    }
                }

                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.Missing)) {
                    var missingZounds = ZoundEngine.MissingZounds;
                    foreach (var z in missingZounds.Values) {
                        result.Add(z);
                    }
                }

                result = result.OrderBy(it => it.name).ToList();

                return result;
            }
        }

        protected override void HandleAddNew() {
            OpenAddNewZoundMenu();
        }

        public static void OpenAddNewZoundMenu(string nameOverride = null) {
            var mousePosition = Event.current.mousePosition;

            var genericMenu = new GenericMenu();

            genericMenu.AddItem(new GUIContent("Klip"), false, () => {
                OpenCreateNewKlipDialog(mousePosition, OnKlipAdded, addMenuSearchText, text => addMenuSearchText = text, nameOverride);
            });

            genericMenu.AddItem(new GUIContent("Zequence"), false, () => {
                ZoundsWindow.ModifyZoundsProject("add new zequence", () => {
                    var newZequence = new Zequence(ZoundLibrary.GetUniqueZoundId());
                    if (string.IsNullOrEmpty(nameOverride)) {
                        newZequence.name = ZoundDictionary.EnsureUniqueZoundName("New Zequence");
                    }
                    else {
                        newZequence.name = nameOverride;
                    }
                    OnZequenceAdded(newZequence);
                }, true);
            });

            genericMenu.ShowAsContext();
        }

        internal static void OnZequenceAdded(Zequence newZequence) {
            var zoundKey = ZoundDictionary.ZoundNameToKey(newZequence.name);
            var existingClipZound = ZoundsAssetPostProcessor.audioClipZoundsCache.Find(z => ZoundDictionary.ZoundNameToKey(z.name) == zoundKey);
            if (existingClipZound != null) {
                ZoundsAssetPostProcessor.audioClipZoundsCache.Remove(existingClipZound);
            }
            if (Application.isPlaying && ZoundEngine.Instance.zoundDictionary.ContainsKey(zoundKey)) {
                ZoundEngine.Instance.zoundDictionary.Remove(zoundKey);
            }

            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            zoundLibrary.zequences.Add(newZequence);
            zoundLibrary.zequences = zoundLibrary.zequences.OrderBy(it => it.name).ToList();
            if (instance != null) {
                instance.SelectZound(newZequence);
                instance.filterCache = null;
            }
            //if (Application.isPlaying) {
                if (ZoundEngine.IsInitialized()) {
                    ZoundDictionary.ValidateZoundRuntime(newZequence);
                }
            //}
        }

        internal static void OnKlipAdded(Klip newKlip) {
            var zoundKey = ZoundDictionary.ZoundNameToKey(newKlip.name);
            var existingClipZound = ZoundsAssetPostProcessor.audioClipZoundsCache.Find(z => ZoundDictionary.ZoundNameToKey(z.name) == zoundKey);
            if (existingClipZound != null) {
                ZoundsAssetPostProcessor.audioClipZoundsCache.Remove(existingClipZound);
            }
            if (Application.isPlaying && ZoundEngine.Instance.zoundDictionary.ContainsKey(zoundKey)) {
                ZoundEngine.Instance.zoundDictionary.Remove(zoundKey);
            }

            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            zoundLibrary.klips.Add(newKlip);
            zoundLibrary.klips = zoundLibrary.klips.OrderBy(it => it.name).ToList();

            if (instance != null) {
                instance.SelectZound(newKlip);
                instance.filterCache = null;
            }
        }

        public static void OpenCreateNewKlipDialog(Vector3 _mousePosition, System.Action<Klip> onKlipAdded, string searchText, System.Action<string> onSearchTextChanged, string nameOverride) {
            var genericMenu = new GenericMenu();
#if ADDRESSABLES_INSTALLED
            AudioAssetUtility.FindAllAudioReferencesInWorkspace(out var libraryAudioRefs, out var workAudioRefs, out var sourcesAudioRefs, out var _);
            foreach (var audioRef in libraryAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "", nameOverride);
            }
            foreach (var audioRef in workAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Work Files/", nameOverride);
            }
            foreach (var audioRef in sourcesAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Sources/", nameOverride);
            }
#endif

            GenericMenuPopup.Show(
                genericMenu,
                "Add New Klip(s)",
                _mousePosition,
                new List<string>(),
                searchText,
                newSearch => onSearchTextChanged?.Invoke(newSearch),
                userData => PlayAudioClip(userData),
                3, false, null, (updateFilter) => DrawFolderFilterButtons(updateFilter));
        }

        /// <summary>
        /// Draws a row of filter buttons for subfolders in the UserFiles directory.
        /// </summary>
        private static void DrawFolderFilterButtons(System.Action<string, bool> updateFilter) {
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
                return;
            }

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

                    bool isLibrary = !string.IsNullOrEmpty(libraryPath) && folderPath.StartsWith(libraryPath);
                    
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
                        string relative = "";
                        if (isLibrary) {
                            relative = folderPath.Replace(libraryPath, "");
                        } else if (!string.IsNullOrEmpty(sourcesPath) && folderPath.StartsWith(sourcesPath)) {
                            relative = folderPath.Replace(sourcesPath, "");
                        } else {
                            relative = folderPath.Replace(defaultRoot, "");
                        }
                        
                        relative = relative.Replace("\\", "/").ToLower();
                        if (relative.StartsWith("/")) relative = relative.Substring(1);
                        if (!string.IsNullOrEmpty(relative) && !relative.EndsWith("/")) relative += "/";
                        
                        updateFilter?.Invoke(relative, true);
                    }
                    GUI.color = defaultColor;
                    currentX += buttonWidth;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

#if ADDRESSABLES_INSTALLED
        private static void AddAudioRefToGenericMenu(System.Action<Klip> onKlipAdded, GenericMenu genericMenu, AssetReferenceT<AudioClip> audioRef, string parentPath, string nameOverride) {
            var clipName = audioRef.editorAsset.name;
            // Generate full path including folder hierarchy
            string assetPath = AssetDatabase.GetAssetPath(audioRef.editorAsset);
            var projectSettings = ZoundsProject.Instance.projectSettings;
            string projectSettingsLibraryPath = projectSettings.libraryFolderPath;
            string projectSettingsSourcesPath = projectSettings.sourcesFolderPath;
            string relativePath = "";
            if (!string.IsNullOrEmpty(projectSettingsLibraryPath) && assetPath.StartsWith(projectSettingsLibraryPath)) {
                relativePath = assetPath.Replace(projectSettingsLibraryPath, "").Replace("\\", "/");
                if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
                int lastSlash = relativePath.LastIndexOf('/');
                relativePath = lastSlash != -1 ? relativePath.Substring(0, lastSlash + 1) : "";
            } else if (!string.IsNullOrEmpty(projectSettingsSourcesPath) && assetPath.StartsWith(projectSettingsSourcesPath)) {
                string subPath = assetPath.Replace(projectSettingsSourcesPath, "").Replace("\\", "/");
                if (subPath.StartsWith("/")) subPath = subPath.Substring(1);
                int lastSlash = subPath.LastIndexOf('/');
                string subFolder = lastSlash != -1 ? subPath.Substring(0, lastSlash + 1) : "";
                relativePath = "Sources/" + subFolder;
            } else if (!string.IsNullOrEmpty(parentPath)) {
                relativePath = parentPath;
            }

            genericMenu.AddItem(new GUIContent(relativePath + clipName), false, userData => {
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

                    if (!string.IsNullOrEmpty(nameOverride)) {
                        newKlip.name = nameOverride;
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
#endif

        private static void PlayAudioClip(object userData) {
            if (userData is AudioClip audioClip) {
                AudioPreviewUtility.PlayPreviewClip(audioClip);
            }
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
            else if (zound is Zequence zequence) {
                ZequenceEditorWindow.OpenWindow(zequence);
            }
            else {
                // Fallback for missing/broken zounds
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

        internal void ConvertKlipToZequence(Klip klip) {
            ZoundsWindow.ModifyZoundsProject("convert to zequence", () => {
                ZoundsProject.Instance.zoundLibrary.klips.Remove(klip);
                var existingID = klip.id;
                var newZeq = new Zequence(existingID);
                newZeq.name = klip.name;

                klip.id = ZoundLibrary.GetUniqueZoundId();
                klip.parentId = newZeq.id;
                newZeq.localKlips.Add(klip);
                var newEntry = new CompositeZound.ZoundEntry();
                newEntry.zoundId = klip.id;
                newEntry.local = true;
                newZeq.zoundEntries.Add(newEntry);

                OnZequenceAdded(newZeq);
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
