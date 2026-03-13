using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zounds {

    public class ZoundsPreprocessBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport {

        private const string DestAssetPath = "Assets/StreamingAssets/DefaultZoundsProject.json";

        public int callbackOrder { get { return 100; } }

        public void OnPreprocessBuild(BuildReport report) {
            CopyDefaultZoundsProject();
        }

        public void OnPostprocessBuild(BuildReport report) {
            CleanupDefaultZoundsProject();
        }

        public static void CopyDefaultZoundsProject() {
            string projectJsonPath = ZoundsProjectInitialization.GetZoundsProjectPath();
            if (string.IsNullOrWhiteSpace(projectJsonPath)) return;
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(projectJsonPath) == null) return;

            if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets")) {
                AssetDatabase.CreateFolder("Assets", "StreamingAssets");
            }

            // Always overwrite to ensure the build is 1:1 with the current source
            AssetDatabase.CopyAsset(projectJsonPath, DestAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Zounds] Bundled active project for build: {projectJsonPath}");
        }

        private static void CleanupDefaultZoundsProject() {
            if (File.Exists(Path.Combine(Application.dataPath, "StreamingAssets/DefaultZoundsProject.json"))) {
                AssetDatabase.DeleteAsset(DestAssetPath);
                
                // If StreamingAssets is now empty, we can remove it to keep the project root clean
                string streamingPath = Path.Combine(Application.dataPath, "StreamingAssets");
                if (Directory.Exists(streamingPath) && Directory.GetFiles(streamingPath).Length == 0 && Directory.GetDirectories(streamingPath).Length == 0) {
                    AssetDatabase.DeleteAsset("Assets/StreamingAssets");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Zounds] Cleaned up temporary build project.");
            }
        }

    }

}
