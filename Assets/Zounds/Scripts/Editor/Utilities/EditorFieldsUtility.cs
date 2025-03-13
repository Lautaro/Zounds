using UnityEditor;
using UnityEngine;

namespace Zounds {

    public static class EditorFieldsUtility {

        private static GUIContent tempGUIContent = new GUIContent();

        public static void DrawMinMaxSliderLayout(GUIContent labelContent, float currentMin, System.Action<float> minSetter, float currentMax, System.Action<float> maxSetter, float leftValue, float rightValue) {
            float min = currentMin;
            float max = currentMax;
            GUILayout.BeginHorizontal();
            {
                if (labelContent != null) {
                    tempGUIContent.tooltip = labelContent.tooltip;
                    EditorGUILayout.LabelField(labelContent, GUILayout.Width(EditorGUIUtility.labelWidth));
                }
                EditorGUI.BeginChangeCheck();
                var newMin = EditorGUILayout.DelayedFloatField(tempGUIContent, currentMin, GUILayout.Width(50));
                if (EditorGUI.EndChangeCheck()) {
                    if (newMin > max) newMin = max;
                    minSetter(newMin);
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.MinMaxSlider(tempGUIContent, ref min, ref max, leftValue, rightValue);
                if (EditorGUI.EndChangeCheck()) {
                    minSetter(min);
                    maxSetter(max);
                }

                EditorGUI.BeginChangeCheck();
                var newMax = EditorGUILayout.DelayedFloatField(tempGUIContent,currentMax, GUILayout.Width(50));
                if (EditorGUI.EndChangeCheck()) {
                    if (newMax < min) newMax = min;
                    maxSetter(newMax);
                }
            }
            GUILayout.EndHorizontal();
        }

        public static void DrawMinMaxSlider(Rect rect, GUIContent labelContent, float currentMin, System.Action<float> minSetter, float currentMax, System.Action<float> maxSetter, float leftValue, float rightValue) {
            float min = currentMin;
            float max = currentMax;

            float labelWidth = EditorGUIUtility.labelWidth;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float boxWidth = 53f;

            Rect labelRect = new Rect(rect.x, rect.y, labelContent==null? 0 : labelWidth, lineHeight);
            Rect sliderRect = new Rect(labelRect.xMax + 2f, rect.y, rect.width - labelWidth - 2f - boxWidth*2f, lineHeight);
            Rect minRect = new Rect(sliderRect.xMax + 3f, rect.y, boxWidth - 2.5f, lineHeight);
            Rect maxRect = new Rect(minRect.xMax + 2f, rect.y, boxWidth - 2.5f, lineHeight);

            if (labelContent != null) {
                tempGUIContent.tooltip = labelContent.tooltip;
                EditorGUI.LabelField(labelRect, labelContent);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderRect, tempGUIContent, ref min, ref max, leftValue, rightValue);
            if (EditorGUI.EndChangeCheck()) {
                minSetter(min);
                maxSetter(max);
            }

            EditorGUI.BeginChangeCheck();
            var newMin = EditorGUI.DelayedFloatField(minRect, tempGUIContent, currentMin);
            if (EditorGUI.EndChangeCheck()) {
                if (newMin > max) newMin = max;
                minSetter(newMin);
            }

            EditorGUI.BeginChangeCheck();
            var newMax = EditorGUI.DelayedFloatField(maxRect, tempGUIContent, currentMax);
            if (EditorGUI.EndChangeCheck()) {
                if (newMax < min) newMax = min;
                maxSetter(newMax);
            }

        }

    }

}
