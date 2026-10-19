using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Creates the facility's editable assets once; existing assets keep their manual edits.</summary>
public sealed class FacilityAssetLibrary
{
    public const string Root = "Assets/Generated/Facility";
    private const int SampleRate = 22050;
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    private readonly Dictionary<string, AudioClip> audio = new Dictionary<string, AudioClip>();

    public FacilityAssetLibrary()
    {
        EnsureFolder(Root);
        foreach (string folder in new[] { "Materials", "Textures", "Meshes", "Audio", "Settings" })
            EnsureFolder(Root + "/" + folder);
    }

    public Material Material(string key)
    {
        if (materials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        string path = Root + "/Materials/" + key + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return materials[key] = material;

        if (key == "SignText")
        {
            Shader signShader = Shader.Find("Facility/Sign Text");
            if (signShader == null)
                throw new InvalidOperationException("The Facility/Sign Text shader has not been imported.");
            material = new Material(signShader) { name = "Facility Sign Text" };
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return materials[key] = material;
        }

        Color32 color;
        bool glow = false;
        switch (key)
        {
            case "Concrete": color = new Color32(145, 151, 150, 255); break;
            case "Floor": color = new Color32(88, 97, 99, 255); break;
            case "Ceiling": color = new Color32(96, 105, 108, 255); break;
            case "Steel": color = new Color32(99, 112, 117, 255); break;
            case "Rust": color = new Color32(133, 89, 64, 255); break;
            case "Hazard": color = new Color32(209, 151, 49, 255); break;
            case "Glass": color = new Color32(15, 44, 49, 255); break;
            case "ViewmodelDark": color = new Color32(20, 23, 28, 255); break;
            case "ViewmodelSteel": color = new Color32(61, 69, 77, 255); break;
            case "ViewmodelAccent": color = new Color32(204, 82, 31, 255); break;
            case "ColdGlow": color = new Color32(134, 215, 237, 255); glow = true; break;
            case "WarmGlow": color = new Color32(255, 159, 76, 255); glow = true; break;
            case "RedGlow": color = new Color32(245, 63, 39, 255); glow = true; break;
            default: throw new ArgumentException("Unknown facility material: " + key, nameof(key));
        }

        Shader shader = Shader.Find(glow ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("The required URP shader could not be found for " + key + ".");

        material = new Material(shader) { name = "Facility " + key };
        bool viewmodel = key.StartsWith("Viewmodel", StringComparison.Ordinal);
        material.SetColor("_BaseColor", glow || key == "Glass" || viewmodel ? (Color)color : Color.white);
        if (!glow)
        {
            material.SetFloat("_Smoothness", key == "Glass" ? 0.22f : 0.08f);
            material.SetFloat("_Metallic", key == "Steel" ? 0.25f : 0f);
            if (key != "Glass" && !viewmodel)
                material.SetTexture("_BaseMap", Texture(key, color));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        if (viewmodel)
        {
            material.SetFloat("_Smoothness", .12f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", (Color)color * .12f);
        }
        if (key == "Glass")
        {
            material.SetColor("_BaseColor", new Color(0.16f, 0.34f, 0.39f, 0.20f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        // Glowing panels are visual markers; explicit baked lights provide their illumination.
        material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        materials[key] = material;
        return material;
    }

    public Mesh BoxMesh(Vector3 size)
    {
        ValidateDimension(size.x, nameof(size));
        ValidateDimension(size.y, nameof(size));
        ValidateDimension(size.z, nameof(size));
        string key = "Box_" + Number(size.x) + "x" + Number(size.y) + "x" + Number(size.z);
        if (TryMesh(key, out Mesh existing))
            return existing;

        Vector3 h = size * 0.5f;
        var builder = new MeshBuilder();
        builder.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
            new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z), size.x, size.y);
        builder.Quad(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z),
            new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z), size.x, size.y);
        builder.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z),
            new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), size.z, size.y);
        builder.Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z),
            new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z), size.z, size.y);
        builder.Quad(new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z),
            new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), size.x, size.z);
        builder.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z),
            new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), size.x, size.z);
        return SaveMesh(key, builder.Build(key));
    }

    /// <summary>Solid wedge: starts at (0,0,0), rises toward +Z, and has a flat bottom at Y=-0.25.</summary>
    public Mesh RampMesh(float width, float run, float rise)
    {
        ValidateDimension(width, nameof(width));
        ValidateDimension(run, nameof(run));
        ValidateDimension(rise, nameof(rise));
        string key = "Ramp_" + Number(width) + "x" + Number(run) + "x" + Number(rise);
        if (TryMesh(key, out Mesh existing))
            return existing;

        float x = width * 0.5f;
        const float bottom = -0.25f;
        var builder = new MeshBuilder();
        builder.Quad(new Vector3(-x, 0, 0), new Vector3(-x, rise, run),
            new Vector3(x, rise, run), new Vector3(x, 0, 0), width, Mathf.Sqrt(run * run + rise * rise));
        builder.Quad(new Vector3(-x, bottom, run), new Vector3(-x, bottom, 0),
            new Vector3(x, bottom, 0), new Vector3(x, bottom, run), width, run);
        builder.Quad(new Vector3(-x, bottom, 0), new Vector3(-x, 0, 0),
            new Vector3(x, 0, 0), new Vector3(x, bottom, 0), width, -bottom);
        builder.Quad(new Vector3(x, bottom, run), new Vector3(x, rise, run),
            new Vector3(-x, rise, run), new Vector3(-x, bottom, run), width, rise - bottom);
        builder.Quad(new Vector3(-x, bottom, run), new Vector3(-x, rise, run),
            new Vector3(-x, 0, 0), new Vector3(-x, bottom, 0),
            new Vector2(run, bottom) * 0.5f, new Vector2(run, rise) * 0.5f,
            Vector2.zero, new Vector2(0, bottom) * 0.5f);
        builder.Quad(new Vector3(x, bottom, 0), new Vector3(x, 0, 0),
            new Vector3(x, rise, run), new Vector3(x, bottom, run),
            new Vector2(0, bottom) * 0.5f, Vector2.zero,
            new Vector2(run, rise) * 0.5f, new Vector2(run, bottom) * 0.5f);
        return SaveMesh(key, builder.Build(key));
    }

    public AudioClip Audio(string key)
    {
        if (key == "Ventilation" || key == "Duct") return FacilitySoundDesign.CreepyHum();
        if (key != "Ventilation" && key != "Generator" && key != "Duct" && key != "Tick")
            throw new ArgumentException("Unknown facility audio: " + key, nameof(key));
        if (audio.TryGetValue(key, out AudioClip cached) && cached != null)
            return cached;

        string path = Root + "/Audio/" + key + ".wav";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip != null)
            return audio[key] = clip;

        int count = key == "Tick" ? Mathf.RoundToInt(SampleRate * 0.7f) : SampleRate * 8;
        float[] samples = new float[count];
        int seed = key == "Ventilation" ? 5279 : key == "Generator" ? 8111 : key == "Duct" ? 1831 : 2903;
        var random = new System.Random(seed);
        float slowNoise = 0f;
        float fastNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            slowNoise = Mathf.Lerp(slowNoise, noise, 0.018f);
            fastNoise = Mathf.Lerp(fastNoise, noise, 0.22f);
            float value;
            switch (key)
            {
                case "Generator":
                    value = Sine(43, t) * 0.20f + Sine(86, t) * 0.07f + Sine(129, t) * 0.025f;
                    value = value * (0.83f + Sine(0.125f, t) * 0.10f) + slowNoise * 0.25f;
                    break;
                case "Duct":
                    value = slowNoise * 0.65f + fastNoise * 0.035f + Sine(111, t) * 0.025f;
                    value *= 0.78f + Sine(0.25f, t) * 0.20f;
                    break;
                case "Tick":
                    float attack = Mathf.Clamp01(t / 0.002f);
                    value = (Sine(740, t) * 0.14f + Sine(1173, t) * 0.09f + noise * 0.09f)
                        * Mathf.Exp(-t * 15f) * attack;
                    break;
                default:
                    value = slowNoise * 0.5f + fastNoise * 0.06f + Sine(55, t) * 0.055f;
                    value *= 0.82f + Sine(0.125f, t) * 0.12f;
                    break;
            }
            // A short, smooth seam prevents random noise discontinuities without an audible hard click.
            float edge = Mathf.Clamp01(Mathf.Min(i, count - 1 - i) / (SampleRate * 0.04f));
            samples[i] = Mathf.Clamp(value * Mathf.SmoothStep(0f, 1f, edge), -0.95f, 0.95f);
        }
        WriteWave(path, samples);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = SampleRate;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            throw new InvalidOperationException("Failed to import facility audio: " + path);
        return audio[key] = clip;
    }

    public VolumeProfile Profile()
    {
        string path = Root + "/Settings/FacilityProfile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null)
            return profile;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Facility Profile";
        AssetDatabase.CreateAsset(profile, path);
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0f);
        color.contrast.Override(12f);
        color.saturation.Override(-12f);
        color.colorFilter.Override(new Color(0.96f, 0.98f, 1f));
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.42f);
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.None);
        // Explicitly override the template bloom setting, including when this is a scene volume.
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0f);
        foreach (VolumeComponent component in profile.components)
            AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    public void ConfigureLighting()
    {
        string path = Root + "/Settings/FacilityLighting.asset";
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Facility Manual Baked Lighting" };
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapResolution = 16f;
            settings.lightmapMaxSize = 1024;
            settings.lightmapPadding = 4;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 128;
            settings.environmentSampleCount = 32;
            settings.maxBounces = 2;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            settings.ao = true;
            settings.aoMaxDistance = 0.6f;
            settings.aoExponentDirect = 0f;
            settings.aoExponentIndirect = 1f;
            AssetDatabase.CreateAsset(settings, path);
        }
        // Unity 6.3 uses explicit baking; deliberately do not start a bake or clear existing lighting.
        Lightmapping.lightingSettings = settings;
    }

    /// <summary>
    /// Deliberately upgrades existing facility assets for the visual cleanup pass.
    /// Does not rewrite geometry/UV2, clear lightmaps, or start a bake; rebake after scene repairs.
    /// </summary>
    public void ApplyPolishSettings()
    {
        ConfigureLighting();
        LightingSettings settings = Lightmapping.lightingSettings;
        settings.lightmapResolution = 24f;
        settings.lightmapMaxSize = 1024;
        settings.lightmapPadding = 8;
        settings.directSampleCount = 64;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 64;
        settings.maxBounces = 2;
        settings.ao = false;
        settings.lightmapCompression = LightmapCompression.HighQuality;
        EditorUtility.SetDirty(settings);

        // Dark matte industrial surfaces should not produce isolated glints from tiny lights.
        // Keep the palette, glowing lenses, glass, signage, and weapon readability unchanged.
        foreach (string key in new[] { "Concrete", "Floor", "Ceiling", "Steel", "Rust", "Hazard" })
        {
            Material material = Material(key);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.03f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);

            Texture texture = material.GetTexture("_BaseMap");
            if (texture == null)
                continue;
            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null && !importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();
    }

    public void ApplyLowQuality()
    {
        ConfigureWindowsStability();
        string rendererPath = Root + "/Settings/FacilityLowRenderer.asset";
        UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            UniversalRendererData template = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/Mobile_Renderer.asset");
            if (template == null)
                throw new InvalidOperationException("The Mobile renderer template is missing.");
            renderer = Object.Instantiate(template);
            renderer.name = "Facility Low Renderer";
            renderer.rendererFeatures.Clear();
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }

        string pipelinePath = Root + "/Settings/FacilityLowPipeline.asset";
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            UniversalRenderPipelineAsset template = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/Mobile_RPAsset.asset");
            if (template == null)
                throw new InvalidOperationException("The Mobile pipeline template is missing.");
            pipeline = Object.Instantiate(template);
            pipeline.name = "Facility Low Pipeline";
            pipeline.supportsHDR = false;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.maxAdditionalLightsCount = 8;
            pipeline.shadowDistance = 40f;
            pipeline.useSRPBatcher = true;
            pipeline.supportsDynamicBatching = false;
            pipeline.colorGradingMode = ColorGradingMode.LowDynamicRange;
            pipeline.colorGradingLutSize = 16;
            // Several URP flags intentionally expose internal setters; use their serialized API.
            var serialized = new SerializedObject(pipeline);
            SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.Disabled;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AnyShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_VolumeProfile").objectReferenceValue = Profile();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 0;
        QualitySettings.realtimeReflectionProbes = false;
        AssetDatabase.SaveAssets();
    }

    public static void ConfigureWindowsStability()
    {
        // This prototype reproduced a native D3D12 graphics-job dependency crash on scene reload.
        // Keep the Windows low preset on the compatible Forward/D3D11 path for subsequent builds.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.graphicsJobs = false;
    }

    private Texture2D Texture(string key, Color32 color)
    {
        string path = Root + "/Textures/" + key + ".png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        const int side = 64;
        var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        var pixels = new Color32[side * side];
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            int noise = ((x * 17 + y * 29 + x * y * 3) % 19) - 9;
            bool seam = false;
            switch (key)
            {
                case "Concrete":
                    seam = x % 32 < 1 || y % 32 < 1;
                    if (x % 32 == 4 && y % 32 == 4) noise -= 38;
                    break;
                case "Floor": seam = x % 16 < 1 || y % 16 < 1; break;
                case "Ceiling": seam = y % 32 < 2 || x % 32 < 1; break;
                case "Steel":
                    seam = x % 32 < 1;
                    if (x % 32 == 4 && y % 32 == 4) noise += 35;
                    break;
                case "Rust":
                    if ((x * 7 + y * 13) % 31 < 8) noise -= 33;
                    seam = x % 32 < 1;
                    break;
                case "Hazard":
                    if (((x + y) / 8) % 2 == 0)
                    {
                        pixels[y * side + x] = new Color32(28, 32, 33, 255);
                        continue;
                    }
                    break;
            }
            if (seam) noise -= 28;
            pixels[y * side + x] = new Color32(
                (byte)Mathf.Clamp(color.r + noise, 0, 255),
                (byte)Mathf.Clamp(color.g + noise, 0, 255),
                (byte)Mathf.Clamp(color.b + noise, 0, 255), 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.sRGBTexture = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 0;
        importer.isReadable = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private bool TryMesh(string key, out Mesh mesh)
    {
        if (meshes.TryGetValue(key, out mesh) && mesh != null)
            return true;
        mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Meshes/" + key + ".asset");
        if (mesh == null)
            return false;
        meshes[key] = mesh;
        return true;
    }

    private Mesh SaveMesh(string key, Mesh mesh)
    {
        UnwrapParam.SetDefaults(out UnwrapParam unwrap);
        unwrap.packMargin = 4f / 1024f;
        if (!Unwrapping.GenerateSecondaryUVSet(mesh, unwrap))
        {
            Object.DestroyImmediate(mesh);
            throw new InvalidOperationException("Could not unwrap lightmap UVs for " + key + ".");
        }
        mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh, Root + "/Meshes/" + key + ".asset");
        meshes[key] = mesh;
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    private static string Number(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture).Replace(".", "p").Replace("-", "m");
    }

    private static void ValidateDimension(float value, string parameter)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(parameter, "Mesh dimensions must be finite and positive.");
    }

    private static float Sine(float frequency, float time)
    {
        return Mathf.Sin(2f * Mathf.PI * frequency * time);
    }

    private static void WriteWave(string path, float[] samples)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);
            foreach (float sample in samples)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767f));
        }
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, float height)
        {
            Quad(a, b, c, d, Vector2.zero, new Vector2(0, height * 0.5f),
                new Vector2(width, height) * 0.5f, new Vector2(width * 0.5f, 0));
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
