using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Persistent, surface-aligned buckshot scars. A fixed pool recycles the oldest marks.</summary>
public sealed class FacilitySurfaceImpacts : MonoBehaviour
{
    public Mesh holeMesh;
    public Material holeMaterial;
    public Material chippedRimMaterial;
    public Material dustMaterial;
    public int ActiveHoles { get; private set; }
    public int TotalImpacts { get; private set; }
    public int Capacity => 128;
    private readonly Transform[] holes = new Transform[128];
    private readonly Collider[] surfaces = new Collider[128];
    private Transform poolRoot;
    private ParticleSystem dust;
    private int cursor;
    private uint seed = 123718u;

    private void Awake()
    {
        GameObject root = new GameObject("Buckshot scars (128 persistent marks)");
        poolRoot = root.transform;
        root.layer = 2;
        Material[] materials = { holeMaterial, chippedRimMaterial };
        for (int i = 0; i < holes.Length; i++)
        {
            GameObject mark = new GameObject("Buckshot hole " + (i + 1), typeof(MeshFilter), typeof(MeshRenderer));
            mark.layer = 2;
            mark.transform.SetParent(poolRoot, false);
            mark.GetComponent<MeshFilter>().sharedMesh = holeMesh;
            MeshRenderer renderer = mark.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            holes[i] = mark.transform;
            mark.SetActive(false);
        }
        if (dustMaterial == null) return;
        GameObject particles = new GameObject("Surface impact dust", typeof(ParticleSystem));
        particles.layer = 2;
        particles.transform.SetParent(poolRoot, false);
        dust = particles.GetComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = dust.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 192;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .30f);
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .085f);
        main.startSpeed = 0f;
        main.gravityModifier = .35f;
        main.startColor = new Color(.48f, .44f, .39f, .45f);
        ParticleSystem.EmissionModule emission = dust.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.enabled = false;
        ParticleSystem.ColorOverLifetimeModule colors = dust.colorOverLifetime;
        colors.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(0f, 1f) });
        colors.color = fade;
        ParticleSystemRenderer dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
        dustRenderer.sharedMaterial = dustMaterial;
        dustRenderer.shadowCastingMode = ShadowCastingMode.Off;
        dustRenderer.receiveShadows = false;
        dustRenderer.lightProbeUsage = LightProbeUsage.Off;
    }

    public void Impact(RaycastHit hit)
    {
        if (hit.collider == null || hit.collider.attachedRigidbody != null || holeMesh == null || poolRoot == null) return;
        int index = cursor++ % holes.Length;
        Transform mark = holes[index];
        bool replacing = mark.gameObject.activeSelf;
        // Millimetres of offset avoid z fighting without leaving floating plates around corners.
        mark.position = hit.point + hit.normal * .0015f;
        mark.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, RandomRange(0f, 360f));
        float diameter = RandomRange(.038f, .058f);
        float aspect = RandomRange(.8f, 1.1f);
        // Trim the footprint beside corners instead of projecting a disc into empty air.
        bool supported = false;
        for (int fit = 0; fit < 5; fit++)
        {
            mark.localScale = new Vector3(diameter * aspect, diameter, 1f);
            supported = SupportedFootprint(mark, hit.collider);
            if (supported) break;
            diameter *= .55f;
        }
        if (!supported)
        {
            mark.gameObject.SetActive(false);
            if (replacing) ActiveHoles--;
            return;
        }
        surfaces[index] = hit.collider;
        mark.gameObject.SetActive(true);
        TotalImpacts++;
        if (!replacing) ActiveHoles++;
        if (dust == null) return;
        for (int i = 0; i < 3; i++)
        {
            Vector3 side = new Vector3(RandomRange(-1f, 1f), RandomRange(-1f, 1f), RandomRange(-1f, 1f));
            ParticleSystem.EmitParams burst = new ParticleSystem.EmitParams
            {
                position = hit.point + hit.normal * .015f,
                velocity = hit.normal * RandomRange(.45f, 1.5f) + side * .40f,
                rotation = RandomRange(0f, 360f)
            };
            dust.Emit(burst, 1);
        }
    }

    public bool ValidateAttachment(out string reason)
    {
        for (int i = 0; i < holes.Length; i++)
        {
            Transform mark = holes[i];
            if (mark == null || !mark.gameObject.activeSelf) continue;
            Collider support = surfaces[i];
            Ray ray = new Ray(mark.position + mark.forward * .025f, -mark.forward);
            if (support == null || !support.Raycast(ray, out RaycastHit hit, .06f) || Vector3.Distance(mark.position, hit.point) > .005f || !SupportedFootprint(mark, support))
            {
                reason = "Buckshot hole " + (i + 1) + " lost its supporting surface.";
                return false;
            }
        }
        reason = "All active buckshot holes are within 5 mm of their original surfaces.";
        return true;
    }

    private static bool SupportedFootprint(Transform mark, Collider surface)
    {
        for (int edge = 0; edge < 8; edge++)
        {
            float angle = edge * Mathf.PI * .25f;
            Vector3 rim = mark.position + mark.right * (Mathf.Cos(angle) * mark.localScale.x * .5f)
                + mark.up * (Mathf.Sin(angle) * mark.localScale.y * .5f);
            Ray ray = new Ray(rim + mark.forward * .025f, -mark.forward);
            if (!surface.Raycast(ray, out RaycastHit hit, .06f) || Mathf.Abs(Vector3.Dot(hit.point - mark.position, mark.forward)) > .005f)
                return false;
        }
        return true;
    }

    private float RandomRange(float min, float max)
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return min + (seed & 0xffffff) / 16777216f * (max - min);
    }

    private void OnDestroy()
    {
        if (poolRoot != null) Destroy(poolRoot.gameObject);
    }
}
