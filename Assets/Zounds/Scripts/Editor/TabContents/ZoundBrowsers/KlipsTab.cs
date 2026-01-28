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
            var genericMenu = new GenericMenu();
#if ADDRESSABLES_INSTALLED
            AudioAssetUtility.FindAllAudioReferencesInWorkspace(out var userAudioRefs, out var workAudioRefs, out var sourceAudioRefs);
            foreach (var audioRef in userAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "");
            }
            foreach (var audioRef in workAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Work Files/");
            }
            foreach (var audioRef in sourceAudioRefs) {
                AddAudioRefToGenericMenu(onKlipAdded, genericMenu, audioRef, "Source Files/");
            }
#endif

            GenericMenuPopup.Show(
                genericMenu,
                "Add New Klip(s)",
                Event.current.mousePosition,
                new List<string>(),
                searchText,
                newSearch => onSearchTextChanged?.Invoke(newSearch),
                userData => PlayAudioClip(userData));
        }

        private static void AddAudioRefToGenericMenu(Action<Klip> onKlipAdded, GenericMenu genericMenu, AssetReferenceT<AudioClip> audioRef, string parentPath) {
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
