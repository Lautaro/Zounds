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
    public class ConsolidatedTab : BaseZoundTab<Zound> {

        private static ConsolidatedTab instance;
        public static ConsolidatedTab Instance => instance;

        public ConsolidatedTab() : base() {
            instance = this;
        }

        ~ConsolidatedTab() {
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

                //if (zoundsProject.browserSettings.showAudioClips) {
                if (selectedTypes == ZoundType.None || selectedTypes.HasFlag(ZoundType.AudioClip)) {
                    result.AddRange(ZoundsAssetPostProcessor.audioClipZoundsCache);
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
            if (Application.isPlaying && ZoundDictionary.zoundDictionary.ContainsKey(zoundKey)) {
                ZoundDictionary.zoundDictionary.Remove(zoundKey);
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
            if (Application.isPlaying && ZoundDictionary.zoundDictionary.ContainsKey(zoundKey)) {
                ZoundDictionary.zoundDictionary.Remove(zoundKey);
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
            AudioAssetUtility.FindAllAudioReferencesInWorkspace(out var userAudioRefs, out var workAudioRefs, out var sourceAudioRefs);
            foreach (var audioRef in userAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "", nameOverride);
            }
            foreach (var audioRef in workAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Work Files/", nameOverride);
            }
            foreach (var audioRef in sourceAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Source Files/", nameOverride);
            }
#endif

            GenericMenuPopup.Show(
                genericMenu,
                "Add New Klip(s)",
                _mousePosition,
                new List<string>(),
                searchText,
                newSearch => onSearchTextChanged?.Invoke(newSearch),
                userData => PlayAudioClip(userData));
        }

#if ADDRESSABLES_INSTALLED
        private static void AddAudioRefToGenericMenu(System.Action<Klip> onKlipAdded, GenericMenu genericMenu, AssetReferenceT<AudioClip> audioRef, string parentPath, string nameOverride) {
            var clipName = audioRef.editorAsset.name;
            genericMenu.AddItem(new GUIContent(parentPath + clipName), false, userData => {
                ZoundsWindow.ModifyZoundsProject("add new klips", () => {
                    var newKlip = new Klip(ZoundLibrary.GetUniqueZoundId());

                    var projectSettings = ZoundsProject.Instance.projectSettings;
                    string assetPath = AssetDatabase.GetAssetPath(audioRef.editorAsset);
                    if (assetPath.StartsWith(projectSettings.workFolderPath)) {
                        // copy to Source path if the clip is a rendered zound
                        string newPath = assetPath.Replace(projectSettings.workFolderPath, projectSettings.sourceFolderPath);
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
                var audioSource = ZoundEngine.Pool.RequestAudioSource();
                audioSource.clip = audioClip;
                audioSource.Play();
                ZoundEngine.Pool.ReturnAudioSource(audioSource);
            }

        }

        public override void OpenZoundEditor(Zound zound) {
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
                Debug.LogError("Invalid zound type: " + zound.name);
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
