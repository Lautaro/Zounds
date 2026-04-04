// ZUIAssetLibrary.cs
// Central registry for ZUI icons and fonts. Resolves assets through a layered chain:
//   Icons:  alias → system folder → custom folder → null
//   Fonts:  skin override → alias → system folder → custom folder → sheet default → ZUI default → Unity default
//
// System assets ship with ZUI (Assets/ZUI/SystemAssets/).
// Custom assets live in a user-configured data folder per style sheet.
// Aliases are per-sheet name→path mappings that let users swap assets without changing code.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[Serializable]
public class ZUIAssetAlias
{
    public string name;        // lookup key (e.g. "Edit", "Title Font")
    public string assetPath;   // path to the actual asset (relative to project)

    public ZUIAssetAlias() { }
    public ZUIAssetAlias(string name, string assetPath) { this.name = name; this.assetPath = assetPath; }
}

[Serializable]
public class ZUIFontOverride
{
    public string aliasName;   // which font alias to override in this skin
    public string assetPath;   // skin's replacement font path
}

public static class ZUIAssetLibrary
{
    // ── System asset paths ───────────────────────────────────────────────────
    // These are relative to the project root.
    internal const string k_SystemIconsPath = "Assets/ZUI/SystemAssets/Icons";
    internal const string k_SystemFontsPath = "Assets/ZUI/SystemAssets/Fonts";

    // ZUI's internal default font (hardcoded fallback)
    static Font _zuiDefaultFont;
    public static Font ZUIDefaultFont
    {
        get
        {
            if (_zuiDefaultFont == null)
            {
                // Try loading from system fonts folder
                _zuiDefaultFont = LoadFirstFontInFolder(k_SystemFontsPath);
                // Ultimate fallback: Unity's built-in font
                if (_zuiDefaultFont == null)
                    _zuiDefaultFont = GUI.skin?.font;
            }
            return _zuiDefaultFont;
        }
    }

    // ── Icon resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves an icon by name. Chain: alias → system icons → custom icons → null.
    /// </summary>
    public static Texture2D FindIcon(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var sheet = ZUI.ActiveSheet;

        // 1. Check aliases on the active sheet
        if (sheet != null)
        {
            var alias = sheet.iconAliases?.Find(a => a.name == name);
            if (alias != null && !string.IsNullOrEmpty(alias.assetPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(alias.assetPath);
                if (tex != null) return tex;
            }
        }

        // 2. Check system icons folder (by filename without extension)
        var systemTex = FindTextureInFolder(k_SystemIconsPath, name);
        if (systemTex != null) return systemTex;

        // 3. Check custom data folder (root + Icons/ subfolder)
        if (sheet != null && !string.IsNullOrEmpty(sheet.dataFolderPath))
        {
            var customTex = FindTextureInFolder(sheet.dataFolderPath, name);
            if (customTex != null) return customTex;
            string customIconsPath = Path.Combine(sheet.dataFolderPath, "Icons");
            customTex = FindTextureInFolder(customIconsPath, name);
            if (customTex != null) return customTex;
        }

        // 4. Legacy: try Resources.Load (backward compat with existing Zounds icons)
        var resTex = Resources.Load<Texture2D>(name);
        if (resTex != null) return resTex;

        return null;
    }

    // ── Font resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a font by name. Chain: skin override → alias → system → custom → sheet default → ZUI default.
    /// </summary>
    public static Font FindFont(string name)
    {
        if (string.IsNullOrEmpty(name)) return ResolveDefaultFont();

        var sheet = ZUI.ActiveSheet;

        // 1. Check skin font overrides
        if (sheet != null)
        {
            var skin = sheet.ActiveSkin;
            if (skin != null)
            {
                var ov = skin.fontOverrides?.Find(f => f.aliasName == name);
                if (ov != null && !string.IsNullOrEmpty(ov.assetPath))
                {
                    var font = AssetDatabase.LoadAssetAtPath<Font>(ov.assetPath);
                    if (font != null) return font;
                }
            }
        }

        // 2. Check aliases
        if (sheet != null)
        {
            var alias = sheet.fontAliases?.Find(a => a.name == name);
            if (alias != null && !string.IsNullOrEmpty(alias.assetPath))
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(alias.assetPath);
                if (font != null) return font;
            }
        }

        // 3. System fonts folder
        var systemFont = FindFontInFolder(k_SystemFontsPath, name);
        if (systemFont != null) return systemFont;

        // 4. Custom data folder (root + Fonts/ subfolder)
        if (sheet != null && !string.IsNullOrEmpty(sheet.dataFolderPath))
        {
            var customFont = FindFontInFolder(sheet.dataFolderPath, name);
            if (customFont != null) return customFont;
            string customFontsPath = Path.Combine(sheet.dataFolderPath, "Fonts");
            customFont = FindFontInFolder(customFontsPath, name);
            if (customFont != null) return customFont;
        }

        return ResolveDefaultFont();
    }

    /// <summary>Returns the sheet's default font, or ZUI's default, or Unity's default.</summary>
    public static Font ResolveDefaultFont()
    {
        var sheet = ZUI.ActiveSheet;
        if (sheet != null && sheet.defaultFont != null)
            return sheet.defaultFont;
        return ZUIDefaultFont;
    }

    // ── Scanning helpers ─────────────────────────────────────────────────────

    /// <summary>Returns all available icons (system + custom) as (name, path) pairs.</summary>
    public static List<(string name, string path)> GetAvailableIcons()
    {
        var result = new List<(string, string)>();
        ScanFolder(k_SystemIconsPath, "t:Texture2D", result);
        var sheet = ZUI.ActiveSheet;
        if (sheet != null && !string.IsNullOrEmpty(sheet.dataFolderPath))
        {
            ScanFolder(sheet.dataFolderPath, "t:Texture2D", result);
            ScanFolder(Path.Combine(sheet.dataFolderPath, "Icons"), "t:Texture2D", result);
        }
        return result;
    }

    /// <summary>Returns all available fonts (system + custom) as (name, path) pairs.</summary>
    public static List<(string name, string path)> GetAvailableFonts()
    {
        var result = new List<(string, string)>();
        ScanFolder(k_SystemFontsPath, "t:Font", result);
        var sheet = ZUI.ActiveSheet;
        if (sheet != null && !string.IsNullOrEmpty(sheet.dataFolderPath))
        {
            ScanFolder(sheet.dataFolderPath, "t:Font", result);
            ScanFolder(Path.Combine(sheet.dataFolderPath, "Fonts"), "t:Font", result);
        }
        return result;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    static Texture2D FindTextureInFolder(string folderPath, string name)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return null;
        // Try exact filename match (with common extensions)
        foreach (var ext in new[] { ".png", ".psd", ".jpg", ".tga", "" })
        {
            string path = Path.Combine(folderPath, name + ext);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
        }
        // Try partial match (name contained in filename)
        var guids = AssetDatabase.FindAssets($"t:Texture2D {name}", new[] { folderPath });
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    static Font FindFontInFolder(string folderPath, string name)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return null;
        foreach (var ext in new[] { ".ttf", ".otf", "" })
        {
            string path = Path.Combine(folderPath, name + ext);
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font != null) return font;
        }
        var guids = AssetDatabase.FindAssets($"t:Font {name}", new[] { folderPath });
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return null;
    }

    static Font LoadFirstFontInFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return null;
        var guids = AssetDatabase.FindAssets("t:Font", new[] { folderPath });
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guids[0]));
        return null;
    }

    static void ScanFolder(string folderPath, string filter, List<(string name, string path)> results)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return;
        var guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            results.Add((name, path));
        }
    }
}
