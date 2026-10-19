using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Saved Hellcat asset checks and reproducible photographs of the real mission car.</summary>
public static class FacilityHellcatValidation
{
    const int PreviewLayer = 30;
    const int Width = 1600, Height = 1000;

    public static string Validate(Scene scene)
    {
        Require(scene.IsValid() && scene.isLoaded, "Load the story scene before checking the Hellcat.");
        FacilityStoryDirector director = FindDirector(scene);
        Require(director != null && director.sedanExit != null && director.sedanExit.transform.parent != null,
            "The mission requires its preserved car extraction reference.");
        Transform car = director.sedanExit.transform.parent;
        Camera playerCamera=director.player!=null?director.player.GetComponentInChildren<Camera>(true):null;
        Require(playerCamera!=null && playerCamera.GetUniversalAdditionalCameraData().renderShadows,
            "The gameplay camera must render the car's physical shadows.");
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        Require(pipeline!=null && pipeline.supportsAdditionalLightShadows && pipeline.shadowDistance>10f,
            "The active rendering pipeline must support local-light shadows at parking-lot distance.");
        var errors = new List<string>();
        var meshes = new HashSet<Mesh>();
        var materials = new HashSet<Material>();
        var wheelCenters = new List<Vector3>();
        Bounds bounds = default;
        bool hasBounds = false;
        int curvedMeshes = 0, solidColliders = 0;
        foreach (Transform node in car.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                errors.Add("Missing script on " + node.name + ".");
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        errors.Add("Broken reference " + component.GetType().Name + "." + property.propertyPath + ".");
            }
            foreach (Collider collider in node.GetComponents<Collider>())
                if (collider.enabled && !collider.isTrigger) solidColliders++;
            MeshFilter filter = node.GetComponent<MeshFilter>();
            Renderer renderer = node.GetComponent<Renderer>();
            if (renderer != null)
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || !EditorUtility.IsPersistent(material) || material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
                        errors.Add("Unsaved, missing or unsupported car material on " + node.name + ".");
                    if (material != null) materials.Add(material);
                }
            if (filter == null) continue;
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !EditorUtility.IsPersistent(mesh)) errors.Add("Unsaved or missing car mesh on " + node.name + ".");
            if (mesh == null) continue;
            meshes.Add(mesh);
            Bounds local = TransformBounds(car.worldToLocalMatrix * node.localToWorldMatrix, mesh.bounds);
            if(!node.name.Contains("ambient occlusion"))
            { if (!hasBounds) { bounds = local; hasBounds = true; } else bounds.Encapsulate(local); }
            if (mesh.isReadable && mesh.vertexCount > 80)
            {
                var directions = new HashSet<Vector3Int>();
                foreach (Vector3 normal in mesh.normals)
                    directions.Add(new Vector3Int(Mathf.RoundToInt(normal.x * 12f), Mathf.RoundToInt(normal.y * 12f), Mathf.RoundToInt(normal.z * 12f)));
                if (directions.Count >= 16) curvedMeshes++;
            }
            string label = (node.name + " " + mesh.name).ToLowerInvariant();
            if ((label.Contains("tire") || label.Contains("tyre")) && local.size.y > .45f)
            {
                bool duplicate = false;
                foreach (Vector3 center in wheelCenters) if (Vector3.Distance(center, local.center) < .18f) duplicate = true;
                if (!duplicate) wheelCenters.Add(local.center);
            }
        }
        Check(hasBounds && bounds.size.z > 4.7f && bounds.size.z < 5.5f && bounds.size.x > 1.85f && bounds.size.x < 2.4f && bounds.size.y > 1.3f && bounds.size.y < 1.8f,
            "Hellcat exterior dimensions are inconsistent with its approximately 5.017 x 2.07 x 1.46 m reference: " + bounds.size + ".", errors);
        Check(curvedMeshes > 0, "No detailed curved body/wheel meshes were found; the car must not be assembled solely from boxes.", errors);
        Check(wheelCenters.Count == 4, "Expected four distinct physical tire centers; found " + wheelCenters.Count + ".", errors);
        Check(solidColliders > 0, "The car needs physical body collision.", errors);
        if (wheelCenters.Count == 4)
        {
            wheelCenters.Sort((a, b) => a.z.CompareTo(b.z));
            float wheelbase = ((wheelCenters[2].z + wheelCenters[3].z) - (wheelCenters[0].z + wheelCenters[1].z)) * .5f;
            Check(wheelbase > 2.7f && wheelbase < 3.2f, "Wheelbase differs substantially from the 2.95 m reference: " + wheelbase + ".", errors);
        }
        FacilityStoryInteractable equipment = director.drillPickup;
        Check(equipment != null && equipment.kind == FacilityStoryInteractionKind.Drill && equipment.transform.IsChildOf(car) && equipment.visualRoot != null,
            "The preserved drill/pistol equipment pickup must remain inside the car.", errors);
        if (equipment != null)
        {
            FacilityDoor trunk = equipment.door;
            Check(trunk != null && trunk.transform.IsChildOf(car) && trunk.leaf != null && trunk.leaf.IsChildOf(car) &&
                trunk.interaction != null && trunk.interaction.door == trunk && trunk.interaction.kind == FacilityStoryInteractionKind.Door && !trunk.IsLocked,
                "The equipment pickup needs its original usable trunk hinge, leaf and door interaction.", errors);
            if (trunk != null)
                Check(trunk.leafColliders != null && Array.Exists(trunk.leafColliders, c => c != null && c.enabled && !c.isTrigger && c.transform.IsChildOf(trunk.leaf)),
                    "The moving trunk leaf needs physical collision.", errors);
        }
        ValidateCourtesyLighting(car, equipment, errors);
        string metrics = meshes.Count + " saved meshes, " + materials.Count + " saved materials, " + curvedMeshes + " curved meshes, " + wheelCenters.Count + " tires; exterior " + bounds.size.ToString("F3") + " m.";
        if (errors.Count > 0) throw new InvalidOperationException("Hellcat validation failed. " + metrics + "\n- " + string.Join("\n- ", errors));
        return "Hellcat validation passed. " + metrics;
    }

    static void ValidateCourtesyLighting(Transform car, FacilityStoryInteractable equipment, List<string> errors)
    {
        FacilityVehicleCourtesyLights courtesy = car.GetComponentInChildren<FacilityVehicleCourtesyLights>(true);
        Check(courtesy != null, "The saved car needs its conditional courtesy-light controller.", errors);
        if (courtesy == null) return;
        Check(courtesy.driverDoor != null && courtesy.passengerDoor != null && courtesy.driverDoor != courtesy.passengerDoor &&
            courtesy.trunkDoor != null && equipment != null && courtesy.trunkDoor == equipment.door,
            "Courtesy lighting must reference two separate side doors and the preserved equipment trunk.", errors);
        foreach (FacilityDoor door in new[] { courtesy.driverDoor, courtesy.passengerDoor, courtesy.trunkDoor })
        {
            if (door == null) continue;
            Check(door.transform.IsChildOf(car) && door.leaf != null && door.leaf.IsChildOf(car) && !door.npcCanOpen && !door.IsLocked && !door.IsOpen &&
                door.interaction != null && door.interaction.kind == FacilityStoryInteractionKind.Door && door.interaction.door == door,
                "A car door lacks its saved hinge/interaction or is not stored closed with NPC opening disabled: " + door.name, errors);
            Check(door.leafColliders != null && Array.Exists(door.leafColliders, collider => collider != null && !collider.isTrigger && collider.enabled &&
                collider.transform.IsChildOf(door.leaf)), "A car door has no physical moving-leaf collision: " + door.name, errors);
            Check(door.openLocalEuler.magnitude > 35f, "A car door needs a meaningful opening arc: " + door.name, errors);
        }
        Check(courtesy.cabinLights != null && courtesy.cabinLights.Length > 0 && Array.TrueForAll(courtesy.cabinLights, light => light != null && light.transform.IsChildOf(car)),
            "The car needs assigned physical cabin courtesy lights.", errors);
        Check(courtesy.trunkLight != null && courtesy.trunkLight.transform.IsChildOf(car), "The equipment trunk needs its assigned physical light.", errors);
        Check(courtesy.cabinLampLenses != null && courtesy.cabinLampLenses.Length > 0 && Array.TrueForAll(courtesy.cabinLampLenses, lens => lens != null && lens.transform.IsChildOf(car)) &&
            courtesy.trunkLampLenses != null && courtesy.trunkLampLenses.Length > 0 && Array.TrueForAll(courtesy.trunkLampLenses, lens => lens != null && lens.transform.IsChildOf(car)),
            "Cabin and trunk lights need visible, correctly assigned lamp lenses.", errors);
        var permitted = new HashSet<Light>();
        if (courtesy.cabinLights != null) foreach (Light light in courtesy.cabinLights) if (light != null) permitted.Add(light);
        if (courtesy.trunkLight != null)
        {
            permitted.Add(courtesy.trunkLight);
            Check(Vector3.Dot(courtesy.trunkLight.transform.forward, -car.up) > .5f,
                "The trunk light must point down into the equipment compartment.", errors);
        }
        Light[] carLights = car.GetComponentsInChildren<Light>(true);
        int enabledLights = 0;
        foreach (Light light in carLights)
        {
            if (light.enabled && light.gameObject.activeInHierarchy) enabledLights++;
            string name = light.name.ToLowerInvariant();
            Check(permitted.Contains(light) || name.Contains("headlight") || name.Contains("headlamp") || name.Contains("beam"),
                "A fixed artificial coachwork/fill light remains on the car: " + light.name, errors);
            if (permitted.Contains(light)) Check(!light.enabled && light.intensity > 0f && light.range > .25f,
                "A closed car must store its real courtesy lights switched off with usable light output: " + light.name, errors);
            Check(light.lightmapBakeType == LightmapBakeType.Realtime && light.shadows != LightShadows.None,
                "Real car lamps must support realtime moving shadows: " + light.name, errors);
        }
        Check(carLights.Length == 4 && enabledLights == 2,
            "The closed car must store two enabled headlights and two disabled courtesy lamps; found " + carLights.Length + " lamps / " + enabledLights + " enabled.", errors);
        foreach (ReflectionProbe probe in car.GetComponentsInChildren<ReflectionProbe>(true))
            Check(probe.mode != ReflectionProbeMode.Custom || probe.customBakedTexture == null,
                "The car still carries an artificial local custom reflection cubemap: " + probe.name, errors);
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            string name = material.name.ToLowerInvariant();
            if (!name.Contains("billet silver") && !name.Contains("silver with twin") && !name.Contains("detail silver metallic")) continue;
            Check(!material.IsKeywordEnabled("_EMISSION") && (!material.HasProperty("_EmissionColor") || material.GetColor("_EmissionColor").maxColorComponent < .001f),
                "Body paint must use real lighting without material emission: " + renderer.name, errors);
            Check(renderer.receiveShadows && renderer.shadowCastingMode != ShadowCastingMode.Off,
                "Body paint must cast and receive scene shadows: " + renderer.name, errors);
        }
        bool parkingShadowSource = false;
        foreach (GameObject root in car.gameObject.scene.GetRootGameObjects())
        foreach (Light light in root.GetComponentsInChildren<Light>(true))
        {
            bool parkingFixture = false;
            for (Transform parent = light.transform; parent != null; parent = parent.parent)
                if (parent.name.IndexOf("Parking sodium lamp", StringComparison.OrdinalIgnoreCase) >= 0) parkingFixture = true;
            if (parkingFixture && !light.transform.IsChildOf(car) && light.enabled && light.lightmapBakeType == LightmapBakeType.Mixed &&
                light.shadows != LightShadows.None && (light.cullingMask & (1 << 29)) != 0) parkingShadowSource = true;
        }
        Check(parkingShadowSource, "The actual exterior parking fixture must cast real mixed-light shadows onto the moving car geometry.", errors);
    }

    public static void Capture(Scene scene)
    {
        Require(scene.IsValid() && scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode, "Capture the loaded story in Edit mode.");
        FacilityStoryDirector director = FindDirector(scene);
        Require(director != null && director.sedanExit != null, "The authored mission car is missing.");
        Transform source = director.sedanExit.transform.parent;
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".codex", "hellcat-previews");
        Directory.CreateDirectory(directory);
        Scene activeScene = SceneManager.GetActiveScene();
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture oldActive = RenderTexture.active;
        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Hellcat photograph 1600x1000" };
        target.Create();
        GameObject cameraObject = new GameObject("Temporary Hellcat photography camera") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(cameraObject, preview);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.fieldOfView = 38f; camera.nearClipPlane = .05f; camera.farClipPlane = 150f;
        camera.aspect = (float)Width / Height; camera.allowHDR = true; camera.allowMSAA = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var lighting = new LightingState();
        Material floorMaterial = null;
        try
        {
            // The actual night scene is photographed before introducing any studio lighting.
            camera.cullingMask = Physics.DefaultRaycastLayers;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .022f, .038f);
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            FacilityWeaponInventory arsenal=director.inventory;
            Transform[] weapons=arsenal!=null?new[]{arsenal.fistsModel,arsenal.pistolModel,arsenal.shotgunModel,arsenal.ak47Model}:Array.Empty<Transform>();
            bool[] shown=new bool[weapons.Length];
            try
            {
                for(int i=0;i<weapons.Length;i++)if(weapons[i]!=null){shown[i]=weapons[i].gameObject.activeSelf;weapons[i].gameObject.SetActive(false);}
                Render(camera, target, directory, "05_bank_night", source.TransformPoint(new Vector3(4.6f, 2.1f, 6.5f)), source.TransformPoint(new Vector3(0f, .82f, 0f)));
            }
            finally{for(int i=0;i<weapons.Length;i++)if(weapons[i]!=null)weapons[i].gameObject.SetActive(shown[i]);}

            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject);
            copy.name = "Photograph of the saved Hellcat";
            SceneManager.MoveGameObjectToScene(copy, preview);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (AudioSource audio in copy.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (Transform node in copy.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightmapIndex = -1; renderer.realtimeLightmapIndex = -1;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            foreach (ReflectionProbe probe in copy.GetComponentsInChildren<ReflectionProbe>(true))
            {
                if (probe.texture != null) { Texture reflection = probe.texture; probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = reflection; }
            }
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Temporary studio floor";
            SceneManager.MoveGameObjectToScene(floor, preview);
            floor.layer = PreviewLayer; floor.transform.position = new Vector3(0f, -.045f, 0f); floor.transform.localScale = new Vector3(120f, .08f, 120f);
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.SetColor("_BaseColor", new Color(.35f, .37f, .4f)); floorMaterial.SetFloat("_Smoothness", .26f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            RenderSettings.fog = false; RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.8f, .86f, 1f); RenderSettings.ambientEquatorColor = new Color(.58f, .63f, .7f); RenderSettings.ambientGroundColor = new Color(.26f, .28f, .32f);
            RenderSettings.ambientIntensity = 1.1f;
            var ambientProbe = new SphericalHarmonicsL2(); ambientProbe.AddAmbientLight(new Color(.5f, .54f, .62f));
            RenderSettings.ambientProbe = ambientProbe;
            StudioLight(preview, "Studio sun", LightType.Directional, new Vector3(40f, -35f, 0f), Vector3.zero, new Color(1f, .93f, .84f), 2f);
            StudioLight(preview, "Studio broad fill", LightType.Directional, new Vector3(60f, 140f, 0f), Vector3.zero, new Color(.7f, .8f, 1f), 1.1f);
            StudioLight(preview, "Studio roof highlight", LightType.Point, Vector3.zero, new Vector3(0f, 5f, -1f), Color.white, 35f);
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.cullingMask = 1 << PreviewLayer;
            camera.backgroundColor = new Color(.4f, .43f, .48f);
            camera.fieldOfView = 34f;
            Render(camera, target, directory, "01_front_three_quarter", new Vector3(3.9f, 1.60f, 5.7f), new Vector3(0f, .78f, .05f));
            camera.fieldOfView = 31f;
            Render(camera, target, directory, "02_side_profile", new Vector3(7.7f, 1.35f, .15f), new Vector3(0f, .73f, 0f));
            camera.fieldOfView = 34f;
            Render(camera, target, directory, "03_rear_three_quarter", new Vector3(-3.7f, 1.6f, -5.6f), new Vector3(0f, .76f, -.15f));
            camera.fieldOfView = 42f;
            Render(camera, target, directory, "04_front_detail", new Vector3(1.7f, 1.22f, 4.7f), new Vector3(0f, .77f, 1.25f));
        }
        finally
        {
            lighting.Restore(); RenderTexture.active = oldActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
            EditorSceneManager.ClosePreviewScene(preview);
            if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
        }
        Debug.Log("Hellcat photographs saved to " + directory);
    }

    static void StudioLight(Scene scene, string name, LightType type, Vector3 euler, Vector3 position, Color color, float intensity)
    {
        var item = new GameObject(name); SceneManager.MoveGameObjectToScene(item, scene);
        item.transform.SetPositionAndRotation(position, Quaternion.Euler(euler)); item.layer = PreviewLayer;
        Light light = item.AddComponent<Light>(); light.type = type; light.color = color; light.intensity = intensity;
        light.cullingMask = 1 << PreviewLayer; light.range = 14f; light.shadows = type == LightType.Directional && intensity > 1.5f ? LightShadows.Soft : LightShadows.None;
    }
    static void Render(Camera camera, RenderTexture target, string directory, string name, Vector3 eye, Vector3 aim)
    {
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try { pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(pixels); }
    }
    static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        var result = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
        for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return result;
    }
    static FacilityStoryDirector FindDirector(Scene scene)
    { foreach (GameObject root in scene.GetRootGameObjects()) { var found = root.GetComponentInChildren<FacilityStoryDirector>(true); if (found != null) return found; } return null; }
    static void Check(bool condition, string error, List<string> errors) { if (!condition) errors.Add(error); }
    static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }

    sealed class LightingState
    {
        readonly bool fog = RenderSettings.fog;
        readonly Material sky = RenderSettings.skybox;
        readonly AmbientMode ambient = RenderSettings.ambientMode;
        readonly Color ambientLight = RenderSettings.ambientLight, skyColor = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
        readonly float intensity = RenderSettings.ambientIntensity;
        readonly SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
        public void Restore()
        {
            RenderSettings.fog = fog; RenderSettings.skybox = sky; RenderSettings.ambientMode = ambient;
            RenderSettings.ambientSkyColor = skyColor; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientLight = ambientLight; RenderSettings.ambientIntensity = intensity;
            RenderSettings.ambientProbe = probe;
        }
    }
}
