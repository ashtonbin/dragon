using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Explicit, one-time scene authoring. Nothing here runs when entering Play.</summary>
public static class FacilitySceneGenerator
{
    public const string ScenePath = "Assets/Scenes/FacilityPrototype.unity";
    public const string RootName = "Facility Exploration";

    public static bool IsGeneratedArea(string name)
    {
        return name == "01_Arrival" || name == "02_Transit" || name == "03_Factory" ||
               name == "04_Research" || name == "05_Maintenance" || name == "06_ServiceReturn";
    }

    [MenuItem("Tools/Facility/Create Exploration Scene")]
    public static void CreateExplorationScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before creating a facility scene.");

        Scene original = SceneManager.GetActiveScene();
        RetroFPSController originalPlayer = FindInScene<RetroFPSController>(original);
        if (originalPlayer == null || originalPlayer.GetComponent<CharacterController>() == null)
            throw new InvalidOperationException("Open the working scene containing your Player before generation.");

        Directory.CreateDirectory("Assets/Scenes");
        string recovery = AssetDatabase.GenerateUniqueAssetPath(
            "Assets/Scenes/PrototypeRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(original, recovery, true))
            throw new IOException("Recovery save failed. The working scene has not been migrated.");

        string destination = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
        if (!EditorSceneManager.SaveScene(original, destination, true))
            throw new IOException("Could not create the exploration scene copy.");

        Scene scene = EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);
        try
        {
            // Only the new scene copy is changed. Keep manually authored objects and the existing rig.
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.name == RootName)
                {
                    sceneRoot.name = "Previous Facility Snapshot";
                    sceneRoot.SetActive(false);
                }
                foreach (RetroWorldBuilder builder in sceneRoot.GetComponentsInChildren<RetroWorldBuilder>(true))
                    builder.enabled = false;
                foreach (FacilityExpansion expansion in sceneRoot.GetComponentsInChildren<FacilityExpansion>(true))
                    expansion.enabled = false;
                foreach (FacilityAtmosphere atmosphere in sceneRoot.GetComponentsInChildren<FacilityAtmosphere>(true))
                    atmosphere.enabled = false;
                foreach (FacilityAmbientEffects effects in sceneRoot.GetComponentsInChildren<FacilityAmbientEffects>(true))
                    effects.enabled = false;
                foreach (Volume volume in sceneRoot.GetComponentsInChildren<Volume>(true))
                    volume.enabled = false;
                foreach (Light light in sceneRoot.GetComponentsInChildren<Light>(true))
                    light.enabled = false;
            }

            var assets = new FacilityAssetLibrary();
            assets.ApplyLowQuality();
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var layout = new FacilityLayoutBuilder(root.transform, assets);
            layout.Build();
            ConfigureSignFont(root, assets);
            ConfigureEnvironment();
            ConfigureLightingAndAudio(root.transform, layout, assets);
            ConfigureBakeParticipation(root);
            assets.ConfigureLighting();
            assets.ApplyPolishSettings();
            PlacePlayer(scene);
            ConfigureViewmodel(scene, assets);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the generated scene. The recovery scene is still available.");

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(destination, true) };
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry.path != destination) scenes.Add(new EditorBuildSettingsScene(entry.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
            Selection.activeGameObject = root;
            Debug.Log("Facility created: " + destination + ". Recovery: " + recovery +
                      ". Edit scene objects freely; use Tools > Facility > Bake Lighting after major edits.");
        }
        catch
        {
            Debug.LogError("Facility creation stopped. The original scene is preserved at " + recovery);
            throw;
        }
    }

    static void ConfigureEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.17f, 0.19f, 0.22f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.12f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.055f, 0.075f, 0.095f);
        RenderSettings.fogStartDistance = 7f;
        RenderSettings.fogEndDistance = 32f;
        RenderSettings.sun = null;
    }

    public static void ConfigureSignFont(GameObject root, FacilityAssetLibrary assets)
    {
        var binding = root.GetComponent<FacilitySignFont>();
        if (binding == null) binding = root.AddComponent<FacilitySignFont>();
        binding.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        binding.material = assets.Material("SignText");
        binding.Refresh();
    }

    public static void ConfigureViewmodel(Scene scene, FacilityAssetLibrary assets)
    {
        RetroFPSController player = FindInScene<RetroFPSController>(scene);
        RetroViewmodel viewmodel = player.GetComponentInChildren<RetroViewmodel>(true);
        if (viewmodel == null) throw new InvalidOperationException("The preserved Player is missing RetroViewmodel.");
        viewmodel.darkMaterial = assets.Material("ViewmodelDark");
        viewmodel.steelMaterial = assets.Material("ViewmodelSteel");
        viewmodel.accentMaterial = assets.Material("ViewmodelAccent");
        EditorUtility.SetDirty(viewmodel);
    }

    public static void ConfigureBakeParticipation(GameObject root)
    {
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.GetComponent<TextMesh>() != null) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Vector3 size = filter.sharedMesh.bounds.size;
            if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) >= .10f) continue;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveGI = ReceiveGI.LightProbes;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
        }
    }

    static void PlacePlayer(Scene scene)
    {
        RetroFPSController player = FindInScene<RetroFPSController>(scene);
        CharacterController capsule = player.GetComponent<CharacterController>();
        bool enabled = capsule.enabled;
        capsule.enabled = false;
        float footOffset = capsule.center.y - capsule.height * 0.5f;
        player.transform.SetPositionAndRotation(new Vector3(0f, 0.03f - footOffset, -3f), Quaternion.identity);
        capsule.enabled = enabled;
        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera == null) throw new InvalidOperationException("The preserved Player has no Camera.");
        player.cameraPivot = camera.transform;
        camera.backgroundColor = RenderSettings.fogColor;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = false;
        camera.farClipPlane = 70f;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.renderShadows = true;
    }

    public static void ConfigureLightingAndAudio(Transform root, FacilityLayoutBuilder layout, FacilityAssetLibrary assets)
    {
        Transform lights = new GameObject("Lighting and Sound").transform;
        lights.SetParent(root, false);
        Color cold = new Color(0.60f, 0.78f, 1f);
        Color neutral = new Color(0.75f, 0.83f, 0.91f);
        Color warm = new Color(1f, 0.64f, 0.36f);
        // Every emitter has one visible fixture, fixed directly to the room shell.
        AddFixture(lights, layout, "Arrival", new Vector3(0, 4.2f, 1.5f), cold, 4f);
        AddFixture(lights, layout, "Transit west", new Vector3(0, 4.2f, 8), cold, 3f);
        AddFixture(lights, layout, "Transit east", new Vector3(6, 4.2f, 14.5f), cold, 3f);
        AddFixture(lights, layout, "Factory entry", new Vector3(5, 8, 22), neutral, 12f);
        AddFixture(lights, layout, "Factory machinery", new Vector3(13, 8, 22), warm, 12f);
        AddFixture(lights, layout, "Factory mezzanine", new Vector3(-2, 8, 30), cold, 10f);
        AddFixture(lights, layout, "Factory gantry", new Vector3(12, 8, 30), neutral, 12f);
        AddFixture(lights, layout, "Research west", new Vector3(-8, 7.2f, 40.5f), cold, 4f);
        AddFixture(lights, layout, "Research east", new Vector3(-1, 7.2f, 38), cold, 3.5f);
        AddFixture(lights, layout, "Research alcove", new Vector3(-11.5f, 7.2f, 32), cold, 4f);
        AddFixture(lights, layout, "Maintenance repair", new Vector3(-16, 5.5f, 14), warm, 5f);
        AddFixture(lights, layout, "Maintenance entry", new Vector3(-8.5f, 5.5f, 22), warm, 5f);
        AddFixture(lights, layout, "Service long", new Vector3(-12, 3.4f, 6), neutral, 2.8f);
        AddFixture(lights, layout, "Service bend", new Vector3(-7.5f, 3.4f, 0), warm, 2.4f);
        AddFixture(lights, layout, "Ramp", new Vector3(-12, 7.2f, 27), cold, 4f);
        Renderer factoryPanel = AddWallFixture(lights, layout, "Factory emergency", new Vector3(17.875f, 2.7f, 30), -1, false);
        Renderer maintenancePanel = AddWallFixture(lights, layout, "Maintenance emergency", new Vector3(-17.875f, 2.7f, 22), 1);

        var volume = lights.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = assets.Profile();

        AudioSource bed = AddAudio(lights, "Ventilation bed", new Vector3(0, 2, 0), assets.Audio("Ventilation"), .16f, false, true);
        AudioSource generator = AddAudio(lights, "Pump vibration", new Vector3(10, 1.5f, 26), assets.Audio("Generator"), .30f, true, true);
        AudioSource duct = AddAudio(lights, "Duct turbulence", new Vector3(-12, 2.6f, 6), assets.Audio("Duct"), .23f, true, true);
        AudioSource lab = AddAudio(lights, "Research ventilation", new Vector3(-9, 5.9f, 40), assets.Audio("Duct"), .14f, true, true);
        AudioSource tick = AddAudio(lights, "Distant thermal contraction", new Vector3(15, 6f, 33), assets.Audio("Tick"), .22f, true, false);
        var effects = lights.gameObject.AddComponent<FacilityAmbientEffects>();
        effects.emergencyLights = Array.Empty<Light>();
        effects.emergencyPanels = new[] { factoryPanel, maintenancePanel };
        effects.loopingSources = new[] { bed, generator, duct, lab };
        effects.intermittentSource = tick;
        FacilityAudioAuthoring.ConfigureAmbient(effects);
        CreateProbes(lights);
    }

    static Light AddAreaLight(Transform parent, string name, Vector3 position, Quaternion rotation,
        Color color, float intensity, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localRotation = rotation;
        var light = obj.AddComponent<Light>();
        light.type = LightType.Rectangle;
        light.color = color;
        light.intensity = intensity * 5f;
        light.range = 18;
        light.areaSize = size;
        light.lightmapBakeType = LightmapBakeType.Baked;
        light.shadows = LightShadows.Soft;
        light.bounceIntensity = 1f;
        return light;
    }

    static void AddFixture(Transform parent, FacilityLayoutBuilder layout, string name, Vector3 position,
        Color color, float intensity)
    {
        Transform fixture = new GameObject(name + " Ceiling Fixture").transform;
        fixture.SetParent(parent, false);
        fixture.localPosition = position;
        FixturePiece(layout, fixture, "Ceiling Mount", new Vector3(0, -.04f, 0), new Vector3(.8f, .08f, 1.5f), "Steel");
        FixturePiece(layout, fixture, "Fixture Housing", new Vector3(0, -.135f, 0), new Vector3(.8f, .11f, 1.5f), "Steel");
        FixturePiece(layout, fixture, "Fluorescent Lens", new Vector3(0, -.2025f, 0), new Vector3(.6f, .025f, 1.3f), color.r > color.b ? "WarmGlow" : "ColdGlow");
        AddAreaLight(fixture, "Baked emitter", new Vector3(0, -.245f, 0), Quaternion.Euler(90, 0, 0), color, intensity, new Vector2(.6f, 1.3f));
    }

    static Renderer FixturePiece(FacilityLayoutBuilder layout, Transform parent, string name, Vector3 center, Vector3 size, string material)
    {
        GameObject piece = layout.Block(parent, name, center, size, material, false);
        GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic);
        var renderer = piece.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveGI = ReceiveGI.LightProbes;
        return renderer;
    }

    static Renderer AddWallFixture(Transform parent, FacilityLayoutBuilder layout, string name, Vector3 position, float inward, bool wash = true)
    {
        Transform fixture = new GameObject(name + " Wall Fixture").transform;
        fixture.SetParent(parent, false);
        fixture.localPosition = position;
        FixturePiece(layout, fixture, "Wall Mount", new Vector3(inward * .035f, 0, 0), new Vector3(.07f, .4f, .7f), "Steel");
        FixturePiece(layout, fixture, "Fixture Housing", new Vector3(inward * .115f, 0, 0), new Vector3(.09f, .35f, .65f), "Steel");
        Renderer panel = FixturePiece(layout, fixture, "Emergency Lens", new Vector3(inward * .175f, 0, 0), new Vector3(.03f, .25f, .5f), "RedGlow");
        if (wash)
            AddAreaLight(fixture, "Baked emergency wash", new Vector3(inward * .21f, 0, 0),
                Quaternion.LookRotation(Vector3.right * inward), new Color(1f, .25f, .08f), 1.4f, new Vector2(.5f, .25f));
        return panel;
    }

    static AudioSource AddAudio(Transform parent, string name, Vector3 position, AudioClip clip,
        float volume, bool spatial, bool loop)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        var source = obj.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = spatial ? 1f : 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = 12f;
        source.volume = volume;
        return source;
    }

    static void CreateProbes(Transform parent)
    {
        var probes = new List<Vector3>();
        Action<float, float, float> pair = (x, z, floor) => {
            probes.Add(new Vector3(x, floor + .8f, z));
            probes.Add(new Vector3(x, floor + 1.9f, z));
        };
        foreach (float x in new[] { -3f, 3f }) foreach (float z in new[] { -3f, 3f }) pair(x, z, 0);
        pair(0, 7, 0); pair(0, 11, 0); pair(3, 11, 0); pair(6, 11, 0); pair(6, 15, 0);
        foreach (float x in new[] { -4f, 3f, 15f }) foreach (float z in new[] { 19f, 25f, 32f }) pair(x, z, 0);
        pair(0, 30, 3); pair(-4, 33, 3);
        foreach (float x in new[] { -12f, -6f, 0f }) foreach (float z in new[] { 37f, 41f }) pair(x, z, 3);
        pair(-10, 32, 3);
        pair(-14.8f, 13, 0); pair(-8, 14, 0); pair(-8, 22, 0); pair(-16, 23, 0);
        foreach (float z in new[] { 20f, 23f, 26f, 28f }) pair(-12, z, Mathf.Max(0f, .5f * (z - 23)));
        pair(-12, 9, 0); pair(-12, 4, 0); pair(-12, 0, 0); pair(-8, 0, 0);
        var obj = new GameObject("Walkway light probes");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<LightProbeGroup>().probePositions = probes.ToArray();
    }

    [MenuItem("Tools/Facility/Bake Lighting")]
    public static void BakeLighting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before baking lighting.");
        if (Lightmapping.isRunning) throw new InvalidOperationException("A lighting bake is already running.");
        EditorSceneManager.SaveOpenScenes();
        if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity could not start the lighting bake.");
    }

    [MenuItem("Tools/Facility/Validate Exploration Scene")]
    public static void ValidateMenu() { Debug.Log(FacilityValidation.Validate(SceneManager.GetActiveScene())); }

    [MenuItem("Tools/Facility/Check Walking Routes")]
    public static void TraverseMenu() { Debug.Log(FacilityValidation.Traverse(SceneManager.GetActiveScene())); }

    public static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(false))
                if (component.gameObject.activeInHierarchy) return component;
        return null;
    }
}
