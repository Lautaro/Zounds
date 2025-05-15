using System;
using System.Collections.Generic;
using System.IO;
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

        // Store previous search keywords when click '+ Add New' button.
        private string addMenuSearchText = "";

        public override string name => "Klips";

        protected override int zoundTabPropertyIndex => 0;

        public override List<Klip> zounds {
            get => ZoundsProject.Instance.zoundLibrary.klips;
            set => ZoundsProject.Instance.zoundLibrary.klips = value;
        }

        protected override void HandleAddNew() {
            OpenCreateNewKlipDialog(OnKlipAdded, addMenuSearchText, text => addMenuSearchText = text);
        }

        private void OnKlipAdded(Klip newKlip) {
            zounds.Add(newKlip);
            SortZounds();
            SelectZound(newKlip);
        }

        public static void OpenCreateNewKlipDialog(Action<Klip> onKlipAdded, string searchText, Action<string> onSearchTextChanged) {
            var genericMenu = new GenericMenu();
#if ADDRESSABLES_INSTALLED
            foreach (var audioRef in AudioAssetUtility.FindAllAudioReferencesInWorkspace()) {
                var clipName = audioRef.editorAsset.name;
                genericMenu.AddItem(new GUIContent(clipName), false, userData => {
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

                        onKlipAdded?.Invoke(newKlip);
                    }, true);
                }, audioRef.editorAsset);
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

        private static void PlayAudioClip(object userData) {
            if (userData is AudioClip audioClip) {
                var audioSource = ZoundEngine.Pool.RequestAudioSource();
                audioSource.clip = audioClip;
                audioSource.Play();
                ZoundEngine.Pool.ReturnAudioSource(audioSource);
            }

        }

        public override void OpenZoundEditor(Klip zound) {
            KlipEditorWindow.OpenWindow(zound);
        }

    }

}
