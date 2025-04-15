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

        public enum SpectrumEditMode {
            Trim, VolumeEnvelope, PitchEnvelope
        }

        [SerializeField] private EditorWindow m_window;
        [SerializeField] private AudioClip m_clip;
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private SpectrumEditMode m_editMode;

        [SerializeField] private float m_trimStart;
        [SerializeField] private float m_trimEnd;
        [SerializeField] private Envelope m_volumeEnvelope;
        [SerializeField] private Envelope m_pitchEnvelope;

        private AudioClip originalClip;
        private bool isTrimStartDragged = false;
        private bool isTrimEndDragged = false;

        public AudioSource audioSource => m_audioSource;

        public AudioSpectrumView(EditorWindow window) {
            m_window = window;
            var audioSourceGO = new GameObject("AudioSpectrumPreviewer");
            audioSourceGO.hideFlags = HideFlags.HideAndDontSave;
            m_audioSource = audioSourceGO.AddComponent<AudioSource>();
            m_audioSource.playOnAwake = false;
            m_audioSource.loop = false;
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
        }

        public void DrawLayout() {
            if (originalClip == null) return;

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60f;
            GUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                var newEditMode = (SpectrumEditMode)EditorGUILayout.EnumPopup("Edit Mode", m_editMode);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(m_window, "change edit mode");
                    m_editMode = newEditMode;
                    EditorUtility.SetDirty(m_window);
                }

                if (m_editMode == SpectrumEditMode.VolumeEnvelope) {
                    GUILayout.Space(4f);
                    EditorGUI.BeginChangeCheck();
                    var enabled = EditorGUILayout.ToggleLeft("Enabled", m_volumeEnvelope.enabled, GUILayout.Width(65f));
                    if (EditorGUI.EndChangeCheck()) {
                        m_volumeEnvelope.enabled = enabled;
                        onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);
                    }
                }
                else if (m_editMode == SpectrumEditMode.PitchEnvelope) {
                    GUILayout.Space(4f);
                    EditorGUI.BeginChangeCheck();
                    var enabled = EditorGUILayout.ToggleLeft("Enabled", m_pitchEnvelope.enabled, GUILayout.Width(65f));
                    if (EditorGUI.EndChangeCheck()) {
                        m_pitchEnvelope.enabled = enabled;
                        onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
                    }
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

            if (drawPlayingSource) {
                AudioWaveformUtility.DrawPlayerHead(trimmedRect, m_audioSource);
                m_window.Repaint();
            }
            else if (m_editMode == SpectrumEditMode.Trim) {
                DrawTrimHandles(spectrumRect, trimStartHandleArea, trimEndHandleArea);
            }
            else if (m_editMode == SpectrumEditMode.VolumeEnvelope && m_volumeEnvelope.enabled) {
                if (EnvelopeGUI.Draw(trimmedRect, m_volumeEnvelope, new Color(0.1f, 0.7f, 0.1f))) {
                    onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);
                }
                m_window.Repaint();
            }
            else if (m_editMode == SpectrumEditMode.PitchEnvelope && m_pitchEnvelope.enabled) {
                if (EnvelopeGUI.Draw(trimmedRect, m_pitchEnvelope, new Color(0.9f, 0.2f, 0.1f))) {
                    onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
                }
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
            GUI.color = new Color32(252, 192, 7, 255);
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
            GUI.color = new Color(1, 1, 1, 0.75f);
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
            GUI.color = new Color(1, 1, 1, 0.75f);
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