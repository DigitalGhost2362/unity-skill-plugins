// Point the user's Scene view at what you are about to change (solo Rule 3): select, ping, frame,
// repaint. Run with script-execute, isMethodBody: false, className "SoloFocus", and either
// methodName "Main" after editing FILL-IN, or methodName "Run" with string parameters target, mode.
// Works in edit and play mode. Returns one line: what was framed, its bounds, any warning.
//
// target: scene path "Root/Child/Leaf" or object name (loaded scenes and the open prefab stage,
//         inactive objects included), an asset path "Assets/.../X.mat" (frames the first renderer
//         using that material, else pings the asset), or explicit bounds "x,y,z;sx,sy,sz".
// mode:   close = frame the bounds (materials, meshes, one object)
//         wide  = establishing 3/4 view around the bounds, keeps the Scene view's yaw (lighting, mood)
//         keep  = select + ping only, camera stays where it is (several steps on one subject)

using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoloFocus
{
    // ---------- FILL-IN ----------
    const string Target = "Root/Child";
    const string Mode = "close";
    // ------------------------------

    const float TinySize = 0.05f;
    const float HugeSize = 2000f;

    public static string Main() => Run(Target, Mode);

    public static string Run(string target, string mode)
    {
        mode = string.IsNullOrEmpty(mode) ? "close" : mode.ToLowerInvariant();
        string warn = "";
        Object pick = null;
        Bounds bounds;

        if (target.Contains(";"))
            bounds = ParseBounds(target);
        else
        {
            GameObject go = null;
            if (target.StartsWith("Assets/") || target.StartsWith("Packages/"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(target);
                if (!asset) return Log("solo-focus: asset not found: " + target);
                if (asset is Material mat)
                {
                    go = AllObjects().FirstOrDefault(g => g.TryGetComponent<Renderer>(out var r) && r.sharedMaterials.Contains(mat));
                    if (!go) warn += " no renderer in the open scenes uses this material;";
                }
                if (!go)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    SceneView.RepaintAll();
                    return Log($"solo-focus: pinged asset {target}, camera unchanged;{warn}");
                }
            }
            else
            {
                go = AllObjects().FirstOrDefault(g => PathOf(g) == target)
                     ?? AllObjects().FirstOrDefault(g => g.name == target);
                if (!go) return Log("solo-focus: not found in the open scenes or prefab stage: " + target);
            }

            pick = go;
            bounds = BoundsOf(go, ref warn);
            if (!go.activeInHierarchy) warn += " object is inactive;";
            if (SceneVisibilityManager.instance.IsHidden(go)) { SceneVisibilityManager.instance.Show(go, true); warn += " was hidden in the Scene view, shown;"; }
        }

        float size = bounds.size.magnitude;
        if (size < TinySize) { warn += $" tiny bounds ({size:0.###} m), framing 1 m around it;"; bounds.size = Vector3.one; }
        if (size > HugeSize) { warn += $" huge bounds ({size:0} m), check the target - framing 200 m around its centre;"; bounds.size = Vector3.one * 200f; }

        if (pick)
        {
            Selection.activeObject = pick;
            EditorGUIUtility.PingObject(pick);
        }

        var sv = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        if (mode == "close")
            sv.Frame(bounds, false);
        else if (mode == "wide")
        {
            float yaw = sv.rotation.eulerAngles.y;
            sv.LookAt(bounds.center, Quaternion.Euler(30f, yaw, 0f), Mathf.Max(bounds.extents.magnitude * 2.5f, 10f));
        }
        else if (mode != "keep")
            warn += $" unknown mode '{mode}', camera unchanged;";

        SceneView.RepaintAll();
        string what = pick ? PathOf((GameObject)pick) : "bounds";
        return Log($"solo-focus: {mode} on {what} centre {Fmt(bounds.center)} size {Fmt(bounds.size)};{warn}");
    }

    static Bounds BoundsOf(GameObject go, ref string warn)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
        var colliders = go.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
        if (colliders.Length > 0)
        {
            var b = colliders[0].bounds;
            foreach (var c in colliders) b.Encapsulate(c.bounds);
            return b;
        }
        if (go.TryGetComponent<Light>(out var light) && light.type != LightType.Directional)
            return new Bounds(go.transform.position, Vector3.one * light.range * 2f);
        warn += " no enabled renderer or collider, framing the pivot;";
        return new Bounds(go.transform.position, Vector3.one * 4f);
    }

    static System.Collections.Generic.IEnumerable<GameObject> AllObjects()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            return stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject);
        return Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
            .SelectMany(s => s.GetRootGameObjects())
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    }

    static string PathOf(GameObject go)
    {
        string path = go.name;
        for (var t = go.transform.parent; t; t = t.parent) path = t.name + "/" + path;
        return path;
    }

    static Bounds ParseBounds(string s)
    {
        var parts = s.Split(';');
        return new Bounds(ParseVec(parts[0]), ParseVec(parts[1]));
    }

    static Vector3 ParseVec(string s)
    {
        var v = s.Split(',').Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(v[0], v[1], v[2]);
    }

    static string Fmt(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

    static string Log(string msg)
    {
        Debug.Log(msg);
        return msg;
    }
}
