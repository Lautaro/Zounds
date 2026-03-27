#if ADDRESSABLES_INSTALLED
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    /// <summary>
    /// Editor tab that visualizes the full dependency graph of Zounds and AudioClips.
    /// Sections: Broken Zounds, Orphaned Files, Dependency Browser, Build AudioClips.
    /// </summary>
    public class DependencyMapTab : TabContent {

        private enum Section {
            DependencyBrowser,
            BrokenZounds,
            Orphans,
            BuildClips
        }

        private const int MaxVisibleSubItems = 5;
        private const float RefreshIntervalSeconds = 5.0f;

        public override string name { get; set; } = "Dep. Map";
        public override string tooltip { get; set; } = "Dependency Map: relationships, orphans, broken refs, and build clips.";
        public override Color headerColor => (analyzer != null && analyzer.brokenZounds.Count > 0) ? new Color(1f, 0.4f, 0.4f) : Color.white;

        private ZoundDependencyAnalyzer analyzer;
        private double lastAnalysisTime;
        private bool needsRefresh = true;

        private Section activeSection = Section.DependencyBrowser;
        private Vector2 scrollPos;
        private string searchFilter = "";

        // Foldout state keyed by Zound id or clip path hash
        private HashSet<int> expandedZoundIds = new HashSet<int>();
        private HashSet<string> expandedClipPaths = new HashSet<string>();
        // Tracks "show all" overflow state per list (keyed by owner id + direction label)
        private HashSet<string> showAllKeys = new HashSet<string>();
        private bool _orphansExpanded = true;
        private bool _unusedSourcesExpanded = true;
        private bool _unusedLibraryExpanded = false;


        private GUIStyle s_foldoutRich;
        private GUIStyle s_miniLabelWrap;

        private GUIStyle FoldoutRich {
            get {
                if (s_foldoutRich == null) {
                    s_foldoutRich = new GUIStyle(EditorStyles.foldout) { richText = true };
                }
                return s_foldoutRich;
            }
        }

        private GUIStyle MiniLabelWrap {
            get {
                if (s_miniLabelWrap == null) {
                    s_miniLabelWrap = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                }
                return s_miniLabelWrap;
            }
        }
        private GUIStyle s_labelRich;
        private GUIStyle LabelRich {
            get {
                if (s_labelRich == null) {
                    s_labelRich = new GUIStyle(EditorStyles.label) { richText = true };
                }
                return s_labelRich;
            }
        }


        // ── Tab lifecycle ───────────────────────────────────────────────

        public override void OnTabOpened() {
            needsRefresh = true;
        }

        public override void Update() {
            if (needsRefresh || EditorApplication.timeSinceStartup - lastAnalysisTime > RefreshIntervalSeconds) {
                RefreshAnalysis();
            }
        }

        /// <summary>
        /// Forces an immediate re-analysis. Call from outside when project data changes.
        /// </summary>
        public void RequestRefresh() {
            needsRefresh = true;
        }

        private void RefreshAnalysis() {
            analyzer = ZoundDependencyAnalyzer.Analyze();
            lastAnalysisTime = EditorApplication.timeSinceStartup;
            needsRefresh = false;
        }

        // ── Main draw ───────────────────────────────────────────────────

        public override void OnGUI(SerializedObject serializedObject, Rect contentRect) {
            if (analyzer == null) RefreshAnalysis();

            using (ZUI.Box())
            {
            GUILayout.Space(4f);

            // Section toolbar
            GUILayout.BeginHorizontal();
            {
                DrawSectionButton(Section.DependencyBrowser, "Dependencies");
                DrawSectionButton(Section.BrokenZounds, $"Broken ({analyzer.brokenZounds.Count})");
                DrawSectionButton(Section.Orphans, $"Orphans ({analyzer.orphanClips.Count})");
                DrawSectionButton(Section.BuildClips, $"Build ({analyzer.buildClips.Count})");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f))) {
                    RefreshAnalysis();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            {
                switch (activeSection) {
                    case Section.DependencyBrowser:
                        DrawDependencyBrowser();
                        break;
                    case Section.BrokenZounds:
                        DrawBrokenZounds();
                        break;
                    case Section.Orphans:
                        DrawOrphans();
                        break;
                    case Section.BuildClips:
                        DrawBuildClips();
                        break;
                }
            }
            GUILayout.EndScrollView();
            } // end ZUI.Box
        }

        private void DrawSectionButton(Section section, string label) {
            bool isActive = activeSection == section;
            var prevColor = GUI.backgroundColor;
            
            if (isActive) GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            else if (section == Section.BrokenZounds && analyzer.brokenZounds.Count > 0) GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            if (GUILayout.Toggle(isActive, label, EditorStyles.toolbarButton, GUILayout.Height(20f))) {
                if (!isActive) {
                    activeSection = section;
                    scrollPos = Vector2.zero;
                }
            }
            GUI.backgroundColor = prevColor;
        }

        // ── Section: Dependency Browser ─────────────────────────────────

        private void DrawDependencyBrowser() {
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Search:", GUILayout.Width(50f));
                searchFilter = EditorGUILayout.TextField(searchFilter);
                if (GUILayout.Button("X", GUILayout.Width(20f))) {
                    searchFilter = "";
                    GUI.FocusControl(null);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            string filterLower = searchFilter.ToLowerInvariant();

            var sortedNodes = analyzer.zoundNodes.Values
                .Where(n => !(n.zound is ClipZound))
                .Where(n => n.zound.parentId == 0) // top-level only
                .OrderBy(n => n.zound.name)
                .ToList();

            if (!string.IsNullOrEmpty(filterLower)) {
                sortedNodes = sortedNodes
                    .Where(n => n.zound.name.ToLowerInvariant().Contains(filterLower))
                    .ToList();
            }

            if (sortedNodes.Count == 0) {
                EditorGUILayout.LabelField("No Zounds match the filter.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            foreach (var node in sortedNodes) {
                DrawZoundNode(node);
            }
        }

        private void DrawZoundNode(ZoundDependencyAnalyzer.ZoundNode node) {
            var z = node.zound;
            bool expanded = expandedZoundIds.Contains(z.id);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.BeginHorizontal();
                {
                    // Foldout
                    string typeLabel = GetTypeLabel(z);
                    string colorTag = node.isBroken ? "<color=#FF6666>" : "<color=#CCCCCC>";
                    string foldoutLabel = $"{colorTag}[{typeLabel}]</color> ";
                    
                    var foldoutRect = GUILayoutUtility.GetRect(new GUIContent(foldoutLabel), EditorStyles.foldout, GUILayout.Width(50f));
                    bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, foldoutLabel, true, FoldoutRich);
                    if (newExpanded != expanded) {
                        if (newExpanded) expandedZoundIds.Add(z.id);
                        else expandedZoundIds.Remove(z.id);
                    }

                    DrawZoundLabelRich(z, $"<b>{z.name}</b>", FoldoutRich);
                }
                GUILayout.EndHorizontal();

                if (expanded) {
                    EditorGUI.indentLevel++;

                    // Broken reason
                    if (node.isBroken) {
                        var prevColor = GUI.color;
                        GUI.color = new Color(1f, 0.4f, 0.4f);
                        EditorGUILayout.LabelField(node.brokenReason, MiniLabelWrap);
                        GUI.color = prevColor;
                    }

                    // Depends on (children / source clips)
                    if (node.dependsOn.Count > 0) {
                        EditorGUILayout.LabelField("Depends on:", EditorStyles.miniBoldLabel);
                        DrawCollapsibleZoundList(node.dependsOn, z.id, "dep");
                    }

                    // Depended on by (parents that reference this)
                    if (node.dependedOnBy.Count > 0) {
                        EditorGUILayout.LabelField("Used by:", EditorStyles.miniBoldLabel);
                        DrawCollapsibleZoundList(node.dependedOnBy, z.id, "usedby");
                    }

                    // AudioClip references
                    if (node.clipPaths.Count > 0) {
                        EditorGUILayout.LabelField("AudioClips:", EditorStyles.miniBoldLabel);
                        foreach (string path in node.clipPaths) {
                            GUILayout.BeginHorizontal();
                            {
                                DrawClipLabel(path, System.IO.Path.GetFileName(path), EditorStyles.miniLabel);
                                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(35f))) {
                                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                                    if (asset != null) EditorGUIUtility.PingObject(asset);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }

                    // No dependencies at all
                    if (node.dependsOn.Count == 0 && node.dependedOnBy.Count == 0 && node.clipPaths.Count == 0) {
                        EditorGUILayout.LabelField("No dependencies.", EditorStyles.centeredGreyMiniLabel);
                    }

                    EditorGUI.indentLevel--;
                }
            }
            GUILayout.EndVertical();
        }

        // ── Section: Broken Zounds ──────────────────────────────────────

        private void DrawBrokenZounds() {
            if (analyzer.brokenGroups.Count == 0) {
                GUILayout.Space(20f);
                EditorGUILayout.LabelField("No broken references detected.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.LabelField($"{analyzer.brokenGroups.Count} missing file(s) identified:", EditorStyles.boldLabel);
            GUILayout.Space(4f);

            foreach (var kvp in analyzer.brokenGroups) {
                var group = kvp.Value;
                GUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    // 1. Missing File Header
                    GUILayout.BeginHorizontal();
                    {
                        GUI.color = new Color(1f, 0.4f, 0.4f);
                        string fileName = System.IO.Path.GetFileName(group.missingKey);
                        if (string.IsNullOrEmpty(fileName)) fileName = group.missingKey;
                        EditorGUILayout.LabelField($"MISSING: {fileName}", EditorStyles.boldLabel);
                        GUI.color = Color.white;
                    }
                    GUILayout.EndHorizontal();

                    EditorGUILayout.LabelField(group.missingKey, EditorStyles.miniLabel);
                    GUILayout.Space(4f);

                    // 2. Affected Zounds list
                    EditorGUILayout.LabelField("Referenced by:", EditorStyles.miniBoldLabel);
                    foreach (var rootEntry in group.affectedHierarchy) {
                        var rootName = rootEntry.Key;
                        var children = rootEntry.Value;

                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        {
                            EditorGUILayout.LabelField($"<b>{rootName}</b>", LabelRich);
                            
                            foreach (var childEntry in children) {
                                var childName = childEntry.Key;
                                var slots = childEntry.Value;
                                
                                string display = string.IsNullOrEmpty(childName) ? "" : $"{childName}";
                                foreach (var slot in slots) {
                                    if (!string.IsNullOrEmpty(display)) display += " / ";
                                    display += slot;
                                }

                                EditorGUILayout.LabelField($"  <color=#FF4444>•</color> {display}", LabelRich);
                            }
                        }
                        GUILayout.EndVertical();
                    }

                    GUILayout.Space(8f);

                    // 3. Group Fix UI
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    {
                        GUILayout.BeginHorizontal();
                        {
                            EditorGUI.BeginChangeCheck();
                            var selectedClip = EditorGUILayout.ObjectField("Replace with:", group.stagedFix, typeof(AudioClip), false) as AudioClip;
                            if (EditorGUI.EndChangeCheck()) {
                                group.stagedFix = selectedClip;
                            }

                            if (group.stagedFix != null) {
                                var originalBg = GUI.backgroundColor;
                                GUI.backgroundColor = Color.green;
                                if (GUILayout.Button("FIX ALL", GUILayout.Width(80f), GUILayout.Height(18f))) {
                                    ApplyGroupFix(group);
                                }
                                GUI.backgroundColor = originalBg;

                                if (GUILayout.Button("X", GUILayout.Width(20f))) {
                                    group.stagedFix = null;
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void ApplyGroupFix(ZoundDependencyAnalyzer.BrokenGroup group) {
            if (group.stagedFix == null) return;

            string newPath = AssetDatabase.GetAssetPath(group.stagedFix);
            string newGuid = AssetDatabase.AssetPathToGUID(newPath);

            ZoundsWindow.ModifyAndSaveZoundsProject($"fix {group.links.Count} broken references", () => {
                var clipRef = new UnityEngine.AddressableAssets.AssetReference(newGuid);
                
                // Get all affected Zounds from the links
                var library = ZoundsProject.Instance.zoundLibrary;
                foreach (var link in group.links) {
                    var z = library.FindZound(zound => zound.id == link.zoundId);
                    if (z == null) continue;

                    if (z is Klip klip) {
                        if (MatchesMissingKey(klip.audioClipRef, klip.audioClipPath, group.missingKey)) {
                            klip.audioClipRef = clipRef;
                            klip.audioClipPath = newPath;
                            klip.needsRender = true;
                        }
                        if (MatchesMissingKey(klip.renderedClipRef, klip.renderedClipPath, group.missingKey)) {
                            klip.renderedClipRef = clipRef;
                            klip.renderedClipPath = newPath;
                        }

                        if (Application.isPlaying && ZoundEngine.IsInitialized()) {
                            ZoundDictionary.ValidateZoundRuntime(klip);
                        }
                    } else if (z is Muzic muzic) {
                        if (MatchesMissingKey(muzic.audioClipRef, muzic.audioClipPath, group.missingKey)) {
                            muzic.audioClipRef = clipRef;
                            muzic.audioClipPath = newPath;
                        }
                    } else if (z is Zequence zeq) {
                        if (MatchesMissingKey(zeq.renderedClipRef, zeq.renderedClipPath, group.missingKey)) {
                            zeq.renderedClipRef = clipRef;
                            zeq.renderedClipPath = newPath;
                        }
                    }
                }
            }, true);

            #if ADDRESSABLES_INSTALLED
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null) {
                var entry = settings.CreateOrMoveEntry(newGuid, settings.DefaultGroup);
                if (entry != null) entry.address = newPath;
            }
            #endif

            RequestRefresh();
        }

        private bool MatchesMissingKey(AssetReference clipRef, string path, string missingKey) {
            if (clipRef != null && clipRef.AssetGUID == missingKey) return true;
            if (path == missingKey) return true;
            return false;
        }

        // ── Section: Orphans ────────────────────────────────────────────

        private void DrawOrphans() {
            // Orphans Section (Work/Rendered Files)
            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.BeginHorizontal();
                _orphansExpanded = EditorGUILayout.Foldout(_orphansExpanded, $"Orphans ({analyzer.orphanClips.Count})", true, EditorStyles.boldLabel);
                if (_orphansExpanded && analyzer.orphanClips.Count > 0) {
                    if (GUILayout.Button("Delete All Orphans", GUILayout.Width(130f))) {
                        var paths = analyzer.orphanClips.Select(c => c.assetPath).ToList();
                        bool confirmed = EditorUtility.DisplayDialog(
                            "Delete Orphans",
                            $"Delete {paths.Count} orphaned file(s)?\n\n{string.Join("\n", paths.Take(15))}" +
                            (paths.Count > 15 ? $"\n...and {paths.Count - 15} more" : ""),
                            "Delete", "Cancel");
                        if (confirmed) {
                            foreach (var p in paths) AssetDatabase.DeleteAsset(p);
                            RefreshAnalysis();
                        }
                    }
                }
                GUILayout.EndHorizontal();

                if (_orphansExpanded) {
                    if (analyzer.orphanClips.Count > 0) {
                        GUILayout.Space(4f);
                        foreach (var cn in analyzer.orphanClips) {
                            DrawCleanupEntry(cn);
                        }
                    } else {
                        EditorGUILayout.LabelField("All Work/Rendered files are in use", EditorStyles.centeredGreyMiniLabel);
                    }
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(12f);

            // Unused Source Section
            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                _unusedSourcesExpanded = EditorGUILayout.Foldout(_unusedSourcesExpanded, $"Unused in Source folder ({analyzer.unusedSourceClips.Count})", true, EditorStyles.boldLabel);
                if (_unusedSourcesExpanded) {
                    if (analyzer.unusedSourceClips.Count > 0) {
                        GUILayout.Space(4f);
                        foreach (var cn in analyzer.unusedSourceClips) {
                            DrawCleanupEntry(cn);
                        }
                    } else {
                        EditorGUILayout.LabelField("All source assets are in use", EditorStyles.centeredGreyMiniLabel);
                    }
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(12f);

            // Unused Library Section
            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                _unusedLibraryExpanded = EditorGUILayout.Foldout(_unusedLibraryExpanded, $"Unused in Library folder ({analyzer.unusedLibraryClips.Count})", true, EditorStyles.boldLabel);
                if (_unusedLibraryExpanded) {
                    if (analyzer.unusedLibraryClips.Count > 0) {
                        GUILayout.Space(4f);
                        foreach (var cn in analyzer.unusedLibraryClips) {
                            DrawCleanupEntry(cn);
                        }
                    } else {
                        EditorGUILayout.LabelField("All library assets are referenced", EditorStyles.centeredGreyMiniLabel);
                    }
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawCleanupEntry(ZoundDependencyAnalyzer.ClipNode cn) {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            {
                DrawClipLabel(cn.assetPath, cn.fileName, EditorStyles.label, GUILayout.MinWidth(100f));
                EditorGUILayout.LabelField(cn.assetPath, EditorStyles.miniLabel);
                
                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(35f))) {
                    if (cn.clip != null) EditorGUIUtility.PingObject(cn.clip);
                }

                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(50f))) {
                    if (EditorUtility.DisplayDialog("Delete Asset?", $"Are you sure you want to permanently delete this audio file?\n\n{cn.assetPath}", "Delete", "Cancel")) {
                        AssetDatabase.DeleteAsset(cn.assetPath);
                        RefreshAnalysis();
                    }
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
        }

        // ── Section: Build AudioClips ───────────────────────────────────

        private void DrawBuildClips() {
            if (analyzer.buildClips.Count == 0) {
                GUILayout.Space(20f);
                EditorGUILayout.LabelField("No AudioClips would be included in the build.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var dependencyClips = analyzer.buildClips.Where(c => c.referencedBy.Count > 0).ToList();
            var libraryOnlyClips = analyzer.buildClips.Where(c => c.referencedBy.Count == 0 && c.isLibraryClip).ToList();

            if (dependencyClips.Count > 0) {
                EditorGUILayout.LabelField("Referenced Dependencies:", EditorStyles.boldLabel);
                foreach (var cn in dependencyClips) {
                    DrawClipEntry(cn, new Color(0.7f, 1f, 0.7f, 0.15f), "Dependency");
                }
                GUILayout.Space(10f);
            }

            if (libraryOnlyClips.Count > 0) {
                EditorGUILayout.LabelField("Library Exports (Available by Name):", EditorStyles.boldLabel);
                foreach (var cn in libraryOnlyClips) {
                    DrawClipEntry(cn, new Color(0.7f, 0.85f, 1f, 0.15f), "Library");
                }
            }
        }

        private void DrawClipEntry(ZoundDependencyAnalyzer.ClipNode cn, Color bgColor, string typeTag) {
            bool expanded = expandedClipPaths.Contains(cn.assetPath);

            var projectSettings = ZoundsProject.Instance.projectSettings;
            Color folderColor = Color.white;
            if (cn.assetPath.StartsWith(projectSettings.sourcesFolderPath)) folderColor = new Color(0.7f, 0.8f, 1.0f); // Blue for Sources
            else if (cn.assetPath.StartsWith(projectSettings.libraryFolderPath)) folderColor = new Color(0.7f, 0.9f, 0.7f); // Green for Library
            else if (cn.assetPath.StartsWith(projectSettings.workFolderPath) || cn.assetPath.StartsWith(projectSettings.zoundFilesFolderPath)) folderColor = new Color(1.0f, 0.8f, 0.6f); // Orange for Work/Rendered

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor * 2f;
            GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox);
            boxStyle.margin = new RectOffset(0, 0, 0, 0);
            
            GUILayout.BeginVertical(boxStyle);
            GUI.backgroundColor = prevBg;
            {
                GUILayout.BeginHorizontal();
                {
                    GUI.color = folderColor;
                    string label = $"<b>{cn.fileName}</b>  <color=#AAAAAA>({typeTag})</color>";

                    bool newExpanded = EditorGUILayout.Foldout(expanded, label, true, FoldoutRich);
                    if (newExpanded != expanded) {
                        if (newExpanded) expandedClipPaths.Add(cn.assetPath);
                        else expandedClipPaths.Remove(cn.assetPath);
                    }
                    GUI.color = Color.white;

                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(35f))) {
                        if (cn.clip != null) EditorGUIUtility.PingObject(cn.clip);
                    }
                }
                GUILayout.EndHorizontal();

                if (expanded) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(cn.assetPath, EditorStyles.miniLabel);

                    // Build full dependency chain
                    var allDependents = new HashSet<Zound>();
                    foreach (var directZound in cn.referencedBy) {
                        allDependents.Add(directZound);
                        // Traverse up the hierarchy to find all parents and roots
                        Zound current = directZound;
                        while (current.parentId != 0) {
                            var parent = ZoundsProject.Instance.zoundLibrary.FindZound(z => z.id == current.parentId);
                            if (parent != null) {
                                allDependents.Add(parent);
                                current = parent;
                            } else break;
                        }
                    }

                    if (allDependents.Count > 0) {
                        EditorGUILayout.LabelField("Dependency Chain:", EditorStyles.miniBoldLabel);
                        
                        // Separate into Public (Roots or Non-Local) and Local for clear display
                        var publicDependents = allDependents.Where(z => z.parentId == 0).OrderBy(z => z.name).ToList();
                        var nestedDependents = allDependents.Where(z => z.parentId != 0).OrderBy(z => z.name).ToList();

                        if (publicDependents.Count > 0) {
                            EditorGUILayout.LabelField("Public (Root) Zounds:", EditorStyles.miniLabel);
                            foreach (var z in publicDependents) {
                                EditorGUILayout.LabelField($"  <color=#AAAAAA>•</color> {z.name}", LabelRich);
                            }
                        }

                        if (nestedDependents.Count > 0) {
                            GUILayout.Space(2f);
                            EditorGUILayout.LabelField("Nested Zounds:", EditorStyles.miniLabel);
                            foreach (var z in nestedDependents) {
                                EditorGUILayout.LabelField($"  <color=#AAAAAA>•</color> {z.name}", LabelRich);
                            }
                        }
                    }
                    else {
                        EditorGUILayout.LabelField("Not directly referenced. Exported because it resides in the Library folder.", EditorStyles.centeredGreyMiniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            GUILayout.EndVertical();
        }

        // ── Shared drawing helpers ──────────────────────────────────────

        /// <summary>
        /// Draws a list of Zounds with overflow control. Shows MaxVisibleSubItems by default,
        /// with a "Show all N" button to expand.
        /// </summary>
        private void DrawCollapsibleZoundList(List<Zound> zounds, int ownerId, string directionLabel) {
            string key = $"{ownerId}_{directionLabel}";
            bool showAll = showAllKeys.Contains(key);
            int limit = showAll ? zounds.Count : Mathf.Min(zounds.Count, MaxVisibleSubItems);

            for (int i = 0; i < limit; i++) {
                GUILayout.BeginHorizontal();
                {
                    string typeTag = GetTypeLabel(zounds[i]);
                    EditorGUILayout.LabelField($"  [{typeTag}] ", EditorStyles.miniLabel, GUILayout.Width(45f));
                    DrawZoundLabel(zounds[i], EditorStyles.miniLabel);
                }
                GUILayout.EndHorizontal();
            }

            if (zounds.Count > MaxVisibleSubItems) {
                if (showAll) {
                    if (GUILayout.Button("Show less", EditorStyles.miniButton, GUILayout.Width(80f))) {
                        showAllKeys.Remove(key);
                    }
                }
                else {
                    if (GUILayout.Button($"Show all {zounds.Count}...", EditorStyles.miniButton, GUILayout.Width(100f))) {
                        showAllKeys.Add(key);
                    }
                }
            }
        }

        /// <summary>Same as DrawCollapsibleZoundList but keyed by a string rather than int.</summary>
        private void DrawCollapsibleZoundListByPath(List<Zound> zounds, string key) {
            bool showAll = showAllKeys.Contains(key);
            int limit = showAll ? zounds.Count : Mathf.Min(zounds.Count, MaxVisibleSubItems);

            for (int i = 0; i < limit; i++) {
                GUILayout.BeginHorizontal();
                {
                    string typeTag = GetTypeLabel(zounds[i]);
                    EditorGUILayout.LabelField($"  [{typeTag}] ", EditorStyles.miniLabel, GUILayout.Width(45f));
                    DrawZoundLabel(zounds[i], EditorStyles.miniLabel);
                }
                GUILayout.EndHorizontal();
            }

            if (zounds.Count > MaxVisibleSubItems) {
                if (showAll) {
                    if (GUILayout.Button("Show less", EditorStyles.miniButton, GUILayout.Width(80f))) {
                        showAllKeys.Remove(key);
                    }
                }
                else {
                    if (GUILayout.Button($"Show all {zounds.Count}...", EditorStyles.miniButton, GUILayout.Width(100f))) {
                        showAllKeys.Add(key);
                    }
                }
            }
        }

        private void DrawPreviewButton(Zound zound) {
            // Button removed, preview is now triggered by clicking labels
        }

        private void DrawClipPreviewButton(string clipPath) {
            // Button removed, preview is now triggered by clicking labels
        }

        private void DrawZoundLabel(Zound zound, GUIStyle style, params GUILayoutOption[] options) {
            string displayName = zound.name;
            if (zound.parentId != 0) {
                var parent = ZoundsProject.Instance.zoundLibrary.FindZound(z => z.id == zound.parentId);
                if (parent != null && displayName.StartsWith($"[{parent.name}]_")) {
                    displayName = displayName.Substring($"[{parent.name}]_".Length);
                }
            }
            
            var rect = GUILayoutUtility.GetRect(new GUIContent(displayName), style, options);
            if (GUI.Button(rect, displayName, style)) {
                ZoundEngine.PlayZound(zound);
            }
        }

        private void DrawZoundLabelRich(Zound zound, string label, GUIStyle style, params GUILayoutOption[] options) {
            string displayName = label;
            if (zound.parentId != 0) {
                var parent = ZoundsProject.Instance.zoundLibrary.FindZound(z => z.id == zound.parentId);
                if (parent != null) {
                    string prefix = $"[{parent.name}]_";
                    // Need to handle the rich text <b> tags in the label passed to this method
                    if (displayName.Contains(prefix)) {
                        displayName = displayName.Replace(prefix, "");
                    }
                }
            }

            var rect = GUILayoutUtility.GetRect(new GUIContent(displayName), style, options);
            if (GUI.Button(rect, displayName, style)) {
                ZoundEngine.PlayZound(zound);
            }
        }

        private void DrawClipLabel(string clipPath, string fileName, GUIStyle style, params GUILayoutOption[] options) {
            var rect = GUILayoutUtility.GetRect(new GUIContent(fileName), style, options);
            if (GUI.Button(rect, fileName, style)) {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) {
                    AudioPreviewUtility.PlayPreviewClip(clip);
                }
            }
        }

        private static string GetTypeLabel(Zound z) {
            if (z is Klip) return "Klip";
            if (z is Zequence) return "Zeq";
            if (z is Muzic) return "Muz";
            if (z is ClipZound) return "AC";
            return "?";
        }
    }
}
#endif
