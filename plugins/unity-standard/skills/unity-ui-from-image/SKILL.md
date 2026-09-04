---
name: unity-ui-from-image
description: Build a Unity UI panel/popup prefab from a mockup image the user sends. Use whenever the user attaches or references a UI screenshot/design and asks to build, recreate, or implement it ("lam panel nay", "lam UI tu anh nay", "dung cai nay ra prefab", "recreate this screen"). Covers the fast path - a pre-baked project facts file instead of exploring, building the prefab by running C# through the Editor instead of hand-writing YAML, matching sprites by native aspect ratio, the overlay-ghost alignment loop, and play-mode verification.
---

# UI from image

Turn a mockup image into a working, verified panel prefab in one pass, cheaply.

`unity-ui-panel` is the code-convention authority (panel contract, `Initialize`, no comments, button
wiring, template prefabs) and still applies in full. **This skill is the execution pipeline** on top
of it.

## Cost rules - read first

These exist because a build of this kind burned ~270k tokens on avoidable work.

1. **Never spawn Explore/general-purpose subagents to survey the codebase.** Everything they would
   find belongs in the project's facts file (see Step 0). Read that instead; if it does not exist
   yet, create it once and reuse it forever.
2. **Never hand-author `.prefab` YAML.** Build the prefab by running C# through the Editor
   (`script-execute`). Unity emits correct fileIDs, GUIDs and component data itself. Hand-written
   YAML is the single biggest time sink and fails in silent ways: fileID overflow, wrong enum ints,
   missing `CanvasRenderer`.
3. **Never read a whole `.prefab` or `.unity` file.** They are 1k-6k lines. Grep for the handful of
   fields you need, or inspect live objects via `script-execute`.
4. **Ask all clarifying questions in a single `AskUserQuestion` call**, before writing any code.
5. Prefer one batched `script-execute` over several small ones; each round trip is ~5-10s.

## Step 0 - Connect to Unity, and load the facts

Try the native MCP tools first (`editor-application-get-state`). If they are not registered in this
session, use the bundled bridge:

```bash
python3 "${CLAUDE_PLUGIN_ROOT}/skills/unity-ui-from-image/scripts/mcp.py" <tool-name> <args.json>
```

The bridge reads the endpoint from the project's `.mcp.json` - never hard-code a port; each Unity
host listens on its own. Confirm with `scene-list-opened` before trusting any call. Import it for C#
execution:

```python
import os, sys; sys.path.insert(0, os.path.join(os.environ['CLAUDE_PLUGIN_ROOT'], 'skills/unity-ui-from-image/scripts'))
import mcp
print(mcp.run_csharp('Debug.Log("hi");', 'Probe'))          # body-only
print(mcp.call('script-execute', {...}))                    # full class
```

If a tool you need reports `FMP_TOOL_NOT_FOUND`, it is disabled in the Unity plugin. Re-enable it
with `scripts/enable_tools.cs` (run it via `script-execute`, then re-list tools) - it flips the
state and calls `Save(true)`, which is what actually republishes the tool list.

**Then read the project's facts file**, normally `.claude/unity-project-facts.md`. It holds the
paths, design resolution, sorting bands, component/font GUIDs and the sprite catalogue that this
whole pipeline depends on. If the project has none, generate one from
`reference/project-facts-template.md` first - it pays for itself on the first panel, and every later
panel is nearly free. If the file exists but contradicts what you observe in the tree, say so and
regenerate it; a stale facts file is worse than none.

## Step 1 - Read the mockup into a layout spec

Look at the image and write down, before touching code:

- Panel bounds and whether it is a full-screen popup (almost always yes).
- Every region: header, tabs, preview, list/grid, footer, currency pills, close button.
- Which parts are **dynamic lists** (one disabled template + instantiate in `Initialize`) vs
  **fixed boards** (N pre-authored instances wired in order into a serialized `List<>`, per-slot
  cosmetics authored per instance). Every repeated element - list cell, board slot, row, card - maps
  to its OWN template prefab, nested into the panel (see Step 4).
- Which parts are decoration you can skip: system ad banners, device frames, drop shadows.

Then **map every element to its exact sprite.** List the target screen's own art folder: in a
project whose art comes out of Photoshop layer exports, those PNGs are literally the layers the
mockup was composited from. Use those exact files; never substitute a look-alike from another
folder or a tinted generic box. **Folder priority is strict**: the screen's own folder, then shared
widget art, then the generic fallback folder. A look-alike from ANOTHER screen's folder is never a
fallback - only after a ratio dump proves the screen's own folder lacks the element may you go
cross-folder.

**Match by native aspect ratio, not by colour.** Look-alike sprites - especially buttons - share
colour but differ in ratio (a 447x62 wide button at r=7.21 vs a 397x159 one at r=2.50 are both
"the wide green button"). Layer exports already have the exact ratio and colour they show in the
mockup, so ratio is the fingerprint: dump `name | WxH | ratio | border` for every candidate folder
in ONE `script-execute` call (snippet in `reference/project-facts-template.md`), measure each mockup
region's ratio, and pick the sprite whose ratio agrees. Compare pixels only when two candidates tie.

**The urge-to-adjust test.** The correct sprite drops in as `Image.Type.Simple` at native ratio and
simply looks right. If a sprite only fits after you "fix" it - switch to Sliced, stretch it
off-ratio, tint it - that urge IS the wrong-sprite signal; stop and re-match by ratio. Most 2D UI
art has `spriteBorder` `{0,0,0,0}`, so Sliced does nothing there but mask a mismatch. Use `Simple`
at native ratio, `Filled` only on progress fills, `preserveAspect` on icons sitting in
differently-shaped slots.

Convert to concrete numbers against the project's **design resolution** (from the facts file). When
a mockup is itself that resolution, **mockup pixels ARE canvas units, 1:1** - no scaling math. Two
consequences:

- **Sizes are free.** Layers composite at 100%, so an element's on-screen size = its layer PNG's
  native size. Set each Image rect to the sprite's native `WxH` from the ratio dump; never guess a
  size off the image.
- **Positions need the overlay loop.** Read initial positions off the mockup (center-anchored:
  `x = px_x - width/2`, `y = height/2 - px_y`), but eyeballed positions land 20-80 units off - the
  overlay alignment pass in Step 6 is mandatory, not optional polish.

## Step 2 - One question round

Ask these together, only the ones the image leaves genuinely ambiguous:

1. **Data source** - a `ScriptableObject` config, or a placeholder list? A reusable package may not
   touch game enums or game config types.
2. **Interactivity depth** - full behaviour, or debug-log stubs for the gameplay dev?
3. **Any 3D preview** - if the project has no preview-stage rig, assume a static sprite.
4. **Scene wiring** - prefab only, or wired into a scene/UI-manager prefab? Default to prefab only
   and do not touch scenes.

## Step 3 - Write the C# first

Follow `unity-ui-panel` exactly: the panel contract (`Initialize` / `OpenPanel` / `ClosePanel`),
listeners null-guarded remove-then-add, **zero comments**, the fixed regions.

Every new panel also ships its **debug surface**: private `[Button, DisableInEditorMode]` methods
covering each implemented feature - open-with-variant, one button per driving state change wired to
the manager's real public command, and a reset for any persistent state the panel writes. The user
debugs agent-written features from the Inspector through these; a panel without them is incomplete.

Copy the shape of the closest existing file in this project rather than inventing one - the facts
file's "files worth copying from" table names them.

Then `assets-refresh` and confirm compilation with `console-get-logs` filtered to `Error`. Compile
before building the prefab: the builder needs the types to exist.

## Step 4 - Build the prefab through Unity

Adapt `scripts/prefab-builder-template.cs` and run it with `script-execute`
(`isMethodBody: false`). It already encodes the root component order, the rect/image/text helpers,
private-`[SerializeField]` assignment, and `SaveAsPrefabAsset`. Fill in the FILL-IN block at the top
from the facts file.

Three things it does that you must not drop:

- Copies `Canvas`, `GraphicRaycaster` and the framework's panel component off an existing working
  panel via `ComponentUtility.CopyComponent` / `PasteComponentValues`, then forces
  `overrideSorting = true` and the right `sortingOrder`. Reconstructing in/out animations by hand is
  a trap - **the framework's `Show()` silently no-ops unless an In animation channel is enabled.**
- Assigns private `[SerializeField]` fields through
  `SerializedObject.FindProperty(...).objectReferenceValue`.
- **Saves every repeated element as its own prefab.** Build the cell/slot/row fully, then
  `SaveTemplate()` writes it to the template folder via `SaveAsPrefabAssetAndConnect` - the in-panel
  GameObject becomes a nested instance. This must happen BEFORE `SaveAsPrefabAsset` on the panel
  root, or the panel bakes a plain copy. Dynamic list: deactivate the nested instance (the asset
  root stays active). Fixed board: add the other copies with `Nest()`
  (`PrefabUtility.InstantiatePrefab`), never `Object.Instantiate`. Skipping this forces someone to
  hand-extract the template afterwards.

Verify wiring with `scripts/verify-wiring.cs` - it prints every serialized ref, the canvas sorting,
every TMP text's font material, each button's persistent-call count (must be 0), audits every
`Image` for wrong-sprite smells (Sliced on a borderless sprite, Simple stretched far off native
ratio), and flags repeated widgets that are plain children instead of nested template instances.

## Step 5 - Verify in play mode

```
editor-application-set-state  -> enter play
```

Then, in **separate** `script-execute` calls (each call is a later frame - this matters, see Traps):

1. Instantiate under the UI root with `Instantiate(prefab, parent, false)` and assign the manager's
   serialized field.
2. `Initialize(...)` then `OpenPanel()`.
3. `screenshot-game-view` - returns the image inline, no file writing.
4. Interaction pass: 5x open/close plus repeated `Initialize()`, then assert the instantiated-cell
   count did not grow (listener/child duplication is the classic bug here). Invoke buttons with
   `button.onClick.Invoke()`.
5. Debug-surface pass: invoke every `Preview*`/`Test*` method via reflection
   (`GetMethod(name, NonPublic|Instance).Invoke(panel, null)`) and screenshot after each - these
   buttons are how the user debugs the panel, so each must visibly work.

Do the instantiation **at runtime**, never in edit mode, so exiting play mode leaves scenes
untouched. Confirm with `scene-list-opened` that every scene still reports `IsDirty: false`.
Full runbook: `unity-playmode-verify`.

## Step 6 - Overlay alignment pass, then fix visuals, then report

Do **not** judge layout by comparing the screenshot and the mockup side-by-side - that eye-diff is
exactly how panels ship 20-80 units off and mis-sized. Ghost the mockup over the live panel at half
alpha and adjust until edges stop doubling.

1. Game View aspect must match the design resolution, or the full-stretch overlay distorts against
   fixed-size elements and the comparison lies.
2. In play mode with the panel open, spawn the overlay: a `RawImage` showing the mockup, full
   stretch, on its own Canvas at a very high `sortingOrder`, `raycastTarget = false`,
   `color.a = 0.5`. A mockup that lives outside `Assets/` is NOT in the AssetDatabase - load it with
   `File.ReadAllBytes` + `Texture2D.LoadImage`.
3. `screenshot-game-view`. Read the ghosting: a **doubled edge** = position off (shift by the gap,
   in canvas units = mockup pixels); a **haloed / one-side-thick edge** = size off (re-check you
   used native `WxH` - a persistent size mismatch means the wrong sprite, go back to the ratio dump).
4. Patch the prefab (`PrefabUtility.LoadPrefabContents` -> edit -> `SaveAsPrefabAsset`), re-open the
   panel, re-screenshot. Loop until every edge reads single.
5. Final check: screenshot at `alpha 0` (UI only) and `alpha 1` (mockup only) as a clean A/B pair,
   then `Object.Destroy` the overlay. It only ever exists in the play-mode scene - it cannot leak
   into the prefab, and the no-runtime-UI rule does not apply to debug scaffolds.

Re-check `m_OverrideSorting` after any prefab patch.

Defects that showed up on real builds - pre-empt them:

- **Wrong font material.** `tmp.font = f` picks the font asset's default material; a project that
  bakes an outline into a variant needs `fontSharedMaterial` set explicitly. Grep the saved prefab
  to confirm every `m_sharedMaterial` is the expected GUID.
- **Substituted art.** The screen's own folder has the exact BG, board, headline, tabs, checkmark. A
  window built from a tinted generic box reads as wrong when the mockup used the screen's own layer.
- **Sliced as compensation.** A same-coloured but wrong sprite forced into the rect via
  `Image.Type.Sliced` or off-ratio stretching. Needing to adjust means the pick was wrong.
- **Template left as a plain child.** Repeated slots baked into the panel instead of being their own
  template prefab nested in.
- **Transparent window background** - a frame PNG is not a fill.
- **Invisible text**, because default TMP white sits on a white cell. Set a dark colour on light
  backgrounds.
- **Cramped labels** - enable TMP auto-sizing with a sensible `fontSizeMin`.

Report: files created/modified, anything touched outside the UI script and prefab folders (layers,
ProjectSettings), what play-mode checks actually ran, and any reference left null because the scene
was deliberately not wired.

## Traps that cost real time

| Symptom | Cause | Fix |
|---|---|---|
| Panel "opens" but nothing renders; `canvas.enabled == false`, `alpha == 0` right after `OpenPanel()` | the show coroutine does `yield return null` before enabling the canvas | Assert a frame later, i.e. in the next `script-execute` call |
| `Show()` does nothing at all, warning in console | No In animation channel enabled on the panel component | Copy the panel component off a working panel |
| Panel geometry wrong, `localScale` like 0.4 | `Instantiate(prefab, parent)` defaults `worldPositionStays = true` | Use the 3-arg overload with `false` |
| Freshly instantiated panel re-hides itself | `startHidden` runs in `Start()`, after your same-frame `OpenPanel()` | Open it in a later call |
| Panel renders behind the lobby | `sortingOrder` 0 | Use the project's popup band + `overrideSorting` |
| Text has no outline / looks flat | default font material auto-assigned by `tmp.font = f` | Set `fontSharedMaterial` explicitly |
| Sprite looks off and only fits when set to Sliced / stretched | Wrong sprite - a same-coloured sibling with a different native ratio | Re-match against the folder's ratio dump; the right sprite fits at `Simple` + native ratio |
| Button subtly wrong (shape/gloss) though colour matches | Cross-folder look-alike instead of the screen folder's own button | Screen's own folder always wins; cross-folder only after the dump proves absence |
| Everything roughly right but sizes/positions drift vs mockup | Sizes guessed off the image, positions eyeballed from a side-by-side | Size = sprite native `WxH` (mockup px = canvas units 1:1); position via the Step 6 overlay loop |
| Copies of a slot stop following edits to the template prefab | Copies made with `Object.Instantiate` or duplication are plain clones | Create editor-time copies with `PrefabUtility.InstantiatePrefab`; `IsPartOfPrefabInstance` must be true on every copy |
| Runtime edits "applied" but nothing changes on screen | TWO live instances: a prefab-stage clone lives in scene "Preview Scene" and matches `FindObjectsOfTypeAll` filters | Filter by `gameObject.scene.name == "<play scene>"`, never `.First()` on type alone |
| Ink-aligned elements land exactly one scroll-offset off | Canvas targets computed for scroll-top while the ScrollRect was scrolled | Fold the scroll offset into targets, and shift the overlay by the same amount |
| Overlay nudges flip-flop, each "fix" makes it worse | At 0.5 alpha you cannot tell which doubled edge is panel vs mockup, and colour-bbox measurements get contaminated by sparkles/frames/icons | Stop eyeballing: screenshot a clean A/B pair (alpha 0 vs alpha 1), or align numerically - `TMP_Text.textBounds` ink center to canvas space, delta to the measured mockup ink center |
| A glyph renders as an empty box + a TMP warning | The font has no glyph for that codepoint | Use a sprite for the symbol instead |
| Mockup overlay will not load from a folder outside `Assets/` | Unity only imports `Assets/` and packages | `File.ReadAllBytes` + `Texture2D.LoadImage`, not `AssetDatabase` |
| `assets-refresh` "times out" after 10s | Compilation still running; not a failure | Sleep, then poll `EditorApplication.isCompiling` |
| Bridge returns "No Unity editor is connected" | Domain reload from entering/exiting play mode | Retry a few times; it reconnects |

## Bundled files

- `reference/project-facts-template.md` - the shape of the per-project facts file, plus the
  `script-execute` snippet that regenerates the sprite catalogue.
- `scripts/mcp.py` - HTTP/SSE bridge client; resolves the endpoint from the project's `.mcp.json`.
- `scripts/prefab-builder-template.cs` - adapt per panel; fill in the FILL-IN block first.
- `scripts/verify-wiring.cs` - serialized-ref / sorting / button / sprite audit.
- `scripts/enable_tools.cs` - republish disabled MCP tools.

> This folder is safe from `unity-skill-generate`, which only overwrites skills named after MCP
> tools. Do not rename it to a tool name.
