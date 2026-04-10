// ZUIBlocks.cs
// Height-matched horizontal cell layout for ZUI.
//
// Cells sit side-by-side. All cells are forced to the height of the tallest cell.
// Each cell can align its content vertically: Top, Center, Bottom, or Spread.
//
// Heights are cached from the previous frame to avoid flicker. On the first frame
// the layout may be slightly off, but EditorWindow repaints correct it immediately.
//
// Usage:
//   using (var blocks = ZUI.Blocks("my_shadow_layout"))
//   {
//       using (blocks.Cell(ZUIAlign.Top))
//       {
//           shadow.offset = ZUI.Slider2D(...);
//       }
//       using (blocks.Cell(ZUIAlign.Spread))
//       {
//           ZUI.MicroSlider(...);
//           ZUI.MicroSlider(...);
//       }
//   }

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum ZUIAlign { Top, Center, Bottom, Spread, Even }

public class ZUIBlocksScope : IDisposable
{
    string _key;
    int _cellIndex;
    float _measuredMaxHeight;
    float _cachedMaxHeight;
    List<float> _cellHeights = new List<float>();

    // Cross-frame height cache keyed by layout key
    static readonly Dictionary<string, float> s_heightCache = new Dictionary<string, float>();

    /// <summary>Clears cached height for a key. Call when the layout context changes (e.g. section collapsed/expanded).</summary>
    public static void InvalidateCache(string key)
    {
        if (key != null) s_heightCache.Remove(key);
    }

    internal ZUIBlocksScope(string key)
    {
        _key = key ?? "ZUIBlocks_default";
        _cellIndex = 0;
        _measuredMaxHeight = 0f;
        s_heightCache.TryGetValue(_key, out _cachedMaxHeight);
        if (_cachedMaxHeight < 1f) _cachedMaxHeight = 0f; // no cache yet — first frame

        GUILayout.BeginHorizontal();
    }

    public CellScope Cell(ZUIAlign align = ZUIAlign.Top, params GUILayoutOption[] options)
    {
        if (_cellIndex > 0)
            ZUI.HorizontalSpace("H Control Gap");

        return new CellScope(this, align, _cellIndex++, options);
    }

    internal void RecordCellHeight(float height)
    {
        _cellHeights.Add(height);
        if (height > _measuredMaxHeight)
            _measuredMaxHeight = height;
    }

    internal float GetTargetHeight()
    {
        // Use cached height from previous frame if available, otherwise 0 (unconstrained)
        return _cachedMaxHeight;
    }

    public void Dispose()
    {
        // Push cells to the left — prevent auto-expanding to fill width
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // Update cache with this frame's measured max height
        if (_measuredMaxHeight > 0f)
            s_heightCache[_key] = _measuredMaxHeight;
    }

    // ── CellScope ────────────────────────────────────────────────────────────

    public struct CellScope : IDisposable
    {
        ZUIBlocksScope _parent;
        ZUIAlign _align;

        internal CellScope(ZUIBlocksScope parent, ZUIAlign align, int index, GUILayoutOption[] options)
        {
            _parent = parent;
            _align = align;

            float targetH = parent.GetTargetHeight();

            // Always use the same control structure to avoid Layout/Repaint mismatch.
            // When no cached height yet, pass 0 (unconstrained) — IMGUI ignores Height(0).
            var allOptions = new GUILayoutOption[options.Length + 1];
            options.CopyTo(allOptions, 0);
            allOptions[options.Length] = targetH > 0f ? GUILayout.Height(targetH) : GUILayout.Height(0f);
            GUILayout.BeginVertical(allOptions);

            // Pre-alignment spacing — always emit to keep Layout/Repaint consistent
            if (_align == ZUIAlign.Center || _align == ZUIAlign.Bottom || _align == ZUIAlign.Even)
                GUILayout.FlexibleSpace();
        }

        public void Dispose()
        {
            // Post-alignment spacing — always emit to keep Layout/Repaint consistent
            if (_align == ZUIAlign.Center || _align == ZUIAlign.Top || _align == ZUIAlign.Even)
                GUILayout.FlexibleSpace();

            GUILayout.EndVertical();

            // Measure actual height (only valid during Repaint)
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                _parent.RecordCellHeight(rect.height);
            }
        }
    }
}

// ── ZUI factory method ──────────────────────────────────────────────────────

public static partial class ZUI
{
    /// <summary>
    /// Creates a horizontal layout of height-matched cells.
    /// Pass a unique key so heights are cached across frames.
    /// </summary>
    public static ZUIBlocksScope Blocks(string key = null)
    {
        return new ZUIBlocksScope(key);
    }
}
