using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Explicit CLI only: -facilityCarTest outputDir. Exercises the real menu, trunk and equipment pickup.</summary>
public sealed class FacilityHellcatSmokeTest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "Actual title-to-story transition, saved Hellcat body collision and natural lighting, both side doors through reachable physical interactions, cabin courtesy-light OR logic, independent downlit trunk, closed-trunk pickup gate, open/close/reopen, simultaneous pistol and drill retrieval, no alarm, no required-fists instructions.";
        public List<string> checks = new List<string>();
        public List<string> failures = new List<string>();
        public Vector3 closedHingeEuler, openHingeEuler;
        public float parkingHoodLitLuminance, parkingHoodBlockedLuminance, parkingHoodRestoredLuminance, parkingShadowDropFraction;
        public string activePipeline;
        public float activePipelineShadowDistance;
        public bool gameplayCameraRendersShadows, activePipelineAdditionalShadows;
        public int pictures;
    }
    readonly Receipt receipt = new Receipt();
    readonly RaycastHit[] hits = new RaycastHit[128];
    readonly Collider[] overlaps = new Collider[96];
    readonly Dictionary<FacilityDoor, Bounds> closedDoorBounds = new Dictionary<FacilityDoor, Bounds>();
    FacilityGameSession session;
    FacilityStoryDirector director;
    RetroFPSController player;
    CharacterController motor;
    Camera eyes;
    string output;
    bool collecting;
    IDisposable account;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-facilityCarTest");
        if (at < 0) return;
        if (at + 1 >= args.Length) { Application.Quit(2); return; }
        if (FindFirstObjectByType<FacilityHellcatSmokeTest>() != null) return;
        var runner = new GameObject("Hellcat interaction checks - CLI only").AddComponent<FacilityHellcatSmokeTest>();
        runner.output = args[at + 1]; DontDestroyOnLoad(runner.gameObject);
    }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        Directory.CreateDirectory(output); Application.runInBackground = true;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        account = FacilityEconomy.UseIsolatedAccount(); collecting = true; Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object current = null;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception failure) { receipt.failures.Add(failure.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        collecting = false; Application.logMessageReceived -= OnLog;
        account?.Dispose(); Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "hellcat-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.7f);
        session = FacilityGameSession.Instance;
        Require(session != null && session.CurrentState == FacilityGameState.MainMenu && session.startButton != null,
            "The real saved title menu loads.");
        session.startButton.onClick.Invoke(); yield return null;
        Require(session.CurrentState == FacilityGameState.ModeSelect && session.storyButton != null, "Start opens the Story/Sandbox mode menu.");
        FacilityGameSession previous = session;
        session.storyButton.onClick.Invoke();
        float until = Time.realtimeSinceStartup + 15f;
        while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance == previous) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(.35f);
        session = FacilityGameSession.Instance;
        Require(session != null && session != previous && session.CurrentMode == FacilityGameMode.Story && session.CurrentState == FacilityGameState.Playing,
            "Story loads through its actual menu button.");
        director = session.storyDirector; player = session.player; motor = player.GetComponent<CharacterController>(); eyes = player.GetComponentInChildren<Camera>();
        Require(director != null && director.HasBegun && director.sedanExit != null && director.drillPickup != null && motor != null && eyes != null,
            "The preserved mission, player and sedan interactions are assigned.");
        player.enabled = false;
        foreach (Text label in session.canvas.GetComponentsInChildren<Text>(true))
        {
            string text = label.text.ToLowerInvariant();
            Require(!(text.Contains("fist") && (text.Contains("must") || text.Contains("use fists") || text.Contains("punch the receptionist"))),
                "Player-facing text does not require fists: " + label.name);
        }
        Transform car = director.sedanExit.transform.parent;
        FacilityStoryInteractable equipment = director.drillPickup;
        FacilityDoor trunk = equipment.door;
        Require(car != null && trunk != null && trunk.interaction != null && trunk.leaf != null && !trunk.IsOpen && !trunk.IsLocked,
            "The Hellcat starts with its preserved closed, unlocked trunk.");
        bool collision = false;
        foreach (Collider collider in car.GetComponentsInChildren<Collider>(true))
            if (collider.enabled && !collider.isTrigger && !collider.transform.IsChildOf(trunk.leaf)) collision = true;
        Require(collision, "The car retains physical body collision.");
        FacilityVehicleCourtesyLights courtesy = car.GetComponentInChildren<FacilityVehicleCourtesyLights>(true);
        Require(courtesy != null && courtesy.driverDoor != null && courtesy.passengerDoor != null && courtesy.driverDoor != courtesy.passengerDoor && courtesy.trunkDoor == trunk,
            "The saved car assigns two separate working side doors and the preserved trunk to its courtesy-light controller.");
        foreach (FacilityDoor door in new[] { courtesy.driverDoor, courtesy.passengerDoor, trunk })
        {
            Require(door.leaf != null && door.interaction != null && door.interaction.door == door && !door.npcCanOpen && !door.IsLocked && door.OpenFraction < .001f,
                "The car door starts closed, has its own interaction and cannot be opened automatically by NPCs: " + door.name);
            bool found = false; Bounds bounds = default;
            foreach (Collider collider in door.leafColliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled) continue;
                if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds);
            }
            Require(found, "The moving car door has real physical leaf collision: " + door.name);
            closedDoorBounds[door] = bounds;
        }
        courtesy.RefreshLights();
        VerifyNaturalLighting(car, courtesy);
        Require(!courtesy.CabinLightOn && !courtesy.TrunkLightOn && AllOff(courtesy.cabinLights) && !courtesy.trunkLight.enabled,
            "With all doors closed, the cabin and trunk courtesy lamps are physically off.");
        VerifyLampLenses(courtesy, false, false, "all closed");
        yield return SideDoors(car, courtesy);
        Require(!director.HasDrill && !session.inventory.HasPistol && !director.IsInteractionAvailable(equipment, out _),
            "The closed trunk blocks equipment collection and keeps the pistol inside the car.");
        receipt.closedHingeEuler = trunk.leaf.localEulerAngles;
        yield return Interact(trunk.interaction);
        yield return new WaitForSeconds(trunk.travelSeconds + .2f);
        Require(trunk.IsOpen && !trunk.IsMoving, "The physical trunk opens fully through TryInteract.");
        Require(courtesy.TrunkLightOn && courtesy.trunkLight.enabled && !courtesy.CabinLightOn && AllOff(courtesy.cabinLights),
            "Opening only the trunk activates its real lamp while the closed cabin remains dark.");
        VerifyLampLenses(courtesy, false, true, "trunk open");
        Vector3 equipmentDirection = equipment.transform.position - courtesy.trunkLight.transform.position;
        Require(Vector3.Dot(courtesy.trunkLight.transform.forward, -car.up) > .5f &&
            Vector3.Dot(courtesy.trunkLight.transform.forward, equipmentDirection.normalized) > .6f &&
            courtesy.trunkLight.range > equipmentDirection.magnitude,
            "The trunk's actual light points downward onto the equipment within its physical range.");
        receipt.openHingeEuler = trunk.leaf.localEulerAngles;
        Require(Quaternion.Angle(Quaternion.Euler(receipt.closedHingeEuler), trunk.leaf.localRotation) > 20f,
            "The visible trunk lid actually rotates through its opening arc.");
        yield return Capture("01_trunk_open");
        yield return CaptureCar("08_trunk_courtesy_open", car, new Vector3(1.9f, 1.65f, -3.7f), new Vector3(0f, .85f, -1.85f), 48f);
        yield return Interact(trunk.interaction);
        yield return new WaitForSeconds(trunk.travelSeconds + .2f);
        Require(!trunk.IsOpen && !trunk.IsMoving && !trunk.WantsOpen && !director.IsInteractionAvailable(equipment, out _),
            "The trunk closes correctly and again prevents pickup through its closed lid.");
        Require(Quaternion.Angle(Quaternion.Euler(receipt.closedHingeEuler), trunk.leaf.localRotation) < .2f,
            "The trunk returns exactly to its authored closed pose.");
        Require(!courtesy.TrunkLightOn && !courtesy.trunkLight.enabled && !courtesy.CabinLightOn,
            "Closing the trunk switches its courtesy lamp off without lighting the cabin.");
        VerifyLampLenses(courtesy, false, false, "trunk closed");
        yield return Capture("02_trunk_closed");
        yield return CaptureCar("09_trunk_courtesy_closed", car, new Vector3(1.9f, 1.65f, -3.7f), new Vector3(0f, .85f, -1.85f), 48f);
        yield return Interact(trunk.interaction);
        yield return new WaitForSeconds(trunk.travelSeconds + .2f);
        Require(trunk.IsOpen, "The trunk can be reopened after closing.");
        yield return Interact(equipment);
        yield return null;
        Require(director.HasDrill && session.inventory.HasPistol && session.inventory.SelectedWeapon == FacilityWeaponKind.Pistol && equipment.Consumed,
            "A reachable open-trunk pickup retrieves and equips the pistol together with the drill.");
        Require(equipment.visualRoot == null || !equipment.visualRoot.activeInHierarchy, "Collected equipment disappears from the trunk.");
        Require(!director.AlarmActive && director.receptionist.HitsTaken == 0,
            "Trunk operation and equipment retrieval do not trigger the receptionist alarm.");
        yield return Capture("03_equipment_retrieved");
        yield return Interact(trunk.interaction);
        yield return new WaitForSeconds(trunk.travelSeconds + .2f);
        Require(!trunk.WantsOpen && !trunk.IsMoving && !trunk.IsOpen, "The trunk still closes correctly after the pickup is consumed.");
        Require(!courtesy.TrunkLightOn && !courtesy.CabinLightOn && AllOff(courtesy.cabinLights) && !courtesy.trunkLight.enabled,
            "All courtesy lamps return off after equipment collection and closing the car.");
        Require(!director.IsInteractionAvailable(director.sedanExit, out _), "The new car still requires both vault loot piles before extraction.");
        Require(FacilityEconomy.Cash == 0, "Car inspection cannot award or spend persistent cash.");
        yield return VerifyParkingShadows(car);
    }
    IEnumerator SideDoors(Transform car, FacilityVehicleCourtesyLights courtesy)
    {
        FacilityDoor driver = courtesy.driverDoor, passenger = courtesy.passengerDoor;
        Quaternion driverClosed = driver.leaf.localRotation, passengerClosed = passenger.leaf.localRotation;
        float driverSide = Mathf.Sign(car.InverseTransformPoint(driver.transform.position).x);
        if (driverSide == 0f) driverSide = -1f;
        Vector3 photographEye = new Vector3(driverSide * 3.2f, 1.7f, -.5f), photographAim = new Vector3(0f, .9f, -.12f);
        yield return CaptureCar("04_cabin_all_closed", car, photographEye, photographAim, 46f);
        yield return Interact(driver.interaction);
        yield return new WaitForSeconds(driver.travelSeconds + .2f);
        Require(driver.IsOpen && driver.OpenFraction > .98f && Quaternion.Angle(driverClosed, driver.leaf.localRotation) > 35f,
            "The driver's complete physical door swings open through its reachable interaction.");
        Require(courtesy.CabinLightOn && AllOn(courtesy.cabinLights) && !courtesy.TrunkLightOn,
            "Opening the driver door activates the cabin's real courtesy lights and leaves the trunk light off.");
        VerifyLampLenses(courtesy, true, false, "driver open");
        yield return CaptureCar("05_cabin_driver_open", car, photographEye, photographAim, 46f);
        yield return Interact(passenger.interaction);
        yield return new WaitForSeconds(passenger.travelSeconds + .2f);
        Require(passenger.IsOpen && passenger.OpenFraction > .98f && Quaternion.Angle(passengerClosed, passenger.leaf.localRotation) > 35f,
            "The passenger's complete physical door also swings open through its reachable interaction.");
        Require(courtesy.CabinLightOn && AllOn(courtesy.cabinLights) && !courtesy.TrunkLightOn,
            "Both open side doors keep cabin courtesy lights on while the closed trunk stays dark.");
        yield return CaptureCar("06_cabin_both_open", car, photographEye, photographAim, 46f);
        yield return Interact(driver.interaction);
        yield return new WaitForSeconds(driver.travelSeconds + .2f);
        Require(!driver.IsOpen && !driver.IsMoving && !driver.WantsOpen && passenger.IsOpen &&
            Quaternion.Angle(driverClosed, driver.leaf.localRotation) < .2f,
            "The driver door closes back to its exact saved pose while the passenger door remains open.");
        Require(courtesy.CabinLightOn && AllOn(courtesy.cabinLights) && !courtesy.TrunkLightOn,
            "Closing one side door cannot turn off cabin lights while the other door is still open.");
        VerifyLampLenses(courtesy, true, false, "passenger still open");
        yield return Interact(passenger.interaction);
        yield return new WaitForSeconds(passenger.travelSeconds + .2f);
        Require(!passenger.IsOpen && !passenger.IsMoving && !passenger.WantsOpen &&
            Quaternion.Angle(passengerClosed, passenger.leaf.localRotation) < .2f,
            "The passenger door closes back to its exact saved pose.");
        Require(!courtesy.CabinLightOn && AllOff(courtesy.cabinLights) && !courtesy.TrunkLightOn,
            "Closing both side doors switches the cabin courtesy lights off.");
        VerifyLampLenses(courtesy, false, false, "both side doors closed again");
        yield return CaptureCar("07_cabin_closed_again", car, photographEye, photographAim, 46f);
    }
    void VerifyNaturalLighting(Transform car, FacilityVehicleCourtesyLights courtesy)
    {
        Require(courtesy.cabinLights != null && courtesy.cabinLights.Length > 0 && courtesy.trunkLight != null &&
            courtesy.cabinLampLenses != null && courtesy.cabinLampLenses.Length > 0 && courtesy.trunkLampLenses != null && courtesy.trunkLampLenses.Length > 0,
            "Courtesy lighting has assigned physical cabin/trunk lights and visible lamp lenses.");
        var permittedLights = new HashSet<Light>(courtesy.cabinLights) { courtesy.trunkLight };
        Light[] carLights = car.GetComponentsInChildren<Light>(true);
        int enabledLights = 0;
        foreach (Light light in carLights)
        {
            if (light.enabled && light.gameObject.activeInHierarchy) enabledLights++;
            string name = light.name.ToLowerInvariant();
            Require(permittedLights.Contains(light) || name.Contains("beam") || name.Contains("headlight") || name.Contains("headlamp"),
                "The car has no fixed coachwork fill light: " + light.name);
            Require(light.bakingOutput.lightmapBakeType == LightmapBakeType.Realtime && light.shadows != LightShadows.None,
                "Each real car light supports moving geometry and scene shadows: " + light.name);
        }
        Require(carLights.Length == 4 && enabledLights == 2, "The closed car has exactly four real lamps, with only its two headlights enabled.");
        foreach (ReflectionProbe probe in car.GetComponentsInChildren<ReflectionProbe>(true))
            Require(probe.mode != ReflectionProbeMode.Custom || probe.customBakedTexture == null,
                "The car does not use an artificial local custom reflection cubemap: " + probe.name);
        int bodyRenderers = 0;
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null || !IsBodyMaterial(material)) continue;
            bodyRenderers++;
            Require(!material.IsKeywordEnabled("_EMISSION") && (!material.HasProperty("_EmissionColor") || material.GetColor("_EmissionColor").maxColorComponent < .001f),
                "Bodywork reflects actual scene light without material emission: " + renderer.name);
            Require(renderer.receiveShadows && renderer.shadowCastingMode != ShadowCastingMode.Off,
                "Bodywork receives and casts real shadows: " + renderer.name);
        }
        Require(bodyRenderers > 0, "The natural-lighting checks inspected the saved silver bodywork materials.");
        bool parkingShadowSource = false;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            bool actualParkingFixture = false;
            for (Transform parent = light.transform; parent != null; parent = parent.parent)
                if (parent.name.IndexOf("Parking sodium lamp", StringComparison.OrdinalIgnoreCase) >= 0) actualParkingFixture = true;
            if (actualParkingFixture && !light.transform.IsChildOf(car) && light.enabled && light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                light.shadows != LightShadows.None && (light.cullingMask & (1 << 29)) != 0) parkingShadowSource = true;
        }
        Require(parkingShadowSource, "The actual exterior parking fixture supplies mixed scene lighting and real moving shadows to the car.");
    }
    static bool IsBodyMaterial(Material material)
    {
        string name = material.name.ToLowerInvariant();
        return name.Contains("billet silver") || name.Contains("silver with twin") || name.Contains("detail silver metallic");
    }
    static bool AllOn(Light[] lights)
    { if (lights == null || lights.Length == 0) return false; foreach (Light light in lights) if (light == null || !light.enabled || !light.gameObject.activeInHierarchy || light.intensity <= 0f) return false; return true; }
    static bool AllOff(Light[] lights)
    { if (lights == null || lights.Length == 0) return false; foreach (Light light in lights) if (light == null || (light.enabled && light.gameObject.activeInHierarchy)) return false; return true; }
    void VerifyLampLenses(FacilityVehicleCourtesyLights courtesy, bool cabinOn, bool trunkOn, string state)
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer lens in courtesy.cabinLampLenses)
        {
            lens.GetPropertyBlock(block);
            Require(lens.enabled && lens.gameObject.activeInHierarchy && (block.GetColor("_EmissionColor").maxColorComponent > .01f) == cabinOn,
                "The cabin lamp lens follows actual door state: " + state); block.Clear();
        }
        foreach (Renderer lens in courtesy.trunkLampLenses)
        {
            lens.GetPropertyBlock(block);
            Require(lens.enabled && lens.gameObject.activeInHierarchy && (block.GetColor("_EmissionColor").maxColorComponent > .01f) == trunkOn,
                "The trunk lamp lens follows its independent door state: " + state); block.Clear();
        }
    }
    IEnumerator VerifyParkingShadows(Transform car)
    {
        UniversalAdditionalCameraData gameplayCameraData = eyes.GetUniversalAdditionalCameraData();
        receipt.gameplayCameraRendersShadows = gameplayCameraData.renderShadows;
        Require(receipt.gameplayCameraRendersShadows, "The actual saved gameplay camera renders real scene shadows.");
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        receipt.activePipeline = pipeline != null ? pipeline.name : "No active URP asset";
        receipt.activePipelineAdditionalShadows = pipeline != null && pipeline.supportsAdditionalLightShadows;
        receipt.activePipelineShadowDistance = pipeline != null ? pipeline.shadowDistance : 0f;
        Require(receipt.activePipelineAdditionalShadows && receipt.activePipelineShadowDistance > 0f,
            "The actually active URP asset enables additional-light shadows at a nonzero distance: " + receipt.activePipeline);
        Light parking = null;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        for (Transform ancestor = light.transform; ancestor != null; ancestor = ancestor.parent)
            if (ancestor.name.IndexOf("Parking sodium lamp", StringComparison.OrdinalIgnoreCase) >= 0) parking = light;
        MeshFilter hood = null;
        foreach (MeshFilter filter in car.GetComponentsInChildren<MeshFilter>(true))
            if (filter.name.IndexOf("aluminum hood", StringComparison.OrdinalIgnoreCase) >= 0) hood = filter;
        Require(parking != null && parking.enabled && hood != null && hood.sharedMesh != null && hood.sharedMesh.isReadable,
            "Shadow proof uses the enabled actual parking fixture and vertex positions from the real hood mesh.");
        var view = new GameObject("Temporary hood shadow comparison camera");
        Camera camera = view.AddComponent<Camera>(); camera.CopyFrom(eyes); camera.enabled = false;
        camera.fieldOfView = 35f; camera.aspect = 1.6f; camera.nearClipPlane = .05f;
        Vector3 eye = car.TransformPoint(new Vector3(2.6f, 3.55f, 3.8f)), aim = car.TransformPoint(new Vector3(0f, 1.065f, 1.65f));
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false; cameraData.renderShadows = gameplayCameraData.renderShadows;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
        Transform[] weapons = { session.inventory.fistsModel, session.inventory.pistolModel, session.inventory.shotgunModel, session.inventory.ak47Model };
        var shown = new bool[weapons.Length];
        Material blockerMaterial = null; GameObject blocker = null;
        Texture2D lit = null, blocked = null, restored = null;
        RenderTexture active = RenderTexture.active;
        try
        {
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) { shown[i] = weapons[i].gameObject.activeSelf; weapons[i].gameObject.SetActive(false); }
            yield return new WaitForEndOfFrame();
            lit = RenderPixels(camera, target, "10_real_parking_hood_lit");
            Vector3 hoodCenter = hood.GetComponent<Renderer>().bounds.center;
            Vector3 toParking = parking.transform.position - hoodCenter;
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Temporary opaque parking-light shadow proof blocker";
            blocker.transform.SetPositionAndRotation(hoodCenter + toParking * .34f, Quaternion.FromToRotation(Vector3.up, toParking.normalized));
            blocker.transform.localScale = new Vector3(3.7f, .12f, 3.7f);
            blockerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); blockerMaterial.SetColor("_BaseColor", Color.black);
            Renderer blockerRenderer = blocker.GetComponent<Renderer>(); blockerRenderer.sharedMaterial = blockerMaterial;
            // A genuine opaque mesh casts the parking light's shadow without covering the measurement pixels.
            blockerRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; blockerRenderer.receiveShadows = false;
            Physics.SyncTransforms(); yield return new WaitForEndOfFrame();
            blocked = RenderPixels(camera, target, "11_real_parking_hood_shadowed");
            blockerRenderer.enabled = false;
            Physics.SyncTransforms(); yield return new WaitForEndOfFrame();
            restored = RenderPixels(camera, target, "12_real_parking_hood_restored");
            double litSum = 0, blockedSum = 0, restoredSum = 0; int samples = 0;
            Vector3[] vertices = hood.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i += 13)
            {
                Vector3 screen = camera.WorldToScreenPoint(hood.transform.TransformPoint(vertices[i]));
                // The explicit request has the fixed dimensions above; viewport projection is independent of the hidden window size.
                Vector3 viewport = camera.WorldToViewportPoint(hood.transform.TransformPoint(vertices[i]));
                int x = Mathf.RoundToInt(viewport.x * 1599f), y = Mathf.RoundToInt(viewport.y * 999f);
                if (screen.z <= 0f || x < 3 || y < 3 || x >= 1597 || y >= 997) continue;
                float baseline = Luminance(lit.GetPixel(x, y));
                if (baseline < .012f) continue;
                litSum += baseline; blockedSum += Luminance(blocked.GetPixel(x, y)); restoredSum += Luminance(restored.GetPixel(x, y)); samples++;
            }
            Require(samples > 40, "The shadow comparison samples a visible patch of actual hood geometry without headlight pixels.");
            receipt.parkingHoodLitLuminance = (float)(litSum / samples);
            receipt.parkingHoodBlockedLuminance = (float)(blockedSum / samples);
            receipt.parkingHoodRestoredLuminance = (float)(restoredSum / samples);
            receipt.parkingShadowDropFraction = 1f - receipt.parkingHoodBlockedLuminance / Mathf.Max(.0001f, receipt.parkingHoodLitLuminance);
        }
        finally
        {
            if (blocker != null) Destroy(blocker);
            if (blockerMaterial != null) Destroy(blockerMaterial);
            if (lit != null) Destroy(lit); if (blocked != null) Destroy(blocked); if (restored != null) Destroy(restored);
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) weapons[i].gameObject.SetActive(shown[i]);
            RenderTexture.active = active; target.Release(); Destroy(target); Destroy(view);
        }
        Require(parking.enabled && receipt.parkingShadowDropFraction > .08f,
            "A physical opaque blocker reduces measured hood brightness by at least 8% while the actual parking lamp stays on (measured " + (receipt.parkingShadowDropFraction * 100f).ToString("F1") + "%).");
        Require(Mathf.Abs(receipt.parkingHoodRestoredLuminance - receipt.parkingHoodLitLuminance) < Mathf.Max(.012f, receipt.parkingHoodLitLuminance * .08f),
            "Removing the shadow blocker restores hood illumination in the same camera view.");
    }
    Texture2D RenderPixels(Camera camera, RenderTexture target, string name)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG()); receipt.pictures++;
        return pixels;
    }
    static float Luminance(Color color) => color.r * .2126f + color.g * .7152f + color.b * .0722f;
    IEnumerator Interact(FacilityStoryInteractable target)
    {
        Vector3 aim = target.transform.position;
        Pose(FindPose(target, aim), aim); yield return null;
        Require(director.TryInteract(target, out string reason), "Reachable " + target.kind + " interaction: " + reason);
    }
    Vector3 FindPose(FacilityStoryInteractable target, Vector3 aim)
    {
        for (int r = 0; r < 7; r++) for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI / 32f, radius = .9f + r * .2f;
            Vector3 candidate = aim + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius); candidate.y = .05f;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .45f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            FacilityDoor closingDoor = target.kind == FacilityStoryInteractionKind.Door ? target.door : null;
            if (closingDoor != null && closingDoor.WantsOpen && closedDoorBounds.TryGetValue(closingDoor, out Bounds sweep))
            {
                foreach (Collider collider in closingDoor.leafColliders)
                    if (collider != null && collider.enabled && !collider.isTrigger) sweep.Encapsulate(collider.bounds);
                sweep.Expand(.24f);
                if (sweep.Intersects(new Bounds(candidate + Vector3.up * .95f, new Vector3(.74f, 1.9f, .74f)))) continue;
            }
            int count = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * .36f, candidate + Vector3.up * 1.5f, .31f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool clear = count < overlaps.Length;
            for (int c = 0; c < count && clear; c++) if (overlaps[c] != null && !overlaps[c].transform.IsChildOf(player.transform)) clear = false;
            if (!clear) continue;
            Vector3 origin = candidate + Vector3.up * 1.65f, offset = aim - origin;
            if (offset.magnitude > director.interactionRange - .04f) continue;
            count = Physics.RaycastNonAlloc(origin, offset.normalized, hits, offset.magnitude + .1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == hits.Length) continue;
            Collider nearest = null; float distance = float.PositiveInfinity;
            for (int h = 0; h < count; h++)
            {
                Collider collider = hits[h].collider;
                if (collider == null || collider is CharacterController || collider.transform.IsChildOf(player.transform)) continue;
                if (collider.isTrigger && collider.GetComponentInParent<FacilityStoryInteractable>() == null) continue;
                if (hits[h].distance < distance) { nearest = collider; distance = hits[h].distance; }
            }
            FacilityStoryInteractable resolved = nearest != null ? nearest.GetComponentInParent<FacilityStoryInteractable>() : null;
            if (resolved == null && nearest != null) resolved = nearest.GetComponentInParent<FacilityDoor>()?.interaction;
            if (resolved == target) return candidate;
        }
        throw new InvalidOperationException("No physically clear, supported line of sight to " + target.name + " at " + aim + ".");
    }
    void Pose(Vector3 feet, Vector3 aim)
    {
        motor.enabled = false; player.transform.position = feet; motor.enabled = true;
        player.ResetRecoil(); Vector3 facing = aim - eyes.transform.position, flat = facing; flat.y = 0f;
        if (flat.sqrMagnitude > .001f) player.transform.rotation = Quaternion.LookRotation(flat);
        eyes.transform.rotation = Quaternion.LookRotation(facing); Physics.SyncTransforms();
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Canvas canvas = session.canvas; RenderMode originalMode = canvas.renderMode; Camera originalCamera = canvas.worldCamera; float plane = canvas.planeDistance;
        RenderTexture active = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32); target.Create();
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = eyes; canvas.planeDistance = .15f; Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(eyes, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG()); receipt.pictures++;
        }
        finally
        {
            canvas.renderMode = originalMode; canvas.worldCamera = originalCamera; canvas.planeDistance = plane;
            RenderTexture.active = active; Destroy(pixels); target.Release(); Destroy(target);
        }
    }
    IEnumerator CaptureCar(string name, Transform car, Vector3 localEye, Vector3 localAim, float fieldOfView)
    {
        yield return new WaitForEndOfFrame();
        Transform[] weapons = { session.inventory.fistsModel, session.inventory.pistolModel, session.inventory.shotgunModel, session.inventory.ak47Model };
        var shown = new bool[weapons.Length];
        var cameraObject = new GameObject("Temporary actual-night courtesy-light camera");
        Camera camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(eyes); camera.enabled = false;
        camera.fieldOfView = fieldOfView; camera.aspect = 1.6f; camera.nearClipPlane = .04f;
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData(), gameplayCameraData = eyes.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = gameplayCameraData.renderPostProcessing; cameraData.renderShadows = gameplayCameraData.renderShadows;
        camera.transform.SetPositionAndRotation(car.TransformPoint(localEye), Quaternion.LookRotation(car.TransformPoint(localAim) - car.TransformPoint(localEye)));
        RenderTexture active = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) { shown[i] = weapons[i].gameObject.activeSelf; weapons[i].gameObject.SetActive(false); }
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG()); receipt.pictures++;
        }
        finally
        {
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) weapons[i].gameObject.SetActive(shown[i]);
            RenderTexture.active = active; Destroy(pixels); target.Release(); Destroy(target); Destroy(cameraObject);
        }
    }
    void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); receipt.checks.Add(description); }
    void OnLog(string message, string trace, LogType kind)
    { if (collecting && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace); }
}
