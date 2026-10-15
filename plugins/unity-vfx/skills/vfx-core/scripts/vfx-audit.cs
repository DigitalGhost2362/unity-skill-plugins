// Static audit of effect prefabs against the vfx-core rules: emitter caps, max particle size,
// Custom Data without matching vertex streams, VFX/Uber stream order, data textures left sRGB,
// ramp import, texture size, fixed seeds, linear curves, sorting ties, costly modules, culling.
// Run with script-execute, isMethodBody: false, className "VfxAudit", methodName "Main".
// Read-only unless FixUberStreams is true.
//
// Output: one line per finding, ERR (broken), WARN (budget or look), INFO (check intent).

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class VfxAudit
{
    // ---------- FILL-IN ----------
    // A prefab, or a folder: every prefab under it that holds a ParticleSystem is audited.
    const string Target = "Assets/<vfx-folder>";

    const bool Mobile = true;
    const int MaxParticlesPerEmitter = 100;
    // Renderer Max Particle Size is a fraction of the viewport; Unity's default is 0.5. Above it a
    // close-up can cover the screen. Tune per effect from a close-up vfx-measure run.
    const float MaxParticleSize = 0.5f;
    const int MaxTextureSize = 512;
    const int MaxEmitters = 7;

    // true = rewrite the vertex streams of every emitter whose material uses VFX/Uber.
    const bool FixUberStreams = false;
    // ------------------------------

    const string UberShader = "VFX/Uber";

    static readonly List<ParticleSystemVertexStream> UberStreams = new List<ParticleSystemVertexStream>
    {
        ParticleSystemVertexStream.Position,
        ParticleSystemVertexStream.Color,
        ParticleSystemVertexStream.UV,
        ParticleSystemVertexStream.Custom1XYZW,
        ParticleSystemVertexStream.StableRandomX,
    };

    static readonly string[] DataSlotWords = { "noise", "mask", "flow", "dissolve", "erosion", "distort", "normal", "data" };
    static readonly string[] RampSlotWords = { "ramp", "lut", "gradient" };

    static StringBuilder report;
    static int errors, warnings;

    public static void Main()
    {
        report = new StringBuilder();
        errors = warnings = 0;

        var paths = Target.EndsWith(".prefab")
            ? new[] { Target }
            : AssetDatabase.FindAssets("t:Prefab", new[] { Target }).Select(AssetDatabase.GUIDToAssetPath).ToArray();

        int audited = 0;
        var texturesSeen = new HashSet<Texture>();
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) continue;
            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            bool hasGraph = prefab.GetComponentsInChildren<Component>(true).Any(c => c && c.GetType().FullName == "UnityEngine.VFX.VisualEffect");
            bool hasTransparent = prefab.GetComponentsInChildren<Renderer>(true)
                .Any(r => r.sharedMaterials.Any(m => m && m.renderQueue > (int)RenderQueue.GeometryLast));
            if (systems.Length == 0 && !hasGraph && !hasTransparent) continue;

            audited++;
            report.AppendLine($"== {path} ({systems.Length} emitters)");
            if (hasGraph && Mobile) Warn(prefab.transform, "VFX Graph needs compute shaders: no GLES, most Android devices and WebGL drop it; ship a Shuriken fallback");
            if (systems.Length > MaxEmitters) Warn(prefab.transform, $"{systems.Length} emitters > {MaxEmitters}: merge layers or move shapes to meshes");

            AuditSorting(prefab);
            foreach (var ps in systems) AuditSystem(ps, prefab.transform);
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                AuditUberCustomData(r);
                foreach (var m in r.sharedMaterials)
                    if (m) AuditMaterial(m, r.transform, texturesSeen);
            }

            if (FixUberStreams && PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                foreach (var ps in systems)
                {
                    var pr = ps.GetComponent<ParticleSystemRenderer>();
                    if (pr && pr.sharedMaterial && pr.sharedMaterial.shader.name == UberShader)
                    {
                        pr.enableGPUInstancing = false;
                        var so = new SerializedObject(pr);
                        so.FindProperty("m_UseCustomVertexStreams").boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        pr.SetActiveVertexStreams(UberStreams);
                    }
                }
                PrefabUtility.SavePrefabAsset(prefab);
                report.AppendLine("  FIXED VFX/Uber vertex streams");
            }
        }

        report.Insert(0, $"VFX audit {Target}: {audited} prefabs, {errors} ERR, {warnings} WARN\n");
        Debug.Log(report.ToString());
    }

    static void AuditSystem(ParticleSystem ps, Transform root)
    {
        var t = ps.transform;
        var main = ps.main;
        var pr = ps.GetComponent<ParticleSystemRenderer>();

        // A controller (no emission, no renderer) only drives Play/Stop of its children.
        bool controller = !ps.emission.enabled && (!pr || !pr.enabled);
        if (controller)
        {
            if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                Info(t, $"controller scalingMode {main.scalingMode}: use Hierarchy so root scale reaches the children");
            return;
        }

        if (Mobile && main.maxParticles > MaxParticlesPerEmitter)
            Warn(t, $"maxParticles {main.maxParticles} > {MaxParticlesPerEmitter}");
        if (!ps.useAutoRandomSeed)
            Warn(t, "autoRandomSeed off: every spawn plays identically");
        if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
            Info(t, $"scalingMode {main.scalingMode}: root scale (radius fitting) will not resize this layer");
        if (main.loop && !ps.proceduralSimulationSupported)
            Info(t, "looping but not procedural: keeps simulating off-screen, set main.cullingMode Pause");
        if (main.loop && !main.prewarm && main.duration > 0.5f)
            Info(t, "looping without prewarm: pops in empty when enabled");

        if (ps.shape.enabled && ps.shape.shapeType == ParticleSystemShapeType.Mesh && ps.shape.mesh && !ps.shape.mesh.isReadable)
            Warn(t, $"shape mesh '{ps.shape.mesh.name}' is not Read/Write: the Shape module may emit nothing in a build");

        if (ps.lights.enabled) Warn(t, "lights module: one realtime light per particle, fake the glow in the texture");
        if (ps.collision.enabled && ps.collision.type == ParticleSystemCollisionType.World && Mobile)
            Warn(t, "world collision on mobile: use planes or kill particles by lifetime");
        if (ps.noise.enabled && ps.noise.quality == ParticleSystemNoiseQuality.High && Mobile)
            Info(t, "noise quality High: Low or Medium is rarely distinguishable on a phone");

        if (pr)
        {
            if (!pr.sharedMaterial && pr.renderMode != ParticleSystemRenderMode.None) Err(t, "no material");
            if (Mobile && pr.maxParticleSize > MaxParticleSize)
                Warn(t, $"renderer maxParticleSize {pr.maxParticleSize:0.##} > {MaxParticleSize} (fraction of screen): a close-up fills the screen");

            if (pr.renderMode == ParticleSystemRenderMode.Mesh)
            {
                var mesh = pr.mesh;
                if (mesh && mesh.HasVertexAttribute(VertexAttribute.Color) &&
                    mesh.GetVertexAttributeFormat(VertexAttribute.Color) != VertexAttributeFormat.UNorm8)
                    Warn(t, $"mesh '{mesh.name}' has float vertex colors: on a Mesh particle they render dark and tinted, rebuild it with Color32");
                if (pr.enableGPUInstancing && ps.customData.enabled)
                    Err(t, "GPU instancing on a Custom Data mesh particle: the instanced path ignores the custom vertex streams, turn instancing off");
            }

            var streams = new List<ParticleSystemVertexStream>();
            pr.GetActiveVertexStreams(streams);
            AuditCustomData(ps, pr, streams, t);

            if (pr.sharedMaterial && pr.sharedMaterial.shader.name == UberShader && !streams.SequenceEqual(UberStreams))
                Err(t, "VFX/Uber stream order wrong: want " + string.Join(", ", UberStreams) + " | have " + string.Join(", ", streams) + " (FixUberStreams=true repairs it)");
        }

        if (ps.sizeOverLifetime.enabled && IsLinear(ps.sizeOverLifetime.size))
            Info(t, "sizeOverLifetime is a straight line: use fast-then-slow");
        if (ps.colorOverLifetime.enabled && IsLinearAlpha(ps.colorOverLifetime.color))
            Info(t, "alpha over lifetime is a straight 1->0 line: fade in fast, hold, fade out");

        var burst = MaxBurst(ps);
        if (burst >= 24 && main.startSpeed.constantMax > 0 && ps.shape.enabled &&
            (ps.shape.shapeType == ParticleSystemShapeType.Circle || ps.shape.shapeType == ParticleSystemShapeType.Sphere) &&
            main.startSize.constantMax >= 0.5f)
            Warn(t, $"burst of {burst} large particles from a {ps.shape.shapeType}: if this reads as a ring or wave, use one ring mesh instead");
    }

    static void AuditCustomData(ParticleSystem ps, ParticleSystemRenderer pr, List<ParticleSystemVertexStream> streams, Transform t)
    {
        if (!ps.customData.enabled) return;
        foreach (var stream in new[] { ParticleSystemCustomData.Custom1, ParticleSystemCustomData.Custom2 })
        {
            if (ps.customData.GetMode(stream) == ParticleSystemCustomDataMode.Disabled) continue;
            string prefix = stream == ParticleSystemCustomData.Custom1 ? "Custom1" : "Custom2";
            if (!streams.Any(s => s.ToString().StartsWith(prefix)))
                Err(t, $"{prefix} is filled but not in the renderer's vertex streams: the shader reads 0");
        }
    }

    // Only particle quads and mesh particles carry the custom streams. Everything else (MeshRenderer,
    // skinned, Trail/Line renderers, the Trails module) must not read them: missing inputs are not 0.
    static void AuditUberCustomData(Renderer r)
    {
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (!m || m.shader.name != UberShader || !m.HasProperty("_CustomData")) continue;
            bool reads = m.GetFloat("_CustomData") > 0.5f;
            bool particleQuad = r is ParticleSystemRenderer && i == 0;
            if (!particleQuad && reads)
                Err(r.transform, $"{m.name} reads Custom Data streams on a {(r is ParticleSystemRenderer ? "trail" : r.GetType().Name)}: garbage offsets and intensity; set _CustomData 0 (separate material)");
            if (particleQuad && !reads && r.GetComponent<ParticleSystem>().customData.enabled)
                Err(r.transform, $"{m.name} has _CustomData 0 but the system fills Custom Data: erosion and flash are ignored");
        }
    }

    static void AuditSorting(GameObject prefab)
    {
        var transparent = prefab.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && r.sharedMaterial && r.sharedMaterial.renderQueue > (int)RenderQueue.GeometryLast)
            .ToArray();
        var groups = transparent.GroupBy(r =>
        {
            float fudge = r is ParticleSystemRenderer p ? p.sortingFudge : 0;
            return $"{r.sortingLayerID}/{r.sortingOrder}/{fudge}/{r.sharedMaterial.renderQueue}";
        });
        foreach (var g in groups)
        {
            var mats = g.Select(r => r.sharedMaterial).Distinct().Count();
            if (g.Count() > 1 && mats > 1)
                Warn(g.First().transform, $"{g.Count()} transparent layers share sorting {g.Key} (layer/order/fudge/queue) with different materials: they can flicker - give each layer its own order, brightest on top ({string.Join(", ", g.Select(r => r.name))})");
        }
    }

    static void AuditMaterial(Material m, Transform t, HashSet<Texture> seen)
    {
        foreach (var name in m.GetTexturePropertyNames())
        {
            var tex = m.GetTexture(name);
            if (!tex || !seen.Add(tex)) continue;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (!importer) continue;
            string slot = name.ToLowerInvariant();
            string label = $"{m.name}.{name} = {tex.name}";

            if (DataSlotWords.Any(slot.Contains) && importer.sRGBTexture)
                Err(t, $"{label}: data texture imported as sRGB - mid grey 0.5 reads as ~0.21, erosion and distortion go wrong; turn sRGB off");
            if (RampSlotWords.Any(slot.Contains))
            {
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    Warn(t, $"{label}: ramp/LUT is compressed - bands and hue shifts; set Compression None");
                if (importer.wrapMode != TextureWrapMode.Clamp)
                    Warn(t, $"{label}: ramp/LUT wraps - the ends bleed into each other; set Wrap Clamp");
                if (importer.mipmapEnabled)
                    Info(t, $"{label}: ramp/LUT has mipmaps - turn them off");
            }

            int size = Mathf.Max(tex.width, tex.height);
            if (Mobile)
            {
                int effective = size;
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var s = importer.GetPlatformTextureSettings(platform);
                    if (s.overridden) effective = Mathf.Min(effective, s.maxTextureSize);
                }
                if (effective > MaxTextureSize)
                    Warn(t, $"{label}: {effective}px on mobile > {MaxTextureSize}; 256 is the norm, set a platform max size");
            }
        }
    }

    static bool IsLinear(ParticleSystem.MinMaxCurve c)
    {
        if (c.mode != ParticleSystemCurveMode.Curve) return false;
        var keys = c.curve.keys;
        if (keys.Length != 2) return false;
        float slope = (keys[1].value - keys[0].value) / Mathf.Max(0.0001f, keys[1].time - keys[0].time);
        return Mathf.Abs(slope) > 0.01f &&
               Mathf.Abs(keys[0].outTangent - slope) < 0.01f && Mathf.Abs(keys[1].inTangent - slope) < 0.01f;
    }

    static bool IsLinearAlpha(ParticleSystem.MinMaxGradient g)
    {
        if (g.mode != ParticleSystemGradientMode.Gradient || g.gradient == null) return false;
        var a = g.gradient.alphaKeys;
        return a.Length == 2 && a[0].time <= 0.01f && a[1].time >= 0.99f && a[0].alpha > 0.9f && a[1].alpha < 0.1f;
    }

    static int MaxBurst(ParticleSystem ps)
    {
        int max = 0;
        var em = ps.emission;
        for (int i = 0; i < em.burstCount; i++) max = Mathf.Max(max, (int)em.GetBurst(i).count.constantMax);
        return max;
    }

    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static void Err(Transform t, string msg) { errors++; report.AppendLine($"  ERR  {PathOf(t)}: {msg}"); }
    static void Warn(Transform t, string msg) { warnings++; report.AppendLine($"  WARN {PathOf(t)}: {msg}"); }
    static void Info(Transform t, string msg) { report.AppendLine($"  INFO {PathOf(t)}: {msg}"); }
}
