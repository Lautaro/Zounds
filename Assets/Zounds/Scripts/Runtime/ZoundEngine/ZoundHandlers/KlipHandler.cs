using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class KlipHandler : ZoundHandler<Klip> {

        public KlipHandler(Klip klip, AudioSource audioSource, ZoundArgs zoundArgs) : base(klip, audioSource, zoundArgs) {
#if ADDRESSABLES_INSTALLED
            var clip = ZoundDictionary.GetOrLoadClip(zound.GetAudioClipReference());
            audioSource.clip = clip;
#endif
        }

    }

}