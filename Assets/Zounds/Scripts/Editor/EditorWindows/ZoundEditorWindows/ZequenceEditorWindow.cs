using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    public class ZequenceEditorWindow : CompositeZoundEditorWindow<Zequence, ZequenceEditorWindow> {

        private GUIContent label_noPlayWeight;
        private GUIContent label_clearRenderedButton;
        private GUIContent label_renderButton;

        private bool notFoundErrorAlreadyShown;

        public static ZequenceEditorWindow OpenWindow(Zequence zequence) {
            return OpenWindow<ZequenceEditorWindow>(zequence, new Vector2(350f, 200f));
        }

        [SerializeField] private Envelope masterVolumeEnvelopeTemp;
        private EnvelopeGUI masterVolumeEnvelopeGUI;

        protected override void OnInit() {
            base.OnInit();
            label_noPlayWeight = new GUIContent("No-Play", "Chance weight for this randomizer to not play any sound.");
            label_clearRenderedButton = new GUIContent("Clear", "Clear rendered audio and use realtime mode.");
            label_renderButton = new GUIContent("Render", "Render this zequence into a new audio clip file. You can use a Mixer Group to apply effects on the clip.\nNote: This will remove randomness and create a fixed result. You can re-render until you get a version you like.");
        }

        protected override Zequence FindZoundTarget() {
            var library = ZoundsProject.Instance.zoundLibrary;
            var result = library.zequences.Find(k => k.id == targetZoundID);
            if (result == null) {
                foreach (var zequence in library.zequences) {
                    var localZeq = zequence.localZequences.Find(z => z.zequence.id == targetZoundID);
                    if (localZeq != null) result = localZeq.zequence;
                    if (result != null) break;
                }
            }
            if (result == null) {
                if (!notFoundErrorAlreadyShown) {
                    notFoundErrorAlreadyShown = true;
                    Debug.LogError("Can't find zequence target for zound id: " + targetZoundID);
                }
            }
            return result;
        }

        protected override void DrawAudioRenderingMenu() {
            bool renderClicked = false;
            GUILayout.BeginHorizontal();
            {
                var labelWidth = EditorGUIUtility.labelWidth;
                var guiEnabled = GUI.enabled;
                EditorGUIUtility.labelWidth = 90f;
                GUI.enabled = false;
#if ADDRESSABLES_INSTALLED
                var currentRenderedAudio = targetZound.renderedClipRef == null ? null : targetZound.renderedClipRef.editorAsset;
#else
                AudioClip currentRenderedAudio = null;
#endif
                EditorGUILayout.ObjectField("Render Output:", currentRenderedAudio, typeof(AudioClip), true);
                GUI.enabled = guiEnabled;

                GUILayout.Space(5f);
                GUI.enabled = guiEnabled && currentRenderedAudio != null;
                if (GUILayout.Button(label_clearRenderedButton, GUILayout.Width(60f))) {
#if ADDRESSABLES_INSTALLED
                    ZoundsWindow.ModifyZoundsProject("clear rendered audio", () => {
                        targetZound.renderedClipPath = "";
                        targetZound.renderedClipRef = new UnityEngine.AddressableAssets.AssetReference();
                    });
#endif
                }
                GUI.enabled = guiEnabled;
                GUILayout.Space(5f);
                if (GUILayout.Button(label_renderButton, GUILayout.Width(60f))) {
                    renderClicked = true;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);

            if (renderClicked) {
                OpenMixerGroupDropdown(mixerGroup => {
                    Render(mixerGroup);
                });
            }
        }

        protected override void OnDrawHeaderLayout() {
            if (targetZound.mode == CompositeZound.Mode.Randomizer) {
                Color bgColor = GUI.backgroundColor;
                var labelWidth = EditorGUIUtility.labelWidth;

                GUI.backgroundColor = Color.red;
                EditorGUIUtility.labelWidth = 50f;

                EditorGUI.BeginChangeCheck();
                int noPlayWeight = EditorGUILayout.IntField(label_noPlayWeight, targetZound.noPlayWeight);
                if (EditorGUI.EndChangeCheck()) {
                    var zoundsProject = ZoundsProject.Instance;
                    Undo.RecordObject(zoundsProject, "change no play weight");
                    targetZound.noPlayWeight = noPlayWeight;
                    EditorUtility.SetDirty(zoundsProject);
                }

                EditorGUIUtility.labelWidth = labelWidth;
                GUI.backgroundColor = bgColor;
            }
        }

        protected override void DrawEntryGroupLeftSection(Rect leftSection, CompositeZound.ZoundEntry entry, CompositeZound compositeZound, float entryDuration) {
            leftSection = DrawEntryChanceWeight(leftSection, targetZound.mode, entry);
            base.DrawEntryGroupLeftSection(leftSection, entry, compositeZound, entryDuration);
        }

        protected override void DrawEntryLeftSection(Rect leftSection, CompositeZound parentZound, CompositeZound.ZoundEntry entry, Zound zound, float entryDuration, float parentDelay) {
            leftSection = DrawEntryChanceWeight(leftSection, parentZound.mode, entry);
            base.DrawEntryLeftSection(leftSection, parentZound, entry, zound, entryDuration, parentDelay);
        }

        protected override void OnValidateEnvelopeGUIs() {
            if (targetZound.masterVolumeEnvelope == null || targetZound.masterVolumeEnvelope.Count == 0) {
                targetZound.masterVolumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                EditorUtility.SetDirty(ZoundsProject.Instance);
            }
            masterVolumeEnvelopeTemp = targetZound.masterVolumeEnvelope.DeepCopy();
            if (masterVolumeEnvelopeGUI == null) masterVolumeEnvelopeGUI = new EnvelopeGUI() { name = "Master" };
        }

        protected override void OnEndOfScrollView(float entryHeight) {
            float masterHeight = targetZound.masterVolumeEnvelope.enabled ? entryHeight : EditorGUIUtility.singleLineHeight * 2f + 10f;
            var masterRect = GUILayoutUtility.GetRect(1, masterHeight, GUILayout.ExpandWidth(true));
            DrawMasterSection(masterRect);
        }

        private void DrawMasterSection(Rect rect) {
            var zoundsProject = ZoundsProject.Instance;
            var editorStyle = zoundsProject.projectSettings.editorStyle;

            var contentRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            var leftSection = new Rect(contentRect.x, contentRect.y, leftSectionWidth, contentRect.height);
            var rightSection = new Rect(leftSection.xMax + 5f, contentRect.y, contentRect.width - leftSection.width - 5f, contentRect.height);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float currentY = leftSection.y;
            var labelRect = new Rect(leftSection.x, currentY, leftSection.width, lineHeight);
            EditorGUI.LabelField(labelRect, "MASTER", EditorStyles.boldLabel);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 134f;
            currentY += lineHeight;
            var enableEnvelopeRect = new Rect(leftSection.x, currentY, leftSection.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            bool tempEnable = EditorGUI.ToggleLeft(enableEnvelopeRect, "Use Volume Envelope", targetZound.masterVolumeEnvelope.enabled);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle master volume envelope");
                targetZound.masterVolumeEnvelope.enabled = tempEnable;
                EditorUtility.SetDirty(zoundsProject);
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;


            if (targetZound.masterVolumeEnvelope.enabled) {
                float fieldBoxWidth = EditorGUIUtility.fieldWidth;
                var timelineRect = new Rect(rightSection.x + 5f, rightSection.y, rightSection.width - fieldBoxWidth - 15f, rightSection.height - 20f);

                float globalMaxDuration = targetZound.editor_maxDuration / targetZound.minPitch;
                float zoundDuration = CalculateCompositeDuration(targetZound, targetZound.minPitch);
                float controlWidth = (zoundDuration / globalMaxDuration) * timelineRect.width;
                var timelineBGRect = new Rect(timelineRect.x, currentY, controlWidth, lineHeight * 4f);

                var prevGUIColor = GUI.color;
                GUI.color = new Color(0.75f, 0.75f, 0.75f, 0.1f);
                GUI.DrawTexture(timelineBGRect, EditorGUIUtility.whiteTexture);

                if (targetZound.masterVolumeEnvelope.enabled) {
                    if (targetZound.masterVolumeEnvelope.Count != masterVolumeEnvelopeTemp.Count) ValidateEnvelopeGUIs();
                    if (masterVolumeEnvelopeGUI.Draw(timelineBGRect, masterVolumeEnvelopeTemp, editorStyle.volumeEnvelopeColor, true)) {
                        Undo.RecordObject(zoundsProject, "modify master volume envelope");
                        targetZound.masterVolumeEnvelope = masterVolumeEnvelopeTemp.DeepCopy();
                        targetZound.masterVolumeEnvelope.enabled = true;
                        EditorUtility.SetDirty(zoundsProject);
                    }
                }

                if (currentToken != null && currentToken.state != ZoundToken.State.Killed) {
                    //float actualDuration = CalculateCompositeDuration(currentToken.zound as CompositeZound, targetZound.minPitch);
                    float actualDuration = currentToken.duration;
                    float adjustedWidth = timelineRect.width / globalMaxDuration * actualDuration;
                    float playerX = timelineRect.x - 1f + ((currentToken.time / actualDuration) * adjustedWidth);
                    var playerRect = new Rect(playerX, timelineRect.y, 1f, timelineRect.height);
                    GUI.DrawTexture(playerRect, EditorGUIUtility.whiteTexture);

                    GUI.color = editorStyle.playerHeadColor;
                    GUI.DrawTexture(playerRect, EditorGUIUtility.whiteTexture);
                }

                GUI.color = prevGUIColor;
            }

        }


        private void Render(AudioMixerGroup mixerGroup) {
            RenderZequenceToKlipPopup.EnsureAllKlipsRendered(targetZound);

            ZoundsWindow.ModifyZoundsProject("set rendered audio clip", () => {
                targetZound.renderedClipRef = new UnityEngine.AddressableAssets.AssetReference();
            });

            var token = ZoundEngine.PlayZound(targetZound, new ZoundArgs() {
                startImmediately = true,
                delay = 0f,
                volumeOverride = 1f,
                pitchOverride = 1f,
                chanceOverride = 1f,
                useFixedAverageValues = false,
                overrideMixerGroup = true,
                mixerGroupOverride = mixerGroup
            });
            var recordingData = new RecordingData(token, renderedClip => {
                var zoundsProject = ZoundsProject.Instance;
                string filePath;
                if (string.IsNullOrEmpty(targetZound.renderedClipPath)) {
                    string zoundName = targetZound.name;
                    if (targetZound.parentId != 0) {
                        zoundName += " (" + targetZound.parentId + ")";
                    }
                    filePath = Path.Combine(zoundsProject.projectSettings.workFolderPath, zoundName + " (Zequence).wav");
                }
                else {
                    filePath = targetZound.renderedClipPath;
                }
                var reloadedAudio = AudioRenderUtility.SaveAudio(renderedClip, filePath);
                var audioRef = AudioRenderUtility.GetAudioReference(reloadedAudio);
                ZoundsWindow.ModifyZoundsProject("set rendered audio clip", () => {
                    targetZound.renderedClipRef = audioRef;
                });
            });
        }

        private void OpenMixerGroupDropdown(System.Action<AudioMixerGroup> onSelectMixerGroup) {
#if ADDRESSABLES_INSTALLED
            List<AudioMixerGroup> allMixerGroups = new List<AudioMixerGroup>();
            RoutingTab.GetAllAddresableMixerGroups(ref allMixerGroups);
            if (allMixerGroups.Count == 0) {
                Debug.LogWarning("There is no MixerGroup found that is set as Addressable.");
            }
            else {
                var mixerGroupMenu = new GenericMenu();

                mixerGroupMenu.AddItem(new GUIContent("-No Effect-"), false, () => {
                    onSelectMixerGroup?.Invoke(null);
                });
                foreach (var mixerGroup in allMixerGroups) {
                    var mg = mixerGroup;
                    mixerGroupMenu.AddItem(new GUIContent(mixerGroup.name), false, () => {
                        onSelectMixerGroup?.Invoke(mg);
                    });
                }

                mixerGroupMenu.ShowAsContext();
            }
#else
            Debug.LogError("Please import Addressables package.");
#endif
        }

    }

}