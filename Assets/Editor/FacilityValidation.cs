using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Editor checks of the saved facility and physical, jump-free player traversal.</summary>
public static class FacilityValidation
{
    private const float StepTime = 1f / 120f;
    private const float ArrivalRadius = 0.10f;
    private const float HeightTolerance = 0.25f;

    private static readonly Vector3[] Circuit =
    {
        new Vector3(0f, 0f, -3f),
        new Vector3(0f, 0f, 11f),
        new Vector3(6f, 0f, 11f),
        new Vector3(6f, 0f, 18f),
        new Vector3(0f, 0f, 18f),
        new Vector3(0f, 3f, 28.5f),
        new Vector3(0f, 3f, 37f),
        new Vector3(-12f, 3f, 37f),
        new Vector3(-12f, 3f, 30f),
        new Vector3(-12f, 0f, 18f),
        new Vector3(-12f, 0f, 5f),
        new Vector3(-12f, 0f, 0f),
        new Vector3(0f, 0f, 0f)
    };

    private static readonly Vector3[] Shortcut =
    {
        new Vector3(-3f, 0f, 22f),
        new Vector3(-8f, 0f, 22f),
        new Vector3(-8f, 0f, 16f),
        new Vector3(-12f, 0f, 16f)
    };

    private static readonly Vector3[] RampForecourt =
    {
        new Vector3(-3f, 0f, 22f),
        new Vector3(-8f, 0f, 22f),
        new Vector3(-12f, 0f, 22f),
        new Vector3(-12f, 0f, 18f)
    };

    private static readonly Opening[] Openings =
    {
        new Opening("Arrival transit portal", new Vector3(0, 0, 5), Vector3.forward, 2),
        new Opening("Factory entrance", new Vector3(6, 0, 17), Vector3.forward, 2),
        new Opening("Research entrance", new Vector3(0, 3, 35), Vector3.forward, 1.5f),
        new Opening("Research ramp entrance", new Vector3(-12, 3, 29), Vector3.forward, 1.5f),
        new Opening("Maintenance service portal", new Vector3(-12, 0, 11), Vector3.forward, 2),
        new Opening("Arrival service portal", new Vector3(-5, 0, 0), Vector3.right, 2),
        new Opening("Maintenance shortcut", new Vector3(-6, 0, 22), Vector3.right, 2),
        new Opening("Stair top landing", new Vector3(0, 3, 28.25f), Vector3.forward, 1.5f),
        new Opening("Ramp lower approach", new Vector3(-12, 0, 21.5f), Vector3.forward, 1.5f)
    };

    private static readonly Vector3[] TurningSpaces =
    {
        new Vector3(0, 0, 11), new Vector3(6, 0, 11), new Vector3(6, 0, 18),
        new Vector3(0, 0, 18.5f), new Vector3(0, 3, 37), new Vector3(-12, 3, 37),
        new Vector3(-8, 0, 22), new Vector3(-8, 0, 16), new Vector3(-12, 0, 16),
        new Vector3(-12, 0, 21), new Vector3(-12, 0, 0)
    };

    public static string Validate(Scene scene)
    {
        RequireScene(scene);
        var failures = new List<string>();
        var roots = scene.GetRootGameObjects();
        var meshes = new HashSet<Mesh>();
        var materials = new HashSet<Material>();
        int renderers = 0;
        int colliders = 0;
        int bakedLights = 0;
        int realtimeLights = 0;
        int activeVolumes = 0;
        int mainCameras = 0;
        int listeners = 0;
        int players = 0;
        int ambientComponents = 0;

        foreach (GameObject root in roots)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject obj = node.gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj) != 0)
                    failures.Add("Missing script on " + Path(node));

                foreach (Component component in obj.GetComponents<Component>())
                {
                    if (component != null)
                        FindBrokenReferences(component, failures);
                }

                if (!obj.activeInHierarchy)
                    continue;

                foreach (MonoBehaviour behaviour in obj.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null || !behaviour.enabled)
                        continue;

                    string type = behaviour.GetType().Name;
                    if (type == "RetroWorldBuilder" || type == "FacilityExpansion" || type == "FacilityAtmosphere")
                        failures.Add("Legacy runtime generator is enabled: " + Path(node) + " / " + type);
                }

                var player = obj.GetComponent<RetroFPSController>();
                if (player != null && player.enabled)
                {
                    players++;
                    if (player.cameraPivot == null)
                        failures.Add("Player cameraPivot is not assigned.");
                    var controller = obj.GetComponent<CharacterController>();
                    if (controller == null || !controller.enabled)
                        failures.Add("Player needs an enabled CharacterController.");
                    else if (controller.radius <= 0f || controller.height < controller.radius * 2f)
                        failures.Add("Player capsule dimensions are invalid.");
                }

                var filter = obj.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    if (filter.sharedMesh == null)
                        failures.Add("Missing mesh on " + Path(node));
                    else
                    {
                        meshes.Add(filter.sharedMesh);
                        if (!EditorUtility.IsPersistent(filter.sharedMesh))
                            failures.Add("Mesh has not been saved as an asset: " + Path(node));
                    }
                }

                var renderer = obj.GetComponent<Renderer>();
                if (renderer != null && renderer.enabled)
                {
                    renderers++;
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                            failures.Add("Missing material or shader on " + Path(node));
                        else
                        {
                            materials.Add(material);
                            if (!EditorUtility.IsPersistent(material))
                                failures.Add("Material has not been saved as an asset: " + Path(node));
                        }
                    }
                }

                foreach (Collider collider in obj.GetComponents<Collider>())
                {
                    if (collider.enabled && !collider.isTrigger && !(collider is CharacterController))
                        colliders++;
                }

                var light = obj.GetComponent<Light>();
                if (light != null && light.enabled)
                {
                    if (light.lightmapBakeType == LightmapBakeType.Baked)
                        bakedLights++;
                    else if (light.lightmapBakeType == LightmapBakeType.Realtime)
                    {
                        realtimeLights++;
                        if (light.shadows != LightShadows.None)
                            failures.Add("Realtime light casts shadows: " + Path(node));
                    }
                    else
                        failures.Add("Mixed lights are outside the prototype lighting budget: " + Path(node));
                }

                var volume = obj.GetComponent<Volume>();
                if (volume != null && volume.enabled)
                {
                    activeVolumes++;
                    if (!volume.isGlobal || volume.sharedProfile == null || !EditorUtility.IsPersistent(volume.sharedProfile))
                        failures.Add("Facility volume needs a saved global VolumeProfile: " + Path(node));
                    else
                    {
                        foreach (VolumeComponent effect in volume.sharedProfile.components)
                        {
                            if (effect == null || !EditorUtility.IsPersistent(effect))
                                failures.Add("Volume contains a missing or unsaved override: " + Path(node));
                        }
                    }
                }

                var camera = obj.GetComponent<Camera>();
                if (camera != null && camera.enabled && obj.CompareTag("MainCamera"))
                {
                    mainCameras++;
                    var cameraData = obj.GetComponent<UniversalAdditionalCameraData>();
                    if (cameraData == null || !cameraData.renderPostProcessing)
                        failures.Add("Main Camera must enable URP post-processing.");
                }

                var listener = obj.GetComponent<AudioListener>();
                if (listener != null && listener.enabled)
                    listeners++;

                var ambient = obj.GetComponent<FacilityAmbientEffects>();
                if (ambient != null && ambient.enabled)
                {
                    ambientComponents++;
                    ValidateAmbience(ambient, failures);
                }
            }
        }

        if (players != 1) failures.Add("Expected one active Player, found " + players + ".");
        if (mainCameras != 1) failures.Add("Expected one active Main Camera, found " + mainCameras + ".");
        if (listeners != 1) failures.Add("Expected one active AudioListener, found " + listeners + ".");
        if (activeVolumes != 1) failures.Add("Expected one active Volume, found " + activeVolumes + ".");
        if (ambientComponents != 1) failures.Add("Expected one active FacilityAmbientEffects, found " + ambientComponents + ".");
        if (colliders == 0) failures.Add("Facility has no solid colliders.");
        if (bakedLights > 16) failures.Add("Baked light budget exceeded: " + bakedLights + " / 16.");
        if (realtimeLights > 2) failures.Add("Realtime light budget exceeded: " + realtimeLights + " / 2.");

        string metrics = string.Format("{0} renderers, {1} unique meshes, {2} materials, {3} solid colliders, {4}/16 baked lights, {5}/2 realtime lights.",
            renderers, meshes.Count, materials.Count, colliders, bakedLights, realtimeLights);
        if (failures.Count != 0)
            throw new InvalidOperationException("Facility validation failed. " + metrics + "\n- " + string.Join("\n- ", failures));

        return "Facility validation passed. " + metrics;
    }

    public static string Traverse(Scene scene)
    {
        RequireScene(scene);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run facility traversal in Edit Mode so gameplay cannot move the Player during validation.");

        RetroFPSController input = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RetroFPSController candidate in root.GetComponentsInChildren<RetroFPSController>(true))
            {
                if (!candidate.gameObject.activeInHierarchy)
                    continue;
                if (input != null)
                    throw new InvalidOperationException("Traversal requires exactly one active Player.");
                input = candidate;
            }
        }

        if (input == null)
            throw new InvalidOperationException("Traversal could not find the Player's RetroFPSController.");

        CharacterController controller = input.GetComponent<CharacterController>();
        if (controller == null)
            throw new InvalidOperationException("Traversal requires the Player's actual CharacterController.");
        if (input.walkSpeed <= 0f || input.gravity >= 0f)
            throw new InvalidOperationException("Traversal needs positive walking speed and negative gravity.");
        if (Vector3.Distance(controller.transform.lossyScale, Vector3.one) > 0.001f)
            throw new InvalidOperationException("Traversal requires an unscaled Player capsule.");

        Transform player = controller.transform;
        Vector3 savedPosition = player.position;
        Quaternion savedRotation = player.rotation;
        bool savedControllerEnabled = controller.enabled;
        bool savedInputEnabled = input.enabled;
        bool savedHasChanged = player.hasChanged;
        PhysicsScene physics = scene.GetPhysicsScene();
        var overlaps = new Collider[64];
        var stats = new TraversalStats();
        try
        {
            input.enabled = false;
            RunRoute("Circuit forward", Circuit, false, controller, input, physics, overlaps, stats);
            RunRoute("Circuit reverse", Circuit, true, controller, input, physics, overlaps, stats);
            RunRoute("Shortcut forward", Shortcut, false, controller, input, physics, overlaps, stats);
            RunRoute("Shortcut reverse", Shortcut, true, controller, input, physics, overlaps, stats);
        }
        finally
        {
            controller.enabled = false;
            player.SetPositionAndRotation(savedPosition, savedRotation);
            controller.enabled = savedControllerEnabled;
            input.enabled = savedInputEnabled;
            Physics.SyncTransforms();
            player.hasChanged = savedHasChanged;
        }

        return string.Format("Physical traversal passed: circuit and shortcut in both directions; {0} checkpoints, {1:F1} m walked, {2} CharacterController.Move steps at 120 Hz, feet {3:F2}..{4:F2} m. No jumps; Player state restored.",
            stats.checkpoints, stats.distance, stats.steps, stats.minimumHeight, stats.maximumHeight);
    }

    /// <summary>Checks visible mounting, guard construction, and usable capsule space across openings.</summary>
    public static string InspectConstruction(Scene scene)
    {
        RequireScene(scene);
        RetroFPSController input = FindPlayer(scene);
        CharacterController controller = RequireController(input);
        Transform facility = FindFacility(scene);
        PhysicsScene physics = scene.GetPhysicsScene();
        Physics.SyncTransforms();
        MeshFilter[] parts = facility.GetComponentsInChildren<MeshFilter>(false);
        Collider[] structural = StructuralColliders(facility);
        var failures = new List<string>();
        var overlaps = new Collider[64];
        int fixtures = 0;
        int posts = 0;
        int guards = 0;
        int plates = 0;
        int clearanceSamples = 0;

        foreach (MeshFilter part in parts)
        {
            string name = part.name;
            if (IsFixtureHousing(part))
            {
                fixtures++;
                if (!ConnectedToStructure(part, parts))
                    failures.Add("Floating fixture housing: " + Path(part.transform));

                Transform group = part.transform.parent;
                if (Contains(group.name, "Ceiling Fixture") || Contains(group.name, "Wall Fixture"))
                {
                    MeshFilter mount = null;
                    MeshFilter lens = null;
                    foreach (MeshFilter sibling in group.GetComponentsInChildren<MeshFilter>(false))
                    {
                        if (Contains(sibling.name, "Mount")) mount = sibling;
                        if (Contains(sibling.name, "Lens")) lens = sibling;
                        var renderer = sibling.GetComponent<Renderer>();
                        if (renderer != null && renderer.shadowCastingMode != ShadowCastingMode.Off)
                            failures.Add("Small fixture geometry casts a shadow: " + Path(sibling.transform));
                    }
                    if (mount == null || !Touching(MeshBounds(part), MeshBounds(mount)))
                        failures.Add("Fixture housing does not contact its mounting piece: " + Path(group));
                    if (lens == null || !Touching(MeshBounds(part), MeshBounds(lens)))
                        failures.Add("Fixture lens does not contact its housing: " + Path(group));
                }
            }

            if (Contains(name, "Guard") && Contains(name, "Post"))
            {
                posts++;
                Bounds bounds = MeshBounds(part);
                CheckFootContact(part, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), structural, parts, true, failures);
                CheckMinimumThickness(part, failures);
            }
            else if (IsBasePlate(part))
            {
                plates++;
                Bounds bounds = MeshBounds(part);
                CheckFootContact(part, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), structural, parts, false, failures);
            }
            else if (Contains(name, "Guard") && (Contains(name, "Handrail") || Contains(name, "Midrail")))
            {
                guards++;
                CheckMinimumThickness(part, failures);
                BoxCollider barrier = part.GetComponent<BoxCollider>();
                if (barrier == null || !barrier.enabled || barrier.isTrigger)
                    failures.Add("Guard rail has no solid collision barrier: " + Path(part.transform));
                else if (part.sharedMesh != null && (Vector3.Distance(barrier.size, part.sharedMesh.bounds.size) > 0.005f ||
                    Vector3.Distance(barrier.center, part.sharedMesh.bounds.center) > 0.005f))
                    failures.Add("Guard collision dimensions disagree with its visible rail: " + Path(part.transform));
            }
        }

        if (fixtures == 0) failures.Add("No authored fixture housings were found.");
        if (posts == 0 || guards == 0) failures.Add("No complete walkway guard rails were found.");
        foreach (Opening opening in Openings)
        {
            float span = opening.halfWidth - controller.radius - 0.12f;
            for (int across = -2; across <= 2; across++)
            {
                for (int along = -1; along <= 1; along++)
                {
                    Vector3 sample = opening.center + opening.Side * (span * across / 2f) + opening.direction * (0.75f * along);
                    CheckStandingSpace(opening.name, sample, controller, physics, structural, overlaps, failures);
                    clearanceSamples++;
                }
            }
        }
        foreach (Vector3 turningSpace in TurningSpaces)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    CheckStandingSpace("Turning/approach space", turningSpace + new Vector3(x * 0.75f, 0, z * 0.75f),
                        controller, physics, structural, overlaps, failures);
                    clearanceSamples++;
                }
            }
        }

        string metrics = string.Format("{0} fixture housings, {1} rail posts, {2} base plates, {3} solid rails; {4} full-width capsule/headroom samples.",
            fixtures, posts, plates, guards, clearanceSamples);
        if (failures.Count != 0)
            throw new InvalidOperationException("Construction inspection failed. " + metrics + "\n- " + string.Join("\n- ", failures));
        return "Construction inspection passed. " + metrics;
    }

    /// <summary>Runs near-edge and turning approaches using the actual capsule, walking and sprinting.</summary>
    public static string TraverseApproaches(Scene scene)
    {
        RequireScene(scene);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run approach traversal in Edit Mode.");
        RetroFPSController input = FindPlayer(scene);
        CharacterController controller = RequireController(input);
        Transform player = controller.transform;
        Vector3 savedPosition = player.position;
        Quaternion savedRotation = player.rotation;
        bool savedControllerEnabled = controller.enabled;
        bool savedInputEnabled = input.enabled;
        bool savedHasChanged = player.hasChanged;
        PhysicsScene physics = scene.GetPhysicsScene();
        Collider[] structural = StructuralColliders(FindFacility(scene));
        var overlaps = new Collider[64];
        var stats = new TraversalStats();
        int routes = 0;
        try
        {
            input.enabled = false;
            foreach (float speed in new[] { 6f, 10f })
            {
                foreach (float offset in new[] { 0f, -0.45f, 0.45f })
                {
                    foreach (Vector3[] original in new[] { Circuit, Shortcut, RampForecourt })
                    {
                        Vector3[] path = OffsetRoute(original, offset);
                        string label = string.Format("Approach {0:F0} m/s, offset {1:F2}", speed, offset);
                        RunRoute(label + " forward", path, false, controller, input, physics, overlaps, stats, speed);
                        RunRoute(label + " reverse", path, true, controller, input, physics, overlaps, stats, speed);
                        routes += 2;
                    }
                }

                foreach (Opening opening in Openings)
                {
                    float span = opening.halfWidth - controller.radius - 0.12f;
                    foreach (float offset in new[] { -span, 0f, span })
                    {
                        Vector3 center = opening.center + opening.Side * offset;
                        Vector3[] crossing = { center - opening.direction, center + opening.direction };
                        for (int i = 0; i < crossing.Length; i++)
                        {
                            if (!TryFloor(crossing[i], structural, out RaycastHit floor))
                                throw new InvalidOperationException("No supported approach floor at " + opening.name + " / " + crossing[i]);
                            crossing[i].y = floor.point.y;
                        }
                        RunRoute(opening.name + " edge crossing forward", crossing, false, controller, input, physics, overlaps, stats, speed);
                        RunRoute(opening.name + " edge crossing reverse", crossing, true, controller, input, physics, overlaps, stats, speed);
                        routes += 2;
                    }
                }
            }
        }
        finally
        {
            controller.enabled = false;
            player.SetPositionAndRotation(savedPosition, savedRotation);
            controller.enabled = savedControllerEnabled;
            input.enabled = savedInputEnabled;
            Physics.SyncTransforms();
            player.hasChanged = savedHasChanged;
        }

        return string.Format("Approach traversal passed: {0} routes at 6 and 10 m/s, both directions, center/±0.45 m circuit, shortcut and west ramp approach, plus full-width portal edges; {1} checkpoints, {2:F1} m, {3} Move steps. Player state restored.",
            routes, stats.checkpoints, stats.distance, stats.steps);
    }

    private static void RunRoute(string label, Vector3[] route, bool reverse, CharacterController controller,
        RetroFPSController input, PhysicsScene physics, Collider[] overlaps, TraversalStats stats, float speedOverride = 0f)
    {
        float speed = speedOverride > 0f ? speedOverride : input.walkSpeed;
        int index = reverse ? route.Length - 1 : 0;
        int increment = reverse ? -1 : 1;
        Vector3 first = route[index];
        Vector3 facing = route[index + increment] - first;
        facing.y = 0f;
        controller.enabled = false;
        controller.transform.SetPositionAndRotation(
            new Vector3(first.x, first.y - controller.center.y + controller.height * 0.5f + 0.05f, first.z),
            Quaternion.LookRotation(facing));
        controller.enabled = true;
        Physics.SyncTransforms();
        float verticalSpeed = 0f;
        Settle(controller, input.gravity, ref verticalSpeed, stats);
        CheckCheckpoint(label, first, controller, physics, overlaps, stats);

        for (index += increment; index >= 0 && index < route.Length; index += increment)
        {
            Vector3 target = route[index];
            float initialDistance = HorizontalDistance(controller.transform.position, target);
            int maximumSteps = Mathf.CeilToInt((initialDistance / speed + 4f) / StepTime);
            int stalledSteps = 0;
            float bestDistance = initialDistance;
            Vector3 heading = target - controller.transform.position;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0001f)
                controller.transform.rotation = Quaternion.LookRotation(heading);

            while (HorizontalDistance(controller.transform.position, target) > ArrivalRadius)
            {
                if (maximumSteps-- <= 0 || stalledSteps > 180)
                    throw new InvalidOperationException(string.Format("{0} blocked before ({1:F1}, {2:F1}); Player at {3}, feet {4:F2} m. Check doorway, stair, ramp, and collider clearance.",
                        label, target.x, target.z, controller.transform.position, FeetHeight(controller)));

                Vector3 delta = target - controller.transform.position;
                delta.y = 0f;
                Vector3 horizontal = delta.normalized * Mathf.Min(speed * StepTime, delta.magnitude);
                Move(controller, horizontal, input.gravity, ref verticalSpeed, stats);
                float distance = HorizontalDistance(controller.transform.position, target);
                if (distance < bestDistance - 0.01f)
                {
                    bestDistance = distance;
                    stalledSteps = 0;
                }
                else
                    stalledSteps++;

                if (FeetHeight(controller) < -0.5f)
                    throw new InvalidOperationException(label + " fell below the facility floor near " + controller.transform.position + ".");
                if ((stats.steps & 7) == 0)
                    CheckClearance(label, controller, physics, overlaps);
            }

            Settle(controller, input.gravity, ref verticalSpeed, stats);
            CheckCheckpoint(label, target, controller, physics, overlaps, stats);
        }
    }

    private static void Settle(CharacterController controller, float gravity, ref float verticalSpeed, TraversalStats stats)
    {
        for (int i = 0; i < 36; i++)
            Move(controller, Vector3.zero, gravity, ref verticalSpeed, stats);
    }

    private static void Move(CharacterController controller, Vector3 horizontal, float gravity, ref float verticalSpeed, TraversalStats stats)
    {
        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        verticalSpeed += gravity * StepTime;
        Vector3 before = controller.transform.position;
        CollisionFlags flags = controller.Move(horizontal + Vector3.up * (verticalSpeed * StepTime));
        if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            verticalSpeed = -2f;
        stats.distance += Vector3.Distance(before, controller.transform.position);
        stats.steps++;
        float height = FeetHeight(controller);
        stats.minimumHeight = Mathf.Min(stats.minimumHeight, height);
        stats.maximumHeight = Mathf.Max(stats.maximumHeight, height);
    }

    private static void CheckCheckpoint(string label, Vector3 expected, CharacterController controller,
        PhysicsScene physics, Collider[] overlaps, TraversalStats stats)
    {
        float feet = FeetHeight(controller);
        if (Mathf.Abs(feet - expected.y) > HeightTolerance || !controller.isGrounded)
            throw new InvalidOperationException(string.Format("{0} has no reliable walkable floor at ({1:F1}, {2:F1}): expected feet {3:F2} m, actual {4:F2} m, grounded={5}.",
                label, expected.x, expected.z, expected.y, feet, controller.isGrounded));
        CheckClearance(label, controller, physics, overlaps);
        stats.checkpoints++;
    }

    private static void CheckClearance(string label, CharacterController controller, PhysicsScene physics, Collider[] overlaps)
    {
        Physics.SyncTransforms();
        Vector3 center = controller.transform.TransformPoint(controller.center);
        float inset = Mathf.Max(0.015f, controller.skinWidth + 0.005f);
        float radius = Mathf.Max(0.01f, controller.radius - inset);
        float segment = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        int count = physics.OverlapCapsule(center - Vector3.up * segment, center + Vector3.up * segment,
            radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length)
            throw new InvalidOperationException("Capsule clearance query overflowed near " + controller.transform.position + ".");
        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];
            if (other == null || other == controller || other.transform.IsChildOf(controller.transform) || other.gameObject.scene != controller.gameObject.scene)
                continue;
            throw new InvalidOperationException(label + " has insufficient capsule clearance beside " + Path(other.transform) + " at " + controller.transform.position + ".");
        }

        float requiredHeadroom = Mathf.Max(2.2f, controller.height + 0.2f);
        Vector3 feet = new Vector3(center.x, FeetHeight(controller), center.z);
        if (physics.Raycast(feet + Vector3.up * 0.05f, Vector3.up, out RaycastHit ceiling,
            requiredHeadroom - 0.05f, Physics.AllLayers, QueryTriggerInteraction.Ignore) &&
            ceiling.collider != controller && !ceiling.collider.transform.IsChildOf(controller.transform))
            throw new InvalidOperationException(string.Format("{0} needs {1:F2} m of headroom at {2}; {3} leaves {4:F2} m.",
                label, requiredHeadroom, controller.transform.position, Path(ceiling.collider.transform), ceiling.distance + 0.05f));
    }

    private static float FeetHeight(CharacterController controller)
    {
        return controller.transform.TransformPoint(controller.center).y - controller.height * 0.5f;
    }

    private static float HorizontalDistance(Vector3 position, Vector3 target)
    {
        float x = position.x - target.x;
        float z = position.z - target.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static Vector3[] OffsetRoute(Vector3[] route, float offset)
    {
        var result = new Vector3[route.Length];
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 before = i > 0 ? route[i] - route[i - 1] : route[1] - route[0];
            Vector3 after = i + 1 < route.Length ? route[i + 1] - route[i] : before;
            before.y = 0f;
            after.y = 0f;
            before.Normalize();
            after.Normalize();
            Vector3 leftBefore = new Vector3(-before.z, 0f, before.x);
            Vector3 leftAfter = new Vector3(-after.z, 0f, after.x);
            float denominator = 1f + Vector3.Dot(leftBefore, leftAfter);
            if (denominator < 0.25f)
                throw new InvalidOperationException("Approach offsets do not support reversing direction at a waypoint.");
            result[i] = route[i] + (leftBefore + leftAfter) * (offset / denominator);
        }
        return result;
    }

    private static RetroFPSController FindPlayer(Scene scene)
    {
        RetroFPSController player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RetroFPSController candidate in root.GetComponentsInChildren<RetroFPSController>(false))
            {
                if (!candidate.gameObject.activeInHierarchy)
                    continue;
                if (player != null)
                    throw new InvalidOperationException("Physical inspection requires exactly one active Player.");
                player = candidate;
            }
        }
        if (player == null)
            throw new InvalidOperationException("Physical inspection could not find the Player.");
        return player;
    }

    private static CharacterController RequireController(RetroFPSController player)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null || controller.height <= controller.radius * 2f || player.gravity >= 0f)
            throw new InvalidOperationException("Physical inspection requires the actual Player capsule and downward gravity.");
        if (Vector3.Distance(controller.transform.lossyScale, Vector3.one) > 0.001f ||
            new Vector2(controller.center.x, controller.center.z).sqrMagnitude > 0.0001f)
            throw new InvalidOperationException("Physical inspection requires an unscaled Player with a horizontally centered capsule.");
        return controller;
    }

    private static Transform FindFacility(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.activeInHierarchy && root.GetComponentInChildren<FacilityAmbientEffects>(false) != null)
                return root.transform;
        }
        throw new InvalidOperationException("No active authored facility was found.");
    }

    private static Collider[] StructuralColliders(Transform facility)
    {
        var result = new List<Collider>();
        foreach (Collider collider in facility.GetComponentsInChildren<Collider>(false))
        {
            if (collider.enabled && !collider.isTrigger && IsArchitecture(collider.transform))
                result.Add(collider);
        }
        if (result.Count == 0)
            throw new InvalidOperationException("No authored Architecture colliders were found.");
        return result.ToArray();
    }

    private static bool IsArchitecture(Transform part)
    {
        for (Transform parent = part.parent; parent != null; parent = parent.parent)
        {
            if (parent.name == "Architecture")
                return true;
        }
        return false;
    }

    private static bool IsFixtureHousing(MeshFilter part)
    {
        return Contains(part.name, "Housing") &&
            (Contains(part.name, "Fixture") || Contains(part.name, "Fluorescent") ||
             (part.transform.parent != null && Contains(part.transform.parent.name, "Fixture")));
    }

    private static bool IsBasePlate(MeshFilter part)
    {
        return Contains(part.name, "Base Plate") || Contains(part.name, "Foot Plate");
    }

    private static bool Contains(string value, string text)
    {
        return value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Bounds MeshBounds(MeshFilter part)
    {
        if (part == null || part.sharedMesh == null)
            throw new InvalidOperationException("Construction inspection found a missing mesh.");
        Bounds local = part.sharedMesh.bounds;
        Bounds result = new Bounds(part.transform.TransformPoint(local.min), Vector3.zero);
        for (int corner = 1; corner < 8; corner++)
        {
            Vector3 vertex = new Vector3((corner & 1) == 0 ? local.min.x : local.max.x,
                (corner & 2) == 0 ? local.min.y : local.max.y,
                (corner & 4) == 0 ? local.min.z : local.max.z);
            result.Encapsulate(part.transform.TransformPoint(vertex));
        }
        return result;
    }

    private static bool Touching(Bounds a, Bounds b)
    {
        Vector3 overlap = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
        if (overlap.x < -0.03f || overlap.y < -0.03f || overlap.z < -0.03f)
            return false;
        int faceDimensions = (overlap.x > 0.01f ? 1 : 0) + (overlap.y > 0.01f ? 1 : 0) + (overlap.z > 0.01f ? 1 : 0);
        return faceDimensions >= 2;
    }

    private static bool ConnectedToStructure(MeshFilter housing, MeshFilter[] parts)
    {
        var visited = new HashSet<MeshFilter> { housing };
        var queue = new Queue<MeshFilter>();
        queue.Enqueue(housing);
        while (queue.Count != 0)
        {
            MeshFilter current = queue.Dequeue();
            Bounds bounds = MeshBounds(current);
            foreach (MeshFilter candidate in parts)
            {
                if (candidate.sharedMesh == null || visited.Contains(candidate) || !Touching(bounds, MeshBounds(candidate)))
                    continue;
                if (IsArchitecture(candidate.transform))
                    return true;
                string name = candidate.name;
                if (Contains(name, "Mount") || Contains(name, "Hanger") || Contains(name, "Support") ||
                    Contains(name, "Beam") || Contains(name, "Truss") || Contains(name, "Column"))
                {
                    visited.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
        }
        return false;
    }

    private static void CheckMinimumThickness(MeshFilter part, List<string> failures)
    {
        if (part.sharedMesh == null)
        {
            failures.Add("Missing guard mesh on " + Path(part.transform));
            return;
        }
        Vector3 size = Vector3.Scale(part.sharedMesh.bounds.size, part.transform.lossyScale);
        float minimum = Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        if (minimum < 0.079f)
            failures.Add(string.Format("Guard cross-section is {0:F3} m; minimum is 0.08 m: {1}", minimum, Path(part.transform)));
    }

    private static void CheckFootContact(MeshFilter part, Vector3 foot, Collider[] structural,
        MeshFilter[] parts, bool allowPlate, List<string> failures)
    {
        Ray ray = new Ray(foot + Vector3.up * 0.35f, Vector3.down);
        float best = float.NegativeInfinity;
        string supportName = "no surface";
        foreach (Collider collider in structural)
        {
            if (collider.Raycast(ray, out RaycastHit hit, 1.4f) && hit.normal.y > 0.5f && hit.point.y > best)
            {
                best = hit.point.y;
                supportName = Path(collider.transform);
            }
        }
        if (allowPlate)
        {
            foreach (MeshFilter plate in parts)
            {
                if (plate == part || !IsBasePlate(plate))
                    continue;
                Bounds bounds = MeshBounds(plate);
                if (bounds.IntersectRay(ray, out float distance) && distance >= 0f && distance <= 1.4f)
                {
                    float top = ray.GetPoint(distance).y;
                    if (top > best)
                    {
                        best = top;
                        supportName = Path(plate.transform);
                    }
                }
            }
        }
        if (float.IsNegativeInfinity(best) || Mathf.Abs(foot.y - best) > 0.03f)
            failures.Add(string.Format("Rail foot is unsupported or embedded: {0}; foot {1:F3} m, support {2:F3} m ({3}).",
                Path(part.transform), foot.y, best, supportName));
    }

    private static bool TryFloor(Vector3 nominal, Collider[] structural, out RaycastHit floor)
    {
        Ray ray = new Ray(nominal + Vector3.up, Vector3.down);
        floor = default;
        bool found = false;
        float closest = float.PositiveInfinity;
        foreach (Collider collider in structural)
        {
            if (collider.Raycast(ray, out RaycastHit candidate, 2f) && candidate.normal.y > 0.5f && candidate.distance < closest)
            {
                closest = candidate.distance;
                floor = candidate;
                found = true;
            }
        }
        return found;
    }

    private static void CheckStandingSpace(string label, Vector3 nominal, CharacterController controller,
        PhysicsScene physics, Collider[] structural, Collider[] overlaps, List<string> failures)
    {
        if (!TryFloor(nominal, structural, out RaycastHit floor))
        {
            failures.Add(label + " has no supported approach floor at " + nominal);
            return;
        }
        float slopeLift = controller.radius * (1f / Mathf.Max(0.5f, floor.normal.y) - 1f);
        Vector3 foot = floor.point + Vector3.up * slopeLift;
        float inset = Mathf.Max(0.015f, controller.skinWidth + 0.005f);
        float radius = Mathf.Max(0.01f, controller.radius - inset);
        int count = physics.OverlapCapsule(foot + Vector3.up * controller.radius,
            foot + Vector3.up * (controller.height - controller.radius), radius, overlaps,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length)
            failures.Add(label + " capsule query overflowed at " + nominal);
        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];
            if (other == null || other == controller || other.transform.IsChildOf(controller.transform) || other.gameObject.scene != controller.gameObject.scene)
                continue;
            // A standing sample just before a tread may touch the next riser. Actual Move routes
            // below prove that step-up; do not mistake a permitted architectural step for a wall.
            if (IsArchitecture(other.transform) && other.bounds.max.y <= foot.y + controller.stepOffset + 0.02f)
                continue;
            failures.Add(label + " approach capsule is blocked by " + Path(other.transform) + " at " + nominal);
            break;
        }
        float height = Mathf.Max(2.2f, controller.height + 0.2f);
        if (physics.Raycast(foot + Vector3.up * 0.05f, Vector3.up, out RaycastHit ceiling,
            height - 0.05f, Physics.AllLayers, QueryTriggerInteraction.Ignore) && ceiling.collider != controller &&
            !ceiling.collider.transform.IsChildOf(controller.transform))
            failures.Add(string.Format("{0} approach has only {1:F2} m headroom at {2}: {3}",
                label, ceiling.distance + 0.05f, nominal, Path(ceiling.collider.transform)));
    }

    private static void FindBrokenReferences(Component component, List<string> failures)
    {
        using (var serialized = new SerializedObject(component))
        {
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    failures.Add("Broken reference " + property.propertyPath + " on " + Path(component.transform));
            }
        }
    }

    private static void ValidateAmbience(FacilityAmbientEffects ambient, List<string> failures)
    {
        if (ambient.emergencyLights != null)
        {
            foreach (Light light in ambient.emergencyLights)
            {
                if (light == null || light.lightmapBakeType != LightmapBakeType.Realtime)
                    failures.Add("Every animated emergency light must reference a realtime light.");
            }
        }

        if (ambient.loopingSources == null || ambient.loopingSources.Length == 0)
            failures.Add("Ambient effects have no assigned looping AudioSources.");
        else
        {
            foreach (AudioSource source in ambient.loopingSources)
            {
                if (source == null || source.clip == null || !source.loop || !EditorUtility.IsPersistent(source.clip))
                    failures.Add("Looping ambience requires an assigned source and saved, looping clip.");
                if (source != null && source == ambient.intermittentSource)
                    failures.Add("Intermittent audio must use a separate source from looping ambience.");
            }
        }

        if (ambient.intermittentSource == null || ambient.intermittentSource.clip == null ||
            !EditorUtility.IsPersistent(ambient.intermittentSource.clip) || ambient.intermittentSource.loop)
            failures.Add("Intermittent ambience requires a separate saved, non-looping clip.");
    }

    private static void RequireScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new ArgumentException("Facility validation requires a valid, loaded Scene.", nameof(scene));
    }

    private static string Path(Transform transform)
    {
        var result = new StringBuilder(transform.name);
        while (transform.parent != null)
        {
            transform = transform.parent;
            result.Insert(0, transform.name + "/");
        }
        return result.ToString();
    }

    private sealed class TraversalStats
    {
        public int checkpoints;
        public int steps;
        public float distance;
        public float minimumHeight = float.PositiveInfinity;
        public float maximumHeight = float.NegativeInfinity;
    }

    private readonly struct Opening
    {
        public readonly string name;
        public readonly Vector3 center;
        public readonly Vector3 direction;
        public readonly float halfWidth;
        public Vector3 Side => new Vector3(-direction.z, 0, direction.x);

        public Opening(string name, Vector3 center, Vector3 direction, float halfWidth)
        {
            this.name = name;
            this.center = center;
            this.direction = direction;
            this.halfWidth = halfWidth;
        }
    }
}
