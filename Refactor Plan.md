# Zound Browser Refactor Plan

---

## Phase 1 — Renames

### 1a. `ConsolidatedTab` → `AllZoundsTab`
- Rename class and file
- Update static `instance` field and `Instance` property
- Update all callers: `ConsolidatedTab.Instance`, `.OpenAddNewZoundMenu`, `.OnKlipAdded`, `.OnZequenceAdded`, `.OpenCreateNewKlipDialog`

### 1b. `ZoundInspector<TZound>` → `ZoundBrowserEditor<TZound>`
- Rename class and file
- Update field on `BaseZoundTab`: `zoundInspector` → `zoundBrowserEditor`
- Update all call sites: `.DrawMulticolumn`, `.DrawZoundSinglecolumn`, `.GetLastTagsWidth`, `.GetTagsLabelStyle`

> **⏸ Test.** Open Zounds window, switch list/grid mode, verify all browser tabs (Zounds, Klips, Zequences, Muzics). No behavior change.

---

## Phase 2 — `ZoundListRowLayout` struct

### 2a. Define `struct ZoundListRowLayout` on `BaseZoundTab`

Holds all geometry derived from `browserSettings` + `itemWidth` + `rowRect`:

- **Settings-derived** (computable before the loop): `editRectWidth`, `muteSoloWidthSingle`, `leftTotalEst`, `removeRectWidth`, `minInspectorWidth`, `tagsZoneWidth`, `tagsGap`
- **Rect-derived** (resolved per-row after `GetRect`): `middleRight`, `multipleRows`, `muteSoloRectWidth`, `editToMSGap`, `leftButtonsWidth`, `leftGap`, and all final `Rect` fields: `editButtonRect`, `muteSoloRect`, `nameButtonRect`, `inspectorRect`, `removeButtonRect`, `tagsRect`, `row2Rect`, `itemAreaRect`
- **Frame-persistent cache**: `lastValidSize` — moves here from the `BaseZoundTab` instance field

The struct is a field on `BaseZoundTab`, not created per-frame, so `lastValidSize` persists correctly.

### 2b. Compute settings-dependent part in `DrawZoundsSinglecolumn` before the loop

After `itemWidth` is resolved, call `layout = ComputeListRowLayout(itemWidth, browserSettings)`. Rect-dependent fields are resolved inside the per-row call once `GUILayoutUtility.GetRect` runs.

### 2c. `DrawSinglecolumnRow` signature becomes `(List<Zound>, int, int, ref ZoundListRowLayout)`

Method body becomes: GetRect → update `layout.lastValidSize` → resolve rect fields → draw. No interleaved computation.

> **⏸ Test.** List mode identical. Resize window — two-row mode flips correctly. No layout jitter on first frame.

---

## Phase 3 — Extract `ZoundListItemView`

### 3a. Create `ZoundListItemView.cs`

Move the drawing portion of `DrawSinglecolumnRow` (everything from "Step 8: draw" onwards) into a static class:

```csharp
internal static class ZoundListItemView {
    public static void Draw(
        List<Zound> filteredList, int selectedIndex, int currentIndex,
        ref ZoundListRowLayout layout,
        ZoundBrowserEditor<TZound> editor,
        BaseZoundTab<TZound> tab)
    { ... }
}
```

`DrawSinglecolumnRow` becomes: compute layout rects, call `ZoundListItemView.Draw(...)`.

Methods that stay on `BaseZoundTab` (orchestrator-level, passed via `tab`):
`TryGetAnyInstanceToken`, `UpdateZoundButtonPulse`, `DrawMuteSoloBackground`, `DrawMuteSoloIndicator`, `ZoundPulseKey`, `SelectZound`, `CopyToClipboard`.

> **⏸ Test.** Full list mode regression. Play from list, right-click select, resize, missing zound display, mute/solo indicators.

---

## Phase 4 — Extract `ZoundGridItemView`

### 4a. Move `HandleZoundButtonMulticolumn` into `ZoundGridItemView`

```csharp
internal static class ZoundGridItemView {
    public static void DrawButton(
        List<Zound> filteredList, int selectedIndex, int currentIndex,
        float itemWidth, ZoundToken token, Event evt,
        BaseZoundTab<TZound> tab)
    { ... }
}
```

`DrawMulticolumnRow` and `DrawFlowRow` stay on `BaseZoundTab` for now. They call `ZoundGridItemView.DrawButton(...)` in place of `HandleZoundButtonMulticolumn(...)`.

> **⏸ Test.** Full grid mode regression. Fixed, Auto, Min modes. Play, right-click select, inspector panel open/close, pulse, missing zound boxes, mute/solo indicators.

---

## Phase 5 — Move grid flow logic into `ZoundGridItemView`

### 5a. Move `DrawMulticolumnRow` and `DrawFlowRow` into `ZoundGridItemView`

Both methods own grid-mode rendering policy (wrapping, centering, inspector-pending tracking) — they belong with the grid view, not the browser orchestrator.

`DrawZoundsMulticolumn` on `BaseZoundTab` calls `ZoundGridItemView.DrawFixedRow(...)` and `ZoundGridItemView.DrawFlowRow(...)` instead. `BaseZoundTab` retains `DrawZoundsMulticolumn` as the scroll-container and dispatcher only.

> **⏸ Test.** Grid mode full regression again. Inspect panel animated open/close, group headers, all size modes.

---

## Phase 6 — Optional: `ZoundEditorColumnLayout` for `ZoundBrowserEditor.DrawMulticolumn`

Extract column-width arithmetic from `DrawMulticolumn` (the `fieldWidthMultiplier`, `tagsWidthMultiplier`, `leftButtonsWidth`, `removeRectWidth`, column-0 through column-4 width computations) into a `struct ZoundEditorColumnLayout`. Resolve to absolute `inspectorColumns[0-4]` rects after `GetRect` gives the origin.

Lower priority — do when `DrawMulticolumn` is being touched for another reason.

> **⏸ Test.** Grid inspector panel layout in all field-visibility configurations.

---

## Phase 7 — Move `ZoundsEditorColors` to `ZoundBrowserPlaybackVisuals`

### 7a. Create `ZoundBrowserPlaybackVisuals.cs`

Move `ZoundsEditorColors` there. If `UpdateZoundButtonPulse`, `DrawMuteSoloBackground`, `DrawMuteSoloIndicator`, and `TryGetAnyInstanceToken` have a clear enough identity by this point, move them here too. Otherwise move `ZoundsEditorColors` alone and leave the methods for the next pass.

> **⏸ Test.** Pulse animations, mute/solo backgrounds, color tinting on play/pause/muted state.

---

## Phase 8 — Extract `ZoundBrowserFilterEngine`

### 8a. Create `ZoundBrowserFilterEngine.cs`

Move `GetFilteredZounds`, `EvaluateGroup`, and related helper logic out of `BaseZoundTab`. Move the `filterCache`, `groupCache`, `prevGroupBy` fields with them.

`BaseZoundTab` holds a `ZoundBrowserFilterEngine` instance and delegates to it. Ownership of filter state moves to the engine; `BaseZoundTab` remains the caller.

> **⏸ Test.** Search, type filter, tag filter, group-by (Tags, Folder if enabled). Cache invalidation on library change (add/remove/rename zound).

---

## What stays on `BaseZoundTab` permanently

| Thing | Reason |
|---|---|
| `selectedZound`, `SelectZound`, `inspectorAnimFloat` | Orchestrator state — drives inspector open/close, row highlight, grid expansion |
| `zoundToRemove`, `zoundToDuplicate` + deferred application | Direct consequence of IMGUI list-iteration rules |
| `DrawZoundsSinglecolumn`, `DrawZoundsMulticolumn` | Retained as scroll-container drivers after rendering content is extracted |
| Toolbar and search bar drawing | Responsibility belongs at browser level; may move to helper file for discoverability |
