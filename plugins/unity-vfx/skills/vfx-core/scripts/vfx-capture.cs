// Contact sheet of an effect over time: N cells left-to-right, top-to-bottom, one PNG.
// Run with script-execute, isMethodBody: false, className "VfxCapture", methodName "Main".
// Edit mode only. The effect is simulated in a preview scene, so the open scene stays clean.
// Then open the PNG with the Read tool (one image per iteration, not per patch).
//
// Limits: no post-processing (judge Bloom in the game view), and shader time (_Time) runs on the
// editor clock, so texture panning is not in step with the simulated time between cells.

using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VfxCapture
{
    // ---------- FILL-IN ----------
    const string PrefabPath = "Assets/<vfx-folder>/MyEffect.prefab";
    const string OutPng = "Temp/vfx-capture.png";

    // A camera in the open scene whose position, rotation and FOV are copied. Empty = 35 deg 3/4 view
    // at Distance, or framed on the effect's bounds when Distance is 0.
    const string CameraName = "";
    const float Distance = 0f;

    const int Cells = 8;
    const int Columns = 4;
    const int CellSize = 192;
    // Dark shows additive layers, mid grey shows alpha layers; check both once.
    static readonly Color Background = new Color(0.12f, 0.12f, 0.14f);
    // Empty = evenly over the estimated length. Otherwise explicit times in seconds.
    static readonly float[] Times = { };
    // ------------------------------

    public static void Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) { Debug.LogError("prefab missing: " + PrefabPath); return; }

        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = null;
        Texture2D sheet = null;
        Camera cam = null;
        try
        {
            var instance = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, preview);

            var camGo = new GameObject("VfxCaptureCamera");
            SceneManager.MoveGameObjectToScene(camGo, preview);
            cam = camGo.AddComponent<Camera>();
            cam.scene = preview;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.aspect = 1;

            Camera source = string.IsNullOrEmpty(CameraName) ? null
                : Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == CameraName);
            if (source)
            {
                cam.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                cam.fieldOfView = source.fieldOfView;
                var ray = source.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                instance.transform.position = new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float hit) ? ray.GetPoint(hit) : ray.GetPoint(Distance);
            }

            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var roots = systems.Where(s => !s.transform.parent || !s.transform.parent.GetComponentInParent<ParticleSystem>()).ToArray();
            for (int i = 0; i < systems.Length; i++)
            {
                // one run across all cells: Simulate(restart) would reseed every cell otherwise
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = (uint)(12345 + i * 7919);
            }
            float length = 0.2f;
            foreach (var s in systems)
            {
                if (!s.emission.enabled) continue;
                var main = s.main;
                length = Mathf.Max(length, main.startDelay.constantMax + (main.loop ? main.duration * 2 : main.duration) + main.startLifetime.constantMax);
            }
            length = Mathf.Min(length, 10f);
            var times = Times.Length > 0 ? Times : Enumerable.Range(0, Cells).Select(i => length * (i + 0.5f) / Cells).ToArray();

            if (!source)
            {
                var center = instance.transform.position;
                float radius = 0.5f;
                if (Distance <= 0)
                {
                    // frame the widest moment among a few probes, from the particles themselves
                    var buffer = new ParticleSystem.Particle[256];
                    foreach (float probe in new[] { 0.15f, 0.35f, 0.6f })
                    {
                        foreach (var s in roots) s.Simulate(length * probe, true, true, true);
                        foreach (var s in systems)
                        {
                            int n = s.GetParticles(buffer);
                            bool local = s.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                            float scale = s.transform.lossyScale.x;
                            for (int k = 0; k < n; k++)
                            {
                                var p = local ? s.transform.TransformPoint(buffer[k].position) : buffer[k].position;
                                radius = Mathf.Max(radius, Vector3.Distance(center, p) + buffer[k].GetCurrentSize(s) * scale * 0.5f);
                            }
                        }
                        foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
                            if (r.enabled) radius = Mathf.Max(radius, Vector3.Distance(center, r.bounds.center) + r.bounds.extents.magnitude);
                    }
                }
                cam.fieldOfView = 50;
                float dist = Distance > 0 ? Distance : radius / Mathf.Tan(25 * Mathf.Deg2Rad) * 1.1f;
                var dir = Quaternion.Euler(35, 0, 0) * Vector3.forward;
                cam.transform.SetPositionAndRotation(center - dir * dist, Quaternion.LookRotation(dir));
            }

            int rows = Mathf.CeilToInt(times.Length / (float)Columns);
            rt = new RenderTexture(CellSize, CellSize, 24);
            cam.targetTexture = rt;
            sheet = new Texture2D(CellSize * Columns, CellSize * rows, TextureFormat.RGB24, false);
            sheet.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 255), sheet.width * sheet.height).ToArray());

            var log = new StringBuilder($"VFX capture {PrefabPath} -> {OutPng} | cells (row, col): t / alive\n");
            for (int i = 0; i < times.Length; i++)
            {
                foreach (var s in roots) s.Simulate(times[i], true, true, true);
                cam.Render();
                RenderTexture.active = rt;
                int col = i % Columns, row = i / Columns;
                sheet.ReadPixels(new Rect(0, 0, CellSize, CellSize), col * CellSize, (rows - 1 - row) * CellSize);
                log.Append($"({row},{col}) {times[i]:0.00}s / {systems.Sum(s => s.particleCount)}   ");
                if (col == Columns - 1) log.AppendLine();
            }
            RenderTexture.active = null;
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutPng)));
            File.WriteAllBytes(OutPng, sheet.EncodeToPNG());
            Debug.Log(log.ToString());
        }
        finally
        {
            if (cam) cam.targetTexture = null;
            if (rt) Object.DestroyImmediate(rt);
            if (sheet) Object.DestroyImmediate(sheet);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
