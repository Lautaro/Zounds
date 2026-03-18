using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Zounds {

    public class ProjectSettingsTab : TabContent {

        public override string name { get; set; } = "Project Settings";

        [SerializeField] private Vector2 scrollPos;

        private string[] availableThemes;
        private int selectedThemeIndex = -1;

        private void RefreshThemeList() {
            var themesPath = ZoundsProject.Instance.projectSettings.themesFolderPath;
            if (!Directory.Exists(themesPath)) {
                availableThemes = new string[0];
                return;
            }
            availableThemes = Directory.GetFiles(themesPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray();
        }

        #region LABELS
        private GUIContent label_playerVolume           = new GUIContent("Player Volume", "Master volume when the game is running. When switching to play mode, this value goes to the master volume.");
        private GUIContent label_systemVolumeModifier   = new GUIContent("System Volume Modifier", "Modifier for the master volume. This is just used if there is a need to modify the overall volume for the game for any reason.");
        private GUIContent label_editorVolume           = new GUIContent("Editor Volume", "Master volume when in edit mode. When switching to edit mode, this value goes to the master volume.");
        private GUIContent label_systemFolderPath       = new GUIContent("System Folder Path", "Set the path for a folder under Resources where system data is stored.");
        private GUIContent label_userFolderPath         = new GUIContent("User Folder Path", "Path within resources where the user stores its sounds.");
        private GUIContent label_sourceFolderPath       = new GUIContent("Source Folder Path", "Path outside of resources where the user stores source sounds.");
        private GUIContent label_cooldownDuration       = new GUIContent("Cooldown Duration", " A timer for a Zound that prohibits the same Zound to be played again before the timer runs out.");
        private GUIContent label_maxPlayedZoundInstances= new GUIContent("Max Played Zound Instances", "If the number of Zounds playing is more than this threshold, when a Zound in a culling group triggers, then it will play, but the one that has been playing for the longest will be culled.");
        private GUIContent label_cullFadeDuration = new GUIContent("Cull Fade Duration", "Fade duration to kill a zound when Max Played Zound Instances is reached.");
        #endregion LABELS

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            if (availableThemes == null) RefreshThemeList();

            contentRect.x += 10f;
            contentRect.y += 30f;
            contentRect.width -= 20f;
            contentRect.height -= 40f;
            GUILayout.BeginArea(contentRect);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            SerializedProperty projectSettings = serializedObject.FindProperty("projectSettings");
            SerializedProperty playerVolume         = projectSettings.FindPropertyRelative("playerVolume");
            SerializedProperty systemVolumeModifier = projectSettings.FindPropertyRelative("systemVolumeModifier");
            SerializedProperty editorVolume         = projectSettings.FindPropertyRelative("editorVolume");
            SerializedProperty systemFolderPath     = projectSettings.FindPropertyRelative("systemFolderPath");
            SerializedProperty userFolderPath       = projectSettings.FindPropertyRelative("userFolderPath");
            SerializedProperty sourceFolderPath     = projectSettings.FindPropertyRelative("sourceFolderPath");

            SerializedProperty cooldownDuration = projectSettings.FindPropertyRelative("cooldownDuration");
            SerializedProperty maxPlayedZoundInstances = projectSettings.FindPropertyRelative("maxPlayedZoundInstances");
            SerializedProperty cullFadeDuration = projectSettings.FindPropertyRelative("cullFadeDuration");

            SerializedProperty editorStyle = projectSettings.FindPropertyRelative("editorStyle");

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150f;

            EditorGUILayout.LabelField("Workspace Directories", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(systemFolderPath, label_systemFolderPath);
            EditorGUILayout.PropertyField(userFolderPath, label_userFolderPath);
            EditorGUILayout.PropertyField(sourceFolderPath, label_sourceFolderPath);
            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField("Engine", EditorStyles.boldLabel);
            EditorGUILayout.Slider(playerVolume, 0f, 1f, label_playerVolume);
            EditorGUILayout.Slider(systemVolumeModifier, 0f, 1f, label_systemVolumeModifier);
            EditorGUILayout.Slider(editorVolume, 0f, 1f, label_editorVolume);

            EditorGUIUtility.labelWidth = 170f;
            cooldownDuration.floatValue = EditorGUILayout.FloatField(label_cooldownDuration, cooldownDuration.floatValue);
            if (cooldownDuration.floatValue < 0f) cooldownDuration.floatValue = 0f;
            maxPlayedZoundInstances.intValue = EditorGUILayout.IntField(label_maxPlayedZoundInstances, maxPlayedZoundInstances.intValue);
            if (maxPlayedZoundInstances.intValue < 0f) maxPlayedZoundInstances.intValue = 1;
            EditorGUILayout.Slider(cullFadeDuration, 0f, 0.5f, label_cullFadeDuration);
            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField("Themes", EditorStyles.boldLabel);
            EditorGUIUtility.labelWidth = 150f;
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            selectedThemeIndex = EditorGUILayout.Popup("Available Themes", selectedThemeIndex, availableThemes);
            if (EditorGUI.EndChangeCheck() && selectedThemeIndex >= 0) {
                string themeName = availableThemes[selectedThemeIndex];
                string path = Path.Combine(ZoundsProject.Instance.projectSettings.themesFolderPath, themeName + ".json");
                if (File.Exists(path)) {
                    string json = File.ReadAllText(path);
                    ZoundsTheme theme = JsonUtility.FromJson<ZoundsTheme>(json);
                    if (theme != null) {
                        ZoundsWindow.ModifyZoundsProject($"apply theme {themeName}", () => {
                            ZoundsProject.Instance.projectSettings.ApplyTheme(theme);
                        });
                    }
                }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60f))) RefreshThemeList();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Save Current Style as New Theme")) {
                string path = EditorUtility.SaveFilePanelInProject("Save Theme", "NewTheme", "json", "Select theme save location", ZoundsProject.Instance.projectSettings.themesFolderPath);
                if (!string.IsNullOrEmpty(path)) {
                    string json = JsonUtility.ToJson(ZoundsProject.Instance.projectSettings.ExtractTheme(), true);
                    File.WriteAllText(path, json);
                    AssetDatabase.Refresh();
                    RefreshThemeList();
                }
            }
            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField("Editor Style (Visual)", EditorStyles.boldLabel);
            EditorGUIUtility.labelWidth = 190f;
            
            SerializedProperty playerHeadColor = editorStyle.FindPropertyRelative("playerHeadColor");
            SerializedProperty playerHeadThickness = editorStyle.FindPropertyRelative("playerHeadThickness");
            SerializedProperty klipWaveformBGColor = editorStyle.FindPropertyRelative("klipWaveformBGColor");
            SerializedProperty zequenceWaveformBGColor = editorStyle.FindPropertyRelative("zequenceWaveformBGColor");
            SerializedProperty volumeEnvelopeColor = editorStyle.FindPropertyRelative("volumeEnvelopeColor");
            SerializedProperty volumeEnvelopeThickness = editorStyle.FindPropertyRelative("volumeEnvelopeThickness");
            SerializedProperty pitchEnvelopeColor = editorStyle.FindPropertyRelative("pitchEnvelopeColor");
            SerializedProperty pitchEnvelopeThickness = editorStyle.FindPropertyRelative("pitchEnvelopeThickness");
            SerializedProperty trimHandleColor = editorStyle.FindPropertyRelative("trimHandleColor");
            SerializedProperty trimHandleThickness = editorStyle.FindPropertyRelative("trimHandleThickness");
            SerializedProperty waveformColor = editorStyle.FindPropertyRelative("waveformColor");
            SerializedProperty renderedWaveformColor = editorStyle.FindPropertyRelative("renderedWaveformColor");
            SerializedProperty renderedWaveformBGColor = editorStyle.FindPropertyRelative("renderedWaveformBGColor");
            SerializedProperty renderedPlayerHeadColor = editorStyle.FindPropertyRelative("renderedPlayerHeadColor");
            SerializedProperty trimAreaColor = editorStyle.FindPropertyRelative("trimAreaColor");
            SerializedProperty selectedEnvelopeLineColor = editorStyle.FindPropertyRelative("selectedEnvelopeLineColor");
            SerializedProperty selectedEnvelopeHandleColor = editorStyle.FindPropertyRelative("selectedEnvelopeHandleColor");
            
            SerializedProperty autoRender = editorStyle.FindPropertyRelative("autoRender");
            SerializedProperty alertOnClosing = editorStyle.FindPropertyRelative("alertOnClosing");

            void DrawThicknessColor(GUIContent label, SerializedProperty thickness, SerializedProperty color) {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));
                var prevWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 10f;
                EditorGUILayout.PropertyField(thickness, GUIContent.none, GUILayout.Width(45f));
                EditorGUILayout.PropertyField(color, GUIContent.none);
                EditorGUIUtility.labelWidth = prevWidth;
                GUILayout.EndHorizontal();
            }

            DrawThicknessColor(new GUIContent("Player Head"), playerHeadThickness, playerHeadColor);
            DrawThicknessColor(new GUIContent("Volume Envelope"), volumeEnvelopeThickness, volumeEnvelopeColor);
            DrawThicknessColor(new GUIContent("Pitch Envelope"), pitchEnvelopeThickness, pitchEnvelopeColor);
            DrawThicknessColor(new GUIContent("Trim Handle"), trimHandleThickness, trimHandleColor);

            EditorGUILayout.PropertyField(klipWaveformBGColor);
            EditorGUILayout.PropertyField(zequenceWaveformBGColor);
            EditorGUILayout.PropertyField(waveformColor);
            EditorGUILayout.PropertyField(renderedWaveformColor);
            EditorGUILayout.PropertyField(renderedWaveformBGColor);
            EditorGUILayout.PropertyField(renderedPlayerHeadColor);
            EditorGUILayout.PropertyField(trimAreaColor);
            EditorGUILayout.PropertyField(selectedEnvelopeLineColor);
            EditorGUILayout.PropertyField(selectedEnvelopeHandleColor);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Operational Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoRender);
            EditorGUILayout.PropertyField(alertOnClosing);

            EditorGUIUtility.labelWidth = prevLabelWidth;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

    }

}
