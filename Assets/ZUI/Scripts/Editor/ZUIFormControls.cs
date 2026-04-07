// ZUIFormControls.cs
// Typed control objects for ZUI.Form. Value types use getter/setter delegates.
// Reference types can be passed directly.
//
// Each control implements IZUIControl which the Form system uses to draw.

using System;
using UnityEditor;
using UnityEngine;

// ── Interface ────────────────────────────────────────────────────────────────

public interface IZUIControl
{
    void Draw();
}

// ── Slider (float) ───────────────────────────────────────────────────────────

public class ZUISliderControl : IZUIControl
{
    Func<float> _get;
    Action<float> _set;
    float _min, _max;
    string _style;

    public ZUISliderControl(Func<float> get, Action<float> set, float min, float max, string style = null)
    {
        _get = get; _set = set; _min = min; _max = max; _style = style;
    }

    public void Draw()
    {
        float val = _get();
        float next;
        if (!string.IsNullOrEmpty(_style))
            next = ZUI.Slider(val, _min, _max, "", _style);
        else
            next = EditorGUILayout.Slider(val, _min, _max);
        if (next != val) _set(next);
    }
}

// ── IntSlider ────────────────────────────────────────────────────────────────

public class ZUIIntSliderControl : IZUIControl
{
    Func<int> _get;
    Action<int> _set;
    int _min, _max;

    public ZUIIntSliderControl(Func<int> get, Action<int> set, int min, int max)
    {
        _get = get; _set = set; _min = min; _max = max;
    }

    public void Draw()
    {
        int val = _get();
        int next = EditorGUILayout.IntSlider(val, _min, _max);
        if (next != val) _set(next);
    }
}

// ── Toggle (bool) ────────────────────────────────────────────────────────────

public class ZUIToggleControl : IZUIControl
{
    Func<bool> _get;
    Action<bool> _set;
    string _label;
    string _style;

    public ZUIToggleControl(Func<bool> get, Action<bool> set, string label = "", string style = null)
    {
        _get = get; _set = set; _label = label; _style = style;
    }

    public void Draw()
    {
        bool val = _get();
        bool next;
        if (!string.IsNullOrEmpty(_style))
            next = ZUI.Toggle(val, _label, _style);
        else if (!string.IsNullOrEmpty(_label))
            next = EditorGUILayout.Toggle(val);
        else
            next = EditorGUILayout.Toggle(val);
        if (next != val) _set(next);
    }
}

// ── FloatField ───────────────────────────────────────────────────────────────

public class ZUIFloatFieldControl : IZUIControl
{
    Func<float> _get;
    Action<float> _set;
    float? _width;

    public ZUIFloatFieldControl(Func<float> get, Action<float> set, float? width = null)
    {
        _get = get; _set = set; _width = width;
    }

    public void Draw()
    {
        float val = _get();
        float next = _width.HasValue
            ? EditorGUILayout.FloatField(val, GUILayout.Width(_width.Value))
            : EditorGUILayout.FloatField(val);
        if (next != val) _set(next);
    }
}

// ── IntField ─────────────────────────────────────────────────────────────────

public class ZUIIntFieldControl : IZUIControl
{
    Func<int> _get;
    Action<int> _set;
    float? _width;

    public ZUIIntFieldControl(Func<int> get, Action<int> set, float? width = null)
    {
        _get = get; _set = set; _width = width;
    }

    public void Draw()
    {
        int val = _get();
        int next = _width.HasValue
            ? EditorGUILayout.IntField(val, GUILayout.Width(_width.Value))
            : EditorGUILayout.IntField(val);
        if (next != val) _set(next);
    }
}

// ── ColorField ───────────────────────────────────────────────────────────────

public class ZUIColorFieldControl : IZUIControl
{
    Func<Color> _get;
    Action<Color> _set;

    public ZUIColorFieldControl(Func<Color> get, Action<Color> set)
    {
        _get = get; _set = set;
    }

    public void Draw()
    {
        Color val = _get();
        Color next = EditorGUILayout.ColorField(val);
        if (next != val) _set(next);
    }
}

// ── CycleButton ──────────────────────────────────────────────────────────────

public class ZUICycleButtonControl : IZUIControl
{
    Func<int> _get;
    Action<int> _set;
    string[] _labels;
    string _style;

    public ZUICycleButtonControl(Func<int> get, Action<int> set, string[] labels, string style = null)
    {
        _get = get; _set = set; _labels = labels;
        _style = style ?? ZUI.Style.Default;
    }

    public void Draw()
    {
        int val = _get();
        int next = ZUI.CycleButton(val, _labels, _style);
        if (next != val) _set(next);
    }
}

// ── Popup (dropdown) ─────────────────────────────────────────────────────────

public class ZUIPopupControl : IZUIControl
{
    Func<int> _get;
    Action<int> _set;
    string[] _options;

    public ZUIPopupControl(Func<int> get, Action<int> set, string[] options)
    {
        _get = get; _set = set; _options = options;
    }

    public void Draw()
    {
        int val = _get();
        int next = EditorGUILayout.Popup(val, _options);
        if (next != val) _set(next);
    }
}

// ── ZUI factory methods ──────────────────────────────────────────────────────

public static partial class ZUI
{
    // Value-type controls — getter/setter delegates

    public static ZUISliderControl Slider(Func<float> get, Action<float> set, float min, float max, string style = null)
        => new ZUISliderControl(get, set, min, max, style);

    public static ZUIIntSliderControl IntSlider(Func<int> get, Action<int> set, int min, int max)
        => new ZUIIntSliderControl(get, set, min, max);

    public static ZUIToggleControl Toggle(Func<bool> get, Action<bool> set, string label = "", string style = null)
        => new ZUIToggleControl(get, set, label, style);

    public static ZUIFloatFieldControl FloatField(Func<float> get, Action<float> set, float? width = null)
        => new ZUIFloatFieldControl(get, set, width);

    public static ZUIIntFieldControl IntField(Func<int> get, Action<int> set, float? width = null)
        => new ZUIIntFieldControl(get, set, width);

    public static ZUIColorFieldControl ColorField(Func<Color> get, Action<Color> set)
        => new ZUIColorFieldControl(get, set);

    public static ZUICycleButtonControl CycleButton(Func<int> get, Action<int> set, string[] labels, string style = null)
        => new ZUICycleButtonControl(get, set, labels, style);

    public static ZUIPopupControl Popup(Func<int> get, Action<int> set, string[] options)
        => new ZUIPopupControl(get, set, options);
}
