using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Stylized infected-worker art, applied to the existing segmented, damageable adult anatomy.</summary>
public static class FacilityZombieStyleAuthoring
{
    const string Root = "Assets/Generated/CharacterStyles/Infected";
    const string Marker = "Infected style - angular decay";
    const string BodyMarker = "Infected style - torn workwear";
    const string SharedMeshes = "Assets/Generated/Combat/Character/Meshes/";
    static Mesh rounded, block, ragged;
    static Material cloth, clothLight, trim, skin, skinLight, socket, eye, hair, blood, bone;

    public static void Apply(FacilityHumanoid actor)
    {
        if (actor == null) return;
        if (Application.isPlaying) throw new InvalidOperationException("Author infected appearance outside Play mode.");
        FacilityCharacterStyleAuthoring.ClearStaff(actor);
        Prepare();
        RemoveOurDetails(actor);
        ReplacePalette(actor);
        Face(actor);
        Workwear(actor);
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            if (region == null) continue;
            if (region.kind == FacilityBodyRegion.Torso) OpenWound(region, 3, true);
            else if (region.kind == FacilityBodyRegion.ForearmRight) OpenWound(region, 0, false);
        }
        EditorUtility.SetDirty(actor);
    }

    static void Prepare()
    {
        Folder(Root + "/Materials"); Folder(Root + "/Meshes");
        rounded = AssetDatabase.LoadAssetAtPath<Mesh>(SharedMeshes + "Rounded low poly form.asset");
        block = AssetDatabase.LoadAssetAtPath<Mesh>(SharedMeshes + "Eight sided chamfered block.asset");
        if (rounded == null || block == null) throw new InvalidOperationException("Create the original character geometry before styling the infected.");
        ragged = RaggedCloth();
        cloth = Mat("Infected - drowned charcoal cloth", new Color(.215f, .27f, .245f));
        clothLight = Mat("Infected - faded cloth edges", new Color(.30f, .345f, .285f));
        trim = Mat("Infected - cracked work boots", new Color(.055f, .065f, .058f));
        skin = Mat("Infected - cold olive skin", new Color(.46f, .505f, .39f));
        skinLight = Mat("Infected - bony olive planes", new Color(.53f, .55f, .425f));
        socket = Mat("Infected - deep facial hollows", new Color(.055f, .064f, .047f));
        eye = Mat("Infected - dull ivory eyes", new Color(.67f, .675f, .51f));
        hair = Mat("Infected - matted dark hair", new Color(.085f, .10f, .075f));
        blood = Mat("Infected - dried burgundy blood", new Color(.285f, .035f, .035f));
        bone = Mat("Infected - stained exposed bone", new Color(.64f, .58f, .405f));
    }

    static void RemoveOurDetails(FacilityHumanoid actor)
    {
        var remove = new List<GameObject>();
        foreach (Transform child in actor.GetComponentsInChildren<Transform>(true))
            if (child != actor.transform && (child.name == Marker || child.name == BodyMarker || child.name == "Infected style - exposed ribs" || child.name == "Infected style - exposed forearm bone" || child.name.StartsWith("Character style - ", StringComparison.Ordinal)))
                remove.Add(child.gameObject);
        foreach (GameObject item in remove) if (item != null) UnityEngine.Object.DestroyImmediate(item);
    }

    static void ReplacePalette(FacilityHumanoid actor)
    {
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                Material replacement = null;
                switch (materials[i].name)
                {
                    case "Coverall - faded sage":
                    case "Water damaged charcoal coverall": replacement = cloth; break;
                    case "Warm skin":
                    case "Cold ashen skin": replacement = skin; break;
                    case "Boots and utility belt":
                    case "Worn black leather": replacement = trim; break;
                    case "Cropped dark hair":
                    case "Dusty cropped hair": replacement = hair; break;
                    case "Black eyes":
                    case "Clouded dead eyes": replacement = eye; break;
                }
                if (replacement == null) continue;
                materials[i] = replacement; changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    static void Face(FacilityHumanoid actor)
    {
        if (actor.head == null) return;
        Transform face = actor.head.Find("Slumped infected face");
        if (face == null) face = actor.head;
        // Keep the body/collider/rest pose intact: all new forms are attached to the anatomical head.
        foreach (Transform child in actor.head.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Removable head quadrant", StringComparison.Ordinal)) child.localScale = new Vector3(.247f, .333f, .245f);
            if (child.name == "Head tissue core") child.localScale = new Vector3(.233f, .315f, .231f);
            if (child.name == "Cropped hair cap")
            { child.localPosition = new Vector3(0, .121f, -.022f); child.localScale = new Vector3(.248f, .09f, .223f); }
            if (child.name == "Nose")
            { child.localPosition = new Vector3(0, -.019f, .127f); child.localScale = new Vector3(.034f, .058f, .043f); child.GetComponent<Renderer>().sharedMaterial = skinLight; }
            if (child.name == "Left ear" || child.name == "Right ear") child.localScale = new Vector3(.044f, .09f, .045f);
            if (child.name == "Left black eye" || child.name == "Right black eye" || child.name == "Quiet mouth" || child.name == "Slack open mouth" || child.name == "Slack lower lip" || child.name.StartsWith("Sunken ", StringComparison.Ordinal) || child.name.StartsWith("Upper tooth ", StringComparison.Ordinal) || child.name == "Old split on left cheek" || child.name.Contains("expressive eyebrow"))
            {
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }
        Transform detail = Group(Marker, face);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1 : 1;
            string name = i == 0 ? "Left" : "Right";
            Piece(name + " angular sunken socket", detail, new Vector3(side * .053f, .018f, .116f), new Vector3(.064f, .047f, .026f), socket, block, new Vector3(0, side * 7, side * 9));
            Piece(name + " hooded dead eye", detail, new Vector3(side * .052f, .015f, .133f), new Vector3(i == 0 ? .024f : .018f, .011f, .009f), eye, block, new Vector3(0, side * 7, side * 9));
            Piece(name + " heavy slanted brow ridge", detail, new Vector3(side * .054f, .044f, .122f), new Vector3(.071f, .024f, .03f), skinLight, block, new Vector3(0, side * 7, side * 14));
            Piece(name + " hollow under eye", detail, new Vector3(side * .072f, -.013f, .107f), new Vector3(.048f, .035f, .015f), socket, rounded, new Vector3(0, side * 25, -side * 28));
            Piece(name + " angular cheek bone", detail, new Vector3(side * .087f, -.028f, .093f), new Vector3(.057f, .027f, .024f), skinLight, block, new Vector3(0, side * 33, -side * 19));
        }
        Piece("Uneven slack mouth cavity", detail, new Vector3(.003f, -.075f, .111f), new Vector3(.085f, .051f, .023f), socket, block, new Vector3(0, 0, -7));
        Piece("Dried split lower lip", detail, new Vector3(.004f, -.101f, .103f), new Vector3(.074f, .014f, .021f), blood, block, new Vector3(0, 0, -7));
        for (int i = 0; i < 4; i++)
            Piece("Uneven upper tooth " + i, detail, new Vector3(-.026f + i * .016f, -.058f + (i % 2 == 0 ? .002f : -.002f), .127f), new Vector3(.012f, i == 1 ? .015f : .010f, .008f), bone, block, new Vector3(0, 0, i * 6 - 9));
        Piece("Single lower tooth", detail, new Vector3(.015f, -.09f, .123f), new Vector3(.012f, .012f, .009f), bone, block, new Vector3(0, 0, 13));
        Piece("Bony angular chin", detail, new Vector3(.004f, -.116f, .087f), new Vector3(.067f, .027f, .027f), skinLight, block, new Vector3(0, 0, -5));
        Piece("Torn left cheek - burgundy edge", detail, new Vector3(-.10f, -.043f, .079f), new Vector3(.036f, .068f, .014f), blood, ragged, new Vector3(0, -47, -15));
        Piece("Torn left cheek - dark hollow", detail, new Vector3(-.104f, -.043f, .086f), new Vector3(.017f, .044f, .009f), socket, block, new Vector3(0, -47, -15));
        Piece("Torn left cheek - exposed jaw sliver", detail, new Vector3(-.105f, -.057f, .087f), new Vector3(.012f, .024f, .01f), bone, block, new Vector3(0, -47, -24));
        Piece("Matted asymmetrical forelock", detail, new Vector3(-.066f, .101f, .078f), new Vector3(.065f, .057f, .025f), hair, ragged, new Vector3(0, -12, -22));
        Piece("Dark temple hollow", detail, new Vector3(.104f, .052f, .069f), new Vector3(.022f, .053f, .011f), socket, rounded, new Vector3(0, 50, 8));
    }

    static void Workwear(FacilityHumanoid actor)
    {
        if (actor.chest != null)
        {
            Transform detail = Group(BodyMarker, actor.chest);
            // Small collar details leave the removable torso panels and neck stump clear.
            Piece("Ragged left collar", detail, new Vector3(-.094f, .211f, .09f), new Vector3(.105f, .057f, .024f), clothLight, ragged, new Vector3(0, 0, -15));
            Piece("Ragged right collar", detail, new Vector3(.08f, .211f, .09f), new Vector3(.096f, .052f, .024f), clothLight, ragged, new Vector3(0, 0, 19));
        }
        if (actor.forearms != null)
            for (int i = 0; i < actor.forearms.Length; i++)
            {
                Transform limb = actor.forearms[i]; if (limb == null) continue;
                Transform cuff = limb.Find("Sleeve cuff");
                if (cuff != null)
                {
                    MeshFilter filter = cuff.GetComponent<MeshFilter>();
                    if (filter != null) filter.sharedMesh = ragged;
                    cuff.localScale = new Vector3(.145f, i == 0 ? .065f : .075f, .16f);
                    cuff.GetComponent<Renderer>().sharedMaterial = clothLight;
                }
                Transform detail = Group(BodyMarker, limb);
                Piece("Dried blood on work-worn knuckles", detail, new Vector3(i == 0 ? -.015f : .018f, -.321f, .067f), new Vector3(.044f, .041f, .009f), blood, ragged, new Vector3(0, 0, i == 0 ? 18 : -24));
            }
    }

    static void OpenWound(FacilityHitRegion region, int index, bool ribs)
    {
        if (region.outerPatches == null || region.woundBeds == null || index >= region.outerPatches.Length || index >= region.woundBeds.Length) return;
        if (region.outerPatches[index] != null) region.outerPatches[index].enabled = false;
        GameObject wound = region.woundBeds[index]; if (wound == null) return;
        wound.SetActive(true);
        Renderer rim = wound.GetComponent<Renderer>(); if (rim != null) rim.sharedMaterial = blood;
        Transform detail = Group(ribs ? "Infected style - exposed ribs" : "Infected style - exposed forearm bone", wound.transform);
        // Wound beds use normalized panel coordinates. Detail is inside the actual open cavity, not a permanent vest on top.
        if (ribs)
        {
            for (int i = 0; i < 3; i++)
                Piece("Visible broken rib " + i, detail, new Vector3(-.02f, -.29f + i * .25f, -.40f), new Vector3(.61f - i * .045f, .055f, .19f), bone, block, new Vector3(0, 0, i * 5 - 5));
            Piece("Torn cloth at cavity lower edge", detail, new Vector3(.02f, -.47f, .045f), new Vector3(.96f, .12f, .15f), clothLight, ragged, new Vector3(0, 0, 0));
        }
        else Piece("Exposed forearm bone within torn sleeve", detail, new Vector3(.04f, 0, -.40f), new Vector3(.19f, .71f, .25f), bone, block, new Vector3(0, 0, -6));
    }

    static Transform Group(string name, Transform parent)
    {
        Transform group = new GameObject(name).transform; group.SetParent(parent, false); return group;
    }

    static Transform Piece(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Mesh mesh, Vector3 euler)
    {
        Transform piece = new GameObject(name).transform; piece.SetParent(parent, false);
        piece.localPosition = position; piece.localRotation = Quaternion.Euler(euler); piece.localScale = scale;
        piece.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = piece.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        return piece;
    }

    static Material Mat(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .025f);
        material.SetFloat("_EnvironmentReflections", 0); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; EditorUtility.SetDirty(material); return material;
    }

    static Mesh RaggedCloth()
    {
        string path = Root + "/Meshes/Torn angular cloth.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
        Vector2[] outline = { new Vector2(-.5f,.5f), new Vector2(.5f,.5f), new Vector2(.5f,-.22f), new Vector2(.33f,-.5f), new Vector2(.16f,-.3f), new Vector2(-.04f,-.48f), new Vector2(-.2f,-.31f), new Vector2(-.39f,-.5f), new Vector2(-.5f,-.19f) };
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int start = vertices.Count; float z = side == 0 ? .5f : -.5f;
            vertices.Add(new Vector3(0, .07f, z));
            foreach (Vector2 point in outline) vertices.Add(new Vector3(point.x, point.y, z));
            for (int i = 0; i < outline.Length; i++)
            { int a = start + 1 + i, b = start + 1 + (i + 1) % outline.Length; triangles.AddRange(side == 0 ? new[] { start, b, a } : new[] { start, a, b }); }
        }
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Length]; int start = vertices.Count;
            vertices.Add(new Vector3(a.x,a.y,-.5f)); vertices.Add(new Vector3(b.x,b.y,-.5f)); vertices.Add(new Vector3(b.x,b.y,.5f)); vertices.Add(new Vector3(a.x,a.y,.5f));
            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
        mesh = new Mesh { name = "Torn angular cloth", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/'); Folder(path.Substring(0, separator)); AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
    }
}
