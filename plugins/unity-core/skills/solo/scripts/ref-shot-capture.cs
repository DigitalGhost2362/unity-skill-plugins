// Game-camera screenshot from a fixed pose, for judging lighting and atmosphere against a reference
// image. Borrows the Main Camera for one synchronous render (post-processing and the Volume stack
// included), writes a PNG, destroys the RenderTexture and Texture2D, and puts the camera back.
// Run with script-execute, isMethodBody: false, className "RefShotCapture", and either methodName
// "Main" after editing FILL-IN, or methodName "Run" with string parameters shotsJson, shot, outPng,
// toggle, record. Works in edit and play mode. Then open the PNG with the Read tool, or ref-compare.py.
//
// shots.json (owned by this script, keep it next to the reference images):
//   { "shots": [ { "name": "home_wide", "pos": [x,y,z], "rot": [pitch,yaw,roll], "fov": 50,
//                  "width": 1600, "height": 720 } ] }
// fov 0 keeps the camera's own. Record = "sceneview" or "main" first writes that camera's current
// pose into the shot (added or replaced), then captures it - frame the view by hand once, record it,
// and every later capture of that shot is pixel-comparable.
//
// toggle: optional scene path or name. When set, two PNGs are written, <out>_A.png as the scene is
// and <out>_B.png with that object's active state flipped (restored afterwards): an A/B for a change
// that "has no effect".
//
// In play mode the camera is moved for one render inside this call only; game scripts do not run in
// between, but the game's next frame recomputes its Volume stack from the restored pose.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class RefShotCapture
{
    // ---------- FILL-IN ----------
    const string ShotsJson = "Docs/<ref-folder>/shots.json";
    const string Shot = "home_wide";
    const string OutPng = "Temp/ref-shot/home_wide.png";
    const string Record = "";        // "", "sceneview" or "main"
    const string Toggle = "";        // object to flip for an A/B pair
    // ------------------------------

    // JsonUtility cannot see types compiled by script-execute (it writes "{}"), so shots.json is read
    // and written by hand: one flat object per shot.
    class ShotDef { public string name; public float[] pos; public float[] rot; public float fov; public int width = 1600; public int height = 720; }

    public static string Main() => Run(ShotsJson, Shot, OutPng, Toggle, Record);

    public static string Run(string shotsJson, string shot, string outPng, string toggle = "", string record = "")
    {
        Camera cam = Camera.main;
        if (!cam) return Log("ref-shot: no camera tagged MainCamera is active");

        var shots = File.Exists(shotsJson) ? ReadShots(File.ReadAllText(shotsJson)) : new List<ShotDef>();
        var def = shots.FirstOrDefault(s => s.name == shot);

        if (!string.IsNullOrEmpty(record))
        {
            Transform src; float fov;
            if (record == "sceneview")
            {
                var sv = SceneView.lastActiveSceneView;
                if (!sv) return Log("ref-shot: no Scene view to record from");
                src = sv.camera.transform; fov = sv.cameraSettings.fieldOfView;
            }
            else { src = cam.transform; fov = cam.fieldOfView; }
            if (def == null) { def = new ShotDef { name = shot }; shots.Add(def); }
            def.pos = new[] { src.position.x, src.position.y, src.position.z };
            var e = src.eulerAngles;
            def.rot = new[] { e.x, e.y, e.z };
            def.fov = fov;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(shotsJson)));
            File.WriteAllText(shotsJson, WriteShots(shots));
        }
        if (def == null) return Log($"ref-shot: shot '{shot}' not in {shotsJson} - record it first");

        GameObject flip = null;
        if (!string.IsNullOrEmpty(toggle))
        {
            flip = FindInScenes(toggle);
            if (!flip) return Log("ref-shot: toggle object not found: " + toggle);
        }

        string result;
        if (!flip)
            result = Capture(cam, def, outPng);
        else
        {
            string stem = Path.Combine(Path.GetDirectoryName(outPng) ?? "", Path.GetFileNameWithoutExtension(outPng));
            bool was = flip.activeSelf;
            try
            {
                result = Capture(cam, def, stem + "_A.png");
                flip.SetActive(!was);
                result += " | B (" + flip.name + (was ? " off" : " on") + "): " + Capture(cam, def, stem + "_B.png");
            }
            finally { flip.SetActive(was); }
        }
        return Log("ref-shot: " + result);
    }

    static string Capture(Camera cam, ShotDef def, string outPng)
    {
        var t = cam.transform;
        Vector3 pos = t.position;
        Quaternion rot = t.rotation;
        float fov = cam.fieldOfView;
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        bool prevAsync = ShaderUtil.allowAsyncCompilation;
        RenderTexture rt = null;
        Texture2D tex = null;
        try
        {
            t.SetPositionAndRotation(new Vector3(def.pos[0], def.pos[1], def.pos[2]), Quaternion.Euler(def.rot[0], def.rot[1], def.rot[2]));
            if (def.fov > 0) cam.fieldOfView = def.fov;
            bool volumes = UpdateVolumeStack(cam);

            // a shader still compiling async renders as nothing; force sync for this one frame
            bool compiling = ShaderUtil.anythingCompiling;
            ShaderUtil.allowAsyncCompilation = false;

            rt = new RenderTexture(def.width, def.height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex = new Texture2D(def.width, def.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, def.width, def.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPng)));
            File.WriteAllBytes(outPng, tex.EncodeToPNG());
            return $"{outPng} {def.width}x{def.height} shot '{def.name}' fov {cam.fieldOfView:0.#}, volume stack {(volumes ? "updated" : "not URP / not updated")}"
                   + (compiling ? ", shaders were compiling - recapture if an object is missing" : "");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = prevAsync;
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            t.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            UpdateVolumeStack(cam);
            if (rt) Object.DestroyImmediate(rt);
            if (tex) Object.DestroyImmediate(tex);
        }
    }

    // URP with Volume Update Mode = Via Scripting only re-blends volumes when asked. Reflection so the
    // script still compiles in a project without URP.
    static bool UpdateVolumeStack(Camera cam)
    {
        var ext = Type.GetType("UnityEngine.Rendering.Universal.CameraExtensions, Unity.RenderPipelines.Universal.Runtime");
        var method = ext?.GetMethod("UpdateVolumeStack", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Camera) }, null);
        if (method == null) return false;
        method.Invoke(null, new object[] { cam });
        return true;
    }

    static List<ShotDef> ReadShots(string json)
    {
        var shots = new List<ShotDef>();
        foreach (Match m in Regex.Matches(json, @"\{[^{}]*""name""[^{}]*\}"))
        {
            string o = m.Value;
            var def = new ShotDef { name = Regex.Match(o, @"""name""\s*:\s*""([^""]*)""").Groups[1].Value, pos = Floats(o, "pos"), rot = Floats(o, "rot") };
            if (def.pos.Length != 3 || def.rot.Length != 3) continue;
            def.fov = Floats(o, "fov").FirstOrDefault();
            float[] w = Floats(o, "width"), h = Floats(o, "height");
            if (w.Length > 0) def.width = (int)w[0];
            if (h.Length > 0) def.height = (int)h[0];
            shots.Add(def);
        }
        return shots;
    }

    static float[] Floats(string obj, string key)
    {
        var m = Regex.Match(obj, $@"""{key}""\s*:\s*\[?([-0-9.eE,\s]+)\]?");
        return m.Success ? m.Groups[1].Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray() : new float[0];
    }

    static string WriteShots(List<ShotDef> shots)
    {
        string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        string V(float[] v) => "[" + string.Join(", ", v.Select(F)) + "]";
        return "{\n  \"shots\": [\n" + string.Join(",\n", shots.Select(d =>
            $"    {{ \"name\": \"{d.name}\", \"pos\": {V(d.pos)}, \"rot\": {V(d.rot)}, \"fov\": {F(d.fov)}, \"width\": {d.width}, \"height\": {d.height} }}")) + "\n  ]\n}\n";
    }

    static GameObject FindInScenes(string pathOrName)
    {
        var all = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
            .SelectMany(s => s.GetRootGameObjects())
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        return (all.FirstOrDefault(t => PathOf(t) == pathOrName) ?? all.FirstOrDefault(t => t.name == pathOrName))?.gameObject;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    static string Log(string msg)
    {
        Debug.Log(msg);
        return msg;
    }
}
