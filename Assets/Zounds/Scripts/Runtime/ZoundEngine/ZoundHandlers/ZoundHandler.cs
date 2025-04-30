using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal interface IZoundHandler {
        float totalDuration { get; }
        float currentTime { get; }
        public bool isDelayFinished { get; }
        void Init();
        void OnStart(float timeOffset);
        void OnPause();
        void OnResume();
        void OnKill();
        void OnFadeAndKill(float fadeDuration);
        /// <summary>
        /// Handle zound playing.
        /// </summary>
        /// <returns>Returns true if the handler request itself to be killed.</returns>
        bool OnUpdate(float deltaDspTime);
    }

    internal class ZoundHandler<TZound> : IZoundHandler where TZound : Zound {

        private TZound m_zound;
        private AudioSource m_audioSource;
        private float m_selfVolume;

        private float latestTime;
        private float m_currentTime;
        private float m_totalDuration;

        private bool isFadingOut;
        private float fadeStartTime;
        private float fadeStartVolume;
        private float fadeDuration;

        protected ZoundArgs args;
        private float delayTimer;
        private bool m_isDelayFinished;

        public ZoundHandler(TZound zound, AudioSource audioSource, ZoundArgs zoundArgs) {
            m_zound = zound;
            m_audioSource = audioSource;
            args = zoundArgs;

            if (args.volumeOverride >= 0f) {
                m_selfVolume = args.volumeOverride;
            }
            else {
                if (useFixedAverageVolumeAndPitch) {
                    m_selfVolume = (zound.minVolume + zound.maxVolume) / 2f;
                }
                else {
                    m_selfVolume = Random.Range(zound.minVolume, zound.maxVolume);
                }
            }
            m_audioSource.volume = m_selfVolume * ZoundEngine.GetMasterVolume();

            if (args.pitchOverride >= 0f) {
                m_audioSource.pitch = args.pitchOverride;
            }
            else {
                if (useFixedAverageVolumeAndPitch) {
                    m_audioSource.pitch = (zound.minPitch + zound.maxPitch) / 2f;
                }
                else {
                    m_audioSource.pitch = Random.Range(zound.minPitch, zound.maxPitch);
                }
            }

            //Debug.Log("Set " + zound.name + ": " + m_audioSource.pitch);
        }

        protected TZound zound => m_zound;
        protected AudioSource audioSource => m_audioSource;
        protected float selfVolume => m_selfVolume;
        public bool isDelayFinished => m_isDelayFinished;
        protected bool useFixedAverageVolumeAndPitch => args.useFixedAverageValues;
        public float currentTime => m_currentTime;
        public float totalDuration => m_totalDuration;

        public void Init() {
            m_totalDuration = PrepareAndCalculateDuration();
        }

        public virtual void OnStart(float timeOffset) {
            float offsetAfterDelay = timeOffset - args.delay;

            if (offsetAfterDelay >= 0) {
                m_currentTime = offsetAfterDelay;
                delayTimer = args.delay;
            }
            else {
                m_currentTime = 0f;
                delayTimer = timeOffset;
            }
            latestTime = m_currentTime;

            if (!ReferenceEquals(m_audioSource.clip, null)) {
                m_audioSource.time = m_currentTime;
            }

            m_isDelayFinished = false;
        }

        public virtual void OnPause() {
            m_audioSource.Pause();
        }

        public virtual void OnResume() {
            m_audioSource.UnPause();
        }

        public virtual void OnKill() {
            m_audioSource.Stop();
        }

        public virtual void OnFadeAndKill(float fadeDuration) {
            this.fadeDuration = fadeDuration;
            isFadingOut = true;
            fadeStartTime = currentTime;
            fadeStartVolume = m_audioSource.volume;
        }

        public virtual bool OnUpdate(float deltaDspTime) {
            if (!m_isDelayFinished) {
                if (isFadingOut) return true;

                if (delayTimer >= args.delay) {
                    float timeStartOffset = delayTimer - args.delay;
                    OnPlayReady(timeStartOffset);
                    m_isDelayFinished = true;

                    if (ZoundEngine.IsCoolingDownAtTime(zound, Time.realtimeSinceStartup)) {
                        return true;
                    }
                    ZoundEngine.RecordLastPlayedTime(zound);

                }
                else {
                    delayTimer += deltaDspTime;
                    return false;
                }
            }

            if (currentTime > latestTime) latestTime = currentTime;

            if (latestTime >= totalDuration - 2 * deltaDspTime) {
                return true;
            }

            if (isFadingOut) {
                float t = (currentTime - fadeStartTime) / fadeDuration;
                t = Mathf.Clamp01(t);
                m_audioSource.volume = Mathf.Lerp(fadeStartVolume * ZoundEngine.GetMasterVolume(), 0, t);
                float endTime = fadeStartTime + fadeDuration - Mathf.Epsilon;
                if (currentTime >= endTime) {
                    return true;
                }
            }
            else {
                m_audioSource.volume = m_selfVolume * ZoundEngine.GetMasterVolume();
            }

            m_currentTime += deltaDspTime;
            if (m_currentTime > totalDuration) {
                m_currentTime = totalDuration;
            }
            return false;
        }

        protected virtual float PrepareAndCalculateDuration() {
            return ReferenceEquals(m_audioSource.clip, null) ? 0f : m_audioSource.clip.length / m_audioSource.pitch;
        }

        protected virtual void OnPlayReady(float timeStartOffset) {
            m_currentTime = timeStartOffset;
            if (!ReferenceEquals(m_audioSource.clip, null)) {
                m_audioSource.time = m_currentTime;
            }
            m_audioSource.Play();
        }
    }

}
