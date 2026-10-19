using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>An explicit, recovery-first upgrade of the existing authored story scene.</summary>
public static class FacilityHeistUpgrade
{
    const string UpgradeName = "Heist equipment and closed doors";
    static FacilityAssetLibrary library;
    static Material steel, dark, glass, ivory, carPaint;

    public static string Apply(FacilityGameSession session, FacilityStoryDirector director)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before upgrading the story scene.");
        if (session == null || director == null) throw new ArgumentNullException("The saved story session and director are required.");
        Scene scene = director.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("The story scene must be open.");
        Transform story = scene.GetRootGameObjects().Select(item => item.transform).FirstOrDefault(item => item.name == "Story Level 1 - Night Deposit");
        if (story == null) throw new InvalidOperationException("The existing authored story environment could not be found; no upgrade was made.");
        EnsureFolder("Assets/Scenes/Recovery");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/StoryBeforeHeist_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true)) throw new IOException("Saving the story recovery copy failed; migration stopped.");
        if (story.Find(UpgradeName) != null) return "Heist upgrade already exists. Existing authored objects were preserved. Recovery: " + recovery;

        library = new FacilityAssetLibrary();
        steel = StoryMaterial("Painted steel", "Steel"); dark = StoryMaterial("Black enamel", "ViewmodelDark"); glass = StoryMaterial("Opaque midnight glazing", "ViewmodelDark"); ivory = StoryMaterial("Aged ivory", "Concrete"); carPaint = StoryMaterial("Sedan black paint", "ViewmodelDark");
        Transform upgrade = Group(story, UpgradeName);
        Transform parking = story.Find("01 Night parking - sedan and entrance");
        Transform lobby = story.Find("02 Reception - quiet before the alarm");
        Transform security = story.Find("03 Bent security passage and watch office");
        Transform records = story.Find("04 Counting floor - records and transfer machinery");
        Transform vault = story.Find("05 Open strongroom - cash and bullion");
        if (parking == null || lobby == null || security == null || records == null || vault == null) throw new InvalidOperationException("A required story area is missing; restore from " + recovery);

        // These old slabs were visibly parked in mid-air and did not meet their hinge axes.
        RemoveNamed(parking, "Open entrance door", "Open door inset glazing", "Door hinge");
        RemoveNamed(vault, "Heavy vault door - swung clear of route", "Massive vault hinge");
        Transform entrance = Group(upgrade, "Entrance - steel glazed double doors");
        DoubleDoors(entrance, director, new Vector3(0, 0, -4), 3.16f, 2.92f, 0, true);
        Transform lobbyDoors = Group(upgrade, "Reception north - security double doors");
        DoubleDoors(lobbyDoors, director, new Vector3(4.5f, 0, 8), 2.86f, 2.92f, 0, false);
        Transform recordsDoors = Group(upgrade, "Records threshold - double fire doors");
        DoubleDoors(recordsDoors, director, new Vector3(-4, 0, 16), 3.26f, 2.92f, 0, false);
        Transform officeDoor = Group(upgrade, "Watch office - hinged access door");
        MakeDoor(officeDoor, director, "Watch office door", new Vector3(1, 0, 11.53f), 2.35f, 2.92f, 90, 90, false, true);

        FacilityDoor trunk = Trunk(parking, upgrade, director);
        Transform sedan = parking.Find("Black sedan - extraction vehicle");
        GameObject portable = FacilityDrillAuthoring.Build(sedan, "Magnetic drill - stowed in sedan trunk");
        portable.transform.localPosition = new Vector3(0, .75f, -1.35f); portable.transform.localRotation = Quaternion.Euler(-90, 0, 0); portable.transform.localScale = Vector3.one * .72f;
        director.drillPickup = Interaction(sedan, "Drill pickup - inside open trunk", new Vector3(0, .78f, -1.65f), new Vector3(.82f, .34f, .70f), director, FacilityStoryInteractionKind.Drill, "Take the magnetic drill", portable);
        director.drillPickup.door = trunk;
        FacilityDoor vaultDoor = VaultDoor(upgrade, director);
        FacilityVaultDrill drill = Group(upgrade, "Vault breach - 45 second magnetic drill").gameObject.AddComponent<FacilityVaultDrill>();
        drill.vaultDoor = vaultDoor;
        drill.deployedDrill = FacilityDrillAuthoring.Build(vaultDoor.leaf, "Magnetic drill - planted on vault door");
        drill.deployedDrill.transform.localPosition = new Vector3(.95f, 1.30f, -.225f); drill.deployedDrill.transform.localRotation = Quaternion.Euler(-90, 0, 0); FacilityDrillAuthoring.ConfigureMotion(drill.deployedDrill, drill); drill.deployedDrill.SetActive(false);
        AudioSource motor = Group(drill.deployedDrill.transform, "Local drill motor loop").gameObject.AddComponent<AudioSource>();
        motor.clip = FacilityDrillAuthoring.DrillLoop(); motor.loop = true; motor.playOnAwake = false; motor.spatialBlend = 1; motor.volume = .27f; motor.minDistance = 2; motor.maxDistance = 22; motor.rolloffMode = AudioRolloffMode.Linear; motor.dopplerLevel = 0; motor.priority = 110;
        drill.drillLoop = motor; director.vaultDrill = drill;
        director.vaultInteraction = Interaction(upgrade, "Vault drill interaction", new Vector3(-.60f, 1.32f, 29.59f), new Vector3(1.15f, 1.20f, .34f), director, FacilityStoryInteractionKind.Vault, "Plant the drill", null);
        director.vaultInteraction.vault = drill;
        vaultDoor.interaction = director.vaultInteraction;
        CorrectProps(story);
        PoliceSpawnPoints(story);
        Transform probeRoot = Group(upgrade, "Trunk and vault equipment light probes");
        var equipmentProbes = new List<Vector3>();
        foreach (float x in new[] { -8.8f, -7.3f }) foreach (float z in new[] { -21.0f, -20.6f }) foreach (float y in new[] { .60f, 1.4f, 2.2f }) equipmentProbes.Add(new Vector3(x, y, z));
        foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { 28.4f, 29.1f }) foreach (float y in new[] { .60f, 1.4f, 2.2f }) equipmentProbes.Add(new Vector3(x, y, z));
        probeRoot.gameObject.AddComponent<LightProbeGroup>().probePositions = equipmentProbes.ToArray();
        // No duplicated persistent environment generation, and no moving doors in a static batch.
        foreach (FacilityDoor door in upgrade.GetComponentsInChildren<FacilityDoor>(true))
        {
            Dynamic(door.leaf.gameObject);
            NavMeshModifier modifier = door.leaf.GetComponent<NavMeshModifier>(); if (modifier == null) modifier = door.leaf.gameObject.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild = true;
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorUtility.SetDirty(director); EditorUtility.SetDirty(session); AssetDatabase.SaveAssets();
        return "Preserved scene and player; corrected benches/shelves/terminals, attached supports and pipework, authored closed hinged doors, sedan trunk drill and 45-second vault equipment. Recovery: " + recovery;
    }

    public static Transform[] PoliceSpawnPoints(Transform storyRoot)
    {
        Transform root = storyRoot.Find("Police arrival points");
        if (root == null)
        {
            root = Group(storyRoot, "Police arrival points");
            Vector3[] positions = { new Vector3(13.7f, .04f, -24), new Vector3(-13.6f, .04f, -24.5f), new Vector3(13.2f, .04f, -10.8f), new Vector3(-14.1f, .04f, -7.2f) };
            for (int i = 0; i < positions.Length; i++) { Transform point = Group(root, "Police entry " + (i + 1)); point.position = positions[i]; point.rotation = Quaternion.LookRotation((new Vector3(0, 0, -5) - positions[i]).normalized); }
        }
        return root.Cast<Transform>().ToArray();
    }

    /// <summary>Idempotently finish the later prop refinements on a scene already upgraded once.</summary>
    public static string FinishAppliedProps(FacilityGameSession session, FacilityStoryDirector director)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Finish the authored props outside Play mode and baking.");
        if (session == null || director == null) throw new ArgumentNullException("The saved story session and director are required.");
        Scene scene = director.gameObject.scene;
        Transform story = scene.GetRootGameObjects().Select(item => item.transform).FirstOrDefault(item => item.name == "Story Level 1 - Night Deposit");
        Transform upgrade = story != null ? story.Find(UpgradeName) : null;
        if (upgrade == null) throw new InvalidOperationException("Apply the heist upgrade before finishing its props.");
        EnsureFolder("Assets/Scenes/Recovery");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/StoryBeforePropFinish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true)) throw new IOException("Prop finish recovery save failed; no changes were made.");
        library = new FacilityAssetLibrary(); steel = StoryMaterial("Painted steel", "Steel"); dark = StoryMaterial("Black enamel", "ViewmodelDark");
        Transform office = story.Find("03 Bent security passage and watch office");
        if (office != null)
        {
            foreach (Transform terminal in office.Cast<Transform>().Where(item => item.name == "Abandoned CRT terminal"))
            { Vector3 at = terminal.localPosition; at.z = 10.28f; terminal.localPosition = at; terminal.localRotation = Quaternion.Euler(0, 180, 0); }
            EnsureOfficeChair(office);
        }
        foreach (FacilityDoor door in upgrade.GetComponentsInChildren<FacilityDoor>(true))
        {
            if (door.name == "Watch office door") { door.transform.position = new Vector3(1,0,11.53f); door.transform.rotation = Quaternion.Euler(0,90,0); door.openLocalEuler = new Vector3(0,90,0); }
            FacilityStoryInteractable interaction = door.GetComponentsInChildren<FacilityStoryInteractable>(true).FirstOrDefault(item => item.kind == FacilityStoryInteractionKind.Door && item.door == door);
            if (interaction != null) { door.interaction = interaction; interaction.label = door.name.StartsWith("Sedan trunk", StringComparison.Ordinal) ? "Sedan trunk" : "Door"; }
            if (director.vaultDrill != null && director.vaultDrill.vaultDoor == door) door.interaction = director.vaultInteraction;
            EditorUtility.SetDirty(door);
        }
        if (director.vaultDrill != null && director.vaultDrill.deployedDrill != null)
        {
            Transform deployed = director.vaultDrill.deployedDrill.transform;
            Vector3 position = deployed.localPosition; position.z = -.225f; deployed.localPosition = position;
            FacilityDrillAuthoring.ConfigureMotion(deployed.gameObject, director.vaultDrill);
            Dynamic(deployed.gameObject);
        }
        FacilityDrillAuthoring.ImproveReadability();
        EnsureEquipmentProbes(upgrade);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        return "Finished office terminal orientation/support, chair, 110-degree office hinge, persistent door interactions and 48 equipment probes. Recovery: " + recovery;
    }

    static void EnsureEquipmentProbes(Transform upgrade)
    {
        if (upgrade.Find("Trunk and vault equipment light probes") != null) return;
        Transform probeRoot = Group(upgrade, "Trunk and vault equipment light probes");
        var equipmentProbes = new List<Vector3>();
        foreach (float x in new[] { -8.8f, -7.3f }) foreach (float z in new[] { -21.0f, -20.6f }) foreach (float y in new[] { .60f, 1.4f, 2.2f }) equipmentProbes.Add(new Vector3(x, y, z));
        foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { 28.4f, 29.1f }) foreach (float y in new[] { .60f, 1.4f, 2.2f }) equipmentProbes.Add(new Vector3(x, y, z));
        probeRoot.gameObject.AddComponent<LightProbeGroup>().probePositions = equipmentProbes.ToArray();
    }

    static void EnsureOfficeChair(Transform office)
    {
        if (office.Find("Watch workstation chair - faces terminal and wall display") != null) return;
        Transform chair = Group(office, "Watch workstation chair - faces terminal and wall display"); chair.localPosition = new Vector3(-4.3f, 0, 11.22f);
        Box(chair, "Chair padded seat", new Vector3(0, .46f, 0), new Vector3(.48f, .10f, .48f), dark, true, true);
        Box(chair, "Chair padded back", new Vector3(0, .79f, .22f), new Vector3(.48f, .43f, .065f), dark, true, true);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(chair, "Backrest steel upright", new Vector3(side * .19f, .65f, .22f), new Vector3(.035f, .48f, .035f), steel, false, true);
            for (int end = -1; end <= 1; end += 2) Box(chair, "Floor standing square chair leg", new Vector3(side * .19f, .22f, end * .19f), new Vector3(.035f, .44f, .035f), steel, false, true);
        }
    }

    static FacilityDoor Trunk(Transform parking, Transform upgrade, FacilityStoryDirector director)
    {
        Transform sedan = parking.Find("Black sedan - extraction vehicle"); if (sedan == null) throw new InvalidOperationException("The original black sedan is missing.");
        RemoveNamed(sedan, "Trunk lid", "Lower coachwork");
        // Replace only the solid rear box with an actual lined trunk cavity; preserve cabin, hood and trim.
        Box(sedan, "Main coachwork - cabin and engine bay", new Vector3(0, .68f, .60f), new Vector3(1.88f, .45f, 3.50f), carPaint, true, true);
        Box(sedan, "Trunk left quarter panel", new Vector3(-.88f, .68f, -1.75f), new Vector3(.12f, .45f, 1.2f), carPaint, true, true);
        Box(sedan, "Trunk right quarter panel", new Vector3(.88f, .68f, -1.75f), new Vector3(.12f, .45f, 1.2f), carPaint, true, true);
        Box(sedan, "Trunk rear sill", new Vector3(0, .68f, -2.30f), new Vector3(1.76f, .45f, .10f), carPaint, true, true);
        Box(sedan, "Trunk carpet floor", new Vector3(0, .585f, -1.76f), new Vector3(1.65f, .05f, 1.08f), dark, false, true);
        Box(sedan, "Trunk carpet forward wall", new Vector3(0, .76f, -1.17f), new Vector3(1.67f, .3f, .05f), dark, false, true);
        Transform unit = Group(sedan, "Sedan trunk - hinged lid"); unit.localPosition = new Vector3(0, .995f, -1.15f);
        Transform leaf = Group(unit, "Trunk leaf and attached hinges");
        Box(leaf, "Trunk painted exterior", new Vector3(0, 0, -.55f), new Vector3(1.84f, .08f, 1.10f), carPaint, true);
        Box(leaf, "Trunk lined underside", new Vector3(0, -.047f, -.55f), new Vector3(1.66f, .025f, .94f), dark);
        for (int i = -1; i <= 1; i += 2) Box(leaf, "Trunk hinge strap", new Vector3(i * .58f, -.05f, -.10f), new Vector3(.055f, .09f, .27f), steel);
        FacilityDoor door = unit.gameObject.AddComponent<FacilityDoor>(); door.leaf = leaf; door.openLocalEuler = new Vector3(62, 0, 0); door.locked = false; door.leafColliders = leaf.GetComponentsInChildren<Collider>();
        FacilityStoryInteractable target = Interaction(leaf, "Trunk handle - open lid", new Vector3(0, .02f, -.88f), new Vector3(.55f, .15f, .25f), director, FacilityStoryInteractionKind.Door, "Sedan trunk", null); target.door = door; door.interaction = target;
        Dynamic(unit.gameObject); NavIgnore(unit.gameObject);
        return door;
    }

    static FacilityDoor VaultDoor(Transform parent, FacilityStoryDirector director)
    {
        Transform unit = Group(parent, "Vault door - shut until drilling completes"); unit.position = new Vector3(-1.69f, 0, 30.16f);
        Transform leaf = Group(unit, "Reinforced vault leaf - physical hinge axis");
        Box(leaf, "Thick reinforced vault slab", new Vector3(1.69f, 1.49f, 0), new Vector3(3.38f, 2.94f, .38f), steel, true);
        Box(leaf, "Recessed steel front plate", new Vector3(1.69f, 1.49f, -.207f), new Vector3(3.03f, 2.59f, .035f), dark);
        Box(leaf, "Inner stiffening plate", new Vector3(1.69f, 1.49f, .21f), new Vector3(3.03f, 2.59f, .04f), steel);
        for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 6; i++)
            Box(leaf, "Vault plate captive bolt", new Vector3(1.69f + side * 1.40f, .30f + i * .47f, -.234f), new Vector3(.064f, .064f, .018f), steel);
        Box(leaf, "Locking wheel vertical spoke", new Vector3(2.48f, 1.55f, -.31f), new Vector3(.075f, .69f, .08f), steel);
        Box(leaf, "Locking wheel horizontal spoke", new Vector3(2.48f, 1.55f, -.31f), new Vector3(.69f, .075f, .08f), steel);
        Box(leaf, "Locking wheel spindle", new Vector3(2.48f, 1.55f, -.245f), new Vector3(.16f, .16f, .16f), steel);
        for (int i = 0; i < 3; i++) Box(unit, "Wall-anchored vault hinge", new Vector3(0, .38f + i * 1.12f, .04f), new Vector3(.27f, .28f, .38f), dark);
        FacilityDoor door = unit.gameObject.AddComponent<FacilityDoor>(); door.leaf = leaf; door.openLocalEuler = new Vector3(0, -100, 0); door.locked = true; door.leafColliders = leaf.GetComponentsInChildren<Collider>();
        Dynamic(unit.gameObject); NavIgnore(leaf.gameObject);
        return door;
    }

    static void DoubleDoors(Transform parent, FacilityStoryDirector director, Vector3 center, float width, float height, float yaw, bool glazed)
    {
        parent.position = center; parent.rotation = Quaternion.Euler(0, yaw, 0);
        float half = width * .5f;
        MakeDoor(parent, director, "Left hinged door", new Vector3(-half, 0, 0), half - .018f, height, 0, -100, glazed, false, true);
        MakeDoor(parent, director, "Right hinged door", new Vector3(half, 0, 0), half - .018f, height, 180, 100, glazed, false, true);
    }

    static FacilityDoor MakeDoor(Transform parent, FacilityStoryDirector director, string name, Vector3 hingePosition, float width, float height, float yaw, float openingAngle, bool glazed, bool solid, bool local = false)
    {
        Transform unit = Group(parent, name);
        if (local) { unit.localPosition = hingePosition; unit.localRotation = Quaternion.Euler(0, yaw, 0); }
        else { unit.position = hingePosition; unit.rotation = Quaternion.Euler(0, yaw, 0); }
        Transform leaf = Group(unit, "Door leaf - rotates at mounted hinges");
        if (glazed)
        {
            Box(leaf, "Glazed door left stile", new Vector3(.045f, height * .5f + .02f, 0), new Vector3(.09f, height, .075f), dark, true);
            Box(leaf, "Glazed door latch stile", new Vector3(width - .045f, height * .5f + .02f, 0), new Vector3(.09f, height, .075f), dark, true);
            Box(leaf, "Glazed door lower rail", new Vector3(width * .5f, .22f, 0), new Vector3(width, .40f, .075f), steel, true);
            Box(leaf, "Glazed door upper rail", new Vector3(width * .5f, height - .04f, 0), new Vector3(width, .12f, .075f), dark, true);
            Box(leaf, "Dark wired glass inset", new Vector3(width * .5f, (height + .42f) * .5f - .04f, 0), new Vector3(width - .17f, height - .54f, .045f), glass, true);
        }
        else
        {
            Box(leaf, "Pressed steel door", new Vector3(width * .5f, height * .5f + .02f, 0), new Vector3(width, height, .09f), steel, true);
            Box(leaf, "Recessed door upper inset", new Vector3(width * .5f, 1.93f, -.051f), new Vector3(width - .23f, .57f, .014f), dark);
            Box(leaf, "Recessed door reverse inset", new Vector3(width * .5f, 1.93f, .051f), new Vector3(width - .23f, .57f, .014f), dark);
            Box(leaf, "Scuffed kick plate", new Vector3(width * .5f, .22f, -.056f), new Vector3(width - .10f, .30f, .018f), dark);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Box(leaf, "Door pull standoff", new Vector3(width - .17f, 1.08f, side * .075f), new Vector3(.035f, .04f, .08f), steel);
            Box(leaf, "Door pull grip", new Vector3(width - .17f, 1.20f, side * .12f), new Vector3(.035f, .28f, .035f), steel);
            Box(unit, "Frame-mounted hinge lower", new Vector3(0, .42f, side * .025f), new Vector3(.13f, .15f, .13f), dark);
            Box(unit, "Frame-mounted hinge upper", new Vector3(0, height - .35f, side * .025f), new Vector3(.13f, .15f, .13f), dark);
        }
        FacilityDoor door = unit.gameObject.AddComponent<FacilityDoor>(); door.leaf = leaf; door.openLocalEuler = new Vector3(0, openingAngle, 0); door.locked = false; door.leafColliders = leaf.GetComponentsInChildren<Collider>();
        FacilityStoryInteractable target = Interaction(leaf, "Door interaction", new Vector3(width * .5f, 1.35f, 0), new Vector3(width, 2.1f, .25f), director, FacilityStoryInteractionKind.Door, "Door", null); target.door = door; door.interaction = target;
        Dynamic(unit.gameObject); NavIgnore(leaf.gameObject);
        return door;
    }

    static void CorrectProps(Transform story)
    {
        foreach (Transform item in story.GetComponentsInChildren<Transform>(true))
        {
            if (item.name == "Waiting room bench") item.localRotation = Quaternion.Euler(0, item.position.x < 0 ? -90 : 90, 0);
            if (item.name == "Records shelf - archived transfer files") item.localRotation = Quaternion.Euler(0, item.position.x < 0 ? -90 : 90, 0);
            if (item.name == "Abandoned CRT terminal")
            {
                Vector3 at = item.localPosition;
                if (item.parent.name == "Reception counter - open central knee space") at.z = 3.48f;
                if (item.parent.name == "03 Bent security passage and watch office")
                { at.z = 10.28f; item.localRotation = Quaternion.Euler(0, 180, 0); }
                item.localPosition = at;
                // Keyboards face the occupied workspace; screen backs face the wall or machine row.
            }
            if (item.name == "Workstation foot rail")
            {
                Vector3 at = item.localPosition; at.z += .45f; item.localPosition = at;
                MeshFilter filter = item.GetComponent<MeshFilter>(); if (filter != null) filter.sharedMesh = library.BoxMesh(new Vector3(3.24f, .06f, .06f));
            }
            if (item.name == "Counting tray")
            { Vector3 at = item.localPosition; at.y = .935f; item.localPosition = at; }
            if (item.name == "Vault gold bars")
            { Vector3 at = item.localPosition; at.y = .90f; item.localPosition = at; }
            if (item.name == "Cast gold ingot" && item.localPosition.y > .05f)
            { Vector3 at = item.localPosition; at.y = .10f; item.localPosition = at; }
            if (item.name == "Parking sodium lamp")
            {
                // The original ceiling backplate sat .16m above its supporting arm.
                Vector3 at = item.localPosition; at.y = 4.54f; item.localPosition = at;
            }
            if (item.name == "Transfer trolley handle upright")
            {
                // The broad panel was a floating wall. Replace with a welded U-shaped push handle.
                item.gameObject.SetActive(false);
                Transform parent = item.parent;
                Box(parent, "Trolley push handle left upright", new Vector3(2.01f, .61f, 39.15f), new Vector3(.055f, .60f, .055f), steel, false, true);
                Box(parent, "Trolley push handle right upright", new Vector3(2.99f, .61f, 39.15f), new Vector3(.055f, .60f, .055f), steel, false, true);
                Box(parent, "Trolley welded push grip", new Vector3(2.50f, .91f, 39.15f), new Vector3(1.035f, .055f, .055f), steel, false, true);
            }
        }
        Transform records = story.Find("04 Counting floor - records and transfer machinery");
        Transform office = story.Find("03 Bent security passage and watch office");
        if (office != null) EnsureOfficeChair(office);
        Transform pump = records != null ? records.Find("Transfer pump and unfinished repair bay") : null;
        if (pump != null)
        {
            // Join the short outlet back into the casing and terminate its free end in a floor flange.
            Transform outlet = pump.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "Mounted exposed service pipe");
            if (outlet != null)
            {
                outlet.localPosition = new Vector3(5.6f, 1.1f, 21.8f);
                // The saved original 0.9m pipe now overlaps the casing instead of stopping short.
            }
            RemoveNamed(pump, "Pipe wall saddle");
            Box(pump, "Pump outlet floor return", new Vector3(5.6f, .55f, 21.35f), new Vector3(.25f, 1.10f, .25f), steel, true, true);
            Box(pump, "Outlet return floor flange", new Vector3(5.6f, .035f, 21.35f), new Vector3(.43f, .07f, .43f), dark, false, true);
            Box(pump, "Motor rear service junction", new Vector3(5.6f, .8f, 24.38f), new Vector3(.38f, .38f, .14f), dark, false, true);
        }
        // Attach both long service pipes to the actual wall planes instead of leaving broad loose saddles.
        foreach (Transform saddle in story.GetComponentsInChildren<Transform>(true).Where(item => item.name == "Pipe wall saddle"))
        {
            if (!saddle.parent.name.StartsWith("03 ", StringComparison.Ordinal) && !saddle.parent.name.StartsWith("04 ", StringComparison.Ordinal)) continue;
            Vector3 at = saddle.localPosition; bool security = saddle.parent.name.StartsWith("03 ", StringComparison.Ordinal);
            at.x = security ? -6.89f : -11.83f; saddle.localPosition = at;
            MeshFilter filter = saddle.GetComponent<MeshFilter>(); if (filter != null) filter.sharedMesh = library.BoxMesh(new Vector3(security ? .36f : .36f, .12f, .075f));
        }
    }

    static FacilityStoryInteractable Interaction(Transform parent, string name, Vector3 position, Vector3 size, FacilityStoryDirector director, FacilityStoryInteractionKind kind, string label, GameObject visual)
    {
        Transform root = Group(parent, name); root.localPosition = position;
        BoxCollider collider = root.gameObject.AddComponent<BoxCollider>(); collider.size = size; collider.isTrigger = true;
        FacilityStoryInteractable interaction = root.gameObject.AddComponent<FacilityStoryInteractable>(); interaction.director = director; interaction.kind = kind; interaction.label = label; interaction.visualRoot = visual; NavIgnore(root.gameObject); return interaction;
    }
    static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collide = false, bool baked = false)
    {
        Transform item = Group(parent, name); item.localPosition = position; item.gameObject.AddComponent<MeshFilter>().sharedMesh = library.BoxMesh(size);
        MeshRenderer renderer = item.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; renderer.lightProbeUsage = baked ? LightProbeUsage.Off : LightProbeUsage.BlendProbes;
        if (collide) { BoxCollider collider = item.gameObject.AddComponent<BoxCollider>(); collider.size = size; }
        if (baked) GameObjectUtility.SetStaticEditorFlags(item.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return item;
    }
    static void Dynamic(GameObject root)
    {
        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
            Renderer renderer = part.GetComponent<Renderer>(); if (renderer != null) { renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.receiveShadows = true; }
        }
    }
    static void NavIgnore(GameObject item) { NavMeshModifier modifier = item.GetComponent<NavMeshModifier>(); if (modifier == null) modifier = item.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild = true; }
    static void RemoveNamed(Transform parent, params string[] names)
    {
        foreach (Transform child in parent.Cast<Transform>().ToArray()) if (names.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
    }
    static Material StoryMaterial(string name, string fallback) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Story/Materials/" + name + ".mat") ?? library.Material(fallback);
    static Transform Group(Transform parent, string name) { var item = new GameObject(name); item.transform.SetParent(parent, false); return item.transform; }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
