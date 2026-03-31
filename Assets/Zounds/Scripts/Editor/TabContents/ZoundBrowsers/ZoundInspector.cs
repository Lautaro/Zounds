using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    /// <summary>
    /// Draws the detail fields and action buttons for a single zound.
    /// Used by BrowserTab in both singlecolumn and multicolumn layouts.
    ///
    /// Three public draw methods exist depending on context:
    ///   DrawMulticolumn   — full inspector panel that expands below a multicolumn row.
    ///                       Lays out 5 columns: [Edit+MS] [Fields A] [Fields B] [Tags] [Route/Conv/Dup/Del]
    ///   DrawSinglecolumn  — draws into pre-computed rects passed by DrawSinglecolumnRow.
    ///                       Edit and M/S rects come from the left; fields fill the middle;
    ///                       Route/Conv/Dup/Del sit in the right-group rect.
    ///   DrawSimple        — a compact read-only strip used inside Zequence editor sub-rows.
    /// </summary>
    public class ZoundBrowserEditor<TZound> where TZound : Zound {

        private BrowserTab parentTab;

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

        public ZoundBrowserEditor(BrowserTab parentTab) {
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
        // Height is animated via BrowserTab.inspectorAnimFloat (smooth open/close).
        // ──────────────────────────────────────────────────────────────────────────
        public void DrawMulticolumn(Zound zoundToInspect, float inspectorHeight) {
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect.IsClipOrLocalZound());

            var browserSettings = ZoundsProject.Instance.browserSettings;
            int fieldCount = 0;
            if (browserSettings.showNameField) fieldCount++;
            if (browserSettings.showVolume) fieldCount++;
            if (browserSettings.showPitch) fieldCount++;
            if (browserSettings.showChance) fieldCount++;

            GUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(inspectorHeight), GUILayout.ExpandWidth(true));
            {
                var inspectorRect = GUILayoutUtility.GetRect(1, inspectorHeight, GUILayout.ExpandWidth(true));

                float fieldWidthMultiplier;
                float tagsWidthMultiplier;

                if (browserSettings.showTags) {
                    if (fieldCount > 2) { fieldWidthMultiplier = 0.4f; tagsWidthMultiplier = 0.2f; }
                    else if (fieldCount > 0) { fieldWidthMultiplier = 0.5f; tagsWidthMultiplier = 0.5f; }
                    else { fieldWidthMultiplier = 0f; tagsWidthMultiplier = 1f; }
                }
                else {
                    fieldWidthMultiplier = fieldCount > 2 ? 0.5f : (fieldCount > 0 ? 1f : 0f);
                    tagsWidthMultiplier = 0f;
                }

                float buttonWidth = 30f;
                float leftButtonsWidth = (browserSettings.showOpenEditor ? buttonWidth : 0f);
                if (browserSettings.showMute || browserSettings.showSolo) leftButtonsWidth += 24f;

                float baseRemoveRectWidth = 0f;
                if (browserSettings.showRouting)   baseRemoveRectWidth += buttonWidth;
                if (browserSettings.showDuplicate) baseRemoveRectWidth += buttonWidth;
                if (browserSettings.showRemove)    baseRemoveRectWidth += buttonWidth;

                float mcLeftGap  = leftButtonsWidth > 0 ? BrowserTab.LEFT_BUTTONS_TO_NAME_GAP : 0f;
                float mcRightGap = baseRemoveRectWidth > 0 ? BrowserTab.INSPECTOR_TO_REMOVE_GAP : 0f;

                // Button zones (edit, M/S, remove) and fields zone use base inspector height —
                // they should not grow with tags. Tags zone uses full inspectorHeight.
                // fieldsRect is top-aligned at inspectorRect.y with base height so that
                // bottom-anchoring in DrawZoundFields places rows just below the button row.
                float baseHeight = Mathf.Min(inspectorHeight, BrowserTab.inspectorHeight);
                float x = inspectorRect.x;
                Rect editRect    = new Rect(x, inspectorRect.y, browserSettings.showOpenEditor ? buttonWidth : 0f, baseHeight);
                x += editRect.width;
                Rect msRect      = new Rect(x, inspectorRect.y, (browserSettings.showMute || browserSettings.showSolo) ? 24f : 0f, baseHeight);
                x += msRect.width + mcLeftGap;
                float remainingWidth = inspectorRect.xMax - x - baseRemoveRectWidth - mcRightGap;
                Rect fieldsRect  = new Rect(x, inspectorRect.y, remainingWidth * (1f - tagsWidthMultiplier), baseHeight);
                Rect tagsRect    = new Rect(fieldsRect.xMax, inspectorRect.y, remainingWidth * tagsWidthMultiplier, inspectorHeight);
                Rect removeRect  = new Rect(tagsRect.xMax + mcRightGap, inspectorRect.y, baseRemoveRectWidth, baseHeight);

                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 12f;

                if (s_debugBorders) {
                    DebugRect(inspectorRect, Color.white, $"panel h={inspectorRect.height:0} bh={baseHeight:0}");
                }

                DrawZoundFields(editRect, msRect, fieldsRect, tagsRect, removeRect, zoundToInspect, fillButtonHeight: true, twoRowFields: true);

                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            GUILayout.EndHorizontal();

            GUI.enabled = guiEnabled;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // SINGLECOLUMN INSPECTOR
        // All rects are pre-computed by DrawSinglecolumnRow in BrowserTab.
        //   editButtonRect   — left-most, the open-editor / convert-clip-to-klip button.
        //   muteSoloRect     — M and S buttons, stacked vertically when multipleRows is true.
        //   removeButtonRect — right-most group: Route / Conv / Dup / Del.
        //   fieldsRect       — the middle area shared by Vol / Pitch / Chance / Tags fields.
        // ──────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Draws the singlecolumn row contents into the provided pre-computed rects.
        /// When <paramref name="tagsRect"/> is non-zero, tags are drawn there instead
        /// of inside <paramref name="fieldsRect"/> — used when tags have their own zone
        /// (separate-zone mode: list two-row layout, or flexible tags column).
        /// </summary>
        /// <param name="multipleRows">True when the list row is in two-row (narrow) mode — edit and M/S
        /// buttons intentionally span both rows and should not be clamped to single-line height.</param>
        public void DrawZoundSinglecolumn(Rect editButtonRect, Rect muteSoloRect, Rect removeButtonRect, Rect fieldsRect, Zound zoundToInspect, Rect tagsRect = default, bool multipleRows = false) {
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect.IsClipOrLocalZound());

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 12f;

            DrawZoundFields(editButtonRect, muteSoloRect, fieldsRect, tagsRect, removeButtonRect, zoundToInspect, fillButtonHeight: multipleRows);

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUI.enabled = guiEnabled;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // SHARED FIELD DRAWING
        // Single method called by both DrawMulticolumn and DrawZoundSinglecolumn.
        // All rects are absolute screen coordinates. Tags rect may be Rect.zero (hidden).
        // Controls are vertically centred to natural height so they stay compact when
        // the row is taller than ROW_HEIGHT (e.g. tags wrapping into multiple lines).
        // Tags are exempt — they fill whatever height they are given.
        //
        // fillButtonHeight: when true, edit/M/S/remove buttons fill the full zone height
        //                   (grid inspector panel, or list two-row mode where buttons span both rows).
        //                   When false, buttons are clamped to singleLineHeight and vertically centred
        //                   (list single-row mode, where the row may be tall from tag wrapping).
        // twoRowFields:     when true, fields are distributed across two rows within fieldsRect.
        //                   Used by the multicolumn inspector which has limited horizontal space.
        // ──────────────────────────────────────────────────────────────────────────
        // DEBUG: set to true to draw colored borders around every zone rect in DrawZoundFields.
        private static bool s_debugBorders = false;
        private static void DebugRect(Rect r, Color c, string label = "") {
            if (Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x,           r.y,            r.width, 1f), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,           r.yMax - 1f,    r.width, 1f), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,           r.y,            1f, r.height), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f,   r.y,            1f, r.height), EditorGUIUtility.whiteTexture);
            GUI.color = prev;
            if (!string.IsNullOrEmpty(label)) {
                var s = new GUIStyle(EditorStyles.label);
                s.normal.textColor = c;
                s.fontSize = 8;
                GUI.Label(new Rect(r.x + 2f, r.y, r.width, 12f), label, s);
            }
        }

        private void DrawZoundFields(Rect editRect, Rect msRect, Rect fieldsRect, Rect tagsRect, Rect removeRect, Zound zoundToInspect, bool fillButtonHeight = false, bool twoRowFields = false) {
            ResetState();
            var browserSettings = ZoundsProject.Instance.browserSettings;
            bool isMissingZound = !(zoundToInspect is ClipZound) && zoundToInspect.id == 0;
            var guiEnabled = GUI.enabled;

            if (s_debugBorders) {
                DebugRect(editRect,   Color.cyan,    "edit");
                DebugRect(msRect,     Color.green,   "ms");
                DebugRect(fieldsRect, Color.yellow,  "fields");
                DebugRect(tagsRect,   Color.magenta, "tags");
                DebugRect(removeRect, Color.red,     "remove");
            }

            // fillButtonHeight: buttons span their full zone (grid inspector, list two-row mode).
            // Otherwise clamp to singleLineHeight so buttons stay compact when the row grows tall
            // from tag wrapping in single-row list mode.
            Rect drawEditRect   = fillButtonHeight ? editRect   : NaturalHeight(editRect);
            Rect drawMsRect     = fillButtonHeight ? msRect     : NaturalHeight(msRect);
            Rect drawRemoveRect = fillButtonHeight ? removeRect : NaturalHeight(removeRect);

            if (browserSettings.showOpenEditor && editRect.width > 0f) {
                DrawOpenEditorButton(drawEditRect, zoundToInspect, isMissingZound);
            }

            if (!isMissingZound && (browserSettings.showMute || browserSettings.showSolo) && msRect.width > 0f) {
                DrawMuteSoloButtonsVertical(drawMsRect, zoundToInspect);
            }

            if ((browserSettings.showRouting || browserSettings.showDuplicate || browserSettings.showRemove) && removeRect.width > 0f) {
                DrawRemoveButton(drawRemoveRect, zoundToInspect, isMissingZound);
            }

            if (!isMissingZound && fieldsRect.width > 1f) {
                bool tagsInSeparateZone = tagsRect.width > 1f;

                int fieldCount = 0;
                if (browserSettings.showNameField) fieldCount++;
                if (browserSettings.showVolume)    fieldCount++;
                if (browserSettings.showPitch)     fieldCount++;
                if (browserSettings.showChance)    fieldCount++;
                if (browserSettings.showTags && !tagsInSeparateZone) fieldCount++;

                if (fieldCount > 0) {
                    float lh = EditorGUIUtility.singleLineHeight;
                    float rowGap = 2f;

                    if (twoRowFields) {
                        // Split fields into two rows: ceil(n/2) on row 0, floor(n/2) on row 1.
                        // Both rows are bottom-anchored so they align with the last tag line when
                        // the inspector panel is taller than the base height.
                        int row0Count = Mathf.CeilToInt(fieldCount / 2f);
                        int row1Count = fieldCount - row0Count;
                        float fw0 = row0Count > 0 ? (fieldsRect.width - 4f) / row0Count : 0f;
                        float fw1 = row1Count > 0 ? (fieldsRect.width - 4f) / row1Count : 0f;
                        float bottomY = fieldsRect.yMax - lh;
                        float row1Y   = row1Count > 0 ? bottomY : fieldsRect.y;
                        float row0Y   = row1Count > 0 ? row1Y - lh - rowGap : bottomY;
                        Rect r0 = new Rect(fieldsRect.x, row0Y, fw0 - 4f, lh);
                        Rect r1 = new Rect(fieldsRect.x, row1Y, fw1 - 4f, lh);

                        // Row 0
                        if (browserSettings.showNameField) { DrawNameField(r0, zoundToInspect); r0.x += fw0; }
                        else if (browserSettings.showVolume) { DrawVolumeField(r0, zoundToInspect); r0.x += fw0; }
                        else if (browserSettings.showPitch)  { DrawPitchField(r0, zoundToInspect); r0.x += fw0; }
                        else if (browserSettings.showChance) { DrawChanceField(r0, zoundToInspect); r0.x += fw0; }

                        if (row0Count > 1) {
                            if (browserSettings.showVolume && !volumeHasDrawn)  { DrawVolumeField(r0, zoundToInspect); r0.x += fw0; }
                            else if (browserSettings.showPitch && !pitchHasDrawn)  { DrawPitchField(r0, zoundToInspect); r0.x += fw0; }
                            else if (browserSettings.showChance && !chanceHasDrawn) { DrawChanceField(r0, zoundToInspect); r0.x += fw0; }
                        }

                        // Row 1
                        if (row1Count > 0) {
                            if (browserSettings.showVolume && !volumeHasDrawn)   { DrawVolumeField(r1, zoundToInspect); r1.x += fw1; }
                            else if (browserSettings.showPitch && !pitchHasDrawn)  { DrawPitchField(r1, zoundToInspect); r1.x += fw1; }
                            else if (browserSettings.showChance && !chanceHasDrawn) { DrawChanceField(r1, zoundToInspect); r1.x += fw1; }

                            if (row1Count > 1) {
                                if (browserSettings.showChance && !chanceHasDrawn) { DrawChanceField(r1, zoundToInspect); r1.x += fw1; }
                            }
                        }

                        // Tags on row 1 if inline
                        if (browserSettings.showTags && !tagsInSeparateZone) DrawTagsField(r1, zoundToInspect);
                    }
                    else {
                        // Single horizontal row of fields.
                        float fieldWidth = (fieldsRect.width - 4f) / fieldCount;
                        Rect fieldRect   = NaturalHeight(fieldsRect);
                        fieldRect.width  = fieldWidth - 4f;

                        if (browserSettings.showNameField) { DrawNameField(fieldRect, zoundToInspect); fieldRect.x += fieldWidth; }
                        if (browserSettings.showVolume)    { DrawVolumeField(fieldRect, zoundToInspect); fieldRect.x += fieldWidth; }
                        if (browserSettings.showPitch)     { DrawPitchField(fieldRect, zoundToInspect); fieldRect.x += fieldWidth; }
                        if (browserSettings.showChance)    { DrawChanceField(fieldRect, zoundToInspect); fieldRect.x += fieldWidth; }
                        if (browserSettings.showTags && !tagsInSeparateZone) { DrawTagsField(fieldRect, zoundToInspect); fieldRect.x += fieldWidth; }
                    }
                }

                if (browserSettings.showTags && tagsInSeparateZone) DrawTagsField(tagsRect, zoundToInspect);
            }

            GUI.enabled = guiEnabled;
        }

        // Returns a copy of rect with height clamped to singleLineHeight, top-aligned.
        // Used so buttons and fields stay compact when the row is taller than normal (tag wrapping).
        private static Rect NaturalHeight(Rect rect) {
            float h = EditorGUIUtility.singleLineHeight;
            if (rect.height <= h) return rect;
            return new Rect(rect.x, rect.y, rect.width, h);
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
                    BrowserTab.OpenAddNewZoundMenu(zoundToInspect.name);
                }
            }
            else if (zoundToInspect is ClipZound clipZound) {
                if (ZUI.Button(rect, icon_convert, ZUI.Style.ZoundBtnFlat)) {
                    if (EditorUtility.DisplayDialog("Convert to Klip: " + clipZound.name, "Convert this into audio clip a Klip?\n" + clipZound.name, "Convert", "Cancel")) {
                        if (parentTab is BrowserTab browserTab) {
                            browserTab.ConvertClipToKlip(clipZound);
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

            float msGap = BrowserTab.MUTE_SOLO_GAP;
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

            float gap = BrowserTab.ZoundItem_spacing;
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
            string tagsString = BrowserTab.GetZoundTagsString(zoundToInspect);
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