---
name: ui-from-image
description: Reward-repo delta for building a UI panel prefab from a mockup image. Read unity-ui-from-image first - it is the pipeline (cost rules, sprite matching by native ratio, building the prefab through Unity, the overlay alignment loop, the traps table) and it ships the bundled scripts. This file holds only the reward-repo facts - the Unity host ports and where the project facts file lives. Use whenever the user attaches or references a UI screenshot/design and asks to build it ("lam panel nay", "lam UI tu anh nay", "dung cai nay ra prefab", "recreate this screen").
---

# UI from image - reward delta

**Run `unity-ui-from-image`.** It owns the whole pipeline and ships the bundled
scripts (`mcp.py`, `prefab-builder-template.cs`, `verify-wiring.cs`) - invoke that
skill so the scripts resolve under its own `${CLAUDE_SKILL_DIR}`, and do not copy them here.

`reward-system:unity-ui-panel` is the code-convention authority on top of it.

## Repo facts

- **Project facts file: `reference/project-facts.md` in this skill folder** - read it instead of
  exploring. Check its generated-on date against the tree before trusting it; if it contradicts what
  you observe, say so and regenerate it rather than working around the contradiction.
- **Unity host ports**: reward-system listens on 20266, reward-system-1 on 20267. The bridge reads
  the endpoint from the project's `.mcp.json`, so never hard-code either - confirm with
  `scene-list-opened` before trusting any call.
- Host panels go in `Samples~/RewardDemo/Scripts/` (namespace `NabaGame.Reward.Sample`, `Sample`
  prefix); package panels in `Packages/com.nabagame.reward/Runtime/Features/<Feature>/` (namespace
  `NabaGame.Reward`). Package templates ship inside the feature folder, not in a host template folder.
- The one question the mockup usually leaves open here: package `ScriptableObject` config vs a host
  placeholder. Package UI may not touch any game enum (see `reward-package`), and the host has no
  config `.asset` files yet, so placeholders are the usual answer. Default to **prefab only, do not
  touch scenes**.
