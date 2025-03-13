using System.Collections.Generic;
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
            var genericMenu = new GenericMenu();
#if ADDRESSABLES_INSTALLED
            foreach (var audioRef in AudioAssetUtility.FindAllAudioReferencesInWorkspace()) {
                var clipName = audioRef.editorAsset.name;
                genericMenu.AddItem(new GUIContent(clipName), false, userData => {
                    ModifyZoundsProject("add new klips", () => {
                        var newKlip = new Klip();
                        newKlip.name = clipName;
                        newKlip.audioClipRef = audioRef;
                        zounds.Add(newKlip);
                        SortZounds();
                        SelectZound(newKlip);
                    }, true);
                }, audioRef);
            }
#endif

            GenericMenuPopup.Show(
                genericMenu,
                "Add New Klip(s)",
                Event.current.mousePosition,
                new List<string>(),
                addMenuSearchText,
                newSearch => addMenuSearchText = newSearch,
                userData => PlayAudioClip(userData));
        }

        private void PlayAudioClip(object userData) {
#if ADDRESSABLES_INSTALLED
            if (userData is AssetReferenceT<AudioClip> audioRef) {
                Debug.Log(audioRef.editorAsset.name + ": Playing AudioClip is not supported yet.");
            }
#endif
        }

    }

}
