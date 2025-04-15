#if DISABLED
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class AudioRenderTestWindow : EditorWindow {

        [MenuItem("Tools/Audio Render Test")]
        private static void OpenWindow() {
            var window = GetWindow<AudioRenderTestWindow>();
            window.Show();
        }

        [SerializeField] private AudioClip clipToTrim;
        [SerializeField] private float startTime = 0.1f;
        [SerializeField] private float endTime = 1.4f;
        [SerializeField] private ClipDelayPair[] clipsToCombine = new ClipDelayPair[0];
        [SerializeField] private AudioClip clipToVolumeEnvelope;
        [SerializeField] private AnimationCurve volumeEnvelope = new AnimationCurve(new Keyframe[] { new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0) });
        [SerializeField] private AudioClip clipToPitchEnvelope;
        [SerializeField] private AnimationCurve pitchEnvelope = new AnimationCurve(new Keyframe[] { new Keyframe(0, 0.5f), new Keyframe(0.5f, 1.5f), new Keyframe(0.75f, 1f), new Keyframe(1, 1f) });
        [SerializeField] private AudioClip clipToCutOffEnvelope;
        [SerializeField] private bool cutOffHighFrequency = true;
        [SerializeField] private float resonance = 1f;
        [SerializeField] private AnimationCurve cutOffEnvelope = new AnimationCurve(new Keyframe[] { new Keyframe(0, 500f), new Keyframe(1, 500f) });

        private SerializedObject serializedObject;
        private Vector2 scrollPos;

        private void OnEnable() {
            titleContent.text = "Audio Render Test";
            minSize = new Vector2(348f, 200f);
            serializedObject = new SerializedObject(this);

            var grumpyWoman = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ZoundsData/UserFiles/Grumpy Woman.wav");
            var fox2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ZoundsData/UserFiles/Fox 2.wav");
            if (clipToTrim == null) clipToTrim = grumpyWoman;
            if (clipsToCombine.Length == 0) {
                clipsToCombine = new[] {
                    new ClipDelayPair(){ clip = grumpyWoman, delay = 0f},
                    new ClipDelayPair(){ clip = fox2, delay = 0.4f},
                };
            }
            if (clipToVolumeEnvelope == null) clipToVolumeEnvelope = grumpyWoman;
            if (clipToPitchEnvelope == null) clipToPitchEnvelope = grumpyWoman;
            if (clipToCutOffEnvelope == null) clipToCutOffEnvelope = grumpyWoman;
        }

        private void OnGUI() {
            serializedObject.Update();

            bool doTrim = false;
            bool doCombine = false;
            bool doVolumeEnvelope = false; 
            bool doPitchEnvelope = false;
            bool doCutOffEnvelope = false;

            bool guiEnabled = GUI.enabled;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.LabelField("Trim", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clipToTrim"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("startTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("endTime"));
            GUI.enabled = guiEnabled && clipToTrim != null;
            if (GUILayout.Button("Render")) {
                doTrim = true;
            }
            GUI.enabled = guiEnabled;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Combine", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clipsToCombine"));
            GUI.enabled = guiEnabled && clipsToCombine.Length > 0;
            if (GUILayout.Button("Render")) {
                doCombine = true;
            }
            GUI.enabled = guiEnabled;
            EditorGUILayout.Space();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AnimationCurve will not be used as the actual envelope.", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This is just for dummy simulation.", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Volume Envelope", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clipToVolumeEnvelope"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("volumeEnvelope"));
            GUI.enabled = guiEnabled && clipToVolumeEnvelope != null;
            if (GUILayout.Button("Render")) {
                doVolumeEnvelope = true;
            }
            GUI.enabled = guiEnabled;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Pitch Envelope", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clipToPitchEnvelope"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchEnvelope"));
            GUI.enabled = guiEnabled && clipToPitchEnvelope != null;
            if (GUILayout.Button("Render")) {
                doPitchEnvelope = true;
            }
            GUI.enabled = guiEnabled;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("CutOff Envelope", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clipToCutOffEnvelope"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cutOffHighFrequency"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("resonance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cutOffEnvelope"));
            GUI.enabled = guiEnabled && clipToCutOffEnvelope != null;
            if (GUILayout.Button("Render")) {
                doCutOffEnvelope = true;
            }
            GUI.enabled = guiEnabled;
            EditorGUILayout.Space();

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();

            if (doTrim) {
                var result = AudioRenderUtility.Trim(clipToTrim, startTime, endTime);
                var projectSettings = ZoundsProject.Instance.projectSettings;
                var filePath = AssetDatabase.GetAssetPath(clipToTrim).Replace(projectSettings.userFolderPath, projectSettings.workFolderPath);
                filePath = filePath.Replace(".wav", "_Trimmed.wav");
                SaveAudio(result, filePath);
            }
            if (doCombine) {
                var result = AudioRenderUtility.Combine(clipsToCombine.Select(c => c.clip).ToArray(), clipsToCombine.Select(c => c.delay).ToArray());
                var projectSettings = ZoundsProject.Instance.projectSettings;
                var filePath = AssetDatabase.GetAssetPath(clipsToCombine[0].clip).Replace(projectSettings.userFolderPath, projectSettings.workFolderPath);
                filePath = filePath.Replace(".wav", "_Combined.wav");
                SaveAudio(result, filePath);
            }
            if (doVolumeEnvelope) {
                var result = AudioRenderUtility.VolumeEnvelope(clipToVolumeEnvelope, volumeEnvelope);
                var projectSettings = ZoundsProject.Instance.projectSettings;
                var filePath = AssetDatabase.GetAssetPath(clipToVolumeEnvelope).Replace(projectSettings.userFolderPath, projectSettings.workFolderPath);
                filePath = filePath.Replace(".wav", "_VolumeEnveloped.wav");
                SaveAudio(result, filePath);
            }
            if (doPitchEnvelope) {
                var result = AudioRenderUtility.PitchEnvelope(clipToPitchEnvelope, pitchEnvelope);
                var projectSettings = ZoundsProject.Instance.projectSettings;
                var filePath = AssetDatabase.GetAssetPath(clipToPitchEnvelope).Replace(projectSettings.userFolderPath, projectSettings.workFolderPath);
                filePath = filePath.Replace(".wav", "_PitchEnveloped.wav");
                SaveAudio(result, filePath);
            }
            if (doCutOffEnvelope) {
                var result = AudioRenderUtility.CutOffEnvelope(clipToCutOffEnvelope, cutOffEnvelope, cutOffHighFrequency, resonance);
                var projectSettings = ZoundsProject.Instance.projectSettings;
                var filePath = AssetDatabase.GetAssetPath(clipToCutOffEnvelope).Replace(projectSettings.userFolderPath, projectSettings.workFolderPath);
                filePath = filePath.Replace(".wav", "_CutOffEnveloped.wav");
                SaveAudio(result, filePath);
            }
        }

        private static void SaveAudio(AudioClip result, string filePath) {
            SavWav.Save(GetAbsolutePath(filePath), result);
            AssetDatabase.ImportAsset(filePath);

            var reloaded = AssetDatabase.LoadAssetAtPath<AudioClip>(filePath);
            Debug.Log("Saved to: " + filePath, reloaded);
            EditorGUIUtility.PingObject(reloaded);
            Selection.activeObject = reloaded;
        }

        public static string GetAbsolutePath(string assetPath) {
            if (string.IsNullOrEmpty(assetPath)) {
                Debug.LogWarning("GetAbsolutePath: assetPath is null or empty.");
                return null;
            }

            if (assetPath.StartsWith("Assets/")) {
                return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring(7)));
            }
            else if (assetPath.StartsWith("Packages/")) {
                // TODO: Handle Packages/ folder
                Debug.LogError("GetAbsolutePath for Packages is not yet implemented.");
            }
            else {
                Debug.LogWarning("GetAbsolutePath: Unsupported asset path " + assetPath);
            }

            return null;
        }

        [System.Serializable]
        public class ClipDelayPair {
            public AudioClip clip;
            public float delay;
        }

    }

}
#endif