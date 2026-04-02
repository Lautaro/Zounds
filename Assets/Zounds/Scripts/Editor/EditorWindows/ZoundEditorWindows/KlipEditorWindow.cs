using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class KlipEditorWindow : BaseZoundEditorWindow<Klip, KlipEditorWindow> {

        [SerializeField] private AudioSpectrumView spectrumView;
        [SerializeField] private bool _showPreview = true;
        [SerializeField] private bool _showGainBoost = false;


        private bool notFoundErrorAlreadyShown;

        private bool isDraggingSlider = false;

        private GUIStyle _centeredMiniLabel;
        private GUIStyle centeredMiniLabel =>
            _centeredMiniLabel ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };

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
            spectrumView = new AudioSpectrumView(this);
            spectrumView.height = 100f; // Set a default height
            RefreshSpectrumView();
            RegisterSpectrumViewEvents();
        }

        protected override void OnBaseDisable() {
            // No revert/alert on close — edits are non-destructive.
            // Disabling trim, envelopes, gain boost, or EQ always restores the source.
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

            spectrumView.onTrimDragStarted = () => {
                if (targetZound != null) {
                    ZoundsWindow.BeginDragUndo("change klip trim");
                }
            };

            spectrumView.onVolumeDragStarted = () => {
                if (targetZound != null) {
                    ZoundsWindow.BeginDragUndo("edit volume envelope");
                }
            };

            spectrumView.onPitchDragStarted = () => {
                if (targetZound != null) {
                    ZoundsWindow.BeginDragUndo("edit pitch envelope");
                }
            };

            spectrumView.onTrimEnabledChanged = enabled => {
                if (targetZound != null) {
                    ZoundsWindow.ModifyAndSaveZoundsProject("toggle klip trim", () => {
                        targetZound.trimEnabled = enabled;
                        targetZound.needsRender = true;
                        if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                            Render();
                        }
                    });
                } else {
                    Debug.LogWarning("[Zounds] KlipEditor: onTrimEnabledChanged fired but targetZound is NULL.");
                }
            };
            spectrumView.onTrimStartChanged = trimStart => {
                if (targetZound != null) {
                    targetZound.trimStart = trimStart;
                    targetZound.needsRender = true;
                    Repaint();
                }
            };

            spectrumView.onTrimEndChanged = trimEnd => {
                if (targetZound != null) {
                    targetZound.trimEnd = trimEnd;
                    targetZound.needsRender = true;
                    Repaint();
                }
            };

            spectrumView.onClampToTrimChanged = clamp => {
                if (targetZound != null) {
                    ZoundsWindow.ModifyAndSaveZoundsProject("toggle klip clamp-to-trim", () => {
                        targetZound.clampToTrim = clamp;
                        targetZound.needsRender = true;
                        if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                            Render();
                        }
                    });
                }
            };

            spectrumView.onVolumeEnvelopeChanged = envelope => {
                if (targetZound != null) {
                    // Continuous drag — mutate in memory, persist on mouseUp via autoRender path.
                    targetZound.volumeEnvelope = envelope;
                    targetZound.needsRender = true;
                    Repaint();
                }
            };

            spectrumView.onVolumeEnabledChanged = enabled => {
                if (targetZound != null) {
                    ZoundsWindow.ModifyAndSaveZoundsProject("toggle klip volume", () => {
                        targetZound.volumeEnvelope.enabled = enabled;
                        targetZound.needsRender = true;
                        if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                            Render();
                        }
                    });
                }
            };

            spectrumView.onPitchEnvelopeChanged = envelope => {
                if (targetZound != null) {
                    // Continuous drag — mutate in memory, persist on mouseUp via autoRender path.
                    targetZound.pitchEnvelope = envelope;
                    targetZound.needsRender = true;
                    Repaint();
                }
            };

            spectrumView.onPitchEnabledChanged = enabled => {
                if (targetZound != null) {
                    ZoundsWindow.ModifyAndSaveZoundsProject("toggle klip pitch", () => {
                        targetZound.pitchEnvelope.enabled = enabled;
                        targetZound.needsRender = true;
                        if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                            Render();
                        }
                    });
                }
            };
        }

        protected override void OnUndoRedoPerformed() {
            targetZound = FindZoundTarget();
            RefreshSpectrumView();
            if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                targetZound.needsRender = true;
                Render();
            }
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

            // Handle slider drag end for gain and EQ
            if (mouseReleased && isDraggingSlider) {
                isDraggingSlider = false;
                ZoundsWindow.EndDragUndo(() => {
                    targetZound.needsRender = true;
                    if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                        Render();
                    }
                });
            }

            ZUI.RowSpace(); // before klip name row
            var fieldsRect = GUILayoutUtility.GetRect(1f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));

            // Validate that we have a clip to edit
            bool hasValidClip = targetZound != null && targetZound.audioClipRef != null && targetZound.audioClipRef.RuntimeKeyIsValid();
            if (!hasValidClip) {
                EditorGUILayout.HelpBox("This Klip has no valid Audio Clip assigned. Please assign one in the Clip References tab or the field below.", MessageType.Warning);
                GUI.color = new Color(1f, 0.5f, 0.5f);
            }

            EditorGUI.BeginChangeCheck();
            inspector.DrawSimple(fieldsRect, targetZound, isLocalZound);
            if (EditorGUI.EndChangeCheck()) {
                RefreshWindowName();
            }

            ZUI.RowSpace(); // after klip name row

            GUI.color = Color.white;
            if (!hasValidClip) {
                return false;
            }

            bool remove = false;

            using (ZUI.Box(ZUI.ZUIStyle.Default))
            {

            ZUI.RowSpace(); // top of content box
            var guiColor = GUI.color;
            var guiEnabled = GUI.enabled;
            var labelWidth = EditorGUIUtility.labelWidth;

            AudioClip sourceAsset = null;
            try { sourceAsset = targetZound.audioClipRef.editorAsset as AudioClip; } catch { }
            
            AudioClip outputAsset = null;
            try { 
                var renderedAsset = targetZound.renderedClipRef == null ? null : targetZound.renderedClipRef.editorAsset;
                outputAsset = renderedAsset as AudioClip;
            } catch { }

            if (sourceAsset == null) {
                // If the source asset is missing, we don't close immediately in the redraw loop
                // but we shouldn't attempt to draw the rest of the window.
                EditorGUILayout.HelpBox("Source Audio Clip is missing or invalid. Please fix it in the 'Clip References' tab.", MessageType.Error);
                if (ZUI.Button("Close Window", ZUI.Style.Default)) Close();
                return false;
            }

            if (targetZound.parentId != 0) {
                if (ZoundDictionary.TryGetZoundById(targetZound.parentId, out var parentZound)) {
                    if (parentZound is CompositeZound parentComposite && parentComposite.localKlips.Find(k => k.id == targetZound.id) == null) {
                        // Close if this local klip is removed by its parent zequence
                        Close(); return false;
                    }
                }
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

            if (spectrumView != null) {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                ZUI.RowSpace(); // above waveform

                // For the spectrum view, we use a fixed height or calculate it based on window
                float spectrumHeight = 150f; 
                spectrumView.height = spectrumHeight;

                ZoundEngine.CullingGroups.TryGetValue(targetZound, out var playingTokens);
                spectrumView.renderedClip = null;
                spectrumView.DrawLayout(playingTokens);

                // On MouseUp: close the undo group opened on MouseDown, persist to JSON,
                // and optionally render. Everything is collapsed into the single named entry.
                if (mouseReleased && targetZound.needsRender) {
                    ZoundsWindow.EndDragUndo(() => {
                        ValidateKlip();
                        if (ZoundsProject.Instance.projectSettings.editorStyle.autoRender) {
                            Render();
                        }
                    });
                }
                else if (mouseReleased) {
                    ZoundsWindow.EndDragUndo();
                }

                ZUI.RowSpace();
                GUILayout.BeginHorizontal();
                {
                    const float btnHeight = 20f;

                    // Group 1: File Actions (Standalone)
                    if (ZUI.Button("Render", ZUI.Style.RichButton, ZUICornerMask.All, GUILayout.Height(btnHeight), GUILayout.Width(60f))) {
                        ValidateKlip();
                        Render();
                    }

                    GUILayout.Space(4f);

                    if (ZUI.Button("Remove", ZUI.Style.Danger, ZUICornerMask.All, GUILayout.Height(btnHeight), GUILayout.Width(70f))) {
                        if (AudioAssetUtility.DisplayZoundRemoveDialog(targetZound)) {
                            remove = true;
                        }
                    }

                    GUILayout.FlexibleSpace();

                    // Group 2: Session Controls (Joined Toggle Group)
                    var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;

                    bool newShowGainBoost = ZUI.Toggle(_showGainBoost, "Gain Boost", ZUI.Style.RichToggle, ZUICornerMask.Left, GUILayout.Height(btnHeight), GUILayout.Width(80f));
                    if (newShowGainBoost != _showGainBoost) _showGainBoost = newShowGainBoost;

                    bool newEqEnabled = ZUI.Toggle(targetZound.eqEnabled, "EQ", ZUI.Style.RichToggle, ZUICornerMask.None, GUILayout.Height(btnHeight), GUILayout.Width(40f));
                    if (newEqEnabled != targetZound.eqEnabled) {
                        ZoundsWindow.ModifyAndSaveZoundsProject("toggle klip eq", () => {
                            targetZound.eqEnabled = newEqEnabled;
                            targetZound.needsRender = true;
                            if (editorStyle.autoRender) Render();
                        });
                    }

                    bool newShowPreview = ZUI.Toggle(_showPreview, "Preview", ZUI.Style.RichToggle, ZUICornerMask.None, GUILayout.Height(btnHeight), GUILayout.Width(65f));
                    if (newShowPreview != _showPreview) _showPreview = newShowPreview;

                    bool newAutoRender = ZUI.Toggle(editorStyle.autoRender, "Auto Render", ZUI.Style.RichToggle, ZUICornerMask.Right, GUILayout.Height(btnHeight), GUILayout.Width(95f));
                    if (newAutoRender != editorStyle.autoRender) {
                        ZoundsWindow.ModifyAndSaveZoundsProject("toggle auto render", () => {
                            editorStyle.autoRender = newAutoRender;
                        });
                    }

                    GUILayout.Space(8f);

                    // Group 3: Global Play Action
                    var audioSource = spectrumView.audioSource;
                    GUI.enabled = audioSource != null;
                    bool isPlaying = IsCurrentTokenPlaying();
                    if (ZUI.Button(
                            !GUI.enabled || !isPlaying ? "Play" : "Stop",
                            isPlaying ? ZUI.Style.Danger : ZUI.Style.RichButton,
                            ZUICornerMask.All,
                            GUILayout.Height(btnHeight),
                            GUILayout.Width(60f))) {
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

                ZUI.RowSpace(2f);

                if (_showGainBoost) {
                    EditorGUI.BeginChangeCheck();
                    float gainDB = 20f * Mathf.Log10(targetZound.gain);
                    float newGain = ZUI.Slider(targetZound.gain, 1f, 20f, $"Gain Boost {gainDB:F1}dB", ZUI.SliderStyle.BigSlider);
                    if (EditorGUI.EndChangeCheck()) {
                        if (!isDraggingSlider) {
                            isDraggingSlider = true;
                            ZoundsWindow.BeginDragUndo("change klip gain");
                        }
                        targetZound.gain = newGain;
                        EditorUtility.SetDirty(ZoundsProject.Instance);
                    }
                }

                // Preview waveform — animated foldout
                {
                    var previewAF = ZUI.GetOrCreateAnimFloat("KlipEditor_preview", _showPreview ? 1f : 0f);
                    float previewTarget = _showPreview ? 1f : 0f;
                    if (!Mathf.Approximately(previewAF.target, previewTarget))
                        previewAF.SetTarget(previewTarget, 10f);
                    float previewH = 46f * previewAF.value; // 40px waveform + 6px spacing
                    if (previewH > 0.5f) {
                        ZUI.RowSpace();
                        var editorStyle = ZoundsProject.Instance.projectSettings.editorStyle;
                        var audioClip   = targetZound.GetAudioClipReference().editorAsset as AudioClip;
                        var waveRect    = GUILayoutUtility.GetRect(10f, previewH - 6f, GUILayout.ExpandWidth(true));
                        var prevColor   = GUI.color;
                        GUI.color = editorStyle.klipWaveformBGColor;
                        GUI.DrawTexture(waveRect, EditorGUIUtility.whiteTexture);
                        if (audioClip != null) {
                            var tex = AudioWaveformUtility.GetWaveformSpectrumTexture(
                                audioClip,
                                Mathf.FloorToInt(waveRect.width),
                                Mathf.FloorToInt(waveRect.height),
                                editorStyle.waveformColor,
                                targetZound.id.ToString());
                            if (tex != null) {
                                GUI.color = Color.white;
                                GUI.DrawTexture(waveRect, tex);
                            }
                        }
                        GUI.color = prevColor;
                    }
                }

                ZUI.RowSpace();
                using (var eqBox = ZUI.FoldoutBox("7-Band Equalizer & Filters", "EQ", targetZound.eqEnabled))
                {
                    if (eqBox.visible)
                    {
                        EditorGUI.BeginChangeCheck();

                        // EQ Curve Visualization
                        Rect curveRect = GUILayoutUtility.GetRect(10, 80f, GUILayout.ExpandWidth(true));
                        DrawEQCurve(curveRect, targetZound);

                        GUILayout.Space(5f);

                        float newHpFreq = targetZound.hpFrequency;
                        float newLpFreq = targetZound.lpFrequency;
                        float newSubGain = targetZound.subGain;
                        float newLowGain = targetZound.lowGain;
                        float newLowMidGain = targetZound.lowMidGain;
                        float newMidGain = targetZound.midGain;
                        float newHighMidGain = targetZound.highMidGain;
                        float newHighGain = targetZound.highGain;
                        float newAirGain = targetZound.airGain;

                        // EQ bands + filter layout.
                        // The three columns share one GUILayout row:
                        //   [Low Pass Filter | EQ bands | High Pass Filter]
                        // LP and HP are vertically centred inside the bands column height.

                        // Measure band column height: slider + label + dB readout
                        const float k_BandSliderH = 150f;
                        const float k_BandLabelH  = 17f;  // singleLineHeight ≈ 17
                        const float k_BandValueH  = 17f;
                        const float k_BandColH    = k_BandSliderH + k_BandLabelH + k_BandValueH;

                        // Filter widget height: label row + slider row
                        float filterLabelH  = EditorGUIUtility.singleLineHeight;
                        float filterSliderH = EditorGUIUtility.singleLineHeight + 2f;
                        float filterH       = filterLabelH + filterSliderH;

                        // Reserve the entire three-column block
                        Rect blockRect = GUILayoutUtility.GetRect(10f, k_BandColH, GUILayout.ExpandWidth(true));

                        float thirdW  = blockRect.width / 3f;
                        var   lpRect  = new Rect(blockRect.x,               blockRect.y, thirdW, blockRect.height);
                        var   midRect = new Rect(blockRect.x + thirdW,      blockRect.y, thirdW, blockRect.height);
                        var   hpRect  = new Rect(blockRect.x + thirdW * 2f, blockRect.y, thirdW, blockRect.height);

                        // Draw EQ bands into the middle column — pure Rect layout, no GUILayout area
                        float bandW = midRect.width / 7f;
                        newSubGain     = DrawEQSlider(new Rect(midRect.x + bandW * 0f, midRect.y, bandW, midRect.height), "Sub",   targetZound.subGain);
                        newLowGain     = DrawEQSlider(new Rect(midRect.x + bandW * 1f, midRect.y, bandW, midRect.height), "Low",   targetZound.lowGain);
                        newLowMidGain  = DrawEQSlider(new Rect(midRect.x + bandW * 2f, midRect.y, bandW, midRect.height), "L-Mid", targetZound.lowMidGain);
                        newMidGain     = DrawEQSlider(new Rect(midRect.x + bandW * 3f, midRect.y, bandW, midRect.height), "Mid",   targetZound.midGain);
                        newHighMidGain = DrawEQSlider(new Rect(midRect.x + bandW * 4f, midRect.y, bandW, midRect.height), "H-Mid", targetZound.highMidGain);
                        newHighGain    = DrawEQSlider(new Rect(midRect.x + bandW * 5f, midRect.y, bandW, midRect.height), "High",  targetZound.highGain);
                        newAirGain     = DrawEQSlider(new Rect(midRect.x + bandW * 6f, midRect.y, bandW, midRect.height), "Air",   targetZound.airGain);

                        // Vertically centre the filter widgets inside the band column height
                        float filterOffsetY = (k_BandColH - filterH) * 0.5f;

                        var lpFilterRect = new Rect(lpRect.x + 4f,  lpRect.y  + filterOffsetY, lpRect.width  - 8f, filterH);
                        var hpFilterRect = new Rect(hpRect.x + 4f,  hpRect.y  + filterOffsetY, hpRect.width  - 8f, filterH);

                        newLpFreq = DrawFilterSliderHorizontal(lpFilterRect, "Low Pass Filter",  targetZound.lpFrequency, 100f,  22000f, resetValue: 22000f);
                        newHpFreq = DrawFilterSliderHorizontal(hpFilterRect, "High Pass Filter", targetZound.hpFrequency, 10f,   10000f, resetValue: 10f);

                        if (EditorGUI.EndChangeCheck()) {
                            if (!isDraggingSlider) {
                                isDraggingSlider = true;
                                ZoundsWindow.BeginDragUndo("change klip eq");
                            }
                            targetZound.hpFrequency   = newHpFreq;
                            targetZound.lpFrequency   = newLpFreq;
                            targetZound.subGain       = newSubGain;
                            targetZound.lowGain       = newLowGain;
                            targetZound.lowMidGain    = newLowMidGain;
                            targetZound.midGain       = newMidGain;
                            targetZound.highMidGain   = newHighMidGain;
                            targetZound.highGain      = newHighGain;
                            targetZound.airGain       = newAirGain;
                            EditorUtility.SetDirty(ZoundsProject.Instance);
                        }
                        GUILayout.Space(5f);
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            } // end ZUI.Box

            return remove;
        }

        [SerializeField] private Vector2 scrollPos;

        private float DrawEQSlider(Rect colRect, string label, float value) {
            float labelH  = EditorGUIUtility.singleLineHeight;
            float valueH  = EditorGUIUtility.singleLineHeight;
            float sliderH = colRect.height - labelH - valueH;

            var sliderRect = new Rect(colRect.x, colRect.y,              colRect.width, sliderH);
            var labelRect  = new Rect(colRect.x, sliderRect.yMax,        colRect.width, labelH);
            var valueRect  = new Rect(colRect.x, labelRect.yMax,         colRect.width, valueH);

            float newValue = ZUI.SliderVertical(sliderRect, value, -36f, 36f, label: "", style: ZUI.SliderStyle.SmallSlider, defaultValue: 0f);
            GUI.Label(labelRect, label, centeredMiniLabel);
            GUI.Label(valueRect, $"{newValue:+0.0;-0.0;0.0}", centeredMiniLabel);
            return newValue;
        }

        private float DrawEQSlider(string label, float value) {
            Rect colRect = GUILayoutUtility.GetRect(35f, 150f, GUILayout.Width(35f), GUILayout.ExpandHeight(false));
            return DrawEQSlider(colRect, label, value);
        }

        // Rect-based entry point used when the caller controls the exact position (e.g. manual column layout).
        private float DrawFilterSliderHorizontal(Rect totalRect, string label, float value, float min, float max, float resetValue) {
            const float k_PercentInputW = 34f;
            const float k_HzLabelW      = 58f;

            float logMin = Mathf.Log10(min);
            float logMax = Mathf.Log10(max);
            float t      = Mathf.InverseLerp(logMin, logMax, Mathf.Log10(Mathf.Clamp(value, min, max)));
            float resetT = Mathf.InverseLerp(logMin, logMax, Mathf.Log10(resetValue));

            float rowH     = totalRect.height * 0.5f; // split rect evenly between label and slider rows
            var   labelRow = new Rect(totalRect.x, totalRect.y, totalRect.width, rowH);
            var   sliderRow = new Rect(totalRect.x, totalRect.y + rowH, totalRect.width, totalRect.height - rowH);

            GUI.Label(labelRow, label, EditorStyles.miniLabel);

            float sliderW    = Mathf.Max(0f, sliderRow.width - k_PercentInputW - k_HzLabelW - 4f);
            var   sliderRect = new Rect(sliderRow.x, sliderRow.y, sliderW, sliderRow.height);
            var   inputRect  = new Rect(sliderRect.xMax + 2f, sliderRow.y, k_PercentInputW, sliderRow.height);
            var   hzRect     = new Rect(inputRect.xMax + 2f,  sliderRow.y, k_HzLabelW,      sliderRow.height);

            float newT = ZUI.Slider(sliderRect, t, 0f, 1f, label: "", style: ZUI.SliderStyle.SmallSlider,
                                    defaultValue: resetT, suppressValueField: true);

            EditorGUI.BeginChangeCheck();
            int percentDisplay = Mathf.RoundToInt(newT * 100f);
            int percentEdited  = EditorGUI.IntField(inputRect, percentDisplay, EditorStyles.miniTextField);
            if (EditorGUI.EndChangeCheck())
                newT = Mathf.Clamp01(percentEdited / 100f);

            float newValue = Mathf.Pow(10f, Mathf.Lerp(logMin, logMax, newT));
            newValue = Mathf.Clamp(newValue, min, max);
            GUI.Label(hzRect, $"{Mathf.Round(newValue)} Hz", centeredMiniLabel);

            return newValue;
        }

        // GUILayout-based entry point (kept for any future standalone use).
        private float DrawFilterSliderHorizontal(string label, float value, float min, float max, float resetValue) {
            const float k_PercentInputW = 34f;
            const float k_HzLabelW      = 58f;

            float logMin = Mathf.Log10(min);
            float logMax = Mathf.Log10(max);
            float t      = Mathf.InverseLerp(logMin, logMax, Mathf.Log10(Mathf.Clamp(value, min, max)));
            float resetT = Mathf.InverseLerp(logMin, logMax, Mathf.Log10(resetValue));

            float rowH = EditorGUIUtility.singleLineHeight;

            Rect labelRow  = GUILayoutUtility.GetRect(10f, rowH,      GUILayout.ExpandWidth(true));
            Rect sliderRow = GUILayoutUtility.GetRect(10f, rowH + 2f, GUILayout.ExpandWidth(true));

            GUI.Label(labelRow, label, EditorStyles.miniLabel);

            float sliderW    = Mathf.Max(0f, sliderRow.width - k_PercentInputW - k_HzLabelW - 4f);
            var   sliderRect = new Rect(sliderRow.x, sliderRow.y, sliderW, sliderRow.height);
            var   inputRect  = new Rect(sliderRect.xMax + 2f, sliderRow.y, k_PercentInputW, sliderRow.height);
            var   hzRect     = new Rect(inputRect.xMax + 2f,  sliderRow.y, k_HzLabelW,      sliderRow.height);

            float newT = ZUI.Slider(sliderRect, t, 0f, 1f, label: "", style: ZUI.SliderStyle.SmallSlider,
                                    defaultValue: resetT, suppressValueField: true);

            EditorGUI.BeginChangeCheck();
            int percentDisplay = Mathf.RoundToInt(newT * 100f);
            int percentEdited  = EditorGUI.IntField(inputRect, percentDisplay, EditorStyles.miniTextField);
            if (EditorGUI.EndChangeCheck())
                newT = Mathf.Clamp01(percentEdited / 100f);

            float newValue = Mathf.Pow(10f, Mathf.Lerp(logMin, logMax, newT));
            newValue = Mathf.Clamp(newValue, min, max);
            GUI.Label(hzRect, $"{Mathf.Round(newValue)} Hz", centeredMiniLabel);

            return newValue;
        }

        private void DrawEQCurve(Rect rect, Klip klip) {
            Color bgColor   = ZUI.PaletteColor("EQ", ZUIPaletteSlot.Shade,   new Color(0.1f, 0.1f, 0.1f, 1f));
            Color lineColor = ZUI.PaletteColor("EQ", ZUIPaletteSlot.Primary, Color.cyan);

            // Draw background
            EditorGUI.DrawRect(rect, bgColor);

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
            Handles.color = lineColor;

            for (int i = 0; i < points; i++) {
                float t = i / (float)(points - 1);
                // Logarithmic frequency scale from 10Hz to 22kHz
                float freq = Mathf.Pow(10, Mathf.Lerp(Mathf.Log10(10), Mathf.Log10(22000), t));

                float totalGain = 0;

                // Add EQ Bands influence
                totalGain += GetBandInfluence(freq, 60f,    klip.subGain,     0.7f);
                totalGain += GetBandInfluence(freq, 150f,   klip.lowGain,     0.8f);
                totalGain += GetBandInfluence(freq, 400f,   klip.lowMidGain,  1.0f);
                totalGain += GetBandInfluence(freq, 1000f,  klip.midGain,     1.0f);
                totalGain += GetBandInfluence(freq, 2500f,  klip.highMidGain, 1.0f);
                totalGain += GetBandInfluence(freq, 6000f,  klip.highGain,    0.8f);
                totalGain += GetBandInfluence(freq, 12000f, klip.airGain,     0.7f);

                // Add Filter cuts
                float filterCut = 0;
                if (freq < klip.hpFrequency) filterCut -= 40f * (1f - freq / klip.hpFrequency);
                if (freq > klip.lpFrequency) filterCut -= 40f * (freq / klip.lpFrequency - 1f);

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
            GUILayout.Space(3f);
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
                if (targetZound.trimEnd <= targetZound.trimStart) {
                    targetZound.trimEnd = clip.length;
                    targetZound.needsRender = true;
                    Debug.Log($"[Zounds] ValidateKlip: Fixed trimEnd (was {targetZound.trimEnd}, now {clip.length})");
                    EditorUtility.SetDirty(zoundsProject);
                }
                
                // NEW: Ensure gain is never exactly 0 (which causes silence). If it's 0 (the default/reset state), fix to 1.0.
                if (Mathf.Abs(targetZound.gain) < 0.0001f) {
                    targetZound.gain = 1f;
                    targetZound.needsRender = true;
                    Debug.Log("[Zounds] ValidateKlip: Auto-corrected gain 0.0 to 1.0.");
                    EditorUtility.SetDirty(zoundsProject);
                }
            }
        }

        /// <summary>
        /// Replaces characters that are invalid in Addressable addresses or on common file systems
        /// with safe equivalents so rendered WAV file paths can be registered without errors.
        /// </summary>
        private static string SanitizeFileName(string name) {
            return name
                .Replace('[', '(')
                .Replace(']', ')')
                .Replace('*', '_');
        }

        public void Render() {
            AudioClip reloadedAudio = RenderToAudioClip(targetZound);

            if (reloadedAudio == null && !targetZound.HasActiveEdits()) {
                // No edits active — fall back to the raw source clip.
                try { reloadedAudio = targetZound.audioClipRef.editorAsset as AudioClip; } catch { }
                AudioWaveformUtility.ClearCache(targetZound);
            }
            else if (reloadedAudio != null) {
                AudioWaveformUtility.ClearCache(targetZound);
            }

            spectrumView.audioSource.clip = reloadedAudio;

            // CRITICAL: Synchronize the runtime ZoundDictionary so that 
            // the outside world (Zequences, Play calls) sees the new render immediately.
            if (Application.isPlaying && ZoundEngine.IsInitialized()) {
                ZoundDictionary.ValidateZoundRuntime(targetZound);
            }
        }

        public static AudioClip RenderToAudioClip(Klip klipToRender) {
            return RenderToAudioClip(klipToRender, false);
        }

        public static AudioClip RenderToAudioClip(Klip klipToRender, bool force) {
            if (klipToRender == null) return null;
            if (!klipToRender.needsRender && !force) return null;

            // If all edits are disabled, fall back to the source clip and clean up any orphan rendered file.
            if (!klipToRender.HasActiveEdits()) {
                DeleteRenderedClip(klipToRender);
                return null;
            }

            AudioClip originalClip = null;
            try { originalClip = klipToRender.audioClipRef.editorAsset as AudioClip; } catch { }
            if (originalClip == null) return null;

            originalClip.LoadAudioData(); // NEW: Ensure audio is loaded from disk for GetData()
            
            AudioClip renderedClip = originalClip;

            if (klipToRender.clampToTrim && klipToRender.trimEnabled) {
                // MODE: Clamped - First trim, then apply envelopes to the segment
                var trimmed = AudioRenderUtility.Trim(originalClip, klipToRender.trimStart, klipToRender.trimEnd);
                if (trimmed != null) {
                    renderedClip = trimmed;
                } else {
                    renderedClip = originalClip;
                }

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
                    
                    var trimmed = AudioRenderUtility.Trim(renderedClip, finalTrimStart, finalTrimEnd);
                    if (trimmed != null) renderedClip = trimmed;
                }
            }

            renderedClip = AudioRenderUtility.ApplyGain(renderedClip, klipToRender.gain);
            
            if (klipToRender.eqEnabled && (Mathf.Abs(klipToRender.subGain) > 0.1f || 
                Mathf.Abs(klipToRender.lowGain) > 0.1f || 
                Mathf.Abs(klipToRender.lowMidGain) > 0.1f || 
                Mathf.Abs(klipToRender.midGain) > 0.1f || 
                Mathf.Abs(klipToRender.highMidGain) > 0.1f || 
                Mathf.Abs(klipToRender.highGain) > 0.1f || 
                Mathf.Abs(klipToRender.airGain) > 0.1f ||
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
                string zoundName = SanitizeFileName(klipToRender.name);
                if (klipToRender.parentId != 0) {
                    zoundName += " (" + klipToRender.parentId + ")";
                }
                
                string baseName = zoundName + " (Klip)";
                filePath = Path.Combine(zoundsProject.projectSettings.zoundFilesFolderPath, baseName + ".wav");
                
                // Ensure unique filename if we are branching
                if (isShared || File.Exists(filePath)) {
                    filePath = Path.Combine(zoundsProject.projectSettings.zoundFilesFolderPath, baseName + "_" + klipToRender.id + ".wav");
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
                klipToRender.renderedClipPath = filePath;
            });

            // Always force-save so renderedClipRef survives play mode exit
            EditorUtility.SetDirty(ZoundsProject.Instance);
            AssetDatabase.SaveAssets();

            return reloadedAudio;
        }

        /// <summary>Deletes the rendered WAV for a klip and clears its reference, so the source clip is used directly.</summary>
        public static void DeleteRenderedClip(Klip klip) {
            if (klip == null) return;
            bool hadRendered = !string.IsNullOrEmpty(klip.renderedClipPath) ||
                               (klip.renderedClipRef != null && klip.renderedClipRef.RuntimeKeyIsValid());

            if (!hadRendered) {
                // Nothing to clean up; mark as done.
                ZoundsWindow.ModifyZoundsProject("clear render flag", () => {
                    klip.needsRender = false;
                });
                return;
            }

            // Only delete if this rendered path is not shared with another klip.
            string pathToDelete = klip.renderedClipPath;
            bool isShared = !string.IsNullOrEmpty(pathToDelete) &&
                            ZoundsProject.Instance.zoundLibrary.CountRenderedPathUsages(pathToDelete) > 1;

            ZoundsWindow.ModifyZoundsProject("remove rendered clip", () => {
                klip.needsRender = false;
                klip.renderedClipRef = null;
                klip.renderedClipPath = string.Empty;
            });

            if (!isShared && !string.IsNullOrEmpty(pathToDelete) && File.Exists(pathToDelete)) {
                AudioWaveformUtility.ClearCache(klip);
                AssetDatabase.DeleteAsset(pathToDelete);
                AssetDatabase.SaveAssets();
            }

            EditorUtility.SetDirty(ZoundsProject.Instance);
        }

    }

}