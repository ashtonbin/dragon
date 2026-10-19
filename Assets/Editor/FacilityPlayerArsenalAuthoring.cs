using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Editor-only authoring of the player's additional weapons and persistent resources.</summary>
public static class FacilityPlayerArsenalAuthoring
{
    public const string AssetRoot = "Assets/Generated/Arsenal";

    public static FacilityWeaponInventory Build(RetroFPSController player)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        EnsureFolder(AssetRoot);
        foreach (string folder in new[] { "Meshes", "Materials", "Audio" }) EnsureFolder(AssetRoot + "/" + folder);
        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera == null) throw new InvalidOperationException("The existing player has no camera.");
        FacilityShotgun shotgun = camera.GetComponent<FacilityShotgun>();
        if (shotgun == null || shotgun.muzzle == null) shotgun = ShotgunModelAuthoring.Build(camera);
        RetroViewmodel motion = camera.GetComponent<RetroViewmodel>();
        FacilityWeaponInventory inventory = player.GetComponent<FacilityWeaponInventory>();
        if (inventory == null) inventory = player.gameObject.AddComponent<FacilityWeaponInventory>();
        Transform shotgunRoot = shotgun.muzzle;
        while (shotgunRoot != null && shotgunRoot.parent != camera.transform) shotgunRoot = shotgunRoot.parent;
        DestroyNamed(camera.transform, "Service Pistol");
        DestroyNamed(camera.transform, "Unarmed Hands");

        GameObject pistolRoot = Model(camera.transform, "Service Pistol");
        BuildPistolGeometry(pistolRoot.transform, true, out Transform muzzle, out Transform slide);
        pistolRoot.transform.localPosition = new Vector3(.22f, -.24f, .46f);
        pistolRoot.transform.localRotation = Quaternion.Euler(0f, -3f, 0f);
        pistolRoot.transform.localScale = Vector3.one * 1.3f;
        FacilityPistol pistol = camera.GetComponent<FacilityPistol>();
        if (pistol == null) pistol = camera.gameObject.AddComponent<FacilityPistol>();
        pistol.aimCamera = camera;
        pistol.inventory = inventory;
        pistol.viewmodel = motion;
        pistol.muzzle = muzzle;
        pistol.slide = slide;
        pistol.fireInterval = .26f;
        pistol.surfaceImpacts = shotgun.surfaceImpacts;
        GameObject flash = Piece(muzzle, "Pistol pressure flash", Flash(), Material("Pistol Flame", new Color(1f, .65f, .18f), true));
        flash.transform.localScale = Vector3.one * .75f;
        Transform core = Piece(flash.transform, "White flame core", Flash(), Material("Pistol Flame Core", new Color(1f, .95f, .76f), true)).transform;
        core.localScale = new Vector3(.43f, .43f, .65f);
        pistol.muzzleFlash = flash;
        flash.SetActive(false);
        GameObject light = Model(muzzle, "Pistol muzzle light (28 ms, no shadows)");
        Light point = light.AddComponent<Light>();
        point.type = LightType.Point; point.range = 2.8f; point.intensity = 1.65f;
        point.color = new Color(1f, .66f, .27f); point.shadows = LightShadows.None;
        point.lightmapBakeType = LightmapBakeType.Realtime; point.enabled = false;
        pistol.muzzleLight = point;
        pistol.reportClip = Audio("Service Pistol Concussion V1", 1.0f, 0);
        pistol.reportSource = Source(camera.transform, "Pistol report", .72f, pistol.reportClip);

        GameObject hands = Model(camera.transform, "Unarmed Hands");
        hands.transform.localPosition = new Vector3(0f, -.24f, .32f);
        Transform left = ClosedFist(hands.transform, "Left fist", -1f);
        Transform right = ClosedFist(hands.transform, "Right fist", 1f);
        left.localPosition = new Vector3(-.21f, -.012f, .075f);
        left.localRotation = Quaternion.Euler(-8f, 10f, -12f);
        right.localPosition = new Vector3(.22f, -.035f, .005f);
        right.localRotation = Quaternion.Euler(-8f, -10f, 12f);
        FacilityFists fists = camera.GetComponent<FacilityFists>();
        if (fists == null) fists = camera.gameObject.AddComponent<FacilityFists>();
        fists.aimCamera = camera; fists.inventory = inventory;
        fists.leftHand = left; fists.rightHand = right;
        fists.swingClip = Audio("Canvas Punch Swing V1", .24f, 1);
        fists.impactClip = Audio("Heavy Gloved Impact V1", .32f, 2);
        fists.impactSource = Source(camera.transform, "Fist contact", .68f, fists.impactClip);

        inventory.player = player; inventory.shotgun = shotgun; inventory.pistol = pistol; inventory.fists = fists;
        inventory.shotgunModel = shotgunRoot;
        inventory.pistolModel = pistolRoot.transform; inventory.fistsModel = hands.transform; inventory.viewmodel = motion;
        inventory.ConfigureLoadout(true);
        EditorUtility.SetDirty(shotgun); EditorUtility.SetDirty(pistol); EditorUtility.SetDirty(fists);
        EditorUtility.SetDirty(inventory); EditorUtility.SetDirty(motion);
        AssetDatabase.SaveAssets();
        return inventory;
    }

    public static GameObject BuildWorldPistol(Transform parent, Vector3 localPosition)
    {
        EnsureFolder(AssetRoot + "/Meshes"); EnsureFolder(AssetRoot + "/Materials");
        GameObject root = Model(parent, "Service Pistol Pickup");
        BuildPistolGeometry(root.transform, false, out _, out _);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        return root;
    }

    private static void BuildPistolGeometry(Transform root, bool held, out Transform muzzle, out Transform slide)
    {
        Material steel = Material("Pistol Blued Steel", new Color(.105f, .125f, .14f));
        Material edge = Material("Pistol Machined Edges", new Color(.32f, .35f, .36f));
        Material dark = Material("Pistol Bore And Port", new Color(.008f, .010f, .011f));
        Material grip = Material("Pistol Walnut Grip", new Color(.29f, .125f, .065f));
        Material dot = Material("Pistol Sight Paint", new Color(.72f, .73f, .63f));
        Piece(root, "Steel frame and beavertail", Extrusion("Pistol frame", .037f, new[] {
            new Vector2(.005f, -.099f), new Vector2(.005f, .12f), new Vector2(-.025f, .12f),
            new Vector2(-.028f, -.021f), new Vector2(-.048f, -.075f), new Vector2(-.040f, -.110f) }), steel);
        Piece(root, "Slanted grip frame", Extrusion("Pistol grip frame", .038f, new[] {
            new Vector2(-.021f, -.080f),new Vector2(-.033f, -.023f),new Vector2(-.164f, -.064f),
            new Vector2(-.170f, -.104f),new Vector2(-.142f, -.116f) }), steel);
        for (int side = -1; side <= 1; side += 2)
        {
            Transform panel = Piece(root, side < 0 ? "Left walnut grip panel" : "Right walnut grip panel", Extrusion("Pistol grip panel", .006f, new[] {
                new Vector2(-.044f, -.078f),new Vector2(-.052f, -.033f),new Vector2(-.149f, -.066f),
                new Vector2(-.154f, -.098f),new Vector2(-.136f, -.105f) }), grip).transform;
            panel.localPosition = new Vector3(side * .021f, 0f, 0f);
            for (int i = 0; i < 2; i++)
            {
                Transform screw = Piece(root, "Grip screw", Cylinder("Pistol grip screw", .0037f, .002f, 8), edge).transform;
                screw.localPosition = new Vector3(side * .0255f, -.061f - i * .074f, -.057f - i * .026f);
                screw.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            for (int i = 0; i < 7; i++)
                Box(root, "Grip checkering " + side + " " + i, new Vector3(side * .025f, -.075f - i * .008f, -.064f - i * .003f), new Vector3(.001f, .0018f, .027f), steel);
        }
        Box(root, "Magazine baseplate", new Vector3(0f, -.171f, -.087f), new Vector3(.043f, .009f, .049f), dark);
        slide = Model(root, "Reciprocating slide").transform;
        Piece(slide, "Beveled slide", Extrusion("Pistol beveled slide", .044f, new[] {
            new Vector2(.007f, -.101f),new Vector2(.007f, .128f),new Vector2(.037f, .128f),
            new Vector2(.050f, .113f),new Vector2(.050f, -.088f),new Vector2(.037f, -.101f) }), steel);
        Box(slide, "Polished slide upper rail", new Vector3(0f, .0495f, .008f), new Vector3(.026f, .002f, .185f), edge);
        Box(slide, "Recessed ejection port", new Vector3(.0222f, .029f, .022f), new Vector3(.0015f, .025f, .050f), dark);
        Box(slide, "Barrel chamber visible in port", new Vector3(.0231f, .026f, .016f), new Vector3(.001f, .015f, .036f), edge);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 6; i++)
                Box(slide, "Rear slide serration " + side + " " + i, new Vector3(side * .0222f, .024f, -.083f + i * .0045f), new Vector3(.0012f, .027f, .0018f), edge);
        Transform barrel = Piece(root, "Hollow pistol barrel and crown", Tube("Pistol hollow barrel", .0095f, .006f, .183f, 14), edge).transform;
        barrel.localPosition = new Vector3(0f, .025f, .0465f);
        Transform bore = Piece(root, "Dark bore recessed behind crown", Cylinder("Pistol dark bore", .0059f, .002f, 14), dark).transform;
        bore.localPosition = new Vector3(0f, .025f, .116f);
        Transform guide = Piece(root, "Recoil spring guide", Cylinder("Pistol spring guide", .005f, .020f, 10), dark).transform;
        guide.localPosition = new Vector3(0f, -.004f, .122f);
        Box(slide, "Front sight blade", new Vector3(0f, .058f, .107f), new Vector3(.005f, .014f, .017f), dark);
        Box(slide, "Front sight ivory dot", new Vector3(0f, .060f, .0977f), new Vector3(.003f, .005f, .001f), dot);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(slide, "Rear sight notch ear " + side, new Vector3(side * .009f, .057f, -.080f), new Vector3(.007f, .015f, .013f), dark);
            Box(slide, "Rear sight ivory dot " + side, new Vector3(side * .009f, .059f, -.087f), new Vector3(.003f, .004f, .001f), dot);
        }
        Box(root, "Exposed hammer", new Vector3(0f, .027f, -.108f), new Vector3(.012f, .034f, .014f), edge);
        Box(root, "Thumb safety", new Vector3(-.025f, -.005f, -.080f), new Vector3(.009f, .008f, .026f), edge);
        Box(root, "Slide release", new Vector3(-.025f, -.012f, -.018f), new Vector3(.008f, .007f, .029f), edge);
        // Three connected bars and an angled front form an actual open guard, rather than a solid box.
        Box(root, "Trigger guard rear", new Vector3(0f, -.053f, -.026f), new Vector3(.015f, .038f, .010f), steel);
        Box(root, "Trigger guard underside", new Vector3(0f, -.072f, .009f), new Vector3(.015f, .009f, .070f), steel);
        Transform guardFront = Box(root, "Trigger guard curved front", new Vector3(0f, -.050f, .043f), new Vector3(.015f, .046f, .009f), steel).transform;
        guardFront.localRotation = Quaternion.Euler(-10f, 0f, 0f);
        Transform trigger = Box(root, "Curved trigger", new Vector3(0f, -.044f, .007f), new Vector3(.008f, .030f, .006f), edge).transform;
        trigger.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        muzzle = Model(root, "Pistol muzzle").transform;
        muzzle.localPosition = new Vector3(0f, .025f, .142f);
        if (held) PistolHands(root);
    }

    private static void PistolHands(Transform root)
    {
        Material leather = Material("Arsenal Worn Leather", new Color(.25f, .205f, .145f));
        Material canvas = Material("Arsenal Olive Canvas", new Color(.19f, .225f, .17f));
        Material cuff = Material("Arsenal Glove Cuffs", new Color(.10f, .12f, .09f));
        Piece(root, "Right palm on pistol grip", Loft("Pistol right palm", new[] {
            new Vector3(.042f, -.047f, -.084f), new Vector3(.044f, -.095f, -.073f), new Vector3(.035f, -.153f, -.090f)
        }, new[] { .023f, .029f, .025f }, new[] { .022f, .028f, .025f }, 8), leather);
        for (int i = 0; i < 3; i++)
        {
            float y = -.082f - i * .025f, z = -.040f - i * .007f;
            Piece(root, "Right gripping finger " + i, Loft("Pistol finger " + i, new[] {
                new Vector3(.047f, y, z - .02f), new Vector3(.027f, y, z), new Vector3(-.014f, y, z), new Vector3(-.028f, y, z - .02f)
            }, new[] { .010f, .010f, .009f, .007f }, new[] { .010f, .010f, .009f, .007f }, 7), leather);
        }
        Piece(root, "Trigger index finger inside guard", Loft("Pistol trigger finger", new[] {
            new Vector3(.045f, -.049f, -.063f), new Vector3(.033f, -.045f, -.005f), new Vector3(.014f, -.048f, .016f), new Vector3(.006f, -.048f, .010f)
        }, new[] { .010f, .009f, .008f, .006f }, new[] { .010f, .009f, .008f, .006f }, 7), leather);
        Piece(root, "Left support palm around grip", Loft("Pistol left support palm", new[] {
            new Vector3(-.048f, -.072f, -.053f),new Vector3(-.049f, -.113f, -.060f),new Vector3(-.025f, -.153f, -.084f)
        }, new[] { .023f, .027f, .024f }, new[] { .023f, .028f, .024f }, 8), leather);
        for (int i = 0; i < 3; i++)
        {
            float y = -.091f - i * .023f, z = -.026f - i * .007f;
            Piece(root, "Support curled finger " + i, Loft("Pistol support finger " + i, new[] {
                new Vector3(-.053f, y, z - .026f),new Vector3(-.036f, y, z),new Vector3(.007f, y, z + .006f),new Vector3(.032f, y, z - .010f)
            }, new[] { .010f, .010f, .009f, .007f }, new[] { .010f, .010f, .009f, .007f }, 7), leather);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, "Pistol sleeve " + side, Loft("Pistol sleeve " + side, new[] {
                new Vector3(side * .23f, -.39f, -.25f),new Vector3(side * .105f, -.24f, -.16f),new Vector3(side * .031f, -.159f, -.098f)
            }, new[] { .052f, .045f, .033f }, new[] { .045f, .040f, .030f }, 9), canvas);
            Piece(root, "Pistol wrist cuff " + side, Loft("Pistol cuff " + side, new[] {
                new Vector3(side * .053f, -.184f, -.120f),new Vector3(side * .027f, -.153f, -.096f)
            }, new[] { .036f, .032f }, new[] { .032f, .029f }, 9), cuff);
        }
    }

    private static Transform ClosedFist(Transform parent, string name, float side)
    {
        Transform root = Model(parent, name).transform;
        Material leather = Material("Arsenal Worn Leather", new Color(.25f, .205f, .145f));
        Material canvas = Material("Arsenal Olive Canvas", new Color(.19f, .225f, .17f));
        Material cuff = Material("Arsenal Glove Cuffs", new Color(.10f, .12f, .09f));
        Piece(root, "Rounded closed glove palm", Loft("Unarmed rounded palm", new[] {
            new Vector3(0f, -.008f, -.065f),new Vector3(0f, .005f, .005f),new Vector3(0f, .008f, .068f)
        }, new[] { .036f, .050f, .045f }, new[] { .030f, .039f, .032f }, 9), leather);
        for (int i = 0; i < 4; i++)
        {
            float x = -.034f + i * .023f;
            Piece(root, "Curled finger and knuckle " + i, Loft("Unarmed curled finger " + i, new[] {
                new Vector3(x, .020f, .015f),new Vector3(x, .029f, .069f),new Vector3(x, .006f, .090f),new Vector3(x, -.029f, .063f),new Vector3(x, -.023f, .031f)
            }, new[] { .011f, .012f, .011f, .010f, .008f }, new[] { .011f, .012f, .011f, .010f, .008f }, 7), leather);
        }
        Piece(root, "Thumb wrapped under fingers", Loft("Unarmed thumb " + side, new[] {
            new Vector3(-side * .049f, -.004f, -.005f),new Vector3(-side * .055f, -.023f, .035f),new Vector3(-side * .024f, -.039f, .060f),new Vector3(side * .006f, -.037f, .054f)
        }, new[] { .014f, .014f, .012f, .008f }, new[] { .013f, .013f, .011f, .008f }, 7), leather);
        Piece(root, "Wrist cuff", Loft("Unarmed cuff", new[] {
            new Vector3(0f, -.015f, -.105f),new Vector3(0f, -.009f, -.058f)
        }, new[] { .038f, .035f }, new[] { .034f, .031f }, 9), cuff);
        Piece(root, "Tapered canvas forearm", Loft("Unarmed sleeve " + side, new[] {
            new Vector3(side * .085f, -.23f, -.25f),new Vector3(side * .024f, -.075f, -.15f),new Vector3(0f, -.015f, -.097f)
        }, new[] { .058f, .046f, .036f }, new[] { .049f, .040f, .032f }, 9), canvas);
        return root;
    }

    private static AudioClip Audio(string name, float duration, int kind)
    {
        EnsureFolder(AssetRoot + "/Audio");
        string path = AssetRoot + "/Audio/" + name + ".wav";
        AudioClip existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null) return existing;
        const int rate = 44100;
        float[] samples = new float[Mathf.RoundToInt(rate * duration)];
        uint seed = 93041u + (uint)kind;
        double low = 0, mid = 0;
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)rate;
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            double noise = (seed & 0xffffff) / 8388608.0 - 1.0;
            low += (noise - low) * .025; mid += (noise - mid) * .19;
            double value;
            if (kind == 0)
            {
                double attack = 1.0 - Math.Exp(-t * 2800.0);
                double crack = (noise - mid) * .21 * Math.Exp(-t * 95.0);
                double body = Math.Sin(2.0 * Math.PI * (92.0 * t + 2.2 * (1.0 - Math.Exp(-t * 48.0)))) * .70 * Math.Exp(-t * 19.0);
                double pressure = low * 3.6 * Math.Exp(-t * 14.0) + mid * .48 * Math.Exp(-t * 31.0);
                value = (crack + body + pressure) * attack;
                if (t > .095) value += mid * .27 * Math.Exp(-(t - .095) * 11.0);
                if (t > .18) value += low * .65 * Math.Exp(-(t - .18) * 9.0);
            }
            else if (kind == 1)
            {
                double envelope = Math.Sin(Math.PI * Math.Min(1.0, t / duration));
                value = mid * .70 * envelope * envelope;
            }
            else
            {
                value = (Math.Sin(2.0 * Math.PI * 88.0 * t) * .42 + low * 3.0 + mid * .50) *
                    (1.0 - Math.Exp(-t * 1600.0)) * Math.Exp(-t * 27.0);
            }
            samples[i] = (float)value;
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        }
        using (FileStream stream = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            for (int i = 0; i < samples.Length; i++)
            {
                float tail = Mathf.Clamp01((samples.Length - 1 - i) / 220f);
                writer.Write((short)(Mathf.Clamp(samples[i] * .78f / Mathf.Max(.001f, peak) * tail, -1f, 1f) * 32767));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings; importer.forceToMono = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static AudioSource Source(Transform parent, string name, float volume, AudioClip clip)
    {
        Transform old = parent.Find(name);
        GameObject go = old != null ? old.gameObject : Model(parent, name);
        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null) source = go.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
        source.volume = volume; source.priority = 40; source.clip = clip;
        return source;
    }

    private static Material Material(string name, Color color, bool unlit = false)
    {
        EnsureFolder(AssetRoot + "/Materials");
        string path = AssetRoot + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        material.SetColor("_BaseColor", color);
        if (!unlit)
        {
            material.SetFloat("_Smoothness", .20f); material.SetFloat("_Metallic", name.Contains("Steel") || name.Contains("Edges") ? .18f : 0f);
            material.SetColor("_EmissionColor", color * .15f); material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject Model(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }
    private static GameObject Piece(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject go = Model(parent, name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Camera camera = parent.GetComponentInParent<Camera>();
        if (camera != null) renderer.probeAnchor = camera.transform;
        return go;
    }
    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject go = Piece(parent, name, Extrusion("Arsenal unit box", 1f, new[] {
            new Vector2(-.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,-.5f) }), material);
        go.transform.localPosition = position; go.transform.localScale = size; return go;
    }
    private static Mesh Extrusion(string name, float width, Vector2[] profile)
    {
        Mesh cached = LoadMesh(name); if (cached != null) return cached;
        MeshBuilder b = new MeshBuilder();
        for (int i = 0; i < profile.Length; i++)
        {
            Vector2 a = profile[i], c = profile[(i + 1) % profile.Length];
            b.Quad(new Vector3(-width*.5f,a.x,a.y),new Vector3(width*.5f,a.x,a.y),new Vector3(width*.5f,c.x,c.y),new Vector3(-width*.5f,c.x,c.y));
        }
        // The beavertail has an inset: clip ears so its side faces do not bridge the recess.
        float area = 0f;
        for (int i = 0; i < profile.Length; i++) { Vector2 a = profile[i], c = profile[(i+1)%profile.Length]; area += a.x*c.y-c.x*a.y; }
        float winding = Mathf.Sign(area);
        List<int> polygon = new List<int>();
        for (int i=0;i<profile.Length;i++) polygon.Add(i);
        int guard = profile.Length * profile.Length;
        while (polygon.Count > 2 && guard-- > 0)
        {
            bool clipped = false;
            for (int i=0;i<polygon.Count;i++)
            {
                int ia=polygon[(i+polygon.Count-1)%polygon.Count], ic=polygon[i], id=polygon[(i+1)%polygon.Count];
                if (Cross(profile[ic]-profile[ia],profile[id]-profile[ic])*winding<=0f)continue;
                bool contains=false;
                foreach(int q in polygon)
                {
                    if(q==ia||q==ic||q==id)continue;
                    Vector2 p=profile[q];
                    if(Cross(profile[ic]-profile[ia],p-profile[ia])*winding>=0f&&
                       Cross(profile[id]-profile[ic],p-profile[ic])*winding>=0f&&
                       Cross(profile[ia]-profile[id],p-profile[id])*winding>=0f){contains=true;break;}
                }
                if(contains)continue;
                Vector3 a=new Vector3(width*.5f,profile[ia].x,profile[ia].y),c=new Vector3(width*.5f,profile[ic].x,profile[ic].y),d=new Vector3(width*.5f,profile[id].x,profile[id].y);
                if(winding>0f)b.Triangle(a,c,d);else b.Triangle(d,c,a);
                a.x=c.x=d.x=-width*.5f;
                if(winding>0f)b.Triangle(d,c,a);else b.Triangle(a,c,d);
                polygon.RemoveAt(i);clipped=true;break;
            }
            if(!clipped)throw new InvalidOperationException("Could not triangulate arsenal mesh " + name);
        }
        return SaveMesh(name,b.Build(name));
    }
    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    private static Mesh Cylinder(string name, float radius, float length, int sides)
    {
        Mesh cached=LoadMesh(name); if(cached!=null)return cached;
        MeshBuilder b=new MeshBuilder();
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2f/sides,c=(i+1)*Mathf.PI*2f/sides;
            Vector3 p=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-length*.5f),q=new Vector3(Mathf.Cos(c)*radius,Mathf.Sin(c)*radius,-length*.5f),z=Vector3.forward*length;
            b.Quad(p,q,q+z,p+z); b.Triangle(Vector3.forward*length*.5f,p+z,q+z); b.Triangle(Vector3.back*length*.5f,q,p);
        }
        return SaveMesh(name,b.Build(name));
    }
    private static Mesh Tube(string name,float outer,float inner,float length,int sides)
    {
        Mesh cached=LoadMesh(name); if(cached!=null)return cached;
        MeshBuilder b=new MeshBuilder();
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2f/sides,c=(i+1)*Mathf.PI*2f/sides;
            Vector3 p=new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,-length*.5f),q=new Vector3(Mathf.Cos(c)*outer,Mathf.Sin(c)*outer,-length*.5f);
            Vector3 ip=new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,-length*.5f),iq=new Vector3(Mathf.Cos(c)*inner,Mathf.Sin(c)*inner,-length*.5f),z=Vector3.forward*length;
            b.Quad(p,q,q+z,p+z);b.Quad(ip+z,iq+z,iq,ip);b.Quad(p+z,q+z,iq+z,ip+z);b.Quad(p,ip,iq,q);
        }
        return SaveMesh(name,b.Build(name));
    }
    private static Mesh Loft(string name,Vector3[] centres,float[] widths,float[] heights,int sides)
    {
        Mesh cached=LoadMesh(name);if(cached!=null)return cached;
        MeshBuilder b=new MeshBuilder();Vector3[,] rings=new Vector3[centres.Length,sides];
        for(int ring=0;ring<centres.Length;ring++)
        {
            Vector3 tangent=(centres[Mathf.Min(ring+1,centres.Length-1)]-centres[Mathf.Max(ring-1,0)]).normalized;
            Vector3 across=Vector3.Cross(tangent,Vector3.up);if(across.sqrMagnitude<.001f)across=Vector3.right;across.Normalize();
            Vector3 depth=Vector3.Cross(tangent,across).normalized;
            for(int i=0;i<sides;i++){float angle=i*Mathf.PI*2f/sides;rings[ring,i]=centres[ring]+across*(Mathf.Cos(angle)*widths[ring])+depth*(Mathf.Sin(angle)*heights[ring]);}
        }
        for(int ring=0;ring<centres.Length-1;ring++)for(int i=0;i<sides;i++)b.Quad(rings[ring,i],rings[ring,(i+1)%sides],rings[ring+1,(i+1)%sides],rings[ring+1,i]);
        for(int i=0;i<sides;i++){int next=(i+1)%sides,last=centres.Length-1;b.Triangle(centres[0],rings[0,next],rings[0,i]);b.Triangle(centres[last],rings[last,i],rings[last,next]);}
        return SaveMesh(name,b.Build(name));
    }
    private static Mesh Flash()
    {
        const string name="Pistol muzzle star V1";Mesh cached=LoadMesh(name);if(cached!=null)return cached;
        MeshBuilder b=new MeshBuilder();Vector3 centre=new Vector3(0f,0f,.018f);
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI*2f/12,c=(i+1)*Mathf.PI*2f/12;
            Vector3 p=centre+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0f)*(i%2==0?.11f:.035f);
            Vector3 q=centre+new Vector3(Mathf.Cos(c),Mathf.Sin(c),0f)*((i+1)%2==0?.11f:.035f);
            b.Triangle(centre,p,q);b.Triangle(centre,q,p);
        }
        for(int i=0;i<2;i++){Vector3 side=i==0?Vector3.right*.04f:Vector3.up*.04f;Vector3 tip=Vector3.forward*.18f;b.Triangle(-side,side,tip);b.Triangle(tip,side,-side);}
        return SaveMesh(name,b.Build(name));
    }
    private static Mesh LoadMesh(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(AssetRoot+"/Meshes/"+name+".asset");
    private static Mesh SaveMesh(string name,Mesh mesh){EnsureFolder(AssetRoot+"/Meshes");AssetDatabase.CreateAsset(mesh,AssetRoot+"/Meshes/"+name+".asset");return mesh;}
    private static void DestroyNamed(Transform parent,string name){Transform old=parent.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<int> triangles=new List<int>();private readonly List<Vector2> uv=new List<Vector2>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);uv.Add(new Vector2(a.z*4,a.y*4));uv.Add(new Vector2(b.z*4,b.y*4));uv.Add(new Vector2(c.z*4,c.y*4));triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        public Mesh Build(string name){Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
}
