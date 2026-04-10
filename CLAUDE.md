# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This project servers to develop two independent products. Even if they intersect it is import to keep them apart when planning and executing development. 

Zounds is a Unity editor-based sound design tool distributed as a UPM package (`com.lautaro.zounds`). It provides a browser/editor UI for creating and managing sound assets ("Zounds") — including Klips (single audio clips) and Zequences (composite sequences of Zounds). 

ZUI is a IMGUI styling/layout framework used to build the Zounds editor UI. Although the goal of ZUI is to be a standalone framework that will help to make tools look better and help with creating useful layouts quicker. Tools uing ZUI will also have the posibility to edito the styles by creating skins that modifies the base appearance.  

## Build & Development

This is a Unity project — there is no CLI build command. Open in Unity 2021.2+ and let it compile. 

No test suite exists in this repo.

## Assembly Structure

Four assemblies, with clear dependency boundaries:

- **Zounds** (`Assets/Zounds/Scripts/Runtime/`) — Runtime types: `ZoundEngine`, `ZoundAPI`, `ZoundsProject`, `ZoundLibrary`, sound handlers. Runs in builds and editor.
- **Zounds.Editor** (`Assets/Zounds/Scripts/Editor/`) — Editor UI: `ZoundsWindow`, browser tabs, zound editor windows, utilities. References Zounds runtime and ZUI.Editor.
- **Zounds.Setup** (`Assets/Zounds/Scripts/Editor/Setup/`) — Lightweight first-run dependency checker. No references to other project assemblies.
- **ZUI.Editor** (`Assets/ZUI/Scripts/Editor/`) — Self-contained IMGUI styling framework. No references to Zounds — ZUI knows nothing about Zounds. Zounds.Editor depends on ZUI, not the reverse.

Cross-assembly rule: ZUI.Editor types used by Zounds.Editor must be `public`. ZUI has no `rootNamespace` set, so its types are in the global namespace.

## Key Architecture Concepts

### Zounds Data Model
- **ZoundsProject** (ScriptableObject): root data container holding `ZoundLibrary`, `ZoundRoutings`, `BrowserSettings`, `ProjectSettings`
- **Zound** base type with two concrete kinds: **Klip** (wraps a single AudioClip via Addressables AssetReference) and **Zequence** (composite, contains ordered list of Zounds)
- Project data is serialized to/from JSON (`TextAsset`), not native Unity serialization. `ZoundsProject.isJSONLoaded` tracks whether the JSON has been deserialized into the ScriptableObject.

### ZoundEngine
- Singleton `MonoBehaviour` (`ExecuteAlways`) that manages audio playback in both editor and runtime
- Creates itself lazily with `HideFlags.HideAndDontSave` in edit mode, `DontDestroyOnLoad` in play mode
- Uses **ZoundHandler** subclasses (`ClipZoundHandler`, `ZequenceHandler`, `KlipHandler`) for polymorphic playback

### ZUI Framework
- Editor-only IMGUI styling system with `ZUIStyleSheetAsset` (ScriptableObject) holding `ZUIStyleDef` entries
- Provides styled controls: `ZUI.Button`, `ZUI.Toggle`, `ZUI.Slider`, `ZUIFormControls`, `ZUIMultiSelect`, etc.
- `ZUIWindow` base class for styled editor windows
- `ZUIStyleEditorWindow` and `ZUIShowcaseWindow` for editing/previewing styles
- Uses `ZUIColorRef` for palette-based color indirection, `ZUIGradient` for gradient rendering, `ZUIAnimation`/`ZUITween` for animated transitions
- **One-struct-one-renderer rule:** Each visual concept (color, shadow, border, text, padding, shape, background) must have exactly one backing data structure and one shared rendering method. Do not introduce inline drawing code or alternate structs for the same concept in a different context. Current shared types: `ZUIColorRef` (color), `ZUIGradient` (background), `ZUIBorderDef` (border), `ZUIPaddingDef` (padding), `ZUIShapeDef` (corners), `ZUITextDef` (text), `ZUIDropShadowDef` (shadow). Text shadow now uses `ZUITextShadowDef` (no blur, for IMGUI second-pass rendering).
- **Use ZUI spacing everywhere:** All layout spacing in ZUI editor windows and consumer tools must use `ZUI.VerticalSpace`/`ZUI.HorizontalSpace` (not raw `GUILayout.Space`), so spacing is tweakable from the style editor.
- **`ZUI.Form` is the standard for structured control rows.** Use it for label+control layouts instead of manual `BeginHorizontal`/`LabelField`/`EndHorizontal` patterns.
- **`ZUI.Blocks` is the standard for side-by-side cells** that need matched heights. Each cell gets vertical alignment (Top/Center/Bottom/Spread/Even). Form handles vertical layout, Blocks handles horizontal layout — they compose freely.

### ZUI API Stability

ZUI is the UI foundation for Zounds, which is used by a 4-person game development team with a title due to launch. The API must be stable and reliable.

**Stable API** — public methods used by consumer tools (Zounds, custom inspectors). Changing signatures or behavior requires checking all consumers. These must stay `public`:
- Controls: `ZUI.Button`, `ZUI.Toggle`, `ZUI.Slider`, `ZUI.SliderStacked`, `ZUI.SliderVertical`, `ZUI.SliderRange`, `ZUI.MicroSlider`, `ZUI.Slider2D`, `ZUI.CycleButton`, `ZUI.MiniRadio`, `ZUI.MicroRadio`
- Layout: `ZUI.Form`, `ZUI.Blocks`, `ZUI.Box`, `ZUI.FoldoutBox`, `ZUI.AreaBox`
- Spacing: `ZUI.VerticalSpace`, `ZUI.HorizontalSpace`, `ZUI.LabelWide`, `ZUI.LabelNarrow`, `ZUI.InputMin`
- Style: `ZUI.ActiveSheet`, `ZUI.UseSheet`, `ZUI.RegisterConsumerSheet`, `ZUI.FindIcon`, `ZUI.FindFont`, `ZUI.PaletteColor`
- Window: `ZUIWindow` base class

**Internal API** — used only by ZUI's own editor windows (Style Editor, Showcase). Safe to change freely. Should be marked `internal` where possible:
- Flash overlay, style debug, section style registry, color picker internals, animation update pump

**Showcase as contract:** Every stable API method should have a demo in the Showcase window. If the Showcase renders correctly after a change, the API is intact.

**IMGUI safety rule:** Never put `BeginVertical`/`EndVertical`, `BeginHorizontal`/`EndHorizontal`, or `FlexibleSpace` inside conditionals that can differ between Layout and Repaint passes. This includes conditionals based on cached state that updates mid-frame. All layout calls must be identical on both passes.

### Zounds Editor UI
- `ZoundsWindow`: main `EditorWindow`, singleton pattern, tab-based layout via `TabViewIMGUI`
- Browser tabs (`BrowserTab`, `ZoundListItemView`, `ZoundGridItemView`) for browsing/filtering Zounds
- Zound editor windows (`KlipEditorWindow`, `CompositeZoundEditorWindow`, `ZequenceEditorWindow`) for editing individual Zounds
- `ZoundsZUIBootstrap`: bridges Zounds into ZUI's style system
- Browser row controls for Klips and Zequences should share the same layout structure. File and Missing entries should be minimal (name button only).

## Conditional Compilation

`ADDRESSABLES_INSTALLED` — defined via asmdef versionDefines when `com.unity.addressables >= 1.18.19` is present. Guards all Addressables API usage in both runtime and editor code.

## Important Patterns

- Undo support: use `Undo.RegisterCompleteObjectUndo` before modifying `ZoundsProject`
- The `ZoundAPI` class exposes editor-internal callbacks (`onEditorAPIKlipCreated`, `onModifyZoundsProject`, etc.) guarded by `#if UNITY_EDITOR` — these wire up editor reactions to API calls
- IMGUI layout: `BeginVertical`/`EndVertical` pairs must never be inside conditionals that differ between Layout and Repaint events

## Active Work Areas

Four parallel tracks — keep them distinct when planning and implementing:

1. **ZUI Framework** — core controls, layout system, API surface (the reusable toolkit)
2. **ZUI Appearance** — styling, skins, style editor, layout tuning for the ZUI editor windows themselves
3. **Zounds Engine** — runtime/editor sound functionality (playback, Klips, Zequences, routing, API)
4. **Zounds UI** — using ZUI to improve the Zounds editor windows (browser, inspectors, editor popups)

## Todo / Ideas

<!-- Claude: update this section when the user adds, completes, or removes items. Keep items short. -->

### ZUI Framework
- [x] New control: `ZUI.Slider2D` (XY pad) — added to ZUISlider.cs and Showcase. Reuses slider track style for background. Square pad with crosshair + dot indicator, axis labels, value readout. Double-click resets to default.
- [x] `ZUI.Blocks` — height-matched horizontal cell layout with per-cell vertical alignment (`ZUIAlign`: Top/Center/Bottom/Spread). Caches heights across frames. Used in shadow section and Showcase.
- [x] `ZUI.SliderStacked` — two-row slider: label+value on row 1, track on row 2. Width auto-constrained to label+field size. Used in Shape editor.
- [ ] Investigate layout code for duplicated logic — fixes keep needing to be applied in multiple places, suggests shared layout helpers are missing or underused (this might actually be a ZUI editor layout thing rather than framework. Remember that the ZUI engine is used to create the ZUI Editor. And the ZUI Editor styles itself). **Assessment:** Confirmed — ZUIStyleEditorWindow has 4 identical animation-timing rows (lines 808-833) and identical scroll+panel shells. ZUIShowcaseWindow repeats "label + control + spacing" ~8 times. These are ZUI editor layout issues, not framework bugs. `ZUI.Form` is the right fix.
- [ ] Continue developing `ZUI.Form` for structured control layout — WIP, currently used in Showcase window. Should become the standard approach for browser row controls. **Assessment:** Form is working well in ShowcaseWindow (4 examples, lines 267-346) with typed controls, conditional rows, and multi-control rows via `ZUI.Row`. Ready to adopt more widely — both in ZUI editor windows and Zounds browser.

### ZUI Appearance
- [ ] Audit ZUI editor windows for layout/behavior quirks — several controls still have weird spacing or behavior. **Assessment:** Many hardcoded pixel widths (58f, 40f, 100f, 300f, 120f) throughout ZUIStyleEditorWindow and ZUIShowcaseWindow instead of ZUI spacing tokens. Inconsistent section headers (some use `EditorStyles.boldLabel`, others use `form.Header()`). Also: stale debug log on line 19 of ZUIStyleEditorWindow fires every domain reload — should be removed.
- [ ] Normalize ZUI Style Editor so identical concepts use identical data + rendering code everywhere. See analysis below.

#### ZUI Style Editor — Structural Consistency Analysis

**Principle:** Each visual concept (color, shadow, border, text, etc.) should have one backing data structure and one rendering method, reused everywhere that concept appears.

**Data layer — mostly clean.** Color (`ZUIColorRef`), background (`ZUIGradient`), border (`ZUIBorderDef`), padding (`ZUIPaddingDef`), shape/corners (`ZUIShapeDef`), and text (`ZUITextDef`) are each a single type used consistently across `ZUIButtonDef`, `ZUIBoxDef`, and `ZUISliderDef`. One exception:
- **Text shadow vs background shadow** — Background shadow uses a dedicated `ZUIDropShadowDef` struct. Text shadow is 3 flat fields on `ZUITextDef` (`shadowEnabled`, `shadowOffset`, `shadowColor`). Should be extracted into its own struct or reuse `ZUIDropShadowDef`.

**Rendering layer — several inconsistencies:**

| Concept | Issue |
|---|---|
| **Border** | Three different draw paths: `DrawBorderDefField` (full, L2384), `DrawBorderColorAndWidth` (hover/active states, L1232), `DrawCompactBorderRow` (inline for slider sub-defs, L5255). Should converge to one method with parameters. |
| **Corner radius** | `DrawShapeEditor` (L2349) is the shared method, but `DrawInlineBoxDef` (L5284) draws corner radius with inline `EditorGUILayout` code instead. Should call `DrawShapeEditor`. |
| **Preview header** | Buttons and Sliders use `DrawPreviewHeader` (L2515) which includes "Simulate No Rounding" toggle. Boxes and Text Styles use plain `InspectorSubheader("Preview")` — different look/behavior for the same section type. |
| **Animation** | Fully inline in `DrawButtonInspector` (L796-837) — 4 identical rows with no shared method. Should be a `DrawAnimationTimingRow` helper. |
| **Color editing** | Clean — `ZUIColorPickerInline` used everywhere consistently. No duplication. |
| **Text fields** | Clean — `DrawTextDefFields`/`DrawTextRow` shared everywhere. Minor difference: standalone text styles skip the style-ref popup variant. |
| **Shadow (bg)** | Clean — `DrawBgShadowFields` (L2243) shared identically between buttons and boxes. |
| **Shadow (text)** | Clean — `DrawShadowTextRow` (L2191) shared across all text contexts. |
| **Padding** | Clean — `DrawPaddingEditor` (L2495) shared, parameterized via `showIcon`/`showMargin`. |

#### Violation Cleanup Plan

Ordered by independence (can be done in any order, no dependencies between them). ZUIShowcaseWindow is already clean — all violations are in ZUIStyleEditorWindow.

**V1. ~~Stale debug log~~ DONE** — Removed `PhaseCheck()`.

**V2. ~~Text shadow data structure~~ DONE** — Created `ZUITextShadowDef` in ZUISubDefs.cs. `ZUITextDef` now holds `shadow` field. Migration from flat fields at version 3. All access sites updated. — Extract `shadowEnabled`, `shadowOffset`, `shadowColor` from `ZUITextDef` (ZUIStyleDef.cs:18-20) into a new `ZUITextShadowDef` struct. Keep it separate from `ZUIDropShadowDef` since text shadow has no blur fields, but follow the same pattern (enabled + offset + color as `ZUIColorRef`). Update `ZUITextDef` to hold a `ZUITextShadowDef shadow` field. Migration: `OnAfterDeserialize` in `ZUITextDef` to populate the struct from flat fields on first load.

**V3. ~~Animation timing rows~~ DONE** — Extracted `DrawAnimationTimingRow` helper, 4 call sites.

**V4. ~~Border rendering — unify 3 paths~~ DONE** — `DrawBorderDefField` now accepts `compact` param. `DrawBorderColorAndWidth` and `DrawCompactBorderRow` removed. — Currently: `DrawBorderDefField` (full editor, L2384), `DrawBorderColorAndWidth` (hover/active compact, L1232), `DrawCompactBorderRow` (slider inline, L5255). Suggestion: make `DrawBorderDefField` accept a `compact` bool parameter. When compact=true, skip the edge-width breakdown and gradient expand toggle, show only color + single width. Then:
- Normal button/box border → `DrawBorderDefField(border, onChange, compact: false)`
- Hover/active button border → `DrawBorderDefField(border, onChange, compact: true)`
- Slider inline border → `DrawBorderDefField(border, onChange, compact: true)` (retire `DrawCompactBorderRow` and `DrawBorderColorAndWidth`)

**V5. ~~Corner radius in DrawInlineBoxDef~~ DONE** — Replaced inline code with `DrawShapeEditor(box.shape, 24)`.

**V6. ~~Preview header inconsistency~~ DONE** — `DrawPreviewHeader` now returns bool (collapsible), accepts `showRoundingToggle` param. All 4 tabs use it. — Boxes (L1308) and Text Styles (L1549) use `InspectorSubheader("Preview")` while Buttons (L732) and Sliders (L5033) use `DrawPreviewHeader()` which adds "Simulate No Rounding" toggle. Options:
- a) Use `DrawPreviewHeader()` everywhere, hide the rounding toggle when irrelevant (boxes/text have no corner radius preview)
- b) Make `DrawPreviewHeader` accept optional toggles, so boxes/text get the same visual header without the rounding toggle
- Option (b) is cleaner — keeps a single code path for all preview headers while allowing per-tab extras.

**V7. Hardcoded spacing in ZUIStyleEditorWindow** — ~60 instances of `GUILayout.Space()` with raw float literals. ZUIShowcaseWindow is already clean. Three categories to address:

- **Vertical gaps (4f, 6f, 8f, 10f)** — ~40 instances. These are row and section gaps that should use `ZUI.VerticalSpace()` or `ZUI.VerticalSpace("V Section Rows")` / `ZUI.VerticalSpace("V Control Gap")`. Approach: batch replace by context — `4f`/`6f` → `ZUI.VerticalSpace("V Control Gap")`, `8f`/`10f` → `ZUI.VerticalSpace("V Section Rows")`. The `1f`/`2f` structural spacers may need to stay or use a micro-spacer scale.
- **Horizontal alignment offsets (44f, 120f, 122f)** — ~6 instances in the palette section. These are column-width alignment hacks. Should be named constants derived from the palette layout (e.g. `k_PaletteNameColumnWidth`). Not ZUI spacing — these are structural layout widths.
- **Popup indent (`k_Pad = 8f`)** — ~18 instances in gradient/pattern popups. Already uses a local constant, which is fine, but could be promoted to `ZUI.HorizontalSpace()` if popups should respect the style sheet. Lower priority — popups are self-contained.

Suggested approach: Do this in 3 passes to keep diffs reviewable:
1. ~~Replace vertical gap `GUILayout.Space(N)` calls with appropriate `ZUI.VerticalSpace` variants~~ DONE
2. ~~Replace horizontal alignment magic numbers with named constants~~ DONE — `k_PaletteNameWidth`, `k_PaletteDetailIndent`, `k_PaletteTrailingPad`, `k_FlashButtonPad`
3. ~~Convert popup vertical gaps to ZUI spacing + horizontal gaps to `ZUI.HorizontalSpace`~~ DONE — `k_Pad` indents kept as named constant (popup-specific structural padding). All 40 remaining `GUILayout.Space` calls are named constants or structural spacers.

**V8. Control-specific improvements (DONE):**
- ~~Shape editor: redesigned to 2-row layout with label+value+slider and 2×2 corner toggle grid with full names~~
- ~~Shadow section: toggle moved after title, section collapses when disabled, offset fields now labeled sliders (X/Y ±20), blur 0-20, passes 1-20~~
- ~~Text section header: "S"/"O" labels replaced with full "Shadow"/"Outline" labels~~

### Zounds Engine
- [ ] Review EQ algorithm — clips too easily and produces noticeable artifacts, may need better filter implementation. **Assessment:** Uses correct biquad (Direct Form I) with peaking EQ filters in `AudioRenderUtility.cs` (lines 376-521). The algorithm itself is standard, BUT there is zero clipping protection — no `Clamp(-1,1)` or soft-saturation after the filter chain. Multi-band boosts push samples past ±1.0 silently. Also, the LowPassFilter has a comment noting it doesn't match the reference calculator. Adding a normalization/limiter pass after EQ would fix most artifacts.
- [ ] EQ: let user configure number of bands and per-band min/max ranges. **Assessment:** Currently hardcoded 7 bands (60Hz–12kHz) with fixed Q values (0.7–1.0). Only per-band gain dB is parameterized. Straightforward to make configurable since the biquad filters are already parameterized — just need to surface frequency/Q/count to the UI.
- [ ] Research third-party audio DSP libraries for Unity — could improve EQ quality and unlock advanced features (compression, limiting, etc.). **Assessment:** Current EQ is editor-only offline rendering (no runtime DSP). A third-party lib could add real-time processing and higher-quality algorithms but would add a dependency to a package meant to be lightweight. Consider whether offline-only processing with better algorithms is sufficient first.
- [ ] Compression effect — reduce dynamic range (difference between quietest and loudest) before gain boost. **Assessment:** No compression exists yet. Since all DSP is offline baked rendering, a compressor would be a new processing stage in `AudioRenderUtility`. Standard approach: envelope follower → gain reduction → makeup gain. Moderate effort.
- [ ] Klip Editor: defer audio re-render while sliders are being dragged — only re-render on mouse-up to avoid lag. **Assessment:** The defer pattern already exists — `isDraggingSlider` flag suppresses `Render()` until `mouseReleased` (KlipEditorWindow.cs lines 204-209, 415-416). The slowness may be coming from waveform preview updates or excessive repaints during drag rather than the actual render. Needs further investigation into what exactly is slow.

### Zounds UI
- [ ] Unify Klip and Zequence browser row controls — too much divergence, should share layout structure. **Assessment:** Surprisingly, ZoundListItemView already uses the same path for both (`DrawZoundSinglecolumn` line 105). The difference is mainly color tinting. ZoundGridItemView has a Klip-specific invalid-ref warning path. Less structural divergence than expected — the real issue may be that the shared path doesn't use Form-based layout yet.
- [ ] Ensure all browser controls use `ZUI.VerticalSpace`/`HorizontalSpace` so spacing is tweakable from the style editor. **Assessment:** Neither file uses `ZUI.VerticalSpace`/`HorizontalSpace` at all. ZoundGridItemView uses raw `GUILayout.Space(...)`. ZoundListItemView uses no spacing calls (spacing is pre-computed in `BrowserTab.ZoundListRowLayout`). Needs a full pass to replace all raw spacing.
- [ ] File and Missing entries should be minimal — just the name button, no extra controls. **Assessment:** ZoundGridItemView handles missing correctly (label only, lines 235-269). ZoundListItemView may still call `DrawZoundSinglecolumn` unconditionally for missing entries (line 105), potentially rendering edit/remove buttons. Needs a guard check.

