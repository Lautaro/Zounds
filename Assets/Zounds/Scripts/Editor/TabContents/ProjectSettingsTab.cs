using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class ProjectSettingsTab : TabContent {

        public override string name => "Project Settings";

        #region LABELS
        private GUIContent label_playerVolume           = new GUIContent("Player Volume", "Master volume when the game is running. When switching to play mode, this value goes to the master volume.");
        private GUIContent label_systemVolumeModifier   = new GUIContent("System Volume Modifier", "Modifier for the master volume. This is just used if there is a need to modify the overall volume for the game for any reason.");
        private GUIContent label_editorVolume           = new GUIContent("Editor Volume", "Master volume when in edit mode. When switching to edit mode, this value goes to the master volume.");
        private GUIContent label_systemFolderPath       = new GUIContent("System Folder Path", "Set the path for a folder under Resources where system data is stored.");
        private GUIContent label_userFolderPath         = new GUIContent("User Folder Path", "Path within resources where the user stores its sounds.");
        private GUIContent label_sourceFolderPath       = new GUIContent("Source Folder Path", "Path outside of resources where the user stores source sounds.");
        #endregion LABELS

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            contentRect.x += 10f;
            contentRect.y += 30f;
            contentRect.width -= 20f;
            contentRect.height -= 20f;
            GUILayout.BeginArea(contentRect);

            SerializedProperty projectSettings = serializedObject.FindProperty("projectSettings");
            SerializedProperty playerVolume         = projectSettings.FindPropertyRelative("playerVolume");
            SerializedProperty systemVolumeModifier = projectSettings.FindPropertyRelative("systemVolumeModifier");
            SerializedProperty editorVolume         = projectSettings.FindPropertyRelative("editorVolume");
            SerializedProperty systemFolderPath     = projectSettings.FindPropertyRelative("systemFolderPath");
            SerializedProperty userFolderPath       = projectSettings.FindPropertyRelative("userFolderPath");
            SerializedProperty sourceFolderPath     = projectSettings.FindPropertyRelative("sourceFolderPath");

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150f;
            EditorGUILayout.Slider(playerVolume, 0f, 1f, label_playerVolume);
            EditorGUILayout.Slider(systemVolumeModifier, 0f, 1f, label_systemVolumeModifier);
            EditorGUILayout.Slider(editorVolume, 0f, 1f, label_editorVolume);
            EditorGUILayout.PropertyField(systemFolderPath, label_systemFolderPath);
            EditorGUILayout.PropertyField(userFolderPath, label_userFolderPath);
            EditorGUILayout.PropertyField(sourceFolderPath, label_sourceFolderPath);
            EditorGUIUtility.labelWidth = prevLabelWidth;

            GUILayout.EndArea();
        }

    }

}
