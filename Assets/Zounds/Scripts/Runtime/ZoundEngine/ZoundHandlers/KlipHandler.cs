using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal class KlipHandler : ZoundHandler<Klip> {

        private float baseVolume;
        private float basePitch;
        private bool m_isRealtime;

        public override bool isRealtime => m_isRealtime;

        public KlipHandler(Klip klip, AudioSource audioSource, ZoundArgs zoundArgs) : base(klip, audioSource, zoundArgs) {
            m_isRealtime = zound.needsRender;

#if ADDRESSABLES_INSTALLED
            var clip = m_isRealtime? ZoundDictionary.GetOrLoadClip(zound.audioClipRef) : ZoundDictionary.GetOrLoadClip(zound.GetAudioClipReference());
#if ZOUNDS_CONSIDER_FOLDERS
            var clipPath = zound.GetAudioClipPath();

            if (!ZoundDictionary.runtimeClipFolders.ContainsKey(clip)) {
                var folderPath = ZoundRoutings.GetFolderFromClipPath(clipPath);
                ZoundDictionary.runtimeClipFolders.Add(clip, folderPath);
            }
#endif
            audioSource.clip = clip;

            if (!zoundArgs.overrideMixerGroup) {
                var zoundRoutings = ZoundsProject.Instance.zoundRoutings;
                var mixerGroup = zoundRoutings.GetRouting(zound
#if ZOUNDS_CONSIDER_FOLDERS
                , clip, clipPath
#endif
                    );
                audioSource.outputAudioMixerGroup = mixerGroup;
            }
#endif

            basePitch = audioSource.pitch;
            baseVolume = audioSource.volume;
        }

        protected override int OnPlayUpdate(float deltaDspTime) {
            if (m_isRealtime) {
                if (zound.pitchEnvelope != null && zound.pitchEnvelope.enabled) {
                    // handle realtime envelope calculation
                    float t = currentTime / totalDuration;
                    var envelopPitch = zound.pitchEnvelope.Evaluate(t);
                    var finalPitch = basePitch * envelopPitch;
                    audioSource.pitch = finalPitch;
                    deltaDspTime *= finalPitch;
                }
                else {
                    audioSource.pitch = basePitch;
                    deltaDspTime *= basePitch;
                }
            }

            int nextTreatment = base.OnPlayUpdate(deltaDspTime);

            if (m_isRealtime) {
                if (zound.volumeEnvelope != null && zound.volumeEnvelope.enabled) {
                    // handle realtime envelope calculation
                    float t = currentTime / totalDuration;
                    var volume = zound.volumeEnvelope.Evaluate(t);
                    audioSource.volume = baseVolume * volume;
                }
                else {
                    audioSource.volume = baseVolume;
                }
            }
            return nextTreatment;
        }

        protected override float PrepareAndCalculateDuration() {
            if (m_isRealtime) {
                return zound.trimEnd - zound.trimStart;
            }
            else {
                return base.PrepareAndCalculateDuration();
            }
        }

        protected override void OnPlayReady(float timeStartOffset, float childFadeDuration) {
            if (m_isRealtime) {
                base.OnPlayReady(timeStartOffset + zound.trimStart, childFadeDuration);
                currentTime = timeStartOffset;
            }
            else {
                base.OnPlayReady(timeStartOffset, childFadeDuration);
            }
        }

        protected override void OnCompleteDuration() {
            if (m_isRealtime) {
                audioSource.Stop();
            }
        }

    }

}