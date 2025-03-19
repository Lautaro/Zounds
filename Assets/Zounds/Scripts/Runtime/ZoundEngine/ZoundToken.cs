using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zounds {

    public class ZoundToken {

        public enum State {
            Playing, Paused, Killed, FadingOut
        }

        private Zound m_zound;
        private IZoundHandler m_handler;
        private AudioSource m_audioSource;
        private State m_state = State.Paused;

        public Zound zound => m_zound;
        public State state => m_state;
        public AudioSource audioSource => m_audioSource;
        public float duration => m_handler.totalDuration;
        public float time => m_handler.currentTime;

        public ZoundToken(Zound zound, AudioSource audioSource) {
            m_zound = zound;
            m_audioSource = audioSource;
            m_state = State.Paused;

            if (zound is Klip klip) {
                m_handler = new KlipHandler(klip, audioSource);
            }
            else if (zound is Zequence zequence) {
                m_handler = new ZequenceHandler(zequence, audioSource);
            }
            else if (zound is Muzic muzic) {
                m_handler = new MuzicHandler(muzic, audioSource);
            }
            else if (zound is Randomizer randomizer) {
                m_handler = new RandomizerHandler(randomizer, audioSource);
            }
            else {
                Debug.LogError("Invalid Zound type: " + zound.GetType()); // actually impossible, but need to fill "else"
                m_handler = null;
            }
        }

        public void Start() {
            if (m_state == State.Killed || m_state == State.FadingOut) {
                Debug.LogError("Invalid token to start: The token has been killed.");
                return;
            }
            m_state = State.Playing;
            m_handler.OnStart();
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
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                if (audioSource == null) {
                    m_state = State.Killed;
                    return;
                }
            }
#endif

            if (m_state == State.Playing || m_state == State.FadingOut) {
                if (m_handler.OnUpdate()) {
                    m_state = State.Killed;
                }
            }
        }

    }

}
