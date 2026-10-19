
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FacilityAtmosphere : MonoBehaviour
{
    [Header("Atmosphere")]
    public Color fogColor = new Color(0.055f, 0.075f, 0.095f);
    public float fogStart = 7f;
    public float fogEnd = 32f;

    [Header("Sound")]
    [Range(0f, 1f)]
    public float ambienceVolume = 0.25f;

    private Light emergencyLight;
    private float emergencyBaseIntensity;
    private AudioClip ambienceClip;
    private VolumeProfile runtimeProfile;

    private Material trimMaterial;
    private Material warningMaterial;
    private Material darkMaterial;
    private Material fluorescentMaterial;

    IEnumerator Start()
    {
        // Let RetroWorldBuilder generate the map first.
        yield return null;

        ConfigureAtmosphere();
        ConfigurePostProcessing();
        CreateMaterials();
        DecorateFacility();
        CreateAmbience();
    }

    void ConfigureAtmosphere()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight =
            new Color(0.12f, 0.145f, 0.175f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        foreach (Light light in
            FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                light.enabled = false;
                continue;
            }

            light.shadows = LightShadows.None;

            switch (light.name)
            {
                case "Starting Light":
                    light.intensity = 2.0f;
                    break;

                case "Corridor Light":
                    light.intensity = 1.65f;
                    break;

                case "Factory Light":
                    light.intensity = 2.6f;
                    emergencyLight = light;
                    emergencyBaseIntensity = light.intensity;
                    break;

                case "Factory Blue Light":
                    light.intensity = 1.65f;
                    break;
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = fogColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    void ConfigurePostProcessing()
    {
        Volume volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = runtimeProfile;

        ColorAdjustments color =
            runtimeProfile.Add<ColorAdjustments>(true);

        color.contrast.Override(17f);
        color.saturation.Override(-16f);
        color.colorFilter.Override(
            new Color(0.90f, 0.94f, 1f));

        Vignette vignette =
            runtimeProfile.Add<Vignette>(true);

        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.42f);
    }

    Material MakeMaterial(string name, Color color, bool unlit)
    {
        string shaderName = unlit
            ? "Universal Render Pipeline/Unlit"
            : "Universal Render Pipeline/Lit";

        Material mat = new Material(Shader.Find(shaderName));
        mat.name = name;
        mat.SetColor("_BaseColor", color);

        if (!unlit)
            mat.SetFloat("_Smoothness", 0.05f);

        return mat;
    }

    void CreateMaterials()
    {
        trimMaterial = MakeMaterial(
            "Dark Structural Steel",
            new Color(0.13f, 0.16f, 0.18f), false);

        warningMaterial = MakeMaterial(
            "Warning Paint",
            new Color(0.55f, 0.25f, 0.07f), false);

        darkMaterial = MakeMaterial(
            "Unpowered Equipment",
            new Color(0.055f, 0.065f, 0.075f), false);

        fluorescentMaterial = MakeMaterial(
            "Cold Fluorescent Tube",
            new Color(0.43f, 0.72f, 0.85f), true);
    }

    void Part(string name, Vector3 position,
        Vector3 size, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Cube);

        obj.name = name;
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        obj.isStatic = true;
    }

    void CeilingFixture(float x, float z)
    {
        Part("Ceiling Fixture Housing",
            new Vector3(x, 3.88f, z),
            new Vector3(0.55f, 0.17f, 1.5f),
            trimMaterial);

        Part("Fluorescent Panel",
            new Vector3(x, 3.78f, z),
            new Vector3(0.40f, 0.035f, 1.3f),
            fluorescentMaterial);
    }

    void DecorateFacility()
    {
        // START ROOM: exposed industrial ceiling beams.
        for (int i = 0; i < 3; i++)
        {
            float z = -4f + i * 4f;

            Part("Overhead Steel Beam",
                new Vector3(0, 3.75f, z),
                new Vector3(11.5f, 0.24f, 0.3f),
                trimMaterial);
        }

        CeilingFixture(0, 0);

        // CORRIDOR: repeated structural bulkheads.
        for (int i = 0; i < 4; i++)
        {
            float z = 8f + i * 4f;

            Part("Bulkhead Left",
                new Vector3(-1.78f, 1.9f, z),
                new Vector3(0.24f, 3.8f, 0.28f),
                trimMaterial);

            Part("Bulkhead Right",
                new Vector3(1.78f, 1.9f, z),
                new Vector3(0.24f, 3.8f, 0.28f),
                trimMaterial);

            Part("Bulkhead Header",
                new Vector3(0, 3.75f, z),
                new Vector3(3.7f, 0.22f, 0.28f),
                trimMaterial);
        }

        // Wall-mounted cable channels.
        Part("Left Cable Channel",
            new Vector3(-1.76f, 2.65f, 14),
            new Vector3(0.1f, 0.12f, 15.5f),
            trimMaterial);

        Part("Right Cable Channel",
            new Vector3(1.76f, 2.65f, 14),
            new Vector3(0.1f, 0.12f, 15.5f),
            trimMaterial);

        CeilingFixture(0, 14);

        // FACTORY: additional overhead infrastructure.
        for (int i = 0; i < 3; i++)
        {
            float z = 25f + i * 5f;

            Part("Factory Crossbeam",
                new Vector3(0, 3.72f, z),
                new Vector3(15.5f, 0.32f, 0.34f),
                trimMaterial);
        }

        // Heavy machinery against the side walls.
        for (int i = 0; i < 3; i++)
        {
            float z = 26f + i * 4f;

            Part("Equipment Cabinet Left",
                new Vector3(-6.7f, 1.15f, z),
                new Vector3(1.25f, 2.3f, 1.6f),
                darkMaterial);

            Part("Equipment Cabinet Right",
                new Vector3(6.7f, 1.15f, z),
                new Vector3(1.25f, 2.3f, 1.6f),
                trimMaterial);

            Part("Cabinet Warning Strip",
                new Vector3(6.7f, 1.95f, z),
                new Vector3(1.27f, 0.12f, 1.62f),
                warningMaterial);
        }

        CeilingFixture(-3, 29);
        CeilingFixture(4, 34);

        // Hazard markings across the factory entrance.
        for (int i = 0; i < 7; i++)
        {
            float x = -1.6f + i * 0.52f;

            Part("Factory Threshold Marking",
                new Vector3(x, 0.012f, 23.25f),
                new Vector3(0.26f, 0.012f, 0.45f),
                warningMaterial);
        }
    }

    void CreateAmbience()
    {
        const int sampleRate = 22050;
        const int duration = 8;
        int sampleCount = sampleRate * duration;

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;

            // Slow ventilation pulsation.
            float pulse = 0.8f +
                0.2f * Mathf.Sin(Mathf.PI * 2f * 0.125f * t);

            // Layered mechanical frequencies.
            float low = Mathf.Sin(
                Mathf.PI * 2f * 55f * t) * 0.23f;

            float mid = Mathf.Sin(
                Mathf.PI * 2f * 83f * t) * 0.10f;

            float high = Mathf.Sin(
                Mathf.PI * 2f * 159f * t) * 0.025f;

            samples[i] = (low + mid + high) * pulse;
        }

        ambienceClip = AudioClip.Create(
            "Facility Ventilation Drone",
            sampleCount, 1, sampleRate, false);

        ambienceClip.SetData(samples, 0);

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = ambienceClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = ambienceVolume;
        source.Play();
    }

    void Update()
    {
        // Slow electrical instability, not rapid flashing.
        if (emergencyLight != null)
        {
            float variation = Mathf.PerlinNoise(
                Time.time * 0.4f, 4.7f);

            emergencyLight.intensity =
                emergencyBaseIntensity *
                Mathf.Lerp(0.68f, 1f, variation);
        }
    }

    void OnDestroy()
    {
        if (ambienceClip != null)
            Destroy(ambienceClip);

        if (runtimeProfile != null)
            Destroy(runtimeProfile);
    }
}
