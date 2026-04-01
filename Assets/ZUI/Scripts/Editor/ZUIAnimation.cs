// ZUIAnimation.cs
// Shared animation driver for all ZUI animation systems (Pulse, Tween, Flash, AnimatedFloat).
//
// Each subsystem (ZUIPulse, ZUITween, flash) registers itself via MarkAnimationActive().
// A single EditorApplication.update callback drives repaint for all of them. The loop
// stops automatically when no animations are active, restarting on demand.
//
// ZUI.AnimatedFloat: a float animator that replaces UnityEditor.AnimFloat.
//   - Create/get:  ZUI.GetOrCreateAnimFloat(key, initial)
//   - Animate:     af.SetTarget(value)   — exponential ease-out toward target
//   - Snap:        af.SnapTo(value)      — instant, no animation
//   - Read:        af.value
//   - Destroy:     ZUI.RemoveAnimFloat(key)
//
// The repaint-all-windows pattern is intentional for editor-only code.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ===== Shared update loop =================================================

    private static bool _animUpdateRunning = false;

    /// <summary>
    /// Called by any animation subsystem when it becomes active.
    /// Starts the shared EditorApplication.update loop if not already running.
    /// </summary>
    internal static void EnsureAnimUpdateRunning()
    {
        if (_animUpdateRunning) return;
        _animUpdateRunning = true;
        EditorApplication.update += OnAnimUpdate;
    }

    private static void OnAnimUpdate()
    {
        double now = EditorApplication.timeSinceStartup;

        // --- Pulses ---
        if (_pulses.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var kv in _pulses)
                if (now >= kv.Value.endTime) toRemove.Add(kv.Key);
            foreach (var k in toRemove) _pulses.Remove(k);
        }

        // --- AnimatedFloats (named + standalone) ---
        bool anyFloatMoving = false;
        anyFloatMoving |= AdvanceAnimFloats(_animFloats.Values, now);
        anyFloatMoving |= AdvanceAnimFloats(_standaloneFloats, now);

        // --- Tweens ---
        bool anyTweenActive = false;
        foreach (var kv in _tweens)
        {
            var e = kv.Value;
            if (e.def == null) continue;
            bool hoverActive = e.def.hoverAnimEnabled &&
                               ((e.hoverForward && e.hoverT < 1f) || (!e.hoverForward && e.hoverT > 0f));
            bool clickActive = e.def.clickAnimEnabled &&
                               ((e.clickForward && e.clickT < 1f) || (!e.clickForward && e.clickT > 0f));
            if (hoverActive || clickActive) { anyTweenActive = true; break; }
        }

        // --- Flash ---
        bool flashActive = (!string.IsNullOrEmpty(_flashStyleName) && now <= _flashEndTime)
                         || (_spaceFlashActive && now <= _spaceFlashEndTime);

        bool anyActive = _pulses.Count > 0 || anyFloatMoving || anyTweenActive || flashActive;

        if (!anyActive)
        {
            _animUpdateRunning = false;
            EditorApplication.update -= OnAnimUpdate;
            return;
        }

        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            w.Repaint();
    }

    // Advances all AnimatedFloats in a collection. Returns true if any are still moving.
    private static bool AdvanceAnimFloats(IEnumerable<AnimatedFloat> floats, double now)
    {
        bool anyMoving = false;
        foreach (var af in floats)
        {
            if (Mathf.Approximately(af.value, af.target)) continue;
            float dt = (float)(now - af.lastUpdateTime);
            dt = Mathf.Min(dt, 0.1f);
            float t = 1f - Mathf.Exp(-af.speed * dt);
            af.value = Mathf.Lerp(af.value, af.target, t);
            if (Mathf.Abs(af.value - af.target) < 0.001f) af.value = af.target;
            af.lastUpdateTime = now;
            anyMoving = true;
        }
        return anyMoving;
    }

    // ===== AnimatedFloat ======================================================

    /// <summary>
    /// A named, smoothly-animated float value. Replaces UnityEditor.AnimFloat
    /// for inspector panel open/close animations without requiring UnityEvent wiring.
    /// </summary>
    public class AnimatedFloat
    {
        public float  value;
        public float  target;
        /// <summary>Speed as a fraction of remaining distance per second (exponential ease-out). Default 4.</summary>
        public float  speed = 4f;
        internal double lastUpdateTime;

        public AnimatedFloat(float initial)
        {
            value = initial;
            target = initial;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            // Auto-register so the shared update loop can advance standalone floats.
            // Floats created via GetOrCreateAnimFloat are tracked in _animFloats instead.
            _standaloneFloats.Add(this);
        }

        /// <summary>Sets the target and starts animation. Returns this for chaining.</summary>
        public AnimatedFloat SetTarget(float newTarget, float newSpeed = -1f)
        {
            target = newTarget;
            if (newSpeed >= 0f) speed = newSpeed;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EnsureAnimUpdateRunning();
            return this;
        }

        /// <summary>Snaps to target immediately without animation.</summary>
        public AnimatedFloat SnapTo(float v) { value = v; target = v; return this; }

        /// <summary>Returns 0-1 animation progress (value / target). Safe when target is 0.</summary>
        public float progress => target > 0.001f ? Mathf.Clamp01(value / target) : (value < 0.001f ? 0f : 1f);
    }

    // Standalone AnimatedFloat instances (created via new rather than GetOrCreateAnimFloat).
    private static readonly List<AnimatedFloat> _standaloneFloats = new List<AnimatedFloat>();

    private static readonly Dictionary<string, AnimatedFloat> _animFloats = new Dictionary<string, AnimatedFloat>();

    /// <summary>
    /// Returns the AnimatedFloat for this key, creating it at <paramref name="initial"/> if it doesn't exist.
    /// </summary>
    public static AnimatedFloat GetOrCreateAnimFloat(string key, float initial = 0f)
    {
        if (!_animFloats.TryGetValue(key, out var af))
        {
            af = new AnimatedFloat(initial);
            _standaloneFloats.Remove(af); // keyed floats are tracked in _animFloats, not _standaloneFloats
            _animFloats[key] = af;
        }
        return af;
    }

    /// <summary>Removes the AnimatedFloat for this key.</summary>
    public static void RemoveAnimFloat(string key) => _animFloats.Remove(key);

}
