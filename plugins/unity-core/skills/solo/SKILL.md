---
name: solo
description: Solo mode for a Unity Editor that only this agent drives while the user watches it live - no other agent will take the Editor, so skip any shared-Editor lock and test-case handoff, but every change must happen in plain sight in the Scene view - select, frame and repaint the object before and after each edit to lights, materials, shaders, meshes, transforms or scene layout, one visible step per call, one chat line announcing each step - and the agent runs the task start to finish while taking any advice or interrupt from the user at once. Use when the user says "solo", "/solo", "làm một mình", "tôi xem trên editor", "hiển thị lên scene view", "I'll watch the Editor", or hands over a scene, lighting, material, level-dressing or mesh-placement task to do live in the Editor.
---

# Solo - live in the Editor, user watching

You are the only agent on this Unity Editor. The user is watching the Scene view and will correct you
the moment you go wrong. That correction only works if they can see what you are doing while you do
it. So the job is two things at once: finish the task end to end, and make every step visible.

## Rule 1 - The Editor is yours

- No other agent will take the Editor. Skip the shared-Editor protocol: no lock, no heartbeat, no
  `TestCases/` handoff. Compile, enter play mode, open scenes and run `script-execute` directly,
  following `recompile` and `verify` as usual.
- One sanity check at the start: if `<project>/Temp/agent-editor-lock/` exists, another session thinks
  it owns this Editor. Tell the user and ask before continuing; do not delete it.
- The user is still a person with the same Editor open. They may move an object, change a value or
  press Play while you work. Their edit is an instruction, not a conflict: never revert it, never exit
  their play mode, never answer a dialog you did not open.

## Rule 2 - Set up the stage once

Before the first change, in one `script-execute` (edit mode):

```csharp
var sv = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
sv.Show(); sv.Focus();                       // Scene tab in front of Game view
sv.in2DMode = false;                         // true for a 2D project
sv.sceneLighting = true;                     // lights you change actually light the view
sv.drawGizmos = true;                        // light ranges, colliders, cameras visible
sv.sceneViewState.SetAllEnabled(true);       // skybox, fog, flares, particles, post effects
sv.sceneViewState.alwaysRefresh = true;      // animated shaders and particles keep moving
                                             // (older Unity: showMaterialUpdate)
SceneView.RepaintAll();
```

- Build in **edit mode**. Changes made in play mode vanish on exit, and play mode puts the Game view
  in front of the user. Exception: lighting and atmosphere that runtime code overrides - Rule 7.
- If the work lives in a prefab, open it in prefab mode (`AssetDatabase.OpenAsset(prefab)`) so the
  Scene view shows the prefab stage, instead of editing the asset invisibly from code.

## Rule 3 - Every change is seen

Each step, inside the same `script-execute` that makes the change (steps 1, 2 and 4 are
`scripts/solo-focus.cs`: `Run(target, "close" | "wide" | "keep")`, with a bounds sanity check):

1. **Point at it.** `Selection.activeGameObject = go;` and `EditorGUIUtility.PingObject(go);` -
   the Hierarchy highlights it and the Inspector shows the component you are about to touch.
2. **Frame it.** `sv.FrameSelected();`, or `sv.LookAt(point, rotation, size)` for a chosen angle -
   both animate the camera so the user follows the cut. For a move, frame a view that holds both the
   start and the end position before moving, so the jump is seen, not inferred.
3. **Change it with Undo.** `Undo.RecordObject(target, "solo: <step>")` before editing a component or
   material, `Undo.RegisterCreatedObjectUndo(go, ...)` after creating, `Undo.DestroyObjectImmediate`
   to delete. The user can Ctrl+Z any step you take.
4. **Repaint.** `SceneView.RepaintAll();` at the end. For a material asset, `EditorUtility.SetDirty(mat)`
   too.

Rules for the camera:

- Keep a stable angle while working on one subject. Re-frame when the subject changes, not every call.
- Lighting and mood work: pull back to a wide establishing view of the whole area, not a close-up of
  the light object.
- Material and shader work: frame a renderer that uses the material, close enough to read it.
- Never leave the view pointing at something other than what you just changed.

Things that do not show on their own - handle them, or say so:

| Change | Why it may not show | Do |
|---|---|---|
| Light set to Baked | baked lights only appear after a bake | iterate in Realtime or Mixed; bake at a milestone (`Lightmapping.BakeAsync()`) and say it is baking |
| New or edited shader | async shader compile shows a cyan placeholder | wait a call, check the console for shader errors, then frame it |
| Post-processing volume | the Scene view only shows post effects with the effects toggle on and a matching volume layer | Rule 2 enables the toggle; check the volume's layer and the Scene camera's |
| Object hidden or locked in the Hierarchy | visibility toggles are per-Editor | `SceneVisibilityManager.instance.Show(go, true)` |
| Huge or tiny scale | framing lands inside or far from it | check `Renderer.bounds` before framing |

## Rule 4 - Pace it for a watcher

- **One visible step per call.** "Add the key light", "tint the floor material", "place the three
  crates": each is its own call, framed and repainted. Do not build the whole scene in one big batch -
  the user sees nothing until it is too late to steer. (Batching advice in other skills still holds
  inside a step: one call per step, not five calls per step.)
- **Announce before acting.** One chat line before each step, concrete enough to object to:
  `Next: warm key light, upper left, 30 deg down, intensity 1.2.` Not a paragraph, not a question.
- **Read before writing.** Start each step by reading the current state of what you are about to
  touch (transform, material values, selection). The user may have changed it by hand since your last
  call; build on their version.
- **Raycast before you assign.** Before changing the material of "the cliff" or "the ground" you see
  in a capture, raycast from that camera through those viewport points and read the renderer that is
  hit, its path and its materials. The object named like the thing is often not the one drawn there
  (a separate art mesh, a collider-only proxy, a sibling under another root).
- Keep going. Do not stop between steps to ask permission. Ask only when you are truly blocked or a
  choice is the user's to make (a style, a direction you cannot infer). Then ask one short question and
  wait.

## Rule 5 - Interrupts and advice

The user's message always beats your plan.

- On any new message mid-task, finish only the call already in flight, then stop and read it.
- Acknowledge in one line what changes: `OK - cooler light, keeping the layout.` Then rework the plan
  and continue from there. Do not finish the old batch first.
- "Undo that" means Undo: `Undo.PerformUndo()` for your last step, or restore the values you read in
  Rule 4. Frame the result.
- If the advice conflicts with something you know (it breaks the bake, the material is shared by 40
  prefabs), say so in one line and offer the alternative - then do what the user decides.
- Advice that applies beyond this step ("always keep lights warm") stays in force for the rest of the
  task.

## Rule 6 - Saving and finishing

- Save the scene (`EditorSceneManager.SaveScene`) at each milestone the user has seen without
  objecting, and say so in the step line. Never save over a scene you did not mean to change.
- Shared assets: before editing a material, check whether other objects use it. Editing it changes
  them all, live, in front of the user - if that is not intended, duplicate it first and say so.
- At the end: one wide shot of the result in the Scene view, save, and report what changed - objects
  added, assets edited, settings touched - and anything not shown or not verified (a pending bake, a
  play-mode behavior not run).
- Capture the view with `screenshot-scene-view` if the bridge has it; otherwise render
  `SceneView.lastActiveSceneView.camera` into a `RenderTexture`, write it as PNG under `Temp/`, and
  read the file. Behavior changes still go through `verify`.
- Any capture snippet you write yourself restores `cam.targetTexture` and `RenderTexture.active`,
  then `Object.DestroyImmediate` **both** the RenderTexture and the Texture2D in a `finally`.
  `rt.Release()` alone leaks the object; a few dozen leaked full-size captures in one Editor session
  are enough to push a 4 GB GPU into a driver crash. Prefer `scripts/ref-shot-capture.cs`.

## Rule 7 - Lighting and atmosphere: tune in play mode, judge the game camera

Matching a look (a reference image, a mood brief) is judged on what the game camera renders, not on
the Scene view: the Scene view has no runtime overrides (scripts writing `RenderSettings`, sun,
fog), different post-processing, and a Volume stack that may never update. The Scene view is only for
the user to follow along.

1. **Find the runtime owners first.** Grep for code that writes `RenderSettings`, lights, fog, sky or
   Volume weights at runtime, and check the URP asset's Volume Update Mode. If any exist, enter play
   mode before the first change - values tuned in edit mode against a hand-copied state land far off.
2. **Fixed camera poses.** Capture with `scripts/ref-shot-capture.cs` from named poses in a
   `shots.json` next to the reference images (record a pose once with `record: "sceneview"` or
   `"main"`), never from the player's live camera - a moving character or an enemy in frame makes
   two rounds incomparable. It borrows the Main Camera for one synchronous render, updates the URP
   Volume stack, forces sync shader compile for that frame, and cleans up.
3. **Compare side by side every round.** `python scripts/ref-compare.py current.png ref.png --before
   first.png --out Temp/cmp.png` prints region and grid scores and writes a current | ref | before |
   diff sheet. Open the sheet first; the numbers plateau while a large miss (a shadow pattern, a
   missing layer) is obvious side by side.
4. **A change that shows nothing:** capture an A/B before turning the value further - `toggle:` flips
   one object for a `_A`/`_B` pair, or capture top-down where the effect is easiest to see. Usually
   the field is the wrong one (see the engine's quirks, e.g. `vfx-environment`
   `reference/urp-environment-traps.md`).
5. **Play-mode edits vanish on exit.** Keep every value you settle in a list as you go, exit, reapply
   them in edit mode with Undo, save, and capture once more in play mode to confirm the saved state.
6. **Never reimport or regenerate a texture, shader or mesh that is in use while in play mode**
   (a cookie the sun is using, a sky shader, terrain prototypes). Tune runtime parameters (material
   properties, light fields) in play mode; regenerate assets in edit mode. Reimporting under a live
   GPU resource is the likeliest cause of `SetTextures`/`OpenResource` driver crashes.
7. Global settings (sun, sky, fog, Volume profile, the pipeline asset) change every area and every
   scene that shares them. Name them in the report and list the areas you did not look at.

## Rule 8 - The bridge drops; tell a reload from a crash

- Entering or leaving play mode and every recompile drop the Editor bridge for about 5-25 s. Poll
  `editor-application-get-state` every few seconds with a timeout instead of guessing a sleep; one
  tool (often `script-execute`) can report "not connected" a few seconds after the others are back -
  retry it.
- A timeout from `assets-refresh` or a bridge that stays down for over a minute is not "wait longer":
  check that the Unity process is still alive and whether a new folder appeared under
  `%LOCALAPPDATA%\Temp\Unity\Editor\Crashes` (macOS: `~/Library/Logs/Unity`). If it crashed, read
  the stack in that folder's `Editor.log` before restarting, and tell the user.
- Before a compile or refresh late in a long session (many play-mode round trips, many captures):
  save everything, run `EditorUtility.UnloadUnusedAssetsImmediate()`, and if the session has been
  heavy, suggest an Editor restart first.
- Save and enter play mode in the same call when a scene's `OnValidate` re-dirties it - otherwise
  the play-mode entry stops on an unsaved-scene prompt.

## Scripts

Run with `script-execute`, `isMethodBody: false`; each file's header gives `className`/`methodName`
and parameters (or edit its FILL-IN and call `Main`).

- `scripts/solo-focus.cs` - select, ping, frame (`close` / `wide` / `keep`) and repaint, with a
  bounds check (tiny, huge, hidden, inactive).
- `scripts/ref-shot-capture.cs` - game-camera PNG from a named pose in `shots.json`, optional A/B
  toggle, records poses, leaks nothing.
- `scripts/ref-compare.py` - region + 40x18 grid scores and the side-by-side sheet (Pillow, numpy);
  `--json` feeds review-loop's `metric` criteria.
