using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zounds {

    public class ZoundsPreprocessBuild : IPreprocessBuildWithReport {

        public int callbackOrder { get { return 100; } }
        public void OnPreprocessBuild(BuildReport report) {
            CopyDefaultZoundsProject();
        }

        public static void CopyDefaultZoundsProject() {

            string projectJsonPath = ZoundsProjectInitialization.GetZoundsProjectPath();
            if (string.IsNullOrWhiteSpace(projectJsonPath)) return;
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(projectJsonPath) == null) return;

            string destAssetPath = "Assets/StreamingAssets/DefaultZoundsProject.json";

            if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets")) {
                AssetDatabase.CreateFolder("Assets", "StreamingAssets");
            }

            AssetDatabase.CopyAsset(projectJsonPath, destAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Updated DefaultZoundsProject.");
        }

    }

}
