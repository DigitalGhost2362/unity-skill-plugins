// Measure what an effect costs on screen: FSE (full-screen equivalents of transparent fill) over
// its lifetime, the deepest pixel stack, peak alive particles, and which layer pays for the peak.
// Run with script-execute, isMethodBody: false, className "VfxMeasure", methodName "Main".
// Edit mode only - it simulates the effect in a preview scene, so the open scene stays clean.
//
// 1 FSE = every pixel of the screen shaded once. Every transparent fragment counts, alpha 0
// included: the GPU pays for the quad, not for what it shows. Opaque renderers are skipped.
//
// FSE depends on framing. Measure twice: from the gameplay camera at gameplay distance, then
// at the worst case the game allows (camera zoomed in, effect at the near edge).
//
// Limits: only particle simulation is stepped. Mesh renderers driven by an Animator or Timeline
// are measured in the pose the prefab is saved in; the report counts them so you know.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class VfxMeasure
{
    // ---------- FILL-IN ----------
    const string PrefabPath = "Assets/<vfx-folder>/MyEffect.prefab";

    // A camera in the open scene. Empty or not found = a temporary 16:9 camera, 60 deg FOV,
    // looking down 35 deg at the effect from TempCameraDistance.
    const string CameraName = "";
    const float TempCameraDistance = 10f;

    // Where the effect is spawned. true = where the camera's centre ray meets the plane y = GroundY.
    const bool SpawnAtCameraCenter = true;
    const float GroundY = 0f;
    static readonly Vector3 SpawnAt = Vector3.zero;

    // Uniform scale on the effect root, e.g. skill radius / authored radius.
    const float EffectScale = 1f;

    const int Samples = 30;
    const int TargetHeight = 270;

    // Budgets reported against. Per effect, at gameplay framing.
    // Defaults suit a frequent combat hit on mobile; see the budget table in vfx-core.
    const float BudgetPeakFse = 0.5f;
    const int BudgetStack = 4;
    const int BudgetAlive = 100;
    // ------------------------------

    public static void Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) { Debug.LogError("prefab missing: " + PrefabPath); return; }

        Scene preview = EditorSceneManager.NewPreviewScene();
        Camera sceneCamera = string.IsNullOrEmpty(CameraName) ? null : FindCamera(CameraName);
        GameObject temp = null;
        var cleanup = new List<Object>();
        try
        {
            Vector3 spawn = SpawnAt;
            if (SpawnAtCameraCenter && sceneCamera)
            {
                var ray = sceneCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                var plane = new Plane(Vector3.up, new Vector3(0, GroundY, 0));
                spawn = plane.Raycast(ray, out float hit) ? ray.GetPoint(hit) : ray.GetPoint(TempCameraDistance);
            }

            var instance = (GameObject)Object.Instantiate(prefab, spawn, prefab.transform.rotation);
            SceneManager.MoveGameObjectToScene(instance, preview);
            instance.transform.localScale = prefab.transform.localScale * EffectScale;

            Camera cam = sceneCamera;
            if (!cam)
            {
                temp = new GameObject("VfxMeasureCamera");
                SceneManager.MoveGameObjectToScene(temp, preview);
                cam = temp.AddComponent<Camera>();
                cam.fieldOfView = 60;
                cam.aspect = 16f / 9f;
                var dir = Quaternion.Euler(35, 0, 0) * Vector3.forward;
                cam.transform.position = spawn - dir * TempCameraDistance;
                cam.transform.rotation = Quaternion.LookRotation(dir);
            }

            int height = TargetHeight;
            int width = Mathf.Max(1, Mathf.RoundToInt(height * cam.aspect));
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat)
                ? RenderTextureFormat.RFloat : RenderTextureFormat.ARGBFloat;
            var rt = new RenderTexture(width, height, 0, format);
            rt.Create();
            cleanup.Add(rt);

            var countMat = new Material(Shader.Find("Hidden/Internal-Colored"));
            countMat.SetInt("_SrcBlend", (int)BlendMode.One);
            countMat.SetInt("_DstBlend", (int)BlendMode.One);
            countMat.SetInt("_ZWrite", 0);
            countMat.SetInt("_ZTest", (int)CompareFunction.Always);
            countMat.SetInt("_Cull", (int)CullMode.Off);
            countMat.SetColor("_Color", Color.white);
            cleanup.Add(countMat);

            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var roots = systems.Where(s => !s.transform.parent || !s.transform.parent.GetComponentInParent<ParticleSystem>()).ToArray();
            var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && IsTransparent(r)).ToArray();
            int staticMeshes = renderers.Count(r => r is MeshRenderer || r is SkinnedMeshRenderer);
            int opaque = instance.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && !IsTransparent(r));

            FixSeeds(systems);
            float duration = EstimateDuration(systems);
            var report = new StringBuilder();
            report.AppendLine($"VFX measure {PrefabPath} | camera={(sceneCamera ? sceneCamera.name : "temp 35deg")} " +
                $"dist={Vector3.Distance(cam.transform.position, spawn):0.0}m rt={width}x{height} scale={EffectScale} " +
                $"T={duration:0.00}s transparentRenderers={renderers.Length} opaqueSkipped={opaque}" +
                (staticMeshes > 0 ? $" staticMeshes={staticMeshes}" : ""));

            float peakFse = 0, peakT = 0, sumFse = 0;
            int peakStack = 0, peakP95 = 0, peakAlive = 0;
            float peakAliveT = 0;
            var perEmitterMax = new Dictionary<ParticleSystem, int>();
            var line = new StringBuilder("t/FSE/alive:");

            for (int i = 0; i < Samples; i++)
            {
                float t = duration * (i + 0.5f) / Samples;
                Simulate(roots, t);
                int alive = 0;
                foreach (var s in systems)
                {
                    alive += s.particleCount;
                    perEmitterMax.TryGetValue(s, out int m);
                    perEmitterMax[s] = Mathf.Max(m, s.particleCount);
                }
                var (fse, stack, p95) = Measure(renderers, cam, rt, countMat);
                sumFse += fse;
                if (fse > peakFse) { peakFse = fse; peakT = t; }
                if (alive > peakAlive) { peakAlive = alive; peakAliveT = t; }
                peakStack = Mathf.Max(peakStack, stack);
                peakP95 = Mathf.Max(peakP95, p95);
                if (i % 3 == 0) line.Append($" {t:0.00}/{fse:0.00}/{alive}");
            }
            report.AppendLine(line.ToString());
            report.AppendLine($"peak FSE {peakFse:0.00} @{peakT:0.00}s | mean FSE {sumFse / Samples:0.00} | deepest stack {peakStack} layers (p95 {peakP95}) | peak alive {peakAlive} @{peakAliveT:0.00}s");

            Simulate(roots, peakT);
            report.AppendLine("at peak, per layer (FSE, alive/maxAlive/maxParticles, material):");
            foreach (var r in renderers)
            {
                var (fse, stack, _) = Measure(new[] { r }, cam, rt, countMat);
                var ps = r.GetComponent<ParticleSystem>();
                string counts = ps ? $"{ps.particleCount}/{perEmitterMax[ps]}/{ps.main.maxParticles}" : "mesh";
                string mat = r.sharedMaterial ? r.sharedMaterial.name : "NONE";
                report.AppendLine($"  {Path(r.transform, instance.transform)}  {fse:0.00}  stack {stack}  {counts}  {mat}");
            }

            var warn = new List<string>();
            if (peakFse > BudgetPeakFse) warn.Add($"peak FSE {peakFse:0.00} > {BudgetPeakFse}");
            if (peakP95 > BudgetStack) warn.Add($"p95 stack {peakP95} > {BudgetStack} layers where the effect covers");
            if (peakAlive > BudgetAlive) warn.Add($"peak alive {peakAlive} > {BudgetAlive}");
            foreach (var kv in perEmitterMax)
                if (kv.Value == 0 && kv.Key.emission.enabled) warn.Add($"{Path(kv.Key.transform, instance.transform)} never emits");
            if (!sceneCamera) warn.Add("temp camera: numbers are relative, re-run with the gameplay camera");
            report.AppendLine(warn.Count == 0 ? "within budget" : "warn: " + string.Join("; ", warn));

            Debug.Log(report.ToString());
        }
        finally
        {
            foreach (var o in cleanup) if (o) Object.DestroyImmediate(o);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static Camera FindCamera(string name)
    {
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.name == name) return c;
        Debug.LogWarning($"camera '{name}' not found in the open scene, using a temp camera");
        return null;
    }

    static bool IsTransparent(Renderer r)
    {
        var m = r.sharedMaterial;
        return !m || m.renderQueue > (int)RenderQueue.GeometryLast;
    }

    static float EstimateDuration(ParticleSystem[] systems)
    {
        float end = 0.2f;
        foreach (var s in systems)
        {
            if (!s.emission.enabled) continue;
            var main = s.main;
            float emitEnd = main.loop ? main.duration * 2 : main.duration;
            end = Mathf.Max(end, main.startDelay.constantMax + emitEnd + main.startLifetime.constantMax);
        }
        return Mathf.Min(end, 10f);
    }

    // Simulate(restart) reseeds every call when auto seed is on: pin the seeds on this temp copy so
    // every sample belongs to the same run.
    static void FixSeeds(ParticleSystem[] systems)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            systems[i].useAutoRandomSeed = false;
            systems[i].randomSeed = (uint)(12345 + i * 7919);
        }
    }

    static void Simulate(ParticleSystem[] roots, float t)
    {
        foreach (var s in roots) s.Simulate(t, true, true, true);
    }

    static (float fse, int stack, int p95) Measure(Renderer[] renderers, Camera cam, RenderTexture rt, Material countMat)
    {
        var cb = new CommandBuffer();
        var meshes = new List<Mesh>();
        cb.SetRenderTarget(rt);
        cb.ClearRenderTarget(true, true, Color.clear);
        cb.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
        foreach (var r in renderers)
        {
            switch (r)
            {
                case ParticleSystemRenderer pr:
                    if (pr.renderMode != ParticleSystemRenderMode.None)
                        Draw(cb, Bake(m => pr.BakeMesh(m, cam, true), meshes), Matrix4x4.identity, countMat, meshes);
                    var trails = pr.GetComponent<ParticleSystem>().trails;
                    if (trails.enabled)
                        Draw(cb, Bake(m => pr.BakeTrailsMesh(m, cam, true), meshes), Matrix4x4.identity, countMat, meshes);
                    break;
                case SkinnedMeshRenderer sr:
                    var skinned = Bake(m => sr.BakeMesh(m, true), meshes);
                    Draw(cb, skinned, Matrix4x4.TRS(sr.transform.position, sr.transform.rotation, Vector3.one), countMat, meshes);
                    break;
                case MeshRenderer mr:
                    var filter = mr.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh) Draw(cb, filter.sharedMesh, mr.transform.localToWorldMatrix, countMat, meshes);
                    break;
                case TrailRenderer tr:
                    Draw(cb, Bake(m => tr.BakeMesh(m, cam, true), meshes), Matrix4x4.identity, countMat, meshes);
                    break;
                case LineRenderer lr:
                    Draw(cb, Bake(m => lr.BakeMesh(m, cam, true), meshes), Matrix4x4.identity, countMat, meshes);
                    break;
            }
        }
        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();

        var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RFloat);
        request.WaitForCompletion();
        foreach (var m in meshes) Object.DestroyImmediate(m);
        if (request.hasError) { Debug.LogError("GPU readback failed"); return (0, 0, 0); }

        var data = request.GetData<float>();
        var histogram = new int[256];
        double sum = 0;
        int covered = 0, max = 0;
        for (int i = 0; i < data.Length; i++)
        {
            int layers = Mathf.Min(255, Mathf.RoundToInt(data[i]));
            sum += data[i];
            if (layers == 0) continue;
            covered++;
            histogram[layers]++;
            max = Mathf.Max(max, layers);
        }
        int p95 = 0;
        for (int k = 1, acc = 0; k < 256 && covered > 0; k++)
            if ((acc += histogram[k]) >= covered * 0.95f) { p95 = k; break; }
        return ((float)(sum / data.Length), max, p95);
    }

    static Mesh Bake(System.Action<Mesh> bake, List<Mesh> owned)
    {
        var m = new Mesh();
        owned.Add(m);
        bake(m);
        return m;
    }

    // The counting shader multiplies by vertex color, so every mesh is drawn with white colors.
    static void Draw(CommandBuffer cb, Mesh mesh, Matrix4x4 matrix, Material countMat, List<Mesh> owned)
    {
        if (!mesh || mesh.vertexCount == 0) return;
        if (mesh.isReadable)
        {
            if (AssetDatabase.Contains(mesh)) { mesh = Object.Instantiate(mesh); owned.Add(mesh); }
            var white = new Color32[mesh.vertexCount];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = white;
        }
        for (int sub = 0; sub < mesh.subMeshCount; sub++) cb.DrawMesh(mesh, matrix, countMat, sub, 0);
    }

    static string Path(Transform t, Transform root)
    {
        if (t == root) return t.name;
        return Path(t.parent, root) + "/" + t.name;
    }
}
