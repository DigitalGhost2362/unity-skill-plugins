// Generate the simple meshes most mobile effects are built on, with UVs laid out for panning.
// Run with script-execute, isMethodBody: false, className "VfxMesh", methodName "Main".
// Edit the Jobs list, run once, use the .asset meshes in a MeshRenderer or as Mesh particles.
//
// UV convention for every shape: U runs AROUND (angle 0..1, scroll U to spin),
// V runs ACROSS (inner -> outer, bottom -> top, root -> tip; scroll V to expand or rise).
// Authored at unit size (radius 1, height 1) so gameplay scales the root by its radius.
// Vertex color alpha fades to 0 on the open edges when FadeEdges is set. Colors are Color32:
// float vertex colors on a Mesh particle's mesh come out dark and tinted.
//
// Shapes:
//   ring  flat annulus in XZ (shockwave, ground ring, telegraph edge)  Inner, Arc
//   arc   flat partial annulus in XZ (slash, swipe)                     Inner, Arc
//   cone  open cone / cylinder along +Y (tornado, beam, pillar, aura)   TopRadius
//   dome  hemisphere along +Y (shield, explosion shell)                 -
//   quad  1x1 quad in XZ (ground decal, telegraph fill)                 -

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class VfxMesh
{
    // ---------- FILL-IN ----------
    const string Folder = "Assets/<vfx-folder>/_Shared/Meshes";

    static readonly Job[] Jobs =
    {
        new Job("MESH_VFX_Ring", "ring") { Inner = 0.6f, Segments = 48 },
        new Job("MESH_VFX_Slash180", "arc") { Inner = 0.55f, Arc = 180, Segments = 32, FadeEdges = true },
        new Job("MESH_VFX_Cone", "cone") { TopRadius = 0.35f, Segments = 32, Rings = 4, FadeEdges = true },
        new Job("MESH_VFX_Cylinder", "cone") { TopRadius = 1f, Segments = 32, Rings = 4, FadeEdges = true },
        new Job("MESH_VFX_Dome", "dome") { Segments = 32, Rings = 12 },
        new Job("MESH_VFX_Quad", "quad"),
    };
    // ------------------------------

    public class Job
    {
        public string Name, Shape;
        public float Inner = 0.5f, Arc = 360, TopRadius = 0.5f;
        public int Segments = 32, Rings = 1;
        public bool FadeEdges;
        public Job(string name, string shape) { Name = name; Shape = shape; }
    }

    public static void Main()
    {
        Directory.CreateDirectory(Folder);
        var made = new List<string>();
        foreach (var job in Jobs)
        {
            var mesh = Build(job);
            mesh.name = job.Name;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{Folder}/{job.Name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); }
            else AssetDatabase.CreateAsset(mesh, path);
            made.Add($"{path} ({job.Shape}, {job.Segments} seg)");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("VFX meshes:\n" + string.Join("\n", made));
    }

    static Mesh Build(Job j)
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var col = new List<Color32>();
        var tri = new List<int>();

        switch (j.Shape)
        {
            case "ring":
            case "arc":
            {
                int rings = Mathf.Max(1, j.Rings);
                Grid(j.Segments, rings, (s, r) =>
                {
                    float a = Mathf.Deg2Rad * (j.Shape == "ring" ? 360 * s : j.Arc * (s - 0.5f));
                    float rad = Mathf.Lerp(j.Inner, 1, r);
                    float fade = j.FadeEdges && j.Shape == "arc" ? Mathf.Sin(Mathf.PI * s) : 1;
                    return (new Vector3(Mathf.Sin(a) * rad, 0, Mathf.Cos(a) * rad), new Vector2(s, r), fade);
                }, v, uv, col, tri);
                break;
            }
            case "cone":
                Grid(j.Segments, Mathf.Max(1, j.Rings), (s, r) =>
                {
                    float a = 2 * Mathf.PI * s;
                    float rad = Mathf.Lerp(1, j.TopRadius, r);
                    float fade = j.FadeEdges ? Mathf.Sin(Mathf.PI * r) : 1;
                    return (new Vector3(Mathf.Sin(a) * rad, r, Mathf.Cos(a) * rad), new Vector2(s, r), fade);
                }, v, uv, col, tri, true);
                break;
            case "dome":
                Grid(j.Segments, Mathf.Max(2, j.Rings), (s, r) =>
                {
                    float a = 2 * Mathf.PI * s, lat = 0.5f * Mathf.PI * r;
                    return (new Vector3(Mathf.Sin(a) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(a) * Mathf.Cos(lat)), new Vector2(s, r), 1);
                }, v, uv, col, tri, true);
                break;
            case "quad":
                Grid(1, 1, (s, r) => (new Vector3(s - 0.5f, 0, r - 0.5f), new Vector2(s, r), 1), v, uv, col, tri);
                break;
        }

        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        return mesh;
    }

    // (segments + 1) x (rings + 1) vertices; the seam column is duplicated so U reaches 1 cleanly.
    // Flat shapes face +Y; outward = true makes walls face away from the axis.
    static void Grid(int segments, int rings, System.Func<float, float, (Vector3 p, Vector2 uv, float alpha)> at,
        List<Vector3> v, List<Vector2> uv, List<Color32> col, List<int> tri, bool outward = false)
    {
        for (int r = 0; r <= rings; r++)
            for (int s = 0; s <= segments; s++)
            {
                var (p, t, a) = at((float)s / segments, (float)r / rings);
                v.Add(p);
                uv.Add(t);
                col.Add(new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255)));
            }
        int row = segments + 1;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int i = r * row + s;
                if (outward) tri.AddRange(new[] { i, i + 1, i + row, i + 1, i + row + 1, i + row });
                else tri.AddRange(new[] { i, i + row, i + 1, i + 1, i + row, i + row + 1 });
            }
    }
}
