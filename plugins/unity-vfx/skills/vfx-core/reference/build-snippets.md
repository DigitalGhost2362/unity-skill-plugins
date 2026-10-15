# Build snippets

Everything runs through the Editor (`script-execute`, `isMethodBody: false`). Prefabs and scenes
are never hand-written or read whole. Runtime C# follows the project's `coding` skill: reuse the
project's pool, timer and event owners before adding any of the code below.

## Build a prefab

Start from `scripts/vfx-build-template.cs`: root controller (no emission, no renderer, Play/Stop
drives the children), one child per layer, every layer `scalingMode = Hierarchy`, auto seed,
own sorting order, VFX/Uber material and the stream contract already set. Its example is a mobile
hit (flash + ring mesh particle + sparks). Replace `Build()`; keep the helpers.

Rules the template already follows - keep them when you edit:
- Stop a new ParticleSystem before setting `main.duration` or seeds (Unity refuses while playing).
- A Mesh particle takes its size from `startSize` x the mesh (authored at radius 1).
- A ring/arc/cylinder MESH takes a band texture (soft across V, e.g. `T_VFX_Trail`); radial shapes
  (`T_VFX_Ring`, `T_VFX_Glow`) belong on billboards and quads. The wrong pairing shows a stray arc.
- Mesh particles flat on the ground: `renderer.alignment = World` and no start rotation.
- Mesh vertex colors are Color32.

## Read back only what you need

```csharp
public class VfxPeek
{
    public static void Main()
    {
        var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/<path>.prefab");
        var sb = new System.Text.StringBuilder();
        foreach (var ps in go.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
        {
            var m = ps.main; var r = ps.GetComponent<UnityEngine.ParticleSystemRenderer>();
            sb.AppendLine($"{ps.name}: dur {m.duration} loop {m.loop} life {m.startLifetime.constantMin}-{m.startLifetime.constantMax} " +
                $"size {m.startSize.constantMin}-{m.startSize.constantMax} max {m.maxParticles} " +
                $"mode {r.renderMode} order {r.sortingOrder} mat {(r.sharedMaterial ? r.sharedMaterial.name : "-")}");
        }
        UnityEngine.Debug.Log(sb.ToString());
    }
}
```

## Pool (runtime)

```csharp
// One pool per effect prefab, pre-warmed at load. Replays instead of instantiating.
public sealed class VfxPool
{
    readonly ParticleSystem prefab;
    readonly Stack<ParticleSystem> free = new();

    public VfxPool(ParticleSystem prefab, int prewarm)
    {
        this.prefab = prefab;
        for (int i = 0; i < prewarm; i++) free.Push(Create());
    }

    public ParticleSystem Play(Vector3 position, Quaternion rotation, float scale = 1f)
    {
        var fx = free.Count > 0 ? free.Pop() : Create();
        fx.transform.SetPositionAndRotation(position, rotation);
        fx.transform.localScale = Vector3.one * scale;
        fx.gameObject.SetActive(true);
        fx.Clear(true);
        fx.Play(true);
        return fx;
    }

    ParticleSystem Create()
    {
        var fx = Object.Instantiate(prefab);
        // The callback fires on the system that owns it, when that system is done: put it on the
        // layer that lives longest, not on an empty controller that finishes first.
        ParticleSystem last = fx;
        float end = -1;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            float e = m.startDelay.constantMax + m.duration + m.startLifetime.constantMax;
            if (ps.emission.enabled && e > end) { end = e; last = ps; }
        }
        var main = last.main;
        main.stopAction = ParticleSystemStopAction.Callback;
        last.gameObject.AddComponent<VfxReturn>().Bind(this, fx);
        fx.gameObject.SetActive(false);
        return fx;
    }

    internal void Return(ParticleSystem fx) { fx.gameObject.SetActive(false); free.Push(fx); }
}

public sealed class VfxReturn : MonoBehaviour
{
    VfxPool pool; ParticleSystem fx;
    public void Bind(VfxPool p, ParticleSystem f) { pool = p; fx = f; }
    void OnParticleSystemStopped() => pool.Return(fx);
}
```

- Looping effects never stop on their own: the gameplay owner calls `Stop`, then the callback
  returns them once the last particle dies.
- Early stop (unit died, cast cancelled): `fx.Stop(true, ParticleSystemStopBehavior.StopEmitting)`
  to let it fade, `StopEmittingAndClear` to cut.
- Cap concurrent instances per effect type on mobile (e.g. 8): beyond the cap steal the oldest or
  play a `_Lite` variant.
- Effects driven by Animator/Timeline (mesh layers) need their animator reset on replay
  (`animator.Rebind(); animator.Update(0)`).

## Scale from gameplay data

```csharp
// The effect is authored at radius 1. Every system uses scalingMode Hierarchy.
pool.Play(hitPoint, Quaternion.identity, scale: skill.Radius);
```

When particle COUNT must follow the area (ground fire, rain zone), scale the emission rate by
`radius * radius` in the same call, not by hand per prefab.

## Shader warm-up

First-time hitches come from shader/PSO compilation. Unity 6: trace with `GraphicsStateCollection`
and warm at load. Older: a `ShaderVariantCollection` of the VFX materials, `WarmUp()` on the
loading screen. Then play each pooled effect once off-screen during loading if hitches remain.
