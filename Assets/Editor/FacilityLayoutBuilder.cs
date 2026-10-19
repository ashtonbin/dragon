using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Creates the editable, six-area facility shell. All coordinates are local to the generated root.</summary>
public sealed class FacilityLayoutBuilder
{
    const float WallThickness = 0.25f;
    const float SlabThickness = 0.3f;
    readonly Transform root;
    readonly FacilityAssetLibrary assets;
    readonly Dictionary<string, Transform> areas = new Dictionary<string, Transform>();
    readonly HashSet<Vector3> railFeet = new HashSet<Vector3>();
    bool built;

    public FacilityLayoutBuilder(Transform root, FacilityAssetLibrary assets)
    {
        this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
        this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public Transform Area(string name)
    {
        if (areas.TryGetValue(name, out Transform existing))
            return existing;

        Transform area = root.Find(name);
        if (area == null)
        {
            area = new GameObject(name).transform;
            area.SetParent(root, false);
        }
        areas.Add(name, area);
        return area;
    }

    public GameObject Block(Transform parent, string name, Vector3 center, Vector3 size,
        string material, bool solid = true)
    {
        if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Facility block dimensions must be positive.");

        GameObject obj = MeshObject(parent, name, center, assets.BoxMesh(size), material);
        if (solid)
        {
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.size = size;
        }
        return obj;
    }

    public void Build()
    {
        if (built)
            throw new InvalidOperationException("A facility layout builder may generate its layout only once.");
        built = true;
        BuildArrival();
        BuildTransit();
        BuildFactory();
        BuildResearch();
        BuildMaintenance();
        BuildServiceReturn();
    }

    GameObject MeshObject(Transform parent, string name, Vector3 center, Mesh mesh, string material)
    {
        GameObject obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = center;
        obj.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = assets.Material(material);
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        GameObjectUtility.SetStaticEditorFlags(obj,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return obj;
    }

    Transform Group(Transform parent, string name)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    void Groups(string name, out Transform architecture, out Transform infrastructure,
        out Transform props, out Transform signage)
    {
        Transform area = Area(name);
        architecture = Group(area, "Architecture");
        infrastructure = Group(area, "Infrastructure");
        props = Group(area, "Props");
        signage = Group(area, "Signage");
    }

    void Slab(Transform parent, string name, float x0, float x1, float z0, float z1,
        float surface, bool ceiling = false)
    {
        float center = surface + (ceiling ? SlabThickness * 0.5f : -SlabThickness * 0.5f);
        Block(parent, name, new Vector3((x0 + x1) * 0.5f, center, (z0 + z1) * 0.5f),
            new Vector3(x1 - x0, SlabThickness, z1 - z0), ceiling ? "Ceiling" : "Floor");
    }

    void ShellSlabs(Transform parent, string name, float x0, float x1, float z0, float z1,
        float floor, float ceiling)
    {
        Slab(parent, name + " Floor", x0, x1, z0, z1, floor);
        Slab(parent, name + " Ceiling", x0, x1, z0, z1, ceiling, true);
    }

    void WallX(Transform parent, string name, float x0, float x1, float z,
        float bottom, float top, string material = "Concrete")
    {
        if (x1 <= x0 || top <= bottom) return;
        Block(parent, name, new Vector3((x0 + x1) * 0.5f, (bottom + top) * 0.5f, z),
            new Vector3(x1 - x0, top - bottom, WallThickness), material);
    }

    void WallZ(Transform parent, string name, float x, float z0, float z1,
        float bottom, float top, string material = "Concrete")
    {
        if (z1 <= z0 || top <= bottom) return;
        Block(parent, name, new Vector3(x, (bottom + top) * 0.5f, (z0 + z1) * 0.5f),
            new Vector3(WallThickness, top - bottom, z1 - z0), material);
    }

    void DoorX(Transform parent, string name, float x0, float x1, float z, float bottom, float top)
    {
        // Frames sit outside the opening; quoted doorway widths remain unobstructed.
        Block(parent, name + " Left Jamb", new Vector3(x0 - 0.07f, (bottom + top) * 0.5f, z),
            new Vector3(0.14f, top - bottom, 0.36f), "Steel", false);
        Block(parent, name + " Right Jamb", new Vector3(x1 + 0.07f, (bottom + top) * 0.5f, z),
            new Vector3(0.14f, top - bottom, 0.36f), "Steel", false);
        Block(parent, name + " Lintel", new Vector3((x0 + x1) * 0.5f, top + 0.07f, z),
            new Vector3(x1 - x0 + 0.28f, 0.14f, 0.36f), "Steel", false);
        Block(parent, name + " Painted Threshold", new Vector3((x0 + x1) * 0.5f, bottom + 0.008f, z),
            new Vector3(x1 - x0, 0.012f, 0.3f), "Hazard", false);
    }

    void DoorZ(Transform parent, string name, float x, float z0, float z1, float bottom, float top)
    {
        Block(parent, name + " South Jamb", new Vector3(x, (bottom + top) * 0.5f, z0 - 0.07f),
            new Vector3(0.36f, top - bottom, 0.14f), "Steel", false);
        Block(parent, name + " North Jamb", new Vector3(x, (bottom + top) * 0.5f, z1 + 0.07f),
            new Vector3(0.36f, top - bottom, 0.14f), "Steel", false);
        Block(parent, name + " Lintel", new Vector3(x, top + 0.07f, (z0 + z1) * 0.5f),
            new Vector3(0.36f, 0.14f, z1 - z0 + 0.28f), "Steel", false);
        Block(parent, name + " Painted Threshold", new Vector3(x, bottom + 0.008f, (z0 + z1) * 0.5f),
            new Vector3(0.3f, 0.012f, z1 - z0), "Hazard", false);
    }

    void Rail(Transform parent, string name, Vector3 start, Vector3 end, bool stairs = false)
    {
        Vector3 delta = end - start;
        float horizontal = new Vector2(delta.x, delta.z).magnitude;
        int divisions = Mathf.Max(1, Mathf.CeilToInt(horizontal / 2.2f));
        for (int i = 0; i <= divisions; i++)
        {
            Vector3 foot = Vector3.Lerp(start, end, i / (float)divisions);
            float handrailTop = foot.y + 1.3f;
            if (stairs) foot.y = Mathf.Clamp((Mathf.Floor((foot.z - 19f) / .55f) + 1f) * .2f, .2f, 3f);
            if (!railFeet.Add(foot)) continue; // Shared corners use one welded post and base.
            if (stairs || Mathf.Abs(delta.y) < .001f)
            {
                Block(parent, name + " Base Plate " + i, foot + Vector3.up * .015f,
                    new Vector3(.18f, .03f, stairs ? .10f : .14f), "Steel", false);
                foot.y += .03f;
            }
            else foot.y -= .025f; // Weld the upright into the sloping deck, leaving no underside wedge.
            float postHeight = handrailTop - foot.y;
            Block(parent, name + " Post " + i, foot + Vector3.up * (postHeight * .5f),
                new Vector3(0.085f, postHeight, 0.085f), "Steel", false);
        }
        foreach (float height in new[] { 0.65f, 1.3f })
        {
            GameObject bar = Block(parent, name + (height > 1f ? " Handrail" : " Midrail"),
                (start + end) * 0.5f + Vector3.up * height,
                new Vector3(0.085f, 0.085f, delta.magnitude), "Steel");
            bar.transform.localRotation = Quaternion.LookRotation(delta, Vector3.up);
        }
    }

    void Hanger(Transform parent, string name, Vector3 foot, float ceiling)
    {
        Block(parent, name + " Hanger", new Vector3(foot.x, (foot.y + ceiling) * .5f, foot.z),
            new Vector3(.08f, ceiling - foot.y, .08f), "Steel", false);
        Block(parent, name + " Ceiling Mount", new Vector3(foot.x, ceiling - .03f, foot.z),
            new Vector3(.24f, .06f, .24f), "Steel", false);
    }

    void Sign(Transform parent, string name, string text, Vector3 center, float width,
        float height = 0.6f, float yaw = 0f)
    {
        Transform sign = Group(parent, name);
        sign.localPosition = center;
        sign.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Block(sign, "Painted Steel Backing", Vector3.zero, new Vector3(width, height, 0.055f), "Steel", false);
        GameObject label = new GameObject("Lettering", typeof(TextMesh));
        label.transform.SetParent(sign, false);
        label.transform.localPosition = new Vector3(0f, 0f, -0.035f);
        TextMesh mesh = label.GetComponent<TextMesh>();
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 64;
        mesh.characterSize = 0.075f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(0.77f, 0.83f, 0.78f);
        mesh.text = text;
        mesh.font.RequestCharactersInTexture(text, mesh.fontSize, mesh.fontStyle);
        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = assets.Material("SignText");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Bounds lettering = renderer.localBounds;
        if (lettering.size.x > 0.0001f && lettering.size.y > 0.0001f)
        {
            float fit = Mathf.Min(width * 0.9f / lettering.size.x, height * 0.75f / lettering.size.y);
            if (!float.IsNaN(fit) && !float.IsInfinity(fit) && fit > 0f)
            {
                label.transform.localScale = Vector3.one * fit;
                label.transform.localPosition = new Vector3(-lettering.center.x * fit,
                    -lettering.center.y * fit, -0.035f);
            }
        }
    }

    void Cabinet(Transform parent, string name, Vector3 center, Vector3 size, float yaw = 0f)
    {
        Transform cabinet = Group(parent, name);
        cabinet.localPosition = center;
        cabinet.localRotation = Quaternion.Euler(0, yaw, 0);
        parent = cabinet;
        center = Vector3.zero;
        Block(parent, name, center, size, "Steel");
        Block(parent, name + " Door", center + new Vector3(0f, 0f, -size.z * 0.5f - 0.018f),
            new Vector3(size.x * 0.88f, size.y * 0.87f, 0.035f), "Rust", false);
        for (int i = 0; i < 3; i++)
            Block(parent, name + " Vent " + i,
                center + new Vector3(0f, size.y * 0.16f + i * 0.12f, -size.z * 0.5f - 0.04f),
                new Vector3(size.x * 0.57f, 0.035f, 0.018f), "Ceiling", false);
    }

    void BuildArrival()
    {
        Groups("01_Arrival", out Transform a, out Transform i, out Transform p, out Transform s);
        ShellSlabs(a, "Arrival", -5, 5, -5, 5, 0, 4.2f);
        WallX(a, "South Wall", -5, 5, -5, 0, 4.2f);
        WallZ(a, "East Wall", 5, -5, 5, 0, 4.2f);
        WallX(a, "North West Wall", -5, -2, 5, 0, 4.2f);
        WallX(a, "North East Wall", 2, 5, 5, 0, 4.2f);
        WallX(a, "Transit Header", -2, 2, 5, 3, 4.2f);
        WallZ(a, "West South Wall", -5, -5, -2, 0, 4.2f);
        WallZ(a, "West North Wall", -5, 2, 5, 0, 4.2f);
        WallZ(a, "Service Header", -5, -2, 2, 3, 4.2f);
        DoorX(i, "Transit Portal", -2, 2, 5, 0, 3);
        DoorZ(i, "Service Portal", -5, -2, 2, 0, 3);
        for (int j = 0; j < 3; j++)
            Block(i, "Ceiling Rib " + j, new Vector3(0, 4.1f, -3.5f + j * 3.5f),
                new Vector3(9.75f, 0.2f, 0.25f), "Steel", false);
        Block(i, "Cable Trunk", new Vector3(4.805f, 2.8f, 0), new Vector3(0.14f, 0.18f, 9), "Rust", false);
        Cabinet(p, "Personnel Locker A", new Vector3(3.4f, 1.05f, -4.525f), new Vector3(1, 2.1f, 0.7f), 180);
        Cabinet(p, "Personnel Locker B", new Vector3(2.25f, 1.05f, -4.525f), new Vector3(1, 2.1f, 0.7f), 180);
        Block(p, "Arrival Bench Seat", new Vector3(-2.8f, .48f, -4.5f), new Vector3(2.4f, .12f, .65f), "Rust");
        foreach (float x in new[] { -3.65f, -1.95f })
            Block(p, "Arrival Bench Leg " + x, new Vector3(x, .21f, -4.5f), new Vector3(.12f, .42f, .55f), "Steel");
        Block(p, "Arrival Bench Wall Back", new Vector3(-2.8f, .9f, -4.8f), new Vector3(2.4f, .45f, .15f), "Rust", false);
        Block(p, "Wall Notice", new Vector3(4.855f, 1.9f, -1.4f), new Vector3(0.04f, 0.95f, 1.2f), "Hazard", false);
    }

    void BuildTransit()
    {
        Groups("02_Transit", out Transform a, out Transform i, out Transform p, out Transform s);
        // Abutting rectangles cover the dogleg once, including both turning squares.
        ShellSlabs(a, "South Leg", -2, 2, 5, 13, 0, 4.2f);
        ShellSlabs(a, "Cross Leg", 2, 8, 9, 13, 0, 4.2f);
        ShellSlabs(a, "North Leg", 4, 8, 13, 17, 0, 4.2f);
        WallZ(a, "West Leg Wall", -2, 5, 13, 0, 4.2f);
        WallZ(a, "Inner South Wall", 2, 5, 9, 0, 4.2f);
        WallX(a, "Cross Leg South Wall", 2, 8, 9, 0, 4.2f);
        WallX(a, "Cross Leg North Wall", -2, 4, 13, 0, 4.2f);
        WallZ(a, "East Leg Wall", 8, 9, 17, 0, 4.2f);
        WallZ(a, "Inner North Wall", 4, 13, 17, 0, 4.2f);
        Block(i, "South Leg Overhead Pipe", new Vector3(-1.5f, 3.3f, 8.8125f), new Vector3(0.15f, 0.15f, 7.375f), "Rust", false);
        Block(i, "Bend Overhead Pipe", new Vector3(3, 3.3f, 12.5f), new Vector3(9, 0.15f, 0.15f), "Rust", false);
        Block(i, "North Leg Overhead Pipe", new Vector3(7.5f, 3.3f, 14.6875f), new Vector3(0.15f, 0.15f, 4.375f), "Rust", false);
        foreach (float z in new[] { 6f, 9f, 12f }) Hanger(i, "South Pipe " + z, new Vector3(-1.5f, 3.375f, z), 4.2f);
        Hanger(i, "Cross Pipe", new Vector3(4.5f, 3.375f, 12.5f), 4.2f);
        Hanger(i, "North Pipe", new Vector3(7.5f, 3.375f, 15.5f), 4.2f);
        for (int j = 0; j < 2; j++)
        {
            float z = 7 + j * 4;
            Block(i, "West Wall Pilaster " + j, new Vector3(-1.785f, 2.1f, z), new Vector3(0.18f, 4.2f, 0.28f), "Steel", false);
        }
        Block(p, "Abandoned Wall Junction Box", new Vector3(7.8f, 1.5f, 14.5f), new Vector3(0.16f, 0.85f, 0.7f), "Steel", false);
    }

    void BuildFactory()
    {
        Groups("03_Factory", out Transform a, out Transform i, out Transform p, out Transform s);
        ShellSlabs(a, "Factory", -6, 18, 17, 35, 0, 8);
        WallX(a, "South West Wall", -6, 4, 17, 0, 8);
        WallX(a, "South East Wall", 8, 18, 17, 0, 8);
        WallX(a, "Transit Entrance Header", 4, 8, 17, 3, 8);
        WallZ(a, "East Wall", 18, 17, 35, 0, 8);
        WallZ(a, "West South Wall", -6, 17, 20, 0, 8);
        WallZ(a, "West North Wall", -6, 24, 35, 0, 8);
        WallZ(a, "Maintenance Header", -6, 20, 24, 3, 8);
        WallX(a, "North East Wall", 2, 18, 35, 0, 8);
        // Factory owns both shared partitions with the elevated L-shaped laboratory.
        WallX(a, "North Mezzanine Base", -6, 2, 35, 0, 3);
        WallX(a, "Observation Sill", -6, -1.5f, 35, 3, 4.05f);
        WallX(a, "Observation Upper Wall", -6, -1.5f, 35, 5.7f, 8);
        WallX(a, "Observation Left Pier", -6, -5.5f, 35, 4.05f, 5.7f);
        WallX(a, "Observation Right Pier", -2, -1.5f, 35, 4.05f, 5.7f);
        WallX(a, "Research East Jamb Wall", 1.5f, 2, 35, 3, 8);
        WallX(a, "Research Header", -1.5f, 1.5f, 35, 6, 8);
        GameObject glazing = Block(a, "Observation Glazing", new Vector3(-3.75f, 4.875f, 35),
            new Vector3(3.5f, 1.65f, 0.06f), "Glass");
        GameObjectUtility.SetStaticEditorFlags(glazing,
            GameObjectUtility.GetStaticEditorFlags(glazing) & ~StaticEditorFlags.ContributeGI);
        MeshRenderer glazingRenderer = glazing.GetComponent<MeshRenderer>();
        glazingRenderer.receiveGI = ReceiveGI.LightProbes;
        glazingRenderer.shadowCastingMode = ShadowCastingMode.Off;
        Slab(a, "Mezzanine Floor", -6, 2, 29, 35, 3);
        Slab(a, "Stair Top Landing", -1.5f, 1.5f, 27.25f, 29, 3);
        for (int step = 0; step < 15; step++)
        {
            float height = (step + 1) * 0.2f;
            Block(a, "Stair Tread " + (step + 1).ToString("00"),
                new Vector3(0, height * 0.5f, 19 + (step + 0.5f) * 0.55f),
                new Vector3(3, height, 0.55f), "Steel");
        }
        DoorX(i, "Factory Entrance", 4, 8, 17, 0, 3);
        DoorZ(i, "Maintenance Shortcut", -6, 20, 24, 0, 3);
        DoorX(i, "Research Entrance", -1.5f, 1.5f, 35, 3, 6);
        Rail(i, "Stair West Guard", new Vector3(-1.37f, .2f, 19.14f), new Vector3(-1.37f, 3, 27.14f), true);
        Rail(i, "Stair East Guard", new Vector3(1.37f, .2f, 19.14f), new Vector3(1.37f, 3, 27.14f), true);
        Rail(i, "Landing West Guard", new Vector3(-1.37f, 3, 27.14f), new Vector3(-1.37f, 3, 29.12f));
        Rail(i, "Landing East Guard", new Vector3(1.37f, 3, 27.14f), new Vector3(1.37f, 3, 29.12f));
        Rail(i, "Mezzanine Front West Guard", new Vector3(-5.8f, 3, 29.12f), new Vector3(-1.37f, 3, 29.12f));
        Rail(i, "Mezzanine Front East Guard", new Vector3(1.37f, 3, 29.12f), new Vector3(1.87f, 3, 29.12f));
        Rail(i, "Mezzanine East Guard", new Vector3(1.87f, 3, 29.12f), new Vector3(1.87f, 3, 34.8f));
        for (int j = 0; j < 3; j++)
        {
            float z = 19.5f + j * 6.5f;
            Block(i, "Roof Truss " + j, new Vector3(6, 7.775f, z), new Vector3(23.75f, 0.45f, 0.32f), "Steel", false);
            Block(i, "East Wall Column " + j, new Vector3(17.65f, 3.775f, z), new Vector3(0.45f, 7.55f, 0.45f), "Steel");
        }
        Block(i, "High Service Duct", new Vector3(15.6f, 6.9f, 26), new Vector3(0.8f, 0.65f, 17.75f), "Steel", false);
        foreach (float z in new[] { 18f, 23f, 28f, 34f }) Hanger(i, "Factory Duct " + z, new Vector3(15.6f, 7.225f, z), 8);
        Block(p, "Reactor Plinth", new Vector3(9, 0.35f, 26.5f), new Vector3(6.2f, 0.7f, 4), "Steel");
        Block(p, "Reactor Main Housing", new Vector3(9, 1.8f, 26.5f), new Vector3(5.2f, 2.2f, 3.2f), "Rust");
        Block(p, "Reactor Cap", new Vector3(9, 3.1f, 26.5f), new Vector3(5.8f, 0.4f, 3.6f), "Steel");
        for (int j = 0; j < 4; j++)
            Block(p, "Reactor Cooling Fin " + j, new Vector3(7.2f + j * 1.2f, 2, 24.82f),
                new Vector3(0.22f, 1.75f, 0.18f), "Steel", false);
        Block(p, "Machine Status Window", new Vector3(9, 2.25f, 24.72f), new Vector3(1.35f, 0.25f, 0.035f), "WarmGlow", false);
        Cabinet(p, "Far East Switchgear", new Vector3(17.325f, 1.3f, 33.7f), new Vector3(1.8f, 2.6f, 1.1f), 90);
        Block(i, "Observation Lower Frame", new Vector3(-3.75f, 4.02f, 34.92f), new Vector3(3.7f, 0.12f, 0.16f), "Steel", false);
        Block(i, "Observation Upper Frame", new Vector3(-3.75f, 5.73f, 34.92f), new Vector3(3.7f, 0.12f, 0.16f), "Steel", false);
        for (int j = 0; j < 5; j++)
            Block(i, "Stair Nosing " + j, new Vector3(0, (j * 3 + 1) * 0.2f + 0.007f, 19.06f + j * 1.65f),
                new Vector3(2.95f, 0.012f, 0.08f), "Hazard", false);
    }

    void BuildResearch()
    {
        Groups("04_Research", out Transform a, out Transform i, out Transform p, out Transform s);
        ShellSlabs(a, "North Laboratory", -14, 2, 35, 43, 3, 7.2f);
        ShellSlabs(a, "West Laboratory Alcove", -14, -6, 29, 35, 3, 7.2f);
        WallZ(a, "West Wall", -14, 29, 43, 3, 7.2f);
        WallX(a, "North Wall", -14, 2, 43, 3, 7.2f);
        WallZ(a, "East Wall", 2, 35, 43, 3, 7.2f);
        WallX(a, "Ramp West Pier", -14, -13.5f, 29, 3, 7.2f);
        WallX(a, "Ramp East Wall", -10.5f, -6, 29, 3, 7.2f);
        WallX(a, "Ramp Entrance Header", -13.5f, -10.5f, 29, 6, 7.2f);
        DoorX(i, "Ramp Entrance", -13.5f, -10.5f, 29, 3, 6);
        Block(i, "Laboratory Ceiling Rib", new Vector3(-6, 7.1f, 39), new Vector3(15.75f, 0.2f, 0.3f), "Steel", false);
        Block(i, "Lab Overhead Cable Tray", new Vector3(-13.5f, 6.3f, 36), new Vector3(0.3f, 0.12f, 13.75f), "Rust", false);
        foreach (float z in new[] { 30f, 34f, 38f, 42f }) Hanger(i, "Lab Tray " + z, new Vector3(-13.5f, 6.36f, z), 7.2f);
        // The clear south aisle links the observation door to the ramp without crossing furniture.
        for (int j = 0; j < 2; j++)
        {
            float x = -11 + j * 5;
            Block(p, "Research Bench " + j, new Vector3(x, 3.55f, 42.15f), new Vector3(3.4f, 1.1f, 1.3f), "Steel");
            Block(p, "Research Worktop " + j, new Vector3(x, 4.16f, 42.15f), new Vector3(3.6f, 0.12f, 1.45f), "Concrete", false);
            Block(p, "Monitor Base " + j, new Vector3(x - .5f, 4.25f, 42.35f), new Vector3(.55f, .06f, .4f), "Steel", false);
            Block(p, "Monitor Stem " + j, new Vector3(x - .5f, 4.37f, 42.35f), new Vector3(.12f, .18f, .12f), "Steel", false);
            Block(p, "Research Monitor " + j, new Vector3(x - 0.5f, 4.8f, 42.35f), new Vector3(0.95f, 0.8f, 0.18f), "Steel", false);
            Block(p, "Monitor Face " + j, new Vector3(x - 0.5f, 4.8f, 42.245f), new Vector3(0.75f, 0.57f, 0.025f), "ColdGlow", false);
        }
        Cabinet(p, "Specimen Cold Storage", new Vector3(-6.725f, 4.15f, 31.55f), new Vector3(1.25f, 2.3f, 1.2f), 90);
        Sign(s, "Specimen Warning", "SAMPLES REMOVED", new Vector3(-6.2f, 5.2f, 31.6f), 2.1f, 0.45f, 90);
    }

    void BuildMaintenance()
    {
        Groups("05_Maintenance", out Transform a, out Transform i, out Transform p, out Transform s);
        ShellSlabs(a, "Maintenance", -18, -6, 11, 25, 0, 5.5f);
        WallZ(a, "West Wall", -18, 11, 25, 0, 5.5f);
        // Factory owns the east boundary north of z17; no duplicate shared wall.
        WallZ(a, "East South Wall", -6, 11, 17, 0, 5.5f);
        WallX(a, "South West Wall", -18, -14, 11, 0, 5.5f);
        WallX(a, "South East Wall", -10, -6, 11, 0, 5.5f);
        WallX(a, "Service Header", -14, -10, 11, 3, 5.5f);
        WallX(a, "North West Wall", -18, -14, 25, 0, 5.5f);
        WallX(a, "North East Wall", -10, -6, 25, 0, 5.5f);
        WallX(a, "Ramp High Header", -14, -10, 25, 4.8f, 5.5f);
        WallZ(a, "Ramp Connector West Wall", -14, 25, 29, 0, 7.2f);
        WallZ(a, "Ramp Connector East Wall", -10, 25, 29, 0, 7.2f);
        Slab(a, "Ramp Connector Ceiling", -14, -10, 25, 29, 7.2f, true);
        Mesh rampMesh = assets.RampMesh(4, 6, 3);
        GameObject ramp = MeshObject(a, "Research Access Ramp", new Vector3(-12, 0, 23), rampMesh, "Rust");
        ramp.AddComponent<MeshCollider>().sharedMesh = rampMesh;
        DoorX(i, "Service Return Portal", -14, -10, 11, 0, 3);
        Rail(i, "Ramp West Guard", new Vector3(-13.75f, .06f, 23.12f), new Vector3(-13.75f, 2.94f, 28.88f));
        Rail(i, "Ramp East Guard", new Vector3(-10.25f, .06f, 23.12f), new Vector3(-10.25f, 2.94f, 28.88f));
        for (int j = 0; j < 3; j++)
            Block(i, "Overhead Steam Pipe " + j, new Vector3(-17 + j * 4.5f, 4.85f, 18), new Vector3(0.2f, 0.2f, 13.75f), "Rust", false);
        Block(i, "Maintenance Cross Beam", new Vector3(-12, 5.36f, 17.8f), new Vector3(11.75f, 0.28f, 0.3f), "Steel", false);
        for (int pipe = 0; pipe < 3; pipe++)
            foreach (float z in new[] { 12f, 17.8f, 24f })
                Hanger(i, "Steam Pipe " + pipe + " " + z, new Vector3(-17 + pipe * 4.5f, 4.95f, z), 5.5f);
        Block(p, "Emergency Generator Base", new Vector3(-16f, 0.3f, 18.5f), new Vector3(2.5f, 0.6f, 3.3f), "Steel");
        Block(p, "Emergency Generator Housing", new Vector3(-16f, 1.3f, 18.5f), new Vector3(2.2f, 1.4f, 2.9f), "Rust");
        Block(p, "Emergency Generator Cap", new Vector3(-16f, 2.12f, 18.5f), new Vector3(2.35f, 0.24f, 3), "Steel");
        for (int j = 0; j < 4; j++)
            Block(p, "Generator Vent " + j, new Vector3(-16f, 1.15f + j * 0.17f, 17.03f), new Vector3(1.55f, 0.055f, 0.035f), "Ceiling", false);
        Block(p, "Generator Power Indicator", new Vector3(-16f, 1.85f, 17.01f), new Vector3(0.32f, 0.12f, 0.04f), "RedGlow", false);
        Block(p, "Maintenance Workbench", new Vector3(-16.65f, 0.48f, 14.5f), new Vector3(1.6f, 0.96f, 3), "Steel");
        Block(p, "Workbench Surface", new Vector3(-16.65f, 1.02f, 14.5f), new Vector3(1.7f, 0.12f, 3.1f), "Rust", false);
        Block(p, "Tool Case", new Vector3(-16.65f, 1.23f, 14.8f), new Vector3(0.7f, 0.3f, 1), "Hazard", false);
        Cabinet(p, "Maintenance Stores", new Vector3(-17.45f, 1.2f, 23.8f), new Vector3(1.7f, 2.4f, 0.85f), -90);
        for (int j = 0; j < 5; j++)
            Block(i, "Generator Floor Marking " + j, new Vector3(-17.4f + j * 0.7f, 0.008f, 20.5f), new Vector3(0.35f, 0.012f, 0.18f), "Hazard", false);
    }

    void BuildServiceReturn()
    {
        Groups("06_ServiceReturn", out Transform a, out Transform i, out Transform p, out Transform s);
        ShellSlabs(a, "Long Service Leg", -14, -10, -2, 11, 0, 3.4f);
        ShellSlabs(a, "Intake Service Leg", -10, -5, -2, 2, 0, 3.4f);
        WallZ(a, "West Wall", -14, -2, 11, 0, 3.4f);
        WallZ(a, "Inner East Wall", -10, 2, 11, 0, 3.4f);
        WallX(a, "South Wall", -14, -5, -2, 0, 3.4f);
        WallX(a, "Inner North Wall", -10, -5, 2, 0, 3.4f);
        // Arrival and maintenance own the two terminal doorway walls.
        Block(i, "Long Cable Raceway", new Vector3(-13.815f, 2.65f, 4.5f), new Vector3(0.12f, 0.14f, 12.6f), "Steel", false);
        Block(i, "Return Pipe", new Vector3(-11.5f, 3.1f, 5.0375f), new Vector3(0.16f, 0.16f, 11.675f), "Rust", false);
        Block(i, "Return Bend Pipe", new Vector3(-8.3125f, 3.1f, -0.8f), new Vector3(6.375f, 0.16f, 0.16f), "Rust", false);
        foreach (float z in new[] { 0f, 4f, 8f, 10.5f }) Hanger(i, "Service Pipe " + z, new Vector3(-11.5f, 3.18f, z), 3.4f);
        Hanger(i, "Return Bend", new Vector3(-9f, 3.18f, -.8f), 3.4f);
        for (int j = 0; j < 3; j++)
            Block(i, "Service Ceiling Brace " + j, new Vector3(-12, 3.32f, 1 + j * 4), new Vector3(3.75f, 0.16f, 0.22f), "Steel", false);
        Block(p, "Wall Pressure Station", new Vector3(-13.635f, 1.1f, 3.3f), new Vector3(0.48f, 0.85f, 0.7f), "Steel");
        Block(p, "Pressure Station Face", new Vector3(-13.3775f, 1.25f, 3.3f), new Vector3(0.035f, 0.25f, 0.35f), "Hazard", false);
    }
}
