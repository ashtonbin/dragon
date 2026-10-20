using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Camera-specific visibility for the helmet's optical flare, including real cover.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class FacilityEliteOptic : MonoBehaviour
{
    public Renderer glowRenderer;
    static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    readonly RaycastHit[] obstructions = new RaycastHit[64];
    MaterialPropertyBlock properties;
    FacilityHumanoid owner;

    void OnEnable()
    {
        owner = GetComponentInParent<FacilityHumanoid>();
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
    }
    void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; }
    void BeforeCamera(ScriptableRenderContext context, Camera camera) { ApplyVisibility(camera); }
    void OnWillRenderObject() { ApplyVisibility(Camera.current); }

    void ApplyVisibility(Camera camera)
    {
        if (glowRenderer == null || camera == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        glowRenderer.GetPropertyBlock(properties);
        properties.SetFloat(VisibilityId, VisibilityFor(camera));
        glowRenderer.SetPropertyBlock(properties);
    }

    public float VisibilityFor(Camera camera)
    {
        if (!isActiveAndEnabled || camera == null || glowRenderer == null || !glowRenderer.enabled) return 0f;
        Vector3 toCamera = camera.transform.position - transform.position;
        float distance = toCamera.magnitude;
        if (distance < .001f) return 0f;
        Vector3 direction = toCamera / distance;
        // The lens is recessed in the front of the helmet; no glare through its back.
        float facing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .62f, Vector3.Dot(transform.forward, direction)));
        if (facing <= 0f) return 0f;
        int count = Physics.RaycastNonAlloc(transform.position, direction, obstructions, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider obstruction = obstructions[i].collider;
            if (obstruction == null || (owner != null && obstruction.transform.IsChildOf(owner.transform))) continue;
            if (obstruction.GetComponentInParent<RetroFPSController>() != null) continue;
            return 0f;
        }
        // Hide rather than spill a flare through unusually dense geometry.
        return count == obstructions.Length ? 0f : facing;
    }
}
