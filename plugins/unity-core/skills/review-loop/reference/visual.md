# Visual loop - matching a reference image

Use this track when a criterion is judged by looking: an effect, a material, lighting, fog or level
dressing that must match a concept image, a before/after spec or a reference capture. Everything in
SKILL.md still holds; this file adds the triage, the frozen shots, the measured gate, the blind pair
judge and the pivot rule.

Why it exists: a run of "fix, then a fresh hostile critic scores 0-10" climbed 1.9 -> 3.9 in eight
rounds and stalled. The props the spec showed in its "after" images were left out of grading, the
inside camera was moved to dodge a rock, each critic was told the last score and how the effect was
built, so scores anchored and proposals stayed inside the current technique. The same "the splash
looks like a cloud" finding came back six rounds running while numbers were retuned, and critics
contradicted each other on colour while the measured colour of that region was already close to the
reference.

## Step 0v - Triage, before any edit

1. Read the whole spec, every section that describes the "after" state - not only the section about
   the part you are building. Whatever an "after" image shows is in scope until the user says
   otherwise.
2. For each reference image, list the elements you can see and tag each one:
   `vfx` (particles, effect shaders), `material`, `lighting` (sun, ambient, fog, local lights), `post`
   (bloom, grading, reflections, depth of field), `geometry` (meshes, rock shapes, props and their
   placement), `camera` (framing), `unreachable` (painted detail the target renderer cannot produce,
   e.g. a concept's wet-floor reflections on a mobile forward renderer).
3. For each element name the technique that reaches it and who owns the asset. A reference made by
   an image model usually changes geometry and lighting as well; when the brief says "only the
   effect", the rest goes into the triage with `in_scope: false` and the user's words - never out of
   grading silently.
4. Write the ceiling: which part of the reference the in-scope set can reach. Show the triage table
   to the user before any code, at every level, and wait for an answer when anything is out of scope
   or unreachable. `loop_gate.py` refuses an out-of-scope item without `excluded_by`.

## Contract additions

```json
{
  "task": "Waterfall entrance matches the after images of the spec",
  "spec": "Docs/Cave/Entrance_Spec.html",
  "reference": {"A": "Docs/Cave/After/A.png", "B1": "Docs/Cave/After/B1.png"},
  "shots": "Docs/Cave/shots.json",
  "triage": [
    {"element": "falling sheet with white columns and deep blue gaps", "kind": "vfx", "in_scope": true},
    {"element": "mossy ledge with lantern sign, front left", "kind": "geometry", "in_scope": true},
    {"element": "wet reflective cave floor", "kind": "unreachable", "in_scope": false,
     "excluded_by": "user: skip reflections, mobile has no SSR"}
  ],
  "criteria": [
    {"id": "V1", "check": "visual", "shot": "B1",
     "text": "Where the head meets the water, thin streaks and droplets are thrown up and out to about twice the head width - not a soft round cloud"},
    {"id": "M1", "check": "metric", "metric": "A.fall.contrast", "ref_tol": 0.03},
    {"id": "M2", "check": "metric", "metric": "particles_alive", "max": 60},
    {"id": "C1", "check": "compile", "text": "Clean console"}
  ],
  "regions": "fall:0-0.6@0.3-0.7,pool:0.6-1"
}
```

- **Frozen**: `init` hashes the reference images, `shots` and `spec`; any change is STOP. Take shots
  from fixed poses with the solo skill's `scripts/ref-shot-capture.cs` (`shots.json`), never from the
  live player camera. A moment that needs a scripted setup (a body half through the water) keeps that
  setup in the file `shots` points at, so it is frozen too.
- **visual** criteria: binary, one observable each, on one shot, written from the spec in "reads as
  X, not Y" form. No 0-10 scores anywhere: a judge can rank two images far better than it can score
  one, and a score invites anchoring.
- **metric** criteria: `shot.region.stat` from ref-compare (`contrast`, `lum_p10`, `lum_p90`, `sat`,
  `score`), `shot.grid` or `shot.regions_score`, or a name passed with `check --value`. Use
  `ref_tol` for "close to the reference", `min`/`max` for budgets. Budgets come from the vfx-core
  skill (particles alive, overdraw, draw calls): a look that blows the budget is not done, and the
  budget is a criterion from round 1, not a cleanup after the loop.

## Each round

```
0. after init, before any change: capture every shot into .review-loop/shots/0/<shot>.png
1. implement                         (the implementer reads pivot from the last verdict)
2. capture                           .review-loop/shots/<N>/<shot>.png, same poses
3. measure, once per shot:
   python "${CLAUDE_SKILL_DIR}/../solo/scripts/ref-compare.py" .review-loop/shots/<N>/<shot>.png <ref>
       --before .review-loop/shots/0/<shot>.png --regions "<contract regions>"
       --json .review-loop/metrics-<N>.json --key <shot> --out .review-loop/sheet-<N>-<shot>.png
4. <gate> check --round <N> --compile ok [--value particles_alive=48 --value overdraw=2.1]
5. <gate> pair --round <N>           (this round vs the best round, blind X/Y)
6. visual judge                      (fresh agent, template in prompts.md) -> judge-<N>.json verbatim
7. <gate> verdict --round <N>
```

Open the sheets yourself every round before you plan the next change: numbers plateau while a big
miss stays obvious side by side.

## Rules the verdict enforces

- **Best round and regressions.** The judge says, per criterion, which of X and Y is closer to the
  reference. If the best round wins any criterion, the round FAILs with REGRESSION and the verdict
  prints the paths to restore from the best round's tree. Restore them before the next change -
  never stack a new change on a regression.
- **Pivot.** A visual or metric criterion that fails two rounds running gets a PIVOT line: retuning
  its numbers is over. The next round changes the technique (a mesh instead of particles, a flipbook
  instead of a noise puff, new geometry from Blender, a different shading model) or ends BLOCKED
  with the alternatives. A third failure in a row stops the loop with the stuck report.
- **Round cap**: 3 rounds at quick, 5 at standard, 7 at strict.

## What the judge never gets

The previous score or verdict, the implementer's summary or a description of how the effect is
built, the chat, a hostile persona ("you hate this author"). A judge told to find faults finds some
whether or not they matter; a judge told the technique proposes only variations of it. The judge
gets the reference, the two blind candidates and the criteria, and may name any technique in
`blocked_by`.

## Report

The last verdict block verbatim, then one ref-compare sheet per shot for the best round, the
out-of-scope list from the triage, and the budget numbers. Not covered: motion and timing between
the frozen shots, unless the contract had a criterion for them.
