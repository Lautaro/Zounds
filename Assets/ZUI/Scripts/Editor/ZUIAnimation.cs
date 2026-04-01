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
            bool clickActive = e.def.clickAnimEnabled && e.clickT > 0f;
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

    // ===== AnimatedFoldout ====================================================

    /// <summary>
    /// Draws an animated foldout header button. Returns the current 0-1 animation value.
    /// The key must be stable (e.g. "KlipEditor_preview"). Speed defaults to 8 (snappy).
    /// </summary>
    public static float FoldoutHeader(string key, ref bool open, string label,
                                      string buttonStyle = Style.Subtle, float speed = 8f)
    {
        if (ZUI.Button(label + (open ? "  ▲" : "  ▼"), buttonStyle, GUILayout.ExpandWidth(true)))
            open = !open;

        var af = GetOrCreateAnimFloat(key, open ? 1f : 0f);
        float wantedTarget = open ? 1f : 0f;
        if (!Mathf.Approximately(af.target, wantedTarget))
            af.SetTarget(wantedTarget, speed);

        return af.value;   // caller multiplies this 0-1 by their cached content height
    }

    // ===== Animated Foldout Scope =============================================

    /// <summary>
    /// State for an animated foldout section. Tracks the animation float and the
    /// measured content height across frames.
    ///
    /// Uses grow+fade: content height animates from 0 to full, and fields fade in
    /// during the last 20% of the animation. This matches the inspector fold-out style.
    ///
    /// Usage:
    ///   // Field:
    ///   private ZUI.AnimatedFoldoutState _foldout = new ZUI.AnimatedFoldoutState("MyKey");
    ///
    ///   // In OnGUI:
    ///   if (_foldout.Begin(isOpen)) {
    ///       DrawContent();   // normal EditorGUILayout calls work here
    ///   }
    ///   _foldout.End();      // ALWAYS call, even if Begin returned false
    /// </summary>
    public class AnimatedFoldoutState
    {
        public string key;
        public float speed;
        public float contentHeight;     // measured on Repaint

        // Transient per-frame state set by Begin(), read by End().
        private bool _drawing;
        private Color _prevColor;

        public AnimatedFoldoutState(string key, float speed = 10f)
        {
            this.key = key;
            this.speed = speed;
        }

        /// <summary>
        /// Call at the start of the foldout section. Returns true if content should be drawn.
        /// End() must always be called afterwards regardless of the return value.
        /// </summary>
        public bool Begin(bool open)
        {
            var af = GetOrCreateAnimFloat(key, open ? 1f : 0f);
            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(af.target, target))
                af.SetTarget(target, speed);

            float t = af.value;
            _drawing = t > 0.001f;

            if (!_drawing) return false;

            // Compute visible height: animate from 0 to full content height.
            // First frame uses a large value so the content can lay out and be measured.
            float visibleH = contentHeight > 0f ? contentHeight * t : 9999f;

            // Grow: constrain the vertical group to the animated height.
            EditorGUILayout.BeginVertical(GUILayout.Height(visibleH));

            // Fade: fields fade in during the last 20% of the animation (matches inspector style).
            float progress = af.progress;
            float alpha = Mathf.Clamp01((progress - 0.8f) / 0.2f);
            _prevColor = GUI.color;
            if (alpha < 1f)
                GUI.color = new Color(_prevColor.r, _prevColor.g, _prevColor.b, _prevColor.a * alpha);

            return true;
        }

        /// <summary>
        /// Must always be called after Begin(), even when Begin() returned false.
        /// Handles height measurement and closing the vertical group.
        /// </summary>
        public void End()
        {
            if (!_drawing) return;

            GUI.color = _prevColor;

            // Capture the natural content height on Repaint for next frame.
            if (Event.current.type == EventType.Repaint)
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                if (lastRect.yMax > 1f)
                    contentHeight = lastRect.yMax;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
