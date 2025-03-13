using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    [InitializeOnLoad]
    static class ZoundsProjectInitialization {
        static ZoundsProjectInitialization() {
#if !ADDRESSABLES_INSTALLED
            Debug.LogError("Zounds Dependency: Addressables package should be installed.");
#endif
        }
    }

    [CustomEditor(typeof(ZoundsProject))]
    public class ZoundsProjectEditor : Editor {

        public override void OnInspectorGUI() {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("Open Zounds Window")) {
                    ZoundsWindow.OpenWindow();
                }
                GUILayout.Space(10f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            // For debuggin purpose.
            // Might need to hide this for production.
            //bool prevGUIEnabled = GUI.enabled;
            //GUI.enabled = false;
            base.OnInspectorGUI();
            //GUI.enabled = prevGUIEnabled;
        }

    }

}