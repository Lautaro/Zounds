// ZUIPaletteMigration.cs
// One-time migration: converts Highlight/Shade slot references to named autocolors.
// Run via: Tools > ZUI > Migrate Palette Slots
// Safe to run multiple times — skips already-migrated entries.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
    public new bool Equals(object x, object y) => ReferenceEquals(x, y);
    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

public static class ZUIPaletteMigration
{
    [MenuItem("Tools/ZUI/Migrate Palette Slots")]
    static void MigrateAllSheets()
    {
        var guids = AssetDatabase.FindAssets("t:ZUIStyleSheetAsset");
        int totalSheets = 0, totalRefs = 0, totalAutoColors = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sheet = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(path);
            if (sheet == null) continue;

            int refs = 0, acs = 0;
            MigrateSheet(sheet, ref refs, ref acs);

            if (refs > 0 || acs > 0)
            {
                EditorUtility.SetDirty(sheet);
                totalSheets++;
                totalRefs += refs;
                totalAutoColors += acs;
                Debug.Log($"[Migration] {path}: created {acs} autocolors, migrated {refs} refs");
            }
        }

        AssetDatabase.SaveAssets();

        if (totalSheets == 0)
            Debug.Log("[Migration] No sheets needed migration.");
        else
            Debug.Log($"[Migration] Done — {totalSheets} sheets, {totalAutoColors} autocolors created, {totalRefs} refs migrated.");
    }

    static void MigrateSheet(ZUIStyleSheetAsset sheet, ref int totalRefs, ref int totalAutoColors)
    {
        if (sheet.palette == null) return;

        // Phase 1: For each palette entry, check if Highlight or Shade slots are referenced.
        // If so, create autocolors from the existing highlight/shade colors.
        foreach (var entry in sheet.palette)
        {
            bool needsHighlight = HasSlotReference(sheet, entry.name, ZUIPaletteSlot.Highlight)
                               || HasSlotReference(sheet, entry.name, ZUIPaletteSlot.Light)
                               || HasSlotReference(sheet, entry.name, ZUIPaletteSlot.Lightest);
            bool needsShade     = HasSlotReference(sheet, entry.name, ZUIPaletteSlot.Shade)
                               || HasSlotReference(sheet, entry.name, ZUIPaletteSlot.Dark)
                               || HasSlotReference(sheet, entry.name, ZUIPaletteSlot.Darkest);

            if (entry.autoColors == null)
                entry.autoColors = new List<ZUIAutoColor>();

            if (needsHighlight && entry.autoColors.Find(a => a.name == "Highlight") == null)
            {
                var ac = ComputeAutoColor("Highlight", entry.color, entry.highlight);
                entry.autoColors.Add(ac);
                totalAutoColors++;
            }

            if (needsShade && entry.autoColors.Find(a => a.name == "Shade") == null)
            {
                var ac = ComputeAutoColor("Shade", entry.color, entry.shade);
                entry.autoColors.Add(ac);
                totalAutoColors++;
            }
        }

        // Phase 2: Rewrite all ZUIColorRef slot references to autoColorRef
        foreach (var b in sheet.buttons)    totalRefs += MigrateRefs(b);
        foreach (var b in sheet.boxes)      totalRefs += MigrateRefs(b);
        foreach (var t in sheet.textStyles) totalRefs += MigrateRefs(t);
        foreach (var s in sheet.sliders)    totalRefs += MigrateRefs(s);
        if (sheet.globalButton != null) totalRefs += MigrateRefs(sheet.globalButton);
        if (sheet.globalBox != null)    totalRefs += MigrateRefs(sheet.globalBox);
    }

    // ── Reverse-compute autocolor mods from base + target ────────────────────

    static ZUIAutoColor ComputeAutoColor(string name, Color baseColor, Color targetColor)
    {
        Color.RGBToHSV(baseColor, out float bH, out float bS, out float bV);
        Color.RGBToHSV(targetColor, out float tH, out float tS, out float tV);

        // Hue: delta mapped to ±1 (±60°)
        float hueDelta = tH - bH;
        if (hueDelta > 0.5f) hueDelta -= 1f;
        if (hueDelta < -0.5f) hueDelta += 1f;
        float hueMod = Mathf.Clamp(hueDelta * 6f, -1f, 1f);

        // Sat/Val: reverse proportional mod
        float satMod = ReverseProportionalMod(bS, tS);
        float valMod = ReverseProportionalMod(bV, tV);

        return new ZUIAutoColor
        {
            name   = name,
            hueMod = RoundMod(hueMod),
            satMod = RoundMod(satMod),
            valMod = RoundMod(valMod),
        };
    }

    static float ReverseProportionalMod(float baseVal, float targetVal)
    {
        float diff = targetVal - baseVal;
        if (Mathf.Abs(diff) < 0.001f) return 0f;

        if (diff > 0f)
        {
            // mod = (target - base) / (1 - base)
            float denom = 1f - baseVal;
            return denom > 0.001f ? Mathf.Clamp(diff / denom, 0f, 1f) : 1f;
        }
        else
        {
            // mod = (target - base) / base
            return baseVal > 0.001f ? Mathf.Clamp(diff / baseVal, -1f, 0f) : -1f;
        }
    }

    static float RoundMod(float v) => Mathf.Round(v * 100f) / 100f;

    // ── Check if any style in the sheet references a specific slot ───────────

    static bool HasSlotReference(ZUIStyleSheetAsset sheet, string paletteName, ZUIPaletteSlot slot)
    {
        foreach (var b in sheet.buttons)    if (HasSlotRef(b, paletteName, slot)) return true;
        foreach (var b in sheet.boxes)      if (HasSlotRef(b, paletteName, slot)) return true;
        foreach (var t in sheet.textStyles) if (HasSlotRef(t, paletteName, slot)) return true;
        foreach (var s in sheet.sliders)    if (HasSlotRef(s, paletteName, slot)) return true;
        if (sheet.globalButton != null && HasSlotRef(sheet.globalButton, paletteName, slot)) return true;
        if (sheet.globalBox != null && HasSlotRef(sheet.globalBox, paletteName, slot)) return true;
        return false;
    }

    static bool HasSlotRef(object obj, string paletteName, ZUIPaletteSlot slot)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return HasSlotRefInner(obj, paletteName, slot, visited);
    }

    static bool HasSlotRefInner(object obj, string paletteName, ZUIPaletteSlot slot, HashSet<object> visited)
    {
        if (obj == null) return false;
        if (!obj.GetType().IsValueType && !visited.Add(obj)) return false;
        foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(ZUIColorRef))
            {
                var cr = (ZUIColorRef)field.GetValue(obj);
                if (cr.paletteRef == paletteName && cr.slot == slot && string.IsNullOrEmpty(cr.autoColorRef))
                    return true;
            }
            else if (IsWalkable(field))
            {
                var sub = field.GetValue(obj);
                if (sub is IList list)
                {
                    foreach (var item in list)
                        if (item != null && HasSlotRefInner(item, paletteName, slot, visited)) return true;
                }
                else if (sub != null && HasSlotRefInner(sub, paletteName, slot, visited))
                    return true;
            }
        }
        return false;
    }

    // ── Rewrite slot references to autoColorRef ──────────────────────────────

    static readonly Dictionary<ZUIPaletteSlot, string> s_slotToAutoColor = new Dictionary<ZUIPaletteSlot, string>
    {
        { ZUIPaletteSlot.Highlight, "Highlight" },
        { ZUIPaletteSlot.Light,     "Highlight" },
        { ZUIPaletteSlot.Lightest,  "Highlight" },
        { ZUIPaletteSlot.Shade,     "Shade" },
        { ZUIPaletteSlot.Dark,      "Shade" },
        { ZUIPaletteSlot.Darkest,   "Shade" },
        { ZUIPaletteSlot.Muted,     "Shade" },
        { ZUIPaletteSlot.Vivid,     "Highlight" },
    };

    static int MigrateRefs(object obj)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return MigrateRefsInner(obj, visited);
    }

    static int MigrateRefsInner(object obj, HashSet<object> visited)
    {
        if (obj == null) return 0;
        if (!obj.GetType().IsValueType && !visited.Add(obj)) return 0;
        int count = 0;
        foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(ZUIColorRef))
            {
                var cr = (ZUIColorRef)field.GetValue(obj);
                if (!string.IsNullOrEmpty(cr.paletteRef)
                    && string.IsNullOrEmpty(cr.autoColorRef)
                    && cr.slot != ZUIPaletteSlot.Primary
                    && s_slotToAutoColor.TryGetValue(cr.slot, out string acName))
                {
                    cr.autoColorRef = acName;
                    cr.slot = ZUIPaletteSlot.Primary;
                    field.SetValue(obj, cr);
                    count++;
                }
            }
            else if (IsWalkable(field))
            {
                var sub = field.GetValue(obj);
                if (sub is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        if (item != null) count += MigrateRefsInner(item, visited);
                    }
                }
                else if (sub != null)
                    count += MigrateRefsInner(sub, visited);
            }
        }
        return count;
    }

    static bool IsWalkable(FieldInfo field)
    {
        if (field.IsNotSerialized) return false;
        var t = field.FieldType;
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Color)
            || t == typeof(Vector2) || t == typeof(Vector4) || t == typeof(Rect))
            return false;
        if (field.Name.StartsWith("_")) return false;
        if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false; // skip ScriptableObject, MonoBehaviour, etc.
        // Walk classes and generic lists
        return t.IsClass || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>));
    }
}
