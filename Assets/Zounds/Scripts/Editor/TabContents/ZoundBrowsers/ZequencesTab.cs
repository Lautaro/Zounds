using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    /// <summary>
    /// Tab drawer for Zequences tab in Zound Browser.
    /// </summary>
    public class ZequencesTab : BaseZoundTab<Zequence> {

        public override string name => "Zequences";

        protected override int zoundTabPropertyIndex => 1;

        public override List<Zequence> zounds {
            get => ZoundsProject.Instance.zoundLibrary.zequences;
            set => ZoundsProject.Instance.zoundLibrary.zequences = value;
        }

        protected override void HandleAddNew() {
            ModifyZoundsProject("add new zequence", () => {
                var newZequence = new Zequence(ZoundLibrary.GetUniqueZoundId());
                newZequence.name = "New Zequence";
                zounds.Add(newZequence);
                SortZounds();
                SelectZound(newZequence);
            }, true);
        }

    }

}
