# Regression Test List — Browser Refactor + Muzic Removal

## Phases 3–8: Browser Refactor

### Single-column list (Phase 3 — `ZoundListItemView`)
- [ ] List rows render correctly: name button, edit button, mute/solo rect, remove button, inspector rect, tags rect all appear in expected positions
- [ ] Left-click a Klip → plays it
- [ ] Right-click a Klip → opens editor panel (not inspector panel)
- [ ] Middle-click / Alt+left-click → copies name to clipboard
- [ ] Ctrl+left-click → opens InfoViewWindow
- [ ] Missing zound (id==0, non-ClipZound) renders the `MissingZound` box style (or fallback red label); clicking it toggles selection
- [ ] `killOnPlay` setting: enabling it causes previous sound to stop when a new one is played

### Grid / multicolumn mode (Phases 4 & 5 — `ZoundGridItemView`)
- [ ] Fixed multicolumn: buttons lay out in correct column count, last partial row has empty placeholders aligned correctly
- [ ] Fixed multicolumn: `FlexibleSpace` centers the grid horizontally
- [ ] Flow multicolumn (Auto/Min size mode): buttons wrap to new rows when they would overflow the panel width
- [ ] Inspector panel (right-click selection) renders inline below the row that contains the selected zound — both in Fixed and Flow modes
- [ ] `DrawFlowRow` inspector after last row: selecting the last item on the last row still draws the inspector
- [ ] Ctrl+left-click in grid mode → InfoViewWindow
- [ ] Alt+left-click in grid mode → copies name to clipboard

### Playback visuals (Phase 7 — `ZoundBrowserPlaybackVisuals`)
- [ ] Pulse animation starts when a Zound begins playing; stops when it finishes
- [ ] Paused token → purple tint (`0.9, 0.5, 0.9`)
- [ ] Muted/zero-volume token → orange tint (`0.9, 0.5, 0.1`)
- [ ] ClipZound playing → cyan / `clipFlashColorStart` tint
- [ ] Non-ClipZound playing → `flashColorStart` blue tint
- [ ] Selected + playing → brighter selected-flash colors
- [ ] Muted Zound → red background stripe; soloed → green stripe
- [ ] Zequence with local mute/solo entry → yellow bottom stripe
- [ ] `TryGetAnyInstanceToken`: prefers `isDelayFinished == true` token; falls back to queued token for pre-delay highlight

### Filter engine (Phase 8 — `ZoundBrowserFilterEngine`)
- [ ] Search by name (direct substring match) filters correctly
- [ ] Search by NicifyVariableName word splits filters correctly
- [ ] Folder filter: selecting a folder shows only Klips whose audio clip is in that folder
- [ ] Tag filter: selecting a tag shows only Zounds with that tag
- [ ] References filter: selecting a Zound shows only its dependencies
- [ ] Mute/Solo only (`msOnly`) filter: shows only muted or soloed zounds
- [ ] GroupBy None: flat unordered list
- [ ] GroupBy Tags: zounds appear under each tag group; untagged under "-Untagged-"
- [ ] GroupBy References: zounds grouped by how many other zounds reference them, descending
- [ ] GroupBy MixerGroup: zounds with manual mixer group override grouped correctly; unrouted under "-Unrouted-"
- [ ] GroupBy Type: groups are AudioClip, Klip, Zequence, Missing; empty groups omitted
- [ ] Filter cache: modifying search text invalidates and rebuilds; unchanged state returns cached list
- [ ] Group cache: changing `GroupBy` rebuilds group; same `GroupBy` reuses group cache
- [ ] Selected index in grouped fixed-column mode: `Tags` groupBy selectedIndex correction still works (item highlighted matches the visually selected row)

### `inspectorAnimFloat` visibility change (Phase 5)
- [ ] Inspector panel animates open/closed smoothly in both list and grid modes
- [ ] No subclass accesses `inspectorAnimFloat` directly (verify `AllZoundsTab`, `KlipsTab`, `ZequencesTab` don't shadow it)

---

## Muzic Removal

- [ ] Project compiles with zero errors — no dangling `Muzic` references
- [ ] `ZoundLibrary.Validate()` still runs without errors on a live project
- [ ] `ZoundLibrary.GetAllZounds()` returns all Klips + Zequences (no crash from removed `muzics` list)
- [ ] `ZoundLibrary.ForEachZound(Action)` and `ForEachZound(Func<bool>)` iterate all expected zounds without error
- [ ] `ZoundToken` construction: playing a Klip and a Zequence both get their correct handlers (`KlipHandler`, `ZequenceHandler`)
- [ ] `AudioAssetUtility.RemoveZound` on Klip and Zequence still works; no null-ref from removed Muzic branch
- [ ] `AudioAssetUtility.DuplicateZound` on Klip and Zequence still works
- [ ] `ZoundsAssetPostProcessor` `AssignZoundPath` runs on asset reimport without error
- [ ] `ZoundDependencyAnalyzer` builds dependency graph for Klips and Zequences correctly
- [ ] `DependencyMapTab` missing-clip reassignment works for Klips and Zequences; `GetTypeLabel` returns correct strings
- [ ] `CompositeZoundEditorWindow` entry editing: clicking a Klip/Zequence entry opens correct editor; no error for those types
- [ ] `CompositeZoundEditorWindow` duration calculation: Klip and Zequence durations compute correctly
- [ ] ZoundBrowserFilterEngine `IsClipContainedInZound`: folder filter works for Klips and Zequences; no crash from removed Muzic branch
- [ ] `ZoundLibrary.ZoundEntry.ZoundType` enum: no serialized data in `.asset` or `.json` files references the old `ZoundType.Muzic` value

> **⚠️ Pay special attention to the last point**: `ZoundEntry.ZoundType` had `Klip=0, Zequence=1, Muzic=2, Randomizer=3`. After removal it becomes `Klip=0, Zequence=1, Randomizer=2`. Any serialized `ZoundEntry` that stored `zoundType = 3` (Randomizer) will now read as `2` (still Randomizer — fine). But if any entry had `zoundType = 2` (Muzic), it will now silently become Randomizer. Since Muzic was never used, this should be a non-issue in practice, but worth confirming no `.json`/`.asset` files contain `"zoundType": 2`.
