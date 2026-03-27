// ZUIToggle.cs
// A button-style toggle control. Fully styleable via ZUIButtonDef.
// Visual states: Off = Normal, Hover = Hover, On = Active.
// Returns the new value (toggled when clicked).

using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    // ===== Toggle API — ZToggleStyle (enum-keyed) =============================

    public static bool Toggle(bool value, string label, ZToggleStyle style = ZToggleStyle.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def  = sheet.FindButton(style.ToString());
            var rect = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(), options);
            return DrawManualToggle(rect, new GUIContent(label), value, def);
        }
        return DrawFallbackToggle(value, label, style, options);
    }

    public static bool Toggle(bool value, GUIContent content, ZToggleStyle style = ZToggleStyle.Default, params GUILayoutOption[] options)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
        {
            var def  = sheet.FindButton(style.ToString());
            var rect = GUILayoutUtility.GetRect(content, def.GetLabelStyle(), options);
            return DrawManualToggle(rect, content, value, def);
        }
        return DrawFallbackToggle(value, content.text, style, options);
    }

    public static bool Toggle(Rect rect, bool value, string label, ZToggleStyle style = ZToggleStyle.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualToggle(rect, new GUIContent(label), value, sheet.FindButton(style.ToString()));
        return DrawFallbackToggle(rect, value, label, style);
    }

    public static bool Toggle(Rect rect, bool value, GUIContent content, ZToggleStyle style = ZToggleStyle.Default)
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null)
            return DrawManualToggle(rect, content, value, sheet.FindButton(style.ToString()));
        return DrawFallbackToggle(rect, value, content.text, style);
    }

    // ===== Toggle API — ZUIButtonDef (named style def) ========================

    public static bool Toggle(bool value, string label, ZUIButtonDef def, params GUILayoutOption[] options)
    {
        var rect = GUILayoutUtility.GetRect(MakeContent(label, def), def.GetLabelStyle(), options);
        return DrawManualToggle(rect, new GUIContent(label), value, def);
    }

    public static bool Toggle(bool value, GUIContent content, ZUIButtonDef def, params GUILayoutOption[] options)
    {
        var rect = GUILayoutUtility.GetRect(content, def.GetLabelStyle(), options);
        return DrawManualToggle(rect, content, value, def);
    }

    public static bool Toggle(Rect rect, bool value, string label, ZUIButtonDef def)
        => DrawManualToggle(rect, new GUIContent(label), value, def);

    public static bool Toggle(Rect rect, bool value, GUIContent content, ZUIButtonDef def)
        => DrawManualToggle(rect, content, value, def);

    // ── Core draw ─────────────────────────────────────────────────────────────
    // On  = Active visual state
    // Off = Normal visual state
    // Hover (over either on/off) = Hover visual state
    // Clicking toggles value.

    static bool DrawManualToggle(Rect rect, GUIContent content, bool value, ZUIButtonDef def)
    {
        // ── Style debug ───────────────────────────────────────────────────────
        if (CheckDebugContextClick(rect))
        {
            CollectButtonDebugInfo(def, ZButtonStyle.Default, rect);
            return value;
        }

        if (!GUI.enabled)
        {
            if (Event.current.type == EventType.Repaint)
            {
                int r   = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
                var prev = GUI.color;
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.4f);
                var drawState = value ? ZUIButtonDrawState.Active : ZUIButtonDrawState.Normal;
                def.DrawVisual(rect, drawState, r);
                DrawButtonLabel(rect, content, def.GetLabelStyle(drawState), null, ZIconPlacement.LeftOfLabel, def, def.GetText(drawState));
                GUI.color = prev;
            }
            return value;
        }

        int id = GUIUtility.GetControlID(FocusType.Passive, rect);
        var ev = Event.current;
        bool isHover  = rect.Contains(ev.mousePosition);
        bool isActive = GUIUtility.hotControl == id;
        bool clicked  = false;

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (isHover && ev.button == 0)
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
                    if (isHover) clicked = true;
                }
                break;
        }

        if (ev.type == EventType.Repaint)
        {
            int r = SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
            // Toggles have no hover state — hover is ignored so the On/Off state reads clearly.
            // While pressed, preview the toggled-to state.
            ZUIButtonDrawState drawState;
            if (isActive)
                drawState = value ? ZUIButtonDrawState.Normal : ZUIButtonDrawState.Active; // preview the toggled state while held
            else
                drawState = value ? ZUIButtonDrawState.Active : ZUIButtonDrawState.Normal;

            def.DrawVisual(rect, drawState, r);
            ZUI.DrawFlashOverlayIfNeeded(rect, def.name, r, ZUI.FlashDefType.Button);
            DrawButtonLabel(rect, content, def.GetLabelStyle(drawState), null, ZIconPlacement.LeftOfLabel, def, def.GetText(drawState));
        }

        return clicked ? !value : value;
    }

    // ── Fallback (no sheet) ───────────────────────────────────────────────────

    static bool DrawFallbackToggle(bool value, string label, ZToggleStyle style, GUILayoutOption[] options)
    {
        var s = ToggleStyleRegistry.Get(style, value);
        if (GUILayout.Button(label, s, options)) return !value;
        return value;
    }

    static bool DrawFallbackToggle(Rect rect, bool value, string label, ZToggleStyle style)
    {
        var s = ToggleStyleRegistry.Get(style, value);
        if (GUI.Button(rect, label, s)) return !value;
        return value;
    }

    // ===== ZToggleStyle enum ==================================================

    public enum ZToggleStyle
    {
        Default,
        Subtle,
        Confirm,
        Danger,
        Accent,
    }

    // ===== ToggleStyleRegistry ================================================
    // Fallback when no sheet is loaded. Reuses ButtonStyleRegistry colors.

    static class ToggleStyleRegistry
    {
        public static GUIStyle Get(ZToggleStyle style, bool value)
        {
            // Map to a corresponding button style for fallback rendering.
            // On = Active-tinted; Off = Default/Subtle.
            var buttonStyle = style switch
            {
                ZToggleStyle.Subtle  => value ? ZButtonStyle.Active  : ZButtonStyle.Subtle,
                ZToggleStyle.Confirm => value ? ZButtonStyle.Confirm : ZButtonStyle.Default,
                ZToggleStyle.Danger  => value ? ZButtonStyle.Danger  : ZButtonStyle.Default,
                ZToggleStyle.Accent  => value ? ZButtonStyle.Active  : ZButtonStyle.Default,
                _                    => value ? ZButtonStyle.Active  : ZButtonStyle.Default,
            };
            return ButtonStyleRegistry.Get(buttonStyle);
        }
    }
}
