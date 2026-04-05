// MixdownZUIBootstrap.cs
// Registers the Mixdown ZUI style sheet on domain reload.

using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class MixdownZUIBootstrap
{
    internal const string k_SheetPath = "Assets/Mixdown/SystemAssets/Mixdown Sheet.asset";

    static MixdownZUIBootstrap()
    {
        EditorApplication.delayCall += Register;
    }

    static void Register()
    {
        var sheet = FindOrCreateSheet();
        if (sheet != null)
            ZUI.RegisterConsumerSheet("Mixdown", sheet);
    }

    static ZUIStyleSheetAsset FindOrCreateSheet()
    {
        var sheet = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(k_SheetPath);
        if (sheet != null) return sheet;
        return CreateDefaultSheet();
    }

    static ZUIStyleSheetAsset CreateDefaultSheet()
    {
        string dir = Path.GetDirectoryName(k_SheetPath).Replace('\\', '/');
        EnsureFolder(dir);

        var sheet = ScriptableObject.CreateInstance<ZUIStyleSheetAsset>();
        sheet.EnsureDefaults();

        // Mixdown uses a warm/orange palette — visually distinct from Zounds' blue
        sheet.palette.Add(new ZUIPaletteColor { name = "Bg",     color = new Color(.18f, .15f, .13f, 1f), highlight = new Color(.26f, .22f, .18f, 1f), shade = new Color(.10f, .08f, .07f, 1f) });
        sheet.palette.Add(new ZUIPaletteColor { name = "Accent", color = new Color(.85f, .50f, .20f, 1f), highlight = new Color(.95f, .65f, .35f, 1f), shade = new Color(.55f, .30f, .10f, 1f) });
        sheet.palette.Add(new ZUIPaletteColor { name = "Text",   color = new Color(.90f, .88f, .82f, 1f), highlight = new Color(1f, 1f, .95f, 1f),     shade = new Color(.55f, .52f, .45f, 1f) });

        AssetDatabase.CreateAsset(sheet, k_SheetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Mixdown] Created default ZUI style sheet at {k_SheetPath}");
        return sheet;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
