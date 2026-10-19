using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Saved, editable industrial magnetic drill. No borrowed game meshes or generated runtime geometry.</summary>
public static class FacilityDrillAuthoring
{
    const string Root = "Assets/Generated/Heist";
    static FacilityAssetLibrary library;

    public static GameObject Build(Transform parent, string name)
    {
        EnsureFolders();
        library = library ?? new FacilityAssetLibrary();
        var root = new GameObject(name); root.transform.SetParent(parent, false);
        Material enamel = Material("Drill chipped ochre enamel", new Color(.65f, .39f, .075f), true);
        Material rubber = Material("Drill black rubber", new Color(.055f, .065f, .065f));
        Material steel = Material("Drill brushed tool steel", new Color(.40f, .44f, .45f));
        Material iron = Material("Drill dark cast iron", new Color(.12f, .15f, .16f));
        Material brass = Material("Drill worn cutting steel", new Color(.39f, .30f, .17f));
        Transform t = root.transform;
        Box(t, "Magnetic foot - contacts mounting surface", new Vector3(0, .0375f, 0), new Vector3(.31f, .075f, .39f), iron);
        Box(t, "Foot rubber perimeter", new Vector3(0, .077f, -.035f), new Vector3(.285f, .018f, .30f), rubber);
        Box(t, "Cast upright bracket", new Vector3(0, .28f, -.125f), new Vector3(.145f, .40f, .10f), iron);
        Box(t, "Chrome rack guide", new Vector3(0, .32f, -.064f), new Vector3(.055f, .43f, .022f), steel);
        for (int i = 0; i < 12; i++)
            Box(t, "Feed rack tooth", new Vector3(0, .15f + .029f * i, -.046f), new Vector3(.064f, .009f, .014f), iron);
        Box(t, "Motor mounting carriage", new Vector3(0, .35f, .018f), new Vector3(.19f, .18f, .105f), steel);
        Cylinder(t, "Vertical motor casing", new Vector3(0, .45f, .135f), .115f, .30f, enamel);
        Cylinder(t, "Rear motor cap", new Vector3(0, .608f, .135f), .112f, .035f, rubber);
        Cylinder(t, "Cast gear housing", new Vector3(0, .272f, .135f), .097f, .07f, iron);
        Cylinder(t, "Steel spindle chuck", new Vector3(0, .210f, .135f), .042f, .065f, steel);
        Cylinder(t, "Sliding steel quill", new Vector3(0, .2625f, .135f), .028f, .175f, steel);
        Cylinder(t, "Spindle collar", new Vector3(0, .245f, .135f), .059f, .024f, steel);
        Cylinder(t, "Cutting bit shaft", new Vector3(0, .129f, .135f), .0175f, .105f, brass);
        FlutedBit(t, brass);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
                Box(t, "Recessed motor cooling vent", new Vector3(side * .108f, .465f + i * .024f, .135f), new Vector3(.012f, .010f, .093f), rubber);
            Box(t, "Carry handle upright", new Vector3(side * .105f, .65f, -.03f), new Vector3(.028f, .12f, .035f), iron);
        }
        Box(t, "Carry handle rubber grip", new Vector3(0, .705f, -.03f), new Vector3(.25f, .035f, .045f), rubber);
        Box(t, "Magnet power switch block", new Vector3(.105f, .15f, -.145f), new Vector3(.09f, .10f, .055f), enamel);
        Box(t, "Inset red power rocker", new Vector3(.105f, .169f, -.176f), new Vector3(.041f, .034f, .008f), Material("Drill red switch", new Color(.45f, .055f, .035f)));
        Transform crank = Group(t, "Three arm rack feed crank"); crank.localPosition = new Vector3(.13f, .35f, -.05f);
        Cylinder(crank, "Feed crank hub", Vector3.zero, .041f, .065f, steel).localRotation = Quaternion.Euler(0, 0, 90);
        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120 + 22) * Mathf.Deg2Rad;
            Vector3 end = new Vector3(.025f, Mathf.Sin(angle) * .155f, Mathf.Cos(angle) * .155f);
            Rod(crank, "Feed lever spoke", new Vector3(.025f, 0, 0), end, .009f, steel);
            Cylinder(crank, "Rubber feed knob", end, .021f, .055f, rubber).localRotation = Quaternion.Euler(0, 0, 90);
        }
        Vector3[] cord = { new Vector3(-.09f, .51f, .17f), new Vector3(-.17f, .44f, .15f), new Vector3(-.20f, .25f, .02f), new Vector3(-.19f, .13f, -.12f), new Vector3(-.10f, .11f, -.17f) };
        for (int i = 0; i < cord.Length - 1; i++) Rod(t, "Attached power cable", cord[i], cord[i + 1], .008f, rubber);
        for (int side = -1; side <= 1; side += 2)
            for (int end = -1; end <= 1; end += 2)
                Cylinder(t, "Recessed hex base bolt", new Vector3(side * .118f, .082f, end * .143f), .012f, .009f, steel, 6);
        Material label = LabelMaterial();
        Box(t, "Stamped MD-45 serial plate", new Vector3(0, .438f, .253f), new Vector3(.15f, .046f, .006f), label);
        // Moving equipment must receive probes rather than being frozen into the world lightmap.
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        }
        return root;
    }

    /// <summary>Retrofit a previously saved drill without replacing its painted mesh or hand-edited parts.</summary>
    public static FacilityDrillMotion ConfigureMotion(GameObject model, FacilityVaultDrill drilling)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        EnsureFolders(); library = library ?? new FacilityAssetLibrary();
        Transform root = model.transform;
        if (root.Find("Sliding steel quill") == null)
            Cylinder(root, "Sliding steel quill", new Vector3(0, .2625f, .135f), .028f, .175f, Material("Drill brushed tool steel", new Color(.40f, .44f, .45f)));
        Transform fluteAxis = root.Find("Cutting flute spin axis");
        if (fluteAxis == null)
        {
            Transform flute = root.Find("Twin helical cutting flutes");
            if (flute == null) throw new InvalidOperationException("The drill's saved cutting flutes are missing.");
            fluteAxis = Group(root, "Cutting flute spin axis"); fluteAxis.localPosition = new Vector3(0, 0, .135f);
            // The existing mesh vertices include the spindle's Z offset. A real axle keeps them from orbiting the foot.
            flute.SetParent(fluteAxis, true);
        }
        FacilityDrillMotion motion = model.GetComponent<FacilityDrillMotion>();
        if (motion == null) motion = model.AddComponent<FacilityDrillMotion>();
        motion.drilling = drilling;
        motion.feedParts = new[] { root.Find("Sliding steel quill"), root.Find("Steel spindle chuck"), root.Find("Cutting bit shaft"), fluteAxis };
        motion.spinningParts = new[] { root.Find("Steel spindle chuck"), root.Find("Cutting bit shaft"), fluteAxis };
        motion.feedCrank = root.Find("Three arm rack feed crank"); motion.feedDistance = .075f; motion.feedSeconds = .65f; motion.revolutionsPerMinute = 780f;
        EditorUtility.SetDirty(motion);
        return motion;
    }

    /// <summary>Keep the small moving tool readable in the night scene without adding illumination.</summary>
    public static void ImproveReadability()
    {
        EnsureFolders();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material != null && material.name.StartsWith("Drill ", StringComparison.Ordinal))
                Readability(material, material.name == "Drill chipped ochre enamel" ? .10f : .08f);
        }
    }

    static void Readability(Material material, float amount)
    {
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
        material.SetTextureScale("_EmissionMap", material.GetTextureScale("_BaseMap"));
        material.SetTextureOffset("_EmissionMap", material.GetTextureOffset("_BaseMap"));
        material.SetColor("_EmissionColor", material.GetColor("_BaseColor") * amount);
        // Realtime GI is disabled in the project: this is a restrained visual floor, not an extra light.
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
    }

    public static AudioClip DrillLoop()
    {
        EnsureFolders(); string path = Root + "/Audio/Magnetic drill motor.wav";
        AudioClip saved = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (saved != null) return saved;
        const int rate = 22050, seconds = 4; float[] data = new float[rate * seconds];
        var random = new System.Random(4516); float lowNoise = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float time = i / (float)rate;
            lowNoise = Mathf.Lerp(lowNoise, (float)random.NextDouble() * 2 - 1, .11f);
            float loading = .8f + .12f * Mathf.Sin(time * Mathf.PI * 2 * 2);
            data[i] = loading * (.27f * Mathf.Sin(time * Mathf.PI * 2 * 85) + .13f * Mathf.Sin(time * Mathf.PI * 2 * 170) + .07f * Mathf.Sin(time * Mathf.PI * 2 * 425) + .08f * lowNoise);
        }
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + data.Length * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(data.Length * 2);
            foreach (float sample in data) writer.Write((short)(Mathf.Clamp(sample, -.95f, .95f) * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void FlutedBit(Transform parent, Material material)
    {
        string path = Root + "/Meshes/Spiral cutting flutes.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (int flute = 0; flute < 2; flute++) for (int step = 0; step < 16; step++)
            {
                float a = step / 16f * Mathf.PI * 4 + flute * Mathf.PI;
                float b = (step + 1) / 16f * Mathf.PI * 4 + flute * Mathf.PI;
                float y0 = .075f + step / 16f * .10f, y1 = .075f + (step + 1) / 16f * .10f;
                int start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a) * .023f, y0, .135f + Mathf.Sin(a) * .023f));
                vertices.Add(new Vector3(Mathf.Cos(b) * .023f, y1, .135f + Mathf.Sin(b) * .023f));
                vertices.Add(new Vector3(Mathf.Cos(b + .42f) * .023f, y1, .135f + Mathf.Sin(b + .42f) * .023f));
                vertices.Add(new Vector3(Mathf.Cos(a + .42f) * .023f, y0, .135f + Mathf.Sin(a + .42f) * .023f));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            }
            mesh = new Mesh { name = "Spiral cutting flutes" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds(); Unwrapping.GenerateSecondaryUVSet(mesh); AssetDatabase.CreateAsset(mesh, path);
        }
        MeshPart(parent, "Twin helical cutting flutes", Vector3.zero, mesh, material);
    }

    static Material LabelMaterial()
    {
        string path = Root + "/Materials/Drill serial plate.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) { Readability(existing, .08f); return existing; }
        Texture2D texture = new Texture2D(64, 32, TextureFormat.RGBA32, false) { name = "MD-45 equipment plate", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32 paper = new Color32(193, 184, 146, 255), ink = new Color32(28, 35, 34, 255);
        var pixels = new Color32[64 * 32]; for (int i = 0; i < pixels.Length; i++) pixels[i] = paper;
        string[] patterns = { "100011101011100111101111", "110110101000100100001000", "101010101001100111101110", "100010101000100000100010", "100011101011100111101110" };
        // A small stamped equipment identifier, not a sign naming the room.
        for (int y = 0; y < 5; y++) for (int x = 0; x < patterns[y].Length; x++) if (patterns[y][x] == '1')
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) pixels[(22 - y * 2 + dy) * 64 + 8 + x * 2 + dx] = ink;
        for (int y = 7; y < 10; y++) for (int x = 8; x < 56; x++) if (x % 4 != 0) pixels[y * 64 + x] = ink;
        texture.SetPixels32(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, Root + "/Textures/MD45 equipment plate.asset");
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Drill serial plate" }; material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", new Vector2(1f / .15f, 1f / .046f)); material.SetFloat("_Smoothness", .12f); Readability(material, .08f); AssetDatabase.CreateAsset(material, path); return material;
    }

    static Material Material(string name, Color color, bool painted = false)
    {
        EnsureFolders(); string path = Root + "/Materials/" + name + ".mat";
        Material saved = AssetDatabase.LoadAssetAtPath<Material>(path); if (saved != null) { Readability(saved, painted ? .10f : .08f); return saved; }
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", painted ? Color.white : color); material.SetFloat("_Metallic", painted ? .12f : .3f); material.SetFloat("_Smoothness", painted ? .16f : .2f);
        if (painted)
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = name + " 64px", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            Color32[] pixels = new Color32[64 * 64]; var random = new System.Random(4519);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float wear = (float)random.NextDouble(); float shade = .93f + wear * .12f;
                bool edge = x < 2 || y < 2 || x > 61 || y > 61;
                bool chip = edge && wear < .55f || ((x * 17 + y * 37) % 181 == 0);
                pixels[y * 64 + x] = chip ? new Color(.23f, .25f, .24f) : color * shade;
            }
            texture.SetPixels32(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, Root + "/Textures/" + name + ".asset"); material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", new Vector2(4, 4));
        }
        Readability(material, painted ? .10f : .08f); AssetDatabase.CreateAsset(material, path); return material;
    }

    static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material) => MeshPart(parent, name, position, library.BoxMesh(size), material);
    static Transform Cylinder(Transform parent, string name, Vector3 position, float radius, float height, Material material, int sides = 12)
    {
        string key = radius.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "_" + height.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "_" + sides;
        string path = Root + "/Meshes/Cylinder_" + key + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides; int n = vertices.Count;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), q = new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
                vertices.AddRange(new[] { p + Vector3.down * height * .5f, p + Vector3.up * height * .5f, q + Vector3.up * height * .5f, q + Vector3.down * height * .5f, Vector3.up * height * .5f, Vector3.down * height * .5f });
                triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3, n + 1, n + 4, n + 2, n + 3, n + 5, n });
                uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector2.one * .5f, Vector2.one * .5f });
            }
            mesh = new Mesh { name = "Drill cylinder " + key }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds(); Unwrapping.GenerateSecondaryUVSet(mesh); AssetDatabase.CreateAsset(mesh, path);
        }
        return MeshPart(parent, name, position, mesh, material);
    }
    static void Rod(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
    { Transform rod = Cylinder(parent, name, (from + to) * .5f, radius, Vector3.Distance(from, to), material, 8); rod.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized); }
    static Transform MeshPart(Transform parent, string name, Vector3 position, Mesh mesh, Material material)
    {
        Transform part = Group(parent, name); part.localPosition = position; part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material; return part;
    }
    static Transform Group(Transform parent, string name) { var part = new GameObject(name); part.transform.SetParent(parent, false); return part.transform; }
    static void EnsureFolders()
    {
        foreach (string path in new[] { Root, Root + "/Meshes", Root + "/Materials", Root + "/Textures", Root + "/Audio" })
        { if (!AssetDatabase.IsValidFolder(path)) { string parent = Path.GetDirectoryName(path).Replace('\\', '/'); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); } }
    }
}
