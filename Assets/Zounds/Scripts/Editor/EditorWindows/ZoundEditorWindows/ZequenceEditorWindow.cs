using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class ZequenceEditorWindow : BaseZoundEditorWindow<Zequence> {

        public static ZequenceEditorWindow OpenWindow(Zequence zequence) {
            return OpenWindow<ZequenceEditorWindow>(zequence, new Vector2(350f, 200f));
        }

        [SerializeField] private Envelope masterVolumeEnvelopeTemp;
        private EnvelopeGUI masterVolumeEnvelopeGUI;
        [SerializeField] private List<Envelope> envelopeCache = new List<Envelope>();
        private List<EnvelopeGUI> envelopeGUICache = new List<EnvelopeGUI>();
        [SerializeField] private Vector2 scrollPos;
        private GUIContent maxDurationLabel;
        private string addMenuSearchText;
        private string createKlipSearchText;

        private GUIContent overrideToggleLabel = new GUIContent("O", "Override.\n\nIf checked, then this will override the original value of the zound. If unchecked, then this will act as a multiplier of the original value.");
        private GUIContent icon_remove;
        private GUIContent icon_duplicate;
        private GUIStyle durationTextStyle;
        private GUIContent muteLabel;
        private GUIContent soloLabel;

        private ZoundToken currentToken;
        protected override Zequence FindZoundTarget() {
            var library = ZoundsProject.Instance.zoundLibrary;
            return library.zequences.Find(k => k.id == targetZoundID);
        }

        protected override void OnInit() {
            maxDurationLabel = new GUIContent("Max Duration", "This is only used to determine editor width, and doesn't affect runtime behaviour.");
            icon_remove = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/remove"), "Remove this zound entry.");
            icon_duplicate = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/duplicate"), "Duplicate this zound entry.");
            muteLabel = new GUIContent("M", "Mute/Unmute");
            soloLabel = new GUIContent("S", "Toggle Solo");
            ValidateEnvelopeGUIs();
        }

        private void OnLostFocus() {
            ResetEnvelopGUIStates();
        }

        protected override void OnUndoRedoPerformed() {
            ResetEnvelopGUIStates();
            ValidateEnvelopeGUIs();
        }

        private void ResetEnvelopGUIStates() {
            foreach (var envelopeGUI in envelopeGUICache) {
                envelopeGUI.ResetStates();
            }
        }

        private void ValidateEnvelopeGUIs() {
            if (targetZound.masterVolumeEnvelope == null || targetZound.masterVolumeEnvelope.Count == 0) {
                targetZound.masterVolumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                EditorUtility.SetDirty(ZoundsProject.Instance);
            }
            masterVolumeEnvelopeTemp = targetZound.masterVolumeEnvelope.DeepCopy();
            if (masterVolumeEnvelopeGUI == null) masterVolumeEnvelopeGUI = new EnvelopeGUI() { name = "Master" };

            for (int i = 0; i < targetZound.zoundEntries.Count; i++) {
                var entry = targetZound.zoundEntries[i];
                if (i >= envelopeCache.Count) {
                    if (entry.volumeEnvelope == null || entry.volumeEnvelope.Count == 0) {
                        entry.volumeEnvelope = new Envelope(Zound.MinVolumeRange, Zound.MaxVolumeRange);
                        EditorUtility.SetDirty(ZoundsProject.Instance);
                    }
                    envelopeCache.Add(null);
                }
                if (i >= envelopeGUICache.Count) {
                    envelopeGUICache.Add(new EnvelopeGUI());
                }
                envelopeCache[i] = entry.volumeEnvelope.DeepCopy();
                var envelopeGUI = envelopeGUICache[i];
                envelopeGUI.name = entry.zoundId.ToString();
            }
        }

        protected override bool OnDrawGUI() {
            bool isPlaying = currentToken != null && currentToken.state == ZoundToken.State.Playing;
            bool remove = false;

            var zoundsProject = ZoundsProject.Instance;
            if (durationTextStyle == null) {
                durationTextStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                durationTextStyle.normal.textColor = new Color32(121, 183, 255, 255);
            }

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float entryHeight = lineHeight * 6f + 10f;

            GUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                float newMaxDuration = EditorGUILayout.FloatField(maxDurationLabel, targetZound.editor_maxDuration);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(zoundsProject, "change max duration");
                    targetZound.editor_maxDuration = newMaxDuration;
                    RecalculateMaxDuration();
                    EditorUtility.SetDirty(zoundsProject);
                }

                GUILayout.Space(5f);
                if (GUILayout.Button("Remove", GUILayout.Width(80f))) {
                    if (AudioAssetUtility.DisplayZoundRemoveDialog(targetZound)) {
                        remove = true;
                    }
                }

                GUILayout.Space(5f);
                if (GUILayout.Button(isPlaying ? "Stop" : "Play", GUILayout.Width(80f))) {
                    if (!isPlaying) {
                        currentToken = ZoundEngine.PlayZound(targetZound, new ZoundArgs() {
                            startImmediately = true,
                            delay = 0f,
                            volumeOverride = -1f,
                            pitchOverride = -1f,
                            chanceOverride = -1f,
                            useFixedAverageValues = true
                        });
                    }
                    else {
                        currentToken.Kill();
                    }
                }

                //bool isPaused = currentToken != null && currentToken.state == ZoundToken.State.Paused;
                //if (GUILayout.Button(isPaused ? "Resume" : "Pause", GUILayout.Width(80f))) {
                //    if (!isPaused) {
                //        if (currentToken != null && currentToken.state != ZoundToken.State.Killed) {
                //            currentToken.Pause();
                //        }
                //    }
                //    else {
                //        if (currentToken != null && currentToken.state != ZoundToken.State.Killed) {
                //            currentToken.Resume();
                //        }
                //    }
                //}
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            if (targetZound.zoundEntries.Count == 0) {
                EditorGUILayout.LabelField("No zound entry.", EditorStyles.centeredGreyMiniLabel);
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            bool darkerBG = false;
            int entryIndexToRemove = -1;
            int entryIndexToDuplicate = -1;
            for (int i = 0; i < targetZound.zoundEntries.Count; i++) {
                var zoundEntry = targetZound.zoundEntries[i];
                var entryRect = GUILayoutUtility.GetRect(1, entryHeight, GUILayout.ExpandWidth(true));
                var color = darkerBG ? new Color(0.3f, 0.3f, 0.3f, 0.2f) : new Color(0.7f, 0.7f, 0.7f, 0.2f);
                var prevGUIColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(entryRect, EditorGUIUtility.whiteTexture);
                GUI.color = prevGUIColor;
                DrawEntry(entryRect, zoundEntry, i, out bool toBeRemoved, out bool toBeDuplicated);
                if (toBeRemoved) {
                    entryIndexToRemove = i;
                }
                if (toBeDuplicated) {
                    entryIndexToDuplicate = i;
                }
                darkerBG = !darkerBG;
                GUILayout.Space(4f);
            }

            var masterRect = GUILayoutUtility.GetRect(1, entryHeight, GUILayout.ExpandWidth(true));
            DrawMasterSection(masterRect);

            GUILayout.EndScrollView();


            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool addExistingZound = false;
            bool addNewKlip = false;
            if (GUILayout.Button("+ New Klip", GUILayout.Width(110f))) {
                addNewKlip = true;
            }
            if (GUILayout.Button("+ Existing Zound", GUILayout.Width(110f))) {
                addExistingZound = true;
            }
            GUILayout.EndHorizontal();

            if (entryIndexToRemove >= 0) {
                Undo.RecordObject(zoundsProject, "remove zound entry");
                targetZound.zoundEntries.RemoveAt(entryIndexToRemove);
                EditorUtility.SetDirty(zoundsProject);
            }

            if (entryIndexToDuplicate >= 0) {
                Undo.RecordObject(zoundsProject, "duplicate zound entry");
                var serialized = JsonUtility.ToJson(targetZound.zoundEntries[entryIndexToDuplicate]);
                var duplicated = JsonUtility.FromJson<Zequence.ZoundEntry>(serialized);
                targetZound.zoundEntries.Insert(entryIndexToDuplicate + 1, duplicated);
                EditorUtility.SetDirty(zoundsProject);
            }

            if (addExistingZound) {
                AddNewEntryFromExisting();
            }
            if (addNewKlip) {
                KlipsTab.OpenCreateNewKlipDialog(klip => {
                    zoundsProject.zoundLibrary.klips.Add(klip);
                    AddZoundEntry(klip);
                }, createKlipSearchText, text => createKlipSearchText = text);
            }

            GUILayout.FlexibleSpace();

            if (isPlaying || HasAnyInstancePlaying()) {
                //Debug.Log("Repaint: " + targetZound.name);
                Repaint();
            }

            return remove;
        }

        private void DrawMasterSection(Rect rect) {
            var zoundsProject = ZoundsProject.Instance;
            var editorStyle = zoundsProject.projectSettings.editorStyle;

            var contentRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            var leftSection = new Rect(contentRect.x, contentRect.y, EditorGUIUtility.labelWidth, contentRect.height);
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
            bool tempEnable = EditorGUI.Toggle(enableEnvelopeRect, "Use Volume Envelope", targetZound.masterVolumeEnvelope.enabled);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle master volume envelope");
                targetZound.masterVolumeEnvelope.enabled = tempEnable;
                EditorUtility.SetDirty(zoundsProject);
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;





            float fieldBoxWidth = EditorGUIUtility.fieldWidth;
            var timelineRect = new Rect(rightSection.x, rightSection.y, rightSection.width - fieldBoxWidth - 5f, rightSection.height - 20f);
            var prevGUIColor = GUI.color;
            GUI.color = new Color(0.75f, 0.75f, 0.75f, 0.1f);
            GUI.DrawTexture(timelineRect, EditorGUIUtility.whiteTexture);

            if (targetZound.masterVolumeEnvelope.enabled) {
                if (targetZound.masterVolumeEnvelope.Count != masterVolumeEnvelopeTemp.Count) ValidateEnvelopeGUIs();
                if (masterVolumeEnvelopeGUI.Draw(timelineRect, masterVolumeEnvelopeTemp, editorStyle.volumeEnvelopeColor, true)) {
                    Undo.RecordObject(zoundsProject, "modify master volume envelope");
                    targetZound.masterVolumeEnvelope = masterVolumeEnvelopeTemp.DeepCopy();
                    targetZound.masterVolumeEnvelope.enabled = true;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }

            if (currentToken != null && currentToken.state != ZoundToken.State.Killed) {
                float actualDuration = CalculateZequenceDuration(currentToken.zound as Zequence, 1f);
                float adjustedWidth = timelineRect.width / targetZound.editor_maxDuration * actualDuration;
                float playerX = timelineRect.x - 1f + ((currentToken.time / currentToken.duration) * adjustedWidth);
                var playerRect = new Rect(playerX, timelineRect.y, 1f, timelineRect.height);
                GUI.DrawTexture(playerRect, EditorGUIUtility.whiteTexture);

                GUI.color = editorStyle.playerHeadColor;
                GUI.DrawTexture(playerRect, EditorGUIUtility.whiteTexture);
            }

            GUI.color = prevGUIColor;
        }

        /// <summary>
        /// Draw a specific zound entry of a zequence.
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="entry"></param>
        /// <returns>Returns true if this entry needs to be removed.</returns>
        private void DrawEntry(Rect rect, Zequence.ZoundEntry entry, int entryIndex, out bool toBeRemoved, out bool toBeDuplicated) {
            if (!ZoundDictionary.TryGetZoundById(entry.zoundId, out var zound)) {
                toBeRemoved = toBeDuplicated = false;
                return;
            }

            float entryDuration = GetEntryDuration(entry, 1f);

            var contentRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);

            var leftSection = new Rect(contentRect.x, contentRect.y, EditorGUIUtility.labelWidth, contentRect.height);
            var rightSection = new Rect(leftSection.xMax + 5f, contentRect.y, contentRect.width - leftSection.width - 5f, contentRect.height);
            DrawEntryLeftSection(leftSection, entry, zound, entryDuration);
            DrawEntryRightSection(rightSection, entry, zound, entryDuration, entryIndex, out toBeRemoved, out toBeDuplicated);
        }

        private void DrawEntryLeftSection(Rect leftSection, Zequence.ZoundEntry entry, Zound zound, float entryDuration) {
            var zoundsProject = ZoundsProject.Instance;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float currentY = leftSection.y;
            var labelRect = new Rect(leftSection.x, currentY, leftSection.width, lineHeight);
            if (GUI.Button(labelRect, zound.name, EditorStyles.boldLabel)) {
                if (zound is Klip k) KlipEditorWindow.OpenWindow(k);
                else if (zound is Zequence z) ZequenceEditorWindow.OpenWindow(z);
                else if (zound is Muzic m) Debug.LogErrorFormat("MuzicEditorWindow is not yet implemented.");
                else if (zound is Randomizer r) Debug.LogErrorFormat("RandomizerEditorWindow is not yet implemented.");
            }

            currentY += lineHeight;
            var durationRect = new Rect(leftSection.x, currentY, leftSection.width * 0.75f, lineHeight);
            string durationString = entryDuration.ToString("0.00") + " sec";
            if (entry.delay > 0f) {
                durationString += " (" + (entryDuration + entry.delay).ToString("0.00") + " sec)";
            }
            EditorGUI.LabelField(durationRect, durationString, durationTextStyle);

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 16;
            var prevFieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.fieldWidth = 40f;

            currentY += lineHeight;
            var vRect = new Rect(leftSection.x, currentY, leftSection.width - 36f, lineHeight);
            EditorGUI.BeginChangeCheck();
            float newV = EditorGUI.Slider(vRect, "V", entry.volume, Zound.MinVolumeRange, Zound.MaxVolumeRange);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "change entry volume");
                entry.volume = newV;
                EditorUtility.SetDirty(zoundsProject);
            }

            var vOverrideRect = new Rect(vRect.xMax + 4f, currentY, 20f, lineHeight);
            EditorGUI.BeginChangeCheck();
            bool overrideV = EditorGUI.Toggle(vOverrideRect, overrideToggleLabel, entry.overrideVolume);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle override entry volume");
                entry.overrideVolume = overrideV;
                EditorUtility.SetDirty(zoundsProject);
            }

            currentY += lineHeight;
            var pRect = new Rect(leftSection.x, currentY, vRect.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            float newP = EditorGUI.Slider(pRect, "P", entry.pitch, Zound.MinPitchRange, Zound.MaxPitchRange);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "change entry pitch");
                entry.pitch = newP;
                EditorUtility.SetDirty(zoundsProject);
            }

            var pOverrideRect = new Rect(vRect.xMax + 4f, currentY, 20f, lineHeight);
            EditorGUI.BeginChangeCheck();
            bool overrideP = EditorGUI.Toggle(pOverrideRect, overrideToggleLabel, entry.overridePitch);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle override entry pitch");
                entry.overridePitch = overrideP;
                EditorUtility.SetDirty(zoundsProject);
            }

            currentY += lineHeight;
            var cRect = new Rect(leftSection.x, currentY, vRect.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            float newC = EditorGUI.Slider(cRect, "C", entry.chance, Zound.MinChanceRange, Zound.MaxChanceRange);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "change entry chance");
                entry.chance = newC;
                EditorUtility.SetDirty(zoundsProject);
            }

            var cOverrideRect = new Rect(vRect.xMax + 4f, currentY, 20f, lineHeight);
            EditorGUI.BeginChangeCheck();
            bool overrideC = EditorGUI.Toggle(cOverrideRect, overrideToggleLabel, entry.overrideChance);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle override entry pitch");
                entry.overrideChance = overrideC;
                EditorUtility.SetDirty(zoundsProject);
            }

            EditorGUIUtility.labelWidth = 134f;
            currentY += lineHeight;
            var enableEnvelopeRect = new Rect(leftSection.x, currentY, vRect.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            bool tempEnable = EditorGUI.Toggle(enableEnvelopeRect, "Use Volume Envelope", entry.volumeEnvelope.enabled);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle entry volume envelope");
                entry.volumeEnvelope.enabled = tempEnable;
                EditorUtility.SetDirty(zoundsProject);
            }

            EditorGUIUtility.fieldWidth = prevFieldWidth;
            EditorGUIUtility.labelWidth = prevLabelWidth;

        }

        private void DrawEntryRightSection(Rect rightSection, Zequence.ZoundEntry entry, Zound zound, float entryDuration, int entryIndex, out bool toBeRemoved, out bool toBeDuplicated) {
            var zoundsProject = ZoundsProject.Instance;
            var editorStyle = zoundsProject.projectSettings.editorStyle;

            var delayRect = new Rect(rightSection.x, rightSection.y, rightSection.width, EditorGUIUtility.singleLineHeight);

            float fieldBoxWidth = EditorGUIUtility.fieldWidth;

            EditorGUI.BeginChangeCheck();
            float newDelay = EditorGUI.Slider(delayRect, entry.delay, 0f, targetZound.editor_maxDuration);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "change zequence entry delay");
                entry.delay = newDelay;
                RecalculateMaxDuration();
                EditorUtility.SetDirty(zoundsProject);
            }

            var timelineRect = new Rect(rightSection.x, rightSection.y + 20f, rightSection.width - fieldBoxWidth - 5f, rightSection.height - 20f);
            var prevGUIColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.1f);
            GUI.DrawTexture(timelineRect, EditorGUIUtility.whiteTexture);

            float spectrumX = (entry.delay / targetZound.editor_maxDuration) * timelineRect.width;
            float spectrumWidth = (entryDuration / targetZound.editor_maxDuration) * timelineRect.width;
            if (spectrumX + spectrumWidth > timelineRect.width) {
                RecalculateMaxDuration();
                spectrumX = (entry.delay / targetZound.editor_maxDuration) * timelineRect.width;
                spectrumWidth = (entryDuration / targetZound.editor_maxDuration) * timelineRect.width;
            }

            var spectrumRect = new Rect(timelineRect.x + spectrumX, timelineRect.y, spectrumWidth, timelineRect.height);

            if (zound is Klip klip) {
                GUI.color = editorStyle.klipWaveformBGColor;
                GUI.DrawTexture(spectrumRect, EditorGUIUtility.whiteTexture);
                GUI.color = prevGUIColor;
                var audioClip = klip.GetAudioClipReference().editorAsset as AudioClip;
                var audioTexture = AudioWaveformUtility.GetWaveformSpectrumTexture(audioClip, Mathf.FloorToInt(spectrumRect.width), Mathf.FloorToInt(spectrumRect.height), Color.black);
                if (audioTexture != null) {
                    GUI.DrawTexture(spectrumRect, audioTexture);
                }
            }
            else if (zound is Zequence zequence) {
                GUI.color = editorStyle.zequenceWaveformBGColor;
                GUI.DrawTexture(spectrumRect, EditorGUIUtility.whiteTexture);
            }
            else if (zound is Muzic muzic) {
                GUI.color = new Color32(230, 115, 115, 255);
                GUI.DrawTexture(spectrumRect, EditorGUIUtility.whiteTexture);
            }
            else if (zound is Randomizer randomizer) {
                GUI.color = new Color32(115, 230, 132, 255);
                GUI.DrawTexture(spectrumRect, EditorGUIUtility.whiteTexture);
            }
            GUI.color = prevGUIColor;


            if (entry.volumeEnvelope.enabled) {
                if (entry.volumeEnvelope.Count != envelopeCache[entryIndex].Count) ValidateEnvelopeGUIs();
                if (envelopeGUICache[entryIndex].Draw(spectrumRect, envelopeCache[entryIndex], editorStyle.volumeEnvelopeColor, true)) {
                    Undo.RecordObject(zoundsProject, "modify entry volume envelope");
                    entry.volumeEnvelope = envelopeCache[entryIndex].DeepCopy();
                    entry.volumeEnvelope.enabled = true;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }
            

            if (ZoundEngine.CullingGroups.TryGetValue(targetZound, out var playingTokens)) {
                GUI.color = ZoundsProject.Instance.projectSettings.editorStyle.playerHeadColor;
                foreach (var playingToken in playingTokens) {
                    if (playingToken == null || playingToken.state == ZoundToken.State.Killed) continue;
                    float actualDuration = CalculateZequenceDuration(playingToken.zound as Zequence, 1f);
                    float adjustedWidth = timelineRect.width / targetZound.editor_maxDuration * actualDuration;
                    float playerX = timelineRect.x - 1f + ((playingToken.time / playingToken.duration) * adjustedWidth);
                    var playerRect = new Rect(playerX, timelineRect.y, 1f, timelineRect.height);
                    GUI.DrawTexture(playerRect, EditorGUIUtility.whiteTexture);
                }
                GUI.color = prevGUIColor;
            }

            if (entry.delay >= Mathf.Epsilon) {
                var preOffsetRect = new Rect(timelineRect.x + 2f, timelineRect.center.y + 10f, 50f, 20f);
                EditorGUI.LabelField(preOffsetRect, entry.delay.ToString("0.00") + " s", durationTextStyle);
            }

            if (entry.delay + entryDuration < targetZound.editor_maxDuration) {
                float postOffset = targetZound.editor_maxDuration - entry.delay - entryDuration;
                var postOffsetRect = new Rect(timelineRect.xMax - 52f, timelineRect.center.y + 10f, 50f, 20f);
                EditorGUI.LabelField(postOffsetRect, postOffset.ToString("0.00") + " s", durationTextStyle);
            }
            GUI.color = prevGUIColor;

            toBeDuplicated = false;
            var duplicateRect = new Rect(timelineRect.xMax + 5f, timelineRect.y, rightSection.width - timelineRect.width - 5f, 20f);
            if (GUI.Button(duplicateRect, icon_duplicate)) {
                toBeDuplicated = true;
            }

            toBeRemoved = false;
            var removeRect = new Rect(duplicateRect.x, duplicateRect.yMax + 2f, duplicateRect.width, duplicateRect.height);
            if (GUI.Button(removeRect, icon_remove)) {
                toBeRemoved = true;
            }

            var muteSoloRect = new Rect(removeRect.x, removeRect.yMax + 2f, removeRect.width, removeRect.height);
            var muteRect = new Rect(muteSoloRect.x, muteSoloRect.y, muteSoloRect.width / 2f, muteSoloRect.height);
            var soloRect = new Rect(muteRect.xMax, muteRect.y, muteRect.width, muteRect.height);

            GUI.color = entry.mute ? prevGUIColor * new Color(1f, 0.6f, 0.6f, 1f) : prevGUIColor;
            if (GUI.Button(muteRect, muteLabel)) {
                Undo.RecordObject(zoundsProject, "toggle mute");
                entry.mute = !entry.mute;
                if (entry.mute) entry.solo = false;
                EditorUtility.SetDirty(zoundsProject);
            }
            GUI.color = entry.solo ? prevGUIColor * new Color(0f, 1f, 0.6f, 1f) : prevGUIColor;
            if (GUI.Button(soloRect, soloLabel)) {
                Undo.RecordObject(zoundsProject, "toggle solo");
                entry.solo = !entry.solo;
                if ( entry.solo) entry.mute = false;
                EditorUtility.SetDirty(zoundsProject);
            }
            GUI.color = prevGUIColor;
        }

        private void AddNewEntryFromExisting() {
            var zoundsProject = ZoundsProject.Instance;
            var library = zoundsProject.zoundLibrary;

            List<Zound> allZounds = new List<Zound>();
            allZounds.AddRange(library.klips);
            allZounds.AddRange(library.zequences);
            allZounds.AddRange(library.muzics);
            allZounds.AddRange(library.randomizers);
            var sortedZounds = allZounds.OrderBy(z => z.name).ToList();

            var genericMenu = new GenericMenu();
            foreach (var z in sortedZounds) {
                if (z.id == targetZoundID) continue;
                if (z is Zequence zeq && ZequenceHandler.CheckRecursiveness(zeq, targetZound)) {
                    continue;
                }

                var zound = z;
                genericMenu.AddItem(new GUIContent(zound.name), false, userData => {
                    AddZoundEntry(zound);

                }, zound);
            }

            GenericMenuPopup.Show(
                genericMenu,
                "Add Zound(s)",
                Event.current.mousePosition,
                new List<string>(),
                addMenuSearchText,
                newSearch => addMenuSearchText = newSearch,
                userData => ZoundEngine.PlayZound(userData as Zound));
        }

        private void AddZoundEntry(Zound zound) {
            var zoundsProject = ZoundsProject.Instance;
            Undo.RecordObject(zoundsProject, "add zound entry");
            var newEntry = new Zequence.ZoundEntry();
            newEntry.zoundId = zound.id;
            targetZound.zoundEntries.Add(newEntry);
            RecalculateMaxDuration();
            EditorUtility.SetDirty(zoundsProject);
            ValidateEnvelopeGUIs();
        }

        private void RecalculateMaxDuration() {
            float max = CalculateZequenceDuration(targetZound, 1f);
            if (max > targetZound.editor_maxDuration) {
                targetZound.editor_maxDuration = max;
                EditorUtility.SetDirty(ZoundsProject.Instance);
            }
        }

        private static float CalculateZequenceDuration(Zequence zequence, float parentPitch) {
            float max = 0f;
            foreach (var entry in zequence.zoundEntries) {
                if (!ZoundDictionary.TryGetZoundById(entry.zoundId, out var zound)) continue;
                if (zound is Zequence zeq && ZequenceHandler.CheckRecursiveness(zeq, zequence)) {
                    Debug.LogError(zequence.name + " is contained recursively in " + zeq.name);
                    continue;
                }
                float effectiveDuration = GetEntryDuration(entry, parentPitch) + entry.delay;
                if (effectiveDuration > max) {
                    max = effectiveDuration;
                }
            }
            return max;
        }

        private static float GetEntryDuration(Zequence.ZoundEntry entry, float parentPitch) {
            if (!ZoundDictionary.TryGetZoundById(entry.zoundId, out var zound)) return 0f;

            float effectivePitch = entry.pitch;
            if (!entry.overridePitch) {
                effectivePitch *= (zound.maxPitch + zound.minPitch) / 2f;
            }

            effectivePitch *= parentPitch;

            float zoundDuration;
            if (zound is Klip klip) {
                if (klip.GetAudioClipReference().editorAsset is AudioClip audioClip) {
                    zoundDuration = audioClip.length / effectivePitch;
                }
                else {
                    zoundDuration = 0f;
                }
            }
            else if (zound is Zequence zequence) {
                zoundDuration = CalculateZequenceDuration(zequence, effectivePitch);
            }
            else if (zound is Muzic muzic) {
                zoundDuration = 0f;
                Debug.LogError("Duration calculator for Muzic is not yet implemented.");
            }
            else if (zound is Randomizer randomizer) {
                zoundDuration = 0f;
                Debug.LogError("Duration calculator for Randomizer is not yet implemented.");
            }
            else {
                zoundDuration = 0f;
            }

            return zoundDuration;
        }
    }

}