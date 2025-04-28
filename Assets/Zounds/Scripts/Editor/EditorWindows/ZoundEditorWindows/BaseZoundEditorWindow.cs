using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zounds {

    public class BaseZoundEditorWindow<TZound> : EditorWindow where TZound : Zound {

        private static readonly Dictionary<int, BaseZoundEditorWindow<TZound>> allWindows = new Dictionary<int, BaseZoundEditorWindow<TZound>>();

        [SerializeField] protected int targetZoundID;

        protected TZound targetZound;

        protected static TWindow OpenWindow<TWindow>(TZound zound, Vector2 minSize) where TWindow : BaseZoundEditorWindow<TZound> {
            if (!allWindows.TryGetValue(zound.id, out var window)) {
                window = CreateInstance<TWindow>();
                window.targetZoundID = zound.id;
                window.minSize = minSize;
                window.Init();
                window.Show();
            }
            else {
                if (window.docked) {
                    window.ShowTab();
                }
                else {
                    window.Focus();
                }
            }
            return (TWindow)window;
        }

        protected virtual TZound FindZoundTarget() {
            return null;
        }

        protected virtual void OnEnable() {
            // ensure init here too to re-register window after recompilation.
            Init();
        }

        private void Init() {
            if (targetZoundID == 0) return;

            var library = ZoundsProject.Instance.zoundLibrary;
            targetZound = FindZoundTarget();
            titleContent.text = typeof(TZound).Name + ": " + (targetZound == null ? "(Invalid)" : targetZound.name);

            if (allWindows.ContainsKey(targetZoundID) && allWindows[targetZoundID] != this) {
                allWindows[targetZoundID] = this;
            }
            else {
                allWindows.Add(targetZoundID, this);
            }

            OnInit();
        }

        protected virtual void OnInit() {

        }

        protected virtual void OnDestroy() {
            if (allWindows.ContainsKey(targetZoundID)) {
                allWindows.Remove(targetZoundID);
            }
        }

        private void OnGUI() {
            if (targetZoundID == 0) {
                Close(); return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, position.width - 20f, position.height - 20f));
            bool remove = OnDrawGUI();
            GUILayout.EndArea();

            if (remove) {
                var zoundsProject = ZoundsProject.Instance;
                Undo.RecordObject(zoundsProject, "remove zound");
                AudioAssetUtility.RemoveZound(targetZound);
                EditorUtility.SetDirty(zoundsProject);
                Close(); return;
            }

            var evt = Event.current;
            if (evt.type == EventType.ValidateCommand) {
                if (evt.commandName == "UndoRedoPerformed") {
                    OnUndoRedoPerformed();
                    // repaint immediately when user undo/redo to make experience feels more fluid
                    Repaint();
                }
            }
        }

        /// <summary>
        /// Draw the GUI of the window
        /// </summary>
        /// <returns>Returns true if this zound needs to be removed.</returns>
        protected virtual bool OnDrawGUI() {
            return false;
        }

        protected virtual void OnUndoRedoPerformed() {

        }

    }

}
