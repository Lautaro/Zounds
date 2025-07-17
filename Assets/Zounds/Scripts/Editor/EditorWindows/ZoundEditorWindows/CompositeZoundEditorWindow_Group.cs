using UnityEditor;
using UnityEngine;

namespace Zounds {
    public partial class CompositeZoundEditorWindow<TZound, TSelf> : BaseZoundEditorWindow<TZound, TSelf> where TZound : CompositeZound {
        
        private void DrawEntryGroup(Rect contentRect, CompositeZound compositeZound, CompositeZound.ZoundEntry entry, int entryIndex, float entryDuration, out bool toBeRemoved, out bool toBeDuplicated, out bool toBeConverted) {
            var leftSection = new Rect(contentRect.x, contentRect.y, leftSectionWidth, contentRect.height);
            var rightSection = new Rect(leftSection.xMax + 5f, contentRect.y, contentRect.width - leftSection.width - 5f, contentRect.height);
            DrawEntryGroupLeftSection(leftSection, compositeZound, entry, entryDuration);
            DrawEntryGroupRightSection(rightSection, compositeZound, entry, targetZound.minPitch, entryDuration, out toBeRemoved, out toBeDuplicated, out toBeConverted);

            if (entry.editor_foldoutExpanded) {
                float currentY = contentRect.y + groupHeaderHeight;
                if (compositeZound is Zequence zequence) {
                    float lineHeight = EditorGUIUtility.singleLineHeight;
                    float masterHeight = entry.volumeEnvelope.enabled ? lineHeight * 4f : lineHeight;
                    currentY += masterHeight;
                }
                bool darkerBG = false;
                int entryIndexToRemove = -1;
                int entryIndexToDuplicate = -1;
                int entryIndexToConvert = -1;

                for (int i = 0; i < compositeZound.zoundEntries.Count; i++) {
                    var childEntry = compositeZound.zoundEntries[i];
                    bool isZoundFound = compositeZound.TryGetEntryZound(childEntry, out var childZound);

                    Rect entryRect = new Rect(contentRect.x, currentY, contentRect.width, entryHeight);

                    var color = darkerBG ? new Color(0.25f, 0.25f, 0.25f, 0.4f) : new Color(0.55f, 0.55f, 0.55f, 0.4f);
                    var prevGUIColor = GUI.color;
                    GUI.color = color;
                    var bgRect = entryRect;
                    bgRect.x += groupEntryLeftOffset - 2f;
                    bgRect.width -= groupEntryLeftOffset - 4f;
                    GUI.DrawTexture(bgRect, EditorGUIUtility.whiteTexture);
                    GUI.color = prevGUIColor;
                    if (isZoundFound) {
                        // no more middle values
                        float parentPitch = targetZound.minPitch * compositeZound.minPitch;
                        float globalDelay = entry.delay / parentPitch;
                        DrawEntry(entryRect, compositeZound, childZound, childEntry, i, parentPitch, globalDelay, out bool childToBeRemoved, out bool childToBeDuplicated, out bool childToBeConverted);
                        if (childToBeRemoved) {
                            entryIndexToRemove = i;
                        }
                        if (childToBeDuplicated) {
                            entryIndexToDuplicate = i;
                        }
                        if (childToBeConverted) {
                            entryIndexToConvert = i;
                        }
                    }
                    darkerBG = !darkerBG;
                    currentY += entryHeight + 4f;
                }

                if (entryIndexToRemove >= 0) {
                    RemoveEntry(compositeZound, entryIndexToRemove);
                }
                if (entryIndexToDuplicate >= 0) {
                    DuplicateEntry(compositeZound, entryIndexToDuplicate);
                }
                if (entryIndexToConvert >= 0) {
                    ConvertEntry(compositeZound, entryIndexToConvert);
                }
            }
        
            
        }

        protected virtual void DrawEntryGroupLeftSection(Rect leftSection, CompositeZound compositeZound, CompositeZound.ZoundEntry entry, float entryDuration) {
            var zoundsProject = ZoundsProject.Instance;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float currentY = leftSection.y;

            var labelRect = new Rect(leftSection.x, currentY, leftSection.width, lineHeight);
            if (entry.editor_isRenaming) {
                labelRect.width = 14f;
            }
            EditorGUI.BeginChangeCheck();
            var expanded = EditorGUI.BeginFoldoutHeaderGroup(labelRect, entry.editor_foldoutExpanded, compositeZound.name);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "toggle foldout expand");
                entry.editor_foldoutExpanded = expanded;
                EditorUtility.SetDirty(zoundsProject);
            }
            EditorGUI.EndFoldoutHeaderGroup();

            if (entry.editor_isRenaming) {
                var renameRect = new Rect(labelRect.xMax, currentY, leftSection.width - labelRect.width, labelRect.height);
                EditorGUI.BeginChangeCheck();
                var newName = EditorGUI.TextField(renameRect, compositeZound.name);
                if (EditorGUI.EndChangeCheck()) {
                    newName = ZoundDictionary.EnsureUniqueZoundName(newName);
                    Undo.RecordObject(zoundsProject, "change local zequence name");
                    compositeZound.name = newName;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }

            currentY += lineHeight + 2f;
            float xOffset = 0f;

            if (compositeZound.mode == CompositeZound.Mode.Randomizer) {
                xOffset += groupEntryLeftOffset;
                var noPlayRect = new Rect(leftSection.x + xOffset, currentY, 22f, 20f);
                xOffset += noPlayRect.width + 2f;

                Color bgColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.red;

                var noPlayWeight = EditorGUI.IntField(noPlayRect, compositeZound.noPlayWeight);
                if (noPlayWeight != compositeZound.noPlayWeight) {
                    Undo.RecordObject(zoundsProject, "change local zequence no-play weight");
                    compositeZound.noPlayWeight = noPlayWeight;
                    EditorUtility.SetDirty(zoundsProject);
                }

                GUI.backgroundColor = bgColor;
            }

            var modeRect = new Rect(leftSection.x + xOffset, currentY + 1f, leftSection.width - xOffset, 20f);
            EditorGUI.BeginChangeCheck();
            var newMode = (CompositeZound.Mode)EditorGUI.EnumPopup(modeRect, compositeZound.mode);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "change local zequence mode");
                compositeZound.mode = newMode;
                EditorUtility.SetDirty(zoundsProject);
            }

            currentY += 22f;

            var durationRect = new Rect(leftSection.x, currentY, leftSection.width * 0.75f, lineHeight);
            string durationString = entryDuration.ToString("0.00") + " sec";
            if (entry.delay > 0f) {
                durationString += " (" + (entryDuration + entry.delay).ToString("0.00") + " sec)";
            }
            EditorGUI.LabelField(durationRect, durationString, durationTextStyle);

            if (compositeZound is Zequence zeq) {
                float prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 134f;
                currentY += lineHeight;
                var enableEnvelopeRect = new Rect(leftSection.x, currentY, leftSection.width, lineHeight);
                EditorGUI.BeginChangeCheck();
                bool tempEnable = EditorGUI.ToggleLeft(enableEnvelopeRect, "Use Group Volume Envelope", entry.volumeEnvelope.enabled);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(zoundsProject, "toggle master volume envelope");
                    entry.volumeEnvelope.enabled = tempEnable;
                    EditorUtility.SetDirty(zoundsProject);
                }
            }
        }

        private void DrawEntryGroupRightSection(Rect rightSection, CompositeZound compositeZound, CompositeZound.ZoundEntry entry, float parentPitch, float entryDuration, out bool toBeRemoved, out bool toBeDuplicated, out bool toBeConverted) {
            var zoundsProject = ZoundsProject.Instance;
            var editorStyle = zoundsProject.projectSettings.editorStyle;
            float currentY = rightSection.y;
            float lineHeight = EditorGUIUtility.singleLineHeight;

            toBeRemoved = toBeDuplicated = toBeConverted = false;

            var inspectorRect = new Rect(rightSection.x, currentY, rightSection.width, lineHeight);
            localZequenceInspector.DrawSimple(inspectorRect, compositeZound, false, false);

            currentY += lineHeight + 2f;

            var renameButtonRect = new Rect(rightSection.x, currentY, 60f, 20f);
            if (GUI.Button(renameButtonRect, entry.editor_isRenaming ? "Done" : "Rename")) {
                entry.editor_isRenaming = !entry.editor_isRenaming;
                EditorUtility.SetDirty(zoundsProject);
                if (!entry.editor_isRenaming) {
                    GUI.FocusControl(null);
                }
            }

            float buttonWidth = (rightSection.width - renameButtonRect.width - 2f) / 5f - 2f;
            var duplicateRect = new Rect(renameButtonRect.xMax + 2f, currentY, buttonWidth, 20f);
            var removeRect = new Rect(duplicateRect.xMax + 2f, duplicateRect.y, duplicateRect.width, duplicateRect.height);
            var muteRect = new Rect(removeRect.xMax + 2f, duplicateRect.y, duplicateRect.width, duplicateRect.height);
            var soloRect = new Rect(muteRect.xMax + 2f, duplicateRect.y, duplicateRect.width, duplicateRect.height);
            var conversionRect = new Rect(soloRect.xMax + 2f, duplicateRect.y, duplicateRect.width, duplicateRect.height);

            toBeDuplicated = false;
            if (GUI.Button(duplicateRect, icon_duplicateEntry)) {
                toBeDuplicated = true;
            }

            toBeRemoved = false;
            if (GUI.Button(removeRect, icon_removeEntry)) {
                toBeRemoved = true;
            }

            var prevGUIColor = GUI.color;

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
                if (entry.solo) entry.mute = false;
                EditorUtility.SetDirty(zoundsProject);
            }
            GUI.color = prevGUIColor;

            toBeConverted = false;
            if (compositeZound.originalId == 0) {
                if (GUI.Button(conversionRect, icon_makeShared)) {
                    toBeConverted = true;
                }
            }
            else {
                if (GUI.Button(conversionRect, icon_reconnectToShared)) {
                    toBeConverted = true;
                }
            }

            currentY += 22f;
            var delayRect = new Rect(rightSection.x, currentY, rightSection.width, lineHeight);

            EditorGUI.BeginChangeCheck();
            float newDelay = EditorGUI.Slider(delayRect, entry.delay, 0f, targetZound.editor_maxDuration);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(zoundsProject, "change zequence entry delay");
                entry.delay = newDelay;
                RecalculateMaxDuration();
                EditorUtility.SetDirty(zoundsProject);
            }

            currentY += lineHeight + 2f;

            if (compositeZound is Zequence zeq) {
                // no more middle values
                float globalMaxDuration = targetZound.editor_maxDuration / targetZound.minPitch;
                float globalDelay = entry.delay / targetZound.minPitch;
                float fieldBoxWidth = EditorGUIUtility.fieldWidth;
                float totalWidth = rightSection.width - fieldBoxWidth - 15f;
                var parentOffset = (globalDelay / globalMaxDuration) * totalWidth;
                float delayRectWidth = totalWidth - parentOffset;

                float controlWidth = (entryDuration / globalMaxDuration) * totalWidth;

                if (entry.volumeEnvelope.enabled) {
                    //var timelineRect = new Rect(rightSection.x + parentOffset + 5f, currentY, delayRectWidth, lineHeight * 4f);

                    var timelineRect = new Rect(rightSection.x + 5f, currentY, totalWidth, lineHeight * 4f);
                    var timelineBGRect = new Rect(rightSection.x + parentOffset + 5f, currentY, controlWidth, lineHeight * 4f);

                    GUI.color = new Color(0.75f, 0.75f, 0.75f, 0.1f);
                    GUI.DrawTexture(timelineBGRect, EditorGUIUtility.whiteTexture);

                    if (entry.volumeEnvelope.enabled) {
                        var envelopeCache = GetAndValidateEnvelopeCache(entry);
                        if (envelopeCache.envelopeGUI.Draw(timelineBGRect, envelopeCache.envelope, editorStyle.volumeEnvelopeColor, true)) {
                            Undo.RecordObject(zoundsProject, "modify group volume envelope");
                            entry.volumeEnvelope = envelopeCache.envelope.DeepCopy();
                            entry.volumeEnvelope.enabled = true;
                            EditorUtility.SetDirty(zoundsProject);
                        }
                    }

                    if (ZoundEngine.CullingGroups.TryGetValue(compositeZound, out var playingTokens)) {
                        GUI.color = ZoundsProject.Instance.projectSettings.editorStyle.playerHeadColor;
                        foreach (var playingToken in playingTokens) {
                            if (playingToken == null || playingToken.state == ZoundToken.State.Killed) continue;
                            //float actualDuration = CalculateCompositeDuration(playingToken.zound as CompositeZound, parentPitch);
                            float actualDuration = playingToken.duration;
                            float adjustedWidth = timelineRect.width / globalMaxDuration * actualDuration;
                            float playerX = timelineBGRect.x - 1f + ((playingToken.time / actualDuration) * adjustedWidth);
                            var playerRect = new Rect(playerX, timelineRect.y, 1f, timelineRect.height);
                            GUI.DrawTexture(playerRect, EditorGUIUtility.whiteTexture);
                        }
                        GUI.color = prevGUIColor;
                    }

                    GUI.color = prevGUIColor;
                    currentY += timelineRect.height;
                }
                else {
                    currentY += lineHeight;
                }
            }

            if (entry.editor_foldoutExpanded) {

                for (int i = 0; i < compositeZound.zoundEntries.Count; i++) {
                    currentY += entryHeight + 4f;
                }
                currentY += 6f;

                var localKlipRect = new Rect(rightSection.x, currentY, 85f, lineHeight);
                //var localZequenceRect = new Rect(localKlipRect.xMax + 4f, currentY, 125f, lineHeight);
                //var sharedZoundRect = new Rect(localZequenceRect.xMax + 4f, currentY, 105f, lineHeight);
                var sharedZoundRect = new Rect(localKlipRect.xMax + 4f, currentY, 105f, lineHeight);

                float xOffset = rightSection.xMax - sharedZoundRect.xMax;
                localKlipRect.x += xOffset;
                //localZequenceRect.x += xOffset;
                sharedZoundRect.x += xOffset;

                if (GUI.Button(localKlipRect, "+ Local Klip", EditorStyles.toolbarButton)) {
                    KlipsTab.OpenCreateNewKlipDialog(klip => {
                        klip.parentId = compositeZound.id;
                        compositeZound.localKlips.Add(klip);
                        AddNewZoundEntry(compositeZound, klip, true);
                    }, createKlipSearchText, text => createKlipSearchText = text);
                }
                //if (GUI.Button(localZequenceRect, "+ Local Zequence", EditorStyles.toolbarButton)) {
                //    Debug.Log("Nested Local Zequence is not supported.");
                //}
                if (GUI.Button(sharedZoundRect, "+ Shared Zound", EditorStyles.toolbarButton)) {
                    AddNewEntryFromExisting(compositeZound);
                }
            }
        }

    }
}
