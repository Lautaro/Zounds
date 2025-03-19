using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class RandomizerHandler : ZoundHandler<Randomizer> {

        public RandomizerHandler(Randomizer randomizer, AudioSource audioSource) : base(randomizer, audioSource) {

        }

        public override void OnStart() {
#if ADDRESSABLES_INSTALLED
            audioSource.clip = null;
#endif
            base.OnStart();
        }

    }

}