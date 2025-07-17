using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class ZequenceEditorWindow : CompositeZoundEditorWindow<Zequence, ZequenceEditorWindow> {

        private GUIContent label_noPlayWeight;

        private bool notFoundErrorAlreadyShown;

        public static ZequenceEditorWindow OpenWindow(Zequence zequence) {
            return OpenWindow<ZequenceEditorWindow>(zequence, new Vector2(350f, 200f));
        }

        [SerializeField] private Envelope masterVolumeEnvelopeTemp;
        private EnvelopeGUI masterVolumeEnvelopeGUI;

        protected override void OnInit() {
            base.OnInit();
            label_noPlayWeight = new GUIContent("No-Play", "Chance weight for this randomizer to not play any sound.");
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

        protected override void DrawEntryGroupLeftSection(Rect leftSection, CompositeZound compositeZound, CompositeZound.ZoundEntry entry, float entryDuration) {
            leftSection = DrawEntryChanceWeight(leftSection, targetZound.mode, entry);
            base.DrawEntryGroupLeftSection(leftSection, compositeZound, entry, entryDuration);
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

    }

}