using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class ClipZoundHandler : ZoundHandler<ClipZound> {

        public ClipZoundHandler(ClipZound clipZound, AudioSource audioSource, ZoundArgs zoundArgs) : base(clipZound, audioSource, zoundArgs) {
            audioSource.clip = zound.audioClip;
        }

    }

}