using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    [System.Serializable]
    public class AudioSpectrumView {

        public System.Action<float> onTrimStartChanged;
        public System.Action<float> onTrimEndChanged;
        public System.Action<Envelope> onVolumeEnvelopeChanged;
        public System.Action<Envelope> onPitchEnvelopeChanged;

        private const float height = 100f;

        [SerializeField] private EditorWindow m_window;
        [SerializeField] private AudioClip m_clip;
        [SerializeField] private AudioSource m_audioSource;

        [SerializeField] private bool m_showTrim = true;
        [SerializeField] private bool m_showVolumeEnvelope = true;
        [SerializeField] private bool m_showPitchEnvelope = true;

        [SerializeField] private float m_trimStart;
        [SerializeField] private float m_trimEnd;
        [SerializeField] private Envelope m_volumeEnvelope;
        [SerializeField] private Envelope m_pitchEnvelope;

        private AudioClip originalClip;
        private bool isTrimStartDragged = false;
        private bool isTrimEndDragged = false;
        private EnvelopeGUI volumeEnvelopeGUI;
        private EnvelopeGUI pitchEnvelopeGUI;

        private static Texture m_visibleTexture;
        public static Texture visibleTexture {
            get {
                if (m_visibleTexture == null) {
                    m_visibleTexture = Resources.Load("AudioSpectrumIcons/Visible") as Texture;
                }
                return m_visibleTexture;
            }
        }

        private static Texture m_hiddenTexture;
        public static Texture hiddenTexture {
            get {
                if (m_hiddenTexture == null) {
                    m_hiddenTexture = Resources.Load("AudioSpectrumIcons/Hidden") as Texture;
                }
                return m_hiddenTexture;
            }
        }

        public AudioSource audioSource => m_audioSource;

        public AudioSpectrumView(EditorWindow window) {
            m_window = window;
            var audioSourceGO = new GameObject("AudioSpectrumPreviewer");
            audioSourceGO.hideFlags = HideFlags.HideAndDontSave;
            m_audioSource = audioSourceGO.AddComponent<AudioSource>();
            m_audioSource.playOnAwake = false;
            m_audioSource.loop = false;
            volumeEnvelopeGUI = new EnvelopeGUI() { name = "Volume" };
            pitchEnvelopeGUI = new EnvelopeGUI() { name = "Pitch" };
        }

        public void Destroy() {
            if (m_audioSource != null) {
                if (Application.isPlaying) {
                    GameObject.Destroy(m_audioSource.gameObject);
                }
                else {
                    GameObject.DestroyImmediate(m_audioSource.gameObject);
                }
                m_audioSource = null;
            }
            m_clip = null;
        }

        public float trimStart {
            get => m_trimStart;
            private set {
                m_trimStart = value;
                onTrimStartChanged?.Invoke(m_trimStart);
            }
        }

        public float trimEnd {
            get => m_trimEnd;
            private set {
                m_trimEnd = value;
                onTrimEndChanged?.Invoke(m_trimEnd);
            }
        }

        public void InitFromKlip(Klip klip) {
            originalClip = klip.audioClipRef.editorAsset as AudioClip;
            m_clip = klip.GetAudioClipReference().editorAsset as AudioClip;
            m_audioSource.clip = m_clip;
            m_trimStart = klip.trimStart;
            m_trimEnd = klip.trimEnd;
            m_volumeEnvelope = klip.volumeEnvelope.DeepCopy();
            m_pitchEnvelope = klip.pitchEnvelope.DeepCopy();
        }

        public void ResetStates() {
            isTrimStartDragged = false;
            isTrimEndDragged = false;
            if (volumeEnvelopeGUI != null) {
                volumeEnvelopeGUI.ResetStates();
            }
            if (pitchEnvelopeGUI != null) {
                pitchEnvelopeGUI.ResetStates();
            }
        }

        public void DrawLayout(IEnumerable<ZoundToken> playingTokens = null) {
            if (originalClip == null) return;

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60f;
            GUILayout.BeginHorizontal();
            {
                var lineHeight = EditorGUIUtility.singleLineHeight;
                if (GUILayout.Button(m_showTrim ? visibleTexture : hiddenTexture, GUILayout.Width(25f), GUILayout.Height(lineHeight))) {
                    Undo.RecordObject(m_window, "toggle show trim");
                    m_showTrim = !m_showTrim;
                    EditorUtility.SetDirty(m_window);
                }
                EditorGUILayout.LabelField("Trim", GUILayout.Width(30f));

                GUILayout.Space(4f);
                if (GUILayout.Button(m_showVolumeEnvelope ? visibleTexture : hiddenTexture, GUILayout.Width(25f), GUILayout.Height(lineHeight))) {
                    Undo.RecordObject(m_window, "toggle show volume envelope");
                    m_showVolumeEnvelope = !m_showVolumeEnvelope;
                    EditorUtility.SetDirty(m_window);
                }
                EditorGUILayout.LabelField("Volume Envelope", GUILayout.Width(101f));
                EditorGUI.BeginChangeCheck();
                var enabled = EditorGUILayout.ToggleLeft("Enabled", m_volumeEnvelope.enabled, GUILayout.Width(65f));
                if (EditorGUI.EndChangeCheck()) {
                    m_volumeEnvelope.enabled = enabled;
                    onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);
                }

                GUILayout.Space(4f);
                if (GUILayout.Button(m_showPitchEnvelope ? visibleTexture : hiddenTexture, GUILayout.Width(25f), GUILayout.Height(lineHeight))) {
                    Undo.RecordObject(m_window, "toggle show pitch envelope");
                    m_showPitchEnvelope = !m_showPitchEnvelope;
                    EditorUtility.SetDirty(m_window);
                }
                EditorGUILayout.LabelField("Pitch Envelope", GUILayout.Width(90f));
                EditorGUI.BeginChangeCheck();
                enabled = EditorGUILayout.ToggleLeft("Enabled", m_pitchEnvelope.enabled, GUILayout.Width(65f));
                if (EditorGUI.EndChangeCheck()) {
                    m_pitchEnvelope.enabled = enabled;
                    onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
                }
            }
            GUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = labelWidth;
            GUILayout.Space(4f);

            Rect spectrumRect = DrawWaveformSpectrum(originalClip, 0f);
            Rect trimStartHandleArea = DrawTrimStartDim(originalClip.length, spectrumRect);
            Rect trimEndHandleArea = DrawTrimEndDim(originalClip.length, ref spectrumRect);

            bool drawPlayingSource = false;
            if (m_audioSource != null && m_audioSource.clip != null) {
                if (m_audioSource.isPlaying) {
                    drawPlayingSource = true;
                }
            }

            Rect trimmedRect = new Rect(trimStartHandleArea.x, spectrumRect.y,
                trimEndHandleArea.x - trimStartHandleArea.x, spectrumRect.height);

            bool needsRepaint = false;

            if (drawPlayingSource) {
                needsRepaint = true;
                float timePercentage;
                if (m_pitchEnvelope.enabled) {
                    float totalTime = trimEnd - trimStart;
                    float integrationSteps = AudioRenderUtility.GetOptimalIntegrationSteps(totalTime);
                    float step = totalTime / integrationSteps;

                    float t = 0f;
                    float renderedTime = 0f;

                    while (t <= totalTime && renderedTime < m_audioSource.time) {
                        float pitch = m_pitchEnvelope.Evaluate(t / totalTime);
                        float dt = step;
                        renderedTime += dt / pitch;
                        t += dt;
                    }
                    //Debug.Log("Pitch: " + (t / totalTime) + " : " + m_pitchEnvelope.Evaluate(t / totalTime));

                    timePercentage = t / totalTime;
                    //Debug.Log(t + " / " + totalTime);
                }
                else {
                    timePercentage = m_audioSource.time / m_audioSource.clip.length;
                }
                AudioWaveformUtility.DrawPlayerHead(trimmedRect, timePercentage);
            }

            if (playingTokens != null && playingTokens.Count() > 0) {
                foreach (var token in playingTokens) {
                    if (token == null || token.state == ZoundToken.State.Killed) continue;
                    
                    if (token.zound is Klip klip && klip.pitchEnvelope.enabled) {
                        float totalTime = klip.trimEnd - klip.trimStart;
                        float integrationSteps = AudioRenderUtility.GetOptimalIntegrationSteps(totalTime);
                        float step = totalTime / integrationSteps;

                        float t = 0f;
                        float renderedTime = 0f;

                        while (t <= totalTime && renderedTime < token.audioSource.time) {
                            float pitch = klip.pitchEnvelope.Evaluate(t / totalTime);
                            float dt = step;
                            renderedTime += dt / pitch;
                            t += dt;
                        }
                        //Debug.Log("Pitch: " + (t / totalTime) + " : " + m_pitchEnvelope.Evaluate(t / totalTime));

                        AudioWaveformUtility.DrawPlayerHead(trimmedRect, t / totalTime);
                    }
                    else {
                        AudioWaveformUtility.DrawPlayerHead(trimmedRect, token.time / token.duration);
                    }
                    needsRepaint = true;
                }
            }

            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;

            if (m_showTrim) {
                DrawTrimHandles(spectrumRect, trimStartHandleArea, trimEndHandleArea);
            }
            bool allowAddPointByDoubleClick = !(m_showVolumeEnvelope && m_showPitchEnvelope);
            if (m_showVolumeEnvelope) {
                needsRepaint = true;
                if (volumeEnvelopeGUI.Draw(trimmedRect, m_volumeEnvelope, editorStyle.volumeEnvelopeColor, allowAddPointByDoubleClick)) {
                    onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);
                }
            }
            if (m_showPitchEnvelope) {
                needsRepaint = true;
                if (pitchEnvelopeGUI.Draw(trimmedRect, m_pitchEnvelope, editorStyle.pitchEnvelopeColor, allowAddPointByDoubleClick)) {
                    onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
                }
            }

            if (needsRepaint) {
                m_window.Repaint();
            }
        }

        #region BASE-VIEW
        private Rect DrawWaveformSpectrum(AudioClip audioClip, float upperOffset) {
            var spectrumRect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            var guiColor = GUI.color;
            GUI.Box(spectrumRect, GUIContent.none);

            var textureRect = spectrumRect;
            if (textureRect.height > 1 && textureRect.width > 1) {
                textureRect.x += 4;
                textureRect.width -= 8;
                textureRect.y += 4 + upperOffset;
                textureRect.height -= 8 + upperOffset;
            }
            GUI.color = ZoundsProject.Instance.projectSettings.editorStyle.klipWaveformBGColor;
            GUI.DrawTexture(textureRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            var audioTexture = AudioWaveformUtility.GetWaveformSpectrumTexture(audioClip, Mathf.FloorToInt(textureRect.width), Mathf.FloorToInt(textureRect.height), Color.black);
            GUI.DrawTexture(textureRect, audioTexture);

            return textureRect;
        }

        private Rect DrawTrimStartDim(float clipDuration, Rect spectrumRect) {
            float trimStartWidth = (trimStart / clipDuration) * spectrumRect.width;
            var trimStartHandleArea = spectrumRect;
            trimStartHandleArea.x += trimStartWidth;
            trimStartHandleArea.width = 2f;

            Color guiColor = GUI.color;
            var trimmedRect = new Rect(spectrumRect.x, spectrumRect.y, trimStartWidth, spectrumRect.height);
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(trimmedRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            return trimStartHandleArea;
        }

        private Rect DrawTrimEndDim(float clipDuration, ref Rect spectrumRect) {
            float trimEndWidth = (trimEnd / clipDuration) * spectrumRect.width;
            var trimEndHandleArea = spectrumRect;
            trimEndHandleArea.x += trimEndWidth;
            trimEndHandleArea.width = 2f;

            Color guiColor = GUI.color;
            var trimmedRect = new Rect(trimEndHandleArea.x, spectrumRect.y, (spectrumRect.width - trimEndWidth), spectrumRect.height);
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(trimmedRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            return trimEndHandleArea;
        }
        #endregion

        #region TRIM-VIEW
        private void DrawTrimHandles(Rect spectrumRect, Rect trimStartHandleArea, Rect trimEndHandleArea) {
            if (trimEnd < trimStart) {
                trimEnd = trimStart;
            }

            if (trimEnd >= originalClip.length) {
                if (trimStart < originalClip.length) {
                    HandleResizeTrimEnd(trimEndHandleArea, originalClip.length, spectrumRect);
                }
            }
            else {
                HandleResizeTrimEnd(trimEndHandleArea, originalClip.length, spectrumRect);
            }

            if (trimStart == 0) {
                if (trimEnd > 0) {
                    HandleResizeTrimStart(trimStartHandleArea, originalClip.length, spectrumRect);
                }
            }
            else {
                HandleResizeTrimStart(trimStartHandleArea, originalClip.length, spectrumRect);
            }
        }

        private void HandleResizeTrimStart(Rect trimStartHandleArea, float clipDuration, Rect spectrumRect) {
            Color guiColor = GUI.color;
            GUI.color = new Color(1, 1, 1, GUI.enabled? 0.75f : 0.35f);
            GUI.DrawTexture(trimStartHandleArea, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;

            EditorGUIUtility.AddCursorRect(trimStartHandleArea, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type) {
                case EventType.MouseDown:
                    if (e.button == 0) {
                        if (trimStartHandleArea.Contains(e.mousePosition)) {
                            isTrimStartDragged = true;
                            isTrimEndDragged = false;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    break;

                case EventType.MouseUp:
                case EventType.Ignore:
                    if (isTrimStartDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var newTrimStart = newPosX / spectrumRect.width * clipDuration;

                        if (newTrimStart < 0) newTrimStart = 0;
                        else if (newTrimStart >= clipDuration) newTrimStart = clipDuration;
                        trimStart = newTrimStart;
                    }
                    isTrimStartDragged = false;
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && isTrimStartDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var newTrimStart = newPosX / spectrumRect.width * clipDuration;

                        if (newTrimStart < 0) newTrimStart = 0;
                        else if (newTrimStart >= clipDuration) newTrimStart = clipDuration;
                        trimStart = newTrimStart;
                        e.Use();
                    }
                    break;

            }
        }

        private void HandleResizeTrimEnd(Rect trimEndHandleArea, float clipDuration, Rect spectrumRect) {
            Color guiColor = GUI.color;
            GUI.color = new Color(1, 1, 1, GUI.enabled ? 0.75f : 0.35f);
            GUI.DrawTexture(trimEndHandleArea, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;

            EditorGUIUtility.AddCursorRect(trimEndHandleArea, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type) {
                case EventType.MouseDown:
                    if (e.button == 0) {
                        if (trimEndHandleArea.Contains(e.mousePosition)) {
                            isTrimEndDragged = true;
                            isTrimStartDragged = false;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    break;

                case EventType.MouseUp:
                case EventType.Ignore:
                    if (isTrimEndDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var newTrimEnd = newPosX / spectrumRect.width * clipDuration;

                        if (newTrimEnd < 0) newTrimEnd = 0;
                        else if (newTrimEnd >= clipDuration) newTrimEnd = clipDuration;
                        trimEnd = newTrimEnd;
                    }
                    isTrimEndDragged = false;
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && isTrimEndDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var newTrimEnd = newPosX / spectrumRect.width * clipDuration;

                        if (newTrimEnd < 0) newTrimEnd = 0;
                        else if (newTrimEnd >= clipDuration) newTrimEnd = clipDuration;
                        trimEnd = newTrimEnd;
                        e.Use();
                    }
                    break;

            }
        }
        #endregion

    }

}