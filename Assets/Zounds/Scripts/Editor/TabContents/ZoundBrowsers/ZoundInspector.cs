using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    /// <summary>
    /// Draws the detail fields and action buttons for a single zound.
    /// Used by BaseZoundTab in both singlecolumn and multicolumn layouts.
    ///
    /// Three public draw methods exist depending on context:
    ///   DrawMulticolumn   — full inspector panel that expands below a multicolumn row.
    ///                       Lays out 5 columns: [Edit+MS] [Fields A] [Fields B] [Tags] [Route/Conv/Dup/Del]
    ///   DrawSinglecolumn  — draws into pre-computed rects passed by DrawSinglecolumnRow.
    ///                       Edit and M/S rects come from the left; fields fill the middle;
    ///                       Route/Conv/Dup/Del sit in the right-group rect.
    ///   DrawSimple        — a compact read-only strip used inside Zequence editor sub-rows.
    /// </summary>
    public class ZoundInspector<TZound> where TZound : Zound {

        private BaseZoundTab<TZound> parentTab;
        // inspectorColumns[0] = left buttons (Edit + M/S)
        // inspectorColumns[1] = primary fields (Name / Vol / Pitch)
        // inspectorColumns[2] = secondary fields (overflow from [1])
        // inspectorColumns[3] = tags field
        // inspectorColumns[4] = right action buttons (Route / Conv / Dup / Del)
        private Rect[] inspectorColumns = new Rect[5];

        private GUIContent label_volume = new GUIContent("V", "Volume");
        private GUIContent label_pitch = new GUIContent("P", "Pitch");
        private GUIContent label_chance = new GUIContent("C", "Chance");
        private GUIContent icon_openEditor;
        private GUIContent icon_openEditorKlip;
        private GUIContent icon_openEditorZequence;
        private GUIContent icon_addMissing;
        private GUIContent icon_routingOn;
        private GUIContent icon_routingOff;
        private GUIContent icon_convert;
        private GUIContent icon_remove;
        private GUIContent icon_convertToZequence;
        private GUIContent icon_duplicate;
        private GUIContent muteLabel;
        private GUIContent soloLabel;
        private GUIStyle tagsLabelStyle;

        //private bool nameHasDrawn; // Not needed since this will be drawn first anyway.
        private bool volumeHasDrawn;
        private bool pitchHasDrawn;
        private bool chanceHasDrawn;

        private float lastTagsWidth = 0f;

        public GUIStyle GetTagsLabelStyle() => tagsLabelStyle;
        public float GetLastTagsWidth() => lastTagsWidth;

        public ZoundInspector(BaseZoundTab<TZound> parentTab) {
            this.parentTab = parentTab;
            icon_openEditor = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/open-editor"), "Open editor.");
            icon_openEditorKlip = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/open-editor-klip"), "Open Klip editor.");
            icon_openEditorZequence = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/open-editor-zequence"), "Open Zequence editor.");
            icon_addMissing = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/add-new"), "Add as a new zound.");
            icon_routingOn = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/routing-on"), "Set manual routing.");
            icon_routingOff = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/routing-off"), "Set manual routing.");
            icon_convert = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/convert"), "Convert to Klip.");
            icon_remove = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/remove"), "Remove this zound.");
            icon_convertToZequence = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/convert-zequence"), "Convert this Klip to Zequence.");
            icon_duplicate = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/duplicate"), "Duplicate this zound.");
            muteLabel = new GUIContent("M", "Mute/Unmute");
            soloLabel = new GUIContent("S", "Toggle Solo");
            tagsLabelStyle = new GUIStyle();
            tagsLabelStyle.normal.textColor = new Color32(163, 198, 255, 255);
            tagsLabelStyle.wordWrap = true;
            tagsLabelStyle.clipping = TextClipping.Clip;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // MULTICOLUMN INSPECTOR
        // Drawn inside a GUILayout HelpBox block that expands below the selected row.
        // Height is animated via BaseZoundTab.inspectorAnimFloat (smooth open/close).
        // Uses BeginClip/EndClip per column for clean overflow clipping without Begin/EndArea.
        // ──────────────────────────────────────────────────────────────────────────
        public void DrawMulticolumn(Zound zoundToInspect, float inspectorHeight) {
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect.IsClipOrLocalZound());

            ResetState();
            var browserSettings = ZoundsProject.Instance.browserSettings;
            int fieldCount = 0;
            if (browserSettings.showNameField) fieldCount++;
            if (browserSettings.showVolume) fieldCount++;
            if (browserSettings.showPitch) fieldCount++;
            if (browserSettings.showChance) fieldCount++;

            GUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(inspectorHeight), GUILayout.ExpandWidth(true));
            {
                var inspectorRect = GUILayoutUtility.GetRect(1, inspectorHeight, GUILayout.ExpandWidth(true));
                // extra heights are for tags only
                inspectorRect.height = Mathf.Min(inspectorHeight, BaseZoundTab<TZound>.inspectorHeight);

                float fieldWidthMultiplier;
                float tagsWidthMultiplier;

                if (browserSettings.showTags) {
                    if (fieldCount > 2) {
                        fieldWidthMultiplier = 0.4f;
                        tagsWidthMultiplier = 0.2f;
                    }
                    else if (fieldCount > 0) {
                        fieldWidthMultiplier = 0.5f;
                        tagsWidthMultiplier = 0.5f;
                    }
                    else {
                        fieldWidthMultiplier = 0f;
                        tagsWidthMultiplier = 1f;
                    }
                }
                else {
                    if (fieldCount > 2) {
                        fieldWidthMultiplier = 0.5f;
                    }
                    else if (fieldCount > 0) {
                        fieldWidthMultiplier = 1f;
                    }
                    else {
                        fieldWidthMultiplier = 0f;
                    }
                    tagsWidthMultiplier = 0f;
                }

                float buttonWidth = 30f;

                if (browserSettings.showOpenEditor) fieldCount++;
                if (browserSettings.showMute || browserSettings.showSolo) fieldCount++;
                float msGapMC = BaseZoundTab<TZound>.MUTE_SOLO_GAP;
                float leftButtonsWidth = (browserSettings.showOpenEditor ? buttonWidth : 0f);
                if (browserSettings.showMute || browserSettings.showSolo) leftButtonsWidth += 24f; // 24f for mute/solo (vertical stacked in multicolumn)

                // Convert-to-Zequence excluded — see DrawRemoveButton comment.
                float baseRemoveRectWidth = 0f;
                if (browserSettings.showRouting) baseRemoveRectWidth += buttonWidth;
                if (browserSettings.showDuplicate) baseRemoveRectWidth += buttonWidth;
                if (browserSettings.showRemove) baseRemoveRectWidth += buttonWidth;
                float removeRectWidth = baseRemoveRectWidth;

                float mcLeftGap  = leftButtonsWidth > 0 ? BaseZoundTab<TZound>.LEFT_BUTTONS_TO_NAME_GAP : 0f;
                float mcRightGap = baseRemoveRectWidth > 0 ? BaseZoundTab<TZound>.INSPECTOR_TO_REMOVE_GAP : 0f;
                inspectorColumns[0] = new Rect(inspectorRect.x, inspectorRect.y, leftButtonsWidth + mcLeftGap, inspectorRect.height);
                inspectorRect.x += inspectorColumns[0].width;
                inspectorRect.width -= (inspectorColumns[0].width + baseRemoveRectWidth + mcRightGap);
                inspectorColumns[1] = new Rect(inspectorRect.x, inspectorRect.y, inspectorRect.width * fieldWidthMultiplier, inspectorRect.height);
                inspectorColumns[2] = new Rect(inspectorColumns[1].xMax, inspectorColumns[1].y, fieldCount > 2 ? inspectorColumns[1].width : 0f, inspectorRect.height);
                inspectorColumns[3] = new Rect(inspectorColumns[2].xMax, inspectorColumns[2].y, inspectorRect.width * tagsWidthMultiplier, inspectorRect.height);
                inspectorColumns[4] = new Rect(inspectorColumns[3].xMax + mcRightGap, inspectorColumns[3].y, removeRectWidth, inspectorRect.height);

                float lineHeight = EditorGUIUtility.singleLineHeight;

                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 12f;

                bool isMissingZound = !(zoundToInspect is ClipZound) && zoundToInspect.id == 0;

                GUI.BeginClip(inspectorColumns[0]);
                {
                    float currentX = 0f;
                    if (browserSettings.showOpenEditor) {
                        var editorButtonRect = new Rect(currentX, 0, 30f, inspectorColumns[0].height);
                        DrawOpenEditorButton(editorButtonRect, zoundToInspect, isMissingZound);
                        currentX += 30f;
                    }
                    if (!isMissingZound && (browserSettings.showMute || browserSettings.showSolo)) {
                        DrawMuteSoloButtonsVertical(new Rect(currentX, 0, 24f, inspectorColumns[0].height), zoundToInspect);
                    }
                }
                GUI.EndClip();

                if (!isMissingZound) {

                    GUI.BeginClip(inspectorColumns[1]);
                    {
                        Rect fieldRect0 = new Rect(0f, 0f, inspectorColumns[1].width - 4f, lineHeight);
                        Rect fieldRect1 = new Rect(0f, 20f, inspectorColumns[1].width - 4f, lineHeight);

                        if (browserSettings.showNameField)
                            DrawNameField(fieldRect0, zoundToInspect);
                        else if (browserSettings.showVolume)
                            DrawVolumeField(fieldRect0, zoundToInspect);
                        else if (browserSettings.showPitch)
                            DrawPitchField(fieldRect0, zoundToInspect);
                        else if (browserSettings.showChance)
                            DrawChanceField(fieldRect0, zoundToInspect);

                        if (browserSettings.showVolume && !volumeHasDrawn)
                            DrawVolumeField(fieldRect1, zoundToInspect);
                        else if (browserSettings.showPitch && !pitchHasDrawn)
                            DrawPitchField(fieldRect1, zoundToInspect);
                        else if (browserSettings.showChance && !chanceHasDrawn)
                            DrawChanceField(fieldRect1, zoundToInspect);
                    }
                    GUI.EndClip();

                    GUI.BeginClip(inspectorColumns[2]);
                    {
                        Rect fieldRect2 = new Rect(0f, 0f, inspectorColumns[2].width, lineHeight);
                        Rect fieldRect3 = new Rect(0f, 20f, inspectorColumns[2].width, lineHeight);

                        if (browserSettings.showVolume && !volumeHasDrawn)
                            DrawVolumeField(fieldRect2, zoundToInspect);
                        else if (browserSettings.showPitch && !pitchHasDrawn)
                            DrawPitchField(fieldRect2, zoundToInspect);
                        else if (browserSettings.showChance && !chanceHasDrawn)
                            DrawChanceField(fieldRect2, zoundToInspect);

                        if (browserSettings.showChance && !chanceHasDrawn)
                            DrawChanceField(fieldRect3, zoundToInspect);
                    }
                    GUI.EndClip();

                    inspectorColumns[3].height = inspectorHeight; // special case, dynamic height for tags
                    GUI.BeginClip(inspectorColumns[3]);
                    {
                        DrawTagsField(new Rect(4f, 0, inspectorColumns[3].width - 4f, inspectorColumns[3].height), zoundToInspect);
                    }
                    GUI.EndClip();
                }

                if (removeRectWidth > 0) {
                    GUI.BeginClip(inspectorColumns[4]);
                    {
                        DrawRemoveButton(new Rect(0, 0, removeRectWidth, inspectorColumns[4].height), zoundToInspect, isMissingZound);
                    }
                    GUI.EndClip();
                }

                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            GUILayout.EndHorizontal();

            GUI.enabled = guiEnabled;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // SINGLECOLUMN INSPECTOR
        // All rects are pre-computed by DrawSinglecolumnRow in BaseZoundTab.
        //   editButtonRect   — left-most, the open-editor / convert-clip-to-klip button.
        //   muteSoloRect     — M and S buttons, stacked vertically when multipleRows is true.
        //   removeButtonRect — right-most group: Route / Conv / Dup / Del.
        //   fieldsRect       — the middle area shared by Vol / Pitch / Chance / Tags fields.
        // ──────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Draws the singlecolumn row contents into the provided pre-computed rects.
        /// When <paramref name="tagsRect"/> is non-zero (two-row mode), tags are drawn
        /// there instead of inside <paramref name="fieldsRect"/>, giving tags a dedicated
        /// right zone that spans both rows.
        /// </summary>
        public void DrawZoundSinglecolumn(Rect editButtonRect, Rect muteSoloRect, Rect removeButtonRect, Rect fieldsRect, Zound zoundToInspect, Rect tagsRect = default) {
            var guiEnabled = GUI.enabled;

            ResetState();
            var browserSettings = ZoundsProject.Instance.browserSettings;

            // When tagsRect is provided, tags are drawn separately — exclude from field count.
            bool tagsInSeparateZone = tagsRect != default && tagsRect.width > 1f;

            int fieldCount = 0;
            if (browserSettings.showNameField) fieldCount++;
            if (browserSettings.showVolume) fieldCount++;
            if (browserSettings.showPitch) fieldCount++;
            if (browserSettings.showChance) fieldCount++;
            if (browserSettings.showTags && !tagsInSeparateZone) fieldCount++;
            float fieldWidth = (fieldsRect.width - 4f) / Mathf.Max(1, fieldCount);
            Rect fieldRect = fieldsRect;
            fieldRect.width = fieldWidth - 4f;

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 12f;

            bool isMissingZound = !(zoundToInspect is ClipZound) && zoundToInspect.id == 0;

            if (browserSettings.showOpenEditor) {
                DrawOpenEditorButton(editButtonRect, zoundToInspect, isMissingZound);
            }
            GUI.enabled = guiEnabled && !(zoundToInspect.IsClipOrLocalZound());

            if (!isMissingZound && (browserSettings.showMute || browserSettings.showSolo)) {
                DrawMuteSoloButtonsVertical(muteSoloRect, zoundToInspect);
            }

            if (browserSettings.showRouting || browserSettings.showDuplicate || browserSettings.showRemove) {
                DrawRemoveButton(removeButtonRect, zoundToInspect, isMissingZound);
            }

            if (!isMissingZound) {
                if (browserSettings.showNameField) {
                    DrawNameField(fieldRect, zoundToInspect);
                    fieldRect.x += fieldWidth;
                }
                if (browserSettings.showVolume) {
                    DrawVolumeField(fieldRect, zoundToInspect);
                    fieldRect.x += fieldWidth;
                }
                if (browserSettings.showPitch) {
                    DrawPitchField(fieldRect, zoundToInspect);
                    fieldRect.x += fieldWidth;
                }
                if (browserSettings.showChance) {
                    DrawChanceField(fieldRect, zoundToInspect);
                    fieldRect.x += fieldWidth;
                }
                if (browserSettings.showTags) {
                    if (tagsInSeparateZone) {
                        // Draw tags filling the dedicated right zone (spans both rows in two-row mode).
                        DrawTagsField(tagsRect, zoundToInspect);
                    }
                    else {
                        DrawTagsField(fieldRect, zoundToInspect);
                        fieldRect.x += fieldWidth;
                    }
                }
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUI.enabled = guiEnabled;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // SIMPLE INSPECTOR
        // Used by the Zequence editor to draw compact sub-rows for each nested zound.
        // No edit/right-group buttons; just M/S (unless isLocalZound) + Name + Vol + Pitch + Chance + Tags.
        // ──────────────────────────────────────────────────────────────────────────
        public void DrawSimple(Rect fieldsRect, Zound zoundToInspect, bool isLocalZound, bool drawName = true, bool drawTags = true) {
            if (zoundToInspect == null) return;
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect is ClipZound);

            ResetState();
            var browserSettings = ZoundsProject.Instance.browserSettings;
            int fieldCount = 3;
            if (drawName) fieldCount++;
            if (drawTags) fieldCount++;
            float muteSoloWidth = isLocalZound? 0f : 44f;
            float fieldWidth = (fieldsRect.width - muteSoloWidth) / fieldCount;
            Rect fieldRect = fieldsRect;

            if (!isLocalZound) {
                DrawMuteSoloButtonsHorizontal(new Rect(fieldRect.x, fieldRect.y, muteSoloWidth, fieldRect.height), zoundToInspect);
            }

            fieldRect.x += muteSoloWidth;
            fieldRect.width = fieldWidth - 4f;

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 12f;

            if (drawName) {
                DrawNameField(fieldRect, zoundToInspect);
                fieldRect.x += fieldWidth;
            }

            DrawVolumeField(fieldRect, zoundToInspect);
            fieldRect.x += fieldWidth;

            DrawPitchField(fieldRect, zoundToInspect);
            fieldRect.x += fieldWidth;

            DrawChanceField(fieldRect, zoundToInspect);
            fieldRect.x += fieldWidth;

            if (drawTags) {
                DrawTagsField(fieldRect, zoundToInspect, true);
                fieldRect.x += fieldWidth;
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUI.enabled = guiEnabled;
        }

        // Resets the "has this field been drawn yet?" flags before each draw call.
        // These flags prevent the same field from being drawn twice when the inspector
        // has fewer columns than fields (overflow logic in DrawMulticolumn column 2).
        private void ResetState() {
            //nameHasDrawn = false;
            volumeHasDrawn = false;
            pitchHasDrawn = false;
            chanceHasDrawn = false;
        }

        // ── Left-side edit / convert / add-missing button ────────────────────────
        // The same rect is reused for three different actions depending on zound type:
        //   Missing zound  → "Add as new" button (green confirm style)
        //   ClipZound      → "Convert to Klip" button (wraps the raw AudioClip into a Klip)
        //   Klip/Zequence  → "Open editor" button (opens the waveform / sequence editor)
        private void DrawOpenEditorButton(Rect rect, Zound zoundToInspect, bool isMissingZound) {
            if (isMissingZound) {
                if (ZUI.Button(rect, icon_addMissing, ZUI.Style.Confirm)) {  // Confirm intentional — green to stand out for missing-zound action
                    RemoveMissingZound(zoundToInspect);
                    ConsolidatedTab.OpenAddNewZoundMenu(zoundToInspect.name);
                }
            }
            else if (zoundToInspect is ClipZound clipZound) {
                if (ZUI.Button(rect, icon_convert, ZUI.Style.ZoundBtnFlat)) {
                    if (EditorUtility.DisplayDialog("Convert to Klip: " + clipZound.name, "Convert this into audio clip a Klip?\n" + clipZound.name, "Convert", "Cancel")) {
                        if (parentTab is KlipsTab klipsTab) {
                            klipsTab.ConvertClipToKlip(clipZound);
                        }
                        else if (parentTab is ConsolidatedTab consolidatedTab) {
                            consolidatedTab.ConvertClipToKlip(clipZound);
                        }
                    }
                }
            }
            else {
                GUIContent icon = (zoundToInspect is Klip) ? icon_openEditorKlip :
                                  (zoundToInspect is Zequence) ? icon_openEditorZequence :
                                  icon_openEditor;
                if (ZUI.Button(rect, icon, ZUI.Style.ZoundBtnFlat)) {
                    parentTab.OpenZoundEditor(zoundToInspect);
                }
            }
        }

        // ── Mute / Solo buttons ───────────────────────────────────────────────────
        // Vertical variant: if both M and S are shown and the rect is narrow (≤24px wide),
        // M is on top and S is below (used in singlecolumn multipleRows mode and multicolumn).
        // If the rect is wide enough, M and S are placed side by side horizontally.
        // Horizontal variant (DrawMuteSoloButtonsHorizontal): always side-by-side, used in DrawSimple.
        private void DrawMuteSoloButtonsVertical(Rect muteSoloRect, Zound zoundToInspect) {
            var browserSettings = ZoundsProject.Instance.browserSettings;

            Rect muteRect = muteSoloRect;
            Rect soloRect = muteSoloRect;

            float msGap = BaseZoundTab<TZound>.MUTE_SOLO_GAP;
            bool bothVisible = browserSettings.showMute && browserSettings.showSolo;
            ZUICornerMask muteMask  = ZUICornerMask.None;
            ZUICornerMask soloMask  = ZUICornerMask.None;
            if (bothVisible) {
                if (muteSoloRect.width > 24f) {
                    muteRect.width = (muteSoloRect.width - msGap) / 2f;
                    soloRect = muteRect;
                    soloRect.x = muteRect.xMax + msGap;
                    muteMask = ZUICornerMask.Left;
                    soloMask = ZUICornerMask.Right;
                }
                else {
                    muteRect.height = (muteSoloRect.height - msGap) / 2f;
                    soloRect = muteRect;
                    soloRect.y = muteRect.yMax + msGap;
                    muteMask = ZUICornerMask.Top;
                    soloMask = ZUICornerMask.Bottom;
                }
            }

            if (browserSettings.showMute) {
                if (ZUI.Toggle(muteRect, zoundToInspect.mute, muteLabel, ZUI.Style.ZoundBtnFlatToggle, ZUI.PaletteColor("Warning", ZUIPaletteSlot.Primary, new Color(.70f, .42f, .08f, 1f)), muteMask) != zoundToInspect.mute) {
                    ToggleMute(zoundToInspect);
                }
            }
            if (browserSettings.showSolo) {
                if (ZUI.Toggle(soloRect, zoundToInspect.solo, soloLabel, ZUI.Style.ZoundBtnFlatToggle, ZUI.PaletteColor("Confirm", ZUIPaletteSlot.Primary, new Color(.14f, .34f, .14f, 1f)), soloMask) != zoundToInspect.solo) {
                    ToggleSolo(zoundToInspect);
                }
            }
        }

        private void DrawMuteSoloButtonsHorizontal(Rect muteSoloRect, Zound zoundToInspect) {
            var muteRect = muteSoloRect;
            muteRect.width = 20f;
            muteRect.width -= 0.25f;
            var soloRect = muteRect;
            soloRect.x = muteRect.xMax + 1f;

            if (ZUI.Toggle(muteRect, zoundToInspect.mute, muteLabel, ZUI.Style.ZoundBtnFlatToggle, ZUI.PaletteColor("Warning", ZUIPaletteSlot.Primary, new Color(.70f, .42f, .08f, 1f)), ZUICornerMask.Left) != zoundToInspect.mute) {
                ToggleMute(zoundToInspect);
            }
            if (ZUI.Toggle(soloRect, zoundToInspect.solo, soloLabel, ZUI.Style.ZoundBtnFlatToggle, ZUI.PaletteColor("Confirm", ZUIPaletteSlot.Primary, new Color(.14f, .34f, .14f, 1f)), ZUICornerMask.Right) != zoundToInspect.solo) {
                ToggleSolo(zoundToInspect);
            }
        }

        private static void ToggleSolo(Zound zoundToInspect) {
            ZoundsWindow.ModifyZoundsProject("solo zound", () => {
                zoundToInspect.solo = !zoundToInspect.solo;
                if (zoundToInspect.solo) zoundToInspect.mute = false;
                ZoundsProject.Instance.zoundLibrary.soloStatusNeedsUpdate = true;
            });
        }

        private static void ToggleMute(Zound zoundToInspect) {
            ZoundsWindow.ModifyZoundsProject("mute zound", () => {
                zoundToInspect.mute = !zoundToInspect.mute;
                if (zoundToInspect.mute) zoundToInspect.solo = false;
                ZoundsProject.Instance.zoundLibrary.soloStatusNeedsUpdate = true;
            });
        }

        // ── Right action button group: Route / Duplicate / Remove ──
        // All buttons share the same rect, divided equally with ZoundItem_spacing gaps.
        // Button count is calculated first so each slot gets the same width.
        // isMissingZound suppresses all buttons except Remove (which clears the missing entry).
        // Buttons are disabled during play mode (removing/duplicating at runtime is unsafe).
        // Note: Convert-to-Zequence is intentionally absent here — it lives in the Klip editor.
        private void DrawRemoveButton(Rect rect, Zound zoundToInspect, bool isMissingZound) {
            var browserSettings = ZoundsProject.Instance.browserSettings;
            bool guiEnabled = GUI.enabled;
            if (isMissingZound) GUI.enabled = true;
            else GUI.enabled = guiEnabled && !Application.isPlaying;

            // Convert-to-Zequence is intentionally excluded from the list view — it will be
            // accessible from the Klip editor instead. This keeps Klip and Zeq rows identical.
            int buttonCount = 0;
            if (!isMissingZound && browserSettings.showRouting) buttonCount++;
            if (!isMissingZound && browserSettings.showDuplicate) buttonCount++;
            if (browserSettings.showRemove) buttonCount++;

            if (buttonCount == 0) return;

            float gap = BaseZoundTab<TZound>.ZoundItem_spacing;
            float buttonWidth = (rect.width - gap * (buttonCount - 1)) / buttonCount;
            float currentX = rect.x;
            int buttonIndex = 0;

            ZUICornerMask MaskFor(int idx) =>
                buttonCount == 1 ? ZUICornerMask.All :
                idx == 0              ? ZUICornerMask.Left :
                idx == buttonCount - 1 ? ZUICornerMask.Right :
                ZUICornerMask.None;

            if (!isMissingZound) {
                if (browserSettings.showRouting) {
                    if (ZUI.Button(new Rect(currentX, rect.y, buttonWidth, rect.height), zoundToInspect.editor_hasManuallySetRouting ? icon_routingOn : icon_routingOff, ZUI.Style.ZoundBtnFlat, MaskFor(buttonIndex))) {
                        OpenManualRoutingDropdown(zoundToInspect);
                    }
                    currentX += buttonWidth + gap; buttonIndex++;
                }
                if (browserSettings.showDuplicate) {
                    if (ZUI.Button(new Rect(currentX, rect.y, buttonWidth, rect.height), icon_duplicate, ZUI.Style.ZoundBtnFlat, MaskFor(buttonIndex))) {
                        parentTab.zoundToDuplicate = zoundToInspect;
                    }
                    currentX += buttonWidth + gap; buttonIndex++;
                }
            }
            if (browserSettings.showRemove) {
                if (ZUI.Button(new Rect(currentX, rect.y, buttonWidth, rect.height), icon_remove, ZUI.Style.Danger, MaskFor(buttonIndex))) {
                    if (isMissingZound) {
                        RemoveMissingZound(zoundToInspect);
                    }
                    else {
                        if (AudioAssetUtility.DisplayZoundRemoveDialog(zoundToInspect)) {
                            parentTab.zoundToRemove = zoundToInspect;
                        }
                    }
                }
            }
            GUI.enabled = guiEnabled;
        }

        private static void RemoveMissingZound(Zound zoundToInspect) {
            string keyToDelete = null;
            foreach (var kvp in ZoundEngine.MissingZounds) {
                if (kvp.Value == zoundToInspect) {
                    keyToDelete = kvp.Key;
                    break;
                }
            }
            if (keyToDelete != null) {
                ZoundEngine.RemovePersistedMissingZound(keyToDelete);
            }
        }

        private void DrawNameField(Rect rect, Zound zoundToInspect) {
            bool guiEnabled = GUI.enabled;
            EditorGUI.BeginChangeCheck();
            string controlName = "rename-" + zoundToInspect.id;
            GUI.SetNextControlName(controlName);
            string newName = EditorGUI.DelayedTextField(rect, GUIContent.none, zoundToInspect.name);
            if (EditorGUI.EndChangeCheck()) {
                newName = ZoundDictionary.EnsureUniqueZoundName(newName, zoundToInspect);
                ZoundsWindow.ModifyZoundsProject("rename zound", () => {
                    zoundToInspect.name = newName;
                    //if (Application.isPlaying) {
                        if (ZoundEngine.IsInitialized()) {
                            ZoundDictionary.ValidateZoundRuntime(zoundToInspect);
                        }
                    //}
                    var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                    if (zoundToInspect is Klip klip && zoundLibrary.klips.Contains(klip)) zoundLibrary.klips = zoundLibrary.klips.OrderBy(it => it.name).ToList();
                    else if (zoundToInspect is Zequence zequence && zoundLibrary.zequences.Contains(zequence)) zoundLibrary.zequences = zoundLibrary.zequences.OrderBy(it => it.name).ToList();
                });
                if (parentTab != null) parentTab.filterCache = null;

                ZoundsWindow.setFocusNextFrame = controlName;
            }
            GUI.enabled = guiEnabled;
            //nameHasDrawn = true;
        }

        public static float RoundTo3DecimalPlaces(float original) {
            return Mathf.Round(original * 1000f) / 1000f;
        }

        private void DrawVolumeField(Rect rect, Zound zoundToInspect) {
            EditorFieldsUtility.DrawMinMaxSlider(
                rect, label_volume,
                zoundToInspect.minVolume,
                newMin => ZoundsWindow.ModifyZoundsProject("change zound volume", () => zoundToInspect.minVolume = RoundTo3DecimalPlaces(newMin)),
                zoundToInspect.maxVolume,
                newMax => ZoundsWindow.ModifyZoundsProject("change zound volume", () => zoundToInspect.maxVolume = RoundTo3DecimalPlaces(newMax)),
                Zound.MinVolumeRange, Zound.MaxVolumeRange);
            volumeHasDrawn = true;
        }

        private void DrawPitchField(Rect rect, Zound zoundToInspect) {
            EditorFieldsUtility.DrawMinMaxSlider(
                rect, label_pitch,
                zoundToInspect.minPitch,
                newMin => ZoundsWindow.ModifyZoundsProject("change zound pitch", () => zoundToInspect.minPitch = RoundTo3DecimalPlaces(newMin)),
                zoundToInspect.maxPitch,
                newMax => ZoundsWindow.ModifyZoundsProject("change zound pitch", () => zoundToInspect.maxPitch = RoundTo3DecimalPlaces(newMax)),
                Zound.MinPitchRange, Zound.MaxPitchRange);
            pitchHasDrawn = true;
        }

        private void DrawChanceField(Rect rect, Zound zoundToInspect) {
            // EditorGUI.Slider clips/glitches below ~80px. Below that, draw a compact float field instead.
            const float minSliderWidth = 80f;
            var fieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.fieldWidth = 40f;
            EditorGUI.BeginChangeCheck();
            float newChance;
            if (rect.width >= minSliderWidth) {
                newChance = EditorGUI.Slider(rect, label_chance, zoundToInspect.chance, Zound.MinChanceRange, Zound.MaxChanceRange);
            }
            else {
                newChance = EditorGUI.FloatField(rect, label_chance, zoundToInspect.chance);
                newChance = Mathf.Clamp(newChance, Zound.MinChanceRange, Zound.MaxChanceRange);
            }
            if (EditorGUI.EndChangeCheck()) {
                ZoundsWindow.ModifyZoundsProject("change zound chance", () => {
                    zoundToInspect.chance = RoundTo3DecimalPlaces(newChance);
                });
            }
            EditorGUIUtility.fieldWidth = fieldWidth;
            chanceHasDrawn = true;
        }

        private GUIContent tempContent = new GUIContent();
        private void DrawTagsField(Rect rect, Zound zoundToInspect, bool drawSimple = false) {
            string tagsString = BaseZoundTab<TZound>.GetZoundTagsString(zoundToInspect);
            if (!drawSimple && rect.width > 0) {
                lastTagsWidth = rect.width;
                tempContent.text = tagsString;
                rect.height = tagsLabelStyle.CalcHeight(tempContent, lastTagsWidth);
            }
            if (GUI.Button(rect, tagsString, tagsLabelStyle)) {
                TagsEditorWindow.OpenWindow(zoundToInspect);
            }
        }


        private void OpenManualRoutingDropdown(Zound zoundToInspect) {
#if ADDRESSABLES_INSTALLED
            List<AudioMixerGroup> allMixerGroups = new List<AudioMixerGroup>();
            RoutingTab.GetAllAddresableMixerGroups(ref allMixerGroups);
            if (allMixerGroups.Count == 0) {
                Debug.LogWarning("There is no MixerGroup found that is set as Addressable.");
            }
            else {
                var currentMixer = zoundToInspect.manuallySetMixerGroupRef != null ? zoundToInspect.manuallySetMixerGroupRef.editorAsset : null;
                var mixerGroupMenu = new GenericMenu();

                mixerGroupMenu.AddItem(new GUIContent("-None-"), currentMixer == null, () => {
                    ZoundsWindow.ModifyZoundsProject("unset manual routing", () => {
                        zoundToInspect.manuallySetMixerGroupRef = null;
                    });
                    RoutingTab.reorderableListNeedsUpdate = true;
                });
                foreach (var mixerGroup in allMixerGroups) {
                    var mg = mixerGroup;
                    bool selected = currentMixer == mixerGroup.audioMixer && mg.name == zoundToInspect.manuallySetMixerGroupRef.SubObjectName;
                    mixerGroupMenu.AddItem(new GUIContent(mixerGroup.name), selected, () => {
                        var audioMixerPath = AssetDatabase.GetAssetPath(mg.audioMixer);
                        var audioMixerGUID = AssetDatabase.GUIDFromAssetPath(audioMixerPath);
                        var mixerGroupRef = new UnityEngine.AddressableAssets.AssetReference(audioMixerGUID.ToString());
                        mixerGroupRef.SubObjectName = mg.name;
                        mixerGroupRef.SetEditorSubObject(mg);
                        ZoundsWindow.ModifyZoundsProject("set manual routing", () => {
                            zoundToInspect.manuallySetMixerGroupRef = mixerGroupRef;
                        });
                        RoutingTab.reorderableListNeedsUpdate = true;
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