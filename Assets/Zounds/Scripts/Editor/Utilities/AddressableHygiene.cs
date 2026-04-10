#if ADDRESSABLES_INSTALLED
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Zounds
{
    /// <summary>
    /// Validates and cleans up Addressable entries for Zounds audio clips.
    /// Ensures only clips needed at runtime are marked as Addressable:
    ///   - Sources: never (editor-only sound design palette)
    ///   - Library: only when the Klip has no rendered replacement
    ///   - WorkFiles/ZoundFiles: only when referenced as a renderedClipRef
    ///   - Any other ZoundsData path: never
    /// </summary>
    public static class AddressableHygiene
    {
        /// <summary>
        /// Scans all Addressable entries and removes any that violate the Zounds build rules.
        /// Returns the number of entries removed.
        /// </summary>
        public static int ValidateAndClean()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return 0;

            var project = ZoundsProject.Instance;
            if (project == null || project.projectSettings == null) return 0;

            var projectSettings = project.projectSettings;
            var zoundLibrary = project.zoundLibrary;

            // Collect all referenced rendered clip GUIDs
            var renderedGuids = new HashSet<string>();
            CollectRenderedGuids(zoundLibrary, renderedGuids);

            // Collect Library GUIDs that are actually needed at runtime
            var neededLibraryGuids = new HashSet<string>();
            CollectNeededLibraryGuids(zoundLibrary, neededLibraryGuids);

            // Derive the ZoundsData root from the configured system path
            // e.g. "Assets/GameData/ZoundsData/SystemFiles" -> "Assets/GameData/ZoundsData"
            string zoundsDataRoot = projectSettings.systemFolderPath;
            int lastSlash = zoundsDataRoot.LastIndexOf('/');
            if (lastSlash > 0) zoundsDataRoot = zoundsDataRoot.Substring(0, lastSlash);

            // Scan all Addressable entries
            var allEntries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(allEntries, false);

            var toRemove = new List<AddressableAssetEntry>();
            int removedCount = 0;

            foreach (var entry in allEntries)
            {
                string path = entry.address;
                if (string.IsNullOrEmpty(path)) continue;

                bool inLibrary = path.StartsWith(projectSettings.libraryFolderPath);
                bool inSources = path.StartsWith(projectSettings.sourcesFolderPath);
                bool inWork = path.StartsWith(projectSettings.workFolderPath);
                bool inZoundFiles = path.StartsWith(projectSettings.zoundFilesFolderPath);
                bool inZoundsData = path.StartsWith(zoundsDataRoot);

                // Skip entries that aren't under ZoundsData at all (not ours to manage)
                if (!inZoundsData)
                    continue;

                bool shouldKeep = false;

                if (inLibrary)
                {
                    shouldKeep = neededLibraryGuids.Contains(entry.guid);
                }
                else if (inSources)
                {
                    shouldKeep = false; // Sources never ship
                }
                else if (inWork || inZoundFiles)
                {
                    shouldKeep = renderedGuids.Contains(entry.guid);
                }
                // else: legacy path (UserFiles, SourceFiles, etc.) — never keep

                if (!shouldKeep)
                {
                    toRemove.Add(entry);
                }
            }

            foreach (var entry in toRemove)
            {
                Debug.Log($"[Zounds Hygiene] Removing Addressable entry: {entry.address}");
                settings.RemoveAssetEntry(entry.guid);
                removedCount++;
            }

            if (removedCount > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true);
                AssetDatabase.SaveAssets();
            }

            return removedCount;
        }

        /// <summary>
        /// Dry-run version that reports what would be removed without making changes.
        /// </summary>
        public static List<string> Preview()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return new List<string>();

            var project = ZoundsProject.Instance;
            if (project == null || project.projectSettings == null) return new List<string>();

            var projectSettings = project.projectSettings;
            var zoundLibrary = project.zoundLibrary;

            var renderedGuids = new HashSet<string>();
            CollectRenderedGuids(zoundLibrary, renderedGuids);

            var neededLibraryGuids = new HashSet<string>();
            CollectNeededLibraryGuids(zoundLibrary, neededLibraryGuids);

            string zoundsDataRoot = projectSettings.systemFolderPath;
            int lastSlash = zoundsDataRoot.LastIndexOf('/');
            if (lastSlash > 0) zoundsDataRoot = zoundsDataRoot.Substring(0, lastSlash);

            var allEntries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(allEntries, false);

            var wouldRemove = new List<string>();

            foreach (var entry in allEntries)
            {
                string path = entry.address;
                if (string.IsNullOrEmpty(path)) continue;

                bool inLibrary = path.StartsWith(projectSettings.libraryFolderPath);
                bool inSources = path.StartsWith(projectSettings.sourcesFolderPath);
                bool inWork = path.StartsWith(projectSettings.workFolderPath);
                bool inZoundFiles = path.StartsWith(projectSettings.zoundFilesFolderPath);
                bool inZoundsData = path.StartsWith(zoundsDataRoot);

                if (!inZoundsData)
                    continue;

                bool shouldKeep = false;

                if (inLibrary)
                    shouldKeep = neededLibraryGuids.Contains(entry.guid);
                else if (inWork || inZoundFiles)
                    shouldKeep = renderedGuids.Contains(entry.guid);

                if (!shouldKeep)
                    wouldRemove.Add(path);
            }

            return wouldRemove;
        }

        private static void CollectRenderedGuids(ZoundLibrary zoundLibrary, HashSet<string> renderedGuids)
        {
            zoundLibrary.ForEachZound(z => {
                if (z is Klip klip && klip.renderedClipRef != null && !string.IsNullOrEmpty(klip.renderedClipRef.AssetGUID))
                {
                    renderedGuids.Add(klip.renderedClipRef.AssetGUID);
                }
                if (z is Zequence zeq && zeq.renderedClipRef != null && !string.IsNullOrEmpty(zeq.renderedClipRef.AssetGUID))
                {
                    renderedGuids.Add(zeq.renderedClipRef.AssetGUID);
                }
            });
        }

        private static void CollectNeededLibraryGuids(ZoundLibrary zoundLibrary, HashSet<string> neededGuids)
        {
            zoundLibrary.ForEachZound(z => {
                if (z is Klip klip && klip.audioClipRef != null && !string.IsNullOrEmpty(klip.audioClipRef.AssetGUID))
                {
                    // Library clip is needed at runtime when:
                    // 1. The Klip has no destructive edits (source plays as-is), OR
                    // 2. The Klip has edits but no rendered clip yet (source is fallback)
                    if (!klip.HasActiveEdits())
                    {
                        neededGuids.Add(klip.audioClipRef.AssetGUID);
                    }
                    else if (klip.renderedClipRef == null || !klip.renderedClipRef.RuntimeKeyIsValid())
                    {
                        neededGuids.Add(klip.audioClipRef.AssetGUID);
                    }
                    // If has edits AND rendered clip — source not needed at runtime
                }
            });
        }

        [MenuItem("Zounds/Build Hygiene/Preview Addressable Cleanup")]
        private static void MenuPreview()
        {
            var wouldRemove = Preview();
            if (wouldRemove.Count == 0)
            {
                Debug.Log("[Zounds Hygiene] All Addressable entries are clean. Nothing to remove.");
                return;
            }

            Debug.Log($"[Zounds Hygiene] Would remove {wouldRemove.Count} entries:");
            foreach (var path in wouldRemove)
            {
                Debug.Log($"  - {path}");
            }
        }

        [MenuItem("Zounds/Build Hygiene/Clean Addressable Entries")]
        private static void MenuClean()
        {
            var preview = Preview();
            if (preview.Count == 0)
            {
                Debug.Log("[Zounds Hygiene] All Addressable entries are clean. Nothing to remove.");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Zounds Addressable Cleanup",
                $"This will remove {preview.Count} Addressable entries that are not needed in builds.\n\n" +
                "The audio files themselves will NOT be deleted — only their Addressable registration.\n\n" +
                "Use 'Preview' first to see what will be removed.",
                "Clean", "Cancel");

            if (proceed)
            {
                int removed = ValidateAndClean();
                Debug.Log($"[Zounds Hygiene] Removed {removed} Addressable entries.");
            }
        }
    }
}
#endif
