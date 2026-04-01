// ZUITween.cs
// Opt-in hover-in / hover-out / click color animation for ZUI buttons and toggles.
//
// How it works:
//   - Each ZUIButtonDef can opt in via hoverAnimEnabled / clickAnimEnabled fields.
//   - DrawManualButton / DrawManualToggle call ZUITween.UpdateHover / UpdateClick
//     on every frame to drive the tween state.
//   - GetAnimatedFill(key, baseFill) returns the interpolated fill color for the
//     current frame. Pass this to DrawVisual instead of the raw state gradient.
//   - The tween dictionary is keyed by control ID so each button instance is
//     independent. EditorApplication.update drives repaint when tweens are active.
//
// Opt-in fields on ZUIButtonDef (added below this class):
//   bool  hoverAnimEnabled     — enable hover-in / hover-out color transition
//   float hoverInDuration      — seconds for hover-in  (default 0.12)
//   float hoverOutDuration     — seconds for hover-out (default 0.20)
//   bool  clickAnimEnabled     — enable on-click color flash
//   float clickDuration        — seconds for click flash (default 0.15)
//   Color hoverAnimFillColor   — target fill color on hover (blended on top of normal)
//   Color clickAnimFillColor   — target fill color on click
//
// Usage in DrawManualButton:
//   ZUITween.NotifyHover(id, isHover, def);
//   ZUITween.NotifyClick(id, clicked, def);
//   var animTint = ZUITween.GetTint(id);    // Color to multiply over the visual

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ===== ZUITween — per-control hover/click animation =======================

    /// <summary>
    /// Notifies the tween system of the current hover state for a control.
    /// Call every frame in the button draw loop before the repaint block.
    /// Does nothing if <paramref name="def"/> has hoverAnimEnabled = false.
    /// </summary>
    public static void TweenNotifyHover(int controlId, bool isHover, ZUIButtonDef def)
    {
        if (!def.hoverAnimEnabled) return;

        // Only update hover state on events where mouse position is meaningful.
        // Layout events fire with stale/wrong mouse positions, causing enter/exit oscillation
        // that resets hoverStart every frame and keeps hoverT at 0.
        var evType = Event.current.type;
        if (evType == EventType.Layout || evType == EventType.Used) return;

        // DEBUG — remove after confirming animation works
        if (isHover && evType == EventType.Repaint)
            Debug.Log($"[ZUITween] NotifyHover id={controlId} isHover={isHover} style={def.name} hoverT={(_tweens.TryGetValue(controlId, out var dbg) ? dbg.hoverT : -1f):F3}");

        if (!_tweens.TryGetValue(controlId, out var entry))
        {
            entry = new TweenEntry();
            _tweens[controlId] = entry;
        }

        bool wasHover = entry.isHover;
        entry.isHover = isHover;
        entry.def     = def;

        if (isHover && !wasHover)
        {
            // Hover enter
            entry.hoverT       = entry.hoverT; // continue from current position
            entry.hoverStart   = EditorApplication.timeSinceStartup - entry.hoverT * def.hoverInDuration;
            entry.hoverForward = true;
            EnsureAnimUpdateRunning();
        }
        else if (!isHover && wasHover)
        {
            // Hover exit
            entry.hoverStart   = EditorApplication.timeSinceStartup - (1f - entry.hoverT) * def.hoverOutDuration;
            entry.hoverForward = false;
            EnsureAnimUpdateRunning();
        }
    }

    /// <summary>
    /// Notifies the tween system that a click occurred.
    /// Does nothing if <paramref name="def"/> has clickAnimEnabled = false.
    /// </summary>
    public static void TweenNotifyClick(int controlId, ZUIButtonDef def)
    {
        if (!def.clickAnimEnabled) return;

        if (!_tweens.TryGetValue(controlId, out var entry))
        {
            entry = new TweenEntry();
            _tweens[controlId] = entry;
        }

        entry.def        = def;
        entry.clickT     = 1f;
        entry.clickStart = EditorApplication.timeSinceStartup;
        EnsureAnimUpdateRunning();
    }

    /// <summary>
    /// Returns a multiplicative color tint for the given control ID.
    /// White (1,1,1,1) means no animation. Blend this on top of the normal visual
    /// by multiplying GUI.color before drawing.
    /// </summary>
    public static Color TweenGetTint(int controlId)
    {
        if (!_tweens.TryGetValue(controlId, out var entry)) return Color.white;

        double now = EditorApplication.timeSinceStartup;

        // Advance hover tween
        if (entry.def != null && entry.def.hoverAnimEnabled)
        {
            float dur = entry.hoverForward ? entry.def.hoverInDuration : entry.def.hoverOutDuration;
            if (dur > 0f)
            {
                float elapsed = (float)(now - entry.hoverStart);
                float t = Mathf.Clamp01(elapsed / dur);
                entry.hoverT = entry.hoverForward ? t : (1f - t);
            }
        }

        // Advance click tween
        if (entry.def != null && entry.def.clickAnimEnabled && entry.clickT > 0f)
        {
            float dur = entry.def.clickDuration > 0f ? entry.def.clickDuration : 0.15f;
            float elapsed = (float)(now - entry.clickStart);
            entry.clickT = Mathf.Max(0f, 1f - elapsed / dur);
        }

        // Compose tint
        Color tint = Color.white;

        if (entry.def != null && entry.def.hoverAnimEnabled && entry.hoverT > 0f)
        {
            Color hc = entry.def.hoverAnimFillColor;
            tint = Color.Lerp(Color.white, hc, entry.hoverT);
        }

        if (entry.def != null && entry.def.clickAnimEnabled && entry.clickT > 0f)
        {
            Color cc = entry.def.clickAnimFillColor;
            Color clickTint = Color.Lerp(Color.white, cc, entry.clickT);
            // Click composites on top of hover
            tint = Color.Lerp(tint, clickTint, entry.clickT);
        }

        return tint;
    }

    /// <summary>
    /// Returns the current hover tween value (0=normal, 1=fully hovered) for a control.
    /// </summary>
    public static float TweenGetHoverT(int controlId)
    {
        if (!_tweens.TryGetValue(controlId, out var entry)) return 0f;
        if (entry.def == null || !entry.def.hoverAnimEnabled) return 0f;
        double now = EditorApplication.timeSinceStartup;
        float dur = entry.hoverForward ? entry.def.hoverInDuration : entry.def.hoverOutDuration;
        if (dur > 0f)
        {
            float elapsed = (float)(now - entry.hoverStart);
            float t = Mathf.Clamp01(elapsed / dur);
            entry.hoverT = entry.hoverForward ? t : (1f - t);
        }
        return entry.hoverT;
    }

    /// <summary>
    /// Returns the current click tween value (1=just clicked, fades to 0).
    /// </summary>
    public static float TweenGetClickT(int controlId)
    {
        if (!_tweens.TryGetValue(controlId, out var entry)) return 0f;
        if (entry.def == null || !entry.def.clickAnimEnabled) return 0f;
        // Advance click tween
        double now = EditorApplication.timeSinceStartup;
        float dur = entry.def.clickDuration > 0f ? entry.def.clickDuration : 0.15f;
        float elapsed = (float)(now - entry.clickStart);
        entry.clickT = Mathf.Max(0f, 1f - elapsed / dur);
        return entry.clickT;
    }

    // ===== Internals ==========================================================

    private class TweenEntry
    {
        public ZUIButtonDef def;
        public bool   isHover;
        public bool   hoverForward;
        public float  hoverT;         // 0=normal, 1=fully hovered
        public double hoverStart;
        public float  clickT;         // 1=just clicked, fades to 0
        public double clickStart;
    }

    private static readonly Dictionary<int, TweenEntry> _tweens = new Dictionary<int, TweenEntry>();
}
