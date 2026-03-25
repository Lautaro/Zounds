// ZUIWindow.cs
// Base EditorWindow class for ZUI-based windows.
// Wires up instant hover by enabling MouseMove events and repainting on them.
//
// Usage:
//   public class MyWindow : ZUIWindow
//   {
//       protected override void DrawGUI() { /* your OnGUI content */ }
//       protected override void OnWindowEnable() { /* optional OnEnable logic */ }
//   }

using UnityEditor;
using UnityEngine;

public abstract class ZUIWindow : EditorWindow
{
    void OnEnable()
    {
        wantsMouseMove = true;
        OnZUIEnable();
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.MouseMove)
            Repaint();
        OnZUI();
    }

    protected virtual void OnZUIEnable() { }
    protected virtual void OnZUI() { }
}
