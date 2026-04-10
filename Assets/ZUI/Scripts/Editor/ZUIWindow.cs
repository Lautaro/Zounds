// ZUIWindow.cs
// Base EditorWindow class for ZUI-based windows.
// Wires up instant hover by enabling MouseMove events and repainting on them.
//
// Sheet selection:
//   - Override ConsumerSheetName to return a registered consumer name (e.g. "Zounds")
//   - Override UseEditorSheet => true for ZUI's own editor windows
//   - Default: uses whatever ZUI.ActiveSheet is set to

using UnityEditor;
using UnityEngine;

public abstract class ZUIWindow : EditorWindow
{
    /// <summary>
    /// When true, this window uses ZUI.EditorSheet for rendering.
    /// Override to false in consumer windows that should use the consumer's sheet.
    /// </summary>
    protected virtual bool UseEditorSheet => true;

    /// <summary>
    /// Override to return a registered consumer sheet name (e.g. "Zounds").
    /// When set, UseEditorSheet is ignored and this sheet is used instead.
    /// </summary>
    protected virtual string ConsumerSheetName => null;

    void OnEnable()
    {
        wantsMouseMove = true;
        OnZUIEnable();
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.MouseMove)
            Repaint();

        // Determine which sheet to use for this window
        ZUI.SheetScope scope = default;
        bool hasScope = false;

        string consumer = ConsumerSheetName;
        if (!string.IsNullOrEmpty(consumer))
        {
            var sheet = ZUI.GetConsumerSheet(consumer);
            if (sheet != null) { scope = ZUI.UseSheet(sheet); hasScope = true; }
        }
        else if (UseEditorSheet && ZUI.EditorSheet != null)
        {
            scope = ZUI.UseSheet(ZUI.EditorSheet);
            hasScope = true;
        }

        ZUI.TryShowPendingMenu();
        OnZUI();
        if (hasScope) scope.Dispose();
    }

    protected virtual void OnZUIEnable() { }
    protected virtual void OnZUI() { }
}
