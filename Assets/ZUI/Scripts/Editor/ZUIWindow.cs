// ZUIWindow.cs
// Base EditorWindow class for ZUI-based windows.
// Wires up instant hover by enabling MouseMove events and repainting on them.
//
// When UseEditorSheet is true (default for ZUI's own windows), the window
// temporarily swaps ActiveSheet to ZUI.EditorSheet during OnGUI, so ZUI's
// editor windows are styled independently of the consumer's sheet.
//
// Consumer windows (e.g., Zounds) should override UseEditorSheet → false
// so they render with the consumer's ActiveSheet.

using UnityEditor;
using UnityEngine;

public abstract class ZUIWindow : EditorWindow
{
    /// <summary>
    /// When true, this window uses ZUI.EditorSheet for rendering.
    /// Override to false in consumer windows that should use the consumer's sheet.
    /// </summary>
    protected virtual bool UseEditorSheet => true;

    void OnEnable()
    {
        wantsMouseMove = true;
        OnZUIEnable();
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.MouseMove)
            Repaint();

        ZUIStyleSheetAsset savedSheet = null;
        if (UseEditorSheet && ZUI.EditorSheet != null)
        {
            savedSheet = ZUI.ActiveSheet;
            ZUI.ActiveSheet = ZUI.EditorSheet;
        }

        ZUI.TryShowPendingMenu();
        OnZUI();

        if (savedSheet != null)
            ZUI.ActiveSheet = savedSheet;
    }

    protected virtual void OnZUIEnable() { }
    protected virtual void OnZUI() { }
}
