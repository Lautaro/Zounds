using UnityEditor;
using UnityEngine;

namespace Zounds {

    public static class EditorFieldsUtility {

        private static GUIContent tempGUIContent = new GUIContent();

        public static void DrawMinMaxSliderLayout(GUIContent labelContent, float currentMin, System.Action<float> minSetter, float currentMax, System.Action<float> maxSetter, float leftValue, float rightValue) {
            string label = labelContent?.text ?? "";
            float min = currentMin;
            float max = currentMax;
            ZUI.SliderRange(ref min, ref max, leftValue, rightValue, label, ZUI.SliderStyle.ZoundMinMax);
            if (!Mathf.Approximately(min, currentMin)) minSetter(min);
            if (!Mathf.Approximately(max, currentMax)) maxSetter(max);
        }

        public static void DrawMinMaxSlider(Rect rect, GUIContent labelContent, float currentMin, System.Action<float> minSetter, float currentMax, System.Action<float> maxSetter, float leftValue, float rightValue) {
            string label = labelContent?.text ?? "";
            float min = currentMin;
            float max = currentMax;
            ZUI.SliderRange(rect, ref min, ref max, leftValue, rightValue, label, ZUI.SliderStyle.ZoundMinMax);
            if (!Mathf.Approximately(min, currentMin)) minSetter(min);
            if (!Mathf.Approximately(max, currentMax)) maxSetter(max);
        }

    }

}
