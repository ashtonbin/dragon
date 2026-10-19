using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Recovery-first, explicit campaign installation. Existing sandbox geometry and lighting are retained.</summary>
public static class FacilityStoryInstaller
{
    public const string StoryScenePath = "Assets/Scenes/StoryLevel01.unity";
    const string AssetRoot = "Assets/Generated/Story";

    /// <summary>Improve the two dark-mode material landmarks without changing geometry or baked illumination.</summary>
    [MenuItem("Tools/Facility/Finish Story Landmark Materials")]
    public static void FinishMaterials()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish story materials outside Play mode and baking.");
        Material paint = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/Sedan black paint.mat");
        Material bullion = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/Bullion gold.mat");
        if (paint == null || bullion == null) throw new InvalidOperationException("The authored sedan paint or bullion material is missing.");
        paint.SetColor("_BaseColor", new Color(.055f, .065f, .075f));
        paint.EnableKeyword("_EMISSION"); paint.SetColor("_EmissionColor", new Color(.006f, .008f, .011f));
        paint.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        bullion.SetColor("_BaseColor", new Color(1f, .84f, .24f));
        bullion.SetFloat("_Metallic", .05f); bullion.SetFloat("_Smoothness", .22f);
        bullion.EnableKeyword("_EMISSION"); bullion.SetColor("_EmissionColor", new Color(.18f, .135f, .028f));
        bullion.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(paint); EditorUtility.SetDirty(bullion); AssetDatabase.SaveAssets();
        Debug.Log("Story landmark materials saved: readable black sedan paint and yellow gold. Baked lighting preserved.");
    }

    /// <summary>Move ceiling emitters clear of the structural beams and illuminate the enclosed watch office.</summary>
    [MenuItem("Tools/Facility/Repair Story Light Placement")]
    public static void TouchUpLights()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Repair story lights outside Play mode and baking.");
        Scene scene = SceneManager.GetActiveScene();
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        GameObject level = null;
        foreach (GameObject item in scene.GetRootGameObjects()) if (item.name == "Story Level 1 - Night Deposit") level = item;
        if (director == null || level == null) throw new InvalidOperationException("Open the authored story scene first.");
        Transform counting = level.transform.Find("04 Counting floor - records and transfer machinery");
        Transform office = level.transform.Find("03 Bent security passage and watch office");
        Transform parkingLamp = level.transform.Find("01 Night parking - sedan and entrance/Parking lamp on concrete footing/Parking sodium lamp");
        if (counting == null || office == null || parkingLamp == null) throw new InvalidOperationException("The exact authored light groups are missing; no geometry was replaced.");
        var ceilingFixtures = new List<Transform>();
        foreach (Transform child in counting) if (child.name == "Counting floor ceiling light") ceilingFixtures.Add(child);
        if (ceilingFixtures.Count != 3) throw new InvalidOperationException("Expected three counting-floor ceiling fixtures before moving them clear of the beams.");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/StoryLightRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true)) throw new IOException("Recovery save failed; story lighting repair stopped.");
        foreach (Transform fixture in ceilingFixtures)
        {
            Undo.RecordObject(fixture, "Move light between ceiling beams");
            Vector3 position = fixture.localPosition; position.z = 21; fixture.localPosition = position;
        }
        Light parking = parkingLamp.GetComponentInChildren<Light>(true);
        Undo.RecordObject(parking, "Light the extraction sedan"); parking.intensity = 20; parking.range = 20;
        Transform officeFixture = FacilityStoryLevelBuilder.BuildOfficeFixture(office);
        foreach (MeshRenderer renderer in officeFixture.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            bool substantial = filter != null && filter.sharedMesh != null && filter.sharedMesh.bounds.size.y >= .08f;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, substantial ? StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic : StaticEditorFlags.OccludeeStatic);
            renderer.receiveGI = substantial ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            renderer.shadowCastingMode = substantial ? ShadowCastingMode.On : ShadowCastingMode.Off;
            EditorUtility.SetDirty(renderer);
        }
        var accents = new HashSet<Light>(director.alarmLights ?? Array.Empty<Light>());
        foreach (Light light in level.GetComponentsInChildren<Light>(true))
        {
            if (!accents.Contains(light)) { light.lightmapBakeType = LightmapBakeType.Baked; light.shadows = LightShadows.Hard; }
            EditorUtility.SetDirty(light);
        }
        FinishMaterials();
        // Keep the current story lighting asset/probes. The explicit next bake replaces only this scene's maps.
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save story light repair. Recovery: " + recovery);
        Debug.Log("Story lights repaired: ten fixed baked emitters clear of geometry, two realtime alarms. Rebake the active story scene. Recovery: " + recovery);
    }

    /// <summary>Apply the verified counter-clearance repair to an existing story snapshot without replacing authored geometry.</summary>
    [MenuItem("Tools/Facility/Repair Story Counter Clearance")]
    public static void TouchUp()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Repair story clearance outside Play mode and baking.");
        Scene scene = SceneManager.GetActiveScene();
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
        GameObject level = null;
        foreach (GameObject item in scene.GetRootGameObjects()) if (item.name == "Story Level 1 - Night Deposit") level = item;
        if (director == null || session == null || level == null) throw new InvalidOperationException("Open the authored story scene first.");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/StoryClearanceRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true)) throw new IOException("Recovery save failed; story clearance repair stopped.");
        Transform basket = level.transform.Find("02 Reception - quiet before the alarm/Waste basket");
        if (basket == null)
            foreach (Transform item in level.GetComponentsInChildren<Transform>(true)) if (item.name == "Waste basket") { basket = item; break; }
        if (basket == null) throw new InvalidOperationException("The named reception waste basket is missing; no geometry was replaced.");
        Undo.RecordObject(basket, "Clear reception counter approach");
        basket.localPosition = new Vector3(-5.9f, .28f, 5.8f);
        foreach (NavMeshSurface navigation in session.transform.root.GetComponentsInChildren<NavMeshSurface>(true))
        { navigation.RemoveData(); Object.DestroyImmediate(navigation); }
        BuildNavigation(session.transform.root.gameObject, level);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save story clearance repair. Recovery: " + recovery);
        Debug.Log("Story counter approach cleared; saved navigation rebuilt. Lighting requires rebaking. Recovery: " + recovery);
    }

    [MenuItem("Tools/Facility/Install Story and Sandbox Modes")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Install story scenes outside Play mode and baking.");
        Scene initial = SceneManager.GetActiveScene();
        if (!initial.IsValid() || !initial.isLoaded) throw new InvalidOperationException("Open the working facility scene first.");
        Folder("Assets/Scenes");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/StoryRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        // This must precede upgrades, scene switching, asset authoring or hierarchy mutations.
        if (!EditorSceneManager.SaveScene(initial, recovery, true))
            throw new IOException("Recovery save failed. Story installation stopped without modifying the working scene.");

        Scene sandbox = initial;
        if (!HasFacility(sandbox))
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FacilitySceneGenerator.ScenePath) == null)
                throw new InvalidOperationException("The editable sandbox scene is missing. Recovery: " + recovery);
            sandbox = EditorSceneManager.OpenScene(FacilitySceneGenerator.ScenePath, OpenSceneMode.Single);
        }
        RetroFPSController player = FacilitySceneGenerator.FindInScene<RetroFPSController>(sandbox);
        FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(sandbox);
        if (player == null || session == null || player.GetComponent<CharacterController>() == null)
            throw new InvalidOperationException("The sandbox needs its existing Player and authored menu session. Recovery: " + recovery);
        if (string.IsNullOrEmpty(sandbox.path))
        {
            string sandboxDestination = AssetDatabase.GenerateUniqueAssetPath(FacilitySceneGenerator.ScenePath);
            if (!EditorSceneManager.SaveScene(sandbox, sandboxDestination)) throw new IOException("Could not save the sandbox snapshot. Recovery: " + recovery);
        }

        string storyPath = AssetDatabase.GenerateUniqueAssetPath(StoryScenePath);
        string sandboxPath = sandbox.path;
        string sandboxName = Path.GetFileNameWithoutExtension(sandboxPath);
        string storyName = Path.GetFileNameWithoutExtension(storyPath);
        FacilityWeaponInventory inventory = FacilityPlayerArsenalAuthoring.Build(player);
        session.player = player; session.health = player.GetComponent<FacilityPlayerHealth>();
        session.inventory = inventory; session.storyDirector = null;
        session.sandboxSceneName = sandboxName; session.storySceneNames = new[] { storyName };
        if (session.spawner != null)
        {
            session.spawner.player = player;
            session.spawner.aimCamera = player.GetComponentInChildren<Camera>(true);
            session.spawner.enabled = true;
            EditorUtility.SetDirty(session.spawner);
        }
        FacilityModeUIAuthoring.Install(session);
        FacilityObserverLoadoutAuthoring.Install(session);
        EditorUtility.SetDirty(session); EditorUtility.SetDirty(inventory);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(sandbox);
        if (!EditorSceneManager.SaveScene(sandbox)) throw new IOException("Could not save upgraded sandbox. Recovery: " + recovery);

        // Copying the serialized scene remaps all Player <-> menu references together. The source
        // is saved and closed before any new geometry, lighting or navigation is collected.
        if (!EditorSceneManager.SaveScene(sandbox, storyPath, true)) throw new IOException("Could not create the separate story snapshot. Recovery: " + recovery);
        Scene story = EditorSceneManager.OpenScene(storyPath, OpenSceneMode.Single);
        try
        {
            player = FacilitySceneGenerator.FindInScene<RetroFPSController>(story);
            session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(story);
            if (player == null || session == null) throw new InvalidOperationException("The story copy lost its preserved Player or interface.");
            GameObject playerRoot = player.transform.root.gameObject;
            GameObject systemsRoot = session.transform.root.gameObject;
            if (playerRoot == systemsRoot) throw new InvalidOperationException("The Player and game systems must use separate authored roots.");
            // These deletions apply only to the new snapshot; unrelated authored content remains in sandbox.
            foreach (GameObject item in story.GetRootGameObjects())
                if (item != playerRoot && item != systemsRoot) Object.DestroyImmediate(item);
            foreach (NavMeshSurface oldNavigation in systemsRoot.GetComponentsInChildren<NavMeshSurface>(true))
            { oldNavigation.RemoveData(); Object.DestroyImmediate(oldNavigation); }
            foreach (FacilityStoryDirector oldDirector in systemsRoot.GetComponentsInChildren<FacilityStoryDirector>(true)) Object.DestroyImmediate(oldDirector);
            foreach (RetroWorldBuilder legacy in playerRoot.GetComponentsInChildren<RetroWorldBuilder>(true)) legacy.enabled = false;
            foreach (FacilityExpansion legacy in playerRoot.GetComponentsInChildren<FacilityExpansion>(true)) legacy.enabled = false;
            foreach (FacilityAtmosphere legacy in playerRoot.GetComponentsInChildren<FacilityAtmosphere>(true)) legacy.enabled = false;

            session.player = player; session.health = player.GetComponent<FacilityPlayerHealth>();
            session.inventory = player.GetComponent<FacilityWeaponInventory>();
            session.sandboxSceneName = sandboxName; session.storySceneNames = new[] { storyName };
            if (session.spawner != null)
            {
                session.spawner.player = player;
                session.spawner.aimCamera = player.GetComponentInChildren<Camera>(true);
                session.spawner.enabled = false;
            }
            FacilityStoryDirector director = systemsRoot.AddComponent<FacilityStoryDirector>();
            session.storyDirector = director; director.player = player; director.inventory = session.inventory;
            GameObject level = FacilityStoryLevelBuilder.Build(player, director);
            FacilityModeUIAuthoring.BindStory(session, director);
            session.inventory.ConfigureLoadout(false);
            ConfigureNight(player, level);
            ConfigureLighting(story, level, director);
            BuildNavigation(systemsRoot, level);
            ConfigureBuildSettings(sandboxPath, storyPath);
            AssetDatabase.SaveAssets(); EditorUtility.SetDirty(session); EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(story);
            if (!EditorSceneManager.SaveScene(story)) throw new IOException("Could not save the authored story scene. Recovery: " + recovery);
            FacilityHeistInstaller.Install();
            FacilityBankAuthoring.Install();
            Selection.activeGameObject = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(SceneManager.GetActiveScene()).gameObject;
            Debug.Log("Story installed: " + storyPath + "; sandbox preserved: " + sandboxPath + "; recovery: " + recovery + ". Bake the active story scene before build.");
        }
        catch
        {
            Debug.LogError("Story installation stopped. The saved sandbox and recovery are preserved at " + recovery);
            throw;
        }
    }

    static bool HasFacility(Scene scene)
    {
        foreach (GameObject item in scene.GetRootGameObjects())
            if (item.name == FacilitySceneGenerator.RootName) return true;
        return false;
    }

    static void ConfigureNight(RetroFPSController player, GameObject level)
    {
        RenderSettings.skybox = null; RenderSettings.sun = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.075f, .095f, .13f); RenderSettings.ambientIntensity = 1;
        RenderSettings.reflectionIntensity = .08f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.018f, .027f, .045f);
        RenderSettings.fogStartDistance = 12; RenderSettings.fogEndDistance = 55;
        Camera camera = player.GetComponentInChildren<Camera>(true);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
        camera.farClipPlane = 95; camera.allowHDR = false; camera.allowMSAA = false;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true; data.renderShadows = true; data.antialiasing = AntialiasingMode.None;
        data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
        GameObject grading = new GameObject("Restrained night grading"); grading.transform.SetParent(level.transform, false);
        Volume volume = grading.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10;
        volume.sharedProfile = new FacilityAssetLibrary().Profile();
        EditorUtility.SetDirty(camera);
    }

    static void ConfigureLighting(Scene scene, GameObject level, FacilityStoryDirector director)
    {
        Folder(AssetRoot + "/Settings");
        LightingSettings settings = new LightingSettings { name = Path.GetFileNameWithoutExtension(scene.path) + " Manual Lighting" };
        settings.bakedGI = true; settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.lightmapResolution = 16; settings.lightmapMaxSize = 1024; settings.lightmapPadding = 8;
        settings.directSampleCount = 64; settings.indirectSampleCount = 192; settings.environmentSampleCount = 64;
        settings.maxBounces = 2; settings.filteringMode = LightingSettings.FilterMode.Auto;
        settings.ao = false; settings.lightmapCompression = LightmapCompression.HighQuality;
        AssetDatabase.CreateAsset(settings, AssetDatabase.GenerateUniqueAssetPath(AssetRoot + "/Settings/Story Lighting.asset"));
        Lightmapping.lightingSettings = settings;
        // The scene snapshot must not retain the sandbox's baked-data reference.
        Lightmapping.lightingDataAsset = null;
        LightmapSettings.lightmaps = Array.Empty<LightmapData>();
        var accents = new HashSet<Light>(director.alarmLights ?? Array.Empty<Light>());
        foreach (Light light in level.GetComponentsInChildren<Light>(true))
        {
            if (accents.Contains(light)) { light.lightmapBakeType = LightmapBakeType.Realtime; light.shadows = LightShadows.None; light.enabled = false; }
            else { light.lightmapBakeType = LightmapBakeType.Baked; light.shadows = LightShadows.Hard; }
            EditorUtility.SetDirty(light);
        }
        foreach (MeshRenderer renderer in level.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.GetComponentInParent<FacilityHumanoid>() != null) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            bool pickup = IsPickupVisual(renderer.transform, director);
            Vector3 size = filter.sharedMesh.bounds.size;
            bool contributor = !pickup && Mathf.Min(size.x, Mathf.Min(size.y, size.z)) >= .08f;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                contributor ? StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic : StaticEditorFlags.OccludeeStatic);
            renderer.receiveGI = contributor ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            renderer.shadowCastingMode = contributor ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            if (contributor)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh.uv2 == null || mesh.uv2.Length != mesh.vertexCount)
                {
                    if (!Unwrapping.GenerateSecondaryUVSet(mesh)) throw new InvalidOperationException("Could not unwrap persistent story mesh: " + mesh.name);
                    EditorUtility.SetDirty(mesh);
                }
            }
            EditorUtility.SetDirty(renderer);
        }
        AddProbes(level.transform);
    }

    static bool IsPickupVisual(Transform item, FacilityStoryDirector director)
    {
        foreach (FacilityStoryInteractable pickup in new[] { director.pistolPickup, director.cashPickup, director.goldPickup })
            if (pickup != null && pickup.visualRoot != null && item.IsChildOf(pickup.visualRoot.transform)) return true;
        return false;
    }

    static void AddProbes(Transform level)
    {
        GameObject holder = new GameObject("Walking route light probes"); holder.transform.SetParent(level, false);
        var probes = new List<Vector3>();
        Vector3[] route = {
            new Vector3(-6,0,-18), new Vector3(-6,0,-12), new Vector3(0,0,-8), new Vector3(0,0,-2), new Vector3(0,0,2.6f),
            new Vector3(3,0,2.6f), new Vector3(3,0,6.5f), new Vector3(4.5f,0,9.5f), new Vector3(4.5f,0,14), new Vector3(-4,0,14),
            new Vector3(-4,0,17.6f), new Vector3(0,0,17.6f), new Vector3(0,0,23), new Vector3(0,0,29), new Vector3(0,0,34.5f), new Vector3(0,0,38.8f)
        };
        for (int i = 0; i < route.Length - 1; i++)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route[i], route[i + 1]) / 3));
            for (int j = 0; j < segments; j++)
            {
                Vector3 at = Vector3.Lerp(route[i], route[i + 1], j / (float)segments);
                probes.Add(at + Vector3.up * .55f); probes.Add(at + Vector3.up * 1.55f); probes.Add(at + Vector3.up * 2.55f);
            }
        }
        foreach (float x in new[] { -9f, 9f }) foreach (float z in new[] { 18f, 22.5f, 28.5f })
        { probes.Add(new Vector3(x, .6f, z)); probes.Add(new Vector3(x, 1.7f, z)); probes.Add(new Vector3(x, 3.5f, z)); }
        foreach (float x in new[] { -4.8f, -2.3f })
        { probes.Add(new Vector3(x, .5f, 11.7f)); probes.Add(new Vector3(x, 1.7f, 11.7f)); probes.Add(new Vector3(x, 2.7f, 11.7f)); }
        holder.AddComponent<LightProbeGroup>().probePositions = probes.ToArray();
    }

    public static void RebuildNavigation(Scene scene)
    {
        FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
        GameObject level = null;
        foreach (GameObject item in scene.GetRootGameObjects()) if (item.name == "Story Level 1 - Night Deposit") level = item;
        if (session == null || level == null) throw new InvalidOperationException("Open the saved story scene before rebuilding its navigation.");
        foreach (NavMeshSurface surface in session.transform.root.GetComponentsInChildren<NavMeshSurface>(true))
        { surface.RemoveData(); Object.DestroyImmediate(surface); }
        BuildNavigation(session.transform.root.gameObject, level);
    }

    static void BuildNavigation(GameObject systems, GameObject level)
    {
        Folder(AssetRoot + "/Navigation");
        Physics.SyncTransforms();
        var markups = new List<NavMeshBuildMarkup>();
        foreach (NavMeshModifier modifier in level.GetComponentsInChildren<NavMeshModifier>(true))
        {
            if (!modifier.enabled || !modifier.gameObject.activeInHierarchy || !modifier.AffectsAgentType(0)) continue;
            markups.Add(new NavMeshBuildMarkup { root = modifier.transform, ignoreFromBuild = modifier.ignoreFromBuild, overrideArea = modifier.overrideArea, area = modifier.area });
        }
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(level.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentHeight = 1.78f; settings.agentRadius = .36f; settings.agentClimb = .24f; settings.agentSlope = 45;
        settings.overrideVoxelSize = true; settings.voxelSize = .06f; settings.overrideTileSize = true; settings.tileSize = 256; settings.minRegionArea = .7f;
        Bounds bounds = new Bounds(new Vector3(0, 4, 6), new Vector3(38, 18, 74));
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) throw new InvalidOperationException("Story navigation bake failed.");
        data.name = "Night deposit walking routes";
        AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(AssetRoot + "/Navigation/Story Walking Routes.asset"));
        NavMeshSurface surface = systems.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume; surface.center = bounds.center; surface.size = bounds.size;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.overrideVoxelSize = true; surface.voxelSize = .06f;
        surface.navMeshData = data; surface.AddData(); EditorUtility.SetDirty(surface);
        Vector3[] route = { new Vector3(-6,0,-18), new Vector3(0,0,-8), new Vector3(0,0,2.8f), new Vector3(3,0,6.5f), new Vector3(4.5f,0,14), new Vector3(-4,0,14), new Vector3(-4,0,17.6f), new Vector3(0,0,23), new Vector3(0,0,34.5f) };
        var path = new NavMeshPath();
        for (int i = 0; i < route.Length - 1; i++)
        {
            if (!NavMesh.SamplePosition(route[i], out NavMeshHit from, .5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(route[i + 1], out NavMeshHit to, .5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Incomplete story navigation: " + route[i] + " to " + route[i + 1]);
        }
    }

    static void ConfigureBuildSettings(string sandbox, string story)
    {
        var entries = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(sandbox, true), new EditorBuildSettingsScene(story, true) };
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path != sandbox && existing.path != story) entries.Add(new EditorBuildSettingsScene(existing.path, false));
        EditorBuildSettings.scenes = entries.ToArray();
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
