# Project facts - template

Copy this to `.claude/unity-project-facts.md` in the Unity project and fill it in ONCE. Every later
UI task reads it instead of exploring, which is the difference between a ~30k-token panel and a
~270k-token one.

Rules for the filled file:

- **Every fact is read from the live tree, never remembered.** Every GUID comes out of a `.meta`.
- **Date it and name what generated it.** A stale facts file is worse than no facts file: it makes
  the agent confidently wrong. If it contradicts the tree, regenerate before doing anything else.
- **Record what does NOT exist too.** Half the cost of exploring is looking for things that are not
  there.

---

## Header

```
# Project facts - <project name> UI
Unity <version>. <render pipeline: built-in / URP / HDRP - say how you checked>.
Generated <YYYY-MM-DD> from the live tree by <script / command>.
```

## Paths

| Thing | Path | Status |
|---|---|---|
| Reusable package(s) | `Packages/<name>/` | |
| Package docs | `Packages/<name>/Documentation~/` | never imported by Unity |
| Game/host scripts | `Assets/<...>/Scripts/` | count, namespace, asmdef or `Assembly-CSharp` |
| UI scripts | `Assets/<...>/Scripts/UI/{Panel,Widget}/` | |
| UI prefabs | `Assets/<...>/Prefabs/UI/{Panel,Widget}/` | |
| UI template prefabs | `Assets/<...>/Prefabs/UI/Template/` | exists? |
| Sprites | `Assets/<...>/Sprites/asset/` | count |
| Mockups | | in AssetDatabase or loose on disk? |
| Fonts | | |
| Scenes | | which one boots |

## Does NOT exist

List the plausible-sounding things that are absent, so nobody hunts for them: managers, prefabs,
config assets, folders, systems.

## Design resolution

`<W> x <H>` (ratio), orientation. Say how it was established - the root `CanvasScaler`'s
`referenceResolution` + `matchWidthOrHeight` if there is one, otherwise the assets that are exactly
that size. Mockup pixels are canvas units 1:1 at this resolution.

## Composition root

Which class owns the UI, what it is called, which prefab hosts it, which scene contains it, and the
boot chain (`X.Start() -> hooks -> Y.Initialize() -> panel.Initialize(rows)`). Note anything that
would NRE if driven before boot.

## Sorting bands

The live band table, e.g. HUD 50-60, popups 200-250, toast 210, transition 300, tutorial 500. Say
whether panels use a nested `Canvas` with `overrideSorting` and whether the order is encoded in the
instance name.

## Panel parking slots

The editor position of each panel instance and the X/Y step new panels continue on.

## Component and font GUIDs

| Asset | guid | source |
|---|---|---|

Built-in uGUI/TMP components use their standard Unity GUIDs; only list the framework and project
ones. Name the exact TMP **material** every label must carry, not just the font asset - assigning
`tmp.font` alone picks the font's default material.

## Sprite catalogue

| path | px | ratio | guid | border |
|---|---|---|---|---|

Note whether any sprite has a non-zero `spriteBorder`; if none do, say so loudly - it means `Sliced`
can only ever mask a wrong-sprite pick. Note any group that shares a ratio to 2 decimal places, so
the agent knows ratio alone cannot separate them.

Regenerate the table with one `script-execute` call:

```csharp
var sb = new System.Text.StringBuilder();
foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/<sprite root>" }))
{
    string p = AssetDatabase.GUIDToAssetPath(guid);
    var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
    if (!s) continue;
    var imp = AssetImporter.GetAtPath(p) as TextureImporter;
    sb.AppendLine(string.Format("{0} | {1}x{2} | {3:0.00} | {4} | {5}",
        p, (int)s.rect.width, (int)s.rect.height,
        s.rect.width / s.rect.height, guid,
        imp != null ? imp.spriteBorder.ToString() : "?"));
}
Debug.Log(sb.ToString());
```

Run the same dump scoped to ONE folder whenever you are matching a mockup region: the ratio is the
fingerprint that separates same-coloured look-alikes.

## Reading mockups

Where the mockups live, and whether Unity imports them. Anything outside `Assets/` and packages -
including `Documentation~/` - is invisible to `AssetDatabase.LoadAssetAtPath`; load it directly:

```csharp
byte[] png = File.ReadAllBytes("<path>");
Texture2D tex = new Texture2D(2, 2);
tex.LoadImage(png);
```

## Files worth copying from

| File | Copy it for |
|---|---|

One row per shape a new panel might need: the panel shell, a dynamic list built from a disabled
template, a fixed board, a timer-driven refresh, a canonical widget, a toast, a staggered intro, a
badge, the editor tooling. Say what each is good for, and call out anything that looks reusable but
is not.
