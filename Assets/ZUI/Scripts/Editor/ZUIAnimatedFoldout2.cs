// ZUIAnimatedFoldout2.cs
// Experimental animated foldout — "measure first, animate second" approach.
//
// State machine:
//   Closed    → (open requested) → Measuring → Opening → Open
//   Open      → (close requested) → Closing  → Closed
//
// Measuring: draws content at full size for one Repaint frame (invisible,
//   alpha=0) to capture the natural height. Space is reserved instantly.
//
// Opening: the reserved height animates from 0 → contentHeight. Content is
//   drawn at full alpha but only visible within the growing reserved space
//   (no clipping — content simply draws at whatever height is reserved and
//   gets cut off by the layout). A fade is applied on top.
//
// Closing: reverse of opening. Height animates contentHeight → 0.

using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    public enum FoldoutState
    {
        Closed,
        Measuring,   // one-frame: draw invisible at full size, capture height
        Opening,     // animating height from 0 to contentHeight
        Open,
        Closing,     // animating height from contentHeight to 0
    }

    public class AnimatedFoldout2
    {
        public readonly string key;
        public float speed;
        public float contentHeight;
        public FoldoutState state = FoldoutState.Closed;

        // Track the requested open state so we only transition on changes
        private bool _requestedOpen;
        private bool _firstFrame = true;

        public AnimatedFoldout2(string key, float speed = 10f)
        {
            this.key = key;
            this.speed = speed;
        }

        /// <summary>Returns the current 0-1 animation value.</summary>
        public float AnimT
        {
            get
            {
                var af = GetOrCreateAnimFloat(key, 0f);
                return af.value;
            }
        }

        public FoldoutScope2 Begin(bool open)
        {
            // Detect open/close transitions
            if (_firstFrame)
            {
                _requestedOpen = open;
                _firstFrame = false;
                if (open) state = FoldoutState.Open;
            }
            else if (open != _requestedOpen)
            {
                _requestedOpen = open;
                if (open)
                {
                    state = FoldoutState.Measuring;
                }
                else
                {
                    if (contentHeight > 1f)
                    {
                        state = FoldoutState.Closing;
                        var af = GetOrCreateAnimFloat(key, 1f);
                        af.SnapTo(1f);
                        af.SetTarget(0f, speed);
                    }
                    else
                    {
                        state = FoldoutState.Closed;
                    }
                }
            }

            // Advance state machine based on animation progress
            if (state == FoldoutState.Opening || state == FoldoutState.Closing)
            {
                var af = GetOrCreateAnimFloat(key, 0f);
                if (state == FoldoutState.Opening && af.value >= 0.999f)
                    state = FoldoutState.Open;
                else if (state == FoldoutState.Closing && af.value <= 0.001f)
                    state = FoldoutState.Closed;
            }

            return new FoldoutScope2(this);
        }
    }

    public struct FoldoutScope2 : System.IDisposable
    {
        /// <summary>True when caller should draw content.</summary>
        public readonly bool visible;

        private readonly AnimatedFoldout2 _state;
        private readonly bool _measuring;
        private readonly Color _prevGuiColor;
        private readonly bool _modifiedColor;

        public FoldoutScope2(AnimatedFoldout2 state)
        {
            _state = state;
            _prevGuiColor = GUI.color;
            _modifiedColor = false;
            _measuring = false;
            visible = false;

            switch (state.state)
            {
                case FoldoutState.Closed:
                    visible = false;
                    break;

                case FoldoutState.Measuring:
                    // Draw at full size but invisible (alpha=0).
                    // This reserves the full space and lets us measure on Repaint.
                    visible = true;
                    _measuring = true;
                    _modifiedColor = true;
                    GUI.color = new Color(_prevGuiColor.r, _prevGuiColor.g, _prevGuiColor.b, 0f);
                    EditorGUILayout.BeginVertical();
                    break;

                case FoldoutState.Opening:
                case FoldoutState.Closing:
                {
                    visible = true;

                    var af = GetOrCreateAnimFloat(state.key, 0f);
                    float t = af.value;
                    float fullH = state.contentHeight;
                    float animatedH = fullH * t;

                    // Reserve the animated height — this is what creates
                    // the growing/shrinking effect in the layout.
                    // Content is drawn inside via BeginVertical with MaxHeight
                    // so it's constrained to the animated size.
                    _modifiedColor = true;
                    float fadeT = Mathf.Clamp01(t / 0.5f);
                    float alpha = fadeT * fadeT * (3f - 2f * fadeT);
                    GUI.color = new Color(_prevGuiColor.r, _prevGuiColor.g, _prevGuiColor.b,
                                          _prevGuiColor.a * alpha);

                    EditorGUILayout.BeginVertical(GUILayout.Height(animatedH));
                    break;
                }

                case FoldoutState.Open:
                    visible = true;
                    break;
            }
        }

        public void Dispose()
        {
            switch (_state.state)
            {
                case FoldoutState.Measuring:
                    EditorGUILayout.EndVertical();
                    if (Event.current.type == EventType.Repaint)
                    {
                        var r = GUILayoutUtility.GetLastRect();
                        if (r.height > 1f)
                        {
                            _state.contentHeight = r.height;
                            var af = GetOrCreateAnimFloat(_state.key, 0f);
                            af.SnapTo(0f);
                            af.SetTarget(1f, _state.speed);
                            _state.state = FoldoutState.Opening;
                        }
                    }
                    break;

                case FoldoutState.Opening:
                case FoldoutState.Closing:
                    EditorGUILayout.EndVertical();
                    break;

                case FoldoutState.Open:
                    if (Event.current.type == EventType.Repaint)
                    {
                        var r = GUILayoutUtility.GetLastRect();
                        if (r.height > 1f)
                            _state.contentHeight = r.height;
                    }
                    break;
            }

            if (_modifiedColor)
                GUI.color = _prevGuiColor;
        }
    }
}
