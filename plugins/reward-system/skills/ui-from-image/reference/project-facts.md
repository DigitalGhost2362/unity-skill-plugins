> **STALE - REGENERATE BEFORE TRUSTING.** This file was written against the tree as it stood on
> 2026-08-19, right after the strip-to-reward-core, and it now contradicts `reward-package` and
> `reward-system:unity-coding` on the most load-bearing facts: it says the package is "docs only -
> zero C#, no asmdef" and that host scripts live in `Assets/_GameBase/Scripts/` under namespace
> `PainAndSeek`, while the package has since shipped `NabaGame.Reward` runtime code with the demo
> host under `Samples~/RewardDemo/`. Regenerate it from the live tree - the template and the
> sprite-catalogue dump snippet are in `unity-ui-from-image`'s
> `reference/project-facts-template.md` - before using anything below. Only the sprite catalogue
> (GUIDs read from `.meta`) and the font GUIDs are likely to have survived unchanged.

# Project facts — reward-system UI

Unity **2022.3.62f3**. **No URP** (built-in render pipeline; `universal` appears nowhere in `Packages/manifest.json`).
Regenerated 2026-08-19 from the live tree after the strip-to-reward-core. Every guid below was read from its `.meta`.

This repo is a **fork of `D:\Fork\paint-and-seek` stripped to reward-core**. Most of that project's panels, sprites and folders are gone. If a fact here contradicts something you remember from paint-and-seek, this file wins.

---

## Paths

| Thing | Path | Status |
|---|---|---|
| Package (the product) | `Packages/com.nabagame.reward/` | **docs only — zero C#, no asmdef** |
| Package docs | `Packages/com.nabagame.reward/Documentation~/` | ARCHITECTURE / CONVENTIONS / ROADMAP / FEATURES / RefUI |
| UI mockups | `Packages/com.nabagame.reward/Documentation~/RefUI/` | **all 3 PNGs missing** — README lists them as "awaiting image" |
| Demo-host scripts | `Assets/_GameBase/Scripts/` | 41 files, namespace `PainAndSeek`, no asmdef → `Assembly-CSharp` |
| Host UI scripts | `Assets/_GameBase/Scripts/UI/{Panel,Widget}/` | 4 panel files + 6 widget files |
| Host UI prefabs | `Assets/_GameBase/Prefabs/UI/{Panel,Widget}/` | **exactly 2 prefabs** — `Panel/RewardPanel.prefab`, `Widget/RedDot.prefab` |
| Sprites | `Assets/_GameBase/Sprites/asset/` | 33 PNGs (see catalogue) |
| Mockup JPGs | `Assets/_GameBase/Sprites/preview/` | 1 file: `reward1.jpg` (2400×1080) |
| Fonts | `Assets/_GameBase/Fonts/` | PassionOne-Bold ttf + SDF + Atlas Black material |
| Scenes | `Assets/Scenes/SampleScene.unity` | camera + light only, **zero MonoBehaviours** |

**Does NOT exist** (do not reference): `Prefabs/Manager/`, `UIMainManager.prefab`, `Prefabs/UI/Template/`, `Assets/_GameBase/Datas/`, `Assets/_GameBase/Scenes/`, `Assets/_Temp/`, `Hack.cs`/`SROptions`, `ObjectPool`, `MatchTimer`, `QuestManager`, `ShopManager`, `LevelManager`, and every panel except `RewardPanel` and its three tabs.

## Design resolution

**2400 × 1080** (ratio 2.22), landscape. There is no root Canvas in this repo to read a `CanvasScaler` from — the figure is corroborated by two surviving assets that are exactly that size: `Sprites/preview/reward1.jpg` and `Sprites/asset/home/home_0005_BG.png`. The reference project used ScaleWithScreenSize with `matchWidthOrHeight: 0.5`. **Confirm against the RefUI mockups once they arrive** before hard-coding layout numbers.

## Composition root — not built yet

`Assets/_GameBase/Scripts/Manager/UIManager.cs` is a `UIManagerSingleton<UIManager>` with exactly two serialized fields:

```csharp
[SerializeField, FoldoutGroup("Common")] public RewardPanel rewardPanel;
[SerializeField, FoldoutGroup("Common")] public ToastWidget toastWidget;
[SerializeField] List<BaseUI> checkHasPopup = new();
```

There is **no prefab hosting it and no scene containing it**. `EditorBuildSettings` has `m_Scenes: []`. Building the demo scene (root Canvas + UIManager prefab + config `.asset` files) is outstanding work — see the `reward-package` skill, Rule 8. Until then, nothing runs: `RewardManager.StartClass()` would NRE on `GameManager.Instance.rewardDataCollection`, because **no config `.asset` exists** (`RewardDataCollection`, `RewardConfig`, `GameConfig`, `SettingData`, `SpriteCollection` exist as classes only).

## Sorting orders

Only one panel exists, so there is no live band table. `RewardPanel.prefab` uses **200** on its root Canvas and **201** on each nested animated group — the reference project's convention. Adopt the reference bands when building more (HUD 50–60, popups 200–250, toast 210, transition 300, tutorial 500); they are documented in `unity-ui-panel`, `reference/ui-reference-hierarchy.md` section B4.

## Component GUIDs

Built-in uGUI/TMP components (`Image`, `Button`, `GraphicRaycaster`, `CanvasScaler`, `TextMeshProUGUI`) use their standard Unity guids. Framework components:

| Component | guid | source |
|---|---|---|
| `BaseUI` | `81d7bd4b…` | `Library/PackageCache/com.nabagame.ui@…/Runtime/Scripts/BaseUI.cs` |
| `UIPanel` | `b1750a62…` | same package; verified in use inside `RewardPanel.prefab` |

## Fonts

| Asset | guid |
|---|---|
| `Fonts/PassionOne-Bold Atlas Black.mat` | `1892185b89f456d48b81da49a8047168` |
| `Fonts/PassionOne-Bold SDF.asset` | `8bf226b02a873e942b04b101f2a31744` |
| `Fonts/PassionOne-Bold.ttf` | `155ea220efb0a194d96dfe8d1f9002bf` |

Every `TextMeshProUGUI` uses the **`PassionOne-Bold Atlas Black` material**, not the plain SDF asset's default material.

## Sprite catalogue — all 33, verified 2026-08-19

**No sprite in this project has a 9-slice border.** Every `spriteBorder` is `{0,0,0,0}`. Scale by ratio, or accept the stretch; do not assume slicing.

| path | px | ratio | guid |
|---|---|---|---|
| `asset/avatar/avatar_0004_tab-gray.png` | 397×159 | 2.50 | `1e2d2b032907ea1468a4324f6774993d` |
| `asset/avatar/avatar_0005_tab-blue.png` | 397×159 | 2.50 | `298e27a6d9af1e24ebc66f5cd4233c04` |
| `asset/avatar/avatar_0006_tab-pink.png` | 397×159 | 2.50 | `34c55504c1f49d1438b3efc1a50e056d` |
| `asset/avatar/avatar_0007_tab-light-blue.png` | 397×159 | 2.50 | `27e46f76b232170469ab62d2a9911cbe` |
| `asset/die/die_0008_button-orange.png` | 397×159 | 2.50 | `9ab68f58078cbd94bbcd2c504dc6654b` |
| `asset/die/die_0009_button-green.png` | 397×159 | 2.50 | `3dba4e248c8a8134b8fd27f4efee2d49` |
| `asset/die/die_0010_button-rainbow.png` | 397×159 | 2.50 | `134922d025216c44c9d2cea0f7418fe2` |
| `asset/home/home_0005_BG.png` | 2400×1080 | 2.22 | `247454a87c7a24f459e1854b3a543374` |
| `asset/Icon/Icon_Crate/Chest_Weapon_PREMIUM.png` | 600×600 | 1.00 | `38b85c2c07f36f049ac4b36f22504226` |
| `asset/Icon/Icon_Crate/Chest_Weapon_Rare.png` | 600×600 | 1.00 | `8110446ff4fc67440ba751e017f81432` |
| `asset/Icon/Icon_Pet/PirateCat.png` | 250×250 | 1.00 | `b06e636eafe405c4daf7bb5250bd416b` |
| `asset/Icon/Icon_Pet/VampireBat.png` | 250×250 | 1.00 | `3e30d852957d5114b8e5bc847a0c301c` |
| `asset/inventory/inventory_0003_box-violet.png` | 237×298 | 0.80 | `a911d22cf9928c3498716931ff1abadf` |
| `asset/level pass/level-pass_0010_headline.png` | 2400×165 | 14.55 | `82c758ea89186574e8ec4d9ee8482ba8` |
| `asset/play/close-button.png` | 88×94 | 0.94 | `ff8e0ca330523f141a312f288e9a9e99` |
| `asset/play/play_0000_video.png` | 39×38 | 1.03 | `6e99f2f2d0664a64f9bd2ce20b1363e3` |
| `asset/play/play_0005_button-dai.png` | 447×62 | 7.21 | `ffceb904c4a69f14e95ce7bd55e5fa6b` |
| `asset/play/play_0006_white-box-1.png` | 52×52 | 1.00 | `36b7b88e4f57fda4093961d8e7327b9e` |
| `asset/quest/quest-red-note-2.png` | 89×83 | 1.07 | `254ad3488c617e149be25468f1ce1bab` |
| `asset/reward/reward_0000_AD.png` | 67×75 | 0.89 | `472ec3de004bc1f47b659d85b195db33` |
| `asset/reward/reward_0001_daily.png` | 68×70 | 0.97 | `21d6ea1a5e8a2384f841fe1be706e4be` |
| `asset/reward/reward_0002_playtime.png` | 75×51 | 1.47 | `d807b8491c0996641a764bc597d628b8` |
| `asset/reward/reward_0003_gift.png` | 130×130 | 1.00 | `9563f4417ec7f8942bcc80ccdbe7139f` |
| `asset/reward/reward_0004_PREMIUM-CHEST.png` | 514×64 | 8.03 | `0f562b6e065cef341b5d69824f0f6088` |
| `asset/reward/reward_0005_progress-1.png` | 569×53 | 10.74 | `fc4a6581c53e5844cb2a9875f888d9a5` |
| `asset/reward/reward_0006_progress-2.png` | 577×61 | 9.46 | `3516d7891e0d72b49b70ea9711ebd80d` |
| `asset/reward/reward_0007_big-box-violet.png` | 834×685 | 1.22 | `8f0b4d6643af9c14e8c5b10f0ba87677` |
| `asset/reward/reward_0008_big-box-yellow.png` | 834×685 | 1.22 | `32dc864e637875843acc1cdd95940ce0` |
| `asset/reward/reward_0009_tich-V.png` (claimed tick) | 169×127 | 1.33 | `78e997e6537efe7429c21f55dd900712` |
| `asset/reward/reward_0010_D7.png` | 578×703 | 0.82 | `da8ae2a1859670940ab5bc6c86d199d5` |
| `asset/reward/reward_0011_select.png` | 362×362 | 1.00 | `b0db33b66ca380f41861f16e9332b3e8` |
| `asset/reward/reward_0012_D1-6.png` | 334×334 | 1.00 | `b7ffe86882d5fca4e9f2bb9cad9c1b29` |
| `asset/reward ads/reward-ads_0002_light.png` | 478×478 | 1.00 | `640b80ad8c2b41a4b87fca6a80b7243c` |
| `preview/reward1.jpg` (mockup) | 2400×1080 | 2.22 | `6cf09f8cf08aaa64b8ea947d4e622e72` |

**Identify a sprite by aspect ratio, then by name.** The four `avatar_*_tab-*` and three `die_*_button-*` files are all exactly 397×159 (ratio 2.50) — the ratio alone cannot separate them, only the colour in the filename can. Nothing else in the project shares a ratio to 2 decimal places.

To regenerate this table after adding art, re-run the scratchpad script that produced it (walks `Sprites/`, reads PNG/JPG headers for size and `.meta` for guid + `spriteBorder`).

## Reading the RefUI mockups

The package mockups live in `Documentation~/`, which **Unity never imports** — `AssetDatabase.LoadAssetAtPath` cannot reach them, so the usual overlay recipe fails. Load the bytes directly instead:

```csharp
byte[] png = File.ReadAllBytes("Packages/com.nabagame.reward/Documentation~/RefUI/daily-reward.png");
Texture2D tex = new Texture2D(2, 2);
tex.LoadImage(png);
```

`Sprites/preview/reward1.jpg` **is** importable and is the one mockup already inside the AssetDatabase.

## Files worth copying from

The strip removed every panel the old version of this file cited. What actually survives, and what each is good for:

| File | Copy it for |
|---|---|
| `Assets/_GameBase/Scripts/UI/Panel/RewardPanel.cs` | `BaseUI` panel shell: `bool started` guard, remove-then-add listeners, `OpenPanel(Action)`/`ClosePanel()`, tab switching, `PlayShellAnimation()` intro replay |
| `Assets/_GameBase/Scripts/UI/Panel/RewardDailyTab.cs` | dynamic list built in `StartClass()` from a disabled in-prefab template |
| `Assets/_GameBase/Scripts/UI/Panel/RewardPlaytimeTab.cs` | timer-driven refresh + ad-boosted buttons (`BoosterButton`) |
| `Assets/_GameBase/Scripts/UI/Widget/RewardDailyItem.cs` | canonical widget: `StartClass(id, label, data, callback)` + `Refresh(state)` + `PlayIntro(delay)` |
| `Assets/_GameBase/Scripts/UI/Widget/RewardPlaytimeSlot.cs` | slot widget with a countdown label |
| `Assets/_GameBase/Scripts/UI/Widget/ToastWidget.cs` | self-initialising DOTween sequence with `SetLink(gameObject, LinkBehaviour.KillOnDisable)` |
| `Assets/_GameBase/Scripts/UI/Widget/TweenIntro.cs` | staggered intro; caller owns the `List<Tween>` store and must `Stop()` in `OnDisable` |
| `Assets/_GameBase/Scripts/UI/Widget/RedDotView.cs` | badge driven by `RedDotChangedEvent`, plus manual `SetOn(bool)` |
| `Assets/_GameBase/Scripts/Editor/BaseUIInspectorProcessor.cs` | the Odin processor injecting OpenPanel/ClosePanel buttons — **only reaches `Assembly-CSharp`, not package panels** |
| `Assets/_GameBase/Prefabs/UI/Panel/RewardPanel.prefab` | the only full panel prefab: nested-Canvas-per-animated-group layout, disabled templates, `UIPanel` animation settings |

`SpinEffect.cs` is a 28-line infinite `DORotate` looper — useful for a glow, **not** reusable as a spin-to-wedge tween for Lucky Spin.

## Template prefabs

`Assets/_GameBase/Prefabs/UI/Template/` **does not exist yet**. The two surviving templates (`RewardPanel/DailyItemTemplate`, `PlaytimeSlotTemplate`) are plain disabled children inside `RewardPanel.prefab`, predating the extract-to-prefab rule. When you touch them, extract them per `unity-ui-panel` Rule 4. Package templates ship inside the package feature folder, not here.
