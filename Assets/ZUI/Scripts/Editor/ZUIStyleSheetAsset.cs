// ZUIStyleSheetAsset.cs
// Extracted into its own file so Unity can resolve the ScriptableObject type reliably.

using System.Collections.Generic;
using UnityEngine;

// ── Missing-style registry ─────────────────────────────────────────────────────
// Records every lookup that fell back to a default because the named style didn't exist.
// Lives as a static class so it persists across repaints but resets on domain reload.

public static class ZUIMissingStyleRegistry
{
    public enum EntryType { Button, Box, Text, Slider }

    public struct Entry
    {
        public EntryType type;
        public string    requestedName;
        public int       hitCount;
    }

    // key = "Button:ZoundBtn" etc.
    private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

    public static IEnumerable<Entry> Entries => _entries.Values;
    public static int Count => _entries.Count;

    public static void Record(EntryType type, string name)
    {
        string key = type + ":" + name;
        if (_entries.TryGetValue(key, out var e))
        {
            e.hitCount++;
            _entries[key] = e;
        }
        else
        {
            _entries[key] = new Entry { type = type, requestedName = name, hitCount = 1 };
        }
    }

    public static void Clear() => _entries.Clear();

    // Called when a style is renamed or added, so stale entries can be removed.
    public static void Remove(EntryType type, string name)
    {
        _entries.Remove(type + ":" + name);
    }
}

[CreateAssetMenu(menuName = "ZUI/Style Sheet", fileName = "ZUIStyleSheet")]
public class ZUIStyleSheetAsset : ScriptableObject
{
    public List<ZUIButtonDef>     buttons    = new List<ZUIButtonDef>();
    public List<ZUIBoxDef>        boxes      = new List<ZUIBoxDef>();
    public List<ZUITextStyleDef>  textStyles = new List<ZUITextStyleDef>();
    public List<ZUISliderDef>     sliders    = new List<ZUISliderDef>();
    public ZUIIconLibraryAsset    iconLibrary;
    public List<ZUIPaletteColor>  palette    = new List<ZUIPaletteColor>();

    // Global defaults — per-def useGlobal* flags pull values from here.
    public ZUIButtonDef globalButton;
    public ZUIBoxDef    globalBox;

    /// <summary>
    /// Default vertical space between rows. Use via ZUI.VerticalSpace().
    /// </summary>
    [Min(0f)]
    public float verticalSpacing = 6f;

    /// <summary>
    /// Default horizontal space between columns/controls. Use via ZUI.HorizontalSpace().
    /// </summary>
    [Min(0f)]
    public float horizontalSpacing = 8f;

    /// <summary>
    /// Named spacing scales. Each is a multiplier applied on top of the base vertical or horizontal
    /// spacing. E.g. scale 0.25 named "EqToolbar" → ZUI.VerticalSpace("EqToolbar") = verticalSpacing × 0.25.
    /// </summary>
    public List<ZUISpacingScale> spacingScales = new List<ZUISpacingScale>();

    public float FindSpacingScale(string name)
    {
        var entry = spacingScales?.Find(s => s.name == name);
        return entry != null ? entry.scale : 1f;
    }

    /// <summary>
    /// Number of flash pulses when flashing a style or spacing marker.
    /// </summary>
    [Min(1)]
    public int flashCount = 8;

    /// <summary>
    /// Duration of each flash pulse in seconds.
    /// </summary>
    [Min(0.02f)]
    public float flashInterval = 0.12f;

    public ZUIPaletteColor FindPaletteColor(string id)
        => palette?.Find(p => p.name == id);

    void OnEnable() => EnsureDefaults();

    public ZUIButtonDef FindButton(string name)
    {
        var found = buttons.Find(b => b.name == name);
        if (found != null) return found;
        ZUIMissingStyleRegistry.Record(ZUIMissingStyleRegistry.EntryType.Button, name);
        // Fall back to the explicitly named "Default", never just buttons[0] (order-dependent).
        return buttons.Find(b => b.name == "Default") ?? (buttons.Count > 0 ? buttons[0] : null);
    }

    public ZUIBoxDef FindBox(string name)
    {
        var found = boxes.Find(b => b.name == name);
        if (found != null) return found;
        ZUIMissingStyleRegistry.Record(ZUIMissingStyleRegistry.EntryType.Box, name);
        return boxes.Find(b => b.name == "Default") ?? (boxes.Count > 0 ? boxes[0] : null);
    }

    public ZUITextStyleDef FindText(string name)
    {
        var found = textStyles.Find(t => t.name == name);
        if (found != null) return found;
        ZUIMissingStyleRegistry.Record(ZUIMissingStyleRegistry.EntryType.Text, name);
        return textStyles.Find(t => t.name == "Default") ?? (textStyles.Count > 0 ? textStyles[0] : null);
    }

    public ZUISliderDef FindSlider(string name)
    {
        if (sliders == null) return null;
        var found = sliders.Find(s => s.name == name);
        if (found != null) return found;
        ZUIMissingStyleRegistry.Record(ZUIMissingStyleRegistry.EntryType.Slider, name);
        return sliders.Find(s => s.name == "Default") ?? (sliders.Count > 0 ? sliders[0] : null);
    }

    public void EnsureDefaults()
    {
        if (buttons == null) buttons = new List<ZUIButtonDef>();
        if (boxes   == null) boxes   = new List<ZUIBoxDef>();

        void EnsureBtn(string n, Color norm, Color hov, Color act, Color txt)
        {
            if (buttons.Find(b => b.name == n) == null)
                buttons.Add(new ZUIButtonDef(n, norm, hov, act, txt));
        }

        void EnsureBox(string n, Color bg, Color label, Color border, float bw, int pH, int pV)
        {
            if (boxes.Find(b => b.name == n) == null)
                boxes.Add(new ZUIBoxDef(n, bg, label, border, bw, pH, pV));
        }

        EnsureBtn("Default",
            new Color(.22f, .22f, .26f, 1f), new Color(.30f, .30f, .36f, 1f),
            new Color(.16f, .16f, .20f, 1f), new Color(.88f, .88f, .88f, 1f));
        EnsureBtn("Confirm",
            new Color(.14f, .34f, .14f, 1f), new Color(.18f, .44f, .18f, 1f),
            new Color(.10f, .24f, .10f, 1f), new Color(.72f, 1f,   .72f, 1f));
        EnsureBtn("Danger",
            new Color(.40f, .12f, .10f, 1f), new Color(.54f, .16f, .13f, 1f),
            new Color(.28f, .08f, .07f, 1f), new Color(1f,   .72f, .70f, 1f));
        EnsureBtn("Subtle",
            new Color(.20f, .20f, .20f, .30f), new Color(.30f, .30f, .30f, .45f),
            new Color(.14f, .14f, .14f, .40f), new Color(.65f, .65f, .65f, 1f));
        EnsureBtn("Active",
            new Color(.20f, .38f, .55f, 1f), new Color(.25f, .46f, .65f, 1f),
            new Color(.14f, .28f, .42f, 1f), new Color(.75f, .92f, 1f,   1f));
        EnsureBtn("Alternative",
            new Color(.55f, .38f, .10f, 1f), new Color(.68f, .48f, .14f, 1f),
            new Color(.40f, .28f, .08f, 1f), new Color(1f,   .88f, .55f, 1f));
        EnsureBtn("Cancel",
            new Color(.38f, .15f, .15f, 1f), new Color(.48f, .20f, .20f, 1f),
            new Color(.28f, .10f, .10f, 1f), new Color(.95f, .70f, .70f, 1f));

        EnsureBox("Default",
            new Color(.18f, .18f, .22f, 1f), new Color(.90f, .90f, .90f, 1f),
            new Color(1f,   1f,   1f,   .06f), 1f, 8, 6);
        EnsureBox("Alternative",
            new Color(.16f, .20f, .28f, 1f), new Color(.80f, .88f, 1f,   1f),
            new Color(.40f, .60f, 1f,   .15f), 1f, 8, 6);
        EnsureBox("Warning",
            new Color(.28f, .12f, .10f, 1f), new Color(1f,   .78f, .68f, 1f),
            new Color(1f,   .40f, .30f, .30f), 1f, 8, 6);
        EnsureBox("Subtle",
            new Color(.15f, .15f, .17f, 1f), new Color(.70f, .70f, .70f, 1f),
            new Color(1f,   1f,   1f,   .03f), 1f, 8, 6);
        EnsureBox("Info",
            new Color(.12f, .20f, .30f, 1f), new Color(.70f, .88f, 1f,   1f),
            new Color(.30f, .60f, 1f,   .25f), 1f, 8, 6);
        EnsureBox("Alternative2",
            new Color(.16f, .26f, .16f, 1f), new Color(.72f, 1f,   .72f, 1f),
            new Color(.30f, .70f, .30f, .20f), 1f, 8, 6);
        EnsureBox("Alternate",
            new Color(.24f, .18f, .28f, 1f), new Color(.90f, .78f, 1f,   1f),
            new Color(.70f, .40f, 1f,   .20f), 1f, 8, 6);

        if (globalButton == null) globalButton = new ZUIButtonDef("Global",
            new Color(.22f, .22f, .26f, 1f), new Color(.30f, .30f, .36f, 1f),
            new Color(.16f, .16f, .20f, 1f), new Color(.88f, .88f, .88f, 1f));
        if (globalBox == null) globalBox = new ZUIBoxDef("Global",
            new Color(.18f, .18f, .22f, 1f), new Color(.90f, .90f, .90f, 1f),
            new Color(1f,   1f,   1f,   .06f), 1f, 8, 6);

        if (palette       == null) palette       = new List<ZUIPaletteColor>();
        if (spacingScales == null) spacingScales = new List<ZUISpacingScale>();
        if (sliders    == null) sliders    = new List<ZUISliderDef>();
        if (sliders.Find(s => s.name == "Default") == null)
            sliders.Add(new ZUISliderDef { name = "Default" });
        if (textStyles == null) textStyles = new List<ZUITextStyleDef>();
        void EnsureText(string n, Color col, int fs = 0, FontStyle fst = FontStyle.Normal)
        {
            if (textStyles.Find(t => t.name == n) == null)
                textStyles.Add(new ZUITextStyleDef { name = n, text = new ZUITextDef(col) { fontSize = fs, fontStyle = fst } });
        }
        EnsureText("Default",   new Color(.88f, .88f, .88f, 1f));
        EnsureText("Header",    new Color(.95f, .95f, .95f, 1f), 14, FontStyle.Bold);
        EnsureText("Subheader", new Color(.90f, .90f, .90f, 1f), 0,  FontStyle.Bold);
        EnsureText("Small",     new Color(.70f, .70f, .70f, 1f), 9,  FontStyle.Normal);
        EnsureText("Subtle",    new Color(.55f, .55f, .55f, 1f));
        EnsureText("Accent",    new Color(.70f, .88f, 1f,   1f));
    }
}
