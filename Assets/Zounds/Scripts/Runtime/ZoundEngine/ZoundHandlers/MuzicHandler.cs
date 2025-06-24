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
#if ZOUNDS_CONSIDER_FOLDERS
            var clipPath = zound.audioClipPath;

            if (!ZoundDictionary.runtimeClipFolders.ContainsKey(clip)) {
                var folderPath = ZoundRoutings.GetFolderFromClipPath(clipPath);
                ZoundDictionary.runtimeClipFolders.Add(clip, folderPath);
            }
#endif
            audioSource.clip = clip;

            var zoundRoutings = ZoundsProject.Instance.zoundRoutings;
            var mixerGroup = zoundRoutings.GetRouting(zound
#if ZOUNDS_CONSIDER_FOLDERS
                , clip, clipPath
#endif
                );
            audioSource.outputAudioMixerGroup = mixerGroup;
#endif
            base.OnStart(timeOffset);
        }

    }

}
