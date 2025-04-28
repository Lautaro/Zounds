using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class RandomizerHandler : ZoundHandler<Randomizer> {

        public RandomizerHandler(Randomizer randomizer, AudioSource audioSource, ZoundArgs zoundArgs) : base(randomizer, audioSource, zoundArgs) {

        }

        public override void OnStart(float timeOffset) {
#if ADDRESSABLES_INSTALLED
            audioSource.clip = null;
#endif
            base.OnStart(timeOffset);
        }

    }

}