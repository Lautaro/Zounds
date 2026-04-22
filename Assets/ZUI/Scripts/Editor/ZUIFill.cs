// ZUIFill.cs
// Unified fill editor for ZUI. Edits a ZUIGradient which can be solid color or gradient.
//
// Usage (in style editor):
//   bool changed = ZUI.Fill(gradient, onOpenStopEditor);
//   bool changed = ZUI.Fill(gradient, onOpenStopEditor, allowGradient: false); // solid only
//
// Layout:
//   [Single|Gradient] toggle
//
//   Single mode:
//     [ZUI.ColorPicker for colorA]
//
//   Gradient mode:
//     [Linear|Radial|Fixed] sub-mode
//     Per-mode controls (angle, curve, shape, edge toggles)
//     [Gradient stop preview bar] → click opens stop editor via callback

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ZUI
{
    static readonly string[] s_fillModeLabels = { "Single", "Gradient" };
    static readonly string[] s_gradientSubModeLabels = { "Linear", "Radial", "Fixed" };
    static readonly string[] s_gradientSubModeLabelsNoFixed = { "Linear", "Radial" };
    static readonly string[] s_radialShapeLabels = { "Ellipse", "Square", "Shape" };

    /// <summary>
    /// Draws a fill editor for a ZUIGradient. Returns true if the value changed.
    /// </summary>
    /// <param name="g">The gradient to edit.</param>
    /// <param name="onOpenStopEditor">Callback to open the gradient stop editor popup, receiving the bar rect.</param>
    /// <param name="allowGradient">If false, only solid color mode is available.</param>
    /// <param name="hidePxEdge">If true, the Fixed/pixel-edge mode is hidden.</param>
    public static bool Fill(ZUIGradient g, Action<Rect> onOpenStopEditor = null,
                             bool allowGradient = true, bool hidePxEdge = false,
                             List<ZUIPaletteColor> paletteOverride = null)
    {
        bool changed = false;

        if (allowGradient)
        {
            // Mode toggle: Single / Gradient
            int mode = g.isGradient ? 1 : 0;
            EditorGUI.BeginChangeCheck();
            int newMode = MiniRadio(mode, s_fillModeLabels, "Toggle");
            if (EditorGUI.EndChangeCheck() && newMode != mode)
            {
                g.isGradient = newMode == 1;
                if (g.isGradient && (g.stops == null || g.stops.Count < 2))
                {
                    g.stops = new List<ZUIGradientStop>
                    {
                        new ZUIGradientStop(g.colorA, 0f, 0.5f),
                        new ZUIGradientStop(g.colorB, 1f, g.bias),
                    };
                }
                g.Invalidate();
                changed = true;
            }
            VerticalSpace("V Control Gap");
        }

        if (!g.isGradient)
        {
            // Solid mode: color picker
            if (ColorPicker(ref g.colorA, paletteOverride))
            {
                g.Invalidate();
                changed = true;
            }
        }
        else
        {
            // Gradient mode
            changed |= DrawFillGradientControls(g, onOpenStopEditor, hidePxEdge);
        }

        return changed;
    }

    static bool DrawFillGradientControls(ZUIGradient g, Action<Rect> onOpenStopEditor, bool hidePxEdge)
    {
        bool changed = false;

        // Sub-mode toggle: Linear / Radial / Fixed
        int subMode = g.isRadial ? 1 : (g.usePixelLength ? 2 : 0);
        string[] subLabels = hidePxEdge ? s_gradientSubModeLabelsNoFixed : s_gradientSubModeLabels;

        EditorGUI.BeginChangeCheck();
        int newSubMode = MiniRadio(subMode, subLabels, "Toggle");
        if (EditorGUI.EndChangeCheck() && newSubMode != subMode)
        {
            g.isRadial = newSubMode == 1;
            g.usePixelLength = newSubMode == 2;
            g.Invalidate();
            changed = true;
        }

        VerticalSpace("V Control Gap");

        // Per-mode controls
        EditorGUI.BeginChangeCheck();
        if (newSubMode == 0) // Linear
        {
            DrawFillLinearControls(g);
        }
        else if (newSubMode == 1) // Radial
        {
            DrawFillRadialControls(g);
        }
        else // Fixed
        {
            DrawFillFixedControls(g);
        }
        if (EditorGUI.EndChangeCheck()) { g.Invalidate(); changed = true; }

        VerticalSpace("V Control Gap");

        // Gradient stop preview bar
        changed |= DrawFillStopBar(g, onOpenStopEditor);

        // Sync colorA/colorB from stops
        if (g.stops != null && g.stops.Count >= 2)
        {
            g.colorA = g.stops[0].color;
            g.colorB = g.stops[g.stops.Count - 1].color;
        }

        return changed;
    }

    static void DrawFillLinearControls(ZUIGradient g)
    {
        g.angle = MicroSlider(g.angle, 0f, 360f, "Angle");
        if (!g.HasMultipleStops)
        {
            VerticalSpace("V Control Gap");
            g.bias = MicroSlider(g.bias, 0f, 1f, "Curve");
        }
    }

    static void DrawFillRadialControls(ZUIGradient g)
    {
        int shape = MiniRadio(g.radialShape, s_radialShapeLabels, "Toggle");
        if (shape != g.radialShape) g.radialShape = shape;
        if (!g.HasMultipleStops)
        {
            VerticalSpace("V Control Gap");
            g.bias = MicroSlider(g.bias, 0f, 1f, "Curve");
        }
    }

    static void DrawFillFixedControls(ZUIGradient g)
    {
        g.pixelLength = Mathf.Max(1, Mathf.RoundToInt(MicroSlider(g.pixelLength, 1f, 64f, "Length")));

        if (!g.HasMultipleStops)
        {
            VerticalSpace("V Control Gap");
            g.bias = MicroSlider(g.bias, 0f, 1f, "Curve");
        }

        VerticalSpace("V Control Gap");

        // Edge toggles — shaped row with arrow icons
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Edges", RowLabelStyle, GUILayout.Width(36f));
        bool[] edgeValues =
        {
            (g.pixelEdges & ZUIPixelEdges.Left)   != 0,
            (g.pixelEdges & ZUIPixelEdges.Right)  != 0,
            (g.pixelEdges & ZUIPixelEdges.Bottom) != 0,
            (g.pixelEdges & ZUIPixelEdges.Top)    != 0,
        };
        GUIContent[] edgeLabels =
        {
            new GUIContent("\u2190", "Left edge"),
            new GUIContent("\u2192", "Right edge"),
            new GUIContent("\u2193", "Bottom edge"),
            new GUIContent("\u2191", "Top edge"),
        };
        edgeValues = ToggleRow(edgeValues, edgeLabels, "Toggle", GUILayout.Width(26f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        g.pixelEdges = (edgeValues[0] ? ZUIPixelEdges.Left   : 0)
                     | (edgeValues[1] ? ZUIPixelEdges.Right  : 0)
                     | (edgeValues[2] ? ZUIPixelEdges.Bottom : 0)
                     | (edgeValues[3] ? ZUIPixelEdges.Top    : 0);
    }

    static bool DrawFillStopBar(ZUIGradient g, Action<Rect> onOpenStopEditor)
    {
        bool changed = false;

        var barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.Height(18f), GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Always show linear preview for the stop bar
            bool wasRadial = g.isRadial;
            bool wasPxLen = g.usePixelLength;
            g.isRadial = false;
            g.usePixelLength = false;
            g.Invalidate();
            var previewTex = g.GetOrBuildTexture(ActiveSheet);
            g.isRadial = wasRadial;
            g.usePixelLength = wasPxLen;
            g.Invalidate();

            if (previewTex != null)
                GUI.DrawTexture(barRect, previewTex, ScaleMode.StretchToFill, true);

            // Border
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1f), new Color(0f, 0f, 0f, 0.4f));
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1f, barRect.width, 1f), new Color(0f, 0f, 0f, 0.4f));

            // Stop markers
            var effectiveStops = g.GetEffectiveStops();
            foreach (var stop in effectiveStops)
            {
                float x = barRect.x + stop.position * barRect.width;
                EditorGUI.DrawRect(new Rect(x - 1f, barRect.y - 2f, 3f, barRect.height + 4f),
                    new Color(1f, 1f, 1f, 0.8f));
            }
        }

        // Click → open stop editor
        if (onOpenStopEditor != null &&
            Event.current.type == EventType.MouseDown && barRect.Contains(Event.current.mousePosition))
        {
            onOpenStopEditor(barRect);
            Event.current.Use();
        }

        return changed;
    }
}
