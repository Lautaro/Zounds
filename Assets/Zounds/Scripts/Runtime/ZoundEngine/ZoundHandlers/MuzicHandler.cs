using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class MuzicHandler : ZoundHandler<Muzic> {

        public MuzicHandler(Muzic muzic, AudioSource audioSource, ZoundArgs zoundArgs) : base(muzic, audioSource, zoundArgs) {

        }

        public override void OnStart(float timeOffset) {
#if ADDRESSABLES_INSTALLED
            var clip = ZoundDictionary.GetOrLoadClip(zound.audioClipRef);
            audioSource.clip = clip;
#endif
            base.OnStart(timeOffset);
        }

    }

}
