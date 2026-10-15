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
  in front of the user.
- If the work lives in a prefab, open it in prefab mode (`AssetDatabase.OpenAsset(prefab)`) so the
  Scene view shows the prefab stage, instead of editing the asset invisibly from code.

## Rule 3 - Every change is seen

Each step, inside the same `script-execute` that makes the change:

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
