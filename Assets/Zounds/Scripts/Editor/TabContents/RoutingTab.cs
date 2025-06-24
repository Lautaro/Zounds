using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Audio;
using static Zounds.TagBrowserTab;

namespace Zounds {

    public class RoutingTab : TabContent {

        public override string name => "Routing";

        private Vector2 scrollPos;
        private ReorderableList ruleList;
        private List<Vector2> ruleSetScrollPoses = new List<Vector2>();
        private string ruleSearchText;

        private GUIContent tempContent = new GUIContent();

        public override void OnTabOpened() {

        }

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            if (ruleList == null || ruleList.serializedProperty.serializedObject != serializedObject) {
                var ruleSetsProp = serializedObject.FindProperty("zoundRoutings.rules");
                ruleList = new ReorderableList(serializedObject, ruleSetsProp, true, true, true, true);
                ruleList.drawHeaderCallback = OnDrawRulesHeader;
                ruleList.elementHeight = 81f;
                ruleList.drawElementCallback = OnDrawRulesElement;
                ruleList.drawElementBackgroundCallback = OnDrawRulesBackground;
                ruleList.drawNoneElementCallback = OnDrawRulesNoneElement;
                ruleList.onAddCallback = OnAddRule;
            }

            contentRect.x += 10f;
            contentRect.y += 30f;
            contentRect.width -= 20f;
            contentRect.height -= 40f;
            GUILayout.BeginArea(contentRect);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            ruleList.DoLayoutList();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void OnAddRule(ReorderableList list) {
            int lastIndex = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            var lastElement = list.serializedProperty.GetArrayElementAtIndex(lastIndex);
            lastElement.FindPropertyRelative("conditions").arraySize = 0;
            lastElement.FindPropertyRelative("mixerGroup").objectReferenceValue = null;
        }

        private void OnDrawRulesNoneElement(Rect rect) {
            EditorGUI.LabelField(rect, "Click + to add a set of rules.");
        }

        private void OnDrawRulesBackground(Rect rect, int index, bool isActive, bool isFocused) {
            bool even = index % 2 == 0;
            var guiColor = GUI.color;
            if (isActive) {
                GUI.color = even ? new Color32(70, 96, 124, 255) : new Color32(80, 106, 134, 255);
            }
            else {
                GUI.color = even ? new Color32(65, 65, 65, 255) : new Color32(75, 75, 75, 255);
            }
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = guiColor;
        }

        private void OnDrawRulesHeader(Rect rect) {
            EditorGUI.LabelField(rect, "Rules");
        }

        private void OnDrawRulesElement(Rect rect, int index, bool isActive, bool isFocused) {
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
            var element = ruleList.serializedProperty.GetArrayElementAtIndex(index);
            var conditionsProp = element.FindPropertyRelative("conditions");
            var mixerGroupProp = element.FindPropertyRelative("mixerGroup");

            var contentRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
            var leftSection = contentRect;
            leftSection.width /= 2f;

            var rightSection = leftSection;
            rightSection.x = leftSection.xMax + 4f;
            rightSection.width -= 4f;
            var mixerGroupRect = rightSection;
            mixerGroupRect.height = EditorGUIUtility.singleLineHeight;
            var addRuleRect = mixerGroupRect;
            addRuleRect.y += mixerGroupRect.height + 5f;

            float ruleElementHeight = 20f;
            float ruleElementSpacing = 2f;

            float currentY = leftSection.y;
            float currentWidth = 2f;
            var labelStyle = EditorStyles.label;

            float scrollViewWidth = 0f;
            float scrollViewHeight = 0f;

            if (conditionsProp.arraySize == 0) {
                tempContent.text = "Empty";
                var labelSize = EditorStyles.centeredGreyMiniLabel.CalcSize(tempContent);
                scrollViewWidth = labelSize.x;
                scrollViewHeight = labelSize.y;
            }

            for (int i = 0; i < conditionsProp.arraySize; i++) {
                var conditionProp = conditionsProp.GetArrayElementAtIndex(i);
                var conditionType = (ZoundRoutings.Condition.ConditionType)conditionProp.FindPropertyRelative("type").enumValueIndex;
                string elementName = conditionProp.FindPropertyRelative("name").stringValue;
                string label;
#if ZOUNDS_CONSIDER_FOLDERS
                if (conditionType == ZoundRoutings.Condition.ConditionType.Folder) {
                    string folderName = elementName.Substring(1, elementName.Length - 1);
                    if (string.IsNullOrEmpty(folderName)) folderName = "[Root]";
                    label = "Folder:" + folderName;
                }
                else
#endif
                if (conditionType == ZoundRoutings.Condition.ConditionType.Tag) {
                    if (!zoundLibrary.TryGetTag(elementName, out var tagByName) && zoundLibrary.tags.Find(t => t.name.StartsWith(elementName + ":")) == null) {
                        label = "(Missing) " + elementName;
                    }
                    else {
                        label = elementName;
                    }
                }
                else {
                    label = "(Undefined)";
                }
                tempContent.text = label;
                float labelWidth = labelStyle.CalcSize(tempContent).x + 4f + ruleElementHeight; // since the remove button is square, height = width

                if (labelWidth > scrollViewWidth) {
                    scrollViewWidth = labelWidth;
                }

                float restWidth = leftSection.width - currentWidth;
                if (currentWidth > ruleElementSpacing && labelWidth > restWidth) { // change row if labelWidth exceeds the remaining row width
                    if (currentWidth > scrollViewWidth) {
                        scrollViewWidth = currentWidth;
                    }
                    currentY += ruleElementHeight;
                    scrollViewHeight += ruleElementHeight;
                    currentWidth = ruleElementSpacing;
                }
                var ruleRect = new Rect(leftSection.x + currentWidth, currentY, labelWidth, ruleElementHeight);
                currentWidth += labelWidth + ruleElementSpacing;
            }
            scrollViewHeight += ruleElementHeight;

            for (int i = ruleSetScrollPoses.Count; i<=index; i++) {
                ruleSetScrollPoses.Add(Vector2.zero);
            }
            var scrollViewRect = leftSection;
            scrollViewRect.width = scrollViewWidth;
            scrollViewRect.height = scrollViewHeight;
            ruleSetScrollPoses[index] = GUI.BeginScrollView(leftSection, ruleSetScrollPoses[index], scrollViewRect);

            currentY = leftSection.y;
            currentWidth = 2f;

            if (conditionsProp.arraySize == 0) {
                EditorGUI.LabelField(leftSection, tempContent, EditorStyles.centeredGreyMiniLabel);
            }

            for (int i = 0; i < conditionsProp.arraySize; i++) {
                var ruleProp = conditionsProp.GetArrayElementAtIndex(i);
                var ruleType = (ZoundRoutings.Condition.ConditionType)ruleProp.FindPropertyRelative("type").enumValueIndex;
                string elementName = ruleProp.FindPropertyRelative("name").stringValue;
                string label;
                bool isError = false;
#if ZOUNDS_CONSIDER_FOLDERS
                if (ruleType == ZoundRoutings.Rule.RuleType.Folder) {
                    label = "Folder:" + elementName.Substring(1, elementName.Length-1);
                }
                else 
#endif
                if (ruleType == ZoundRoutings.Condition.ConditionType.Tag) {
                    if (!zoundLibrary.TryGetTag(elementName, out var tagByName) && zoundLibrary.tags.Find(t => t.name.StartsWith(elementName + ":")) == null) {
                        isError = true;
                        label = "(Missing) " + elementName;
                    }
                    else {
                        label = elementName;
                    }
                }
                else {
                    isError = true;
                    label = "(Undefined)";
                }
                tempContent.text = label;
                float labelWidth = labelStyle.CalcSize(tempContent).x + 4f + ruleElementHeight; // since the remove button is square, height = width

                float restWidth = leftSection.width - currentWidth;
                if (currentWidth > ruleElementSpacing && labelWidth > restWidth) { // change row if labelWidth exceeds the remaining row width
                    currentY += ruleElementHeight;
                    currentWidth = ruleElementSpacing;
                }
                var ruleRect = new Rect(leftSection.x + currentWidth, currentY, labelWidth, ruleElementHeight);
                currentWidth += labelWidth + ruleElementSpacing;

                EditorGUI.HelpBox(ruleRect, GUIContent.none);
                var ruleContentRect = new Rect(ruleRect.x + 2f, ruleRect.y + 2f, ruleRect.width - 4f, ruleRect.height - 4f);
                var ruleLabelRect = ruleContentRect;
                ruleLabelRect.width -= ruleElementHeight;
                var removeButtonRect = ruleContentRect;
                removeButtonRect.x = removeButtonRect.xMax - ruleElementHeight;
                removeButtonRect.width = ruleElementHeight;

                var guiColor = GUI.color;
                if (isError) GUI.color = new Color(1f, 0.7f, 0.7f, 1f);
                EditorGUI.LabelField(ruleLabelRect, label);
                GUI.color = guiColor;

                if (GUI.Button(removeButtonRect, "X")) {
                    conditionsProp.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }

            GUI.EndScrollView();

            EditorGUI.ObjectField(mixerGroupRect, mixerGroupProp, GUIContent.none);
            if (GUI.Button(addRuleRect, "+ Condition")) {
                var addMenu = new GenericMenu();
#if ZOUNDS_CONSIDER_FOLDERS
                var folders = ZoundsFilter.GetFolders();
                foreach (var folder in folders) {
                    string displayName = folder.Substring(1, folder.Length-1).Replace('/', '\\');
                    if (string.IsNullOrEmpty(displayName)) displayName = "[Root]";
                    string f = folder;
                    addMenu.AddItem(new GUIContent("Folder/" + displayName), false, () => {
                        AddRule(index, ZoundRoutings.Rule.RuleType.Folder, f);
                    });
                }
#endif
                var tags = zoundLibrary.tags;
                var addedKeyTags = new HashSet<string>();
                foreach (var tag in tags) {
                    var t = tag;
                    var nameSplit = tag.name.Split(':');
                    if (nameSplit.Length > 1) {
                        string keyTag = nameSplit[0];
                        if (!addedKeyTags.Contains(keyTag)) {
                            addedKeyTags.Add(keyTag);
                            addMenu.AddItem(new GUIContent("Tag/" + keyTag), false, () => {
                                AddRule(index, ZoundRoutings.Condition.ConditionType.Tag, keyTag);
                            });
                        }
                    }
                    addMenu.AddItem(new GUIContent("Tag/" + tag.name), false, () => {
                        AddRule(index, ZoundRoutings.Condition.ConditionType.Tag, t.name);
                    });
                }

                GenericMenuPopup.Show(
                    addMenu,
                    "Select Condition",
                    Event.current.mousePosition,
                    new List<string>(),
                    ruleSearchText,
                    searchText => ruleSearchText = searchText,
                    null, 1
                    );
            }
        }

        private static void AddRule(int setIndex, ZoundRoutings.Condition.ConditionType type, string name) {
            var zoundsProject = ZoundsProject.Instance;
            var rules = zoundsProject.zoundRoutings.rules[setIndex].conditions;

            var existingRule = rules.Find(r => r.type == type && r.name == name);
            if (existingRule != null) {
                return;
            }

            Undo.RecordObject(zoundsProject, "add rule");
            var newRule = new ZoundRoutings.Condition();
            newRule.type = type;
            newRule.name = name;
            rules.Add(newRule);
            EditorUtility.SetDirty(zoundsProject);
        }

    }

}
