// ZUIDebug.cs
// Right-click any ZUI control while StyleDebugMode is on to inspect its style info.
// Toggle via the menu: ZUI / Toggle Style Debug Mode
//
// How it works:
//   ContextClick frame: every ZUI control checks rect.Contains(mousePosition) directly.
//   ALL hit controls call RegisterCandidate — the event is NOT consumed, so every control
//   in the frame sees the same live ContextClick with correct local-space coordinates.
//   RegisterCandidate queues a delayCall Repaint on the first hit.
//   On the next Repaint: TryShowPendingMenu (called at the top of each window's
//   OnGUI) picks the winner — highest priority (Button > Label > Box), then
//   smallest area — and shows the GenericMenu.
//   TryShowPendingMenu must be called from each window's OnGUI, not from individual
//   draw helpers, to avoid premature firing on intermediate Repaints.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ── Public toggle ─────────────────────────────────────────────────────────

    public static bool StyleDebugMode { get; private set; }

    [MenuItem("ZUI/Toggle Style Debug Mode")]
    static void ToggleStyleDebugMode()
    {
        StyleDebugMode = !StyleDebugMode;
        Menu.SetChecked("ZUI/Toggle Style Debug Mode", StyleDebugMode);
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            w.Repaint();
    }

    // ── Internal access (used by ZUIStyleDef.cs) ──────────────────────────────

    internal static ZUIStyle _pendingBoxStyle    = ZUIStyle.Default;
    internal static bool     _pendingBoxStyleSet;

    // ── Candidate system ──────────────────────────────────────────────────────

    enum DebugHitPriority { Box = 0, Label = 1, Button = 2 }

    internal struct DebugEntry { public string label; public string value; }

    struct DebugCandidate
    {
        public DebugHitPriority priority;
        public float            area;
        public List<DebugEntry> entries;
    }

    static List<DebugCandidate> _candidates;
    static bool                 _flushQueued;

    // Register a candidate. On first hit per frame: queues a repaint.
    // The ContextClick event is never consumed — every control sees it with correct local coords.
    static void RegisterCandidate(DebugHitPriority priority, Rect rect, List<DebugEntry> entries)
    {
        if (!_flushQueued)
        {
            _flushQueued = true;
            EditorApplication.delayCall += () =>
            {
                foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    w.Repaint();
            };
        }

        if (_candidates == null) _candidates = new List<DebugCandidate>();
        _candidates.Add(new DebugCandidate
        {
            priority = priority,
            area     = rect.width * rect.height,
            entries  = entries,
        });
    }

    // Called at the top of each ZUI draw method every frame.
    // Shows the menu on the Repaint that follows the click frame.
    public static void TryShowPendingMenu()
    {
        if (!_flushQueued) return;
        if (Event.current.type != EventType.Repaint) return;

        _flushQueued = false;
        var candidates = _candidates;
        _candidates    = null;

        if (candidates == null || candidates.Count == 0) return;

        // Pick best: highest priority, then smallest area.
        var winner = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.priority > winner.priority) { winner = c; continue; }
            if (c.priority == winner.priority && c.area < winner.area) winner = c;
        }

        var entries = winner.entries;
        if (entries == null || entries.Count == 0) return;

        var menu = new GenericMenu();
        menu.AddDisabledItem(new GUIContent("── ZUI Style Inspector ──"));
        menu.AddSeparator("");
        foreach (var e in entries)
        {
            var captured = e;
            menu.AddItem(new GUIContent($"{captured.label}:  {captured.value}   [copy]"),
                false, () => EditorGUIUtility.systemCopyBuffer = captured.value);
        }
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Copy All"), false, () =>
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in entries) sb.AppendLine($"{e.label}: {e.value}");
            EditorGUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
        });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Exit Style Debug Mode"), false, () =>
        {
            StyleDebugMode = false;
            Menu.SetChecked("ZUI/Toggle Style Debug Mode", false);
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                w.Repaint();
        });
        menu.ShowAsContext();
    }

    // ── Check helpers ─────────────────────────────────────────────────────────
    // Returns true if a ContextClick landed inside rect this frame.

    static bool IsDebugHit(Rect rect)
    {
        if (!StyleDebugMode) return false;
        if (Event.current.type != EventType.ContextClick) return false;
        return rect.Contains(Event.current.mousePosition);
    }

    internal static bool CheckDebugBoxClick(Rect rect)     => IsDebugHit(rect);
    internal static bool CheckDebugContextClick(Rect rect) => IsDebugHit(rect);

    // ── Record helpers called from Collect* ───────────────────────────────────

    static void RecordBoxHit(Rect rect, List<DebugEntry> entries)
        => RegisterCandidate(DebugHitPriority.Box, rect, entries);

    static void RecordButtonHit(Rect rect, List<DebugEntry> entries)
        => RegisterCandidate(DebugHitPriority.Button, rect, entries);

    static void RecordLabelHit(Rect rect, List<DebugEntry> entries)
        => RegisterCandidate(DebugHitPriority.Label, rect, entries);

    // ── Entry helpers ─────────────────────────────────────────────────────────

    static List<DebugEntry> MakeEntries() => new List<DebugEntry>();

    static void Add(List<DebugEntry> list, string label, string value)
        => list.Add(new DebugEntry { label = label, value = value });

    // ── Collect helpers (called from draw code, pass rect for area comparison) ─

    internal static void CollectButtonDebugInfo(ZUIButtonDef def, ZButtonStyle style, Rect rect)
    {
        var e = MakeEntries();
        Add(e, "ButtonStyle", style.ToString());
        Add(e, "Def name",    def.name);
        if (!string.IsNullOrEmpty(def.normal?.colorARef))
            Add(e, "BG palette",   FormatPaletteRef(def.normal.colorARef, def.normal.colorASlot));
        if (!string.IsNullOrEmpty(def.normal?.colorBRef))
            Add(e, "BG palette B", FormatPaletteRef(def.normal.colorBRef, def.normal.colorBSlot));
        if (!string.IsNullOrEmpty(def.text?.colorRef))
            Add(e, "Text palette", FormatPaletteRef(def.text.colorRef, def.text.colorSlot));
        RecordButtonHit(rect, e);
    }

    internal static void CollectBoxDebugInfo(ZUIBoxDef def, ZUIStyle style, Rect rect)
    {
        var e = MakeEntries();
        Add(e, "BoxStyle", style.ToString());
        Add(e, "Def name", def.name);
        var bg = def.GetResolvedBackground();
        if (!string.IsNullOrEmpty(bg?.colorARef))
            Add(e, "BG palette",           FormatPaletteRef(bg.colorARef, bg.colorASlot));
        if (!string.IsNullOrEmpty(bg?.colorBRef))
            Add(e, "BG palette B",         FormatPaletteRef(bg.colorBRef, bg.colorBSlot));
        if (!string.IsNullOrEmpty(def.borderColorRef))
            Add(e, "Border palette",       FormatPaletteRef(def.borderColorRef, def.borderColorSlot));
        if (!string.IsNullOrEmpty(def.contentText?.colorRef))
            Add(e, "Content text palette", FormatPaletteRef(def.contentText.colorRef, def.contentText.colorSlot));
        if (!string.IsNullOrEmpty(def.titleText?.colorRef))
            Add(e, "Title text palette",   FormatPaletteRef(def.titleText.colorRef, def.titleText.colorSlot));
        RecordBoxHit(rect, e);
    }

    internal static void CollectTextDebugInfo(ZUITextStyleDef def, ZTextStyle style, Rect rect)
    {
        var e = MakeEntries();
        Add(e, "TextStyle", style.ToString());
        if (def != null)
        {
            Add(e, "Def name", def.name);
            if (!string.IsNullOrEmpty(def.text?.colorRef))
                Add(e, "Text palette", FormatPaletteRef(def.text.colorRef, def.text.colorSlot));
            if (def.text != null && def.text.shadowEnabled && !string.IsNullOrEmpty(def.text.shadowColorRef))
                Add(e, "Shadow palette", FormatPaletteRef(def.text.shadowColorRef, def.text.shadowColorSlot));
        }
        RecordLabelHit(rect, e);
    }

    internal static void CollectTextDebugInfo(ZUITextStyleDef styleDef, ZTextStyle style, ZUITextDef fallbackTextDef, Rect rect)
    {
        if (styleDef != null) { CollectTextDebugInfo(styleDef, style, rect); return; }
        var e = MakeEntries();
        Add(e, "TextStyle", style.ToString());
        Add(e, "Def name",  "(no sheet def — fallback)");
        if (!string.IsNullOrEmpty(fallbackTextDef?.colorRef))
            Add(e, "Text palette", FormatPaletteRef(fallbackTextDef.colorRef, fallbackTextDef.colorSlot));
        RecordLabelHit(rect, e);
    }

    internal static void CollectTextDebugInfo(ZUITextDef textDef, ZTextStyle style, Rect rect)
    {
        var e = MakeEntries();
        Add(e, "TextStyle", style.ToString());
        if (textDef != null && !string.IsNullOrEmpty(textDef.colorRef))
            Add(e, "Text palette", FormatPaletteRef(textDef.colorRef, textDef.colorSlot));
        RecordLabelHit(rect, e);
    }

    static string FormatPaletteRef(string colorRef, ZUIPaletteSlot slot)
        => slot == ZUIPaletteSlot.Primary ? colorRef : $"{colorRef} ({slot})";
}
