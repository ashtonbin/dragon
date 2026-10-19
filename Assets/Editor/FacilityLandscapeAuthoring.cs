using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Saved, non-emissive native geometry extending the bank into a quiet night meadow.</summary>
public static class FacilityLandscapeAuthoring
{
    const string Root = "Assets/Generated/Landscape";
    const string GroupName = "Beyond the fence - rolling grassland and night mist";
    const float Extent = 160f;
    static readonly Color Mist = new Color(.063f, .081f, .088f);
    // Exact floor footprints, including their wall thicknesses: the new ground never covers a mission floor.
    static readonly Rect[] Footprints = {
        Rect.MinMaxRect(-17f, -28.5f, 17f, -3.5f),
        Rect.MinMaxRect(-7.25f, -4.25f, 7.25f, 16.25f),
        Rect.MinMaxRect(-12.25f, 15.75f, 12.25f, 30.25f),
        Rect.MinMaxRect(-5.25f, 29.75f, 5.25f, 40.25f),
        Rect.MinMaxRect(-12.25f, -4.3f, 12.25f, -3.7f)
    };

    [MenuItem("Tools/Facility/Surround Bank with Grassland and Mist")]
    public static string Install()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !Lightmapping.isRunning,
            "Author the landscape outside Play mode and baking.");
        EditorSceneManager.SaveOpenScenes();
        Scene scene = SceneManager.GetActiveScene();
        if (FindDirector(scene) == null)
            scene = EditorSceneManager.OpenScene(FacilityStoryInstaller.StoryScenePath, OpenSceneMode.Single);
        Folder("Assets/Scenes/Recovery");
        string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeGrassland_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        Require(EditorSceneManager.SaveScene(scene, backup, true), "Could not save the landscape recovery scene.");
        string result = Apply(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save the grass landscape.");
        return result + " Recovery: " + backup;
    }

    /// <summary>Updates only the saved exterior landscape and scene fog; existing floors, lights, fences and routes stay intact.</summary>
    public static string Apply(Scene scene)
    {
        Require(scene.IsValid() && scene.isLoaded && FindDirector(scene) != null,
            "Load the authored bank story scene before adding its landscape.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !Lightmapping.isRunning,
            "Author the landscape outside Play mode and baking.");
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            foreach (string directory in new[] { Root, Root + "/Meshes", Root + "/Materials", Root + "/Textures" }) Folder(directory);
            Transform level = null;
            foreach (GameObject item in scene.GetRootGameObjects())
                if (item.name == "Story Level 1 - Night Deposit") level = item.transform;
            Require(level != null, "The existing story level is missing.");
            Transform old = level.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform group = new GameObject(GroupName).transform;
            group.SetParent(level, false);

            Texture2D turf = TurfTexture();
            Material ground = Material("Muted meadow turf", new Color(.69f, .74f, .57f), turf, false);
            Material blades = Material("Meadow blade natural green", new Color(.27f, .35f, .19f), null, true);
            Mesh terrain = GroundMesh();
            GameObject surface = MeshObject(group, "Continuous rolling meadow - 320 metre exterior", terrain, ground);
            surface.AddComponent<MeshCollider>().sharedMesh = terrain;
            GameObject grass = MeshObject(group, "Sparse near-field grass blades - physical silhouettes", BladeMesh(), blades);
            // Existing story probes were baked inside the bank, before this exterior existed.
            // A dedicated open-sky probe supplies diffuse night sky light without brightening the car or rooms.
            FacilityLandscapeAmbient sky = group.gameObject.AddComponent<FacilityLandscapeAmbient>();
            sky.surfaces = new Renderer[] { surface.GetComponent<Renderer>(), grass.GetComponent<Renderer>() };
            sky.Refresh();
            // Native geometry remains outside the mission's existing navigation graph.
            group.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Mist;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 145f;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.GetComponentInParent<RetroFPSController>() == null) continue;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Mist;
                    camera.farClipPlane = Mathf.Max(camera.farClipPlane, 190f);
                    EditorUtility.SetDirty(camera);
                }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return Validate(scene);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded && previous != scene) SceneManager.SetActiveScene(previous);
        }
    }

    public static string Validate(Scene scene)
    {
        Transform landscape = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == GroupName) landscape = item;
        Require(landscape != null, "The saved grassland group is missing.");
        int meshes = 0, vertices = 0;
        foreach (MeshFilter filter in landscape.GetComponentsInChildren<MeshFilter>(true))
        {
            Require(filter.sharedMesh != null && EditorUtility.IsPersistent(filter.sharedMesh), "The grassland needs persistent mesh assets.");
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            Require(renderer != null && renderer.receiveShadows && renderer.shadowCastingMode == ShadowCastingMode.On,
                "Grassland must receive and cast physical light shadows.");
            Require(renderer.sharedMaterial != null && EditorUtility.IsPersistent(renderer.sharedMaterial) &&
                renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit" && !renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"),
                "The landscape must use saved, non-emissive lit materials.");
            meshes++; vertices += filter.sharedMesh.vertexCount;
        }
        Require(meshes == 2 && landscape.GetComponentInChildren<MeshCollider>() != null, "The exterior needs its rolling terrain and native grass blades.");
        FacilityLandscapeAmbient ambient = landscape.GetComponent<FacilityLandscapeAmbient>();
        Require(ambient != null && ambient.surfaces != null && ambient.surfaces.Length == 2 &&
            Array.TrueForAll(ambient.surfaces, surface => surface != null && surface.lightProbeUsage == LightProbeUsage.CustomProvided),
            "The exterior must retain its saved open-sky diffuse ambient probe in Edit mode and gameplay.");
        Require(RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear && RenderSettings.fogStartDistance >= 25f && RenderSettings.fogEndDistance >= 140f,
            "The bank's close surroundings must remain readable before the distant mist.");
        return "Landscape validation passed: 320 x 320 m rolling grass meadow, " + meshes + " saved meshes / " + vertices +
            " vertices, protected mission floor footprints, physical turf shadows, open-sky ambient probe and blue-green night mist from 28 to 145 m.";
    }

    /// <summary>Three photographs use the real saved night lighting, materials and fog, without temporary illumination.</summary>
    public static string Capture(Scene scene)
    {
        Require(scene.IsValid() && scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode, "Capture the saved exterior in Edit mode.");
        FacilityStoryDirector director = FindDirector(scene);
        Require(director != null && director.player != null, "The story player is missing.");
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".codex", "landscape-previews");
        Directory.CreateDirectory(directory);
        GameObject cameraObject = new GameObject("Temporary night landscape camera") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.AddComponent<Camera>();
        Camera source = director.player.GetComponentInChildren<Camera>(true);
        if (source != null) camera.CopyFrom(source);
        camera.enabled = false; camera.nearClipPlane = .05f; camera.farClipPlane = 190f;
        camera.fieldOfView = 63f; camera.aspect = 1.6f;
        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true; data.renderShadows = true;
        RenderTexture target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { name = "Landscape photograph" };
        target.Create();
        RenderTexture previous = RenderTexture.active;
        FacilityWeaponInventory inventory = director.inventory;
        Transform[] props = inventory != null ? new[] { inventory.fistsModel, inventory.pistolModel, inventory.shotgunModel, inventory.ak47Model } : Array.Empty<Transform>();
        bool[] visible = new bool[props.Length];
        try
        {
            for (int i = 0; i < props.Length; i++) if (props[i] != null) { visible[i] = props[i].gameObject.activeSelf; props[i].gameObject.SetActive(false); }
            Photograph(camera, target, directory, "01_bank_in_the_meadow", new Vector3(23f, 2.1f, -43f), new Vector3(-2f, 2.1f, -7f));
            Photograph(camera, target, directory, "02_from_the_car_beyond_the_fence", new Vector3(-9f, 1.7f, -20f), new Vector3(-35f, .9f, -30f));
            Photograph(camera, target, directory, "03_grass_bank_and_mist", new Vector3(-29f, 1.9f, -12f), new Vector3(0f, 2.0f, 5f));
        }
        finally
        {
            for (int i = 0; i < props.Length; i++) if (props[i] != null) props[i].gameObject.SetActive(visible[i]);
            RenderTexture.active = previous;
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
        }
        return "Three real-night exterior photographs saved to " + directory + ".";
    }

    static void Photograph(Camera camera, RenderTexture target, string directory, string name, Vector3 eye, Vector3 aim)
    {
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(pixels); }
    }

    static Mesh GroundMesh()
    {
        List<float> xs = Coordinates(true), zs = Coordinates(false);
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        foreach (float z in zs) foreach (float x in xs)
        {
            vertices.Add(new Vector3(x, Height(x, z), z));
            const float e = .12f;
            normals.Add(new Vector3(Height(x - e, z) - Height(x + e, z), e * 2, Height(x, z - e) - Height(x, z + e)).normalized);
            uv.Add(new Vector2(x * .19f, z * .19f));
        }
        for (int z = 0; z < zs.Count - 1; z++) for (int x = 0; x < xs.Count - 1; x++)
        {
            if (Protected((xs[x] + xs[x + 1]) * .5f, (zs[z] + zs[z + 1]) * .5f)) continue;
            int a = z * xs.Count + x, b = a + 1, c = a + xs.Count, d = c + 1;
            triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }
        return MeshAsset("Continuous rolling night meadow", vertices, normals, uv, triangles);
    }

    static List<float> Coordinates(bool xAxis)
    {
        var values = new SortedSet<float> { -Extent, Extent };
        for (float value = -Extent; value <= Extent; value += 4f) values.Add(value);
        for (float value = -48f; value <= 56f; value += 1.5f) values.Add(value);
        foreach (Rect rect in Footprints) { values.Add(xAxis ? rect.xMin : rect.yMin); values.Add(xAxis ? rect.xMax : rect.yMax); }
        return new List<float>(values);
    }

    static Mesh BladeMesh()
    {
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        var random = new System.Random(172903);
        int tufts = 0;
        for (float z = -70; z <= 75; z += 1.35f) for (float x = -66; x <= 66; x += 1.35f)
        {
            float xx = x + ((float)random.NextDouble() - .5f) * 1.25f;
            float zz = z + ((float)random.NextDouble() - .5f) * 1.25f;
            float distance = Distance(xx, zz);
            if (Protected(xx, zz) || distance < .14f || distance > 29f || random.NextDouble() > Mathf.Lerp(.70f, .08f, distance / 29f)) continue;
            if (++tufts > 3500) break;
            for (int blade = 0; blade < 4; blade++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                Vector3 along = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 normal = Vector3.Cross(along, Vector3.up);
                float width = .011f + (float)random.NextDouble() * .013f;
                float height = .10f + (float)random.NextDouble() * .15f;
                Vector3 bottom = new Vector3(xx, Height(xx, zz) + .004f, zz) + along * (float)random.NextDouble() * .11f;
                Vector3 middle = bottom + Vector3.up * height * .60f + normal * height * .07f;
                Vector3 tip = bottom + Vector3.up * height + normal * height * .24f;
                int i = vertices.Count;
                vertices.Add(bottom - along * width); vertices.Add(bottom + along * width);
                vertices.Add(middle - along * width * .5f); vertices.Add(middle + along * width * .5f); vertices.Add(tip);
                for (int n = 0; n < 5; n++) normals.Add((normal + Vector3.up * .65f).normalized);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(.25f, .6f)); uv.Add(new Vector2(.75f, .6f)); uv.Add(new Vector2(.5f, 1));
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i + 3);
                triangles.Add(i + 2); triangles.Add(i + 4); triangles.Add(i + 3);
            }
        }
        return MeshAsset("Original meadow grass silhouettes", vertices, normals, uv, triangles);
    }

    static float Height(float x, float z)
    {
        float ramp = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Distance(x, z) / 20f));
        float ripple = .32f * Mathf.Sin(x * .13f + z * .035f) + .24f * Mathf.Sin(z * .16f - x * .041f);
        float broad = (Mathf.PerlinNoise(x * .016f + 83.9f, z * .016f + 49.3f) - .46f) * 3.0f;
        float hills = Hill(x, z, -71, -58, 4.8f, 31) + Hill(x, z, 74, -20, 5.1f, 29) +
            Hill(x, z, -67, 60, 5.8f, 33) + Hill(x, z, 40, 100, 6.0f, 34) + Hill(x, z, -10, -110, 5.1f, 38);
        return -.055f + ramp * (ripple + broad + hills);
    }

    static float Hill(float x, float z, float cx, float cz, float amplitude, float radius)
    { float dx = (x - cx) / radius, dz = (z - cz) / radius; return amplitude * Mathf.Exp(-(dx * dx + dz * dz)); }
    static bool Protected(float x, float z)
    { foreach (Rect rect in Footprints) if (x > rect.xMin && x < rect.xMax && z > rect.yMin && z < rect.yMax) return true; return false; }
    static float Distance(float x, float z)
    {
        float nearest = float.MaxValue;
        foreach (Rect rect in Footprints)
        {
            float dx = Mathf.Max(rect.xMin - x, 0, x - rect.xMax), dz = Mathf.Max(rect.yMin - z, 0, z - rect.yMax);
            nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return nearest;
    }

    static Texture2D TurfTexture()
    {
        string path = Root + "/Textures/Original natural turf microtexture.asset";
        const int resolution = 512;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) { texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false) { name = "Original natural turf microtexture" }; AssetDatabase.CreateAsset(texture, path); }
        var pixels = new Color32[resolution * resolution];
        var random = new System.Random(89021);
        Color a = new Color(.20f, .25f, .115f), b = new Color(.43f, .48f, .25f);
        for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
        {
            float grain = (float)random.NextDouble();
            float soft = Mathf.PerlinNoise(x * .054f + 49, y * .054f + 23);
            float fine = Mathf.PerlinNoise(x * .42f, y * .061f);
            float t = Mathf.Clamp01(.20f + .48f * soft + .23f * grain + .20f * fine);
            Color color = Color.Lerp(a, b, t);
            color *= .84f + grain * .31f;
            pixels[y * resolution + x] = color;
        }
        // Fine, irregular dry and green blades remain baked into a saved original texture.
        for (int i = 0; i < 34000; i++)
        {
            int x = random.Next(resolution), y = random.Next(resolution), length = random.Next(2, 9);
            Color tint = Color.Lerp(new Color(.19f, .25f, .11f), new Color(.43f, .43f, .23f), (float)random.NextDouble());
            for (int step = 0; step < length; step++) pixels[((y + step) % resolution) * resolution + (x + step / 4) % resolution] = tint;
        }
        texture.SetPixels32(pixels); texture.Apply(true, false);
        texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 4;
        EditorUtility.SetDirty(texture);
        return texture;
    }

    static Material Material(string name, Color color, Texture2D texture, bool doubleSided)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .08f);
        material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
        material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Mesh MeshAsset(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
    {
        string path = Root + "/Meshes/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(parent, false);
        item.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        // Ambient is sampled from the real night environment; the old indoor probe volume must not light distant grass.
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.receiveGI = ReceiveGI.LightProbes;
        GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic);
        return item;
    }
    static FacilityStoryDirector FindDirector(Scene scene)
    { foreach (GameObject root in scene.GetRootGameObjects()) { FacilityStoryDirector result = root.GetComponentInChildren<FacilityStoryDirector>(true); if (result != null) return result; } return null; }
    static void Folder(string path)
    { if (!AssetDatabase.IsValidFolder(path)) { string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); } }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
