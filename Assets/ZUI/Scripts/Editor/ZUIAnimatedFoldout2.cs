// ZUIAnimatedFoldout2.cs
// Experimental animated foldout — "measure first, animate second" approach.
//
// State machine:
//   Closed    → (open requested) → Measuring → Opening → Open
//   Open      → (close requested) → Closing  → Closed
//
// Measuring: draws content at full size for ONE Repaint frame (invisible,
//   alpha=0) to capture the natural height.
//
// Opening/Closing: content is drawn at full size using normal EditorGUILayout
//   (so IMGUI controls render correctly). After drawing, the excess space
//   beyond the animated height is reclaimed with negative GUILayout.Space,
//   and GUI.BeginClip masks the visual overflow.

using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    public enum FoldoutState
    {
        Closed,
        Measuring,
        Opening,
        Open,
        Closing,
    }

    public class AnimatedFoldout2
    {
        public readonly string key;
        public float speed;
        public float contentHeight;
        public FoldoutState state = FoldoutState.Closed;

        private bool _requestedOpen;
        private bool _firstFrame = true;

        public AnimatedFoldout2(string key, float speed = 10f)
        {
            this.key = key;
            this.speed = speed;
        }

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
        public readonly bool visible;

        private readonly AnimatedFoldout2 _foldout;
        private readonly FoldoutState _stateAtBegin;
        private readonly Color _prevGuiColor;
        private readonly bool _modifiedColor;
        private readonly Rect _clipRect;

        public FoldoutScope2(AnimatedFoldout2 foldout)
        {
            _foldout = foldout;
            _stateAtBegin = foldout.state;
            _prevGuiColor = GUI.color;
            _modifiedColor = false;
            _clipRect = Rect.zero;
            visible = false;

            switch (_stateAtBegin)
            {
                case FoldoutState.Closed:
                    visible = false;
                    break;

                case FoldoutState.Measuring:
                    // Draw at full size but invisible. Measure on Repaint.
                    visible = true;
                    _modifiedColor = true;
                    GUI.color = new Color(_prevGuiColor.r, _prevGuiColor.g, _prevGuiColor.b, 0f);
                    break;

                case FoldoutState.Opening:
                case FoldoutState.Closing:
                {
                    // Content draws normally (full size via EditorGUILayout).
                    // We set up a clip rect BEFORE content draws.
                    // In Dispose we'll reclaim the excess space.
                    visible = true;

                    var af = GetOrCreateAnimFloat(foldout.key, 0f);
                    float t = af.value;
                    float animatedH = foldout.contentHeight * t;

                    // We need the clip rect position. Use GetRect(0) to peek
                    // at the current layout Y without reserving space.
                    // The content will draw starting from this Y position.
                    _clipRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
                    _clipRect.height = animatedH;

                    // Apply the clip — only the top animatedH pixels of
                    // whatever follows will be visible.
                    GUI.BeginClip(_clipRect);

                    // Offset the clip so content draws from (0,0) inside it
                    // at the full width. GUILayout calls after this will draw
                    // relative to the clip origin... but wait, GUILayout
                    // doesn't know about the clip. We need a different approach.

                    // Actually: GUI.BeginClip only affects rendering, not layout.
                    // So GUILayout content will still be positioned in the outer
                    // coordinate space. We need to NOT use BeginClip here.
                    // Instead we'll apply the clip in Dispose, after content drew.
                    GUI.EndClip();

                    // For now, just let content draw normally. We'll clip in Dispose.
                    break;
                }

                case FoldoutState.Open:
                    visible = true;
                    break;
            }
        }

        public void Dispose()
        {
            switch (_stateAtBegin)
            {
                case FoldoutState.Measuring:
                    if (Event.current.type == EventType.Repaint)
                    {
                        var r = GUILayoutUtility.GetLastRect();
                        if (r.height > 1f)
                        {
                            _foldout.contentHeight = r.height;
                            var af = GetOrCreateAnimFloat(_foldout.key, 0f);
                            af.SnapTo(0f);
                            af.SetTarget(1f, _foldout.speed);
                            _foldout.state = FoldoutState.Opening;
                        }
                    }
                    break;

                case FoldoutState.Opening:
                case FoldoutState.Closing:
                {
                    // Content just drew at full height via normal GUILayout.
                    // We need to:
                    // 1. Clip the visual to animatedH (curtain)
                    // 2. Reclaim the excess layout space so following content
                    //    is positioned at the animated height, not full height.

                    float fullH = _foldout.contentHeight;
                    var af = GetOrCreateAnimFloat(_foldout.key, 0f);
                    float animatedH = fullH * af.value;
                    float excess = fullH - animatedH;

                    // Reclaim excess layout space — this pulls following content up
                    if (excess > 0.5f)
                        GUILayout.Space(-excess);

                    // Clip visual overflow: paint over the area below animatedH
                    // with the window background color. This is the simplest
                    // reliable way to hide overflow in IMGUI without affecting
                    // layout or event processing.
                    if (Event.current.type == EventType.Repaint && excess > 0.5f)
                    {
                        // The content ended at _clipRect.y + fullH.
                        // We want to hide from _clipRect.y + animatedH downward.
                        Rect coverRect = new Rect(
                            _clipRect.x,
                            _clipRect.y + animatedH,
                            _clipRect.width,
                            excess + 2f // +2 to avoid sub-pixel gaps
                        );
                        // Use the editor background color
                        Color bgColor = EditorGUIUtility.isProSkin
                            ? new Color(0.22f, 0.22f, 0.22f, 1f)
                            : new Color(0.76f, 0.76f, 0.76f, 1f);
                        EditorGUI.DrawRect(coverRect, bgColor);
                    }
                    break;
                }

                case FoldoutState.Open:
                    if (Event.current.type == EventType.Repaint)
                    {
                        var r = GUILayoutUtility.GetLastRect();
                        if (r.height > 1f)
                            _foldout.contentHeight = r.height;
                    }
                    break;
            }

            if (_modifiedColor)
                GUI.color = _prevGuiColor;
        }
    }
}
