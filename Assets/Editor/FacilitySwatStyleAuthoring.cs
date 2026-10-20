using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Saved, deliberately stylized SWAT equipment on the existing destructible anatomy.</summary>
public static class FacilitySwatStyleAuthoring
{
    public const string Root = "Assets/Generated/CharacterStyles/Swat";
    public const string HeadGroup = "SWAT style - segmented helmet and face";
    public const string EquipmentGroup = "SWAT style - equipment";
    public const string ArmorPrefix = "SWAT removable armor ";
    static Material navy, charcoal, plate, webbing, edge, ivory, blood, glass;
    static Material[] panelMaterials;

    /// <summary>Replaces cosmetic meshes only. Colliders, joints, damage regions and combat tuning are retained.</summary>
    public static void Apply(FacilityHumanoid actor)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (Application.isPlaying) throw new InvalidOperationException("Author SWAT equipment outside Play mode.");
        if (actor.head == null || actor.hitRegions == null || actor.hitRegions.Length != 11)
            throw new InvalidOperationException("SWAT styling requires the complete existing eleven-region anatomy.");
        FacilityCharacterStyleAuthoring.ClearStaff(actor);
        Prepare();
        RemoveLegacyEquipment(actor);
        RecolorOriginalClothing(actor);
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            if (region == null || region.kind == FacilityBodyRegion.Head) continue;
            RebuildRemovablePanels(region);
            if (region.kind == FacilityBodyRegion.ForearmLeft || region.kind == FacilityBodyRegion.ForearmRight)
                Gloves(region);
            if (region.kind == FacilityBodyRegion.ShinLeft || region.kind == FacilityBodyRegion.ShinRight)
                Boots(region);
            EditorUtility.SetDirty(region);
        }
        HelmetAndFace(actor);
        EditorUtility.SetDirty(actor);
    }

    static void Prepare()
    {
        Folder(Root + "/Materials"); Folder(Root + "/Meshes");
        navy = Mat("SWAT dark navy fabric", new Color(.105f, .14f, .18f), .045f);
        charcoal = Mat("SWAT charcoal rubber", new Color(.055f, .068f, .075f), .09f);
        plate = Mat("SWAT blue charcoal armor", new Color(.12f, .155f, .175f), .13f);
        webbing = Mat("SWAT gray webbing", new Color(.19f, .22f, .23f), .05f);
        edge = Mat("SWAT worn armor edges", new Color(.27f, .30f, .31f), .16f);
        ivory = Mat("SWAT identification and eyes", new Color(.69f, .72f, .65f), .025f);
        blood = Mat("SWAT dried crimson scuffs", new Color(.22f, .021f, .025f), .13f);
        glass = Mat("SWAT smoked visor", new Color(.15f, .205f, .215f, .36f), .44f);
        glass.SetFloat("_Surface", 1); glass.SetFloat("_Blend", 0); glass.SetFloat("_ZWrite", 0);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue = 3000;
        EditorUtility.SetDirty(glass);
        panelMaterials = new[] { navy, charcoal, plate, webbing, edge, ivory, blood };
    }

    static void RemoveLegacyEquipment(FacilityHumanoid actor)
    {
        // The old vest is permanent geometry. Removing it prevents an invisible wound behind an intact plate.
        var exact = new HashSet<string> {
            "Fitted ballistic vest front", "Fitted ballistic vest back", "Vest shoulder webbing",
            "Radio and magazine pouch", "Front police identification", "Rear police identification",
            "Navy patrol cap crown", "Cap visor", "Cap shield badge", "Centre closure"
        };
        Transform[] children = actor.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null || child == actor.transform) continue;
            if (exact.Contains(child.name) || child.name.StartsWith("SWAT style - ", StringComparison.Ordinal))
                Object.DestroyImmediate(child.gameObject);
        }
    }

    static void RecolorOriginalClothing(FacilityHumanoid actor)
    {
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null) continue;
            string name = material.name;
            if (name == "Coverall - faded sage" || name == "Police navy woven uniform") renderer.sharedMaterial = navy;
            else if (name == "Boots and utility belt" || name == "Police ballistic vest") renderer.sharedMaterial = charcoal;
        }
    }

    static void RebuildRemovablePanels(FacilityHitRegion region)
    {
        if (region.outerPatches == null) return;
        bool torso = region.kind == FacilityBodyRegion.Torso;
        Vector3 size = Size(region.kind);
        Vector3 center = Center(region.kind);
        int columns = torso ? 3 : 1, rows = 2, faceCount = columns * rows * 2;
        for (int index = 0; index < region.outerPatches.Length; index++)
        {
            Renderer renderer = region.outerPatches[index];
            if (renderer == null) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null) throw new InvalidOperationException("A SWAT damage panel has no saved mesh filter.");
            MeshBuilder mesh = new MeshBuilder(panelMaterials.Length);
            if (index < faceCount)
            {
                int face = index / (columns * rows), cell = index % (columns * rows);
                int row = cell / columns, column = cell % columns;
                float sign = face == 0 ? 1f : -1f;
                Vector3 p = center + new Vector3(-size.x * .5f + (column + .5f) * size.x / columns,
                    -size.y * .5f + (row + .5f) * size.y / rows, sign * (size.z * .5f - .003f));
                Vector3 panelSize = new Vector3(size.x / columns + .002f, size.y / rows + .002f, .022f);
                mesh.Box(p, panelSize, 0);
                if (torso) CarrierPanel(mesh, p, panelSize, row, column, sign);
                else LimbPanel(mesh, region.kind, p, panelSize, row, sign);
            }
            else
            {
                float side = index == faceCount ? -1f : 1f;
                Vector3 p = center + Vector3.right * side * (size.x * .5f - .007f);
                mesh.Box(p, new Vector3(.025f, size.y, size.z), 0);
                if (torso)
                {
                    mesh.Box(p + Vector3.right * side * .021f, new Vector3(.035f, .26f, .235f), 1);
                    for (int r = 0; r < 3; r++)
                        mesh.Box(new Vector3(p.x + side * .042f, -.088f + r * .065f, 0), new Vector3(.008f, .025f, .21f), 3);
                }
            }
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.name = ArmorPrefix + region.kind + " " + index;
            filter.sharedMesh = SaveMesh(region.kind + " damage panel " + index + " V1", mesh.Build());
            renderer.sharedMaterials = panelMaterials;
            ConfigureRenderer(renderer);
            EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(filter);
        }
    }

    static void CarrierPanel(MeshBuilder mesh, Vector3 p, Vector3 size, int row, int column, float sign)
    {
        // Each pouch, strap, letter and armor edge belongs to this SAME removable renderer.
        mesh.Box(p + Vector3.forward * sign * .029f,
            new Vector3(size.x - .007f, size.y - .008f, .052f), 2);
        mesh.Box(p + Vector3.forward * sign * .058f,
            new Vector3(size.x - .020f, size.y - .022f, .014f), 1);
        if (row == 0)
        {
            mesh.Box(new Vector3(p.x, -.133f, sign * .221f), new Vector3(.118f, .149f, .075f), 2);
            mesh.Box(new Vector3(p.x, -.073f, sign * .262f), new Vector3(.124f, .035f, .014f), 3);
            mesh.Box(new Vector3(p.x, -.122f, sign * .264f), new Vector3(.021f, .059f, .012f), 1);
            mesh.Box(new Vector3(p.x, -.133f, sign * .274f), new Vector3(.030f, .022f, .010f), 4);
            if (column == 0 && sign > 0)
            {
                mesh.Box(new Vector3(p.x + .026f, -.16f, .267f), new Vector3(.019f, .045f, .004f), 6, Quaternion.Euler(0, 0, -18));
                mesh.Box(new Vector3(p.x + .009f, -.143f, .267f), new Vector3(.032f, .012f, .004f), 6, Quaternion.Euler(0, 0, -18));
            }
        }
        else
        {
            for (int r = 0; r < 2; r++)
                mesh.Box(new Vector3(p.x, .025f + r * .032f, sign * .202f), new Vector3(.122f, .012f, .009f), 3);
            if (column != 1)
            {
                mesh.Box(new Vector3(p.x, .198f, sign * .183f), new Vector3(.069f, .067f, .025f), 3);
                mesh.Box(new Vector3(p.x, .194f, sign * .200f), new Vector3(.042f, .025f, .010f), 1);
            }
            PoliceLetters(mesh, column, sign);
            if (column == 0 && sign > 0)
            {
                mesh.Box(new Vector3(p.x - .013f, .052f, .226f), new Vector3(.052f, .078f, .04f), 1);
                mesh.Box(new Vector3(p.x - .013f, .095f, .224f), new Vector3(.012f, .10f, .014f), 1);
                for (int i = 0; i < 3; i++) mesh.Box(new Vector3(p.x - .013f, .047f + i * .014f, .249f), new Vector3(.035f, .004f, .004f), 4);
            }
        }
    }

    static void PoliceLetters(MeshBuilder mesh, int column, float sign)
    {
        if (sign < 0) return;
        string[] letters = { "11110100011000111110100001000010000", "01110100011000110001100011000101110",
            "10000100001000010000100001000011111", "11111001000010000100001000010011111",
            "01111100001000010000100001000001111", "11111100001000011110100001000011111" };
        const float pixel = .0072f;
        float half = 35 * pixel * .5f;
        for (int c = 0; c < letters.Length; c++)
            for (int y = 0; y < 7; y++) for (int x = 0; x < 5; x++)
            {
                if (letters[c][y * 5 + x] != '1') continue;
                float px = -half + (c * 6 + x + .5f) * pixel;
                // The officer faces +Z; the viewer's screen-right is the officer's -X.
                float panelX = -px;
                if (Mathf.Clamp(Mathf.FloorToInt((panelX + .22f) / (.44f / 3)), 0, 2) != column) continue;
                mesh.Box(new Vector3(panelX, .155f - (y + .5f) * pixel, sign * .206f),
                    new Vector3(pixel * .85f, pixel * .85f, .002f), 5, Quaternion.identity, .03f);
            }
    }

    static void LimbPanel(MeshBuilder mesh, FacilityBodyRegion kind, Vector3 p, Vector3 size, int row, float sign)
    {
        bool upperArm = kind == FacilityBodyRegion.UpperArmLeft || kind == FacilityBodyRegion.UpperArmRight;
        bool forearm = kind == FacilityBodyRegion.ForearmLeft || kind == FacilityBodyRegion.ForearmRight;
        bool shin = kind == FacilityBodyRegion.ShinLeft || kind == FacilityBodyRegion.ShinRight;
        bool thigh = kind == FacilityBodyRegion.ThighLeft || kind == FacilityBodyRegion.ThighRight;
        if (upperArm && row == 1)
        {
            mesh.Box(p + Vector3.forward * sign * .018f, new Vector3(.161f, .132f, .039f), 2);
            mesh.Box(p + Vector3.forward * sign * .041f, new Vector3(.125f, .025f, .008f), 3);
            if (sign > 0)
            {
                mesh.Box(p + new Vector3(0, .024f, .044f), new Vector3(.05f, .018f, .005f), 5);
                mesh.Box(p + new Vector3(0, -.011f, .044f), new Vector3(.05f, .009f, .005f), 4);
            }
        }
        if (forearm && row == 1 && sign > 0 || shin && row == 1 && sign > 0)
        {
            Vector3 padSize = new Vector3(shin ? .153f : .142f, shin ? .128f : .117f, .050f);
            mesh.Box(p + Vector3.forward * .021f, padSize, 1);
            mesh.Box(p + Vector3.forward * .049f, new Vector3(padSize.x * .77f, padSize.y * .73f, .016f), 2);
            mesh.Box(p + new Vector3(0, -.025f, .059f), new Vector3(padSize.x * .62f, .008f, .006f), 4);
        }
        if ((forearm || shin) && row == 1 && sign < 0)
            mesh.Box(p + Vector3.forward * -.009f, new Vector3(size.x + .002f, .035f, .023f), 3);
        if (thigh && row == 0)
        {
            mesh.Box(p + Vector3.forward * sign * .008f, new Vector3(size.x, .033f, .025f), 1);
            if (sign > 0)
            {
                mesh.Box(p + new Vector3(0, .017f, .025f), new Vector3(.119f, .117f, .037f), 2);
                mesh.Box(p + new Vector3(0, .052f, .046f), new Vector3(.123f, .024f, .008f), 3);
            }
        }
        if (kind == FacilityBodyRegion.Pelvis && sign > 0 && row == 1)
        {
            mesh.Box(p + Vector3.forward * .014f, new Vector3(.32f, .040f, .035f), 1);
            mesh.Box(p + Vector3.forward * .035f, new Vector3(.050f, .033f, .015f), 4);
        }
    }

    static void Gloves(FacilityHitRegion region)
    {
        Transform group = Group(region.transform, EquipmentGroup);
        MeshBuilder mesh = new MeshBuilder(panelMaterials.Length);
        mesh.Box(new Vector3(0, -.306f, .059f), new Vector3(.096f, .043f, .032f), 1);
        for (int i = 0; i < 3; i++)
            mesh.Box(new Vector3(-.027f + i * .027f, -.326f, .076f), new Vector3(.020f, .025f, .012f), 2);
        Piece(group, "SWAT reinforced glove knuckles", SaveMesh(region.kind + " glove V1", mesh.Build()), panelMaterials);
        foreach (Transform child in region.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Hand" && child.name != "Thumb") continue;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = charcoal;
        }
    }

    static void Boots(FacilityHitRegion region)
    {
        Transform group = Group(region.transform, EquipmentGroup);
        MeshBuilder mesh = new MeshBuilder(panelMaterials.Length);
        mesh.Box(new Vector3(0, -.34f, .166f), new Vector3(.162f, .08f, .035f), 1);
        for (int i = 0; i < 4; i++)
            mesh.Box(new Vector3(0, -.298f + i * .017f, .098f), new Vector3(.086f, .008f, .009f), 3);
        Piece(group, "SWAT boot toecap and lacing", SaveMesh(region.kind + " boot V1", mesh.Build()), panelMaterials);
    }

    static void HelmetAndFace(FacilityHumanoid actor)
    {
        Transform group = Group(actor.head, HeadGroup);
        MeshBuilder mesh = new MeshBuilder(panelMaterials.Length);
        mesh.Dome(new Vector3(0, .050f, -.007f), new Vector3(.168f, .180f, .160f), 16, 5, 2);
        mesh.Ring(new Vector3(0, .057f, -.007f), .166f, .158f, .025f, 16, 1);
        mesh.Box(new Vector3(0, .078f, .018f), new Vector3(.290f, .054f, .260f), 2, Quaternion.identity, .20f);
        // A cheek/face wrap, with an actual open band exposing the eyes. No solid visor hides the face damage.
        mesh.FaceWrap(new Vector3(0, -.010f, .002f), new Vector3(.137f, .162f, .133f), 16, 12, 1);
        mesh.Box(new Vector3(0, -.151f, .014f), new Vector3(.15f, .065f, .12f), 1);
        mesh.Box(new Vector3(0, -.085f, .103f), new Vector3(.226f, .120f, .106f), 0, Quaternion.identity, .19f);
        mesh.Box(new Vector3(0, -.025f, .139f), new Vector3(.070f, .056f, .047f), 1, Quaternion.identity, .20f);
        for (int s = -1; s <= 1; s += 2)
        {
            mesh.Box(new Vector3(s * .151f, .073f, .002f), new Vector3(.025f, .066f, .14f), 1);
            mesh.Box(new Vector3(s * .168f, .074f, .002f), new Vector3(.012f, .025f, .082f), 3);
            mesh.Box(new Vector3(s * .145f, -.027f, .004f), new Vector3(.036f, .092f, .082f), 2);
            mesh.Box(new Vector3(s * .139f, -.085f, .067f), new Vector3(.015f, .106f, .025f), 3, Quaternion.Euler(25, 0, s * -18));
            mesh.Box(new Vector3(s * .140f, .045f, .13f), new Vector3(.026f, .045f, .035f), 3);
            mesh.Box(new Vector3(s * .054f, .057f, .130f), new Vector3(.090f, .018f, .019f), 1, Quaternion.Euler(0, 0, s * 13));
        }
        mesh.Box(new Vector3(0, .160f, .110f), new Vector3(.058f, .044f, .029f), 1);
        mesh.Box(new Vector3(0, .166f, .130f), new Vector3(.031f, .015f, .012f), 4);
        mesh.Box(new Vector3(0, -.039f, .155f), new Vector3(.060f, .010f, .006f), 1);
        mesh.Box(new Vector3(.075f, .135f, .103f), new Vector3(.040f, .009f, .004f), 4, Quaternion.Euler(0, 18, -25));
        mesh.Box(new Vector3(-.154f, -.045f, .079f), new Vector3(.011f, .011f, .083f), 1, Quaternion.Euler(0, 14, 0));
        mesh.Box(new Vector3(-.143f, -.043f, .122f), new Vector3(.025f, .016f, .022f), 1);
        Piece(group, "SWAT helmet balaclava and segmented equipment", SaveMesh("SWAT helmet and face V1", mesh.Build()), panelMaterials);

        MeshBuilder visor = new MeshBuilder(1);
        visor.Box(new Vector3(0, .030f, .142f), new Vector3(.253f, .052f, .008f), 0);
        Piece(group, "SWAT clear smoked eye shield", SaveMesh("SWAT eye shield V1", visor.Build()), new[] { glass });
        foreach (Transform part in actor.head.GetComponentsInChildren<Transform>(true))
        {
            if (part.name == "Left black eye" || part.name == "Right black eye")
            {
                part.localScale = new Vector3(.034f, .012f, .014f);
                part.localPosition = new Vector3(part.name.StartsWith("Left", StringComparison.Ordinal) ? -.055f : .055f, .023f, .126f);
                Renderer renderer = part.GetComponent<Renderer>(); if (renderer != null) renderer.sharedMaterial = ivory;
            }
            else if (part.name == "Cropped hair cap") part.gameObject.SetActive(false);
            else if (part.name == "Nose" || part.name == "Left ear" || part.name == "Right ear")
            {
                Renderer renderer = part.GetComponent<Renderer>(); if (renderer != null) renderer.sharedMaterial = charcoal;
            }
        }
        // The original removable head quadrants are the cloth balaclava itself. This keeps the
        // exposed eye band readable, avoids skin poking through a thin overlay, and retains damage.
        FacilityHitRegion headRegion = actor.head.GetComponent<FacilityHitRegion>();
        if (headRegion != null && headRegion.outerPatches != null)
            foreach (Renderer panel in headRegion.outerPatches) if (panel != null) panel.sharedMaterial = charcoal;
    }

    static Vector3 Size(FacilityBodyRegion kind)
    {
        switch (kind)
        {
            case FacilityBodyRegion.Pelvis: return new Vector3(.34f, .19f, .25f);
            case FacilityBodyRegion.Torso: return new Vector3(.44f, .47f, .27f);
            case FacilityBodyRegion.UpperArmLeft: case FacilityBodyRegion.UpperArmRight: return new Vector3(.16f, .29f, .18f);
            case FacilityBodyRegion.ForearmLeft: case FacilityBodyRegion.ForearmRight: return new Vector3(.135f, .28f, .15f);
            case FacilityBodyRegion.ThighLeft: case FacilityBodyRegion.ThighRight: return new Vector3(.17f, .34f, .21f);
            default: return new Vector3(.145f, .31f, .165f);
        }
    }

    static Vector3 Center(FacilityBodyRegion kind)
    {
        switch (kind)
        {
            case FacilityBodyRegion.UpperArmLeft: case FacilityBodyRegion.UpperArmRight: return new Vector3(0, -.145f, 0);
            case FacilityBodyRegion.ForearmLeft: case FacilityBodyRegion.ForearmRight: return new Vector3(0, -.14f, 0);
            case FacilityBodyRegion.ThighLeft: case FacilityBodyRegion.ThighRight: return new Vector3(0, -.17f, 0);
            case FacilityBodyRegion.ShinLeft: case FacilityBodyRegion.ShinRight: return new Vector3(0, -.155f, 0);
            default: return Vector3.zero;
        }
    }

    static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform; group.SetParent(parent, false); return group;
    }
    static void Piece(Transform parent, string name, Mesh mesh, Material[] materials)
    {
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterials = materials; ConfigureRenderer(renderer);
    }
    static void ConfigureRenderer(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
    static Material Mat(string name, Color color, float smoothness)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0);
        material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material); return material;
    }
    static Mesh SaveMesh(string name, Mesh mesh)
    {
        name = name.Replace(" V1", " V3");
        string path = Root + "/Meshes/" + name + ".asset"; mesh.name = name;
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }
        FacilityCharacterStyleAuthoring.CopyMesh(saved, mesh);
        Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved);
        AssetDatabase.SaveAssetIfDirty(saved);
        return saved;
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] triangles;
        public MeshBuilder(int materialCount) { triangles = new List<int>[materialCount]; for (int i = 0; i < materialCount; i++) triangles[i] = new List<int>(); }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material)
        {
            int i = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
            triangles[material].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        void ShellQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material)
        {
            // The saved shell has an inner surface as well as an outer surface, so the lower
            // helmet rim remains visible from below and the mask opening has no one-sided gaps.
            Quad(a, b, c, d, material); Quad(d, c, b, a, material);
        }
        public void Box(Vector3 position, Vector3 size, int material, Quaternion rotation = default, float bevel = .13f)
        {
            if (rotation == default) rotation = Quaternion.identity;
            float c = .5f - bevel;
            Vector2[] ring = { new Vector2(-c, -.5f), new Vector2(c, -.5f), new Vector2(.5f, -c), new Vector2(.5f, c), new Vector2(c, .5f), new Vector2(-c, .5f), new Vector2(-.5f, c), new Vector2(-.5f, -c) };
            Vector3[] front = new Vector3[8], back = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                front[i] = position + rotation * Vector3.Scale(size, new Vector3(ring[i].x, ring[i].y, .5f));
                back[i] = position + rotation * Vector3.Scale(size, new Vector3(ring[i].x, ring[i].y, -.5f));
            }
            Vector3 fc = position + rotation * Vector3.forward * size.z * .5f, bc = position - rotation * Vector3.forward * size.z * .5f;
            for (int i = 0; i < 8; i++)
            {
                int n = (i + 1) % 8;
                Quad(front[i], back[i], back[n], front[n], material);
                Quad(fc, front[i], front[n], fc, material);
                Quad(bc, back[n], back[i], bc, material);
            }
        }
        public void Dome(Vector3 center, Vector3 radii, int sides, int rings, int material)
        {
            for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
            {
                float p0 = r / (float)rings * Mathf.PI * .5f, p1 = (r + 1) / (float)rings * Mathf.PI * .5f;
                float a0 = s / (float)sides * Mathf.PI * 2, a1 = (s + 1) / (float)sides * Mathf.PI * 2;
                ShellQuad(Ellipse(center, radii, p0, a0), Ellipse(center, radii, p1, a0), Ellipse(center, radii, p1, a1), Ellipse(center, radii, p0, a1), material);
            }
        }
        public void Ring(Vector3 center, float x, float z, float height, int sides, int material)
        {
            for (int s = 0; s < sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2, b = (s + 1) / (float)sides * Mathf.PI * 2;
                Vector3 p = new Vector3(Mathf.Sin(a) * x, 0, Mathf.Cos(a) * z), q = new Vector3(Mathf.Sin(b) * x, 0, Mathf.Cos(b) * z);
                ShellQuad(center + p + Vector3.up * height * .5f, center + p - Vector3.up * height * .5f,
                    center + q - Vector3.up * height * .5f, center + q + Vector3.up * height * .5f, material);
            }
        }
        public void FaceWrap(Vector3 center, Vector3 radii, int sides, int rings, int material)
        {
            for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
            {
                float p0 = r / (float)rings * Mathf.PI, p1 = (r + 1) / (float)rings * Mathf.PI;
                float a0 = s / (float)sides * Mathf.PI * 2, a1 = (s + 1) / (float)sides * Mathf.PI * 2;
                Vector3 middle = Ellipse(center, radii, (p0 + p1) * .5f, (a0 + a1) * .5f);
                if (middle.z > .07f && middle.y > -.005f && middle.y < .077f) continue;
                ShellQuad(Ellipse(center, radii, p0, a0), Ellipse(center, radii, p1, a0), Ellipse(center, radii, p1, a1), Ellipse(center, radii, p0, a1), material);
            }
        }
        static Vector3 Ellipse(Vector3 c, Vector3 r, float p, float a)
            => c + Vector3.Scale(r, new Vector3(Mathf.Sin(p) * Mathf.Sin(a), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Cos(a)));
        public Mesh Build()
        {
            Mesh mesh = new Mesh(); if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
