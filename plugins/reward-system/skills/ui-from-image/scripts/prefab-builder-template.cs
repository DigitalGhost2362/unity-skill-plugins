// Template: build a reward-system UI panel prefab through Unity.
// Run with script-execute, isMethodBody: false, className "PanelBuilder", methodName "Main".
//
// Adapt: PrefabPath, the panel component type, and everything inside BuildContent().
// Keep: the root component order, the Copy/PasteComponentValues block, and Set().
//
// The only full panel prefab in this repo to study is:
//   Assets/_GameBase/Prefabs/UI/Panel/RewardPanel.prefab (tabs + grid + disabled templates).

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using PainAndSeek;   // host panels. For package panels use NabaGame.Reward instead.
using NabaGame.UI;

public class PanelBuilder
{
    const string PrefabPath = "Assets/_GameBase/Prefabs/UI/Panel/MyPanel.prefab";

    // Any existing popup. Its Canvas / GraphicRaycaster / UIPanel (incl. in+out
    // animations) get copied onto the new panel. Do NOT rebuild animations by hand:
    // UIElement.Show() silently no-ops unless an In animation is enabled.
    const string SourcePanel = "Assets/_GameBase/Prefabs/UI/Panel/RewardPanel.prefab";

    // Prefer the target screen's own art folder (Sprites/asset/<screen>/) over these
    // generic sprites — see project-facts.md § sprites. Pick each sprite by NATIVE
    // ASPECT RATIO (dump the folder first, § sprite rule #2), never by colour alone.
    // Checkmarks: reward_0009_tich-V "78e997e6537efe7429c21f55dd900712".
    const string GuidBoxFill = "36b7b88e4f57fda4093961d8e7327b9e"; // play_0006_white-box-1 (fallback fill only)
    const string GuidClose = "ff8e0ca330523f141a312f288e9a9e99"; // play/close-button
    const string GuidFont = "8bf226b02a873e942b04b101f2a31744";     // PassionOne-Bold SDF
    const string GuidFontMat = "1892185b89f456d48b81da49a8047168";  // PassionOne-Bold Atlas Black.mat — mandatory

    static readonly Color PanelDark = new Color(0.101f, 0.121f, 0.219f, 1f);
    static readonly Color DarkText = new Color(0.129f, 0.145f, 0.243f, 1f);

    // ---------- helpers ----------

    static Sprite S(string guid)
    {
        string p = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    static GameObject UI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform Rect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Center(GameObject go, Vector2 pos, Vector2 size)
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        return Rect(go, c, c, c, pos, size);
    }

    static RectTransform Stretch(GameObject go)
    {
        return Rect(go, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    // Default Simple + preserveAspect. The art ships at the exact ratio it has in the
    // mockup: if a sprite seems to need Sliced / off-ratio stretching / tinting to fit,
    // it is the WRONG sprite (same-coloured siblings differ by ratio) — re-match by
    // native ratio instead of adjusting. Only 6/468 project sprites have 9-slice
    // borders at all; the warning below fires when Sliced would do nothing.
    static Image Img(GameObject go, string guid, bool sliced, bool raycast)
    {
        Image img = go.AddComponent<Image>();
        Sprite s = S(guid);
        img.sprite = s;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = raycast;
        if (!sliced) img.preserveAspect = true;
        if (sliced && s && s.border == Vector4.zero)
            Debug.LogWarning(go.name + ": Sliced but '" + s.name + "' has no 9-slice border — wrong sprite?");
        if (!s) img.color = new Color(1f, 1f, 1f, 0.15f);
        return img;
    }

    // Size a rect from the sprite's native ratio: give the height measured on the
    // mockup, the width follows. Use for structural art (boards, buttons, banners)
    // instead of hand-picking both numbers.
    static void FitNative(RectTransform rt, Image img, float height)
    {
        if (!img.sprite) return;
        Rect r = img.sprite.rect;
        rt.sizeDelta = new Vector2(height * r.width / r.height, height);
    }

    // Every repeated cell/slot/row is its own prefab in Prefabs/UI/Template/, named after
    // its widget script (QuestSlot.prefab hosts QuestSlotItem) — the user has had to
    // extract one by hand when a build skipped this. Call BEFORE SaveAsPrefabAsset on the
    // panel root: Connect turns the in-panel GO into a nested instance of the new prefab.
    // Deactivate the returned INSTANCE for dynamic-list templates; the asset root stays active.
    static GameObject SaveTemplate(GameObject templateGo, string prefabName)
    {
        string dir = "Assets/_GameBase/Prefabs/UI/Template";
        Directory.CreateDirectory(dir);
        PrefabUtility.SaveAsPrefabAssetAndConnect(templateGo, dir + "/" + prefabName + ".prefab",
            InteractionMode.AutomatedAction);
        return templateGo;
    }

    // Extra copies for a fixed board (a 7-day strip, spin wedges). Object.Instantiate here would
    // create plain clones that stop following the Template prefab. After nesting, wire the
    // instances IN ORDER into the panel's serialized List<> (SerializedObject.FindProperty)
    // and author per-slot cosmetics (background sprites) on each instance — decision #28.
    static GameObject Nest(string prefabName, Transform parent)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_GameBase/Prefabs/UI/Template/" + prefabName + ".prefab");
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.SetParent(parent, false);
        return go;
    }

    static TextMeshProUGUI Text(GameObject go, string content, float size, TextAlignmentOptions align, Color color)
    {
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        string fp = AssetDatabase.GUIDToAssetPath(GuidFont);
        TMP_FontAsset f = string.IsNullOrEmpty(fp) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fp);
        if (f) t.font = f;
        // Setting .font alone assigns the WRONG default material ("Atlas Material").
        // The project convention is Atlas Black (baked black outline) — must be explicit.
        string mp = AssetDatabase.GUIDToAssetPath(GuidFontMat);
        Material fm = string.IsNullOrEmpty(mp) ? null : AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (fm) t.fontSharedMaterial = fm;
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    // Private [SerializeField] fields can only be assigned this way.
    static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("no such field: " + field); return; }
        p.objectReferenceValue = value;
    }

    // ---------- build ----------

    public static void Main()
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePanel);
        if (!src) { Debug.LogError("source panel not found: " + SourcePanel); return; }

        GameObject root = new GameObject("MyPanel", typeof(RectTransform));
        root.layer = 5;
        Stretch(root);

        // Root component order matters: RectTransform, Canvas, CanvasGroup,
        // GraphicRaycaster, UIPanel, <your panel>.
        Canvas canvas = root.AddComponent<Canvas>();
        UnityEditorInternal.ComponentUtility.CopyComponent(src.GetComponent<Canvas>());
        UnityEditorInternal.ComponentUtility.PasteComponentValues(canvas);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;              // popups 200, animated sub-groups 201

        root.AddComponent<CanvasGroup>();

        GraphicRaycaster gr = root.AddComponent<GraphicRaycaster>();
        UnityEditorInternal.ComponentUtility.CopyComponent(src.GetComponent<GraphicRaycaster>());
        UnityEditorInternal.ComponentUtility.PasteComponentValues(gr);

        UIPanel uiPanel = root.AddComponent<UIPanel>();
        UnityEditorInternal.ComponentUtility.CopyComponent(src.GetComponent<UIPanel>());
        UnityEditorInternal.ComponentUtility.PasteComponentValues(uiPanel);

        // ---- replace with your panel type ----
        MonoBehaviour panel = null; // e.g. root.AddComponent<MyPanel>();

        BuildContent(root, uiPanel, panel);

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(saved ? "prefab created: " + PrefabPath : "FAILED to save prefab");
    }

    // Design resolution is 2400x1080 (see project-facts.md; re-confirm against the RefUI mockups).
    static void BuildContent(GameObject root, UIPanel uiPanel, MonoBehaviour panel)
    {
        GameObject window = UI("Window", root.transform);
        Center(window, Vector2.zero, new Vector2(2100f, 900f));
        Img(window, GuidBoxFill, true, true).color = PanelDark;

        GameObject header = UI("Header", window.transform);
        Rect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
             Vector2.zero, new Vector2(0f, 140f));
        Img(header, GuidBoxFill, true, false);

        GameObject titleGo = UI("Title", header.transform);
        Rect(titleGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
             new Vector2(60f, 0f), new Vector2(600f, 100f));
        Text(titleGo, "TITLE", 64f, TextAlignmentOptions.Left, Color.white);

        GameObject closeGo = UI("CloseButton", header.transform);
        Rect(closeGo, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
             new Vector2(-40f, 0f), new Vector2(100f, 100f));
        Img(closeGo, GuidClose, false, true);
        Button closeButton = closeGo.AddComponent<Button>();

        // Scrolling grid: ScrollRect > Viewport(RectMask2D) > Content(Grid+Fitter) > disabled template.
        GameObject scrollGo = UI("ItemScroll", window.transform);
        Center(scrollGo, new Vector2(0f, -60f), new Vector2(1900f, 640f));
        scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;

        GameObject viewport = UI("Viewport", scrollGo.transform);
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();

        GameObject content = UI("Content", viewport.transform);
        RectTransform contentRt = Rect(content, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                       new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 600f));
        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(255f, 285f);
        grid.spacing = new Vector2(20f, 20f);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = contentRt;

        GameObject itemGo = UI("ItemTemplate", content.transform);
        Center(itemGo, Vector2.zero, grid.cellSize);
        Img(itemGo, GuidBoxFill, true, true);
        itemGo.AddComponent<Button>();
        GameObject priceGo = UI("Price", itemGo.transform);
        Rect(priceGo, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
             new Vector2(0f, 12f), new Vector2(-20f, 56f));
        Text(priceGo, "950", 40f, TextAlignmentOptions.Center, DarkText);  // dark on light cells
        SaveTemplate(itemGo, "MyItem");   // BEFORE the panel save — itemGo becomes a nested instance
        itemGo.SetActive(false);          // deactivate the INSTANCE only; asset root stays active

        if (!panel) return;
        SerializedObject so = new SerializedObject(panel);
        Set(so, "uiPanel", uiPanel);          // BaseUI's field; only auto-filled by editor OnValidate
        Set(so, "closeButton", closeButton);
        Set(so, "itemScroll", scroll);
        Set(so, "itemContent", contentRt);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
