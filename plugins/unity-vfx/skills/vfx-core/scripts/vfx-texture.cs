// Generate simple VFX textures by code and import them with the right settings.
// Run with script-execute, isMethodBody: false, className "VfxTexture", methodName "Main".
// Edit the Jobs list, run once, reference the PNGs from materials.
//
// Shape textures: RGB white, shape in alpha (alpha is always linear, so sRGB does not matter).
// Data textures (noise): value in RGB and alpha, sRGB OFF, so 0.5 stays 0.5.
// Ramps: colors, sRGB ON, uncompressed, clamp, no mipmaps, 256x4.
//
// Shapes: glow, circle, ring, spark, star, trail, noise, ramp.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class VfxTexture
{
    // ---------- FILL-IN ----------
    const string Folder = "Assets/<vfx-folder>/_Shared/Textures";

    static readonly Job[] Jobs =
    {
        new Job("T_VFX_Glow", "glow", 128) { Falloff = 2f },
        new Job("T_VFX_Ring", "ring", 256) { Radius = 0.8f, Width = 0.12f, Soft = 0.5f },
        new Job("T_VFX_Spark", "spark", 128) { Width = 0.12f },
        new Job("T_VFX_Star4", "star", 128) { Points = 4, Width = 0.08f },
        new Job("T_VFX_Trail", "trail", 128) { Soft = 0.6f },
        new Job("T_VFX_Noise", "noise", 256) { Period = 4, Octaves = 4, Seed = 1 },
        new Job("T_VFX_Ramp_Fire", "ramp", 256) { Colors = new[] { "#1A0500@0", "#C22A00@0.35", "#FF8A00@0.7", "#FFF2C0@1" } },
    };
    // ------------------------------

    public class Job
    {
        public string Name, Shape;
        public int Size;
        public float Falloff = 2, Radius = 0.8f, Width = 0.1f, Soft = 0.5f;
        public int Points = 4, Period = 4, Octaves = 4, Seed = 1;
        public string[] Colors;
        public Job(string name, string shape, int size) { Name = name; Shape = shape; Size = size; }
    }

    public static void Main()
    {
        Directory.CreateDirectory(Folder);
        var made = new List<string>();
        foreach (var job in Jobs)
        {
            bool ramp = job.Shape == "ramp", data = job.Shape == "noise";
            int w = job.Size, h = ramp ? 4 : job.Size;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = Pixel(job, (x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            string path = $"{Folder}/{job.Name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var im = (TextureImporter)AssetImporter.GetAtPath(path);
            im.textureType = TextureImporterType.Default;
            im.sRGBTexture = ramp;
            im.alphaSource = TextureImporterAlphaSource.FromInput;
            im.alphaIsTransparency = !data && !ramp;
            im.mipmapEnabled = !ramp;
            im.wrapMode = ramp || job.Shape != "noise" && job.Shape != "trail" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (job.Shape == "trail") { im.wrapModeU = TextureWrapMode.Repeat; im.wrapModeV = TextureWrapMode.Clamp; }
            im.maxTextureSize = Mathf.Max(32, job.Size);
            im.textureCompression = ramp ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            im.SaveAndReimport();
            made.Add($"{path} ({job.Shape} {w}x{h}, sRGB {(ramp ? "on" : "off")})");
        }
        Debug.Log("VFX textures:\n" + string.Join("\n", made));
    }

    static Color Pixel(Job j, float u, float v)
    {
        float dx = u * 2 - 1, dy = v * 2 - 1;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        switch (j.Shape)
        {
            case "glow":
                return Alpha(Mathf.Pow(Mathf.Clamp01(1 - r), j.Falloff));
            case "circle":
                return Alpha(1 - Mathf.SmoothStep(1 - j.Soft * 0.5f, 1, r));
            case "ring":
            {
                float d = Mathf.Abs(r - j.Radius) / Mathf.Max(0.001f, j.Width);
                return Alpha(Mathf.Pow(Mathf.Clamp01(1 - d), 1 + j.Soft * 2));
            }
            case "spark":
            {
                float along = Mathf.Clamp01(1 - Mathf.Abs(dx));
                float across = Mathf.Clamp01(1 - Mathf.Abs(dy) / Mathf.Max(0.001f, j.Width));
                return Alpha(along * along * across * across);
            }
            case "star":
            {
                float a = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * j.Points * 0.5f)), 1 / Mathf.Max(0.001f, j.Width * 4));
                float core = Mathf.Pow(Mathf.Clamp01(1 - r), 3);
                return Alpha(Mathf.Clamp01(ray * Mathf.Clamp01(1 - r) + core));
            }
            case "trail":
                return Alpha(Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(dy)), 1 + j.Soft * 3));
            case "noise":
            {
                float n = Fbm(u, v, j.Period, j.Octaves, j.Seed);
                return new Color(n, n, n, n);
            }
            case "ramp":
                return Ramp(j.Colors, u);
        }
        return Color.magenta;
    }

    static Color Alpha(float a) => new Color(1, 1, 1, Mathf.Clamp01(a));

    static Color Ramp(string[] keys, float t)
    {
        var stops = new List<(float t, Color c)>();
        foreach (var k in keys)
        {
            var parts = k.Split('@');
            ColorUtility.TryParseHtmlString(parts[0], out var c);
            stops.Add((float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), c));
        }
        if (t <= stops[0].t) return stops[0].c;
        for (int i = 1; i < stops.Count; i++)
            if (t <= stops[i].t)
                return Color.Lerp(stops[i - 1].c, stops[i].c, Mathf.InverseLerp(stops[i - 1].t, stops[i].t, t));
        return stops[stops.Count - 1].c;
    }

    // Tileable value-noise fBm: the lattice wraps every `period` cells, so the texture repeats seamlessly.
    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            int p = period << o;
            sum += amp * ValueNoise(u * p, v * p, p, seed + o * 101);
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
        float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int period, int seed)
    {
        x = ((x % period) + period) % period;
        y = ((y % period) + period) % period;
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
