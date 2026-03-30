# Claude Health Check Report
**Date:** 2026-03-29
**Project:** Zounds (Unity Audio Management Framework)
**Scope:** ZoundsEngine · ZoundsUI · ZUI

---

## Overview

The project is divided into three distinct layers:

1. **ZoundsEngine** — Runtime audio logic. Zound data model, playback engine, token lifecycle, object pooling.
2. **ZoundsUI** — IMGUI-based Unity Editor tooling. The Zounds browser window, inspector panels, per-zound editor windows, tab views, preset system.
3. **ZUI** — General-purpose IMGUI UI framework. Styled buttons, sliders, toggles, text, boxes. ZUI is developed *alongside* Zounds as its test case but is architecturally independent.

---

# Part 1: ZoundsEngine

## Architecture

ZoundsEngine has a clear, well-structured core architecture:

```
ZoundEngine (MonoBehaviour, lazy singleton)
  └── ZoundPool        — AudioSource object pool
  └── ZoundToken[]     — Active playback instances
      └── IZoundHandler  — Polymorphic playback logic
          ├── KlipHandler        (single-clip audio)
          ├── ZequenceHandler    (composite / tree of zounds)
          ├── MuzicHandler       (music/looping)
          └── ClipZoundHandler   (raw AudioClip path)
```

**ZoundArgs** is a flat struct passed at play time to carry overrides (volume, pitch, chance, delay, solo, mixer group) without polluting Zound data. This is a good pattern.

**ZoundDictionary** handles the runtime name→Zound and id→Zound lookups, and async Addressables loading.

**ZoundAPI** is a clean static façade that game code interacts with. It routes editor vs runtime paths via `#if UNITY_EDITOR` blocks.

**ZoundsProject** is a serializable singleton ScriptableObject that holds all settings, the library, and routing tables. JSON serialization means the project data is portable, diffable, and version-control friendly — a smart design decision.

### What's Good
- The `IZoundHandler` interface cleanly separates the token lifecycle (Start/Pause/Resume/Kill) from playback type. Adding a new Zound type only requires adding a new handler.
- Token-based API with event callbacks (`onComplete`, `onFrameUpdate`) is composable and integrates naturally into game code.
- DSP time is used for timing instead of `Time.deltaTime`, which is correct for audio.
- The pool handles edit-mode gracefully (non-savable GameObjects with `HideFlags.HideAndDontSave`).
- Async initialization path (`InitializeAsync`) runs alongside sync path cleanly.

## Issues

### Impact: High · Invasiveness: Low

**1. `ZoundArgs` has no documentation for negative-value convention**
`volumeOverride = -1f` means "use default." `chanceOverride = -1f` means "use default." This sentinel pattern is implicit and spread across all handler constructors. If a caller accidentally passes `0f` thinking it means "use default," the zound is silenced. The convention is only discoverable by reading handler code.
→ A `HasVolumeOverride` / `HasPitchOverride` boolean pair, or a nullable float, would be safer and self-documenting. Low code change, high clarity improvement.

**2. `ZoundArgs.ignoreCooldown` is not set in `ZoundArgs.Default`**
`Default` omits `ignoreCooldown`, which means it defaults to `false` — correct. But `Deferred` also omits it. Neither preset sets it, so callers must remember to set it manually when needed. This is a footgun when doing editor playback (which always needs cooldown bypassed).

**3. `MissingZounds` persistence uses a static dictionary that is never fully cleared**
`s_persistedMissingZounds` accumulates across editor sessions and is only partially drained by `RemovePersistedMissingZound`. A large project with many renamed or deleted zounds over time will accumulate ghost entries silently. Add a max-age or session-scoped clear mechanism.

**4. `RoundRobin` mode uses a retry loop with a hard cap of 100 attempts**
`ZequenceHandler.InitRuntimeZoundEntries` has `do { ... } while (...attempts < 100)`. For large entry lists this could silently repeat an index. This is an O(n) problem best solved with a shuffle-bag. Low urgency in small projects.

### Impact: Medium · Invasiveness: Low

**5. `KlipHandler.isRealtime` is always `false`**
The realtime path (pitch/volume envelopes applied live) exists in `KlipHandler` but is disabled by a hardcoded `m_isRealtime = false` comment: *"The real-time modification logic is preserved but disabled."* This dead code path is ~40 lines of logic that will never run, adding confusion to anyone reading the file.
→ Either remove the dead path entirely (since Klips now render to clips), or document the intended future use.

**6. `ZequenceHandler.PrepareAndCalculateDuration` calls `Random.value` and `Random.Range` during duration calculation**
Duration calculation is supposed to be a pure measurement, but in real-time Zequence mode it also rolls chance (`if (Random.value > parentChanceOverride) continue`). This means the same tokens that were spawned for the duration calc are the ones that play, which couples initialization with measurement. The result is correct but fragile — if this method is ever called twice, randomness is consumed twice.

**7. `ZoundEngine.OnUpdate` builds a `List<int>` on every frame where any token dies**
`removedIndices` allocates a `new List<int>()` per frame that has dead tokens. In a busy game this is one GC allocation per killed zound per frame. An alternative is iterating the list in reverse directly.

**8. Volume volume volume — three volume layers, unclear hierarchy**
The volume stack is: `masterVolume × parentVolume × selfVolume × IsMutedOrExcluded`. In ZequenceHandler, there is also `audioSource.volume / ZoundEngine.GetMasterVolume()` to extract the browser slider value. This implicit division-to-undo-master pattern is fragile. If the order of operations changes, the volume balance breaks silently.

### Impact: Low · Invasiveness: Low

**9. `HandleMissingZound` has a commented-out `Debug.LogError`**
Line 308: `//Debug.LogError(...)`. Missing zounds fail silently unless the developer looks at the MissingZounds dictionary in the inspector. Consider at minimum a once-per-session warning.

**10. Duplicate initialization guards between `InitializeEngine` and `InitializeEngineAsync`**
Both methods copy-paste the same 5 lines of `inst.xxx.Clear()` and `UpdateMasterVolume`. A shared private method would remove duplication.

---

# Part 2: ZoundsUI

## Architecture

ZoundsUI is a feature-rich Unity Editor tooling layer built on top of IMGUI. It is organized into:

```
ZoundsWindow  (main EditorWindow, tabbed layout)
  └── TabViewIMGUI         — generic tab view controller
      ├── ZoundBrowserTab  — main browsing/playing panel
      │   ├── BaseZoundTab         — Klips / Zequences list rows
      │   ├── ZoundInspector       — inline inspector per zound row
      │   └── PresetsBarDrawer     — preset save/load toolbar
      ├── RoutingTab
      ├── ProjectSettingsTab
      ├── DependencyMapTab
      ├── TagBrowserTab
      └── ClipReferencesTab

ZoundEditorWindows (separate floating windows per selected zound)
  └── BaseZoundEditorWindow
      ├── KlipEditorWindow        — waveform, trim, envelopes, gain
      └── CompositeZoundEditorWindow
          ├── ZequenceEditorWindow
          └── (MuzicEditorWindow implied)
```

`ZoundAPIBridge` routes editor-side events from `ZoundAPI` (modify project, set dirty, etc.) without creating a compile dependency in the opposite direction. This is a clean inversion.

`EditorFieldsUtility` is a utility layer for drawing common controls (min/max sliders, fields) that was previously hand-rolled but is now largely adapted to delegate to ZUI.

### What's Good
- The tab architecture in `ZoundBrowserTab` cleanly separates Klips, Zequences, and Muzics without duplicating core list-drawing logic.
- `ZoundInspector` handles the per-row inline editing without coupling to the parent list.
- `CompositeZoundEditorWindow` uses a clean template-method pattern with `DrawRenderToKlipExtras()`, `DrawEntryGroupLeftSection()`, etc. for subclass extension.
- `RecordingData` cleanly wraps the recording session for audio rendering.
- The preset system (`ZoundsEditorPresets`, `PresetsBarDrawer`) is well isolated.

## Issues

### Impact: High · Invasiveness: Medium

**1. `ZoundBrowserTab` is very large and does too much**
`ZoundBrowserTab.cs` is the most complex file in the UI layer. It manages quick controls, preset placement, filter toolbar, group-by logic, column layout, multi-select, and the overall frame of the browser. It has grown to the point where adding a feature requires understanding the entire file. It would benefit from extracting the toolbar/quick-controls section into a `ZoundBrowserToolbar` helper.

**2. `BaseZoundTab` draws all row controls directly without a data structure**
Each row is a long chain of if-guards checking `browserSettings.showXxx` before drawing each field inline. This is readable for simple cases but now spans many fields (name, volume, pitch, chance, tags, mute, solo, routing, buttons). A `ZoundRowLayout` struct that resolves which controls are active before drawing would make the row logic cleaner and easier to extend.

**3. `EditorFieldsUtility` is a mixed bag**
Some methods in `EditorFieldsUtility` are now thin adapters to ZUI (good). Others are still raw IMGUI calls. The file has no clear ownership boundary — it's a dumping ground for "controls that don't fit elsewhere." This creates confusion about whether to call `EditorFieldsUtility.X` or `ZUI.X` for a given control.
→ Long term: migrate remaining raw controls into ZUI, retire the file or reduce it to Zounds-specific helpers only.

**4. `CompositeZoundEditorWindow` has a large rect-math section for drawing entries**
The entry drawing section (timeline rects, label rects, waveform rects, nested groups) manually calculates many pixel positions. While this is inherent to a custom IMGUI timeline, the magic constants (`4f`, `8f`, `leftSectionWidth`, `entryHeight`) are scattered and not named. A layout constants block would aid maintainability.

### Impact: Medium · Invasiveness: Low

**5. `BrowserSettings` has grown to ~35 boolean fields**
`ZoundsProject.BrowserSettings` is a flat list of `showXxx` booleans for every possible control. It works, but adding a new control requires editing the class, the serialization, the JSON, and the settings UI. A dictionary-based visibility map or a flags enum would reduce boilerplate.

**6. `EditorStyle` inside `ProjectSettings` mixes visual constants with behavior flags**
`EditorStyle.autoRender` is a boolean behavior flag living in what should be a pure visual style class. These should be in separate containers.

**7. `ZoundInspector` re-fetches state on every repaint**
The inspector reads audio clip metadata, waveform textures, and calculates layout every `OnGUI` call. Some of these calculations (e.g. waveform generation) are cached, but others appear to be re-evaluated. A dirty-flag system on the zound object would let the inspector skip recalculation unless the zound actually changed.

**8. The `ZoundAPIBridge` editor-side events pattern is inconsistent**
`ZoundAPI` stores internal Action delegates (`onEditorAPIKlipCreated`, `onModifyZoundsProject`, etc.) that are set by `ZoundAPIBridge` at initialization. This means runtime code (`ZoundAPI`) has editor-only member fields guarded by `#if UNITY_EDITOR`. While functional, it means the runtime assembly technically knows about editor hooks. A cleaner split would use an interface or a Registry pattern callable from both sides.

### Impact: Low · Invasiveness: Medium

**9. `AudioSpectrumView` and `EnvelopeGUI` are essentially standalone mini-frameworks**
Both files contain their own event handling, state machines, and drawing logic. They work, but they duplicate some interaction patterns (hover detection, drag anchors, hit testing) already solved in ZUI. As ZUI matures, these could be refactored to delegate to ZUI primitives for consistency.

**10. `RenderZequenceToKlipPopup` uses `#if ADDRESSABLES_INSTALLED` heavily**
The render popup and the ZequenceEditorWindow both have large `#if ADDRESSABLES_INSTALLED` blocks that make the audio rendering feature effectively unavailable without Addressables. The Addressables dependency is currently mandatory for rendering but the code structure implies it's optional. This should be documented clearly or the `#if` blocks replaced with a more explicit feature-gate.

---

# Part 3: ZUI

## Architecture

ZUI is a general-purpose IMGUI styling framework structured around:

```
ZUIStyleSheetAsset  (ScriptableObject, root of all style data)
  └── List<ZUIButtonDef>   — button/toggle/slider styles
  └── List<ZUIBoxDef>      — box/panel styles
  └── List<ZUISliderDef>   — slider styles
  └── ZUIIconLibraryAsset  — icon set
  └── List<ZUIPaletteColor> — named color palette

ZUI (static partial class — main API)
  ├── ZUI.cs          — Box, flash, palette, corner mask utilities
  ├── ZUIButton.cs    — Button, Toggle API
  ├── ZUIText.cs      — Label, text rendering
  ├── ZUISlider.cs    — Slider, SliderRange (range slider)
  └── ZUIToggle.cs    — Toggle helpers

ZUIStyleDef.cs       — ZUIButtonDef, ZUIBoxDef, ZUISliderDef, ZUITextDef
ZUIGradient.cs       — Multi-stop gradient renderer (DrawRect)
ZUIPalette.cs        — Named color palette entries
ZUIStyleEditorWindow — Inspector for editing style defs
ZUIDebug.cs          — Context-menu debug info for hit testing
```

Style resolution order: named style def from active sheet → fallback inline def → hardcoded color. This layering is consistent.

`ZUIGradient` is the core rendering primitive. It can draw solid, two-stop, or multi-stop gradients with border, corner radius, and shadow — all via immediate-mode `GUI.DrawTexture` calls using cached textures invalidated by hash.

`SectionStyleRegistry` inside `ZUI.cs` is a thin legacy fallback for the old enum-keyed `ZUIStyle` system, kept for backwards compatibility with call sites that predate the named def system.

### What's Good
- `ZUIButtonDef` stores style state (Normal/Hover/Active) as `ZUIGradient` fields, which means every state gets full gradient support with the same code path.
- The `Invalidate()` pattern (clearing cached textures when a def is modified) is clean and prevents stale draws.
- `ZUICornerMask` allows per-call corner overrides without needing a separate style def per variant — very ergonomic.
- The palette color reference system (`colorRef` + `ZUIPaletteSlot`) enables global color theming without touching individual defs.
- `ZUIDebug` is a good developer-experience investment. Right-click context menus showing style names and hit rects reduce debugging friction enormously.
- The `BoxScope` / `AreaBoxScope` IDisposable pattern integrates naturally with C# `using` blocks.

## Issues

### Impact: High · Invasiveness: Low

**1. `ZUI` is a static class split across many `partial` files — no clear file ownership**
`ZUI.cs`, `ZUIButton.cs`, `ZUISlider.cs`, `ZUIText.cs`, `ZUIToggle.cs` are all `partial class ZUI`. This is fine for organization, but there's no convention for which partial file "owns" shared state (like `_flashStyleName`, `_pendingBoxStyle`). A developer new to ZUI has to search all partials to understand the full static field surface.
→ Document a "ZUI.cs is the state owner" convention, or group shared state into a `ZUIState.cs` partial.

**2. `SectionStyleRegistry` is a parallel style system that creates confusion**
There are now *two* ways to style a box: the old `ZUIStyle` enum → `SectionStyle` path (which creates plain `GUIStyle` boxes with texture backgrounds), and the new `ZUIBoxDef` path (which uses `ZUIGradient.DrawRect`). `ZUI.Box()` supports both. The enum path cannot do gradients, corner radius, borders, or shadows. The `SectionStyleRegistry` is essentially dead weight since any real usage should migrate to `ZUIBoxDef`.
→ Audit call sites that still use the old enum path. If none remain in Zounds, mark `SectionStyleRegistry` as deprecated.

**3. `ZUISliderDef` now has `thumb` and optional `thumbMax` — but the style editor only conditionally shows thumbMax**
The thumbMax field was recently added for range slider differentiation. This is correct design, but the style editor's `Normal | Min/Max` tab mode controls whether `thumbMax` is seeded or cleared at mode switch time. If a user edits a style, switches mode, and switches back, `thumbMax` is silently cleared. There is no "keep thumbMax" confirmation. This could cause accidental data loss in the style editor.

**4. `ZUIGradient` texture cache uses instance identity as the key**
Each `ZUIGradient` instance manages its own cached texture via `_cachedTex`. If a def is serialized, deserialized, or cloned, the cache is lost and must be rebuilt. More importantly, if the same def object is drawn repeatedly from different windows, the texture is shared — which is correct — but if two defs have identical parameters they each maintain their own texture, wasting GPU memory for identical content. This is an acceptable trade-off for a tool, but worth noting for large sheets.

### Impact: Medium · Invasiveness: Low

**5. `ZUIStyleEditorWindow` state fields are growing without structure**
State fields like `_sliderThumbModeTab`, `_sliderThumbMinState`, `_sliderThumbMaxState`, `_pendingSelectedDef`, `_filterText`, `_scrollPos` etc. are flat fields on the EditorWindow class. As ZUI grows, this will become hard to manage. Group per-inspector state into small structs (e.g. `SliderInspectorState`, `ButtonInspectorState`).

**6. Palette color references (`colorRef` string + `ZUIPaletteSlot`) are repeated on every def**
`ZUITextDef`, `ZUIBoxDef`, `ZUIButtonDef` all carry `colorRef`, `colorSlot`, `shadowColorRef`, `shadowColorSlot` etc. as raw string fields. A dedicated `ZUIPaletteRef` struct would reduce duplication and allow the reference resolution to be uniform.

**7. The `useGlobalXxx` flags on `ZUIBoxDef` are an incomplete feature**
`ZUIBoxDef` has `useGlobalBorder`, `useGlobalPadding`, `useGlobalShape`, `useGlobalBackground`, `useGlobalTitleText`, `useGlobalContentText` flags. These appear to be hooks for a "inherit from global style" system, but there is no global style object and these flags are not checked in the actual draw code. Either implement the feature or remove the flags to reduce confusion.

**8. ZUIButton and ZUIToggle share most drawing code but are separate files**
`ZUIButton.DrawButton` and `ZUIToggle.DrawToggle` are nearly identical in their gradient drawing, border drawing, and corner radius resolution. The only difference is toggle's checked-state visual. Consolidating into a shared `DrawControl(Rect, ZUIButtonDef, bool isDown, ...)` would eliminate duplication.

### Impact: Low · Invasiveness: Low

**9. `ZUITween` and `ZUIPulse` are in the codebase but appear unused in production Zounds UI**
These files contain animation/easing utilities that are referenced in showcase windows but not in the actual Zounds browser or editor windows. They are low-risk to keep but represent dead production code.

**10. Showcase windows (`ZUIShowcaseWindow`, `ZUIShowcase2Window`, `ZUIInputShowcaseWindow`) are shipped in the production codebase**
These are development/testing windows with no runtime value. They should either live in an Editor-only test assembly (asmdef) or be excluded from the final package.

**11. `ZUIStyleSheetAsset` name-based lookup uses `List.Find()` in hot paths**
`ActiveSheet.FindBox(name)`, `FindButton(name)`, `FindSlider(name)` are O(n) linear searches called every repaint for every control. For sheets with many defs this degrades. A `Dictionary<string, T>` populated lazily on first access would give O(1) lookup with no authoring change.

---

# Issue Priority Matrix

| # | Issue | System | Impact | Invasiveness | Recommended Action |
|---|-------|--------|--------|-------------|-------------------|
| 1 | `ZoundArgs` negative sentinel convention undocumented | Engine | High | Low | Add XML docs + consider nullable |
| 2 | `SectionStyleRegistry` dual-path box system | ZUI | High | Low | Audit/deprecate old enum path |
| 3 | `ZoundBrowserTab` monolith | ZoundsUI | High | Medium | Extract toolbar helper |
| 4 | `useGlobalXxx` flags not implemented | ZUI | Medium | Low | Remove dead flags |
| 5 | `EditorFieldsUtility` mixed ownership | ZoundsUI | Medium | Medium | Migrate remaining raw IMGUI to ZUI |
| 6 | `ZUIStyleSheetAsset` O(n) lookup in hot path | ZUI | Medium | Low | Add lazy Dictionary cache |
| 7 | Dead `KlipHandler.isRealtime` always-false path | Engine | Medium | Low | Remove dead code or document intent |
| 8 | Volume stack fragility (implicit division) | Engine | Medium | Medium | Named constants + single volume compositor |
| 9 | `BrowserSettings` 35+ flat booleans | ZoundsUI | Medium | Medium | Flags enum or grouped sub-settings |
| 10 | `ZUIButtonDef` / `ZUIBoxDef` repeated palette ref fields | ZUI | Medium | Low | Extract `ZUIPaletteRef` struct |
| 11 | Per-frame GC alloc in `ZoundEngine.OnUpdate` | Engine | Low-Med | Low | Reverse-iterate instead of building index list |
| 12 | `thumbMax` silently cleared on mode switch | ZUI | Low | Low | Add confirmation or preserve on switch |
| 13 | `ZUIStyleEditorWindow` flat state fields | ZUI | Low | Low | Group into state structs |
| 14 | Showcase windows in production codebase | ZUI | Low | Low | Move to test asmdef |
| 15 | `ZUITween` / `ZUIPulse` unused in production | ZUI | Low | None | No action needed, or isolate to test |
| 16 | `RoundRobin` retry-loop with hard cap | Engine | Low | Low | Replace with shuffle-bag |
| 17 | `HandleMissingZound` silent (commented LogError) | Engine | Low | None | Restore as LogWarning once-per-session |

---

## Summary

**ZoundsEngine** is architecturally the healthiest of the three layers. The handler pattern is clean, the token lifecycle is well-defined, and the async/sync initialization duality works well. The main risks are subtle: the negative-value convention in `ZoundArgs`, the dead real-time code in `KlipHandler`, and the volume stacking ambiguity in `ZequenceHandler`. These are low-effort to address and high-value for long-term maintainability.

**ZoundsUI** is functional and feature-rich but shows signs of organic growth. `ZoundBrowserTab` is the highest-priority refactor target — it has become a coordination hub that mixes layout, filtering, and control rendering without clear separation. The adapter pattern in `EditorFieldsUtility` is a good transitional step but should be completed. The `BrowserSettings` boolean explosion is manageable now but will become a serialization headache at scale.

**ZUI** is the youngest layer and is actively being designed, so some rough edges are expected. The biggest structural risk is the coexistence of the old enum-based `SectionStyle` system alongside the new `ZUIBoxDef` system. Clients calling `ZUI.Box(ZUIStyle.Default)` get a visually different result than `ZUI.Box(ZUIBoxDef)`, which is confusing. Audit and unify as soon as possible. The O(n) lookup in the style sheet is the highest-priority performance fix, and the `useGlobalXxx` dead flags should be cleaned up to avoid misleading anyone building on ZUI.
