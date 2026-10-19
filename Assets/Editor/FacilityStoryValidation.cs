using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Explicit saved-asset, navigation and physical traversal checks for the authored story scene.</summary>
public static class FacilityStoryValidation
{
    const float Step = 1f / 120f;
    static readonly Vector3[] MainRoute =
    {
        new Vector3(-6,0,-18), new Vector3(-6,0,-12), new Vector3(0,0,-8), new Vector3(0,0,2.35f),
        new Vector3(3,0,2.35f), new Vector3(3,0,6.5f), new Vector3(4.5f,0,6.5f), new Vector3(4.5f,0,14),
        new Vector3(-4,0,14), new Vector3(-4,0,17.6f), new Vector3(0,0,17.6f), new Vector3(0,0,29), new Vector3(0,0,34.5f)
    };
    static readonly Vector3[] CounterLoop =
    {
        new Vector3(0,0,2.35f), new Vector3(3,0,2.35f), new Vector3(3,0,6.1f),
        new Vector3(-3,0,6.1f), new Vector3(-3,0,2.35f), new Vector3(0,0,2.35f)
    };
    static readonly Vector3[] OfficeBranch =
    {
        new Vector3(4.5f,0,10.35f), new Vector3(-1,0,10.35f), new Vector3(-1,0,9.2f), new Vector3(-5.6f,0,9.2f)
    };

    public static string Validate(Scene scene)
    {
        RequireScene(scene);
        var failures = new List<string>();
        FacilityGameSession session = Find<FacilityGameSession>(scene);
        FacilityStoryDirector director = Find<FacilityStoryDirector>(scene);
        if (session == null || director == null) throw new InvalidOperationException("Story scene requires its saved session and director.");
        RetroFPSController player = session.player;
        FacilityWeaponInventory inventory = session.inventory;
        Check(player != null && player.GetComponent<CharacterController>() != null, "Original Player capsule is missing.", failures);
        Check(inventory != null && inventory.fists != null && inventory.pistol != null && inventory.shotgun != null &&
            inventory.ak47 != null && inventory.fistsModel != null && inventory.pistolModel != null && inventory.shotgunModel != null && inventory.ak47Model != null,
            "The four authored weapons and inventory are not fully assigned.", failures);
        Check(session.storyDirector == director && director.player == player && director.inventory == inventory,
            "Session, director and preserved Player references disagree.", failures);
        Check(session.modeMenu != null && session.storyButton != null && session.sandboxButton != null && session.modeBackButton != null &&
            session.completionMenu != null && session.returnMainButton != null && session.nextLevelButton != null && session.objectiveHud != null &&
            session.weaponNameText != null && session.healthFill != null && session.damageOverlay != null,
            "Story/Sandbox, health, weapon or completion UI references are missing.", failures);
        Check(director.typedObjective != null && director.typedObjective.titleText != null && director.typedObjective.bodyText != null &&
            director.typedObjective.counterText != null && director.interactionText != null, "Typed objective/interaction UI is missing.", failures);
        Check(director.receptionist != null && director.spawnPoint != null && director.lobbyEntry != null &&
            director.cashPickup != null && director.goldPickup != null && director.sedanExit != null,
            "Mission actors, spawn or interactables are missing.", failures);
        Check(director.pistolPickup == null && director.pistolAlarmWarning == null,
            "The removed counter pistol and its warning must not remain in the mission.", failures);
        if (director.cashPickup != null) Check(director.cashPickup.kind == FacilityStoryInteractionKind.Cash, "Cash interaction has the wrong kind.", failures);
        if (director.goldPickup != null) Check(director.goldPickup.kind == FacilityStoryInteractionKind.Gold, "Gold interaction has the wrong kind.", failures);
        if (director.sedanExit != null) Check(director.sedanExit.kind == FacilityStoryInteractionKind.Sedan, "Sedan interaction has the wrong kind.", failures);
        Check(director.drillPickup != null && director.drillPickup.door != null && director.drillPickup.kind == FacilityStoryInteractionKind.Drill,
            "The drill must be saved inside the sedan's closed trunk.", failures);
        Check(director.vaultInteraction != null && director.vaultDrill != null && director.vaultDrill.vaultDoor != null &&
            director.vaultDrill.vaultDoor.locked && director.vaultDrill.deployedDrill != null && !director.vaultDrill.deployedDrill.activeSelf &&
            FacilityVaultDrill.RequiredDrillingSeconds == 45f, "The locked vault requires a persistent forty-five-second drill.", failures);
        Check(director.policeWaves != null && director.policeWaves.interval == 15f && director.policeWaves.maxActivePolice == 8 &&
            director.policeWaves.policePrefab != null && EditorUtility.IsPersistent(director.policeWaves.policePrefab) &&
            director.policeWaves.projectiles != null && director.policeWaves.spawnPoints.Length >= 3,
            "The saved fifteen-second police waves, bounded actors or swept projectiles are missing.", failures);
        Check(director.heistStatusText != null && director.typedObjective.typingSource != null && director.typedObjective.typingClick != null &&
            director.typedObjective.bodyText.font != null && director.typedObjective.bodyText.font.name.Contains("JetBrains"),
            "Heist countdown, JetBrains Mono or typing audio is missing.", failures);
        foreach (GameObject doorRoot in scene.GetRootGameObjects())
        foreach (FacilityDoor door in doorRoot.GetComponentsInChildren<FacilityDoor>(true))
            Check(door.leaf != null && !door.IsOpen, "An authored door is not stored closed.", failures);
        Check(RenderSettings.ambientLight.maxColorComponent < .2f && RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear &&
            RenderSettings.fogColor.maxColorComponent < .1f, "Night ambient and fog settings are missing.", failures);

        var meshes = new HashSet<Mesh>(); var materials = new HashSet<Material>(); var shirtColors = new HashSet<Color32>();
        int baked = 0, mixed = 0, realtime = 0, renderers = 0, giMeshes = 0, audio = 0, navSurfaces = 0;
        Transform vehicle=director.sedanExit!=null?director.sedanExit.transform.parent:null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0) failures.Add("Missing script on " + Path(node));
            foreach (Component component in node.GetComponents<Component>()) if (component != null) CheckReferences(component, failures);
            MeshFilter filter = node.GetComponent<MeshFilter>();
            if (filter != null)
            {
                Check(filter.sharedMesh != null && EditorUtility.IsPersistent(filter.sharedMesh), "Unsaved/missing mesh on " + Path(node), failures);
                if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            }
            Renderer renderer = node.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderers++;
                foreach (Material material in renderer.sharedMaterials)
                {
                    Check(material != null && EditorUtility.IsPersistent(material) && material.shader != null && material.shader.isSupported &&
                        material.shader.name != "Hidden/InternalErrorShader", "Unsaved/missing/unsupported material on " + Path(node), failures);
                    if (material != null) materials.Add(material);
                }
                if ((GameObjectUtility.GetStaticEditorFlags(node.gameObject) & StaticEditorFlags.ContributeGI) != 0 && filter != null && filter.sharedMesh != null)
                {
                    giMeshes++;
                    Check(filter.sharedMesh.uv2 != null && filter.sharedMesh.uv2.Length == filter.sharedMesh.vertexCount,
                        "Baked geometry has no persistent lightmap UVs: " + Path(node), failures);
                }
            }
            AudioSource source = node.GetComponent<AudioSource>();
            if (source != null)
            {
                audio++;
                Check(source.clip != null && EditorUtility.IsPersistent(source.clip), "Audio source lacks a persistent clip: " + Path(node), failures);
            }
            Light light = node.GetComponent<Light>();
            if (light != null && (player == null || !node.IsChildOf(player.transform)))
            {
                if (light.lightmapBakeType == LightmapBakeType.Baked) baked++;
                else if (light.lightmapBakeType == LightmapBakeType.Realtime)
                {
                    realtime++;
                    bool carLight=vehicle!=null && node.IsChildOf(vehicle);
                    Check(carLight ? light.shadows != LightShadows.None : light.shadows == LightShadows.None,
                        "Incorrect shadow setting on realtime story light: " + Path(node), failures);
                }
                else
                {
                    mixed++;
                    Check(node.parent!=null && node.parent.name=="Parking sodium lamp" && light.shadows!=LightShadows.None,
                        "Only the physical shadow-casting parking fixture should use mixed lighting: " + Path(node), failures);
                }
                if (light.type == LightType.Directional && light.enabled && light.intensity > .15f) failures.Add("Strong daylight is enabled in the night mission.");
            }
            NavMeshSurface surface = node.GetComponent<NavMeshSurface>();
            if (surface != null)
            {
                navSurfaces++;
                Check(surface.navMeshData != null && EditorUtility.IsPersistent(surface.navMeshData), "Story navigation data is missing or unsaved.", failures);
            }
        }
        int carBeams=0;
        if(director.sedanExit!=null && director.sedanExit.transform.parent!=null)
            foreach(Light light in director.sedanExit.transform.parent.GetComponentsInChildren<Light>(true))
                if(light.lightmapBakeType==LightmapBakeType.Realtime)carBeams++;
        Check(baked == 9 && mixed == 1 && realtime == 2+carBeams,
            "Expected nine baked fixtures, one mixed parking lamp, two alarm lights and the Challenger lamps; found " + baked + "/" + mixed + "/" + realtime + ".", failures);
        Check(giMeshes > 0, "No static story geometry contributes to the baked lighting.", failures);
        Check(navSurfaces == 1, "Expected one saved story navigation surface, found " + navSurfaces + ".", failures);
        Check(director.alarmSource != null && director.alarmSource.clip != null && !director.alarmSource.playOnAwake,
            "Alarm must have a persistent clip and remain quiet until the receptionist is attacked.", failures);
        Check(director.alarmLights != null && director.alarmLights.Length == 2, "Exactly two alarm spill lights must be assigned.", failures);
        if (director.alarmLights != null)
            foreach (Light light in director.alarmLights)
                Check(light != null && !light.enabled && light.intensity > 0f && light.shadows == LightShadows.None,
                    "Alarm light must be disabled before the mission but retain nonzero authored intensity.", failures);
        Check(director.guards != null && director.guards.Length == 5, "Expected five knife-capable observers including the receptionist.", failures);
        if (director.guards != null)
            foreach (FacilityKnifeGuard guard in director.guards)
            {
                Check(guard != null && guard.actor != null && guard.knifeProp != null && guard.walkSpeed >= 2f && !guard.startAlerted,
                    "A story observer is missing its faster, initially passive knife behavior.", failures);
                if (guard == null || guard.actor == null) continue;
                Check(guard.actor.player == player.transform, "Knife guard targets a different Player.", failures);
                Check(guard.knifeProp != null && guard.knifeProp.GetComponentInParent<FacilityHitRegion>() != null,
                    "Knife is not physically attached to a dismemberable hand/arm.", failures);
                FacilityHitRegion torso = guard.actor.chest != null ? guard.actor.chest.GetComponent<FacilityHitRegion>() : null;
                if (torso != null && torso.outerPatches != null)
                    foreach (Renderer panel in torso.outerPatches)
                        if (panel != null && panel.sharedMaterial != null) { shirtColors.Add((Color32)panel.sharedMaterial.color); break; }
                if (director.spawnPoint != null) CheckNavPath(guard.transform.position, director.spawnPoint.position, "Guard to sedan route", failures);
            }
        Check(shirtColors.Count >= 4, "Observer shirts do not have enough distinct authored colors.", failures);
        for (int i = 1; i < MainRoute.Length; i++) CheckNavPath(MainRoute[i - 1], MainRoute[i], "Main route " + i, failures);
        CheckNavPath(OfficeBranch[0], OfficeBranch[OfficeBranch.Length - 1], "Watch office branch", failures);
        string metrics = renderers + " renderers, " + meshes.Count + " persistent meshes, " + materials.Count + " materials, " +
            baked + " baked / " + mixed + " mixed / " + realtime + " realtime lights, " + giMeshes + " GI meshes, " + audio + " audio sources, " +
            shirtColors.Count + " shirt colors, " + LightmapSettings.lightmaps.Length + " lightmaps.";
        if (failures.Count > 0) throw new InvalidOperationException("Story validation failed. " + metrics + "\n- " + string.Join("\n- ", failures));
        return "Story validation passed. " + metrics;
    }

    public static string Traverse(Scene scene)
    {
        RequireScene(scene);
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run story traversal in Edit mode.");
        RetroFPSController input = Find<RetroFPSController>(scene);
        if (input == null) throw new InvalidOperationException("The preserved Player controller is missing.");
        CharacterController controller = input.GetComponent<CharacterController>();
        if (controller == null || input.walkSpeed <= 0f || input.gravity >= 0f || Vector3.Distance(input.transform.lossyScale, Vector3.one) > .001f)
            throw new InvalidOperationException("Traversal requires the original unscaled CharacterController and movement settings.");
        Vector3 position = input.transform.position; Quaternion rotation = input.transform.rotation;
        bool inputEnabled = input.enabled, controllerEnabled = controller.enabled, changed = input.transform.hasChanged;
        var doorLeaves = new List<Transform>(); var doorPositions = new List<Vector3>(); var doorRotations = new List<Quaternion>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (FacilityDoor door in root.GetComponentsInChildren<FacilityDoor>(true))
        {
            if (door.leaf == null) continue;
            doorLeaves.Add(door.leaf); doorPositions.Add(door.leaf.localPosition); doorRotations.Add(door.leaf.localRotation);
            // Route clearance is checked with the physically rotated leaf in its usable open pose.
            // The separate runtime check verifies closed collision, E use and the real 45-second lock.
            door.leaf.localPosition += door.openLocalOffset;
            door.leaf.localRotation *= Quaternion.Euler(door.openLocalEuler);
        }
        Physics.SyncTransforms();
        var stats = new Stats(); var overlaps = new Collider[64];
        try
        {
            input.enabled = false;
            Walk("Heist outward", MainRoute, false, controller, input, scene.GetPhysicsScene(), overlaps, stats);
            Walk("Heist return", MainRoute, true, controller, input, scene.GetPhysicsScene(), overlaps, stats);
            Walk("Reception both sides", CounterLoop, false, controller, input, scene.GetPhysicsScene(), overlaps, stats);
            Walk("Reception reverse", CounterLoop, true, controller, input, scene.GetPhysicsScene(), overlaps, stats);
            Walk("Watch office", OfficeBranch, false, controller, input, scene.GetPhysicsScene(), overlaps, stats);
            Walk("Watch office return", OfficeBranch, true, controller, input, scene.GetPhysicsScene(), overlaps, stats);
        }
        finally
        {
            controller.enabled = false; input.transform.SetPositionAndRotation(position, rotation);
            controller.enabled = controllerEnabled; input.enabled = inputEnabled;
            for (int i = 0; i < doorLeaves.Count; i++)
                if (doorLeaves[i] != null) doorLeaves[i].SetLocalPositionAndRotation(doorPositions[i], doorRotations[i]);
            Physics.SyncTransforms(); input.transform.hasChanged = changed;
        }
        return string.Format("Story physical traversal passed: sedan, entrance, both counter aisles, security dogleg, counting floor, vault and office branch in both directions; {0} checkpoints, {1:F1} m, {2} actual CharacterController.Move steps at 120 Hz. No jumping; Player restored.", stats.checkpoints, stats.distance, stats.steps);
    }

    public static void Capture(Scene scene)
    {
        RequireScene(scene);
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture the story outside Play mode.");
        RetroFPSController player = Find<RetroFPSController>(scene);
        Camera camera = player != null ? player.GetComponentInChildren<Camera>() : null;
        if (camera == null) throw new InvalidOperationException("The original Player camera is missing.");
        FacilityWeaponInventory inventory = player.GetComponent<FacilityWeaponInventory>();
        Transform[] models = inventory != null ? new[] { inventory.fistsModel, inventory.pistolModel, inventory.shotgunModel } : new Transform[0];
        var activeModels = new bool[models.Length];
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        RenderTexture originalTarget = camera.targetTexture, active = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        string directory = System.IO.Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".codex", "story-previews");
        Directory.CreateDirectory(directory); target.Create();
        try
        {
            for (int i = 0; i < models.Length; i++) if (models[i] != null) { activeModels[i] = models[i].gameObject.activeSelf; models[i].gameObject.SetActive(false); }
            Render(camera, target, directory, "01_sedan_night", new Vector3(-2,1.65f,-23), new Vector3(-6,1.1f,-15));
            Render(camera, target, directory, "02_parking_entrance", new Vector3(-6,1.65f,-18), new Vector3(0,1.8f,-4));
            Render(camera, target, directory, "15_national_bank_roof_sign", new Vector3(0,1.65f,-14), new Vector3(0,4.3f,-4));
            Render(camera, target, directory, "03_reception", new Vector3(0,1.65f,-2), new Vector3(0,1.3f,4.4f));
            FacilityStoryDirector faceDirector = Find<FacilityStoryDirector>(scene);
            FacilityHostileExpression expression = faceDirector != null && faceDirector.receptionist != null ? faceDirector.receptionist.GetComponent<FacilityHostileExpression>() : null;
            if (expression != null && expression.leftBrow != null && expression.rightBrow != null)
            {
                Quaternion left = expression.leftBrow.localRotation, right = expression.rightBrow.localRotation;
                Vector3 face = faceDirector.receptionist.head.position;
                Vector3 faceCamera = face + faceDirector.receptionist.transform.forward * .8f + Vector3.up * .02f;
                try
                {
                    Render(camera,target,directory,"16_neutral_face",faceCamera,face);
                    expression.leftBrow.localRotation = Quaternion.Euler(0,0,-24); expression.rightBrow.localRotation = Quaternion.Euler(0,0,24);
                    Render(camera,target,directory,"17_angry_face",faceCamera,face);
                }
                finally { expression.leftBrow.localRotation = left; expression.rightBrow.localRotation = right; }
            }
            Render(camera, target, directory, "04_under_counter_pistol", new Vector3(0,1.65f,2.35f), new Vector3(0,.52f,3.72f));
            Render(camera, target, directory, "14_reception_employee_side", new Vector3(1.1f,1.65f,5.2f), new Vector3(0,.49f,3.72f));
            Render(camera, target, directory, "05_security_approach", new Vector3(4.5f,1.65f,7), new Vector3(4.5f,1.65f,14));
            Render(camera, target, directory, "06_watch_office", new Vector3(-.3f,1.65f,9.2f), new Vector3(-4,1.3f,10.4f));
            Render(camera, target, directory, "07_counting_floor", new Vector3(0,1.65f,18), new Vector3(0,2.1f,28));
            Render(camera, target, directory, "08_transfer_pump", new Vector3(1,1.65f,23), new Vector3(6,1.4f,23));
            Render(camera, target, directory, "09_vault_door", new Vector3(0,1.65f,29), new Vector3(0,1.4f,35));
            Render(camera, target, directory, "10_cash_and_gold", new Vector3(0,1.65f,34.5f), new Vector3(0,1.1f,36.2f));
            FacilityStoryDirector director = Find<FacilityStoryDirector>(scene);
            if (director != null && director.drillPickup != null && director.drillPickup.door != null)
            {
                FacilityDoor trunk = director.drillPickup.door;
                Render(camera, target, directory, "11_trunk_closed", new Vector3(-8.1f,1.65f,-23), new Vector3(-8.1f,.9f,-20));
                Vector3 localPosition = trunk.leaf.localPosition; Quaternion localRotation = trunk.leaf.localRotation;
                try
                {
                    trunk.leaf.localPosition += trunk.openLocalOffset;
                    trunk.leaf.localRotation *= Quaternion.Euler(trunk.openLocalEuler);
                    Render(camera, target, directory, "12_drill_in_open_trunk", new Vector3(-7.8f,1.45f,-21.4f), director.drillPickup.transform.position);
                }
                finally { trunk.leaf.SetLocalPositionAndRotation(localPosition, localRotation); }
            }
            if (director != null && director.vaultDrill != null && director.vaultDrill.deployedDrill != null)
            {
                GameObject drill = director.vaultDrill.deployedDrill; bool activeDrill = drill.activeSelf;
                try
                {
                    drill.SetActive(true);
                    Render(camera, target, directory, "13_drill_on_vault", new Vector3(-1.2f,1.65f,27.8f), drill.transform.position);
                }
                finally { drill.SetActive(activeDrill); }
            }
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.targetTexture = originalTarget; RenderTexture.active = active;
            for (int i = 0; i < models.Length; i++) if (models[i] != null) models[i].gameObject.SetActive(activeModels[i]);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void Walk(string label, Vector3[] route, bool reverse, CharacterController cc, RetroFPSController input, PhysicsScene physics, Collider[] overlaps, Stats stats)
    {
        int increment = reverse ? -1 : 1, index = reverse ? route.Length - 1 : 0;
        Vector3 first = route[index];
        cc.enabled = false; cc.transform.position = first + Vector3.up * (.05f - cc.center.y + cc.height * .5f); cc.enabled = true;
        Physics.SyncTransforms(); float vertical = 0f;
        Settle(cc, input.gravity, ref vertical, stats); CheckPoint(label, first, cc, physics, overlaps, stats);
        for (index += increment; index >= 0 && index < route.Length; index += increment)
        {
            Vector3 goal = route[index];
            float initial = Horizontal(cc.transform.position, goal), best = initial;
            int budget = Mathf.CeilToInt((initial / input.walkSpeed + 4f) / Step), stalled = 0;
            while (Horizontal(cc.transform.position, goal) > .10f)
            {
                if (budget-- <= 0 || stalled > 180) throw new InvalidOperationException(label + " blocked before " + goal + "; player at " + cc.transform.position + ".");
                Vector3 delta = goal - cc.transform.position; delta.y = 0f;
                Move(cc, delta.normalized * Mathf.Min(input.walkSpeed * Step, delta.magnitude), input.gravity, ref vertical, stats);
                float distance = Horizontal(cc.transform.position, goal);
                if (distance < best - .01f) { best = distance; stalled = 0; } else stalled++;
                if (Feet(cc) < -.5f) throw new InvalidOperationException(label + " fell below the level near " + cc.transform.position + ".");
                if ((stats.steps & 7) == 0) Clearance(label, cc, physics, overlaps);
            }
            Settle(cc, input.gravity, ref vertical, stats); CheckPoint(label, goal, cc, physics, overlaps, stats);
        }
    }

    static void Move(CharacterController cc, Vector3 horizontal, float gravity, ref float vertical, Stats stats)
    {
        if (cc.isGrounded && vertical < 0f) vertical = -2f;
        vertical += gravity * Step;
        Vector3 before = cc.transform.position;
        CollisionFlags flags = cc.Move(horizontal + Vector3.up * (vertical * Step));
        if ((flags & CollisionFlags.Below) != 0 && vertical < 0f) vertical = -2f;
        stats.distance += Vector3.Distance(before, cc.transform.position); stats.steps++;
    }
    static void Settle(CharacterController cc, float gravity, ref float vertical, Stats stats)
    { for (int i = 0; i < 36; i++) Move(cc, Vector3.zero, gravity, ref vertical, stats); }
    static void CheckPoint(string label, Vector3 goal, CharacterController cc, PhysicsScene physics, Collider[] overlaps, Stats stats)
    {
        if (!cc.isGrounded || Mathf.Abs(Feet(cc) - goal.y) > .25f)
            throw new InvalidOperationException(label + " is not grounded at " + goal + "; actual feet " + Feet(cc) + ".");
        Clearance(label, cc, physics, overlaps); stats.checkpoints++;
    }
    static void Clearance(string label, CharacterController cc, PhysicsScene physics, Collider[] overlaps)
    {
        Physics.SyncTransforms();
        Vector3 center = cc.transform.TransformPoint(cc.center);
        float radius = Mathf.Max(.01f, cc.radius - Mathf.Max(.015f, cc.skinWidth + .005f));
        float segment = Mathf.Max(0f, cc.height * .5f - cc.radius);
        int count = physics.OverlapCapsule(center - Vector3.up * segment, center + Vector3.up * segment, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) throw new InvalidOperationException("Story capsule query overflowed.");
        for (int i = 0; i < count; i++)
        {
            Collider item = overlaps[i];
            if (item == null || item == cc || item.transform.IsChildOf(cc.transform) || item.gameObject.scene != cc.gameObject.scene) continue;
            throw new InvalidOperationException(label + " intersects " + Path(item.transform) + " at " + cc.transform.position + ".");
        }
    }
    static void CheckNavPath(Vector3 from, Vector3 to, string label, List<string> failures)
    {
        if (!NavMesh.SamplePosition(from + Vector3.up * .15f, out NavMeshHit a, 1f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(to + Vector3.up * .15f, out NavMeshHit b, 1f, NavMesh.AllAreas))
        { failures.Add(label + " has an unsupported endpoint."); return; }
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            failures.Add(label + " has no complete navigation route.");
    }
    static void Render(Camera camera, RenderTexture target, string directory, string name, Vector3 eye, Vector3 look)
    {
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try { texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), texture.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    static void CheckReferences(Component component, List<string> failures)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter))
        {
            enter = false;
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                failures.Add("Broken reference " + component.GetType().Name + "." + property.propertyPath + " on " + Path(component.transform));
        }
    }
    static T Find<T>(Scene scene) where T : Component
    {
        T found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T candidate in root.GetComponentsInChildren<T>(false))
            {
                if (found != null) throw new InvalidOperationException("Expected one active " + typeof(T).Name + ".");
                found = candidate;
            }
        return found;
    }
    static float Feet(CharacterController cc) => cc.transform.TransformPoint(cc.center).y - cc.height * .5f;
    static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    static string Path(Transform node) { string path = node.name; while (node.parent != null) { node = node.parent; path = node.name + "/" + path; } return path; }
    static void Check(bool condition, string failure, List<string> failures) { if (!condition) failures.Add(failure); }
    static void RequireScene(Scene scene) { if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded story scene is required."); }
    sealed class Stats { public int checkpoints, steps; public float distance; }
}
