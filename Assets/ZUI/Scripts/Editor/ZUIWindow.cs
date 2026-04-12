// ZUIWindow.cs
// Base EditorWindow class for ZUI-based windows.
// Wires up: instant hover, sheet scoping, root box container.
//
// Consumer windows override ConsumerSheetName to use their registered sheet.
// ZUI's own editor windows (Style Editor) handle sheet selection internally.

using UnityEditor;
using UnityEngine;

public abstract class ZUIWindow : EditorWindow
{

    /// <summary>
    /// Override to return a registered consumer sheet name (e.g. "Zounds", "Showcase").
    /// The base class scopes ZUI.ActiveSheet to this sheet for the duration of OnGUI.
    /// Return null to use whatever ZUI.ActiveSheet is currently set to.
    /// </summary>
    protected virtual string ConsumerSheetName => null;

    /// <summary>
    /// The box style name used for the root container that wraps the window content.
    /// Override to change the root box appearance. Set to null to disable the root box.
    /// </summary>
    protected virtual string RootBoxStyle => "Default";

    // Tracks the consumer sheet's version so we detect external modifications and repaint.
    [System.NonSerialized] private int _lastSheetVersion = -1;

    void OnEnable()
    {
        wantsMouseMove = true;
        OnZUIEnable();
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.MouseMove)
            Repaint();

        // Scope the active sheet for this window
        ZUI.SheetScope scope = default;
        bool hasScope = false;
        ZUIStyleSheetAsset consumerSheet = null;

        string consumer = ConsumerSheetName;
        if (!string.IsNullOrEmpty(consumer))
        {
            consumerSheet = ZUI.GetConsumerSheet(consumer);
            if (consumerSheet != null) { scope = ZUI.UseSheet(consumerSheet); hasScope = true; }
        }

        // Detect external sheet modifications and repaint with fresh caches.
        // BumpVersion() already invalidated all defs — we just need to notice the change
        // and schedule a repaint so the window redraws with the new values.
        if (consumerSheet != null && consumerSheet.Version != _lastSheetVersion)
        {
            _lastSheetVersion = consumerSheet.Version;
            Repaint();
        }

        ZUI.TryShowPendingMenu();

        // Root box container
        string rootStyle = RootBoxStyle;
        if (!string.IsNullOrEmpty(rootStyle))
        {
            using (ZUI.BoxNamed(rootStyle))
            {
                OnZUI();
            }
        }
        else
        {
            OnZUI();
        }

        if (hasScope) scope.Dispose();
    }

    protected virtual void OnZUIEnable() { }
    protected virtual void OnZUI() { }
}
