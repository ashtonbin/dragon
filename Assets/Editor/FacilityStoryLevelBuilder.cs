using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit authoring of the first story level. Every visible part is an ordinary saved scene object.</summary>
public sealed class FacilityStoryLevelBuilder
{
    const string AssetRoot = "Assets/Generated/Story";
    readonly FacilityAssetLibrary facility = new FacilityAssetLibrary();
    readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    readonly List<Light> alarmLights = new List<Light>();
    Transform root;
    Material concrete, floor, steel, dark, white, rust, wood, coldGlow, warmGlow, redGlow, glass, gold, paper;

    public static GameObject Build(RetroFPSController player, FacilityStoryDirector director)
    {
        if (player == null || director == null) throw new ArgumentNullException("The story level needs its preserved player and director.");
        return new FacilityStoryLevelBuilder().BuildLevel(player, director);
    }

    /// <summary>Add the watch office's dedicated, ceiling-mounted fixture without rebuilding the map.</summary>
    public static Transform BuildOfficeFixture(Transform office)
    {
        Transform existing = office.Find("Watch office ceiling fluorescent");
        if (existing != null) return existing;
        var builder = new FacilityStoryLevelBuilder();
        builder.dark = builder.Mat("Black enamel", new Color(.045f, .055f, .065f));
        builder.steel = builder.Mat("Painted steel", new Color(.27f, .34f, .35f), true);
        Material diffuser = builder.Mat("Cold fluorescent diffuser", new Color(.45f, .73f, .80f), false, true);
        builder.Fixture(office, "Watch office ceiling fluorescent", new Vector3(-3.8f, 3.4f, 10.4f), diffuser, new Color(.59f, .74f, .88f), 5.5f, 7.5f);
        return office.Find("Watch office ceiling fluorescent");
    }

    GameObject BuildLevel(RetroFPSController player, FacilityStoryDirector director)
    {
        foreach (string folder in new[] { AssetRoot, AssetRoot + "/Meshes", AssetRoot + "/Materials", AssetRoot + "/Textures", AssetRoot + "/Audio" }) Folder(folder);
        concrete = Mat("Weathered concrete", new Color(.43f, .46f, .46f), true);
        floor = Mat("Worn charcoal floor", new Color(.19f, .23f, .24f), true);
        steel = Mat("Painted steel", new Color(.27f, .34f, .35f), true);
        dark = Mat("Black enamel", new Color(.045f, .055f, .065f));
        white = Mat("Aged ivory", new Color(.62f, .64f, .59f), true);
        rust = Mat("Oxidized iron", new Color(.34f, .20f, .12f), true);
        wood = Mat("Reception walnut", new Color(.23f, .13f, .075f), true);
        coldGlow = Mat("Cold fluorescent diffuser", new Color(.45f, .73f, .80f), false, true);
        warmGlow = Mat("Warm tungsten diffuser", new Color(.92f, .55f, .21f), false, true);
        redGlow = Mat("Alarm red lens", new Color(.80f, .045f, .018f), false, true);
        glass = Mat("Opaque midnight glazing", new Color(.035f, .09f, .12f));
        gold = Mat("Bullion gold", new Color(1f, .84f, .24f));
        gold.SetColor("_BaseColor", new Color(1f, .84f, .24f));
        gold.SetFloat("_Metallic", .05f); gold.SetFloat("_Smoothness", .22f);
        gold.EnableKeyword("_EMISSION"); gold.SetColor("_EmissionColor", new Color(.18f, .135f, .028f));
        // URP validates _EMISSION against AnyEmissive. Realtime GI is disabled in the story settings,
        // so this flag preserves the visual floor without adding baked illumination.
        gold.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(gold);
        paper = Mat("Banknote bundles", new Color(.38f, .45f, .28f), true);

        root = Group(null, "Story Level 1 - Night Deposit");
        Parking(player, director);
        Lobby(player, director);
        Security();
        Records();
        Vault(director);
        Infrastructure(director);
        Cast(player, director);
        director.player = player;
        AssetDatabase.SaveAssets();
        return root.gameObject;
    }

    void Parking(RetroFPSController player, FacilityStoryDirector director)
    {
        Transform area = Group(root, "01 Night parking - sedan and entrance");
        Material asphalt = Mat("Coarse asphalt", new Color(.115f, .135f, .15f), true);
        Material line = Mat("Faded parking paint", new Color(.45f, .45f, .35f));
        Box(area, "Continuous parking surface", new Vector3(0, -.16f, -16), new Vector3(34, .32f, 25), asphalt);
        Box(area, "Flush entrance pavement", new Vector3(0, -.1f, -5.5f), new Vector3(16, .2f, 3), concrete);
        for (int i = 0; i < 6; i++)
        {
            float x = -14f + i * 4f;
            Box(area, "Painted parking division", new Vector3(x, .007f, -19), new Vector3(.075f, .012f, 6), line, false);
            Box(area, "Concrete wheel stop", new Vector3(x + 2, .09f, -15.9f), new Vector3(2.3f, .18f, .23f), concrete);
        }
        Fence(area, -16.5f, -28, -4, false);
        Fence(area, 16.5f, -28, -4, false);
        Fence(area, -28, -16.5f, 16.5f, true);
        Fence(area, -4, -16.5f, -12, true);
        Fence(area, -4, 12, 16.5f, true);
        // A continuous exterior facade closes every room boundary; only the central entrance is open.
        WallZ(area, "South facade", -4, -12, 12, 4.8f, -1.65f, 1.65f);
        Box(area, "Front parapet coping", new Vector3(0, 4.85f, -4), new Vector3(24.4f, .22f, .65f), steel);
        Box(area, "Entrance canopy roof", new Vector3(0, 3.4f, -5.25f), new Vector3(5.2f, .22f, 2.6f), steel);
        Box(area, "Canopy fascia", new Vector3(0, 3.23f, -6.5f), new Vector3(5.2f, .22f, .12f), dark, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(area, "Canopy support", new Vector3(side * 2.42f, 1.65f, -6.3f), new Vector3(.16f, 3.3f, .16f), steel);
            Box(area, "Entrance steel jamb", new Vector3(side * 1.68f, 1.5f, -4), new Vector3(.12f, 3, .42f), dark);
            Box(area, "Facade recessed window", new Vector3(side * 4.6f, 1.9f, -4.135f), new Vector3(2.5f, 1.8f, .028f), glass, false);
            Box(area, "Window sill", new Vector3(side * 4.6f, .98f, -4.2f), new Vector3(2.66f, .12f, .22f), steel, false);
        }
        Fixture(area, "Mounted entrance strip", new Vector3(0, 3.25f, -5.25f), coldGlow, new Color(.48f, .70f, 1), 7.5f, 9);
        // The door leaf is parked against the inside wall, physically supported on two hinges.
        Box(area, "Open entrance door", new Vector3(-1.56f, 1.45f, -2.6f), new Vector3(.09f, 2.9f, 2.8f), dark);
        Box(area, "Open door inset glazing", new Vector3(-1.50f, 1.7f, -2.6f), new Vector3(.025f, 1.8f, 2.25f), glass, false);
        for (int i = 0; i < 2; i++) Box(area, "Door hinge", new Vector3(-1.66f, .45f + i * 1.8f, -3.9f), new Vector3(.15f, .18f, .18f), steel, false);
        Transform lamp = Group(area, "Parking lamp on concrete footing");
        Box(lamp, "Concrete footing", new Vector3(-12, .3f, -13), new Vector3(.65f, .6f, .65f), concrete);
        Box(lamp, "Lamp pole", new Vector3(-12, 2.5f, -13), new Vector3(.16f, 4.4f, .16f), steel);
        Box(lamp, "Lamp arm", new Vector3(-11.45f, 4.65f, -13), new Vector3(1.25f, .12f, .12f), steel, false);
        Fixture(lamp, "Parking sodium lamp", new Vector3(-10.9f, 4.6f, -13), warmGlow, new Color(1, .58f, .26f), 20, 20);
        Transform sedan = Sedan(area);
        director.sedanExit = Interactable(sedan, "Sedan extraction point", new Vector3(1.8f, .85f, 0), new Vector3(.6f, 1.5f, 1.8f), director, FacilityStoryInteractionKind.Sedan, "Get into the driver's seat", null);
        Transform spawn = Group(root, "Story spawn - beside sedan driver door");
        spawn.position = new Vector3(-6, .05f, -18);
        spawn.rotation = Quaternion.Euler(0, 13, 0);
        director.spawnPoint = spawn;
        bool enabled = player.GetComponent<CharacterController>().enabled;
        player.GetComponent<CharacterController>().enabled = false;
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        player.GetComponent<CharacterController>().enabled = enabled;
    }

    Transform Sedan(Transform area)
    {
        Transform car = Group(area, "Black sedan - extraction vehicle"); car.position = new Vector3(-8.05f, 0, -18);
        Material paint = Mat("Sedan black paint", new Color(.055f, .065f, .075f));
        paint.SetColor("_BaseColor", new Color(.055f, .065f, .075f));
        paint.EnableKeyword("_EMISSION"); paint.SetColor("_EmissionColor", new Color(.006f, .008f, .011f));
        paint.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; EditorUtility.SetDirty(paint);
        Material rubber = Mat("Tire rubber", new Color(.028f, .031f, .034f));
        Material chrome = Mat("Dull chrome", new Color(.37f, .40f, .42f));
        Box(car, "Chassis", new Vector3(0, .38f, 0), new Vector3(1.65f, .27f, 4.45f), dark);
        Box(car, "Lower coachwork", new Vector3(0, .68f, 0), new Vector3(1.88f, .45f, 4.7f), paint);
        Box(car, "Long hood", new Vector3(0, .94f, 1.43f), new Vector3(1.84f, .15f, 1.6f), paint);
        Box(car, "Trunk lid", new Vector3(0, .93f, -1.68f), new Vector3(1.84f, .17f, 1.05f), paint);
        MeshObject(car, "Sloped cabin shell", Vector3.zero, SedanCabinMesh(), paint, true);
        // Separate dark glazing follows the cabin slopes rather than floating rectangular panels.
        MeshObject(car, "Cabin glazing", Vector3.zero, SedanGlassMesh(), glass);
        Box(car, "Roof skin", new Vector3(0, 1.52f, -.12f), new Vector3(1.62f, .065f, 1.62f), paint, false);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int axle = -1; axle <= 1; axle += 2)
            {
                GameObject tire = MeshObject(car, "Twelve sided tire", new Vector3(side * .94f, .39f, axle * 1.48f), CylinderMesh("Sedan tire", .38f, .22f, 12), rubber);
                tire.transform.localRotation = Quaternion.Euler(0, 0, 90);
                GameObject hub = MeshObject(car, "Recessed steel wheel", new Vector3(side * 1.058f, .39f, axle * 1.48f), CylinderMesh("Sedan hub", .235f, .026f, 10), chrome);
                hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            Box(car, "Side rubbing strip", new Vector3(side * .953f, .67f, 0), new Vector3(.028f, .055f, 3.9f), dark, false);
            Box(car, "Rear door handle", new Vector3(side * .957f, .96f, -.83f), new Vector3(.035f, .045f, .19f), chrome, false);
            Box(car, "Front door handle", new Vector3(side * .957f, .96f, .23f), new Vector3(.035f, .045f, .19f), chrome, false);
            Box(car, "Wing mirror stalk", new Vector3(side * 1.02f, 1.1f, .9f), new Vector3(.18f, .055f, .06f), dark, false);
            Box(car, "Wing mirror", new Vector3(side * 1.11f, 1.13f, .9f), new Vector3(.14f, .13f, .23f), paint, false);
            Box(car, "Headlamp", new Vector3(side * .61f, .79f, 2.36f), new Vector3(.52f, .2f, .035f), warmGlow, false);
            Box(car, "Tail lamp", new Vector3(side * .64f, .76f, -2.36f), new Vector3(.49f, .17f, .035f), redGlow, false);
            // Narrow B-pillars and lower chrome seals tie side windows into the body.
            Box(car, "Central window pillar", new Vector3(side * .875f, 1.24f, -.13f), new Vector3(.06f, .53f, .07f), paint, false);
            Box(car, "Window lower seal", new Vector3(side * .93f, 1.01f, -.03f), new Vector3(.045f, .045f, 2.2f), chrome, false);
        }
        Box(car, "Front bumper", new Vector3(0, .47f, 2.4f), new Vector3(1.95f, .17f, .13f), chrome);
        Box(car, "Rear bumper", new Vector3(0, .47f, -2.4f), new Vector3(1.95f, .17f, .13f), chrome);
        Box(car, "Black front grille", new Vector3(0, .76f, 2.375f), new Vector3(.59f, .23f, .035f), dark, false);
        for (int i = 0; i < 5; i++) Box(car, "Grille slat", new Vector3(-.22f + i * .11f, .76f, 2.4f), new Vector3(.022f, .18f, .015f), chrome, false);
        Box(car, "Blank rear number plate", new Vector3(0, .72f, -2.385f), new Vector3(.43f, .16f, .02f), white, false);
        return car;
    }

    void Lobby(RetroFPSController player, FacilityStoryDirector director)
    {
        Transform area = Group(root, "02 Reception - quiet before the alarm");
        RoomSlabs(area, -7, 7, -4, 8, 4.2f);
        WallX(area, "Lobby west wall", -7, -4, 8, 4.2f);
        WallX(area, "Lobby east wall", 7, -4, 8, 4.2f);
        WallZ(area, "Lobby north wall", 8, -7, 7, 4.2f, 3, 6);
        Skirting(area, -7, 7, -4, 8);
        Fixture(area, "Reception ceiling fluorescent", new Vector3(0, 4.1f, 2.3f), coldGlow, new Color(.59f, .74f, 1), 8, 10);
        Fixture(area, "Lobby entrance fluorescent", new Vector3(0, 4.1f, -1.4f), coldGlow, new Color(.57f, .74f, 1), 5.5f, 8);
        Transform desk = Group(area, "Reception counter - open central knee space");
        Box(desk, "Walnut counter top", new Vector3(0, 1.01f, 3.6f), new Vector3(4.4f, .12f, .72f), wood);
        Box(desk, "Counter left cabinet", new Vector3(-1.67f, .47f, 3.6f), new Vector3(.84f, .94f, .65f), white);
        Box(desk, "Counter right cabinet", new Vector3(1.67f, .47f, 3.6f), new Vector3(.84f, .94f, .65f), white);
        Box(desk, "Under-counter pistol shelf", new Vector3(0, .41f, 3.56f), new Vector3(1.9f, .065f, .55f), wood);
        Box(desk, "Undershelf small lamp", new Vector3(.80f, .78f, 3.66f), new Vector3(.22f, .035f, .09f), warmGlow, false);
        for (int side = -1; side <= 1; side += 2)
            for (int drawer = 0; drawer < 3; drawer++)
            {
                Box(desk, "Cabinet drawer face", new Vector3(side * 1.67f, .19f + drawer * .27f, 3.255f), new Vector3(.73f, .245f, .025f), wood, false);
                Box(desk, "Drawer pull", new Vector3(side * 1.67f, .21f + drawer * .27f, 3.225f), new Vector3(.18f, .035f, .035f), steel, false);
            }
        Computer(desk, new Vector3(-1.38f, 1.08f, 3.48f), 180);
        Box(desk, "Desk telephone base", new Vector3(.98f, 1.105f, 3.62f), new Vector3(.26f, .07f, .22f), dark, false);
        Box(desk, "Telephone receiver", new Vector3(.98f, 1.17f, 3.62f), new Vector3(.29f, .06f, .07f), dark, false);
        Box(desk, "Open logbook", new Vector3(.48f, 1.079f, 3.54f), new Vector3(.35f, .015f, .25f), white, false);
        FacilityReceptionAuthoring.ConfigureDesk(desk, director);
        // Seating and dropped paperwork belong against the wall, leaving both counter approaches clear.
        Bench(area, new Vector3(-5.9f, 0, .5f), -90);
        Bench(area, new Vector3(5.9f, 0, .5f), 90);
        Box(area, "Waste basket", new Vector3(-5.9f, .28f, 5.8f), new Vector3(.4f, .56f, .4f), dark);
        Box(area, "Rear filing cupboard", new Vector3(-5.85f, 1.0f, 6.9f), new Vector3(1.5f, 2, .68f), steel);
        for (int i = 0; i < 4; i++) Box(area, "Filing cupboard handle", new Vector3(-5.85f, .3f + i * .46f, 6.55f), new Vector3(.23f, .025f, .03f), dark, false);
        Transform lobbyEntry = Group(area, "Lobby objective arrival point"); lobbyEntry.position = new Vector3(0, 0, -.8f); director.lobbyEntry = lobbyEntry;
    }

    void Security()
    {
        Transform area = Group(root, "03 Bent security passage and watch office");
        RoomSlabs(area, -7, 7, 8, 16, 3.5f);
        WallX(area, "Security west wall", -7, 8, 16, 3.5f);
        WallX(area, "Security east wall", 7, 8, 16, 3.5f);
        WallZ(area, "Records threshold wall", 16, -7, 7, 3.5f, -5.7f, -2.3f);
        // The security office occupies the western side; two wide door openings form a useful branch.
        WallX(area, "Watch office partition", 1.0f, 8, 12.5f, 3.5f, 9.1f, 11.6f);
        Box(area, "Watch office rear partition", new Vector3(-2.95f, 1.75f, 12.5f), new Vector3(8.1f, 3.5f, .2f), concrete);
        // Passage turns left behind the office, keeping the next entrance visible from its bend.
        Fixture(area, "Bent passage fluorescent", new Vector3(2.5f, 3.4f, 13.8f), warmGlow, new Color(1, .67f, .39f), 9, 12);
        BuildOfficeFixture(area);
        Table(area, new Vector3(-3.8f, 0, 10.4f), new Vector2(2.8f, .85f), steel);
        Computer(area, new Vector3(-4.3f, .90f, 10.28f), 180);
        Computer(area, new Vector3(-3.2f, .90f, 10.28f), 180);
        Box(area, "Security wall monitor frame", new Vector3(-3.8f, 1.8f, 8.14f), new Vector3(2.1f, .90f, .16f), dark, false);
        Box(area, "Security monitor dim glass", new Vector3(-3.8f, 1.8f, 8.23f), new Vector3(1.88f, .70f, .018f), glass, false);
        Box(area, "Floor cable trough - against wall", new Vector3(6.73f, .10f, 11), new Vector3(.22f, .2f, 5), dark);
        Box(area, "Maintenance panel", new Vector3(6.82f, 1.35f, 10.1f), new Vector3(.18f, 1.2f, .8f), steel, false);
        PipeRun(area, new Vector3(-6.7f, 2.95f, 8.2f), new Vector3(-6.7f, 2.95f, 15.8f), .075f);
    }

    void Records()
    {
        Transform area = Group(root, "04 Counting floor - records and transfer machinery");
        RoomSlabs(area, -12, 12, 16, 30, 5.3f);
        WallX(area, "Counting floor west wall", -12, 16, 30, 5.3f);
        WallX(area, "Counting floor east wall", 12, 16, 30, 5.3f);
        // The former security building occupies the middle of this facade. Side infill is explicit.
        Box(area, "South west infill", new Vector3(-9.5f, 2.65f, 16), new Vector3(5, 5.3f, .25f), concrete);
        Box(area, "South east infill", new Vector3(9.5f, 2.65f, 16), new Vector3(5, 5.3f, .25f), concrete);
        Box(area, "Security roof level infill", new Vector3(0, 4.4f, 16), new Vector3(14, 1.8f, .25f), concrete);
        WallZ(area, "Vault security wall", 30, -12, 12, 5.3f, -1.8f, 1.8f);
        for (int x = -1; x <= 1; x++)
            Fixture(area, "Counting floor ceiling light", new Vector3(x * 7.8f, 5.2f, 21), coldGlow, new Color(.54f, .74f, .88f), 12, 15);
        // A raised cable beam and duct sit at the ceiling, supported at both structural walls.
        for (int z = 19; z <= 27; z += 4)
        {
            Box(area, "Ceiling cross beam", new Vector3(0, 4.9f, z), new Vector3(23.8f, .32f, .22f), steel, false);
            Box(area, "Beam wall seat west", new Vector3(-11.8f, 4.55f, z), new Vector3(.36f, .7f, .42f), steel, false);
            Box(area, "Beam wall seat east", new Vector3(11.8f, 4.55f, z), new Vector3(.36f, .7f, .42f), steel, false);
        }
        Box(area, "Suspended ventilation trunk", new Vector3(8.8f, 4.5f, 23), new Vector3(.8f, .65f, 13.7f), steel, false);
        for (int z = 18; z <= 28; z += 5)
            Box(area, "Duct ceiling hanger", new Vector3(8.8f, 4.98f, z), new Vector3(.055f, .65f, .055f), dark, false);
        // Distinct rows provide cover without closing the 4 m central aisle or side return lanes.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int row = 0; row < 2; row++)
            {
                Vector3 at = new Vector3(side * 6.6f, 0, 19.6f + row * 6.3f);
                Table(area, at, new Vector2(3.4f, 1.1f), steel);
                Computer(area, at + new Vector3(-.85f, .9f, 0), 180);
                Box(area, "Currency counting machine", at + new Vector3(.65f, 1.10f, 0), new Vector3(.6f, .4f, .5f), white);
                Box(area, "Machine note inlet", at + new Vector3(.65f, 1.32f, .05f), new Vector3(.42f, .05f, .22f), dark, false);
                Box(area, "Counting tray", at + new Vector3(.65f, .935f, -.36f), new Vector3(.48f, .07f, .35f), dark, false);
                Box(area, "Workstation foot rail", at + new Vector3(0, .24f, .45f), new Vector3(3.24f, .06f, .06f), dark, false);
            }
            for (int row = 0; row < 3; row++)
                FilingShelf(area, new Vector3(side * 10.8f, 0, 18.5f + row * 4.8f), side > 0 ? 90 : -90);
        }
        // Staged repair is contained within a machine bay; it does not spill across an entrance.
        Transform machine = Group(area, "Transfer pump and unfinished repair bay");
        Box(machine, "Machine plinth", new Vector3(5.6f, .16f, 23), new Vector3(2.5f, .32f, 2.5f), concrete);
        MeshObject(machine, "Horizontal pump casing", new Vector3(5.6f, .91f, 23), CylinderMesh("Pump casing", .62f, 1.5f, 12), rust, true).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box(machine, "Pump motor", new Vector3(5.6f, .82f, 24.05f), new Vector3(1.05f, 1.0f, .65f), steel);
        PipeRun(machine, new Vector3(5.6f, 1.1f, 22.1f), new Vector3(5.6f, 1.1f, 21.2f), .15f);
        Box(machine, "Toolbox on work bay floor", new Vector3(7.4f, .18f, 22.7f), new Vector3(.6f, .36f, .32f), rust);
        Box(machine, "Removed pump cover on plinth", new Vector3(6.35f, .42f, 23), new Vector3(.18f, .2f, .9f), dark, false);
        PipeRun(area, new Vector3(-11.65f, 3.8f, 16.5f), new Vector3(-11.65f, 3.8f, 29.5f), .11f);
    }

    void Vault(FacilityStoryDirector director)
    {
        Transform area = Group(root, "05 Open strongroom - cash and bullion");
        RoomSlabs(area, -5, 5, 30, 40, 4.1f);
        WallX(area, "Vault west reinforced wall", -5, 30, 40, 4.1f);
        WallX(area, "Vault east reinforced wall", 5, 30, 40, 4.1f);
        WallZ(area, "Vault rear reinforced wall", 40, -5, 5, 4.1f);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(area, "Strongroom steel jamb", new Vector3(side * 1.95f, 1.65f, 30), new Vector3(.3f, 3.3f, .75f), steel);
            Box(area, "Strongroom jamb inset", new Vector3(side * 1.77f, 1.65f, 30), new Vector3(.055f, 3.05f, .65f), dark, false);
        }
        Box(area, "Strongroom steel lintel", new Vector3(0, 3.3f, 30), new Vector3(4.2f, .3f, .75f), steel);
        Transform door = Group(area, "Heavy vault door - swung clear of route");
        door.position = new Vector3(-2.35f, 1.55f, 32.15f);
        Box(door, "Thick door slab", Vector3.zero, new Vector3(.45f, 3.1f, 3.4f), steel);
        Box(door, "Reinforcing back plate", new Vector3(.25f, 0, 0), new Vector3(.09f, 2.75f, 3), dark, false);
        for (int z = -1; z <= 1; z++) Box(door, "Vault locking bolt", new Vector3(.32f, 0, z * 1.10f), new Vector3(.18f, 2.2f, .12f), steel, false);
        GameObject wheel = MeshObject(door, "Vault handle spindle", new Vector3(.45f, .15f, 0), CylinderMesh("Vault handle hub", .17f, .25f, 10), steel);
        wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Box(door, "Vault wheel crossbar", new Vector3(.59f, .15f, 0), new Vector3(.07f, .76f, .08f), steel, false);
        Box(door, "Vault wheel crossbar horizontal", new Vector3(.59f, .15f, 0), new Vector3(.07f, .08f, .76f), steel, false);
        for (int i = 0; i < 2; i++) Box(area, "Massive vault hinge", new Vector3(-1.98f, .55f + i * 2.15f, 30.45f), new Vector3(.5f, .48f, .5f), dark);
        Fixture(area, "Vault warm fluorescent", new Vector3(0, 4, 36), warmGlow, new Color(1, .71f, .39f), 11, 12);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++) FilingShelf(area, new Vector3(side * 4.1f, 0, 33.5f + i * 2.5f), side > 0 ? 90 : -90);
        Table(area, new Vector3(0, 0, 36.1f), new Vector2(3.3f, 1.3f), steel);
        Transform cash = Group(area, "Vault cash bundles");
        cash.position = new Vector3(-.70f, .93f, 36.1f);
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
            {
                Vector3 at = new Vector3((column - 1) * .22f, row * .075f, (row % 2 == 0 ? -.12f : .12f));
                Box(cash, "Bound banknote stack", at, new Vector3(.20f, .065f, .38f), paper, false);
                Box(cash, "Paper currency band", at + new Vector3(0, .034f, 0), new Vector3(.067f, .004f, .39f), white, false);
            }
        director.cashPickup = Interactable(area, "Cash collection", new Vector3(-.70f, 1.05f, 36.1f), new Vector3(.95f, .45f, .80f), director, FacilityStoryInteractionKind.Cash, "Take the cash", cash.gameObject);
        Transform bullion = Group(area, "Vault gold bars"); bullion.position = new Vector3(.77f, .90f, 36.1f);
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3 - row; col++)
            {
                GameObject bar = MeshObject(bullion, "Cast gold ingot", new Vector3((col - (2 - row) * .5f) * .24f, row * .10f, 0), IngotMesh(), gold);
                bar.transform.localRotation = Quaternion.Euler(0, row * 90, 0);
            }
        director.goldPickup = Interactable(area, "Gold collection", new Vector3(.77f, 1.07f, 36.1f), new Vector3(.95f, .45f, .85f), director, FacilityStoryInteractionKind.Gold, "Take the gold", bullion.gameObject);
        Box(area, "Empty transfer trolley deck", new Vector3(2.5f, .28f, 38.5f), new Vector3(1.1f, .12f, 1.5f), steel);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            Box(area, "Trolley caster", new Vector3(2.5f + x * .42f, .11f, 38.5f + z * .58f), new Vector3(.15f, .22f, .12f), dark, false);
        Box(area, "Transfer trolley handle upright", new Vector3(2.5f, .75f, 39.2f), new Vector3(1.1f, .95f, .08f), steel, false);
    }

    void Cast(RetroFPSController player, FacilityStoryDirector director)
    {
        Transform cast = Group(root, "Characters - persistent editable story actors");
        director.receptionist = HumanoidModelAuthoring.Build(new Vector3(0, .01f, 4.35f), player.transform, cast);
        director.receptionist.name = "Receptionist - behind open counter";
        ColorShirt(director.receptionist, Mat("Shirt muted ivory", new Color(.58f, .57f, .48f)));
        NavIgnore(director.receptionist.gameObject);
        Vector3[] positions = { new Vector3(-2f, .04f, 11.4f), new Vector3(-7.5f, .01f, 22.4f), new Vector3(7.1f, .01f, 28), new Vector3(3.2f, .04f, 27.3f) };
        string[] names = { "Watch officer - blue shirt", "Cash handler - rust shirt", "Transfer worker - green shirt", "Vault keeper - burgundy shirt" };
        Color[] colors = { new Color(.18f, .31f, .43f), new Color(.44f, .25f, .15f), new Color(.24f, .38f, .25f), new Color(.38f, .16f, .22f) };
        var guards = new List<FacilityKnifeGuard>();
        FacilityKnifeGuard receptionistGuard = director.receptionist.gameObject.AddComponent<FacilityKnifeGuard>();
        receptionistGuard.actor = director.receptionist; receptionistGuard.startAlerted = false; receptionistGuard.walkSpeed = 2.35f;
        receptionistGuard.knifeProp = Knife(director.receptionist.forearms[1]); guards.Add(receptionistGuard);
        for (int i = 0; i < positions.Length; i++)
        {
            FacilityHumanoid actor = HumanoidModelAuthoring.Build(positions[i], player.transform, cast);
            actor.name = names[i]; ColorShirt(actor, Mat(names[i], colors[i]));
            FacilityKnifeGuard guard = actor.gameObject.AddComponent<FacilityKnifeGuard>();
            guard.actor = actor; guard.walkSpeed = 2.35f; guard.startAlerted = false;
            guard.knifeProp = Knife(actor.forearms[1]);
            guards.Add(guard); NavIgnore(actor.gameObject);
        }
        director.guards = guards.ToArray();
    }

    void Infrastructure(FacilityStoryDirector director)
    {
        Transform services = Group(root, "Lighting and sound - mounted services");
        AudioSource hum = Group(services, "Low structural hum").gameObject.AddComponent<AudioSource>();
        hum.clip = FacilitySoundDesign.CreepyHum(); hum.loop = true; hum.playOnAwake = true;
        hum.volume = .065f; hum.spatialBlend = 0; hum.priority = 220; hum.dopplerLevel = 0;
        AudioSource alarm = Group(services, "Security alarm - receptionist attacked").gameObject.AddComponent<AudioSource>();
        alarm.clip = AlarmAudio(); alarm.loop = true; alarm.playOnAwake = false;
        alarm.volume = .21f; alarm.spatialBlend = .15f; alarm.priority = 90; alarm.dopplerLevel = 0;
        alarm.transform.position = new Vector3(0, 3, 12);
        director.alarmSource = alarm;
        AlarmFixture(services, new Vector3(6.82f, 2.75f, 12), Quaternion.Euler(0, -90, 0));
        AlarmFixture(services, new Vector3(-11.82f, 3.5f, 24), Quaternion.Euler(0, 90, 0));
        director.alarmLights = alarmLights.ToArray();
    }

    void AlarmFixture(Transform parent, Vector3 at, Quaternion facing)
    {
        Transform group = Group(parent, "Alarm beacon bolted to wall"); group.position = at; group.rotation = facing;
        Box(group, "Wall mounting plate", Vector3.zero, new Vector3(.28f, .38f, .08f), dark, false);
        Box(group, "Red alarm lens", new Vector3(0, 0, .08f), new Vector3(.18f, .25f, .12f), redGlow, false);
        Light lamp = Group(group, "Alarm spill - no shadows").gameObject.AddComponent<Light>();
        lamp.transform.localPosition = new Vector3(0, 0, .25f); lamp.type = LightType.Point; lamp.range = 7;
        lamp.color = new Color(1, .055f, .015f); lamp.intensity = 2.2f; lamp.enabled = false;
        lamp.shadows = LightShadows.None; lamp.lightmapBakeType = LightmapBakeType.Realtime;
        alarmLights.Add(lamp);
    }

    static void ColorShirt(FacilityHumanoid actor, Material cloth)
    {
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            if (region.kind != FacilityBodyRegion.Torso && region.kind != FacilityBodyRegion.UpperArmLeft && region.kind != FacilityBodyRegion.UpperArmRight && region.kind != FacilityBodyRegion.ForearmLeft && region.kind != FacilityBodyRegion.ForearmRight) continue;
            foreach (MeshRenderer renderer in region.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material original = renderer.sharedMaterial;
                if (original != null && original.name.StartsWith("Coverall", StringComparison.Ordinal)) renderer.sharedMaterial = cloth;
            }
        }
    }

    Transform Knife(Transform forearm)
    {
        Transform knife = Group(forearm, "Steel utility knife - held in right hand");
        knife.localPosition = new Vector3(0, -.36f, .045f); knife.localRotation = Quaternion.Euler(-20, 0, 0);
        Box(knife, "Textured knife grip", new Vector3(0, .018f, .015f), new Vector3(.043f, .09f, .047f), dark, false);
        Box(knife, "Knife finger guard", new Vector3(0, -.025f, .055f), new Vector3(.09f, .018f, .035f), steel, false);
        MeshObject(knife, "Tapered steel blade", new Vector3(0, -.025f, .06f), BladeMesh(), steel);
        // The knife follows a physical forearm and must never enter the world's static batch.
        foreach (Transform part in knife.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
        return knife;
    }

    static void NavIgnore(GameObject actor)
    {
        NavMeshModifier modifier = actor.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = actor.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }

    FacilityStoryInteractable Interactable(Transform parent, string name, Vector3 at, Vector3 size, FacilityStoryDirector director, FacilityStoryInteractionKind kind, string label, GameObject visual)
    {
        Transform marker = Group(parent, name); marker.localPosition = at;
        BoxCollider collider = marker.gameObject.AddComponent<BoxCollider>(); collider.size = size; collider.isTrigger = true;
        FacilityStoryInteractable target = marker.gameObject.AddComponent<FacilityStoryInteractable>();
        target.director = director; target.kind = kind; target.label = label; target.visualRoot = visual;
        NavIgnore(marker.gameObject);
        return target;
    }

    void RoomSlabs(Transform parent, float x0, float x1, float z0, float z1, float ceiling)
    {
        Box(parent, "Continuous walking slab", new Vector3((x0 + x1) * .5f, -.15f, (z0 + z1) * .5f), new Vector3(x1 - x0, .3f, z1 - z0), floor);
        Box(parent, "Concrete ceiling", new Vector3((x0 + x1) * .5f, ceiling + .13f, (z0 + z1) * .5f), new Vector3(x1 - x0 + .25f, .26f, z1 - z0 + .25f), concrete);
    }

    void WallZ(Transform parent, string name, float z, float x0, float x1, float h, float gap0 = float.NaN, float gap1 = float.NaN)
    {
        if (float.IsNaN(gap0)) Box(parent, name, new Vector3((x0 + x1) * .5f, h * .5f, z), new Vector3(x1 - x0, h, .25f), concrete);
        else
        {
            if (gap0 > x0) Box(parent, name + " left", new Vector3((x0 + gap0) * .5f, h * .5f, z), new Vector3(gap0 - x0, h, .25f), concrete);
            if (gap1 < x1) Box(parent, name + " right", new Vector3((gap1 + x1) * .5f, h * .5f, z), new Vector3(x1 - gap1, h, .25f), concrete);
            if (h > 3) Box(parent, name + " doorway header", new Vector3((gap0 + gap1) * .5f, (h + 3) * .5f, z), new Vector3(gap1 - gap0, h - 3, .25f), concrete);
        }
    }

    void WallX(Transform parent, string name, float x, float z0, float z1, float h, float gap0 = float.NaN, float gap1 = float.NaN)
    {
        if (float.IsNaN(gap0)) Box(parent, name, new Vector3(x, h * .5f, (z0 + z1) * .5f), new Vector3(.25f, h, z1 - z0), concrete);
        else
        {
            if (gap0 > z0) Box(parent, name + " south", new Vector3(x, h * .5f, (z0 + gap0) * .5f), new Vector3(.25f, h, gap0 - z0), concrete);
            if (gap1 < z1) Box(parent, name + " north", new Vector3(x, h * .5f, (gap1 + z1) * .5f), new Vector3(.25f, h, z1 - gap1), concrete);
            if (h > 3) Box(parent, name + " doorway header", new Vector3(x, (h + 3) * .5f, (gap0 + gap1) * .5f), new Vector3(.25f, h - 3, gap1 - gap0), concrete);
        }
    }

    void Skirting(Transform area, float x0, float x1, float z0, float z1)
    {
        Box(area, "West wall skirting", new Vector3(x0 + .14f, .13f, (z0 + z1) * .5f), new Vector3(.035f, .25f, z1 - z0), dark, false);
        Box(area, "East wall skirting", new Vector3(x1 - .14f, .13f, (z0 + z1) * .5f), new Vector3(.035f, .25f, z1 - z0), dark, false);
    }

    void Fixture(Transform parent, string name, Vector3 at, Material diffuser, Color color, float intensity, float range)
    {
        Transform fixture = Group(parent, name); fixture.localPosition = at;
        Box(fixture, "Flush mounted metal housing", Vector3.zero, new Vector3(1.6f, .12f, .36f), dark, false);
        Box(fixture, "Housing mounting backplate", new Vector3(0, .08f, 0), new Vector3(1.5f, .04f, .29f), steel, false);
        Box(fixture, "Recessed luminous diffuser", new Vector3(0, -.067f, 0), new Vector3(1.4f, .025f, .26f), diffuser, false);
        Light lamp = Group(fixture, "Baked fixture illumination").gameObject.AddComponent<Light>();
        lamp.transform.localPosition = new Vector3(0, -.19f, 0); lamp.type = LightType.Point;
        lamp.color = color; lamp.range = range; lamp.intensity = intensity; lamp.shadows = LightShadows.None;
        lamp.lightmapBakeType = LightmapBakeType.Realtime; lamp.renderMode = LightRenderMode.Auto;
    }

    void Table(Transform parent, Vector3 at, Vector2 size, Material top)
    {
        Transform table = Group(parent, "Industrial workstation"); table.localPosition = at;
        Box(table, "Worktop", new Vector3(0, .85f, 0), new Vector3(size.x, .1f, size.y), top);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            Box(table, "Square steel leg", new Vector3(x * (size.x * .5f - .12f), .40f, z * (size.y * .5f - .1f)), new Vector3(.075f, .8f, .075f), dark);
    }

    void Computer(Transform parent, Vector3 at, float yaw)
    {
        Transform monitor = Group(parent, "Abandoned CRT terminal"); monitor.localPosition = at; monitor.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(monitor, "Monitor pedestal", new Vector3(0, .055f, .04f), new Vector3(.34f, .11f, .29f), white, false);
        Box(monitor, "Monitor shell", new Vector3(0, .31f, .08f), new Vector3(.52f, .44f, .44f), white);
        Box(monitor, "Glass tube face", new Vector3(0, .32f, -.146f), new Vector3(.42f, .32f, .018f), glass, false);
        Box(monitor, "Keyboard", new Vector3(0, .025f, -.38f), new Vector3(.49f, .05f, .20f), white, false);
        for (int i = 0; i < 3; i++) Box(monitor, "Keyboard key row", new Vector3(0, .053f, -.43f + i * .047f), new Vector3(.39f, .014f, .028f), dark, false);
    }

    void Bench(Transform parent, Vector3 at, float yaw)
    {
        Transform bench = Group(parent, "Waiting room bench"); bench.localPosition = at; bench.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(bench, "Bench seat", new Vector3(0, .48f, 0), new Vector3(2.0f, .14f, .58f), dark);
        Box(bench, "Bench back", new Vector3(0, .85f, .26f), new Vector3(2, .65f, .11f), dark);
        for (int i = -1; i <= 1; i += 2) Box(bench, "Bench pedestal foot", new Vector3(i * .72f, .21f, 0), new Vector3(.1f, .42f, .51f), steel);
    }

    void FilingShelf(Transform parent, Vector3 at, float yaw)
    {
        Transform shelf = Group(parent, "Records shelf - archived transfer files"); shelf.localPosition = at; shelf.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(shelf, "Shelf solid back", new Vector3(0, 1.08f, .25f), new Vector3(1.6f, 2.16f, .06f), steel);
        for (int side = -1; side <= 1; side += 2) Box(shelf, "Shelf upright", new Vector3(side * .78f, 1.08f, 0), new Vector3(.06f, 2.16f, .55f), steel);
        for (int level = 0; level < 4; level++)
        {
            Box(shelf, "Shelf deck", new Vector3(0, .16f + level * .62f, 0), new Vector3(1.6f, .055f, .56f), steel);
            if (level < 3) for (int file = 0; file < 4; file++)
                Box(shelf, "Grouped archive boxes", new Vector3(-.57f + file * .38f, .38f + level * .62f, .02f), new Vector3(.32f, .38f, .42f), file % 2 == 0 ? white : wood, false);
        }
    }

    void Fence(Transform parent, float fixedAxis, float start, float end, bool acrossX)
    {
        Transform fence = Group(parent, "Parking perimeter steel fence");
        float length = end - start;
        Vector3 center = acrossX ? new Vector3((start + end) * .5f, 1.05f, fixedAxis) : new Vector3(fixedAxis, 1.05f, (start + end) * .5f);
        BoxCollider barrier = fence.gameObject.AddComponent<BoxCollider>(); barrier.center = center;
        barrier.size = acrossX ? new Vector3(length, 2.1f, .12f) : new Vector3(.12f, 2.1f, length);
        for (float a = start; a <= end + .05f; a += 3.3f)
            Box(fence, "Fence structural post", acrossX ? new Vector3(a, 1.05f, fixedAxis) : new Vector3(fixedAxis, 1.05f, a), new Vector3(.1f, 2.1f, .1f), steel, false);
        for (int i = 0; i < 2; i++) Box(fence, "Continuous fence rail", center + Vector3.up * (i == 0 ? -.55f : .65f), acrossX ? new Vector3(length, .06f, .06f) : new Vector3(.06f, .06f, length), steel, false);
        for (float a = start + .2f; a < end; a += .4f)
            Box(fence, "Vertical fence bar", acrossX ? new Vector3(a, 1.05f, fixedAxis) : new Vector3(fixedAxis, 1.05f, a), new Vector3(.025f, 1.7f, .025f), dark, false);
    }

    void PipeRun(Transform parent, Vector3 from, Vector3 to, float radius)
    {
        Vector3 axis = to - from;
        GameObject pipe = MeshObject(parent, "Mounted exposed service pipe", (from + to) * .5f, CylinderMesh("Pipe " + radius.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " " + axis.magnitude.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), radius, axis.magnitude, 8), rust);
        pipe.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        int count = Mathf.Max(2, Mathf.CeilToInt(axis.magnitude / 3));
        for (int i = 0; i <= count; i++)
        {
            Vector3 at = Vector3.Lerp(from, to, i / (float)count);
            Box(parent, "Pipe wall saddle", at + Vector3.left * .13f, new Vector3(.3f, radius * 2.8f, .1f), dark, false);
        }
    }

    GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid = true)
    {
        string key = "Box " + size.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + size.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + size.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        Mesh mesh = GetMesh(key, () => UnityEngine.Object.Instantiate(facility.BoxMesh(size)));
        GameObject item = MeshObject(parent, name, at, mesh, material);
        if (solid) item.AddComponent<BoxCollider>().size = size;
        return item;
    }

    GameObject MeshObject(Transform parent, string name, Vector3 at, Mesh mesh, Material material, bool solid = false)
    {
        GameObject item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        item.transform.SetParent(parent, false); item.transform.localPosition = at;
        item.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        if (solid) { MeshCollider collider = item.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; }
        return item;
    }

    static Transform Group(Transform parent, string name)
    {
        Transform group = new GameObject(name).transform; if (parent != null) group.SetParent(parent, false); return group;
    }

    Material Mat(string name, Color color, bool texture = false, bool glow = false)
    {
        if (materials.TryGetValue(name, out Material cached)) return cached;
        string path = AssetRoot + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return materials[name] = material;
        material = new Material(Shader.Find(glow ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color);
        if (!glow)
        {
            material.SetFloat("_Smoothness", name == "Sedan black paint" ? .22f : .06f);
            material.SetFloat("_Metallic", name == "Bullion gold" ? .52f : .08f);
            // A restrained material-level floor keeps a night scene readable on the low preset.
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .025f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (texture) { material.SetTexture("_BaseMap", Texture(name)); }
        }
        AssetDatabase.CreateAsset(material, path);
        return materials[name] = material;
    }

    Texture2D Texture(string name)
    {
        string path = AssetRoot + "/Textures/" + name + ".png";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;
        Texture2D image = new Texture2D(32, 32, TextureFormat.RGB24, false);
        var random = new System.Random(13731 + StableHash(name));
        var pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            float value = .82f + (float)random.NextDouble() * .18f;
            if (random.NextDouble() < .05) value -= .09f;
            pixels[i] = new Color(value, value, value);
        }
        image.SetPixels(pixels); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.anisoLevel = 1; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static int StableHash(string value) { unchecked { int hash = 17; foreach (char c in value) hash = hash * 31 + c; return hash; } }

    Mesh GetMesh(string name, Func<Mesh> make)
    {
        if (meshes.TryGetValue(name, out Mesh cached)) return cached;
        string path = AssetRoot + "/Meshes/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = make(); mesh.name = name;
            if (mesh.uv2 == null || mesh.uv2.Length != mesh.vertexCount) Unwrapping.GenerateSecondaryUVSet(mesh);
            AssetDatabase.CreateAsset(mesh, path);
        }
        return meshes[name] = mesh;
    }

    Mesh CylinderMesh(string name, float radius, float length, int sides)
    {
        return GetMesh(name, () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), q = new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
                Quad(v, uv, t, p + Vector3.down * length * .5f, p + Vector3.up * length * .5f, q + Vector3.up * length * .5f, q + Vector3.down * length * .5f);
                Tri(v, uv, t, Vector3.up * length * .5f, q + Vector3.up * length * .5f, p + Vector3.up * length * .5f);
                Tri(v, uv, t, Vector3.down * length * .5f, p + Vector3.down * length * .5f, q + Vector3.down * length * .5f);
            }
            return Mesh(v, uv, t);
        });
    }

    Mesh SedanCabinMesh()
    {
        return GetMesh("Sedan sloped cabin", () => Prism(new[] { new Vector2(-1.2f, .95f), new Vector2(-.94f, 1.50f), new Vector2(.70f, 1.50f), new Vector2(1.26f, .95f) }, .88f));
    }

    Mesh SedanGlassMesh()
    {
        return GetMesh("Sedan inset glazing", () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Quad(v, uv, t, new Vector3(-.82f, 1.015f, 1.205f), new Vector3(-.80f, 1.447f, .767f), new Vector3(.80f, 1.447f, .767f), new Vector3(.82f, 1.015f, 1.205f));
            Quad(v, uv, t, new Vector3(.82f, 1.015f, -1.173f), new Vector3(.80f, 1.445f, -.968f), new Vector3(-.80f, 1.445f, -.968f), new Vector3(-.82f, 1.015f, -1.173f));
            Quad(v, uv, t, new Vector3(.889f, 1.02f, -.99f), new Vector3(.889f, 1.45f, -.91f), new Vector3(.889f, 1.45f, .67f), new Vector3(.889f, 1.02f, 1.12f));
            Quad(v, uv, t, new Vector3(-.889f, 1.02f, 1.12f), new Vector3(-.889f, 1.45f, .67f), new Vector3(-.889f, 1.45f, -.91f), new Vector3(-.889f, 1.02f, -.99f));
            return Mesh(v, uv, t);
        });
    }

    Mesh IngotMesh()
    {
        return GetMesh("Tapered cast gold bar", () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 a = new Vector3(-.11f, 0, -.25f), b = new Vector3(-.09f, .10f, -.22f), c = new Vector3(.09f, .10f, -.22f), d = new Vector3(.11f, 0, -.25f);
            Vector3 e = new Vector3(-.11f, 0, .25f), f = new Vector3(-.09f, .10f, .22f), g = new Vector3(.09f, .10f, .22f), h = new Vector3(.11f, 0, .25f);
            Quad(v, uv, t, a, b, c, d); Quad(v, uv, t, h, g, f, e); Quad(v, uv, t, e, f, b, a); Quad(v, uv, t, d, c, g, h); Quad(v, uv, t, b, f, g, c); Quad(v, uv, t, e, a, d, h);
            return Mesh(v, uv, t);
        });
    }

    Mesh BladeMesh()
    {
        return GetMesh("Knife tapered blade", () =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 a = new Vector3(-.022f, -.004f, 0), b = new Vector3(.022f, -.004f, 0), tip = new Vector3(.012f, -.004f, .23f);
            Tri(v, uv, t, a, tip, b); Tri(v, uv, t, a + Vector3.up * .008f, b + Vector3.up * .008f, tip + Vector3.up * .008f);
            Quad(v, uv, t, a, a + Vector3.up * .008f, tip + Vector3.up * .008f, tip); Quad(v, uv, t, b, tip, tip + Vector3.up * .008f, b + Vector3.up * .008f);
            return Mesh(v, uv, t);
        });
    }

    static Mesh Prism(Vector2[] profile, float halfWidth)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i < profile.Length; i++)
        {
            Vector2 a = profile[i], b = profile[(i + 1) % profile.Length];
            Quad(v, uv, t, new Vector3(-halfWidth, a.y, a.x), new Vector3(-halfWidth, b.y, b.x), new Vector3(halfWidth, b.y, b.x), new Vector3(halfWidth, a.y, a.x));
        }
        Quad(v, uv, t, new Vector3(-halfWidth, profile[3].y, profile[3].x), new Vector3(-halfWidth, profile[2].y, profile[2].x), new Vector3(-halfWidth, profile[1].y, profile[1].x), new Vector3(-halfWidth, profile[0].y, profile[0].x));
        Quad(v, uv, t, new Vector3(halfWidth, profile[0].y, profile[0].x), new Vector3(halfWidth, profile[1].y, profile[1].x), new Vector3(halfWidth, profile[2].y, profile[2].x), new Vector3(halfWidth, profile[3].y, profile[3].x));
        return Mesh(v, uv, t);
    }

    static Mesh Mesh(List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        Mesh mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static void Quad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int n = v.Count; v.AddRange(new[] { a, b, c, d }); uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
        t.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
    }

    static void Tri(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        int n = v.Count; v.AddRange(new[] { a, b, c }); uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one }); t.AddRange(new[] { n, n + 1, n + 2 });
    }

    AudioClip AlarmAudio()
    {
        string path = AssetRoot + "/Audio/Low facility warning.wav";
        AudioClip existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (existing != null) return existing;
        const int rate = 22050, count = rate * 4;
        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                double seconds = i / (double)rate;
                double sweep = .5 - .5 * Math.Cos(seconds * Math.PI);
                phase += Math.PI * 2 * (155 + 145 * sweep) / rate;
                double pulse = .6 + .4 * Math.Pow(Math.Sin(seconds * Math.PI), 2);
                double edge = Math.Min(1, Math.Min(i, count - 1 - i) / (rate * .03));
                double value = (Math.Sin(phase) * .32 + Math.Sin(phase * 2) * .10 + Math.Sin(phase * 3) * .025) * pulse * edge;
                writer.Write((short)(value * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path); importer.forceToMono = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
