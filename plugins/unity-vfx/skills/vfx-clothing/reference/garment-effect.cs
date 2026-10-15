// Runtime glue for an outfit effect prefab placed as a child of a garment's SkinnedMeshRenderer.
// Binds every SkinnedMeshRenderer-shaped emitter to the garment (or to a low-poly proxy skinned to
// the same bones), optionally adds an overlay skin with an effect material, and feeds the overlay
// the garment's height range and texture so sweeps and color keys fit every garment and body.
// Adapt to the project's coding rules (Initialize convention, naming) before adding it.

using UnityEngine;
using UnityEngine.Rendering;

public class GarmentEffect : MonoBehaviour
{
    static readonly int SweepMinY = Shader.PropertyToID("_SweepMinY");
    static readonly int SweepMaxY = Shader.PropertyToID("_SweepMaxY");
    static readonly int GarmentTex = Shader.PropertyToID("_GarmentTex");

    [Tooltip("Shell drawn over the whole garment (sequins, holo, rim). Empty = particles only.")]
    [SerializeField] Material overlayMaterial;
    [Tooltip("Low-poly mesh skinned to the garment's bones that particles emit from (trim, hem). Empty = emit from the garment itself.")]
    [SerializeField] Mesh emitterMesh;
    [Tooltip("Emitters that always use the full garment surface, even when an emitter mesh is set.")]
    [SerializeField] ParticleSystem[] wholeGarmentEmitters;

    bool built;

    public void Initialize()
    {
        if (built) return;
        var garment = transform.parent ? transform.parent.GetComponent<SkinnedMeshRenderer>() : null;
        if (!garment)
        {
            Debug.LogError($"{nameof(GarmentEffect)} '{name}' must be a direct child of a garment SkinnedMeshRenderer.", this);
            return;
        }
        built = true;

        var emitterSkin = emitterMesh ? CreateSkin(garment, emitterMesh, "EmitterSkin") : garment;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var shape = ps.shape;
            if (!shape.enabled || shape.shapeType != ParticleSystemShapeType.SkinnedMeshRenderer) continue;
            bool whole = wholeGarmentEmitters != null && System.Array.IndexOf(wholeGarmentEmitters, ps) >= 0;
            shape.skinnedMeshRenderer = whole ? garment : emitterSkin;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
        }

        if (overlayMaterial) CreateOverlay(garment);

        var root = GetComponent<ParticleSystem>();
        if (root) { root.Clear(true); root.Play(true); }
    }

    // Same bones as the garment, so it deforms with it - but it is skinned again: one more skinning
    // pass and one more draw for the overlay.
    SkinnedMeshRenderer CreateSkin(SkinnedMeshRenderer garment, Mesh mesh, string skinName)
    {
        var skin = new GameObject(skinName).AddComponent<SkinnedMeshRenderer>();
        skin.transform.SetParent(transform, false);
        skin.sharedMesh = mesh;
        skin.bones = garment.bones;
        skin.rootBone = garment.rootBone;
        skin.localBounds = garment.localBounds;
        skin.sharedMaterials = new Material[0];
        skin.shadowCastingMode = ShadowCastingMode.Off;
        skin.receiveShadows = false;
        skin.lightProbeUsage = LightProbeUsage.Off;
        skin.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return skin;
    }

    void CreateOverlay(SkinnedMeshRenderer garment)
    {
        var skin = CreateSkin(garment, garment.sharedMesh, "OverlaySkin");
        skin.sharedMaterial = overlayMaterial;
        float origin = garment.transform.position.y;
        var block = new MaterialPropertyBlock();
        block.SetFloat(SweepMinY, garment.bounds.min.y - origin);
        block.SetFloat(SweepMaxY, garment.bounds.max.y - origin);
        var tex = garment.sharedMaterial ? garment.sharedMaterial.mainTexture : null;
        if (tex) block.SetTexture(GarmentTex, tex);
        skin.SetPropertyBlock(block);
    }
}
