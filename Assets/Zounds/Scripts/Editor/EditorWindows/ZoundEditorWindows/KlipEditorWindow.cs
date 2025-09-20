using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class KlipEditorWindow : BaseZoundEditorWindow<Klip, KlipEditorWindow> {

        [SerializeField] private AudioSpectrumView spectrumView;

        private bool notFoundErrorAlreadyShown;
        private ZoundToken currentToken;

        public static KlipEditorWindow OpenWindow(Klip klip) {
            return OpenWindow<KlipEditorWindow>(klip, new Vector2(479.2f, 251f));
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
            RefreshSpectrumView();
            RegisterSpectrumViewEvents();
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
            spectrumView.onTrimStartChanged = trimStart => {
                if (targetZound != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "change trim start");
                    targetZound.trimStart = trimStart;
                    targetZound.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };

            spectrumView.onTrimEndChanged = trimEnd => {
                if (targetZound != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "change trim end");
                    targetZound.trimEnd = trimEnd;
                    targetZound.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };

            spectrumView.onVolumeEnvelopeChanged = envelope => {
                if (targetZound != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "modify volume envelope");
                    targetZound.volumeEnvelope = envelope.DeepCopy();
                    targetZound.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };

            spectrumView.onPitchEnvelopeChanged = envelope => {
                if (targetZound != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "modify pitch envelope");
                    targetZound.pitchEnvelope = envelope.DeepCopy();
                    targetZound.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };
        }

        private void OnLostFocus() {
            if (spectrumView != null) {
                spectrumView.ResetStates();
            }
        }

        protected override bool OnDrawGUI() {
            var fieldsRect = GUILayoutUtility.GetRect(1f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            EditorGUI.BeginChangeCheck();
            inspector.DrawSimple(fieldsRect, targetZound);
            if (EditorGUI.EndChangeCheck()) {
                RefreshWindowName();
            }

            GUILayout.Space(4f);
            var guiColor = GUI.color;
            GUI.color = Color.gray;
            var lineRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(lineRect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
            GUILayout.Space(2f);

            AudioClip sourceAsset = targetZound.audioClipRef.editorAsset as AudioClip;
            var renderedAsset = targetZound.renderedClipRef.editorAsset;
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

            float labelWidth = EditorGUIUtility.labelWidth;

            //if (targetZound.parentId != 0) {
            //    EditorGUIUtility.labelWidth = 100f;
            //    EditorGUI.BeginChangeCheck();
            //    var newName = EditorGUILayout.TextField("Local Klip Name:", targetZound.name);
            //    if (EditorGUI.EndChangeCheck()) {
            //        Undo.RecordObject(ZoundsProject.Instance, "change local klip name");
            //        targetZound.name = "";
            //        var uniqueName = ZoundDictionary.EnsureUniqueZoundName(newName);
            //        targetZound.name = uniqueName;
            //        EditorUtility.SetDirty(ZoundsProject.Instance);
            //    }
            //}

            bool guiEnabled = GUI.enabled;
            //bool guiEnabled = !Application.isPlaying; // TODO: Enable clip editing during play mode
            //if (guiEnabled) {
            //    if (spectrumView.audioSource.isPlaying || HasAnyInstancePlaying()) guiEnabled = false;
            //}

            GUI.enabled = false;
            EditorGUIUtility.labelWidth = 55f;

            EditorGUILayout.ObjectField("Source:", sourceAsset, typeof(AudioClip), false);
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

            bool remove = false;

            if (spectrumView != null) {
                GUILayout.Space(10f);

                ZoundEngine.CullingGroups.TryGetValue(targetZound, out var playingTokens);
                spectrumView.DrawLayout(playingTokens);


                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(80f))) {
                        if (AudioAssetUtility.DisplayZoundRemoveDialog(targetZound)) {
                            remove = true;
                        }
                    }
                    GUILayout.FlexibleSpace();
                    GUI.enabled = guiEnabled && targetZound.needsRender;
                    if (GUILayout.Button("Render", GUILayout.Width(80f))) {
                        Render();
                    }
                    var audioSource = spectrumView.audioSource;
                    GUI.enabled = audioSource != null;
                    if (GUILayout.Button(!GUI.enabled || /*!audioSource.isPlaying*/ (currentToken == null || currentToken.state != ZoundToken.State.Playing) ? "Play" : "Stop", GUILayout.Width(80f))) {
                        if (/*audioSource.isPlaying*/currentToken != null && currentToken.state == ZoundToken.State.Playing) {
                            //audioSource.Stop();
                            currentToken.Kill();
                            currentToken = null;
                        }
                        else {
                            if (!Application.isPlaying) {
                                Render();
                            }
                            //audioSource.volume = Random.Range(targetZound.minVolume, targetZound.maxVolume);
                            //audioSource.pitch = Random.Range(targetZound.minPitch, targetZound.maxPitch);
                            //audioSource.Play();
                            var needsRenderTemp = targetZound.needsRender;
                            targetZound.needsRender = true;
                            currentToken = ZoundEngine.PlayZound(targetZound, new ZoundArgs() {
                                startImmediately = true,
                                delay = 0f,
                                volumeOverride = Random.Range(targetZound.minVolume, targetZound.maxVolume),
                                pitchOverride = Random.Range(targetZound.minPitch, targetZound.maxPitch),
                                chanceOverride = 1f,
                                useFixedAverageValues = false
                            });
                            targetZound.needsRender = needsRenderTemp;
                        }
                    }
                    GUI.enabled = guiEnabled;
                }
                GUILayout.EndHorizontal();
            }

            return remove;
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

        private void Render() {
            if (targetZound == null) return;
            if (!targetZound.needsRender) return;

            if (targetZound == null) return;
            var originalClip = targetZound.audioClipRef.editorAsset as AudioClip;
            if (originalClip == null) return;

            if (Application.isPlaying) {
                Debug.LogWarning(targetZound.name + ": Can't render a Klip during play mode.");
                return;
            }

            AudioClip renderedClip = AudioRenderUtility.Trim(originalClip, 
                targetZound.trimStart, targetZound.trimEnd);

            if (targetZound.volumeEnvelope.enabled) {
                renderedClip = AudioRenderUtility.VolumeEnvelope(renderedClip, targetZound.volumeEnvelope);
            }

            if (targetZound.pitchEnvelope.enabled) {
                renderedClip = AudioRenderUtility.PitchEnvelope(renderedClip, targetZound.pitchEnvelope);
            }

            var zoundsProject = ZoundsProject.Instance;
            string filePath;
            if (string.IsNullOrEmpty(targetZound.renderedClipPath)) {
                string zoundName = targetZound.name;
                if (targetZound.parentId != 0) {
                    zoundName += " (" + targetZound.parentId + ")";
                }
                filePath = Path.Combine(zoundsProject.projectSettings.workFolderPath, zoundName + " (Klip).wav");
            }
            else {
                filePath = targetZound.renderedClipPath;
            }
            var reloadedAudio = AudioRenderUtility.SaveAudio(renderedClip, filePath);
            var audioRef = AudioRenderUtility.GetAudioReference(reloadedAudio);

            Undo.RecordObject(zoundsProject, "render klip");
            targetZound.needsRender = false;
            targetZound.renderedClipRef = audioRef;

            Undo.RecordObject(spectrumView.audioSource, "render klip");
            spectrumView.audioSource.clip = reloadedAudio;

            EditorUtility.SetDirty(zoundsProject);
            EditorUtility.SetDirty(spectrumView.audioSource);
        }

        protected override void OnUndoRedoPerformed() {
            RefreshSpectrumView();
        }

    }

}