// ZUISlider.cs
// Fully custom slider drawn from ZUISliderDef.
//
// Single-value horizontal: ZUI.Slider(value, min, max, label, style, ...)
// Single-value vertical:   ZUI.SliderVertical(value, min, max, label, style, ...)
// Range (min/max handles): ZUI.SliderRange(minVal, maxVal, absMin, absMax, label, style, ...)
//
// Layout (horizontal): [label] [trackFill | track] [value field]
//   label      — auto-sized to the text content (font-size aware). 0 = no label.
//                labelPosition = Above/Below draws it on a separate row with configurable alignment.
//   track      — groove, split at thumb into fill (left) + empty (right)
//   thumb      — ZUIButtonDef, full Normal/Hover/Active gradient + border + corners
//   value field — optional editable float on the right
//
// Double-clicking a single slider (not a range slider) resets it to defaultValue
// if a defaultValue was supplied when calling the API (use the float? overloads).

using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    /// <summary>When true, range slider value fields are suppressed for the next draw call.
    /// If CompactLabelWidth > 0, that much space is reserved on the right for a compact label instead.</summary>
    public static bool SuppressSliderValueFields { get; set; }

    /// <summary>When SuppressSliderValueFields is true, reserve this much width on the right for a compact label.</summary>
    public static float CompactLabelWidth { get; set; }

    // ===== Slider style name constants ========================================

    public static class SliderStyle
    {
        public const string Default     = "Default";
        public const string BigSlider   = "BigSlider";
        public const string SmallSlider = "SmallSlider";
        public const string ZoundMinMax = "ZoundMinMax";
    }

    // ===== Single-value horizontal slider API =================================

    /// <summary>Draws a horizontal single-value slider. Double-click resets to defaultValue if provided.</summary>
    public static float Slider(float value, float min, float max,
                                string label = "",
                                string style = SliderStyle.Default,
                                float? defaultValue = null,
                                params GUILayoutOption[] options)
    {
        var   def  = ActiveSheet?.FindSlider(style) ?? new ZUISliderDef();
        float h    = SliderTotalHeight(def, vertical: false);
        var   rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, AppendHeight(options, h));
        return DrawManualSlider(rect, value, min, max, label, def, style, defaultValue);
    }

    public static float Slider(Rect rect, float value, float min, float max,
                                string label = "",
                                string style = SliderStyle.Default,
                                float? defaultValue = null,
                                bool suppressValueField = false)
    {
        var def = ActiveSheet?.FindSlider(style) ?? new ZUISliderDef();
        return DrawManualSlider(rect, value, min, max, label, def, style, defaultValue, suppressValueField);
    }

    public static float Slider(float value, float min, float max,
                                string label, ZUISliderDef def,
                                float? defaultValue = null,
                                params GUILayoutOption[] options)
    {
        if (def == null) def = new ZUISliderDef();
        float h    = SliderTotalHeight(def, vertical: false);
        var   rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, AppendHeight(options, h));
        return DrawManualSlider(rect, value, min, max, label, def, def.name, defaultValue);
    }

    public static float Slider(Rect rect, float value, float min, float max,
                                string label, ZUISliderDef def,
                                float? defaultValue = null,
                                bool suppressValueField = false)
    {
        if (def == null) def = new ZUISliderDef();
        return DrawManualSlider(rect, value, min, max, label, def, def.name, defaultValue, suppressValueField);
    }

    // ===== Single-value vertical slider API ===================================

    /// <summary>Draws a vertical single-value slider. Double-click resets to defaultValue if provided.</summary>
    public static float SliderVertical(float value, float min, float max,
                                        string label = "",
                                        string style = SliderStyle.Default,
                                        float? defaultValue = null,
                                        params GUILayoutOption[] options)
    {
        var   def  = ActiveSheet?.FindSlider(style) ?? new ZUISliderDef();
        float w    = SliderTotalHeight(def, vertical: true); // height field is used as width for vertical
        var   rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, AppendWidth(options, w));
        return DrawManualSliderVertical(rect, value, min, max, label, def, style, defaultValue);
    }

    public static float SliderVertical(Rect rect, float value, float min, float max,
                                        string label = "",
                                        string style = SliderStyle.Default,
                                        float? defaultValue = null)
    {
        var def = ActiveSheet?.FindSlider(style) ?? new ZUISliderDef();
        return DrawManualSliderVertical(rect, value, min, max, label, def, style, defaultValue);
    }

    public static float SliderVertical(float value, float min, float max,
                                        string label, ZUISliderDef def,
                                        float? defaultValue = null,
                                        params GUILayoutOption[] options)
    {
        if (def == null) def = new ZUISliderDef();
        float w    = SliderTotalHeight(def, vertical: true);
        var   rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, AppendWidth(options, w));
        return DrawManualSliderVertical(rect, value, min, max, label, def, def.name, defaultValue);
    }

    public static float SliderVertical(Rect rect, float value, float min, float max,
                                        string label, ZUISliderDef def,
                                        float? defaultValue = null)
    {
        if (def == null) def = new ZUISliderDef();
        return DrawManualSliderVertical(rect, value, min, max, label, def, def.name, defaultValue);
    }

    // ===== Range slider API ==================================================
    // Two thumbs: minVal (left) and maxVal (right). Both are draggable.

    public static void SliderRange(ref float minVal, ref float maxVal,
                                    float absMin, float absMax,
                                    string label = "",
                                    string style = SliderStyle.Default,
                                    params GUILayoutOption[] options)
    {
        var   def  = ActiveSheet?.FindSlider(style) ?? new ZUISliderDef();
        float h    = SliderTotalHeight(def, vertical: false);
        var   rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, AppendHeight(options, h));
        DrawManualRangeSlider(rect, ref minVal, ref maxVal, absMin, absMax, label, def, style);
    }

    public static void SliderRange(Rect rect, ref float minVal, ref float maxVal,
                                    float absMin, float absMax,
                                    string label = "",
                                    string style = SliderStyle.Default)
    {
        var def = ActiveSheet?.FindSlider(style) ?? new ZUISliderDef();
        DrawManualRangeSlider(rect, ref minVal, ref maxVal, absMin, absMax, label, def, style);
    }

    public static void SliderRange(ref float minVal, ref float maxVal,
                                    float absMin, float absMax,
                                    string label, ZUISliderDef def,
                                    params GUILayoutOption[] options)
    {
        if (def == null) def = new ZUISliderDef();
        float h    = SliderTotalHeight(def, vertical: false);
        var   rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, AppendHeight(options, h));
        DrawManualRangeSlider(rect, ref minVal, ref maxVal, absMin, absMax, label, def, def.name);
    }

    // =========================================================================
    // Single-value horizontal core draw
    // =========================================================================

    static float DrawManualSlider(Rect totalRect, float value, float min, float max,
                                   string label, ZUISliderDef def, string styleName = "",
                                   float? defaultValue = null, bool suppressValueField = false)
    {
        value = Mathf.Clamp(value, min, max);

        // Carve label + value rects
        Rect labelRect, sliderRect, valueRect;
        CarveSliderLayout(totalRect, label, def, out labelRect, out sliderRect, out valueRect);

        // Geometry
        float thumbW    = Mathf.Max(4f, def.thumbWidth);
        float thumbH    = def.thumbHeight > 0f ? def.thumbHeight : sliderRect.height;
        float trackH    = Mathf.Min(def.trackHeight, sliderRect.height);
        float travelMin = sliderRect.x + thumbW * 0.5f;
        float travelMax = sliderRect.xMax - thumbW * 0.5f;

        float t       = InverseLerpSafe(min, max, value);
        float thumbCx = travelMin + t * Mathf.Max(1f, travelMax - travelMin);

        // Input
        int  id     = GUIUtility.GetControlID(FocusType.Passive, sliderRect);
        var  ev     = Event.current;
        bool isDrag = GUIUtility.hotControl == id;

        // Double-click to reset (must come before the main switch to avoid being consumed)
        if (defaultValue.HasValue
            && ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2
            && sliderRect.Contains(ev.mousePosition))
        {
            value = Mathf.Clamp(defaultValue.Value, min, max);
            GUIUtility.hotControl = 0;
            GUI.changed = true;
            ev.Use();
        }

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (sliderRect.Contains(ev.mousePosition) && ev.button == 0)
                {
                    GUIUtility.hotControl = id;
                    value = SamplePosition(ev.mousePosition.x, travelMin, travelMax, min, max);
                    GUI.changed = true;
                    ev.Use();
                }
                break;
            case EventType.MouseDrag:
                if (isDrag)
                {
                    value = SamplePosition(ev.mousePosition.x, travelMin, travelMax, min, max);
                    GUI.changed = true;
                    ev.Use();
                }
                break;
            case EventType.MouseUp:
                if (isDrag) { GUIUtility.hotControl = 0; ev.Use(); }
                break;
        }

        // Recompute after input
        t       = InverseLerpSafe(min, max, value);
        thumbCx = travelMin + t * Mathf.Max(1f, travelMax - travelMin);

        if (ev.type == EventType.Repaint)
        {
            float trackY    = sliderRect.y + (sliderRect.height - trackH) * 0.5f;
            var   fillRect  = new Rect(sliderRect.x, trackY, thumbCx - sliderRect.x, trackH);
            var   emptyRect = new Rect(thumbCx, trackY, sliderRect.xMax - thumbCx, trackH);
            float thumbY    = sliderRect.y + (sliderRect.height - thumbH) * 0.5f;
            var   thumbRect = new Rect(thumbCx - thumbW * 0.5f, thumbY, thumbW, thumbH);

            if (fillRect.width  > 0f) def.trackFill?.DrawBackground(fillRect);
            if (emptyRect.width > 0f) def.track?.DrawBackground(emptyRect);

            bool isHover  = thumbRect.Contains(ev.mousePosition);
            var  state    = isDrag ? ZUIButtonDrawState.Active : isHover ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal;
            int  cr       = def.thumb?.GetResolvedCornerRadius() ?? 0;
            def.thumb?.DrawVisual(thumbRect, state, cr);

            DrawSliderLabel(labelRect, label, def);
        }

        if (StyleDebugMode && IsDebugHit(sliderRect))
            CollectSliderDebugInfo(def, styleName, sliderRect, isRange: false);

        DrawFlashOverlayIfNeeded(sliderRect, styleName, 0, FlashDefType.Slider);

        // Value field
        if (!suppressValueField && def.showValueField && def.valueWidth > 0f)
        {
            EditorGUI.BeginChangeCheck();
            string fmt = !string.IsNullOrEmpty(def.valueFormat)
                ? def.valueFormat
                : AutoFormat(min, max);
            float newVal = EditorGUI.FloatField(valueRect,
                float.Parse(value.ToString(fmt), System.Globalization.CultureInfo.InvariantCulture),
                def.GetValueStyle());
            if (EditorGUI.EndChangeCheck())
                value = Mathf.Clamp(newVal, min, max);
        }

        return value;
    }

    // =========================================================================
    // Single-value vertical core draw
    // =========================================================================

    static float DrawManualSliderVertical(Rect totalRect, float value, float min, float max,
                                           string label, ZUISliderDef def, string styleName = "",
                                           float? defaultValue = null)
    {
        value = Mathf.Clamp(value, min, max);

        // Carve label + slider rects for vertical layout
        Rect labelRect, sliderRect;
        CarveVerticalSliderLayout(totalRect, label, def, out labelRect, out sliderRect);

        // Geometry — for vertical: thumbWidth = horizontal size, thumbHeight = knob travel height
        float thumbW    = Mathf.Max(4f, def.thumbWidth);   // knob width (across track)
        float thumbH    = def.thumbHeight > 0f ? def.thumbHeight : thumbW; // knob height along travel
        float trackW    = Mathf.Min(def.trackHeight, sliderRect.width);    // track groove width
        float travelMin = sliderRect.yMax - thumbH * 0.5f; // top of travel = max value
        float travelMax = sliderRect.y    + thumbH * 0.5f; // bottom of travel = min value
        // Note: y increases downward, so max value is at top (small y)
        float travelTop    = sliderRect.y    + thumbH * 0.5f;  // pixel y for max
        float travelBottom = sliderRect.yMax - thumbH * 0.5f;  // pixel y for min

        // t=0 → bottom (min), t=1 → top (max)
        float t       = InverseLerpSafe(min, max, value);
        float thumbCy = travelBottom - t * Mathf.Max(1f, travelBottom - travelTop);

        // Input
        int  id     = GUIUtility.GetControlID(FocusType.Passive, sliderRect);
        var  ev     = Event.current;
        bool isDrag = GUIUtility.hotControl == id;

        // Double-click to reset
        if (defaultValue.HasValue
            && ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2
            && sliderRect.Contains(ev.mousePosition))
        {
            value = Mathf.Clamp(defaultValue.Value, min, max);
            GUIUtility.hotControl = 0;
            GUI.changed = true;
            ev.Use();
        }

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (sliderRect.Contains(ev.mousePosition) && ev.button == 0)
                {
                    GUIUtility.hotControl = id;
                    value = SamplePositionVertical(ev.mousePosition.y, travelTop, travelBottom, min, max);
                    GUI.changed = true;
                    ev.Use();
                }
                break;
            case EventType.MouseDrag:
                if (isDrag)
                {
                    value = SamplePositionVertical(ev.mousePosition.y, travelTop, travelBottom, min, max);
                    GUI.changed = true;
                    ev.Use();
                }
                break;
            case EventType.MouseUp:
                if (isDrag) { GUIUtility.hotControl = 0; ev.Use(); }
                break;
        }

        // Recompute after input
        t       = InverseLerpSafe(min, max, value);
        thumbCy = travelBottom - t * Mathf.Max(1f, travelBottom - travelTop);

        if (ev.type == EventType.Repaint)
        {
            float trackX = sliderRect.x + (sliderRect.width - trackW) * 0.5f;

            // trackFill = the filled portion from bottom up to the thumb (represents value level)
            // track     = the empty groove above the thumb
            var fillRect  = new Rect(trackX, thumbCy, trackW, sliderRect.yMax - thumbCy);
            var emptyRect = new Rect(trackX, sliderRect.y, trackW, thumbCy - sliderRect.y);

            float thumbX    = sliderRect.x + (sliderRect.width - thumbW) * 0.5f;
            var   thumbRect = new Rect(thumbX, thumbCy - thumbH * 0.5f, thumbW, thumbH);

            if (fillRect.height  > 0f) def.trackFill?.DrawBackground(fillRect);
            if (emptyRect.height > 0f) def.track?.DrawBackground(emptyRect);

            bool isHover = thumbRect.Contains(ev.mousePosition);
            var  state   = isDrag ? ZUIButtonDrawState.Active : isHover ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal;
            int  cr      = def.thumb?.GetResolvedCornerRadius() ?? 0;
            def.thumb?.DrawVisual(thumbRect, state, cr);

            DrawSliderLabelVertical(labelRect, label, def);
        }

        if (StyleDebugMode && IsDebugHit(sliderRect))
            CollectSliderDebugInfo(def, styleName, sliderRect, isRange: false);

        DrawFlashOverlayIfNeeded(sliderRect, styleName, 0, FlashDefType.Slider);

        return value;
    }

    // =========================================================================
    // Range slider core draw
    // =========================================================================

    static readonly int s_RangeMinHash  = "ZUIRangeMin".GetHashCode();
    static readonly int s_RangeMaxHash  = "ZUIRangeMax".GetHashCode();
    static readonly int s_RangeFillHash = "ZUIRangeFill".GetHashCode();

    // Drag-pan state for the fill region
    static float _rangeDragAnchorMin;
    static float _rangeDragAnchorMax;
    static float _rangeDragAnchorX;

    static void DrawManualRangeSlider(Rect totalRect, ref float minVal, ref float maxVal,
                                       float absMin, float absMax,
                                       string label, ZUISliderDef def, string styleName = "")
    {
        minVal = Mathf.Clamp(minVal, absMin, absMax);
        maxVal = Mathf.Clamp(maxVal, minVal, absMax);

        Rect labelRect, sliderRect, valueRect;
        CarveSliderLayout(totalRect, label, def, out labelRect, out sliderRect, out valueRect);

        float thumbW    = Mathf.Max(4f, def.thumbWidth);
        float thumbH    = def.thumbHeight > 0f ? def.thumbHeight : sliderRect.height;
        float trackH    = Mathf.Min(def.trackHeight, sliderRect.height);
        float travelMin = sliderRect.x + thumbW * 0.5f;
        float travelMax = sliderRect.xMax - thumbW * 0.5f;
        float travelLen = Mathf.Max(1f, travelMax - travelMin);

        float tMin = InverseLerpSafe(absMin, absMax, minVal);
        float tMax = InverseLerpSafe(absMin, absMax, maxVal);
        float cxMin = travelMin + tMin * travelLen;
        float cxMax = travelMin + tMax * travelLen;

        int  idMin     = GUIUtility.GetControlID(s_RangeMinHash,  FocusType.Passive, sliderRect);
        int  idMax     = GUIUtility.GetControlID(s_RangeMaxHash,  FocusType.Passive, sliderRect);
        int  idFill    = GUIUtility.GetControlID(s_RangeFillHash, FocusType.Passive, sliderRect);
        var  ev        = Event.current;
        bool dragMin   = GUIUtility.hotControl == idMin;
        bool dragMax   = GUIUtility.hotControl == idMax;
        bool dragFill  = GUIUtility.hotControl == idFill;

        float trackY    = sliderRect.y + (sliderRect.height - trackH) * 0.5f;

        // Thumbs are "fused" when their pixel positions overlap (gap < thumbW)
        bool fused = (cxMax - cxMin) < thumbW;

        // When fused, render them side-by-side: min at its natural position, max immediately right.
        float drawCxMin = cxMin;
        float drawCxMax = fused ? cxMin + thumbW : cxMax;

        var thumbRectMin = ThumbRect(drawCxMin, sliderRect, thumbW, thumbH);
        var thumbRectMax = ThumbRect(drawCxMax, sliderRect, thumbW, thumbH);

        // Fill hit rect spans full control height for easy grabbing
        var fillHitRect  = new Rect(cxMin, sliderRect.y, cxMax - cxMin, sliderRect.height);
        var fillDrawRect = new Rect(cxMin, trackY, cxMax - cxMin, trackH);

        // ── Double-click to fuse — must be checked BEFORE the switch so ev.Use() in the
        //    single-click branch doesn't consume it first. Uses draw rects (pre-recompute)
        //    which are correct for hit-testing the visual thumb positions.
        if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2
            && sliderRect.Contains(ev.mousePosition))
        {
            if (fused)
            {
                // Both thumbs visible side-by-side: left half = min, right half = max
                float midX = drawCxMin + thumbW * 0.5f;
                if (ev.mousePosition.x <= midX) { maxVal = minVal; } // click on min → fuse max to min
                else                             { minVal = maxVal; } // click on max → fuse min to max
            }
            else
            {
                if      (thumbRectMin.Contains(ev.mousePosition)) maxVal = minVal;
                else if (thumbRectMax.Contains(ev.mousePosition)) minVal = maxVal;
            }
            GUIUtility.hotControl = 0;
            GUI.changed = true;
            ev.Use();
        }
        else switch (ev.type)
        {
            case EventType.MouseDown when ev.button == 0:
            {
                if (!sliderRect.Contains(ev.mousePosition)) break;

                float mx = ev.mousePosition.x;

                if (fused)
                {
                    // Fused: whole block pans together.
                    // Clicking outside the block on the empty track separates by moving the
                    // nearer thumb — handled by the outer-track branch below.
                    var fusedHitRect = new Rect(drawCxMin - thumbW * 0.5f, sliderRect.y,
                                                thumbW * 2f, sliderRect.height);
                    if (fusedHitRect.Contains(ev.mousePosition))
                    {
                        // Click is on the fused block — pan drag
                        GUIUtility.hotControl = idFill;
                        _rangeDragAnchorMin   = minVal;
                        _rangeDragAnchorMax   = maxVal;
                        _rangeDragAnchorX     = mx;
                    }
                    else
                    {
                        // Click is outside fused block on empty track — separate by moving nearer thumb
                        bool goLeft = mx < cxMin;
                        GUIUtility.hotControl = goLeft ? idMin : idMax;
                        ApplyRangeDrag(mx, travelMin, travelMax, absMin, absMax,
                                       ref minVal, ref maxVal, dragMin: goLeft);
                    }
                }
                else
                {
                    bool hitMin  = thumbRectMin.Contains(ev.mousePosition);
                    bool hitMax  = thumbRectMax.Contains(ev.mousePosition);
                    bool hitFill = !hitMin && !hitMax && fillHitRect.width > 0f && fillHitRect.Contains(ev.mousePosition);

                    if (hitFill)
                    {
                        GUIUtility.hotControl = idFill;
                        _rangeDragAnchorMin   = minVal;
                        _rangeDragAnchorMax   = maxVal;
                        _rangeDragAnchorX     = mx;
                    }
                    else
                    {
                        if (!hitMin && !hitMax)
                            hitMin = Mathf.Abs(mx - cxMin) <= Mathf.Abs(mx - cxMax);
                        GUIUtility.hotControl = hitMin ? idMin : idMax;
                        ApplyRangeDrag(mx, travelMin, travelMax, absMin, absMax,
                                       ref minVal, ref maxVal, dragMin: GUIUtility.hotControl == idMin);
                    }
                }
                GUI.changed = true;
                ev.Use();
                break;
            }
            case EventType.MouseDrag when dragFill:
            {
                float dx   = ev.mousePosition.x - _rangeDragAnchorX;
                float dVal = dx / travelLen * (absMax - absMin);
                float span = _rangeDragAnchorMax - _rangeDragAnchorMin;
                float nMin = Mathf.Clamp(_rangeDragAnchorMin + dVal, absMin, absMax - span);
                minVal = nMin;
                maxVal = nMin + span;
                GUI.changed = true;
                ev.Use();
                break;
            }
            case EventType.MouseDrag when dragMin || dragMax:
                ApplyRangeDrag(ev.mousePosition.x, travelMin, travelMax, absMin, absMax,
                               ref minVal, ref maxVal, dragMin: dragMin);
                GUI.changed = true;
                ev.Use();
                break;
            case EventType.MouseUp when dragMin || dragMax || dragFill:
                GUIUtility.hotControl = 0;
                ev.Use();
                break;
        }

        // Recompute after input
        minVal = Mathf.Clamp(minVal, absMin, absMax);
        maxVal = Mathf.Clamp(maxVal, minVal, absMax);
        tMin  = InverseLerpSafe(absMin, absMax, minVal);
        tMax  = InverseLerpSafe(absMin, absMax, maxVal);
        cxMin = travelMin + tMin * travelLen;
        cxMax = travelMin + tMax * travelLen;
        trackY = sliderRect.y + (sliderRect.height - trackH) * 0.5f;

        fused        = (cxMax - cxMin) < thumbW;
        drawCxMin    = cxMin;
        drawCxMax    = fused ? cxMin + thumbW : cxMax;
        thumbRectMin = ThumbRect(drawCxMin, sliderRect, thumbW, thumbH);
        thumbRectMax = ThumbRect(drawCxMax, sliderRect, thumbW, thumbH);
        fillHitRect  = new Rect(cxMin, sliderRect.y, cxMax - cxMin, sliderRect.height);
        fillDrawRect = new Rect(cxMin, trackY, cxMax - cxMin, trackH);

        if (ev.type == EventType.Repaint)
        {
            var emptyLeft  = new Rect(sliderRect.x, trackY, cxMin - sliderRect.x, trackH);
            // Empty right starts after the drawn max thumb position
            var emptyRight = new Rect(drawCxMax, trackY, sliderRect.xMax - drawCxMax, trackH);

            if (emptyLeft.width  > 0f) def.track?.DrawBackground(emptyLeft);

            // Fill between logical thumb positions (zero-width when fused)
            if (fillDrawRect.width > 0f)
            {
                def.trackFill?.DrawBackground(fillDrawRect);
                bool fillHovered = !fused && (dragFill ||
                    (!dragMin && !dragMax && fillHitRect.Contains(ev.mousePosition)));
                if (fillHovered)
                    EditorGUI.DrawRect(fillDrawRect, new Color(1f, 1f, 1f, 0.12f));
            }

            if (emptyRight.width > 0f) def.track?.DrawBackground(emptyRight);

            if (fused)
            {
                // Both thumbs move as one: show fill-drag hover state on both
                var fusedHoverRect = new Rect(drawCxMin - thumbW * 0.5f, sliderRect.y,
                                              thumbW * 2f, sliderRect.height);
                bool fusedHovered = dragFill || fusedHoverRect.Contains(ev.mousePosition);
                var  fusedState   = fusedHovered ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal;
                if (dragFill) fusedState = ZUIButtonDrawState.Active;
                int crMin = def.thumb?.GetResolvedCornerRadius() ?? 0;
                int crMax = (def.thumbMax ?? def.thumb)?.GetResolvedCornerRadius() ?? 0;
                def.thumb?.DrawVisual(thumbRectMin, fusedState, crMin);
                (def.thumbMax ?? def.thumb)?.DrawVisual(thumbRectMax, fusedState, crMax);
            }
            else
            {
                DrawRangeThumb(thumbRectMin, def.thumb,                 idMin, ev);
                DrawRangeThumb(thumbRectMax, def.thumbMax ?? def.thumb, idMax, ev);
            }

            DrawSliderLabel(labelRect, label, def);
        }

        if (StyleDebugMode && IsDebugHit(sliderRect))
            CollectSliderDebugInfo(def, styleName, sliderRect, isRange: true);

        DrawFlashOverlayIfNeeded(sliderRect, styleName, 0, FlashDefType.Slider);

        // Value fields for range: show min/max as two fields if space
        if (!SuppressSliderValueFields && def.showValueField && def.valueWidth > 0f && valueRect.width > 0f)
        {
            float halfW = (valueRect.width - 2f) * 0.5f;
            var   minFR = new Rect(valueRect.x,             valueRect.y, halfW, valueRect.height);
            var   maxFR = new Rect(valueRect.x + halfW + 2f, valueRect.y, halfW, valueRect.height);
            var   vs    = def.GetValueStyle();
            string fmt  = !string.IsNullOrEmpty(def.valueFormat) ? def.valueFormat : AutoFormat(absMin, absMax);
            EditorGUI.BeginChangeCheck();
            float newMin = EditorGUI.FloatField(minFR,
                float.Parse(minVal.ToString(fmt), System.Globalization.CultureInfo.InvariantCulture), vs);
            if (EditorGUI.EndChangeCheck()) minVal = Mathf.Clamp(newMin, absMin, absMax);
            EditorGUI.BeginChangeCheck();
            float newMax = EditorGUI.FloatField(maxFR,
                float.Parse(maxVal.ToString(fmt), System.Globalization.CultureInfo.InvariantCulture), vs);
            if (EditorGUI.EndChangeCheck()) maxVal = Mathf.Clamp(newMax, minVal, absMax);
        }
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    // Measures auto label width from the label string using the def's label style + font size.
    static float MeasureLabelWidth(string label, ZUISliderDef def)
    {
        if (string.IsNullOrEmpty(label)) return 0f;
        var ls = def.GetLabelStyle();
        return ls.CalcSize(new GUIContent(label)).x + 4f; // +4 right padding
    }

    static float MeasureLabelHeight(string label, ZUISliderDef def)
    {
        if (string.IsNullOrEmpty(label)) return 0f;
        var ls = def.GetLabelStyle();
        return ls.CalcSize(new GUIContent(label)).y + 2f; // +2 bottom padding
    }

    // Horizontal slider layout: supports Inline (label left), Above, Below.
    static void CarveSliderLayout(Rect totalRect, string label, ZUISliderDef def,
                                   out Rect labelRect, out Rect sliderRect, out Rect valueRect)
    {
        labelRect  = Rect.zero;
        valueRect  = Rect.zero;
        sliderRect = totalRect;

        bool hasLabel = !string.IsNullOrEmpty(label);

        if (hasLabel && def.labelPosition != ZUILabelPosition.Inline)
        {
            float lh = MeasureLabelHeight(label, def);

            if (def.labelPosition == ZUILabelPosition.Above)
            {
                labelRect  = new Rect(totalRect.x, totalRect.y, totalRect.width, lh);
                sliderRect = new Rect(totalRect.x, totalRect.y + lh,
                                      totalRect.width, totalRect.height - lh);
            }
            else // Below
            {
                sliderRect = new Rect(totalRect.x, totalRect.y,
                                      totalRect.width, totalRect.height - lh);
                labelRect  = new Rect(totalRect.x, sliderRect.yMax, totalRect.width, lh);
            }
        }
        else if (hasLabel)
        {
            // Inline: label to the left
            float lw = def.labelWidth > 0f
                ? Mathf.Max(def.labelWidth, MeasureLabelWidth(label, def))
                : MeasureLabelWidth(label, def);
            labelRect  = new Rect(totalRect.x, totalRect.y, lw, totalRect.height);
            sliderRect = new Rect(totalRect.x + lw, totalRect.y,
                                  totalRect.width - lw, totalRect.height);
        }

        if (SuppressSliderValueFields && CompactLabelWidth > 0f && def.labelPosition == ZUILabelPosition.Inline)
        {
            // Reserve space for the compact label but don't create a value field
            float lw = CompactLabelWidth;
            valueRect  = new Rect(sliderRect.xMax - lw, sliderRect.y, lw, sliderRect.height);
            sliderRect = new Rect(sliderRect.x, sliderRect.y,
                                  sliderRect.width - lw, sliderRect.height);
        }
        else if (!SuppressSliderValueFields && def.showValueField && def.valueWidth > 0f && def.labelPosition == ZUILabelPosition.Inline)
        {
            float vw  = def.valueWidth;
            valueRect  = new Rect(sliderRect.xMax - vw, sliderRect.y, vw, sliderRect.height);
            sliderRect = new Rect(sliderRect.x, sliderRect.y,
                                  sliderRect.width - vw, sliderRect.height);
        }
    }

    // Vertical slider layout: label carved from top or bottom of the rect.
    static void CarveVerticalSliderLayout(Rect totalRect, string label, ZUISliderDef def,
                                           out Rect labelRect, out Rect sliderRect)
    {
        labelRect  = Rect.zero;
        sliderRect = totalRect;

        if (string.IsNullOrEmpty(label)) return;

        float lh = MeasureLabelHeight(label, def);

        // For vertical sliders: Above = label at top of rect (above track), Below = label at bottom.
        // Default (Inline) falls back to Below so the track gets maximum height.
        bool above = def.labelPosition == ZUILabelPosition.Above;

        if (above)
        {
            labelRect  = new Rect(totalRect.x, totalRect.y, totalRect.width, lh);
            sliderRect = new Rect(totalRect.x, totalRect.y + lh,
                                  totalRect.width, totalRect.height - lh);
        }
        else
        {
            sliderRect = new Rect(totalRect.x, totalRect.y,
                                  totalRect.width, totalRect.height - lh);
            labelRect  = new Rect(totalRect.x, sliderRect.yMax, totalRect.width, lh);
        }
    }

    // Draws label text with shadow support (EditorGUI.LabelField ignores GUIStyle shadow).
    static void DrawSliderLabel(Rect labelRect, string label, ZUISliderDef def)
    {
        if (labelRect.width <= 0f || string.IsNullOrEmpty(label)) return;
        var ls = def.GetLabelStyle();

        if (def.labelPosition == ZUILabelPosition.Inline)
        {
            ls.alignment = TextAnchor.MiddleLeft;
        }
        else
        {
            ls.alignment = def.labelAlignment switch
            {
                ZUILabelAlignment.Center => TextAnchor.UpperCenter,
                ZUILabelAlignment.Right  => TextAnchor.UpperRight,
                _                        => TextAnchor.UpperLeft,
            };
        }

        DrawLabelWithShadow(labelRect, label, def, ls);
    }

    // Draws label text for vertical sliders.
    static void DrawSliderLabelVertical(Rect labelRect, string label, ZUISliderDef def)
    {
        if (labelRect.width <= 0f || string.IsNullOrEmpty(label)) return;
        var ls = def.GetLabelStyle();
        ls.alignment = def.labelAlignment switch
        {
            ZUILabelAlignment.Center => TextAnchor.UpperCenter,
            ZUILabelAlignment.Right  => TextAnchor.UpperRight,
            _                        => TextAnchor.UpperLeft,
        };
        DrawLabelWithShadow(labelRect, label, def, ls);
    }

    static void DrawLabelWithShadow(Rect labelRect, string label, ZUISliderDef def, GUIStyle ls)
    {
        // Route through the central DrawLabel for shadow, outline, and gradient support.
        ZUI.DrawLabel(labelRect, new GUIContent(label), ls, def.labelText);
    }

    static Rect ThumbRect(float cx, Rect sliderRect, float thumbW, float thumbH)
    {
        float thumbY = sliderRect.y + (sliderRect.height - thumbH) * 0.5f;
        return new Rect(cx - thumbW * 0.5f, thumbY, thumbW, thumbH);
    }

    static void DrawRangeThumb(Rect thumbRect, ZUIButtonDef thumbDef, int id, Event ev)
    {
        bool isHover = thumbRect.Contains(ev.mousePosition);
        bool isDrag  = GUIUtility.hotControl == id;
        var  state   = isDrag ? ZUIButtonDrawState.Active : isHover ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal;
        int  cr      = thumbDef?.GetResolvedCornerRadius() ?? 0;
        thumbDef?.DrawVisual(thumbRect, state, cr);
    }

    static void ApplyRangeDrag(float mouseX, float travelMin, float travelMax,
                                float absMin, float absMax,
                                ref float minVal, ref float maxVal, bool dragMin)
    {
        float v = SamplePosition(mouseX, travelMin, travelMax, absMin, absMax);
        if (dragMin) minVal = Mathf.Clamp(v, absMin, maxVal);
        else         maxVal = Mathf.Clamp(v, minVal, absMax);
    }

    static float InverseLerpSafe(float min, float max, float v)
        => max > min ? Mathf.InverseLerp(min, max, v) : 0f;

    static float SamplePosition(float mouseX, float travelMin, float travelMax, float min, float max)
    {
        float t = Mathf.InverseLerp(travelMin, travelMax, mouseX);
        return Mathf.Lerp(min, max, Mathf.Clamp01(t));
    }

    // Vertical: mouseY maps to value. travelTop = max value pixel, travelBottom = min value pixel.
    static float SamplePositionVertical(float mouseY, float travelTop, float travelBottom, float min, float max)
    {
        float t = Mathf.InverseLerp(travelBottom, travelTop, mouseY);
        return Mathf.Lerp(min, max, Mathf.Clamp01(t));
    }

    // For horizontal sliders: height is derived from thumbHeight / trackHeight.
    // For vertical sliders: we return thumbWidth as the "size" dimension for GUILayoutUtility.
    static float SliderTotalHeight(ZUISliderDef def, bool vertical)
    {
        if (vertical)
            return Mathf.Max(def.thumbWidth > 0f ? def.thumbWidth : 20f, def.trackHeight);
        return Mathf.Max(def.thumbHeight > 0f ? def.thumbHeight : 20f, def.trackHeight);
    }

    /// <summary>Auto-format based on slider range. Small range = more decimals.</summary>
    static string AutoFormat(float min, float max)
    {
        float range = Mathf.Abs(max - min);
        if (range <= 1f)   return "F2";
        if (range <= 10f)  return "F1";
        return "F0";
    }

    static GUILayoutOption[] AppendHeight(GUILayoutOption[] options, float height)
    {
        var result = new GUILayoutOption[options.Length + 1];
        options.CopyTo(result, 0);
        result[options.Length] = GUILayout.Height(height);
        return result;
    }

    static GUILayoutOption[] AppendWidth(GUILayoutOption[] options, float width)
    {
        var result = new GUILayoutOption[options.Length + 1];
        options.CopyTo(result, 0);
        result[options.Length] = GUILayout.Width(width);
        return result;
    }
}

