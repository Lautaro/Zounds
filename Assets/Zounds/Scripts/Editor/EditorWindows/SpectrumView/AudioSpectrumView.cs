using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    [System.Serializable]
    public class AudioSpectrumView {

        public System.Action<bool> onTrimEnabledChanged;
        public System.Action<float> onTrimStartChanged;
        public System.Action<float> onTrimEndChanged;
        public System.Action<bool> onClampToTrimChanged;
        public System.Action<Envelope> onVolumeEnvelopeChanged;
        public System.Action<Envelope> onPitchEnvelopeChanged;
        public System.Action<bool> onVolumeEnabledChanged;
        public System.Action<bool> onPitchEnabledChanged;

        // Fired on MouseDown before any mutation — caller should call Undo.RecordObject here.
        public System.Action onTrimDragStarted;
        public System.Action onVolumeDragStarted;
        public System.Action onPitchDragStarted;

        [SerializeField] private float m_height = 100f;

        public float height {
            get => m_height;
            set {
                if (Mathf.Approximately(m_height, value)) return;
                m_height = value;
                EditorUtility.SetDirty(m_window);
            }
        }

        [SerializeField] private EditorWindow m_window;
        [SerializeField] private AudioClip m_clip;
        [SerializeField] private AudioSource m_audioSource;

        [SerializeField] private bool m_trimEnabled = true;
        [SerializeField] private bool m_showVolumeEnvelopeHandles = true;
        [SerializeField] private bool m_showPitchEnvelopeHandles = true;

        [SerializeField] private float m_trimStart;
        [SerializeField] private float m_trimEnd;
        [SerializeField] private bool m_clampToTrim = true;
        [SerializeField] private Envelope m_volumeEnvelope;
        [SerializeField] private Envelope m_pitchEnvelope;

        private AudioClip originalClip;
        private bool isTrimStartDragged = false;
        private bool isTrimEndDragged = false;
        private bool isTrimBothDragged = false;
        private float dragTrimDistance = 0f;
        private float dragMouseOffset = 0f;
        private EnvelopeGUI volumeEnvelopeGUI;
        private EnvelopeGUI pitchEnvelopeGUI;

        private static Texture m_eyeOpenIcon;
        public static Texture eyeOpenIcon {
            get {
                if (m_eyeOpenIcon == null) {
                    m_eyeOpenIcon = Resources.Load("AudioSpectrumIcons/Visible") as Texture;
                }
                return m_eyeOpenIcon;
            }
        }

        private static Texture m_hiddenTexture;
        public static Texture eyeClosedIcon {
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

            volumeEnvelopeGUI.onDragStarted = () => onVolumeDragStarted?.Invoke();
            volumeEnvelopeGUI.onDragUpdated = () => onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);
            volumeEnvelopeGUI.onMutated = () => onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);

            pitchEnvelopeGUI.onDragStarted = () => onPitchDragStarted?.Invoke();
            pitchEnvelopeGUI.onDragUpdated = () => onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
            pitchEnvelopeGUI.onMutated = () => onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
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
            AudioClip newClip = null;
            try { 
                originalClip = klip.audioClipRef.editorAsset as AudioClip;
                newClip = klip.GetAudioClipReference().editorAsset as AudioClip;
            } catch { }

            if (newClip == null) return;

            if (m_clip != newClip) {
                AudioWaveformUtility.ClearCache(newClip);
            }
            m_clip = newClip;
            m_audioSource.clip = m_clip;
            m_trimEnabled = klip.trimEnabled;
            m_trimStart = klip.trimStart;
            m_trimEnd = klip.trimEnd;
            m_clampToTrim = klip.clampToTrim;
            m_volumeEnvelope = klip.volumeEnvelope;
            m_pitchEnvelope = klip.pitchEnvelope;
        }

        public void ResetStates() {
            isTrimStartDragged = false;
            isTrimEndDragged = false;
            isTrimBothDragged = false;
            if (volumeEnvelopeGUI != null) {
                volumeEnvelopeGUI.ResetStates();
            }
            if (pitchEnvelopeGUI != null) {
                pitchEnvelopeGUI.ResetStates();
            }
        }

        public void DrawLayout(IEnumerable<ZoundToken> playingTokens = null) {
            if (originalClip == null) return;

            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60f;
            GUILayout.BeginHorizontal();
            {
                var lineHeight = EditorGUIUtility.singleLineHeight;
                
                EditorGUI.BeginChangeCheck();
                var trimEnabled = EditorGUILayout.Toggle(m_trimEnabled, GUILayout.Width(15f));
                if (EditorGUI.EndChangeCheck()) {
                    Debug.Log($"[UndoTrace] Trim toggle changed to {trimEnabled}. onTrimEnabledChanged is {(onTrimEnabledChanged != null ? "SET" : "NULL")}");
                    m_trimEnabled = trimEnabled;
                    onTrimEnabledChanged?.Invoke(m_trimEnabled);
                }
                EditorGUILayout.LabelField("Trim", GUILayout.Width(30f));

                GUILayout.Space(10f);
                EditorGUI.BeginChangeCheck();
                var volEnabled = EditorGUILayout.Toggle(m_volumeEnvelope.enabled, GUILayout.Width(15f));
                if (EditorGUI.EndChangeCheck()) {
                    Debug.Log($"[UndoTrace] Volume toggle changed to {volEnabled}. onVolumeEnabledChanged is {(onVolumeEnabledChanged != null ? "SET" : "NULL")}");
                    m_volumeEnvelope.enabled = volEnabled;
                    onVolumeEnabledChanged?.Invoke(volEnabled);
                }
                if (GUILayout.Button(m_showVolumeEnvelopeHandles ? eyeOpenIcon : eyeClosedIcon, GUILayout.Width(25f), GUILayout.Height(lineHeight))) {
                    Undo.RecordObject(m_window, "toggle show volume handles");
                    m_showVolumeEnvelopeHandles = !m_showVolumeEnvelopeHandles;
                    EditorUtility.SetDirty(m_window);
                }
                EditorGUILayout.LabelField("Volume", GUILayout.Width(45f));

                GUILayout.Space(10f);
                EditorGUI.BeginChangeCheck();
                var pitchEnabled = EditorGUILayout.Toggle(m_pitchEnvelope.enabled, GUILayout.Width(15f));
                if (EditorGUI.EndChangeCheck()) {
                    Debug.Log($"[UndoTrace] Pitch toggle changed to {pitchEnabled}. onPitchEnabledChanged is {(onPitchEnabledChanged != null ? "SET" : "NULL")}");
                    m_pitchEnvelope.enabled = pitchEnabled;
                    onPitchEnabledChanged?.Invoke(pitchEnabled);
                }
                if (GUILayout.Button(m_showPitchEnvelopeHandles ? eyeOpenIcon : eyeClosedIcon, GUILayout.Width(25f), GUILayout.Height(lineHeight))) {
                    Undo.RecordObject(m_window, "toggle show pitch handles");
                    m_showPitchEnvelopeHandles = !m_showPitchEnvelopeHandles;
                    EditorUtility.SetDirty(m_window);
                }
                EditorGUILayout.LabelField("Pitch", GUILayout.Width(35f));

                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                var clamp = EditorGUILayout.ToggleLeft("Clamp To Trim", m_clampToTrim, GUILayout.Width(105f));
                if (EditorGUI.EndChangeCheck()) {
                    m_clampToTrim = clamp;
                    onClampToTrimChanged?.Invoke(m_clampToTrim);
                }

                if (originalClip != null) {
                    float duration = m_trimEnabled ? (m_trimEnd - m_trimStart) : originalClip.length;
                    EditorGUILayout.LabelField($"{duration:F3}s", EditorStyles.miniLabel, GUILayout.Width(50f));
                }
            }
            GUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = labelWidth;
            GUILayout.Space(4f);

            Rect spectrumTotalRect = DrawWaveformSpectrum(originalClip, 0f);
            
            Rect trimStartHandleArea = spectrumTotalRect;
            Rect trimEndHandleArea = spectrumTotalRect;
            Rect trimmedRect = spectrumTotalRect;

            if (m_trimEnabled) {
                trimStartHandleArea = DrawTrimStartDim(originalClip.length, spectrumTotalRect);
                trimEndHandleArea = DrawTrimEndDim(originalClip.length, ref spectrumTotalRect);
                trimmedRect = new Rect(trimStartHandleArea.x, spectrumTotalRect.y,
                    trimEndHandleArea.x - trimStartHandleArea.x, spectrumTotalRect.height);
                
                // Handle dragging for the whole trim area (Right click)
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 1 && trimmedRect.Contains(e.mousePosition)) {
                    onTrimDragStarted?.Invoke();
                    isTrimBothDragged = true;
                    isTrimStartDragged = false;
                    isTrimEndDragged = false;
                    dragTrimDistance = trimEnd - trimStart;
                    float mouseTime = ((e.mousePosition.x - spectrumTotalRect.x) / spectrumTotalRect.width) * originalClip.length;
                    dragMouseOffset = mouseTime - trimStart;
                    GUI.changed = true;
                    e.Use();
                }
            }

            Rect envelopeRect = m_clampToTrim ? trimmedRect : spectrumTotalRect;

            bool drawPlayingSource = false;
            if (m_audioSource != null && m_audioSource.clip != null) {
                if (m_audioSource.isPlaying) {
                    drawPlayingSource = true;
                }
            }

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
                        float pitch = Mathf.Max(0.01f, m_pitchEnvelope.Evaluate(t / totalTime));
                        float dt = step;
                        renderedTime += dt / pitch;
                        t += dt;
                    }

                    timePercentage = t / totalTime;
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
                        if (klip.needsRender || token.isRealtime) {
                            AudioWaveformUtility.DrawPlayerHead(trimmedRect, token.time / token.duration);
                        }
                        else {
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

                            AudioWaveformUtility.DrawPlayerHead(trimmedRect, t / totalTime);
                        }
                    }
                    else {
                        AudioWaveformUtility.DrawPlayerHead(trimmedRect, token.time / token.duration);
                    }
                    needsRepaint = true;
                }
            }

            if (m_trimEnabled) {
                DrawTrimHandles(spectrumTotalRect, trimStartHandleArea, trimEndHandleArea);
            }
            bool allowAddPointByDoubleClick = !(m_showVolumeEnvelopeHandles && m_showPitchEnvelopeHandles);
            if (m_volumeEnvelope.enabled) {
                bool isEditable = m_showVolumeEnvelopeHandles; 
                if (volumeEnvelopeGUI.Draw(envelopeRect, m_volumeEnvelope, editorStyle.volumeEnvelopeColor, editorStyle.volumeEnvelopeThickness, isEditable, allowAddPointByDoubleClick)) {
                    onVolumeEnvelopeChanged?.Invoke(m_volumeEnvelope);
                }
            }

            if (m_pitchEnvelope.enabled) {
                bool isEditable = m_showPitchEnvelopeHandles;
                if (pitchEnvelopeGUI.Draw(envelopeRect, m_pitchEnvelope, editorStyle.pitchEnvelopeColor, editorStyle.pitchEnvelopeThickness, isEditable, allowAddPointByDoubleClick)) {
                    onPitchEnvelopeChanged?.Invoke(m_pitchEnvelope);
                }
            }

            if (needsRepaint) {
                m_window.Repaint();
            }
            else {
                // If there's an active interaction or playback, we still want to repaint
                // to keep the player head moving smoothly.
                bool isAnyTokenPlaying = false;
                if (playingTokens != null) {
                    foreach (var token in playingTokens) {
                        if (token != null && token.state == ZoundToken.State.Playing) {
                            isAnyTokenPlaying = true;
                            break;
                        }
                    }
                }
                
                if (drawPlayingSource || isAnyTokenPlaying) {
                    m_window.Repaint();
                }
            }
        }

        #region BASE-VIEW
        private Rect DrawWaveformSpectrum(AudioClip audioClip, float upperOffset) {
            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
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
            GUI.color = editorStyle.klipWaveformBGColor;
            GUI.DrawTexture(textureRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            var audioTexture = AudioWaveformUtility.GetWaveformSpectrumTexture(audioClip, Mathf.FloorToInt(textureRect.width), Mathf.FloorToInt(textureRect.height), editorStyle.waveformColor);
            GUI.DrawTexture(textureRect, audioTexture);

            return textureRect;
        }

        private Rect DrawTrimStartDim(float clipDuration, Rect spectrumRect) {
            float trimStartWidth = (trimStart / clipDuration) * spectrumRect.width;
            var trimStartHandleArea = spectrumRect;
            trimStartHandleArea.x += trimStartWidth;
            trimStartHandleArea.width = ZoundsProject.Instance.projectSettings.editorStyle.trimHandleThickness;

            Color guiColor = GUI.color;
            var trimmedRect = new Rect(spectrumRect.x, spectrumRect.y, trimStartWidth, spectrumRect.height);
            GUI.color = ZoundsProject.Instance.projectSettings.editorStyle.trimAreaColor;
            GUI.DrawTexture(trimmedRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            return trimStartHandleArea;
        }

        private Rect DrawTrimEndDim(float clipDuration, ref Rect spectrumRect) {
            float trimEndWidth = (trimEnd / clipDuration) * spectrumRect.width;
            var trimEndHandleArea = spectrumRect;
            trimEndHandleArea.x += trimEndWidth;
            trimEndHandleArea.width = ZoundsProject.Instance.projectSettings.editorStyle.trimHandleThickness;

            Color guiColor = GUI.color;
            var trimmedRect = new Rect(trimEndHandleArea.x, spectrumRect.y, (spectrumRect.width - trimEndWidth), spectrumRect.height);
            GUI.color = ZoundsProject.Instance.projectSettings.editorStyle.trimAreaColor;
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
            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
            Color guiColor = GUI.color;
            GUI.color = editorStyle.trimHandleColor;
            if (!GUI.enabled) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.35f);
            GUI.DrawTexture(trimStartHandleArea, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;

            EditorGUIUtility.AddCursorRect(trimStartHandleArea, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type) {
                case EventType.MouseDown:
                    if (e.button == 0) {
                        if (trimStartHandleArea.Contains(e.mousePosition)) {
                            onTrimDragStarted?.Invoke();
                            isTrimStartDragged = true;
                            isTrimEndDragged = false;
                            isTrimBothDragged = false;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    else if (e.button == 1) { // Right click
                        if (trimStartHandleArea.Contains(e.mousePosition)) {
                            onTrimDragStarted?.Invoke();
                            isTrimBothDragged = true;
                            isTrimStartDragged = false;
                            isTrimEndDragged = false;
                            dragTrimDistance = trimEnd - trimStart;
                            float mouseTime = ((e.mousePosition.x - spectrumRect.x) / spectrumRect.width) * clipDuration;
                            dragMouseOffset = mouseTime - trimStart;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    break;

                case EventType.MouseUp:
                case EventType.Ignore:
                    isTrimStartDragged = false;
                    isTrimBothDragged = false;
                    break;

                case EventType.MouseDrag:
                    if (isTrimStartDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var newTrimStart = (newPosX / spectrumRect.width) * clipDuration;

                        if (newTrimStart < 0) newTrimStart = 0;
                        else if (newTrimStart >= trimEnd) newTrimStart = trimEnd;
                        trimStart = newTrimStart;
                        m_window.Repaint();
                        e.Use();
                    }
                    else if (isTrimBothDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var mouseTime = (newPosX / spectrumRect.width) * clipDuration;
                        var newTrimStart = mouseTime - dragMouseOffset;

                        if (newTrimStart < 0) newTrimStart = 0;
                        else if (newTrimStart + dragTrimDistance > clipDuration) newTrimStart = clipDuration - dragTrimDistance;
                        
                        trimStart = newTrimStart;
                        trimEnd = newTrimStart + dragTrimDistance;
                        m_window.Repaint();
                        e.Use();
                    }
                    break;
            }
        }

        private void HandleResizeTrimEnd(Rect trimEndHandleArea, float clipDuration, Rect spectrumRect) {
            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
            Color guiColor = GUI.color;
            GUI.color = editorStyle.trimHandleColor;
            if (!GUI.enabled) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.35f);
            GUI.DrawTexture(trimEndHandleArea, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;

            EditorGUIUtility.AddCursorRect(trimEndHandleArea, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type) {
                case EventType.MouseDown:
                    if (e.button == 0) {
                        if (trimEndHandleArea.Contains(e.mousePosition)) {
                            onTrimDragStarted?.Invoke();
                            isTrimEndDragged = true;
                            isTrimStartDragged = false;
                            isTrimBothDragged = false;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    else if (e.button == 1) { // Right click
                        if (trimEndHandleArea.Contains(e.mousePosition)) {
                            onTrimDragStarted?.Invoke();
                            isTrimBothDragged = true;
                            isTrimStartDragged = false;
                            isTrimEndDragged = false;
                            dragTrimDistance = trimEnd - trimStart;
                            float mouseTime = ((e.mousePosition.x - spectrumRect.x) / spectrumRect.width) * clipDuration;
                            dragMouseOffset = mouseTime - trimStart;
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    break;

                case EventType.MouseUp:
                case EventType.Ignore:
                    isTrimEndDragged = false;
                    isTrimBothDragged = false;
                    break;

                case EventType.MouseDrag:
                    if (isTrimEndDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var newTrimEnd = (newPosX / spectrumRect.width) * clipDuration;

                        if (newTrimEnd < trimStart) newTrimEnd = trimStart;
                        else if (newTrimEnd >= clipDuration) newTrimEnd = clipDuration;
                        trimEnd = newTrimEnd;
                        m_window.Repaint();
                        e.Use();
                    }
                    else if (isTrimBothDragged) {
                        var newPosX = e.mousePosition.x - spectrumRect.x;
                        var mouseTime = (newPosX / spectrumRect.width) * clipDuration;
                        var newTrimStart = mouseTime - dragMouseOffset;

                        if (newTrimStart < 0) newTrimStart = 0;
                        else if (newTrimStart + dragTrimDistance > clipDuration) newTrimStart = clipDuration - dragTrimDistance;
                        
                        trimStart = newTrimStart;
                        trimEnd = newTrimStart + dragTrimDistance;
                        m_window.Repaint();
                        e.Use();
                    }
                    break;
            }
        }
        #endregion

    }

}