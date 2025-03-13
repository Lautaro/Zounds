using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    /// <summary>
    /// Tab drawer for Randomizers tab in Zound Browser.
    /// </summary>
    public class RandomizersTab : BaseZoundTab<Randomizer> {

        public override string name => "Randomizers";

        protected override int zoundTabPropertyIndex => 3;

        public override List<Randomizer> zounds {
            get => ZoundsProject.Instance.zoundLibrary.randomizers;
            set => ZoundsProject.Instance.zoundLibrary.randomizers = value;
        }

        protected override void HandleAddNew() {
            ModifyZoundsProject("add new randomizer", () => {
                var newZequence = new Randomizer();
                newZequence.name = "New Randomizer";
                zounds.Add(newZequence);
                SortZounds();
                SelectZound(newZequence);
            }, true);
        }

    }

}
