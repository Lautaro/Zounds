using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    internal interface IZoundHandler {
        float totalDuration { get; }
        float currentTime { get; }
        void OnStart();
        void OnPause();
        void OnResume();
        void OnKill();
        void OnFadeAndKill(float fadeDuration);
        /// <summary>
        /// Handle zound playing.
        /// </summary>
        /// <returns>Returns true if the handler request itself to be killed.</returns>
        bool OnUpdate();
    }

    internal class ZoundHandler<TZound> : IZoundHandler where TZound : Zound {

        private TZound m_zound;
        private AudioSource m_audioSource;
        private float m_selfVolume;

        private float latestTime;

        private bool isFadingOut;
        private float fadeStartTime;
        private float fadeStartVolume;
        private float fadeDuration;


        public ZoundHandler(TZound zound, AudioSource audioSource) {
            m_zound = zound;
            m_audioSource = audioSource;
        }

        protected TZound zound => m_zound;
        protected AudioSource audioSource => m_audioSource;
        protected float selfVolume => m_selfVolume;
        public virtual float totalDuration => ReferenceEquals(m_audioSource.clip, null) ? 0f : m_audioSource.clip.length;
        public virtual float currentTime => ReferenceEquals(m_audioSource.clip, null) ? 0f : m_audioSource.time;

        public virtual void OnStart() {
            latestTime = 0f;
            m_selfVolume = Random.Range(zound.minVolume, zound.maxVolume);
            m_audioSource.volume = m_selfVolume * ZoundEngine.GetMasterVolume();
            m_audioSource.pitch = Random.Range(zound.minPitch, zound.maxPitch);
            if (!ReferenceEquals(m_audioSource.clip, null)) {
                m_audioSource.time = 0f;
            }
            m_audioSource.Play();
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
            fadeStartTime = ReferenceEquals(m_audioSource.clip, null) ? 0f : m_audioSource.time;
            fadeStartVolume = m_audioSource.volume;
        }

        public virtual bool OnUpdate() {
            if (ReferenceEquals(m_audioSource.clip, null)) {
                latestTime = 0f;
            }
            else {
                if (m_audioSource.time > latestTime) latestTime = m_audioSource.time;
            }
            if (latestTime >= totalDuration - 2*Time.unscaledDeltaTime) {
                return true;
            }

            if (isFadingOut) {
                if (ReferenceEquals(m_audioSource.clip, null)) {
                    m_audioSource.volume = 0f;
                    return true;
                }
                else {
                    float t = (m_audioSource.time - fadeStartTime) / fadeDuration;
                    t = Mathf.Clamp01(t);
                    m_audioSource.volume = Mathf.Lerp(fadeStartVolume * ZoundEngine.GetMasterVolume(), 0, t);
                    float endTime = fadeStartTime + fadeDuration - Mathf.Epsilon;
                    if (m_audioSource.time >= endTime) {
                        return true;
                    }
                }
            }
            else {
                m_audioSource.volume = m_selfVolume * ZoundEngine.GetMasterVolume();
            }

            return false;
        }

    }

}
