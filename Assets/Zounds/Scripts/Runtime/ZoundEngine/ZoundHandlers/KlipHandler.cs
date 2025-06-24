using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class KlipHandler : ZoundHandler<Klip> {

        public KlipHandler(Klip klip, AudioSource audioSource, ZoundArgs zoundArgs) : base(klip, audioSource, zoundArgs) {
#if ADDRESSABLES_INSTALLED
            var clip = ZoundDictionary.GetOrLoadClip(zound.GetAudioClipReference());
#if ZOUNDS_CONSIDER_FOLDERS
            var clipPath = zound.GetAudioClipPath();

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
        }

    }

}