using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    /// <summary>
    /// Tab drawer for Muzics tab in Zound Browser.
    /// </summary>
    public class MuzicsTab : BaseZoundTab<Muzic> {

        public override string name => "Muzics";

        protected override int zoundTabPropertyIndex => 2;

        public override List<Muzic> zounds {
            get => ZoundsProject.Instance.zoundLibrary.muzics;
            set => ZoundsProject.Instance.zoundLibrary.muzics = value;
        }

        protected override void HandleAddNew() {
            ZoundsWindow.ModifyZoundsProject("add new muzic", () => {
                var newMuzic = new Muzic(ZoundLibrary.GetUniqueZoundId());
                newMuzic.name = ZoundDictionary.EnsureUniqueZoundName("New Muzic");
                zounds.Add(newMuzic);
                SortZounds();
                SelectZound(newMuzic);
                //if (Application.isPlaying) {
                    if (ZoundEngine.IsInitialized()) {
                        ZoundDictionary.ValidateZoundRuntime(newMuzic);
                    }
                //}
            }, true);
        }

    }

}
