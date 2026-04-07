// ZUIMultiSelect.cs
// Multi-selection controls for ZUI: CycleButton, CycleButtonWithArrows, MiniRadio.
//
// CycleButton:   single button that cycles through options on click, sized to longest label.
// CycleArrows:   same as CycleButton but with left/right arrow buttons flanking.
// MiniRadio:     tight inline labels (horizontal or vertical), selected one is highlighted.

using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ===== CycleButton ========================================================
    // A single button that displays the current selection. Clicking cycles forward.
    // Right-clicking cycles backward. Sized to the widest label so it doesn't jump.

    public static int CycleButton(int selected, string[] labels,
                                   string style = Style.Default,
                                   params GUILayoutOption[] options)
    {
        var sheet = ActiveSheet;
        var def   = sheet?.FindButton(style);
        if (def == null)
        {
            // Fallback: Unity popup
            return EditorGUILayout.Popup(selected, labels, options);
        }

        // Measure widest label to keep button stable
        var labelStyle = def.GetLabelStyle();
        float maxW = 0f;
        for (int i = 0; i < labels.Length; i++)
        {
            float w = labelStyle.CalcSize(new GUIContent(labels[i])).x;
            if (w > maxW) maxW = w;
        }
        // Add padding for the button chrome
        float padH = def.padding.PadLeft + def.padding.PadRight;
        float minW = maxW + padH + 4f;

        var content = new GUIContent(labels[Mathf.Clamp(selected, 0, labels.Length - 1)]);
        var rect = GUILayoutUtility.GetRect(content, labelStyle,
            AppendMinWidth(options, minW));

        return DrawCycleButton(rect, selected, labels, content, def, style);
    }

    public static int CycleButton(Rect rect, int selected, string[] labels,
                                   string style = Style.Default)
    {
        var sheet = ActiveSheet;
        var def   = sheet?.FindButton(style);
        if (def == null)
            return EditorGUI.Popup(rect, selected, labels);

        var content = new GUIContent(labels[Mathf.Clamp(selected, 0, labels.Length - 1)]);
        return DrawCycleButton(rect, selected, labels, content, def, style);
    }

    static int DrawCycleButton(Rect rect, int selected, string[] labels,
                                GUIContent content, ZUIButtonDef def, string debugStyle)
    {
        if (CheckDebugContextClick(rect))
        {
            CollectButtonDebugInfo(def, debugStyle, rect);
            return selected;
        }

        int id = GUIUtility.GetControlID(FocusType.Passive, rect);
        var ev = Event.current;
        bool isHover  = rect.Contains(ev.mousePosition);
        bool isActive = GUIUtility.hotControl == id;

        TweenNotifyHover(id, isHover, def);
        TweenNotifyActive(id, isActive, def);

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (isHover && (ev.button == 0 || ev.button == 1))
                {
                    GUIUtility.hotControl = id;
                    ev.Use();
                }
                break;

            case EventType.MouseUp:
                if (isActive)
                {
                    GUIUtility.hotControl = 0;
                    ev.Use();
                    if (isHover)
                    {
                        int dir = ev.button == 1 ? -1 : 1;
                        selected = (selected + dir + labels.Length) % labels.Length;
                        GUI.changed = true;
                    }
                }
                break;
        }

        if (ev.type == EventType.Repaint)
        {
            int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
            var s = isActive ? ZUIButtonDrawState.Active : (isHover ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Normal);
            def.DrawVisual(rect, s, r);
            DrawButtonLabel(rect, content, def.GetLabelStyle(s), null, ZIconPlacement.LeftOfLabel, def, def.GetText(s));
        }

        return selected;
    }

    // ===== CycleArrows ========================================================
    // Left arrow | current label | right arrow — all in one row.

    public static int CycleArrows(int selected, string[] labels,
                                   string style = Style.Default,
                                   string arrowStyle = "",
                                   params GUILayoutOption[] options)
    {
        var sheet = ActiveSheet;
        var def   = sheet?.FindButton(style);
        if (def == null)
            return EditorGUILayout.Popup(selected, labels, options);

        string arrStyle = string.IsNullOrEmpty(arrowStyle) ? style : arrowStyle;

        // Measure widest label
        var labelStyle = def.GetLabelStyle();
        float maxW = 0f;
        for (int i = 0; i < labels.Length; i++)
        {
            float w = labelStyle.CalcSize(new GUIContent(labels[i])).x;
            if (w > maxW) maxW = w;
        }
        float padH = def.padding.PadLeft + def.padding.PadRight;
        float arrowW = 20f;
        float totalMinW = maxW + padH + 4f + arrowW * 2f + 2f;

        var totalRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            AppendMinWidth(options, totalMinW));

        return DrawCycleArrows(totalRect, selected, labels, def, arrStyle);
    }

    public static int CycleArrows(Rect rect, int selected, string[] labels,
                                   string style = Style.Default,
                                   string arrowStyle = "")
    {
        var sheet = ActiveSheet;
        var def   = sheet?.FindButton(style);
        if (def == null)
            return EditorGUI.Popup(rect, selected, labels);

        string arrStyle = string.IsNullOrEmpty(arrowStyle) ? style : arrowStyle;
        return DrawCycleArrows(rect, selected, labels, def, arrStyle);
    }

    static int DrawCycleArrows(Rect rect, int selected, string[] labels,
                                ZUIButtonDef def, string arrowStyle)
    {
        float arrowW = 20f;
        float gap = 1f;
        var leftRect  = new Rect(rect.x, rect.y, arrowW, rect.height);
        var rightRect = new Rect(rect.xMax - arrowW, rect.y, arrowW, rect.height);
        var centerRect = new Rect(rect.x + arrowW + gap, rect.y,
                                   rect.width - arrowW * 2f - gap * 2f, rect.height);

        // Draw center label as a cycle button (click to cycle)
        var content = new GUIContent(labels[Mathf.Clamp(selected, 0, labels.Length - 1)]);
        selected = DrawCycleButton(centerRect, selected, labels, content, def, def.name);

        // Arrow buttons
        if (Button(leftRect, "\u25C0", arrowStyle))
        {
            selected = (selected - 1 + labels.Length) % labels.Length;
            GUI.changed = true;
        }
        if (Button(rightRect, "\u25B6", arrowStyle))
        {
            selected = (selected + 1) % labels.Length;
            GUI.changed = true;
        }

        return selected;
    }

    // ===== MiniRadio ==========================================================
    // Tight inline labels — selected one draws with Active state, others with Normal.
    // Horizontal by default. Use MiniRadioVertical for vertical layout.

    public static int MiniRadio(int selected, string[] labels,
                                 string style = Style.Default,
                                 params GUILayoutOption[] options)
    {
        var sheet = ActiveSheet;
        var def   = sheet?.FindButton(style);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < labels.Length; i++)
        {
            bool isSel = (i == selected);
            if (def != null)
            {
                // Use ZUI manual draw — Active state for selected, Normal for others
                var labelContent = new GUIContent(labels[i]);
                var labelStyle = def.GetLabelStyle(isSel ? ZUIButtonDrawState.Active : ZUIButtonDrawState.Normal);
                var sz = labelStyle.CalcSize(labelContent);
                var rect = GUILayoutUtility.GetRect(sz.x + 4f, sz.y, options);

                int id = GUIUtility.GetControlID(FocusType.Passive, rect);
                bool hover = rect.Contains(Event.current.mousePosition);
                TweenNotifyHover(id, hover, def);

                if (Event.current.type == EventType.MouseDown && hover && Event.current.button == 0)
                {
                    selected = i;
                    GUI.changed = true;
                    Event.current.Use();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
                    var state = isSel ? ZUIButtonDrawState.Active
                              : hover ? ZUIButtonDrawState.Hover
                              : ZUIButtonDrawState.Normal;
                    def.DrawVisual(rect, state, r);
                    DrawButtonLabel(rect, labelContent, def.GetLabelStyle(state), null,
                                    ZIconPlacement.LeftOfLabel, def, def.GetText(state));
                }
            }
            else
            {
                // Fallback
                var s = isSel ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Toggle(isSel, labels[i], s, options) && !isSel)
                    selected = i;
            }
        }
        GUILayout.EndHorizontal();
        return selected;
    }

    public static int MiniRadio(Rect rect, int selected, string[] labels,
                                 string style = Style.Default)
    {
        var sheet = ActiveSheet;
        var def   = sheet?.FindButton(style);
        float itemW = rect.width / labels.Length;

        for (int i = 0; i < labels.Length; i++)
        {
            var itemRect = new Rect(rect.x + i * itemW, rect.y, itemW, rect.height);
            bool isSel = (i == selected);

            if (def != null)
            {
                int id = GUIUtility.GetControlID(FocusType.Passive, itemRect);
                bool hover = itemRect.Contains(Event.current.mousePosition);
                TweenNotifyHover(id, hover, def);

                if (Event.current.type == EventType.MouseDown && hover && Event.current.button == 0)
                {
                    selected = i;
                    GUI.changed = true;
                    Event.current.Use();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
                    var state = isSel ? ZUIButtonDrawState.Active
                              : hover ? ZUIButtonDrawState.Hover
                              : ZUIButtonDrawState.Normal;
                    def.DrawVisual(itemRect, state, r);
                    DrawButtonLabel(itemRect, new GUIContent(labels[i]),
                                    def.GetLabelStyle(state), null,
                                    ZIconPlacement.LeftOfLabel, def, def.GetText(state));
                }
            }
            else
            {
                if (GUI.Toggle(itemRect, isSel, labels[i], EditorStyles.miniButton) && !isSel)
                    selected = i;
            }
        }
        return selected;
    }

    public static int MiniRadioVertical(int selected, string[] labels,
                                         string style = Style.Default,
                                         params GUILayoutOption[] options)
    {
        GUILayout.BeginVertical();
        for (int i = 0; i < labels.Length; i++)
        {
            bool isSel = (i == selected);
            if (Toggle(isSel, labels[i], style, options) && !isSel)
                selected = i;
        }
        GUILayout.EndVertical();
        return selected;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static GUILayoutOption[] AppendMinWidth(GUILayoutOption[] options, float minW)
    {
        var result = new GUILayoutOption[options.Length + 1];
        options.CopyTo(result, 0);
        result[options.Length] = GUILayout.MinWidth(minW);
        return result;
    }
}
