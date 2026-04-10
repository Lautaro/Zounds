using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    /// <summary>
    /// One-time migration utility that transforms the old UserFiles/SourceFiles/WorkFiles layout
    /// into the new Library/Sources/WorkFiles/ZoundFiles structure.
    /// Run via Tools > Zounds > Run Folder Migration.
    /// Delete this script after a successful migration and git commit.
    /// </summary>
    public static class ZoundsMigration {

        private const string EditorPrefsKeyOldUserFiles   = "Zounds_Migration_OldUserFilesFolder";
        private const string EditorPrefsKeyOldSourceFiles = "Zounds_Migration_OldSourceFilesFolder";

        private const string DefaultOldUserFilesFolder   = "Assets/ZoundsData/UserFiles";
        private const string DefaultOldSourceFilesFolder = "Assets/ZoundsData/SourceFiles";

        // Menu items removed — run migration from code if needed.
        // private static void RunDryRunMenuItem() => MigrationSetupWindow.Show(dryRun: true);
        // private static void RunMigrationMenuItem() => MigrationSetupWindow.Show(dryRun: false);

        /// <summary>
        /// Executes the migration. When dryRun is true, only logs what would happen without making changes.
        /// </summary>
        public static void RunMigration(bool dryRun) {
            string oldUserFilesFolder   = EditorPrefs.GetString(EditorPrefsKeyOldUserFiles,   DefaultOldUserFilesFolder);
            string oldSourceFilesFolder = EditorPrefs.GetString(EditorPrefsKeyOldSourceFiles, DefaultOldSourceFilesFolder);
            RunMigration(dryRun, oldUserFilesFolder, oldSourceFilesFolder);
        }

        /// <summary>
        /// Executes the migration with explicit legacy folder paths.
        /// When dryRun is true, only logs what would happen without making changes.
        /// </summary>
        public static void RunMigration(bool dryRun, string oldUserFilesFolder, string oldSourceFilesFolder) {
            string dryRunPrefix = dryRun ? "[DRY RUN] " : "";
            Debug.Log($"[Zounds Migration] {dryRunPrefix}Starting migration...");

            var projectSettings = ZoundsProject.Instance.projectSettings;
            var zoundLibrary = ZoundsProject.Instance.zoundLibrary;

            string libraryPath = projectSettings.libraryFolderPath;
            string sourcesPath = projectSettings.sourcesFolderPath;
            string workPath = projectSettings.workFolderPath;
            string zoundFilesPath = projectSettings.zoundFilesFolderPath;

            // Step 1: Backup ZoundsProject.json
            string jsonPath = ZoundsProjectInitialization.GetZoundsProjectPath();
            if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath)) {
                string backupPath = jsonPath + ".backup";
                if (!dryRun) {
                    File.Copy(jsonPath, backupPath, overwrite: true);
                }
                Debug.Log($"[Zounds Migration] {dryRunPrefix}Backup: {jsonPath} -> {backupPath}");
            }

            // Step 2: Ensure new folders exist
            if (!dryRun) {
                ZoundsProject.EnsureDirectoryExists(libraryPath);
                ZoundsProject.EnsureDirectoryExists(sourcesPath);
                ZoundsProject.EnsureDirectoryExists(zoundFilesPath);
            }

            // Step 3: Move UserFiles -> Sources (source clips are the sound design palette, not shipped)
            int movedUserFiles = MoveFolder(oldUserFilesFolder, sourcesPath, dryRun, dryRunPrefix);

            // Step 4: Move SourceFiles -> Sources (legacy rendered-source copies also go to Sources)
            int movedSourceFiles = MoveFolder(oldSourceFilesFolder, sourcesPath, dryRun, dryRunPrefix);

            // Step 5: Move rendered clips from WorkFiles -> ZoundFiles
            // Collect all renderedClipPaths referenced by Klips or Zequences
            var renderedPaths = new HashSet<string>();
            zoundLibrary.ForEachZound(z => {
                if (z is Klip klip && !string.IsNullOrEmpty(klip.renderedClipPath))
                    renderedPaths.Add(klip.renderedClipPath.Replace('\\', '/'));
                else if (z is Zequence zeq && !string.IsNullOrEmpty(zeq.renderedClipPath))
                    renderedPaths.Add(zeq.renderedClipPath.Replace('\\', '/'));
            });

            // Track which files were moved for JSON path updates
            var workToZoundFilesMap = new Dictionary<string, string>();
            int zoundFilesMoveCount = 0;

            if (AssetDatabase.IsValidFolder(workPath)) {
                string[] workGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { workPath });
                foreach (string guid in workGuids) {
                    string srcPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                    if (renderedPaths.Contains(srcPath)) {
                        string relativeName = srcPath.Substring(workPath.Length).TrimStart('/');
                        string destPath = zoundFilesPath + "/" + relativeName;
                        destPath = MakeUniqueAssetPath(destPath);

                        Debug.Log($"[Zounds Migration] {dryRunPrefix}Move rendered: {srcPath} -> {destPath}");
                        if (!dryRun) {
                            EnsureParentDirectory(destPath);
                            string error = AssetDatabase.MoveAsset(srcPath, destPath);
                            if (!string.IsNullOrEmpty(error)) {
                                Debug.LogError($"[Zounds Migration] Failed to move {srcPath}: {error}");
                            }
                            else {
                                workToZoundFilesMap[srcPath] = destPath;
                                zoundFilesMoveCount++;
                            }
                        }
                        else {
                            zoundFilesMoveCount++;
                        }
                    }
                    else {
                        Debug.Log($"[Zounds Migration] {dryRunPrefix}Keep in WorkFiles (source-of-truth): {srcPath}");
                    }
                }
            }

            if (dryRun) {
                Debug.Log($"[Zounds Migration] [DRY RUN] Summary: would move {movedUserFiles} UserFiles → Sources, {movedSourceFiles} SourceFiles → Sources, {zoundFilesMoveCount} WorkFiles → ZoundFiles.");
                return;
            }

            // Step 6: Update string paths in ZoundsProject JSON
            UpdateLibraryStringPaths(zoundLibrary, oldUserFilesFolder, sourcesPath, oldSourceFilesFolder, sourcesPath, workToZoundFilesMap);

            // Step 7 & 8: Save the updated project
            ZoundsWindow.SetZoundsProjectDirty();
            AssetDatabase.Refresh();

            // Step 9: AssetDatabase.Refresh already called above

            // Step 10: Validate all Klip/Zequence references
            ValidateReferences(zoundLibrary);

            // Step 11: Offer to delete empty legacy folders
            PromptDeleteLegacyFolders(oldUserFilesFolder, oldSourceFilesFolder);

            Debug.Log("[Zounds Migration] Migration complete.");
            EditorUtility.DisplayDialog("Zounds Migration", "Migration complete. Check the Console for details.", "OK");
        }

        private static int MoveFolder(string srcFolder, string destFolder, bool dryRun, string dryRunPrefix) {
            if (!AssetDatabase.IsValidFolder(srcFolder)) {
                Debug.Log($"[Zounds Migration] {dryRunPrefix}Source folder not found (skipping): {srcFolder}");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { srcFolder });
            int count = 0;
            foreach (string guid in guids) {
                string srcPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                string relative = srcPath.Substring(srcFolder.Length).TrimStart('/');
                string destPath = destFolder + "/" + relative;
                destPath = MakeUniqueAssetPath(destPath);

                Debug.Log($"[Zounds Migration] {dryRunPrefix}Move: {srcPath} -> {destPath}");
                if (!dryRun) {
                    EnsureParentDirectory(destPath);
                    string error = AssetDatabase.MoveAsset(srcPath, destPath);
                    if (!string.IsNullOrEmpty(error)) {
                        Debug.LogError($"[Zounds Migration] Failed to move {srcPath}: {error}");
                    }
                    else {
                        count++;
                    }
                }
                else {
                    count++;
                }
            }
            return count;
        }

        private static void UpdateLibraryStringPaths(
            ZoundLibrary zoundLibrary,
            string oldUserFolder, string newLibraryFolder,
            string oldSourceFolder, string newSourcesFolder,
            Dictionary<string, string> workToZoundFilesMap) {

            zoundLibrary.ForEachZound(z => {
                if (z is Klip klip) {
                    klip.audioClipPath = RemapPath(klip.audioClipPath, oldUserFolder, newLibraryFolder, oldSourceFolder, newSourcesFolder, workToZoundFilesMap);
                    klip.renderedClipPath = RemapPath(klip.renderedClipPath, oldUserFolder, newLibraryFolder, oldSourceFolder, newSourcesFolder, workToZoundFilesMap);
                }
                else if (z is Zequence zeq) {
                    zeq.renderedClipPath = RemapPath(zeq.renderedClipPath, oldUserFolder, newLibraryFolder, oldSourceFolder, newSourcesFolder, workToZoundFilesMap);
                }
            });
        }

        private static string RemapPath(string path, string oldUserFolder, string newLibraryFolder, string oldSourceFolder, string newSourcesFolder, Dictionary<string, string> workToZoundFilesMap) {
            if (string.IsNullOrEmpty(path)) return path;
            string normalized = path.Replace('\\', '/');

            if (normalized.StartsWith(oldUserFolder))
                return newLibraryFolder + normalized.Substring(oldUserFolder.Length);

            if (normalized.StartsWith(oldSourceFolder))
                return newSourcesFolder + normalized.Substring(oldSourceFolder.Length);

            if (workToZoundFilesMap.TryGetValue(normalized, out string mappedPath))
                return mappedPath;

            return path;
        }

        private static void ValidateReferences(ZoundLibrary zoundLibrary) {
            int warnings = 0;
            zoundLibrary.ForEachZound(z => {
                if (z is Klip klip) {
                    if (klip.audioClipRef != null && !string.IsNullOrEmpty(klip.audioClipRef.AssetGUID)) {
                        string path = AssetDatabase.GUIDToAssetPath(klip.audioClipRef.AssetGUID);
                        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                            Debug.LogWarning($"[Zounds Migration] Klip '{klip.name}' has an unresolved audioClipRef GUID: {klip.audioClipRef.AssetGUID}");
                            warnings++;
                        }
                    }
                    if (klip.renderedClipRef != null && !string.IsNullOrEmpty(klip.renderedClipRef.AssetGUID)) {
                        string path = AssetDatabase.GUIDToAssetPath(klip.renderedClipRef.AssetGUID);
                        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                            Debug.LogWarning($"[Zounds Migration] Klip '{klip.name}' has an unresolved renderedClipRef GUID: {klip.renderedClipRef.AssetGUID}");
                            warnings++;
                        }
                    }
                }
                else if (z is Zequence zeq) {
                    if (zeq.renderedClipRef != null && !string.IsNullOrEmpty(zeq.renderedClipRef.AssetGUID)) {
                        string path = AssetDatabase.GUIDToAssetPath(zeq.renderedClipRef.AssetGUID);
                        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                            Debug.LogWarning($"[Zounds Migration] Zequence '{zeq.name}' has an unresolved renderedClipRef GUID: {zeq.renderedClipRef.AssetGUID}");
                            warnings++;
                        }
                    }
                }
            });

            if (warnings == 0)
                Debug.Log("[Zounds Migration] Validation passed — all references resolved successfully.");
            else
                Debug.LogWarning($"[Zounds Migration] Validation complete with {warnings} warning(s). Review the above messages.");
        }

        private static void PromptDeleteLegacyFolders(params string[] legacyFolders) {
            var emptyFolders = new List<string>();
            foreach (string folder in legacyFolders) {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                string[] remaining = AssetDatabase.FindAssets("", new[] { folder });
                if (remaining.Length == 0) {
                    emptyFolders.Add(folder);
                }
                else {
                    Debug.Log($"[Zounds Migration] Legacy folder '{folder}' is not empty ({remaining.Length} assets remain). Skipping deletion.");
                }
            }

            if (emptyFolders.Count == 0) return;

            string folderList = string.Join("\n", emptyFolders);
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Legacy Folders",
                $"The following legacy folders are now empty and can be deleted:\n\n{folderList}\n\nDelete them?",
                "Delete",
                "Keep");

            if (confirmed) {
                foreach (string folder in emptyFolders) {
                    AssetDatabase.DeleteAsset(folder);
                    Debug.Log($"[Zounds Migration] Deleted empty legacy folder: {folder}");
                }
                AssetDatabase.Refresh();
            }
        }

        private static string MakeUniqueAssetPath(string path) {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;
            string candidate;
            do {
                candidate = $"{dir}/{fileNameNoExt}_{i}{ext}";
                i++;
            } while (File.Exists(candidate));
            return candidate;
        }

        private static void EnsureParentDirectory(string assetPath) {
            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) {
                ZoundsProject.EnsureDirectoryExists(dir);
            }
        }
    }

    /// <summary>
    /// Pre-migration setup window. Lets the user specify the old folder paths before committing to the migration.
    /// </summary>
    internal class MigrationSetupWindow : EditorWindow {

        private const string EditorPrefsKeyOldUserFiles   = "Zounds_Migration_OldUserFilesFolder";
        private const string EditorPrefsKeyOldSourceFiles = "Zounds_Migration_OldSourceFilesFolder";

        private string oldUserFilesFolder;
        private string oldSourceFilesFolder;
        private bool dryRun;

        internal static void Show(bool dryRun) {
            var window = GetWindow<MigrationSetupWindow>(true, "Zounds Folder Migration", true);
            window.dryRun = dryRun;
            window.oldUserFilesFolder   = EditorPrefs.GetString(EditorPrefsKeyOldUserFiles,   "Assets/ZoundsData/UserFiles");
            window.oldSourceFilesFolder = EditorPrefs.GetString(EditorPrefsKeyOldSourceFiles, "Assets/ZoundsData/SourceFiles");
            window.minSize = new Vector2(500f, 220f);
            window.maxSize = new Vector2(700f, 220f);
            window.ShowUtility();
        }

        private void OnGUI() {
            var projectSettings = ZoundsProject.Instance.projectSettings;

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Specify where the old folders currently live in this project. " +
                "The new target paths come from Workspace Directories in the Project Settings tab.",
                MessageType.Info);
            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField("Old Folder Paths (source)", EditorStyles.boldLabel);

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160f;

            oldUserFilesFolder   = EditorGUILayout.TextField("Old UserFiles Path",   oldUserFilesFolder);
            oldSourceFilesFolder = EditorGUILayout.TextField("Old SourceFiles Path", oldSourceFilesFolder);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("New Folder Paths (destination, from Project Settings)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Sources Path (UserFiles + SourceFiles →)",  projectSettings.sourcesFolderPath);
            EditorGUILayout.TextField("Library Path (manual placement only)",  projectSettings.libraryFolderPath);
            EditorGUI.EndDisabledGroup();

            EditorGUIUtility.labelWidth = prevLabelWidth;

            EditorGUILayout.Space(10f);
            GUILayout.BeginHorizontal();

            string buttonLabel = dryRun ? "Run Dry Run" : "Migrate";
            string dialogTitle = dryRun ? "Dry Run" : "Zounds Folder Migration";
            string dialogMessage = dryRun
                ? "This will log what the migration would do without making any changes. Proceed?"
                : "This will migrate your ZoundsData folder structure. A JSON backup will be created first. This operation cannot be undone automatically.\n\nProceed?";

            if (GUILayout.Button(buttonLabel)) {
                EditorPrefs.SetString(EditorPrefsKeyOldUserFiles,   oldUserFilesFolder);
                EditorPrefs.SetString(EditorPrefsKeyOldSourceFiles, oldSourceFilesFolder);

                bool confirmed = EditorUtility.DisplayDialog(dialogTitle, dialogMessage, dryRun ? "Run" : "Migrate", "Cancel");
                if (confirmed) {
                    Close();
                    ZoundsMigration.RunMigration(dryRun, oldUserFilesFolder, oldSourceFilesFolder);
                }
            }

            if (GUILayout.Button("Cancel")) Close();

            GUILayout.EndHorizontal();
        }
    }

}
