// Audit a built panel prefab: every serialized object reference, canvas sorting,
// UIPanel state, each button's persistent-call count (must be 0 in this project),
// every Image's sprite/type vs its native ratio (wrong-sprite smells), and every
// repeated widget/template child that should be a nested Prefabs/UI/Template/ prefab.
// Run with script-execute, isMethodBody: false, className "PanelVerify", methodName "Main".
// Set PrefabPath before running.
//
// Not every NULL is a bug — fields that are populated at runtime (e.g. a RenderTexture
// the widget creates in StartClass) legitimately read NULL on the asset. Check each one
// against the panel's code rather than assuming the wiring failed.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using NabaGame.UI;

public class PanelVerify
{
    const string PrefabPath = "Assets/_GameBase/Prefabs/UI/Panel/MyPanel.prefab";

    public static void Main()
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!go) { Debug.LogError("prefab missing: " + PrefabPath); return; }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== " + PrefabPath + " ===");

        int nullRefs = 0;
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string ns = mb.GetType().Namespace ?? "";
            if (ns != "PainAndSeek" && !ns.StartsWith("NabaGame.Reward")) continue;   // skip uGUI/framework components

            sb.AppendLine("--- " + mb.GetType().Name + " on '" + mb.name +
                          "' (active=" + mb.gameObject.activeSelf + ") ---");
            SerializedObject so = new SerializedObject(mb);
            SerializedProperty p = so.GetIterator();
            bool enter = true;
            while (p.NextVisible(enter))
            {
                enter = false;
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (p.name == "m_Script") continue;
                bool isNull = p.objectReferenceValue == null;
                if (isNull) nullRefs++;
                sb.AppendLine("  " + p.name + " = " +
                              (isNull ? "*** NULL ***" : p.objectReferenceValue.name));
            }
        }

        Canvas c = go.GetComponent<Canvas>();
        if (c) sb.AppendLine("canvas: overrideSorting=" + c.overrideSorting +
                             " order=" + c.sortingOrder + "  (popups must be 200)");

        UIPanel up = go.GetComponent<UIPanel>();
        if (up) sb.AppendLine("uiPanel: startHidden=" + up.startHidden +
                              " useBackground=" + up.useBackground);

        int badFontMats = 0;
        foreach (TMPro.TextMeshProUGUI t in go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
        {
            // Project convention: PassionOne-Bold Atlas Black, never the font's default material.
            string matPath = t.fontSharedMaterial ? AssetDatabase.GetAssetPath(t.fontSharedMaterial) : "";
            bool ok = matPath.EndsWith("PassionOne-Bold Atlas Black.mat");
            if (!ok) badFontMats++;
            sb.AppendLine("text '" + t.name + "' mat=" +
                          (t.fontSharedMaterial ? t.fontSharedMaterial.name : "NULL") +
                          (ok ? "" : "  *** must be PassionOne-Bold Atlas Black ***"));
        }

        int badButtons = 0;
        foreach (Button b in go.GetComponentsInChildren<Button>(true))
        {
            int n = b.onClick.GetPersistentEventCount();
            if (n > 0) badButtons++;
            sb.AppendLine("button '" + b.name + "' persistentCalls=" + n + (n > 0 ? "  *** must be 0 ***" : ""));
        }

        // The art ships at the exact ratio it has in the mockup, so a sprite that needed
        // Sliced or off-ratio stretching to fit is almost always the WRONG sprite (see
        // project-facts.md § sprite rule #2). Only 6/468 project sprites have borders.
        // User-corrected panels stay within ~20% of native ratio; 25% is the flag line.
        int spriteSmells = 0;
        foreach (Image img in go.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null) continue;
            float nativeR = img.sprite.rect.width / img.sprite.rect.height;
            RectTransform rt = (RectTransform)img.transform;
            string flag = "";
            if (img.type == Image.Type.Sliced && img.sprite.border == Vector4.zero)
            {
                flag = "  *** Sliced but sprite has no border — wrong sprite? ***";
                spriteSmells++;
            }
            else if (img.type == Image.Type.Simple && !img.preserveAspect &&
                     rt.rect.width > 1f && rt.rect.height > 1f)   // stretch-anchored rects read 0 on the asset
            {
                float rectR = rt.rect.width / rt.rect.height;
                if (Mathf.Abs(rectR - nativeR) / nativeR > 0.25f)
                {
                    flag = "  *** stretched " + rectR.ToString("F2") + " vs native " +
                           nativeR.ToString("F2") + " — wrong sprite? ***";
                    spriteSmells++;
                }
            }
            sb.AppendLine("image '" + img.name + "' sprite=" + img.sprite.name +
                          " type=" + img.type + (img.preserveAspect ? " pa" : "") + flag);
        }

        // Repeated elements must be nested instances of Prefabs/UI/Template/ prefabs.
        // IsPartOfPrefabInstance works on LoadAssetAtPath-loaded prefabs: true only for
        // nested instances; plain children (the QuestSlot_1..4 state the user had to fix
        // by hand) return false. Flags widget types that appear >1 time, plus anything
        // named like a template, when the GameObject is not a prefab instance.
        int plainTemplates = 0;
        Dictionary<System.Type, int> widgetCounts = new Dictionary<System.Type, int>();
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue; { var mns = mb.GetType().Namespace ?? ""; if (mns != "PainAndSeek" && !mns.StartsWith("NabaGame.Reward")) continue; }
            int n; widgetCounts.TryGetValue(mb.GetType(), out n);
            widgetCounts[mb.GetType()] = n + 1;
        }
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue; { var mns = mb.GetType().Namespace ?? ""; if (mns != "PainAndSeek" && !mns.StartsWith("NabaGame.Reward")) continue; }
            if (mb.transform == go.transform) continue;
            string lower = mb.name.ToLower();
            bool repeated = widgetCounts[mb.GetType()] > 1;
            bool looksTemplate = lower.Contains("template") || lower.Contains("slot") ||
                                 lower.Contains("tier") || lower.Contains("cell");
            if (!repeated && !looksTemplate) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(mb.gameObject))
            {
                plainTemplates++;
                sb.AppendLine("template '" + mb.name + "' (" + mb.GetType().Name +
                              ") is a PLAIN child  *** save to Prefabs/UI/Template/ and nest ***");
            }
        }

        Camera cam = go.GetComponentInChildren<Camera>(true);
        if (cam) sb.AppendLine("camera: cullingMask=" + cam.cullingMask +
                               " enabled=" + cam.enabled + " clear=" + cam.clearFlags);

        sb.AppendLine("SUMMARY nullRefs=" + nullRefs + " buttonsWithInspectorCalls=" + badButtons +
                      " wrongFontMaterials=" + badFontMats + " spriteSmells=" + spriteSmells +
                      " plainTemplates=" + plainTemplates);
        Debug.Log(sb.ToString());
    }
}
