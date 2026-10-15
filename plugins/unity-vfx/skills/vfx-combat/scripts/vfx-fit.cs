// Does the effect cover the damage area? Renders the effect top-down at one moment, measures how
// far its visible edge reaches along 72 rays, compares with the gameplay shape, and writes a PNG
// with the gameplay outline drawn over it in red.
// Run with script-execute, isMethodBody: false, className "VfxFit", methodName "Main".
// Edit mode only; preview scene, the open scene stays clean. Open the PNG with the Read tool.
//
// Shapes are in the effect's local frame, +Z forward, Y up:
//   Circle  radius R around the origin
//   Cone    radius R, total angle A, opening along +Z
//   Box     width W (X) x length L (Z), from the origin forward (a line skill); Centered = around it

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VfxFit
{
    // ---------- FILL-IN ----------
    const string PrefabPath = "Assets/<vfx-folder>/MyEffect.prefab";
    const string OutPng = "Temp/vfx-fit.png";

    // The gameplay values, copied from the config the damage code reads.
    static readonly string Shape = "Circle";   // Circle | Cone | Box
    const float Radius = 3f;                // Circle, Cone
    const float Angle = 90f;                // Cone, degrees
    const float Width = 2f, Length = 6f;    // Box
    static readonly bool Centered = false;     // Box around the origin instead of forward from it

    // Root scale applied before measuring (authored at radius 1 -> EffectScale = Radius).
    const float EffectScale = 1f;
    // Moment to judge, seconds. Negative = the moment the effect is widest.
    static readonly float AtTime = -1f;
    // Only these layers count as the primary shape (child names). Empty = every layer.
    static readonly string[] PrimaryLayers = { };
    // Luminance above which a pixel counts as visible.
    const float Threshold = 0.08f;
    const int Size = 512;
    // ------------------------------

    public static void Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) { Debug.LogError("prefab missing: " + PrefabPath); return; }

        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = null;
        Camera cam = null;
        Texture2D image = null;
        try
        {
            var fx = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(fx, preview);
            fx.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            fx.transform.localScale = prefab.transform.localScale * EffectScale;

            if (PrimaryLayers.Length > 0)
                foreach (var r in fx.GetComponentsInChildren<Renderer>(true))
                    if (!PrimaryLayers.Contains(r.name)) r.enabled = false;

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = (uint)(12345 + i * 7919);
            }
            var roots = systems.Where(s => !s.transform.parent || !s.transform.parent.GetComponentInParent<ParticleSystem>()).ToArray();

            float extent = Shape == "Box" ? Mathf.Max(Width, Length) : Radius;
            var camGo = new GameObject("VfxFitCamera");
            SceneManager.MoveGameObjectToScene(camGo, preview);
            cam = camGo.AddComponent<Camera>();
            cam.scene = preview;
            cam.orthographic = true;
            cam.orthographicSize = extent * 1.5f;
            cam.aspect = 1;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.SetPositionAndRotation(new Vector3(0, extent * 4, Shape == "Box" && !Centered ? Length * 0.5f : 0),
                Quaternion.Euler(90, 0, 0));
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = extent * 10;
            rt = new RenderTexture(Size, Size, 24);
            cam.targetTexture = rt;
            image = new Texture2D(Size, Size, TextureFormat.RGB24, false);

            float length = 0.2f;
            foreach (var s in systems)
            {
                if (!s.emission.enabled) continue;
                var m = s.main;
                length = Mathf.Max(length, m.startDelay.constantMax + m.duration + m.startLifetime.constantMax);
            }
            var candidates = AtTime >= 0 ? new[] { AtTime } : Enumerable.Range(1, 12).Select(i => length * i / 13f).ToArray();

            float bestT = candidates[0];
            float[] best = null;
            float bestMedian = -1;
            foreach (float t in candidates)
            {
                Render(roots, t, cam, rt, image);
                var reach = Reach(image, cam);
                float median = Median(reach.Where(r => r >= 0));
                if (median > bestMedian) { bestMedian = median; best = reach; bestT = t; }
            }
            Render(roots, bestT, cam, rt, image);

            var report = Compare(best, cam);
            DrawOutline(image, cam);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutPng)));
            File.WriteAllBytes(OutPng, image.EncodeToPNG());
            Debug.Log($"VFX fit {PrefabPath} @{bestT:0.00}s shape={Shape} scale={EffectScale} -> {OutPng}\n{report}");
        }
        finally
        {
            if (cam) cam.targetTexture = null;
            if (rt) Object.DestroyImmediate(rt);
            if (image) Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void Render(ParticleSystem[] roots, float t, Camera cam, RenderTexture rt, Texture2D image)
    {
        foreach (var s in roots) s.Simulate(t, true, true, true);
        cam.Render();
        RenderTexture.active = rt;
        image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        RenderTexture.active = null;
    }

    static Vector2 ToPixel(Camera cam, Vector3 world)
    {
        var v = cam.WorldToViewportPoint(world);
        return new Vector2(v.x * Size, v.y * Size);
    }

    // Farthest visible pixel along each of 72 rays from the origin, in metres (-1 = nothing visible).
    static float[] Reach(Texture2D image, Camera cam)
    {
        var px = image.GetPixels();
        var o = ToPixel(cam, Vector3.zero);
        float metresPerPixel = cam.orthographicSize * 2 / Size;
        var reach = new float[72];
        for (int k = 0; k < 72; k++)
        {
            float a = k * 5 * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            reach[k] = -1;
            for (float d = 0; d < Size; d += 0.5f)
            {
                var p = o + dir * d;
                int x = (int)p.x, y = (int)p.y;
                if (x < 0 || y < 0 || x >= Size || y >= Size) break;
                var c = px[y * Size + x];
                if (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b > Threshold) reach[k] = d * metresPerPixel;
            }
        }
        return reach;
    }

    // Gameplay boundary distance along the ray at angle a (degrees, 0 = +Z, clockwise), or -1 outside the shape.
    static float Boundary(float a)
    {
        float rad = a * Mathf.Deg2Rad;
        switch (Shape)
        {
            case "Circle": return Radius;
            case "Cone":
                float off = Mathf.DeltaAngle(0, a);
                return Mathf.Abs(off) <= Angle * 0.5f ? Radius : -1;
            case "Box":
                var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                float zMin = Centered ? -Length * 0.5f : 0, zMax = Centered ? Length * 0.5f : Length;
                float tx = Mathf.Abs(dir.x) > 1e-4f ? Width * 0.5f / Mathf.Abs(dir.x) : float.MaxValue;
                float tz = dir.y > 1e-4f ? zMax / dir.y : dir.y < -1e-4f ? zMin / dir.y : float.MaxValue;
                float t = Mathf.Min(tx, tz);
                return t > 0 && t < float.MaxValue ? t : -1;
        }
        return -1;
    }

    static string Compare(float[] reach, Camera cam)
    {
        var ratios = new List<float>();
        int outside = 0, missing = 0;
        for (int k = 0; k < 72; k++)
        {
            float b = Boundary(k * 5);
            if (b <= 0) { if (reach[k] > 0.05f) outside++; continue; }
            if (reach[k] < 0) { missing++; continue; }
            ratios.Add(reach[k] / b);
        }
        if (ratios.Count == 0) return "nothing visible inside the gameplay shape";
        ratios.Sort();
        float min = ratios[0], max = ratios[ratios.Count - 1], med = Median(ratios);
        string verdict = med >= 0.95f && med <= 1.1f && min >= 0.85f ? "PASS"
            : med < 0.95f || min < 0.85f ? "UNDER: the damage reaches where nothing is drawn"
            : "OVER: the visual promises more area than the damage covers";
        return $"visual/gameplay reach: median {med:0.00}, min {min:0.00}, max {max:0.00} over {ratios.Count} rays" +
               (missing > 0 ? $", {missing} rays with nothing drawn" : "") +
               (outside > 0 ? $", {outside} rays drawn outside the shape" : "") +
               $"\n{verdict} (pass: median 0.95-1.1, no ray under 0.85; glow may overshoot, the primary edge may not)";
    }

    static void DrawOutline(Texture2D image, Camera cam)
    {
        var red = Color.red;
        for (float a = 0; a < 360; a += 0.25f)
        {
            float b = Boundary(a);
            if (b <= 0) continue;
            float rad = a * Mathf.Deg2Rad;
            Plot(image, ToPixel(cam, new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * b), red);
        }
        if (Shape == "Cone")
            foreach (float side in new[] { -Angle * 0.5f, Angle * 0.5f })
                for (float d = 0; d <= Radius; d += Radius / 400)
                    Plot(image, ToPixel(cam, Quaternion.Euler(0, side, 0) * Vector3.forward * d), red);
    }

    static void Plot(Texture2D image, Vector2 p, Color c)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = (int)p.x + dx, y = (int)p.y + dy;
                if (x >= 0 && y >= 0 && x < Size && y < Size) image.SetPixel(x, y, c);
            }
    }

    static float Median(IEnumerable<float> values)
    {
        var v = values.OrderBy(x => x).ToArray();
        return v.Length == 0 ? -1 : v[v.Length / 2];
    }
}
