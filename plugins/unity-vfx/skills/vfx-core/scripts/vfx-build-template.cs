// Template: build an effect prefab through the Editor - mesh backbone + particle garnish on the
// VFX/Uber material, Custom Data and vertex streams already wired to the shader contract.
// Run with script-execute, isMethodBody: false, className "VfxBuild", methodName "Main".
//
// Adapt: the FILL-IN block and Build(). Keep: the helpers (Root, MeshLayer, ParticleLayer, Uber,
// UberStreams, Ease). The example is a 0.4 s mobile hit: flash, ring mesh, sparks.
// Re-running overwrites the prefab; humans may have tuned it since - vfx-read or diff it first.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class VfxBuild
{
    // ---------- FILL-IN ----------
    const string PrefabPath = "Assets/<vfx-folder>/Combat/Hit/VFX_Combat_Hit.prefab";
    const string MaterialFolder = "Assets/<vfx-folder>/Combat/Hit";
    const string Textures = "Assets/<vfx-folder>/_Shared/Textures";
    const string Meshes = "Assets/<vfx-folder>/_Shared/Meshes";
    // ------------------------------

    public static void Main()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var root = Root("VFX_Combat_Hit", 0.4f);
        Build(root);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("built " + PrefabPath);
    }

    static void Build(GameObject root)
    {
        // Flash: one big glow, gone by 0.1 s, white to warm.
        var flash = ParticleLayer(root, "Flash", Uber("M_VFX_Hit_Flash", "T_VFX_Glow", m => m.SetColor("_Color", new Color(1f, 0.85f, 0.6f))), 3);
        var fm = flash.main; fm.startLifetime = 0.1f; fm.startSpeed = 0; fm.startSize = 2.2f;
        Burst(flash, 1);
        var fs = flash.sizeOverLifetime; fs.enabled = true; fs.size = new ParticleSystem.MinMaxCurve(1, Ease(0.6f, 1f, 0.3f));
        Fade(flash, 0.0f);

        // Ring: one mesh particle, scales out fast then slow, erodes away. 1 layer of fill.
        // A ring MESH takes a band texture (soft across V); a ring-shaped texture belongs on a quad.
        var ring = ParticleLayer(root, "Ring", Uber("M_VFX_Hit_Ring", "T_VFX_Trail", m =>
        {
            m.SetColor("_Color", new Color(1f, 0.6f, 0.25f));
            m.SetFloat("_Intensity", 2.5f);
            m.SetFloat("_NoiseOn", 1); m.EnableKeyword("_NOISE_ON");
            m.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/T_VFX_Noise.png"));
            m.SetFloat("_EdgeWidth", 0.08f); m.SetColor("_EdgeColor", new Color(1f, 0.9f, 0.6f) * 2);
        }), 1);
        var rr = ring.GetComponent<ParticleSystemRenderer>();
        rr.renderMode = ParticleSystemRenderMode.Mesh;
        rr.mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Meshes}/MESH_VFX_Ring.asset");
        rr.alignment = ParticleSystemRenderSpace.World;
        var rm = ring.main; rm.startLifetime = 0.35f; rm.startSpeed = 0; rm.startSize = 1.6f;
        Burst(ring, 1);
        var rs = ring.sizeOverLifetime; rs.enabled = true; rs.size = new ParticleSystem.MinMaxCurve(1, Ease(0.2f, 1f, 0.2f));
        CustomErosion(ring, 0.25f);

        // Sparks: stretched streaks, fast, shrink, gravity.
        var sparks = ParticleLayer(root, "Sparks", Uber("M_VFX_Hit_Spark", "T_VFX_Spark", m => m.SetColor("_Color", new Color(1f, 0.75f, 0.4f) * 1.5f)), 2);
        var sm = sparks.main; sm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(6, 11); sm.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        sm.gravityModifier = 0.5f;
        Burst(sparks, 12, 18);
        var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f;
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.08f; sr.lengthScale = 1;
        var ss = sparks.sizeOverLifetime; ss.enabled = true; ss.size = new ParticleSystem.MinMaxCurve(1, Ease(1f, 0f, 0.6f));
    }

    // ---------- helpers ----------

    static GameObject Root(string name, float duration)
    {
        var root = new GameObject(name);
        var ps = root.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.duration = duration; main.playOnAwake = false;
        main.maxParticles = 1; main.startLifetime = duration; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        root.GetComponent<ParticleSystemRenderer>().enabled = false;
        return root;
    }

    // A child emitter: burst-only, Hierarchy scaling, auto seed, sorting order per layer.
    static ParticleSystem ParticleLayer(GameObject root, string name, Material material, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.duration = root.GetComponent<ParticleSystem>().main.duration;
        main.playOnAwake = false; main.maxParticles = 50;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ps.useAutoRandomSeed = true;
        var em = ps.emission; em.rateOverTime = 0;
        var shape = ps.shape; shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.sortingOrder = sortingOrder;
        r.maxParticleSize = 0.5f;
        UberStreams(r);
        return ps;
    }

    static void Burst(ParticleSystem ps, int min, int max = -1)
    {
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0, (short)min, (short)(max < 0 ? min : max)) });
    }

    // Alpha: in by `fadeIn` of life (0 = born visible), drops fast, tails out to 0.
    static void Fade(ParticleSystem ps, float fadeIn)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            fadeIn > 0
                ? new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, fadeIn), new GradientAlphaKey(0, 1) }
                : new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0, 1) });
        col.color = g;
    }

    // Custom1.x erosion 0 -> 1, holding until `hold` of life then eating fast; Custom1.y birth burst.
    static void CustomErosion(ParticleSystem ps, float hold)
    {
        var cd = ps.customData; cd.enabled = true;
        cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
        cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1,
            new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(hold, 0.05f), new Keyframe(1, 1, 2.5f, 0))));
        cd.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(1.5f,
            new AnimationCurve(new Keyframe(0, 1, 0, -6), new Keyframe(0.2f, 0, 0, 0))));
        cd.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(0, 1));
        cd.SetVector(ParticleSystemCustomData.Custom1, 3, new ParticleSystem.MinMaxCurve(0, 1));
    }

    // Fast-then-slow curve from `from` to `to`; `knee` = share of life where most of the change is done.
    static AnimationCurve Ease(float from, float to, float knee)
    {
        float mid = Mathf.Lerp(from, to, 0.85f);
        return new AnimationCurve(new Keyframe(0, from, 0, (mid - from) / knee * 1.6f), new Keyframe(knee, mid), new Keyframe(1, to, 0, 0));
    }

    static void UberStreams(ParticleSystemRenderer r)
    {
        r.enableGPUInstancing = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.StableRandomX,
        });
    }

    // One material per look, saved next to the effect; reused if it already exists.
    static Material Uber(string name, string texture, System.Action<Material> setup)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) return existing;
        var m = new Material(Shader.Find("VFX/Uber"));
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{texture}.png"));
        setup?.Invoke(m);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
