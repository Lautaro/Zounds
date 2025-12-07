using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    internal interface IZoundHandler {
        float totalDuration { get; }
        float currentTime { get; }
        bool isDelayFinished { get; }
        float parentVolume { get; set; }
        int playedEntryIndex { get; }
        bool isRealtime { get; }
        List<AudioSource> GetAudioSources();
        void Init();
        void ApplyMixerGroupToChildren(AudioMixerGroup mixerGroup);
        void OnStart(float timeOffset, float fadeDuration, System.Action onFadeComplete);
        void OnPause();
        void OnFadeAndPause(float fadeDuration, System.Action onFadeComplete);
        void OnResume(float fadeDuratio, System.Action onFadeCompleten);
        void OnKill();
        void OnFadeAndKill(float fadeDuration, System.Action onFadeComplete);
        /// <summary>
        /// Update and check what's to be done next.
        /// </summary>
        /// <param name="deltaDspTime"></param>
        /// <returns>0: Nothing happens. 1: Needs to be killed. 2: Needs to be paused.</returns>
        int OnUpdate(float deltaDspTime);
    }

    internal class ZoundHandler<TZound> : IZoundHandler where TZound : Zound {

        private TZound m_zound;
        private AudioSource m_audioSource;
        private float m_selfVolume;

        private float latestTime;
        private float m_currentTime;
        private float m_totalDuration;

        public enum FadeState {
            None, FadingOut, FadingIn
        }

        private FadeState fadeState;
        private System.Action onFadeComplete;
        private float fadeStartTime;
        private float fadeInitialVolume;
        private float fadeDuration;
        private bool killOnFadeOut;
        private bool isPaused;

        protected ZoundArgs args;
        private float delayTimer;
        private bool m_isDelayFinished;

        public float parentVolume { get; set; } = 1f;
        public virtual bool isRealtime => false;

        public virtual List<AudioSource> GetAudioSources() { return new List<AudioSource> { m_audioSource }; }

        public ZoundHandler(TZound zound, AudioSource audioSource, ZoundArgs zoundArgs) {
            m_zound = zound;
            m_audioSource = audioSource;
            args = zoundArgs;

            if (args.volumeOverride >= 0f) {
                m_selfVolume = args.volumeOverride;
            }
            else {
                //if (useFixedAverageVolumeAndPitch) {
                //    m_selfVolume = (zound.minVolume + zound.maxVolume) / 2f;
                //}
                //else {
                //    m_selfVolume = Random.Range(zound.minVolume, zound.maxVolume);
                //}
                // no more middle values
                m_selfVolume = Random.Range(zound.minVolume, zound.maxVolume);
            }
            m_audioSource.volume = m_selfVolume * ZoundEngine.GetMasterVolume();

            if (args.pitchOverride >= 0f) {
                m_audioSource.pitch = args.pitchOverride;
            }
            else {
                //if (useFixedAverageVolumeAndPitch) {
                //    m_audioSource.pitch = (zound.minPitch + zound.maxPitch) / 2f;
                //}
                //else {
                //    m_audioSource.pitch = Random.Range(zound.minPitch, zound.maxPitch);
                //}
                // no more middle values
                m_audioSource.pitch = Random.Range(zound.minPitch, zound.maxPitch);
            }

            //Debug.Log("Set " + zound.name + ": " + m_audioSource.pitch);

            if (zoundArgs.overrideMixerGroup) {
                audioSource.outputAudioMixerGroup = zoundArgs.mixerGroupOverride;
            }
        }

        protected TZound zound => m_zound;
        protected AudioSource audioSource => m_audioSource;
        protected float selfVolume => m_selfVolume;
        public bool isDelayFinished => m_isDelayFinished;
        protected bool useFixedAverageVolumeAndPitch => args.useFixedAverageValues;
        public float currentTime { get => m_currentTime; protected set { m_currentTime = value; } }
        public float totalDuration => m_totalDuration;
        public virtual int playedEntryIndex => 0;

        public void Init() {
            m_totalDuration = PrepareAndCalculateDuration();
            if (args.overrideDuration > 0f) {
                m_totalDuration = args.overrideDuration;
            }
            parentVolume = 1f;
        }

        public virtual void ApplyMixerGroupToChildren(AudioMixerGroup mixerGroup) {
            audioSource.outputAudioMixerGroup = mixerGroup;
        }

        public virtual void OnStart(float timeOffset, float fadeDuration, System.Action onFadeComplete) {
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
            isPaused = false;

            if (fadeDuration > Mathf.Epsilon) {
                this.fadeDuration = fadeDuration;
                this.onFadeComplete = onFadeComplete;
                fadeState = FadeState.FadingIn;
                fadeStartTime = currentTime;
                fadeInitialVolume = 0f;
            }
            else {
                fadeState = FadeState.None;
            }
        }

        public virtual void OnPause() {
            isPaused = true;
            m_audioSource.Pause();
        }

        public virtual void OnFadeAndPause(float fadeDuration, System.Action onFadeComplete) {
            this.fadeDuration = fadeDuration;
            this.onFadeComplete = onFadeComplete;
            fadeState = FadeState.FadingOut;
            fadeStartTime = currentTime;
            fadeInitialVolume = m_audioSource.volume;
            killOnFadeOut = false;
        }

        public virtual void OnResume(float fadeDuration, System.Action onFadeComplete) {
            m_audioSource.UnPause();
            if (fadeDuration > Mathf.Epsilon) {
                this.fadeDuration = fadeDuration;
                this.onFadeComplete = onFadeComplete;
                fadeState = FadeState.FadingIn;
                fadeStartTime = currentTime;
                fadeInitialVolume = isPaused ? 0f : m_audioSource.volume;
            }
            else {
                fadeState = FadeState.None;
            }
            isPaused = false;
        }

        public virtual void OnKill() {
            m_audioSource.Stop();
        }

        public virtual void OnFadeAndKill(float fadeDuration, System.Action onFadeComplete) {
            if (isPaused) {
                m_audioSource.UnPause();
            }
            isPaused = false;
            this.fadeDuration = fadeDuration;
            this.onFadeComplete = onFadeComplete;
            fadeState = FadeState.FadingOut;
            fadeStartTime = currentTime;
            fadeInitialVolume = m_audioSource.volume;
            killOnFadeOut = true;
        }

        /// <summary>
        /// Update and check what's to be done next.
        /// </summary>
        /// <param name="deltaDspTime"></param>
        /// <returns>0: Nothing happens. 1: Needs to be killed. 2: Needs to be paused.</returns>
        public virtual int OnUpdate(float deltaDspTime) {
            if (!m_isDelayFinished) {
                if (fadeState == FadeState.FadingOut && killOnFadeOut) return 1;

                if (delayTimer >= args.delay) {
                    float timeStartOffset = delayTimer - args.delay;
                    float childFadeDuration;
                    if (fadeState == FadeState.FadingIn) childFadeDuration = fadeDuration;
                    else childFadeDuration = 0f;
                    OnPlayReady(timeStartOffset, childFadeDuration);
                    m_isDelayFinished = true;

                    if (!args.ignoreCooldown) {
                        if (ZoundEngine.IsCoolingDownAtTime(zound, Time.realtimeSinceStartup)) {
                            OnKill();
                            return 1;
                        }
                        ZoundEngine.RecordLastPlayedTime(zound);
                    }

                }
                else {
                    delayTimer += deltaDspTime;
                    return 0;
                }
            }

            return OnPlayUpdate(deltaDspTime);
        }

        /// <summary>
        /// Update when the zound is actually playing (delay finished).
        /// </summary>
        /// <param name="deltaDspTime"></param>
        /// <returns>0: Nothing happens. 1: Needs to be killed. 2: Needs to be paused.</returns>
        protected virtual int OnPlayUpdate(float deltaDspTime) {
            if (currentTime > latestTime) latestTime = currentTime;

            if (latestTime >= totalDuration - 2 * deltaDspTime) {
                OnCompleteDuration();
                return 1;
            }

            if (fadeState == FadeState.FadingOut) {
                float t = (currentTime - fadeStartTime) / fadeDuration;
                t = Mathf.Clamp01(t);
                m_audioSource.volume = parentVolume * Mathf.Lerp(fadeInitialVolume * ZoundEngine.GetMasterVolume(), 0, t);
                float endTime = fadeStartTime + fadeDuration - Mathf.Epsilon;
                if (killOnFadeOut) {
                    if (currentTime >= endTime) {
                        CompleteFade();
                        return 1;
                    }
                }
                else {
                    if (t >= 1f) {
                        fadeState = FadeState.None;
                        OnPause();
                        CompleteFade();
                    }
                }
            }
            else if (fadeState == FadeState.FadingIn) {
                float t = (currentTime - fadeStartTime) / fadeDuration;
                t = Mathf.Clamp01(t);
                float masterVolume = ZoundEngine.GetMasterVolume();
                m_audioSource.volume = parentVolume * Mathf.Lerp(fadeInitialVolume * masterVolume, m_selfVolume * masterVolume, t);
                if (t >= 1f - Mathf.Epsilon) {
                    fadeState = FadeState.None;
                    CompleteFade();
                }
            }
            else {
                m_audioSource.volume = parentVolume * m_selfVolume * ZoundEngine.GetMasterVolume();
            }

            if (!isPaused) {
                m_currentTime += deltaDspTime;
                if (m_currentTime > totalDuration) {
                    m_currentTime = totalDuration;
                }
            }
            return isPaused ? 2 : 0;
        }

        private void CompleteFade() {
            var action = onFadeComplete;
            onFadeComplete = null;
            action?.Invoke();
        }

        protected virtual void OnCompleteDuration() {
            
        }

        protected virtual float PrepareAndCalculateDuration() {
            return ReferenceEquals(m_audioSource.clip, null) ? 0f : m_audioSource.clip.length / m_audioSource.pitch;
        }

        protected virtual void OnPlayReady(float timeStartOffset, float childFadeDuration) {
            m_currentTime = timeStartOffset;
            if (!ReferenceEquals(m_audioSource.clip, null)) {
                if (m_currentTime > m_audioSource.clip.length) {
                    m_audioSource.time = m_audioSource.clip.length;
                    return;
                }
                else {
                    m_audioSource.time = m_currentTime;
                }
            }
            m_audioSource.Play();
        }
    }

}
