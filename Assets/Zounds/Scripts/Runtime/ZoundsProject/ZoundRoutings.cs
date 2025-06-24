using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Zounds {

    [System.Serializable]
    public class ZoundRoutings {

        [System.Serializable]
        public class Condition {
            public enum ConditionType {
                Tag = 0,
#if ZOUNDS_CONSIDER_FOLDERS
                Folder = 1
#endif
            }
            public ConditionType type;
            public string name; // can be either folder name or tag name.
        }

        [System.Serializable]
        public class Rule {
            public List<Condition> conditions = new List<Condition>();
            public AudioMixerGroup mixerGroup;
        }

        public List<Rule> rules = new List<Rule>();

        public AudioMixerGroup GetRouting(Zound zound
#if ZOUNDS_CONSIDER_FOLDERS
            , AudioClip clip, string clipPath
#endif
            ) {
            AudioMixerGroup foundKeyTag = null; // this is a "weak" find, since tag with exact value is prefered.
            foreach (var set in rules) {
                foreach (var rule in set.conditions) {
#if ZOUNDS_CONSIDER_FOLDERS
                    if (ReferenceEquals(foundKeyTag, null) && rule.type == Condition.ConditionType.Folder) {
                        if (clip == null) continue;
                        if (!ZoundDictionary.runtimeClipFolders.TryGetValue(clip, out var clipFolder)) {
                            clipFolder = GetFolderFromClipPath(clipPath);
                            if (!string.IsNullOrEmpty(clipFolder)) {
                                ZoundDictionary.runtimeClipFolders.Add(clip, clipFolder);
                            }
                        }
                        if (clipFolder == rule.name) {
                            return set.mixerGroup;
                        }
                    }
                    else 
#endif
                    if (rule.type == Condition.ConditionType.Tag) {
                        var zoundLibrary = ZoundsProject.Instance.zoundLibrary;
                        foreach (var tagId in zound.tags) {
                            if (zoundLibrary.TryGetTag(tagId, out var tag)) {
                                if (tag.name == rule.name) {
                                    return set.mixerGroup;
                                }
                                else if (ReferenceEquals(foundKeyTag, null)) {
                                    var nameSplit = tag.name.Split(':');
                                    if (nameSplit.Length > 1) {
                                        if (nameSplit[0] == rule.name) {
                                            foundKeyTag = set.mixerGroup;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return foundKeyTag;
        }

        public static string GetFolderFromClipPath(string clipPath) {
            if (string.IsNullOrEmpty(clipPath)) return null;

            var projectSettings = ZoundsProject.Instance.projectSettings;
            string parentFolderPath = projectSettings.sourceFolderPath;
            if (!clipPath.StartsWith(parentFolderPath)) {
                parentFolderPath = projectSettings.workFolderPath;
                if (!clipPath.StartsWith(parentFolderPath)) {
                    parentFolderPath = projectSettings.userFolderPath;
                    if (!clipPath.StartsWith(parentFolderPath)) {
                        return null;
                    }
                }
            }
            string folderPath = System.IO.Path.GetDirectoryName(clipPath);
            folderPath = folderPath.Substring(parentFolderPath.Length, folderPath.Length - parentFolderPath.Length);
            folderPath = folderPath.Replace('\\', '/');
            return folderPath;
        }

    }

}
