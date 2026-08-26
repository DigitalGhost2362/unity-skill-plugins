---
name: ui-from-image
description: Build a Unity UI panel/popup prefab in the reward-system project from a mockup image the user sends. Use whenever the user attaches or references a UI screenshot/design and asks to build, recreate, or implement it ("làm panel này", "làm UI từ ảnh này", "dựng cái này ra prefab", "recreate this screen"). Covers the fast path: pre-baked project facts, building the prefab through Unity via script-execute instead of hand-writing YAML, and play-mode verification.
---

# UI From Image (reward-system)

Turn a mockup image into a working, verified panel prefab in one pass, cheaply.

`unity-ui-panel` is the code-convention authority (BaseUI, SetInfo, no comments, SampleUIManager registration) and still applies in full. **This skill is the execution pipeline** on top of it.

## Cost rules — read first

These exist because the first build of this kind burned ~270k tokens on avoidable work.

1. **Never spawn Explore/general-purpose subagents to survey the codebase.** Everything they would find is already in `reference/project-facts.md`. Read that instead.
2. **Never hand-author `.prefab` YAML.** Build the prefab by running C# through Unity (`script-execute`). Unity emits correct fileIDs, GUIDs and component data itself. Hand-written YAML is the single biggest time sink and fails in silent ways (fileID overflow, wrong enum ints, missing CanvasRenderer).
3. **Never read a whole `.prefab` or `.unity` file.** They are 1k–6k lines. Use `grep` for the handful of fields you need, or inspect live objects via `script-execute`.
4. **Ask all clarifying questions in a single `AskUserQuestion` call**, before writing any code (see Step 2).
5. Prefer one batched `script-execute` over several small ones; each round trip is ~5–10s.

## Step 0 — Connect to Unity

Try the native MCP tools first (`editor-application-get-state`). If they are not registered in this session, use the bundled bridge:

```bash
python3 "${CLAUDE_PLUGIN_ROOT}/skills/ui-from-image/scripts/mcp.py" <tool-name> <args.json>
```

The bridge reads the endpoint from the project's `.mcp.json` — never hard-code a port. Each Unity host listens on its own (reward-system 20266, reward-system-1 20267). Confirm with `scene-list-opened` before trusting any call. Import it for C# execution:

```python
import os, sys; sys.path.insert(0, os.path.join(os.environ['CLAUDE_PLUGIN_ROOT'], 'skills/ui-from-image/scripts'))
import mcp
print(mcp.run_csharp('Debug.Log("hi");', 'Probe'))          # body-only
print(mcp.call('script-execute', {...}))                    # full class
```

If a tool you need reports `FMP_TOOL_NOT_FOUND`, it is disabled in the Unity plugin. Re-enable with `scripts/enable_tools.cs` (run it via `script-execute`, then re-list tools) — it flips the state and calls `Save(true)`, which is what actually republishes the tool list.

## Step 1 — Read the mockup into a layout spec

Look at the image and write down, before touching code:

- Panel bounds and whether it is a full-screen popup (almost always yes).
- Every region: header, tabs, preview, list/grid, footer, currency pills, close button.
- Which parts are **dynamic lists** (→ ONE disabled template + instantiate in `SetInfo`) vs **fixed boards** (→ N pre-authored instances wired in order into a serialized `List<>` on the panel, per-slot cosmetics authored per instance — the shipped pattern: `DailyRewardPanel.cards`, `LuckySpinPanel.wedges`, decision #28). Every repeated element — list cell, fixed slot board, row, card — maps to its OWN template prefab (host: `Assets/_GameBase/Prefabs/UI/Template/`, which you must create; package: inside the feature folder), nested into the panel (see Step 4).
- Which parts are decoration you can skip (system ad banners, device frames, drop shadows).

Then **map every element to its exact sprite**: list `Assets/_GameBase/Sprites/asset/<screen>/` for the target screen — the PNGs there are the Photoshop layer exports the mockup was composited from (`<screen>_<NNNN>_<layer-name>.png`). Use those exact files; never substitute look-alike art from another folder or a tinted generic box (the user has had to fix e.g. a checkmark that should have been the claimed tick `reward_0009_tich-V`). **Folder priority is strict**: screen's own folder → `avatar/` shared widgets → `play/` generic fallback. A look-alike from ANOTHER screen's folder is never a fallback — the die/result screens' green button is `die_0009_button-green` (397×159 r=2.50); grabbing a look-alike from another folder is a user-corrected mistake. Only after the ratio dump proves the screen's folder lacks the element may you go cross-folder. Details and GUIDs: `reference/project-facts.md` § sprites.

**Match by native aspect ratio, not by colour.** Look-alike sprites (especially buttons) share colour but differ in ratio — e.g. `play_0005_button-dai` r=7.21 vs `die_0009_button-green` r=2.50, both wide buttons. The layer exports already have the exact ratio (and colour) they show in the mockup, so ratio is the fingerprint: dump `name | WxH | ratio | border` for every candidate folder in one `script-execute` call (snippet in `reference/project-facts.md` § sprite rule #2), measure each mockup region's ratio, and pick the sprite whose ratio agrees. Compare pixels only when two candidates tie.

**The urge-to-adjust test.** The correct sprite drops in as `Image.Type.Simple` at native ratio and simply looks right. If a sprite only fits after you "fix" it — switch to Sliced, stretch it off-ratio, tint it — that urge IS the wrong-sprite signal; stop and re-match by ratio. (This exact chain — similar-coloured button → ratio off → Sliced to compensate — is a correction the user has had to make by hand.) **Not one of this project's 33 sprites has a 9-slice border** — every `spriteBorder` is `{0,0,0,0}`, so Sliced does nothing here but mask a mismatch. Use `Simple` at native ratio, `Filled` only on progress fills, `preserveAspect` on icons sitting in differently-shaped slots.

Convert to concrete numbers against the **2400×1080 design resolution** (corroborated by `Sprites/preview/reward1.jpg` and `home_0005_BG.png`; there is no root Canvas in this repo to read a CanvasScaler from, so re-confirm against the preview jpgs in `Assets/_ASMR-Tower/Art/preview/`). When a mockup is itself 2400×1080, **mockup pixels ARE canvas units, 1:1** — no scaling math (details: `reference/project-facts.md` § mockup previews). Two consequences:

- **Sizes are free.** Layers composite at 100%, so an element's on-screen size = its layer PNG's native size. Set each Image rect to the sprite's native `W×H` from the ratio dump; never guess a size off the image.
- **Positions need the overlay loop.** Read initial positions off the mockup (center-anchored: `x = px_x − 1200`, `y = 540 − px_y`), but eyeballed positions land 20–80 units off — the overlay alignment pass in Step 6 is mandatory, not optional polish.

A window of 2100×900 centered is a good default and matches existing popups.

## Step 2 — One question round

Ask these together, only the ones the image leaves genuinely ambiguous:

1. **Data source** — a package `ScriptableObject` config, or a host placeholder? Package UI may not touch `_Others/EnumCollection.cs` or any game enum (see `reward-package`). The host has no config `.asset` files yet, so placeholders are the usual answer.
2. **Interactivity depth** — full behaviour, or debug-log stubs for the gameplay dev?
3. **Any 3D preview** — this project has no 3D content and no preview-stage rig; assume a static sprite unless the user says otherwise.
4. **Scene wiring** — prefab only. There is no `UIMainManager.prefab` and no wired scene in this repo yet; default is **prefab only, do not touch scenes**.

## Step 3 — Write the C# first

Host panels go in `Samples~/RewardDemo/Scripts/` (namespace `NabaGame.Reward.Sample`, `Sample` prefix); package panels go in `Packages/com.nabagame.reward/Runtime/Features/<Feature>/` (namespace `NabaGame.Reward`). Follow `unity-ui-panel` exactly: `BaseUI`, `SetInfo`/`OpenPanel`/`ClosePanel`, listeners null-guarded remove-then-add (`RewardUi.Bind`), **zero comments**.

Every new panel also ships its **debug surface** (user requirement): private `[Button, DisableInEditorMode]` methods covering each implemented feature — open-with-variant (`PreviewClaimable`/`PreviewClaimed`), one button per driving state change (`PreviewAddPlaytime(60)` → the manager's public command), and a reset for any persistent state the panel writes. The user debugs agent-written features from the Inspector through these; a panel without them is incomplete.

Copy the shape of the closest existing file rather than inventing:
- selection cell → `UI/Widget/RewardDailyItem.cs`
- dynamic list built from a disabled template → `UI/Panel/RewardDailyTab.cs`
- tab/selected-sprite swap → `UI/Panel/RewardPanel.cs`

Then `assets-refresh` and confirm compilation with `console-get-logs` filtered to `Error`. Compile before building the prefab — the builder needs the types to exist.

## Step 4 — Build the prefab through Unity

Adapt `scripts/prefab-builder-template.cs` and run it with `script-execute` (`isMethodBody: false`). It already encodes the correct root (RectTransform + Canvas + CanvasGroup + GraphicRaycaster + UIPanel + your panel), the helper functions for rects/images/text, and `SaveAsPrefabAsset`.

Three things it does that you must not drop:
- Copies `Canvas`, `GraphicRaycaster` and `UIPanel` off an existing working panel via `ComponentUtility.CopyComponent` / `PasteComponentValues`, then forces `overrideSorting = true; sortingOrder = 200`. Reconstructing `inAnimations`/`outAnimations` by hand is a trap — **`UIElement.Show()` silently no-ops unless an In animation is enabled.**
- Assigns private `[SerializeField]` fields through `SerializedObject.FindProperty(...).objectReferenceValue`.
- **Saves every repeated element as its own prefab.** Build the cell/slot/row fully, then `SaveTemplate()` writes it to `Assets/_GameBase/Prefabs/UI/Template/<Widget>.prefab` (host) or the package feature folder via `SaveAsPrefabAssetAndConnect` — the in-panel GO becomes a nested instance. This must happen BEFORE `SaveAsPrefabAsset` on the panel root, or the panel bakes a plain copy. Dynamic list → deactivate the nested instance (asset root stays active). Fixed board (a 7-day strip, a spin wheel's wedges) → add the other copies with `Nest()` (`PrefabUtility.InstantiatePrefab`), never `Object.Instantiate`. A build that skips this forces the user to hand-extract the template afterwards.

Verify wiring with `scripts/verify-wiring.cs` — it prints every serialized ref, the canvas sorting, every TMP text's font material (must be `PassionOne-Bold Atlas Black`), each button's persistent-call count (must be 0; this project forbids Inspector OnClick), audits every `Image` for wrong-sprite smells (Sliced on a borderless sprite, Simple stretched far off the sprite's native ratio), and flags repeated widgets/templates that are plain children instead of nested template-prefab instances. It accepts both the `PainAndSeek` and `NabaGame.Reward` namespaces.

## Step 5 — Verify in play mode

```
editor-application-set-state  → enter play
```
Then, in **separate** `script-execute` calls (each call is a later frame — this matters, see Traps):

1. Instantiate under `UIManager.Instance.transform` with `Instantiate(prefab, parent, false)` and assign `UIManager.Instance.<yourPanel>`.
2. `SetInfo(...)` then `OpenPanel()`.
3. `screenshot-game-view` — returns the image inline, no file writing.
4. Interaction pass: 5× open/close plus repeated `SetInfo()`, then assert the instantiated-cell count did not grow (listener/child duplication is the classic bug here). Invoke buttons with `button.onClick.Invoke()`.
5. Debug-surface pass: invoke every `Preview*`/`Test*` method via reflection (`GetMethod(name, NonPublic|Instance).Invoke(panel, null)`) and screenshot after each — these buttons are how the user debugs the panel, so each must visibly work.

Do the instantiation **at runtime**, never in edit mode, so exiting play mode leaves scenes untouched. Confirm with `scene-list-opened` that every scene still reports `IsDirty: false`.

## Step 6 — Overlay alignment pass, then fix visuals, then report

Do **not** judge layout by comparing the screenshot and the mockup side-by-side — that eye-diff is exactly how panels shipped 20–80 units off and mis-sized. Reproduce the user's manual technique instead: ghost the mockup JPG over the live panel at half alpha and adjust until edges stop doubling.

1. Game View aspect must be 20:9 (e.g. 2400×1080), or the full-stretch overlay distorts against fixed-size elements and the comparison lies.
2. In play mode with the panel open, spawn the overlay (recipe + snippet: `reference/project-facts.md` § mockup previews): a `RawImage` showing the mockup. Only `Sprites/preview/reward1.jpg` is inside the AssetDatabase; the preview jpgs in `Assets/_ASMR-Tower/Art/preview/` are imported, but any mockup outside `Assets/` is NOT — load those with `File.ReadAllBytes` + `Texture2D.LoadImage`, full-stretch, own Canvas `sortingOrder = 999`, `raycastTarget = false`, `color.a = 0.5`.
3. `screenshot-game-view`. Read the ghosting: a **doubled edge** = position off (shift by the gap, in canvas units = mockup pixels); a **haloed/one-side-thick edge** = size off (re-check you used native `W×H` — persistent size mismatch means the wrong sprite, go back to the ratio dump).
4. Patch the prefab (`PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset`), re-open the panel, re-screenshot. Loop until every edge reads single.
5. Final check: screenshot at `alpha 0` (UI only) and `alpha 1` (mockup only) as a clean A/B pair, then `Object.Destroy` the overlay. It only ever exists in the play-mode scene — it cannot leak into the prefab, and the no-runtime-UI rule does not apply to debug scaffolds.

Re-check `m_OverrideSorting` after any prefab patch.

Defects that showed up on real builds (all later corrected by the user — pre-empt them):
- **Wrong font material.** `tmp.font = f` picks the default "PassionOne-Bold Atlas Material"; the project uses **`PassionOne-Bold Atlas Black.mat`** (guid `1892185b89f456d48b81da49a8047168`, outline baked in). Set `fontSharedMaterial` explicitly and grep the saved prefab to confirm every `m_sharedMaterial` is that guid.
- **Substituted art.** The screen's own folder (`Sprites/asset/<screen>/`) has the exact BG, board, headline, tabs, checkmark. A window built from `play_0006_white-box-1` tinted dark reads as wrong when the mockup used the screen's `_BG`/`_board` layer.
- **Sliced as compensation.** A same-coloured but wrong button/box sprite got forced into the rect via `Image.Type.Sliced` or off-ratio stretching. The art is pre-sized to the mockup — needing to adjust means the pick was wrong; re-match by native ratio instead. Sliced is legitimate only on the few genuinely bordered sprites (`play_0006_white-box-1`, `inventory_0009_black-board`) used as fallback fills.
- **Template left as a plain child.** Repeated slots/cells were baked into the panel instead of being their own template prefab nested in — forcing a hand-extract and re-connect afterwards.
- **Transparent window background.** `home_0000_black-box.png` is a frame, not a fill — only pick a filled sliced box if the screen truly has no BG layer.
- **Invisible text**, because default TMP white sits on white cells. Set label/price to a dark colour on light backgrounds.
- **Cramped labels** — enable TMP auto-sizing with a sensible `fontSizeMin`.

Report: files created/modified, anything touched outside `Assets/_GameBase/Scripts` and `Prefabs/UI` (layers, ProjectSettings), what play-mode checks actually ran, and any ref left null because the scene was deliberately not wired.

## Traps that cost real time

| Symptom | Cause | Fix |
|---|---|---|
| Panel "opens" but nothing renders; `canvas.enabled == false`, `alpha == 0` right after `OpenPanel()` | `iShow` does `yield return null` before enabling the canvas | Assert a frame later, i.e. in the next `script-execute` call |
| `Show()` does nothing at all, warning in console | No In animation enabled on `UIPanel` | Copy `UIPanel` off a working panel |
| Panel geometry wrong, `localScale` like 0.4 | `Instantiate(prefab, parent)` defaults `worldPositionStays = true` | Use the 3-arg overload with `false` |
| Freshly instantiated panel re-hides itself | `startHidden` runs in `Start()`, after your same-frame `OpenPanel()` | Open it in a later call |
| Panel renders behind the lobby | `sortingOrder` 0 | Popups are **200** + `overrideSorting`, animated sub-groups 201 (see `RewardPanel.prefab`) |
| Text has no black outline / looks flat | default font material auto-assigned by `tmp.font = f` | Set `fontSharedMaterial` to `PassionOne-Bold Atlas Black.mat` (`1892185b...`) |
| Sprite looks off and only fits when set to Sliced / stretched | Wrong sprite — a same-coloured sibling with a different native ratio | Re-match against the folder's ratio dump; the right sprite fits at `Simple` + native ratio |
| Button subtly wrong (shape/gloss) though colour matches | Cross-folder look-alike instead of the screen folder's own button | Screen's own folder always wins; cross-folder only after the dump proves absence |
| Everything roughly right but sizes/positions drift vs mockup | Sizes guessed off the image, positions eyeballed from a side-by-side | Size = sprite native `W×H` (mockup px = canvas units 1:1); position via the Step 6 overlay ghost loop |
| Copies of a slot stop following edits to the Template prefab | Copies made with `Object.Instantiate` or duplication are plain clones, not prefab instances | Create editor-time copies with `PrefabUtility.InstantiatePrefab`; `IsPartOfPrefabInstance` must be true on every copy |
| Runtime edits "applied" but nothing changes on screen | TWO live instances: a prefab-stage clone lives in scene "Preview Scene" and matches `FindObjectsOfTypeAll` filters | Filter by `gameObject.scene.name == "<play scene>"`, never `.First()` on type alone |
| Ink-aligned elements land exactly one scroll-offset off | Canvas targets computed for scroll-top while the ScrollRect was scrolled | Fold the scroll offset into targets: `target_y = 540 + scrolledPx − mockup_y` (and shift the overlay by the same `scrolledPx`) |
| Overlay nudges flip-flop, each "fix" makes it worse | At 0.5 alpha you can't tell which doubled edge is panel vs mockup, and colour-bbox measurements get contaminated by sparkles/frames/icons | Stop eyeballing: screenshot a clean A/B pair (alpha 0 vs alpha 1), or align numerically — `TMP_Text.textBounds` ink center → canvas space → delta to the measured mockup ink center |
| ✓ renders as □ + TMP warning | PassionOne has no U+2713 glyph | Sprite `reward_0009_tich-V` (`78e997e6537efe7429c21f55dd900712`) |
| Mockup overlay will not load from a folder outside `Assets/` | Unity only imports `Assets/` and packages | `File.ReadAllBytes` + `Texture2D.LoadImage`, not `AssetDatabase` |
| `assets-refresh` "times out" after 10s | Compilation still running; not a failure | Sleep, then poll `EditorApplication.isCompiling` |
| Bridge returns "No Unity editor is connected" | Domain reload from entering/exiting play mode | Retry a few times; it reconnects |

## Bundled files

- `reference/project-facts.md` — paths, script/component GUIDs, the full UI sprite catalogue, font, conventions, preview-stage recipe. **Read this instead of exploring.**
- `scripts/mcp.py` — HTTP bridge client.
- `scripts/prefab-builder-template.cs` — adapt per panel.
- `scripts/verify-wiring.cs` — serialized-ref/sorting/button audit.
- `scripts/enable_tools.cs` — republish disabled MCP tools.

> This folder is safe from `unity-skill-generate`, which only overwrites skills named after MCP tools. Do not rename it to a tool name.
