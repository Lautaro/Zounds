using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

#if ADDRESSABLES_INSTALLED
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
#endif

namespace Zounds {

    /// <summary>
    /// Inspector for multicolumn zound
    /// </summary>
    /// <typeparam name="TZound"></typeparam>
    public class ZoundInspector<TZound> where TZound : Zound {

        private BaseZoundTab<TZound> parentTab;
        private Rect[] inspectorColumns = new Rect[5];

        private GUIContent label_volume = new GUIContent("V", "Volume");
        private GUIContent label_pitch = new GUIContent("P", "Pitch");
        private GUIContent label_chance = new GUIContent("C", "Chance");
        private GUIContent icon_openEditor;
        private GUIContent icon_routingOn;
        private GUIContent icon_routingOff;
        private GUIContent icon_convert;
        private GUIContent icon_remove;
        private GUIContent icon_duplicate;
        private GUIStyle tagsLabelStyle;

        //private bool nameHasDrawn; // Not needed since this will be drawn first anyway.
        private bool volumeHasDrawn;
        private bool pitchHasDrawn;
        private bool chanceHasDrawn;

        private const float RoutingButtonWidth = 16f;
        private string routingSearchTerm = "";

        public ZoundInspector(BaseZoundTab<TZound> parentTab) {
            this.parentTab = parentTab;
            icon_openEditor = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/open-editor"), "Open editor.");
            icon_routingOn = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/routing-on"), "Set manual routing.");
            icon_routingOff = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/routing-off"), "Set manual routing.");
            icon_convert = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/convert"), "Convert to Klip.");
            icon_remove = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/remove"), "Remove this zound.");
            icon_duplicate = new GUIContent(Resources.Load<Texture>("ZoundsWindowIcons/duplicate"), "Duplicate this zound.");
            tagsLabelStyle = new GUIStyle();
            tagsLabelStyle.normal.textColor = new Color32(163, 198, 255, 255);
            tagsLabelStyle.wordWrap = true;
            tagsLabelStyle.clipping = TextClipping.Clip;
        }

        public void DrawMulticolumn(Zound zoundToInspect, float inspectorHeight) {
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect is ClipZound);

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
                float removeRectWidth = buttonWidth * 2f;

                inspectorColumns[0] = new Rect(inspectorRect.x, inspectorRect.y, buttonWidth + 4f, inspectorRect.height);
                inspectorRect.x += buttonWidth + 4f;
                inspectorRect.width -= (buttonWidth + 4f + removeRectWidth + 4f);
                inspectorColumns[1] = new Rect(inspectorRect.x, inspectorRect.y, inspectorRect.width * fieldWidthMultiplier, inspectorRect.height);
                inspectorColumns[2] = new Rect(inspectorColumns[1].xMax, inspectorColumns[1].y, fieldCount > 2 ? inspectorColumns[1].width : 0f, inspectorRect.height);
                inspectorColumns[3] = new Rect(inspectorColumns[2].xMax, inspectorColumns[2].y, inspectorRect.width * tagsWidthMultiplier, inspectorRect.height);
                inspectorColumns[4] = new Rect(inspectorColumns[3].xMax + 4f, inspectorColumns[3].y, removeRectWidth + 4f, inspectorRect.height);

                float lineHeight = EditorGUIUtility.singleLineHeight;

                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 12f;

                GUI.BeginClip(inspectorColumns[0]);
                {
                    DrawOpenEditorButton(new Rect(0, 0, 30f, inspectorColumns[0].height), zoundToInspect);
                }
                GUI.EndClip();

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

                GUI.BeginClip(inspectorColumns[3]);
                {
                    float tagsFieldWidth = inspectorColumns[3].width - 4f - RoutingButtonWidth - 2f;
                    DrawTagsField(new Rect(4f, 0, tagsFieldWidth, inspectorColumns[3].height), zoundToInspect);

                    var routingRect = new Rect(4f + tagsFieldWidth + 2f, 0, RoutingButtonWidth, RoutingButtonWidth);
                    routingRect.width = RoutingButtonWidth;
                    routingRect.height = RoutingButtonWidth;
                    if (GUI.Button(routingRect, zoundToInspect.editor_hasManuallySetRouting ? icon_routingOn : icon_routingOff, EditorStyles.label)) {
                        OpenManualRoutingDropdown(zoundToInspect);
                    }
                }
                GUI.EndClip();

                GUI.BeginClip(inspectorColumns[4]);
                {
                    DrawRemoveButton(new Rect(0, 0, removeRectWidth, inspectorColumns[4].height), zoundToInspect);
                }
                GUI.EndClip();

                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            GUILayout.EndHorizontal();

            GUI.enabled = guiEnabled;
        }

        public void DrawSinglecolumn(Rect editButtonRect, Rect removeButtonRect, Rect fieldsRect, Zound zoundToInspect) {
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect is ClipZound);

            ResetState();
            var browserSettings = ZoundsProject.Instance.browserSettings;
            int fieldCount = 0;
            if (browserSettings.showNameField) fieldCount++;
            if (browserSettings.showVolume) fieldCount++;
            if (browserSettings.showPitch) fieldCount++;
            if (browserSettings.showChance) fieldCount++;
            if (browserSettings.showTags) fieldCount++;
            float fieldWidth = (fieldsRect.width - RoutingButtonWidth - 4f) / fieldCount;
            Rect fieldRect = fieldsRect;
            fieldRect.width = fieldWidth - 4f;

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 12f;

            Rect openEditorRect = editButtonRect;

            DrawOpenEditorButton(editButtonRect, zoundToInspect);
            DrawRemoveButton(removeButtonRect, zoundToInspect);
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
                DrawTagsField(fieldRect, zoundToInspect);
                fieldRect.x += fieldWidth;
            }

            var routingRect = fieldRect;
            routingRect.width = RoutingButtonWidth;
            routingRect.height = RoutingButtonWidth;
            if (GUI.Button(routingRect, zoundToInspect.editor_hasManuallySetRouting ? icon_routingOn : icon_routingOff, EditorStyles.label)) {
                OpenManualRoutingDropdown(zoundToInspect);
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUI.enabled = guiEnabled;
        }

        public void DrawSimple(Rect fieldsRect, Zound zoundToInspect, bool drawName = true, bool drawTags = true) {
            if (zoundToInspect == null) return;
            var guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !(zoundToInspect is ClipZound);

            ResetState();
            var browserSettings = ZoundsProject.Instance.browserSettings;
            int fieldCount = 3;
            if (drawName) fieldCount++;
            if (drawTags) fieldCount++;
            float fieldWidth = fieldsRect.width / fieldCount;
            Rect fieldRect = fieldsRect;
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
                DrawTagsField(fieldRect, zoundToInspect);
                fieldRect.x += fieldWidth;
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUI.enabled = guiEnabled;
        }

        private void ResetState() {
            //nameHasDrawn = false;
            volumeHasDrawn = false;
            pitchHasDrawn = false;
            chanceHasDrawn = false;
        }

        private void DrawOpenEditorButton(Rect rect, Zound zoundToInspect) {
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !Application.isPlaying;
            if (zoundToInspect is ClipZound clipZound) {
                GUI.enabled = !Application.isPlaying;
                if (GUI.Button(rect, icon_convert)) {
                    if (EditorUtility.DisplayDialog("Convert to Klip: " + clipZound.name, "Convert this into audio clip a Klip?\n" + clipZound.name, "Convert", "Cancel")) {
                        if (parentTab is KlipsTab klipsTab) {
                            klipsTab.ConvertClipToKlip(clipZound);
                        }
                    }
                }
            }
            else {
                if (GUI.Button(rect, icon_openEditor)) {
                    parentTab.OpenZoundEditor(zoundToInspect);
                }
            }
            GUI.enabled = guiEnabled;
        }

        private void DrawRemoveButton(Rect rect, Zound zoundToInspect) {
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !Application.isPlaying;

            var leftRect = new Rect(rect.x, rect.y, rect.width / 2f, rect.height);
            var rightRect = new Rect(leftRect.xMax, leftRect.y, leftRect.width, leftRect.height);

            if (GUI.Button(leftRect, icon_duplicate)) {
                parentTab.zoundToDuplicate = zoundToInspect;
            }
            if (GUI.Button(rightRect, icon_remove)) {
                if (AudioAssetUtility.DisplayZoundRemoveDialog(zoundToInspect)) {
                    parentTab.zoundToRemove = zoundToInspect;
                }
            }
            GUI.enabled = guiEnabled;
        }

        private void DrawNameField(Rect rect, Zound zoundToInspect) {
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !Application.isPlaying;
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.TextField(rect, GUIContent.none, zoundToInspect.name);
            if (EditorGUI.EndChangeCheck()) {
                newName = ZoundDictionary.EnsureUniqueZoundName(newName, zoundToInspect);
                ZoundsWindow.ModifyZoundsProject("rename zound", () => {
                    zoundToInspect.name = newName;
                    //if (Application.isPlaying) {
                        if (ZoundEngine.IsInitialized()) {
                            ZoundDictionary.ValidateZoundRuntime(zoundToInspect);
                        }
                    //}
                });
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
            var fieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.fieldWidth = 40f;
            EditorGUI.BeginChangeCheck();
            float newChance = EditorGUI.Slider(rect, label_chance, zoundToInspect.chance, Zound.MinChanceRange, Zound.MaxChanceRange);
            if (EditorGUI.EndChangeCheck()) {
                ZoundsWindow.ModifyZoundsProject("change zound chance", () => {
                    zoundToInspect.chance = RoundTo3DecimalPlaces(newChance);
                });
            }
            EditorGUIUtility.fieldWidth = fieldWidth;
            chanceHasDrawn = true;
        }

        private void DrawTagsField(Rect rect, Zound zoundToInspect) {
            string tagsString = BaseZoundTab<TZound>.GetZoundTagsString(zoundToInspect);
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
                        mixerGroupRef.SetEditorSubObject(mixerGroup);
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