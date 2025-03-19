using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class MuzicHandler : ZoundHandler<Muzic> {

        public MuzicHandler(Muzic muzic, AudioSource audioSource) : base(muzic, audioSource) {

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
