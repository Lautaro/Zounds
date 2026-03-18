using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class KlipEditorWindow : BaseZoundEditorWindow<Klip, KlipEditorWindow> {

        [SerializeField] private AudioSpectrumView spectrumView;

        private bool notFoundErrorAlreadyShown;

        public static KlipEditorWindow OpenWindow(Klip klip) {
            return OpenWindow<KlipEditorWindow>(klip, new Vector2(479.2f, 400f));
        }

        protected override Klip FindZoundTarget() {
            var library = ZoundsProject.Instance.zoundLibrary;
            var result = library.klips.Find(k => k.id == targetZoundID);
            if (result == null) {
                foreach (var zequence in library.zequences) {
                    result = zequence.localKlips.Find(k => k.id == targetZoundID);
                    if (result != null) break;
                    foreach (var localZequence in zequence.localZequences) {
                        result = localZequence.zequence.localKlips.Find(k => k.id == targetZoundID);
                        if (result != null) break;
                    }
                    if (result != null) break;
                }
            }
            if (result == null) {
                if (!notFoundErrorAlreadyShown) {
                    notFoundErrorAlreadyShown = true;
                    Debug.LogError("Can't find klip target for zound id: " + targetZoundID);
                }
            }
            return result;
        }

        protected override void OnInit() {
            if (targetZound != null) {
                targetZound.CreateBackup();
            }
            spectrumView = new AudioSpectrumView(this);
            spectrumView.height = 100f; // Set a default height
            RefreshSpectrumView();
            RegisterSpectrumViewEvents();
        }

        private void Revert() {
            if (targetZound != null) {
                ZoundsWindow.ModifyZoundsProject("revert klip changes", () => {
                    targetZound.RevertFromBackup();
                    Klip.playModeRenderCache.Remove(targetZound.id);
                    RefreshSpectrumView();
                    Render();
                });
            }
        }

        protected override void OnBaseDisable() {
            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
            // Check for changes only if window is actually closing
            if (editorStyle.alertOnClosing && targetZound != null && targetZound.HasChangesToRevert()) {
                if (EditorUtility.DisplayDialog("Unsaved Changes", 
                    $"The Klip '{targetZound.name}' has unsaved changes. Would you like to render them now or revert to the original state?", 
                    "Render and Save", "Revert Changes")) {
                    if (targetZound.needsRender) {
                        Render();
                    }
                }
                else {
                    targetZound.RevertFromBackup();
                }
            }
        }

        protected override void OnDestroy() {
            if (spectrumView != null) {
                spectrumView.Destroy();
                spectrumView = null;
            }
            base.OnDestroy();
        }

        private void RefreshSpectrumView() {
            if (targetZound != null) {
                ValidateKlip();
                spectrumView.InitFromKlip(targetZound);
            }
        }

        private void RegisterSpectrumViewEvents() {
            if (spectrumView == null) return;
            spectrumView.onTrimEnabledChanged = enabled => {
                if (targetZound != null) {
                    targetZound.trimEnabled = enabled;
                    targetZound.needsRender = true;
                }
            };
            spectrumView.onTrimStartChanged = trimStart => {
                if (targetZound != null) {
                    targetZound.trimStart = trimStart;
                    targetZound.needsRender = true;
                }
            };

            spectrumView.onTrimEndChanged = trimEnd => {
                if (targetZound != null) {
                    targetZound.trimEnd = trimEnd;
                    targetZound.needsRender = true;
                }
            };

            spectrumView.onClampToTrimChanged = clamp => {
                if (targetZound != null) {
                    targetZound.clampToTrim = clamp;
                    targetZound.needsRender = true;
                }
            };

            spectrumView.onVolumeEnvelopeChanged = envelope => {
                if (targetZound != null) {
                    targetZound.volumeEnvelope = envelope.DeepCopy();
                    targetZound.needsRender = true;
                }
            };

            spectrumView.onPitchEnvelopeChanged = envelope => {
                if (targetZound != null) {
                    targetZound.pitchEnvelope = envelope.DeepCopy();
                    targetZound.needsRender = true;
                }
            };
        }

        private void OnLostFocus() {
            if (spectrumView != null) {
                spectrumView.ResetStates();
            }
        }

        protected override bool OnDrawGUI() {
            var evt = Event.current;

            // Check for MouseUp to trigger a final render after dragging ends
            bool mouseReleased = evt.type == EventType.MouseUp || evt.type == EventType.Ignore;
            
            var fieldsRect = GUILayoutUtility.GetRect(1f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            EditorGUI.BeginChangeCheck();
            inspector.DrawSimple(fieldsRect, targetZound, isLocalZound);
            if (EditorGUI.EndChangeCheck()) {
                RefreshWindowName();
            }

            GUILayout.Space(4f);
            var guiColor = GUI.color;
            var guiEnabled = GUI.enabled;
            var labelWidth = EditorGUIUtility.labelWidth;

            AudioClip sourceAsset = targetZound.audioClipRef.editorAsset as AudioClip;
            var renderedAsset = targetZound.renderedClipRef == null? null : targetZound.renderedClipRef.editorAsset;
            AudioClip outputAsset = renderedAsset == null? null : renderedAsset as AudioClip;

            if (sourceAsset == null) {
                Close(); return false;
            }

            if (targetZound.parentId != 0) {
                if (ZoundDictionary.TryGetZoundById(targetZound.parentId, out var parentZound)) {
                    if (parentZound is CompositeZound parentComposite && parentComposite.localKlips.Find(k => k.id == targetZound.id) == null) {
                        // Close if this local klip is removed by its parent zequence
                        Close(); return false;
                    }
                }
            }

            EditorGUIUtility.labelWidth = 55f;

            EditorGUI.BeginChangeCheck();
            targetZound.gain = EditorGUILayout.Slider("Gain Boost", targetZound.gain, 1f, 20f);
            if (EditorGUI.EndChangeCheck()) {
                targetZound.needsRender = true;
            }

            EditorGUI.BeginChangeCheck();
            var newSource = EditorGUILayout.ObjectField("Source:", sourceAsset, typeof(AudioClip), false) as AudioClip;
            if (EditorGUI.EndChangeCheck() && newSource != sourceAsset && newSource != null) {
#if ADDRESSABLES_INSTALLED
                if (currentToken != null && currentToken.state == ZoundToken.State.Playing) {
                    currentToken.Kill();
                    currentToken = null;
                }
                ZoundsWindow.ModifyZoundsProject("replace source clip", () => {
                    var assetPath = AssetDatabase.GetAssetPath(newSource);
                    var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    var clipRef = new UnityEngine.AddressableAssets.AssetReference(assetGuid);
                    targetZound.audioClipRef = clipRef;
                    targetZound.audioClipPath = assetPath;
                    if (!ReferenceEquals(outputAsset, null)) {
                        targetZound.needsRender = true;
                    }
                    RefreshSpectrumView();
                    RegisterSpectrumViewEvents();
                });
                ClipReferencesTab.needsRefresh = true;
#endif
            }

            GUI.enabled = false;

            if (ReferenceEquals(outputAsset, null)) {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Output:", GUILayout.Width(EditorGUIUtility.labelWidth));
                EditorGUILayout.LabelField("Same with Source (Unmodified)");
                GUILayout.EndHorizontal();
            }
            else {
                EditorGUILayout.ObjectField("Output:", outputAsset, typeof(AudioClip), false);
            }

            GUI.enabled = guiEnabled;
            EditorGUIUtility.labelWidth = labelWidth;

            // The whole content is wrapped in GUILayout.BeginArea in BaseZoundEditorWindow,
            // but we need to ensure our layout allows the bottom section to be visible.

            bool remove = false;

            if (spectrumView != null) {
                // We use a scroll view for the entire content to handle overflow
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                GUILayout.Space(10f);

                // For the spectrum view, we use a fixed height or calculate it based on window
                float spectrumHeight = 150f; 
                spectrumView.height = spectrumHeight;

                ZoundEngine.CullingGroups.TryGetValue(targetZound, out var playingTokens);
                spectrumView.DrawLayout(playingTokens);

                // If mouse was released this frame and we need a render, do it now
                if (mouseReleased && targetZound.needsRender && ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                    Render();
                    ZoundsWindow.ModifyZoundsProject("Apply Klip changes", () => {
                        // Project values are already updated via the events
                    }, true);
                }

                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(80f))) {
                        if (AudioAssetUtility.DisplayZoundRemoveDialog(targetZound)) {
                            remove = true;
                        }
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUI.BeginChangeCheck();
                    targetZound.showRenderedWaveform = EditorGUILayout.ToggleLeft("Preview", targetZound.showRenderedWaveform, GUILayout.Width(65f));
                    targetZound.eqEnabled = EditorGUILayout.ToggleLeft("EQ", targetZound.eqEnabled, GUILayout.Width(45f));
                    if (EditorGUI.EndChangeCheck()) {
                        EditorUtility.SetDirty(ZoundsProject.Instance);
                        targetZound.needsRender = true;
                    }

                    var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
                    EditorGUI.BeginChangeCheck();
                    editorStyle.autoRender = EditorGUILayout.ToggleLeft("Auto Render", editorStyle.autoRender, GUILayout.Width(95f));
                    editorStyle.alertOnClosing = EditorGUILayout.ToggleLeft("Alert", editorStyle.alertOnClosing, GUILayout.Width(50f));
                    if (EditorGUI.EndChangeCheck()) {
                        EditorUtility.SetDirty(ZoundsProject.Instance);
                    }

                    GUI.enabled = targetZound != null && targetZound.HasChangesToRevert();
                    if (GUILayout.Button("Revert", GUILayout.Width(80f))) {
                        Revert();
                    }
                    GUI.enabled = guiEnabled;
                    var audioSource = spectrumView.audioSource;
                    GUI.enabled = audioSource != null;
                    if (GUILayout.Button(!GUI.enabled || !IsCurrentTokenPlaying() ? "Play" : "Stop", GUILayout.Width(80f))) {
                        if (currentToken != null && currentToken.state == ZoundToken.State.Playing) {
                            currentToken.Kill();
                            currentToken = null;
                        }
                        else {
                            SimulatePlay();
                        }
                    }
                    GUI.enabled = guiEnabled;
                }
                GUILayout.EndHorizontal();

                if (targetZound.eqEnabled) {
                    GUILayout.Space(5f);
                    EditorGUI.BeginChangeCheck();
                    
                    EditorGUILayout.LabelField("7-Band Equalizer & Filters", EditorStyles.boldLabel);
                    
                    // NEW: EQ Curve Visualization
                    Rect curveRect = GUILayoutUtility.GetRect(10, 80f, GUILayout.ExpandWidth(true));
                    DrawEQCurve(curveRect, targetZound);
                    
                    GUILayout.Space(5f);

                    // Horizontal Filter Sliders centered vertically to EQ
                    GUILayout.BeginHorizontal();
                    {
                        // High Pass (Left)
                        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                        GUILayout.Space(60f); // Center relative to 120px EQ sliders
                        targetZound.hpFrequency = DrawHorizontalFilter("High Pass Filter", targetZound.hpFrequency, 10f, 10000f, true);
                        GUILayout.EndVertical();

                        GUILayout.Space(10f);

                        // EQ Bands
                        GUILayout.BeginHorizontal();
                        targetZound.subGain = DrawEQSlider("Sub", targetZound.subGain);
                        targetZound.lowGain = DrawEQSlider("Low", targetZound.lowGain);
                        targetZound.lowMidGain = DrawEQSlider("L-Mid", targetZound.lowMidGain);
                        targetZound.midGain = DrawEQSlider("Mid", targetZound.midGain);
                        targetZound.highMidGain = DrawEQSlider("H-Mid", targetZound.highMidGain);
                        targetZound.highGain = DrawEQSlider("High", targetZound.highGain);
                        targetZound.airGain = DrawEQSlider("Air", targetZound.airGain);
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10f);

                        // Low Pass (Right)
                        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                        GUILayout.Space(60f); // Center relative to 120px EQ sliders
                        targetZound.lpFrequency = DrawHorizontalFilter("Low Pass Filter", targetZound.lpFrequency, 100f, 22000f, false);
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                    
                    if (EditorGUI.EndChangeCheck()) {
                        targetZound.needsRender = true;
                    }
                    GUILayout.Space(5f);
                }

                GUILayout.Space(5f);
                if (targetZound.showRenderedWaveform) {
                    var outputClip = targetZound.renderedClipRef?.editorAsset as AudioClip;
                    
                    // Prioritize the global session render cache.
                    if (Klip.playModeRenderCache.TryGetValue(targetZound.id, out var cachedClip) && cachedClip != null) {
                        outputClip = cachedClip;
                    }

                    if (outputClip != null) {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Rendered Waveform Preview", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField($"{outputClip.length:F3}s", EditorStyles.miniLabel, GUILayout.Width(50f));
                        GUILayout.EndHorizontal();
                    }
                    
                    var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
                    var rect = GUILayoutUtility.GetRect(1f, 60f, GUILayout.ExpandWidth(true));
                    
                    if (outputClip != null) {
                        AudioWaveformUtility.DrawWaveformRect(rect, outputClip, editorStyle.renderedWaveformBGColor, editorStyle.renderedWaveformColor, targetZound.id.ToString());
                        
                        // Draw playerhead for preview if playing
                        if (currentToken != null && currentToken.state == ZoundToken.State.Playing) {
                            float timePercentage = currentToken.audioSource.time / outputClip.length;
                            AudioWaveformUtility.DrawPlayerHead(rect, timePercentage, editorStyle.renderedPlayerHeadColor);
                            Repaint();
                        }
                    } else {
                        EditorGUI.HelpBox(rect, "No rendered clip available. Adjust settings and ensure Auto Render is on.", MessageType.Info);
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.EndScrollView();
            }

            return remove;
        }

        [SerializeField] private Vector2 scrollPos;

        private float DrawEQSlider(string label, float value) {
            GUILayout.BeginVertical(GUILayout.Width(35f));
            
            // Capture the Rect BEFORE drawing the slider to ensure it's available for events
            Rect sliderRect = GUILayoutUtility.GetRect(35f, 120f);
            
            // Handle Mouse Events for Reset before the slider consumes them
            if (Event.current.type == EventType.MouseDown && sliderRect.Contains(Event.current.mousePosition)) {
                if (Event.current.clickCount >= 2) {
                    value = 0f;
                    GUI.changed = true;
                    Event.current.Use();
                }
            }
            
            // Draw the vertical slider manually in the reserved rect
            float newValue = GUI.VerticalSlider(sliderRect, value, 36f, -36f);
            
            // Label area
            var centeredMiniLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
            Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(label), centeredMiniLabel, GUILayout.Width(35f));
            if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition)) {
                if (Event.current.clickCount >= 2) {
                    newValue = 0f;
                    GUI.changed = true;
                    Event.current.Use();
                }
            }
            GUI.Label(labelRect, label, centeredMiniLabel);
            
            // Draw value
            EditorGUILayout.LabelField($"{newValue:F1}", centeredMiniLabel, GUILayout.Width(35f));
            
            GUILayout.EndVertical();
            return newValue;
        }

        private float DrawHorizontalFilter(string label, float value, float min, float max, bool isHPF) {
            GUILayout.BeginVertical();
            var centeredMiniLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
            EditorGUILayout.LabelField(label, centeredMiniLabel);

            // Using Logarithmic scale for frequency sliders
            float logMin = Mathf.Log10(min);
            float logMax = Mathf.Log10(max);
            float t = (Mathf.Log10(value) - logMin) / (logMax - logMin);

            GUILayout.BeginHorizontal();
            
            // Draw the slider (0 to 1 linear representation of the log scale)
            // Use GUILayout.HorizontalSlider to avoid the built-in numeric field of EditorGUILayout.Slider
            float newT = GUILayout.HorizontalSlider(t, 0f, 1f, GUILayout.ExpandWidth(true));
            
            // Handle Double Click to Reset on the slider
            Rect sliderRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && sliderRect.Contains(Event.current.mousePosition)) {
                if (Event.current.clickCount >= 2) {
                    float resetFreq = isHPF ? min : max;
                    newT = (Mathf.Log10(resetFreq) - logMin) / (logMax - logMin);
                    GUI.changed = true;
                    Event.current.Use();
                }
            }

            float newValue = Mathf.Pow(10, logMin + newT * (logMax - logMin));

            // Numeric input for precise control - showing freq without fractions
            newValue = EditorGUILayout.FloatField(Mathf.Round(newValue), GUILayout.Width(60f));
            newValue = Mathf.Clamp(newValue, min, max);

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return newValue;
        }

        private void DrawEQCurve(Rect rect, Klip klip) {
            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
            
            // Draw frequency grid lines (approximate log scale)
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            float[] gridFreqs = { 100, 1000, 10000 };
            foreach (var f in gridFreqs) {
                float x = Mathf.InverseLerp(Mathf.Log10(10), Mathf.Log10(22000), Mathf.Log10(f)) * rect.width;
                Handles.DrawLine(new Vector2(rect.x + x, rect.y), new Vector2(rect.x + x, rect.y + rect.height));
            }

            // Generate Curve Points
            int points = 100;
            Vector3[] curve = new Vector3[points];
            Handles.color = Color.cyan;

            for (int i = 0; i < points; i++) {
                float t = i / (float)(points - 1);
                // Logarithmic frequency scale from 10Hz to 22kHz
                float freq = Mathf.Pow(10, Mathf.Lerp(Mathf.Log10(10), Mathf.Log10(22000), t));
                
                float totalGain = 0;
                
                // Add EQ Bands influence
                totalGain += GetBandInfluence(freq, 60f, klip.subGain, 0.7f);
                totalGain += GetBandInfluence(freq, 150f, klip.lowGain, 0.8f);
                totalGain += GetBandInfluence(freq, 400f, klip.lowMidGain, 1.0f);
                totalGain += GetBandInfluence(freq, 1000f, klip.midGain, 1.0f);
                totalGain += GetBandInfluence(freq, 2500f, klip.highMidGain, 1.0f);
                totalGain += GetBandInfluence(freq, 6000f, klip.highGain, 0.8f);
                totalGain += GetBandInfluence(freq, 12000f, klip.airGain, 0.7f);

                // Add Filter cuts
                float filterCut = 0;
                if (freq < klip.hpFrequency) filterCut -= 40f * (1f - freq/klip.hpFrequency); // Simple visualization of roll-off
                if (freq > klip.lpFrequency) filterCut -= 40f * (freq/klip.lpFrequency - 1f);

                float y = Mathf.InverseLerp(36, -36, totalGain + filterCut) * rect.height;
                curve[i] = new Vector3(rect.x + t * rect.width, rect.y + y, 0);
            }
            
            Handles.DrawAAPolyLine(2f, curve);
            
            // Draw 0dB line
            Handles.color = new Color(1, 1, 1, 0.2f);
            float zeroY = rect.y + rect.height * 0.5f;
            Handles.DrawLine(new Vector2(rect.x, zeroY), new Vector2(rect.x + rect.width, zeroY));
        }

        private float GetBandInfluence(float freq, float center, float gain, float q) {
            // Simplified bell curve for visualization
            float width = center / q;
            float diff = Mathf.Abs(Mathf.Log10(freq) - Mathf.Log10(center));
            return gain * Mathf.Exp(-diff * diff * 5f); // 5f is a tuning constant for the visual "width"
        }

        protected override void OnDrawHeader() {
            var guiColor = GUI.color;

            // Header line removed per request (Waveform Color moved to Settings)
            /*
            var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
            
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Waveform Color:", GUILayout.Width(110f));
                editorStyle.waveformColor = EditorGUILayout.ColorField(GUIContent.none, editorStyle.waveformColor, false, false, false, GUILayout.Width(50f));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
            */

            GUI.color = Color.gray;
            var lineRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(lineRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            GUILayout.Space(5f);
        }

        protected override void OnPressSpaceKey() {
            if (IsCurrentTokenPlaying()) {
                currentToken.Kill();
            }
            else {
                SimulatePlay();
            }
        }

        private void SimulatePlay() {
            if (!Application.isPlaying && targetZound.needsRender) {
                Render();
            }

            var needsRenderTemp = targetZound.needsRender;
            targetZound.needsRender = false; // Force playback of the rendered clip

            float targetPitch = Random.Range(targetZound.minPitch, targetZound.maxPitch);
            currentToken = ZoundEngine.PlayZound(targetZound, new ZoundArgs() {
                startImmediately = true,
                delay = 0f,
                volumeOverride = Random.Range(targetZound.minVolume, targetZound.maxVolume),
                pitchOverride = targetPitch,
                chanceOverride = 1f,
                useFixedAverageValues = false,
                bypassGlobalSolo = isLocalZound,
                ignoreCooldown = true
            });
            targetZound.needsRender = needsRenderTemp;
        }

        private void ValidateKlip() {
            var zoundsProject = ZoundsProject.Instance;
            if (targetZound.trimStart < 0) {
                targetZound.trimStart = 0;
                targetZound.needsRender = true;
                EditorUtility.SetDirty(zoundsProject);
            }
            if (targetZound.trimEnd < 0) {
                targetZound.trimEnd = 0;
                targetZound.needsRender = true;
                EditorUtility.SetDirty(zoundsProject);
            }
            if (targetZound.audioClipRef.editorAsset is AudioClip clip) {
                if (targetZound.trimStart > clip.length) {
                    targetZound.trimStart = clip.length;
                    targetZound.needsRender = true;
                    EditorUtility.SetDirty(zoundsProject);
                }
                if (targetZound.trimEnd > clip.length) {
                    targetZound.trimEnd = clip.length;
                    targetZound.needsRender = true;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }
        }

        public void Render() {
            AudioClip reloadedAudio = RenderToAudioClip(targetZound);
            if (reloadedAudio != null) {
                // Cache the rendered clip so the Zequence editor shows it immediately
                Klip.playModeRenderCache[targetZound.id] = reloadedAudio;
                AudioWaveformUtility.ClearCache(targetZound);
                AudioWaveformUtility.ClearCache(reloadedAudio);
            }
            Undo.RecordObject(spectrumView.audioSource, "render klip");
            spectrumView.audioSource.clip = reloadedAudio;
            EditorUtility.SetDirty(spectrumView.audioSource);
        }

        public static AudioClip RenderToAudioClip(Klip klipToRender) {
            if (klipToRender == null) return null;
            if (!klipToRender.needsRender) return null;

            var originalClip = klipToRender.audioClipRef.editorAsset as AudioClip;
            if (originalClip == null) return null;

            AudioClip renderedClip = originalClip;

            if (klipToRender.clampToTrim && klipToRender.trimEnabled) {
                // MODE: Clamped - First trim, then apply envelopes to the segment
                renderedClip = AudioRenderUtility.Trim(originalClip, klipToRender.trimStart, klipToRender.trimEnd);
                if (klipToRender.volumeEnvelope.enabled) {
                    renderedClip = AudioRenderUtility.VolumeEnvelope(renderedClip, klipToRender.volumeEnvelope);
                }
                if (klipToRender.pitchEnvelope.enabled) {
                    renderedClip = AudioRenderUtility.PitchEnvelope(renderedClip, klipToRender.pitchEnvelope, 0, renderedClip.length);
                }
            } else {
                // MODE: Global - Apply envelopes to FULL original clip, then trim
                if (klipToRender.volumeEnvelope.enabled) {
                    renderedClip = AudioRenderUtility.VolumeEnvelope(renderedClip, klipToRender.volumeEnvelope);
                }
                if (klipToRender.pitchEnvelope.enabled) {
                    renderedClip = AudioRenderUtility.PitchEnvelope(renderedClip, klipToRender.pitchEnvelope, 0, originalClip.length);
                }
                if (klipToRender.trimEnabled) {
                    // Fix: Trim times must be recalculated because the pitch envelope changed the timing
                    float finalTrimStart = klipToRender.trimStart;
                    float finalTrimEnd = klipToRender.trimEnd;
                    
                    if (klipToRender.pitchEnvelope.enabled) {
                        finalTrimStart = AudioRenderUtility.GetOutputTimeForSourceTime(klipToRender.trimStart, klipToRender.pitchEnvelope, originalClip.length);
                        finalTrimEnd = AudioRenderUtility.GetOutputTimeForSourceTime(klipToRender.trimEnd, klipToRender.pitchEnvelope, originalClip.length);
                    }
                    
                    renderedClip = AudioRenderUtility.Trim(renderedClip, finalTrimStart, finalTrimEnd);
                }
            }

            renderedClip = AudioRenderUtility.ApplyGain(renderedClip, klipToRender.gain);
            
            if (klipToRender.eqEnabled && (!Mathf.Approximately(klipToRender.subGain, 0f) || 
                !Mathf.Approximately(klipToRender.lowGain, 0f) || 
                !Mathf.Approximately(klipToRender.lowMidGain, 0f) || 
                !Mathf.Approximately(klipToRender.midGain, 0f) || 
                !Mathf.Approximately(klipToRender.highMidGain, 0f) || 
                !Mathf.Approximately(klipToRender.highGain, 0f) || 
                !Mathf.Approximately(klipToRender.airGain, 0f) ||
                klipToRender.lpFrequency < 21900f ||
                klipToRender.hpFrequency > 20f)) {
                renderedClip = AudioRenderUtility.ApplyEqualizer(renderedClip, 
                    klipToRender.subGain, 
                    klipToRender.lowGain, 
                    klipToRender.lowMidGain, 
                    klipToRender.midGain, 
                    klipToRender.highMidGain, 
                    klipToRender.highGain, 
                    klipToRender.airGain,
                    klipToRender.lpFrequency,
                    klipToRender.hpFrequency);
            }

            var zoundsProject = ZoundsProject.Instance;
            string filePath;
            bool isShared = zoundsProject.zoundLibrary.CountRenderedPathUsages(klipToRender.renderedClipPath) > 1;

            if (string.IsNullOrEmpty(klipToRender.renderedClipPath) || isShared) {
                string zoundName = klipToRender.name;
                if (klipToRender.parentId != 0) {
                    zoundName += " (" + klipToRender.parentId + ")";
                }
                
                string baseName = zoundName + " (Klip)";
                filePath = Path.Combine(zoundsProject.projectSettings.workFolderPath, baseName + ".wav");
                
                // Ensure unique filename if we are branching
                if (isShared || File.Exists(filePath)) {
                    filePath = Path.Combine(zoundsProject.projectSettings.workFolderPath, baseName + "_" + klipToRender.id + ".wav");
                }
            }
            else {
                filePath = klipToRender.renderedClipPath;
            }

            var reloadedAudio = AudioRenderUtility.SaveAudio(renderedClip, filePath);

            // Clear the texture cache so the Zequence editor generates a fresh waveform
            AudioWaveformUtility.ClearCache(klipToRender);
            AudioWaveformUtility.ClearCache(reloadedAudio);

            var audioRef = AudioRenderUtility.GetAudioReference(reloadedAudio);

            ZoundsWindow.ModifyZoundsProject("render klip", () => {
                klipToRender.needsRender = false;
                klipToRender.renderedClipRef = audioRef;
            });

            // Always force-save so renderedClipRef survives play mode exit
            EditorUtility.SetDirty(ZoundsProject.Instance);
            AssetDatabase.SaveAssets();

            return reloadedAudio;
        }

        protected override void OnUndoRedoPerformed() {
            RefreshSpectrumView();
        }

    }

}