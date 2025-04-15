using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class KlipEditorWindow : EditorWindow {

        private static readonly Dictionary<int, KlipEditorWindow> allWindows = new Dictionary<int, KlipEditorWindow>();

        [SerializeField] private int targetKlipID;
        [SerializeField] private AudioSpectrumView spectrumView;

        private Klip targetKlip;

        public static KlipEditorWindow OpenWindow(Klip klip) {
            if (!allWindows.TryGetValue(klip.id, out var window)) {
                window = CreateInstance<KlipEditorWindow>();
                window.targetKlipID = klip.id;
                window.minSize = new Vector2(280f, 230f);
                window.Init();
                window.Show();
            }
            else {
                window.ShowTab();
            }
            return window;
        }

        private void OnEnable() {
            // ensure init here too to re-register window after recompilation.
            Init();
        }

        private void Init() {
            if (targetKlipID == 0) return;

            var library = ZoundsProject.Instance.zoundLibrary;
            targetKlip = library.klips.Find(k => k.id == targetKlipID);
            titleContent.text = "Klip: " + (targetKlip == null ? "(Invalid)" : targetKlip.name);

            if (allWindows.ContainsKey(targetKlipID) && allWindows[targetKlipID] != this) {
                allWindows[targetKlipID] = this;
            }
            else {
                allWindows.Add(targetKlipID, this);
            }

            spectrumView = new AudioSpectrumView(this);
            RefreshSpectrumView();
            RegisterSpectrumViewEvents();
        }

        private void OnDestroy() {
            if (spectrumView != null) {
                spectrumView.Destroy();
                spectrumView = null;
            }
            if (allWindows.ContainsKey(targetKlipID)) {
                allWindows.Remove(targetKlipID);
            }
        }

        private void RefreshSpectrumView() {
            if (targetKlip != null) {
                ValidateKlip();
                spectrumView.InitFromKlip(targetKlip);
            }
        }

        private void RegisterSpectrumViewEvents() {
            if (spectrumView == null) return;
            spectrumView.onTrimStartChanged = trimStart => {
                if (targetKlip != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "change trim start");
                    targetKlip.trimStart = trimStart;
                    targetKlip.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };

            spectrumView.onTrimEndChanged = trimEnd => {
                if (targetKlip != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "change trim end");
                    targetKlip.trimEnd = trimEnd;
                    targetKlip.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };

            spectrumView.onVolumeEnvelopeChanged = envelope => {
                if (targetKlip != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "modify volume envelope");
                    targetKlip.volumeEnvelope = envelope.DeepCopy();
                    targetKlip.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };

            spectrumView.onPitchEnvelopeChanged = envelope => {
                if (targetKlip != null) {
                    Undo.RecordObject(ZoundsProject.Instance, "modify pitch envelope");
                    targetKlip.pitchEnvelope = envelope.DeepCopy();
                    targetKlip.needsRender = true;
                    EditorUtility.SetDirty(ZoundsProject.Instance);
                }
            };
        }

        private void OnLostFocus() {
            EnvelopeGUI.ResetStates();
            if (spectrumView != null) {
                spectrumView.ResetStates();
            }
        }

        private void OnGUI() {
            if (targetKlipID == 0) {
                Close(); return;
            }

            AudioClip sourceAsset = targetKlip.audioClipRef.editorAsset as AudioClip;
            AudioClip outputAsset = targetKlip.renderedClipRef.editorAsset as AudioClip;

            if (sourceAsset == null) {
                Close(); return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, position.width - 20f, position.height - 20f));

            bool guiEnabled = GUI.enabled;
            float labelWidth = EditorGUIUtility.labelWidth;
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
                spectrumView.DrawLayout();


                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                {
                    GUI.enabled = guiEnabled && targetKlip.needsRender;
                    if (GUILayout.Button("Render", GUILayout.Width(80f))) {
                        Render();
                    }
                    var audioSource = spectrumView.audioSource;
                    GUI.enabled = guiEnabled && audioSource != null;
                    if (GUILayout.Button(!GUI.enabled || !audioSource.isPlaying ? "Play" : "Stop", GUILayout.Width(80f))) {
                        if (audioSource.isPlaying) {
                            audioSource.Stop();
                        }
                        else {
                            Render();
                            audioSource.Play();
                        }
                    }
                    GUI.enabled = guiEnabled;
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Remove", GUILayout.Width(80f))) {
                        if (AudioAssetUtility.DisplayZoundRemoveDialog(targetKlip)) {
                            remove = true;
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();

            if (remove) {
                var zoundsProject = ZoundsProject.Instance;
                Undo.RecordObject(zoundsProject, "remove klip");
                AudioAssetUtility.RemoveZound(targetKlip);
                EditorUtility.SetDirty(zoundsProject);
            }

            var evt = Event.current;
            if (evt.type == EventType.ValidateCommand) {
                if (evt.commandName == "UndoRedoPerformed") {
                    RefreshSpectrumView();
                    // repaint immediately when user undo/redo to make experience feels more fluid
                    Repaint();
                }
            }
        }

        private void ValidateKlip() {
            var zoundsProject = ZoundsProject.Instance;
            if (targetKlip.trimStart < 0) {
                targetKlip.trimStart = 0;
                targetKlip.needsRender = true;
                EditorUtility.SetDirty(zoundsProject);
            }
            if (targetKlip.trimEnd < 0) {
                targetKlip.trimEnd = 0;
                targetKlip.needsRender = true;
                EditorUtility.SetDirty(zoundsProject);
            }
            if (targetKlip.audioClipRef.editorAsset is AudioClip clip) {
                if (targetKlip.trimStart > clip.length) {
                    targetKlip.trimStart = clip.length;
                    targetKlip.needsRender = true;
                    EditorUtility.SetDirty(zoundsProject);
                }
                if (targetKlip.trimEnd > clip.length) {
                    targetKlip.trimEnd = clip.length;
                    targetKlip.needsRender = true;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }
        }

        private void Render() {
            if (targetKlip == null) return;
            if (!targetKlip.needsRender) return;

            if (targetKlip == null) return;
            var originalClip = targetKlip.audioClipRef.editorAsset as AudioClip;
            if (originalClip == null) return;

            AudioClip renderedClip = AudioRenderUtility.Trim(originalClip, 
                targetKlip.trimStart, targetKlip.trimEnd);

            if (targetKlip.volumeEnvelope.enabled) {
                renderedClip = AudioRenderUtility.VolumeEnvelope(renderedClip, targetKlip.volumeEnvelope);
            }

            if (targetKlip.pitchEnvelope.enabled) {
                renderedClip = AudioRenderUtility.PitchEnvelope(renderedClip, targetKlip.pitchEnvelope);
            }

            var zoundsProject = ZoundsProject.Instance;
            var filePath = Path.Combine(zoundsProject.projectSettings.workFolderPath, targetKlip.name + " (Klip).wav");
            var reloadedAudio = AudioRenderUtility.SaveAudio(renderedClip, filePath);
            var audioRef = AudioRenderUtility.GetAudioReference(reloadedAudio);

            Undo.RecordObject(zoundsProject, "render klip");
            targetKlip.needsRender = false;
            targetKlip.renderedClipRef = audioRef;

            Undo.RecordObject(spectrumView.audioSource, "render klip");
            spectrumView.audioSource.clip = reloadedAudio;

            EditorUtility.SetDirty(zoundsProject);
            EditorUtility.SetDirty(spectrumView.audioSource);
        }

    }

}