using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class KlipHandler : ZoundHandler<Klip> {

        public KlipHandler(Klip klip, AudioSource audioSource) : base(klip, audioSource) {

        }

        public override void OnStart() {
#if ADDRESSABLES_INSTALLED
            var clip = ZoundDictionary.GetOrLoadClip(zound.audioClipRef);
            audioSource.clip = clip;
#endif
            base.OnStart();
        }

    }

}