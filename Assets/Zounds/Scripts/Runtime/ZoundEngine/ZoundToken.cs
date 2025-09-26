using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    public class ZoundToken {

        public event System.Action onFrameUpdate;
        public event System.Action onComplete;

        public enum State {
            Playing, Paused, Killed, FadingOut
        }

        private Zound m_zound;
        private IZoundHandler m_handler;
        private AudioSource m_audioSource;
        private State m_state = State.Paused;
        private double m_lastDspTime;
        private bool m_isChildZound;

        public Zound zound => m_zound;
        public State state => m_state;
        public AudioSource audioSource => m_audioSource;
        internal List<AudioSource> audioSources => m_handler.GetAudioSources();
        public float duration => m_handler.totalDuration;
        public float time => m_handler.currentTime;
        public bool isDelayFinished => m_handler.isDelayFinished;
        public bool isChildZound => m_isChildZound;
        public int playedEntryIndex => m_handler.playedEntryIndex;

        internal float parentVolume { set => m_handler.parentVolume = value; }
        internal bool isRealtime => m_handler.isRealtime;

        public ZoundToken(Zound zound, AudioSource audioSource, ZoundArgs zoundArgs) {
            m_zound = zound;
            m_audioSource = audioSource;
            m_state = State.Paused;
            m_isChildZound = zoundArgs.isChild;

            if (zound is Klip klip) {
                m_handler = new KlipHandler(klip, audioSource, zoundArgs);
            }
            else if (zound is Zequence zequence) {
                m_handler = new ZequenceHandler(zequence, audioSource, zoundArgs);
            }
            else if (zound is Muzic muzic) {
                m_handler = new MuzicHandler(muzic, audioSource, zoundArgs);
            }
            else if (zound is ClipZound clipZound) {
                m_handler = new ClipZoundHandler(clipZound, audioSource, zoundArgs);
            }
            else {
                Debug.LogError("Invalid Zound type: " + zound.GetType()); // actually impossible, but need to fill "else"
                m_handler = null;
            }

            if (m_handler != null) {
                m_handler.Init();
                if (!isChildZound) {
                    ApplyMixerGroupToChildren(audioSource.outputAudioMixerGroup);
                }
            }
        }

        internal void ApplyMixerGroupToChildren(AudioMixerGroup mixerGroup) {
            m_handler?.ApplyMixerGroupToChildren(mixerGroup);
        }

        public void Start(float timeOffset = 0f) {
            if (m_state == State.Killed || m_state == State.FadingOut) {
                Debug.LogError("Invalid token to start: The token has been killed.");
                return;
            }
            m_lastDspTime = AudioSettings.dspTime;
            m_state = State.Playing;
            m_handler.OnStart(timeOffset);
        }

        public void Pause() {
            if (m_state == State.Killed || m_state == State.FadingOut) {
                Debug.LogError("Invalid token to pause: The token has been killed.");
                return;
            }
            if (m_state == State.Paused) return;
            m_state = State.Paused;
            m_handler.OnPause();
        }
        
        public void Resume() {
            if (m_state == State.Killed || m_state == State.FadingOut) {
                Debug.LogError("Invalid token to resume: The token has been killed.");
                return;
            }
            if (m_state == State.Playing) return;
            m_lastDspTime = AudioSettings.dspTime;
            m_state = State.Playing;
            m_handler.OnResume();
        }

        public void Kill() {
            if (m_state == State.Killed) return;
            m_state = State.Killed;
            m_handler.OnKill();
        }

        public void FadeAndKill(float fadeDuration) {
            if (m_state == State.Killed || m_state == State.FadingOut) return;
            m_state = State.FadingOut;
            m_handler.OnFadeAndKill(fadeDuration);
        }

        public void OnUpdate() {
            double currentDspTime = AudioSettings.dspTime;
            float deltaDspTime = (float)(currentDspTime - m_lastDspTime);
            m_lastDspTime = currentDspTime;
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                if (audioSource == null) {
                    m_state = State.Killed;
                    return;
                }
            }
#endif

            if (m_state == State.Playing || m_state == State.FadingOut) {
                if (m_handler.OnUpdate(deltaDspTime)) {
                    m_state = State.Killed;
                    onComplete?.Invoke();
                }
                else {
                    onFrameUpdate?.Invoke();
                }
            }
            else {
                onFrameUpdate?.Invoke();
            }

        }

    }

}
