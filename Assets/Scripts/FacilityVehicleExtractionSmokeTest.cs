using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Explicit CLI only: -facilitySeatTest outputDir. Real loot-to-driver-seat extraction, using an isolated account.</summary>
public sealed class FacilityVehicleExtractionSmokeTest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "Real menu, trunk pickup, pistol alarm and forty-five-second vault drill at 4x simulation speed; no-loot/cash-only/closed-driver/passenger-only gates; physical CharacterController return from vault to outside driver door and visible seat; boarding control lock, pause, exact seated camera, one payout and normal restart. AI is held for this focused extraction regression. Disposable wallet.";
        public List<string> checks = new List<string>(), failures = new List<string>();
        public float walkedMeters;
        public int movementSteps, pictures;
        public Vector3 outsideDriverPose, outsideSeatPose, finalSeatRoot, finalSeatCamera;
    }
    readonly Receipt receipt = new Receipt();
    readonly RaycastHit[] hits = new RaycastHit[128];
    readonly Collider[] overlaps = new Collider[96];
    FacilityGameSession session;
    FacilityStoryDirector director;
    FacilityVehicleExtraction extraction;
    FacilityVehicleCourtesyLights courtesy;
    RetroFPSController player;
    CharacterController motor;
    Camera eyes;
    Transform car;
    string output;
    bool collecting;
    IDisposable account;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-facilitySeatTest");
        if (at < 0) return;
        if (at + 1 >= args.Length) { Application.Quit(2); return; }
        var runner = new GameObject("Driver seat extraction regression - CLI only").AddComponent<FacilityVehicleExtractionSmokeTest>();
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
            catch (Exception error) { receipt.failures.Add(error.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        collecting = false; Application.logMessageReceived -= OnLog;
        account?.Dispose(); Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "seat-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.7f);
        session = FacilityGameSession.Instance;
        Require(session != null && session.CurrentState == FacilityGameState.MainMenu, "The saved game starts at its actual title menu.");
        session.startButton.onClick.Invoke(); yield return null;
        Require(session.CurrentState == FacilityGameState.ModeSelect, "Start opens the real mode selector.");
        FacilityGameSession previous = session; session.storyButton.onClick.Invoke(); yield return WaitForSession(previous);
        Require(extraction != null && extraction == director.vehicleExtraction && extraction.driverDoor == courtesy.driverDoor &&
            extraction.seatPose != null && extraction.eyePose != null && extraction.boardingSeconds >= .5f,
            "Saved driver-seat extraction references the physical driver door and authored seated poses.");
        Require(!extraction.IsBoarding && !extraction.IsSeated && !session.VehicleInputLocked &&
            !director.IsInteractionAvailable(director.sedanExit, out _) && !director.TryInteract(director.sedanExit, out _),
            "A new run cannot enter the extraction seat without the required loot.");
        CheckCarWords();
        yield return Interact(courtesy.passengerDoor.interaction);
        yield return new WaitForSeconds(courtesy.passengerDoor.travelSeconds + .15f);
        Require(courtesy.passengerDoor.IsOpen && !courtesy.driverDoor.IsOpen && !director.IsInteractionAvailable(director.sedanExit, out _),
            "Opening only the passenger door does not permit boarding on a new run.");
        yield return Interact(director.drillPickup.door.interaction);
        yield return new WaitForSeconds(director.drillPickup.door.travelSeconds + .15f);
        yield return Interact(director.drillPickup);
        Require(director.HasDrill && session.inventory.HasPistol && !director.AlarmActive, "The real open-trunk pickup grants drill and pistol without an alarm.");
        foreach (FacilityKnifeGuard guard in director.guards) { guard.enabled = false; guard.actor.enabled = false; }
        Pose(FindActorPose(director.receptionist), director.receptionist.AimPoint);
        yield return new WaitForSecondsRealtime(.25f);
        Require(session.inventory.pistol.TryFire() && director.AlarmActive && director.receptionist.HitsTaken == 1,
            "A real nonfatal pistol hit starts the receptionist alarm and unlocks the heist sequence.");
        director.policeWaves.StopAssault();
        foreach (FacilityDoor door in FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None))
        {
            if (door.transform.IsChildOf(car) || door.IsLocked || door.IsOpen || door.interaction == null) continue;
            yield return Interact(door.interaction);
            yield return new WaitForSeconds(door.travelSeconds + .08f);
        }
        yield return Interact(director.vaultInteraction);
        Time.timeScale = 4f;
        float deadline = Time.realtimeSinceStartup + 22f;
        while (!director.VaultOpen && Time.realtimeSinceStartup < deadline) yield return null;
        Require(director.VaultOpen && director.vaultDrill.ElapsedSeconds == 45f, "The actual vault completes exactly forty-five simulated seconds of drilling.");
        yield return new WaitForSeconds(director.vaultDrill.vaultDoor.travelSeconds + .15f); Time.timeScale = 1f;
        yield return Interact(director.cashPickup);
        Require(director.HasCash && !director.HasGold && !director.IsInteractionAvailable(director.sedanExit, out _) &&
            !extraction.IsBoarding && FacilityEconomy.Cash == 0,
            "Cash alone cannot board the car or pay the account.");
        yield return Interact(director.goldPickup);
        Require(director.HasCash && director.HasGold && director.Stage == FacilityStoryStage.ReturnToSedan,
            "Retrieving both real vault loot piles advances to the return-to-car objective.");
        Require(courtesy.passengerDoor.IsOpen && !courtesy.driverDoor.IsOpen &&
            !director.IsInteractionAvailable(director.sedanExit, out _) && !director.TryInteract(director.sedanExit, out _) &&
            !extraction.IsBoarding && FacilityEconomy.Cash == 0,
            "Even with both loot piles, the open passenger door cannot bypass the closed driver door.");
        CheckCarWords();

        Vector3 driverApproach = FindPose(courtesy.driverDoor.interaction);
        receipt.outsideDriverPose = car.InverseTransformPoint(driverApproach);
        Require(receipt.outsideDriverPose.x < -.9f, "The reachable driver-door approach is physically outside the car on its left side.");
        yield return WalkTo(driverApproach);
        Aim(courtesy.driverDoor.interaction.transform.position); yield return null;
        Require(director.TryInteract(courtesy.driverDoor.interaction, out string doorReason), "After walking from the vault, E opens the physical driver door: " + doorReason);
        yield return new WaitForSeconds(courtesy.driverDoor.travelSeconds + .15f);
        Require(courtesy.driverDoor.IsOpen, "The driver's door finishes opening before seat use.");
        Vector3 seatApproach = FindPose(director.sedanExit);
        receipt.outsideSeatPose = car.InverseTransformPoint(seatApproach);
        Require(receipt.outsideSeatPose.x < -.9f, "The seat is visible from a collision-free supported position outside the open left doorway.");
        yield return WalkTo(seatApproach);
        Aim(director.sedanExit.transform.position); yield return null;
        Require(ClearCapsule(player.transform.position) && director.IsInteractionAvailable(director.sedanExit, out _) && extraction.CanBoard(out _),
            "A player can physically walk around the open door and reach the real driver's seat interaction.");
        Require(receipt.walkedMeters > 20f && receipt.movementSteps > 100, "Reachability uses actual CharacterController movement from the vault through the bank and parking lot.");
        yield return Capture("01_open_driver_seat_prompt");
        Require(director.TryInteract(director.sedanExit, out string boardingReason), "E on the visible seat begins boarding: " + boardingReason);
        Require(extraction.IsBoarding && !extraction.IsSeated && session.VehicleInputLocked &&
            !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed &&
            director.Stage != FacilityStoryStage.Completed && FacilityEconomy.Cash == 0,
            "Boarding immediately blocks movement and weapons, keeps the run incomplete, and cannot pay early.");
        yield return new WaitForSeconds(Mathf.Min(.15f, extraction.boardingSeconds * .2f));
        session.PauseGame(); yield return null;
        Vector3 heldRoot = player.transform.position, heldEye = eyes.transform.position;
        float heldProgress = extraction.BoardingProgress;
        yield return new WaitForSecondsRealtime(.3f);
        Require(session.CurrentState == FacilityGameState.Paused && extraction.IsBoarding && !extraction.IsSeated &&
            Mathf.Abs(extraction.BoardingProgress - heldProgress) < .0001f && Vector3.Distance(heldRoot, player.transform.position) < .0001f &&
            Vector3.Distance(heldEye, eyes.transform.position) < .0001f && FacilityEconomy.Cash == 0,
            "Pausing freezes the boarding path and seated camera transition without completing or paying the run.");
        yield return Capture("02_boarding_paused");
        session.ResumeGame();
        deadline = Time.realtimeSinceStartup + 3f;
        while (!extraction.IsSeated && Time.realtimeSinceStartup < deadline) yield return null;
        Require(extraction.IsSeated && extraction.IsBoarding && session.CurrentState == FacilityGameState.Playing,
            "The actual seated driver view is visible during the door-closing portion of boarding before completion UI appears.");
        yield return Capture("05_driver_seat_world", false);
        deadline = Time.realtimeSinceStartup + 5f;
        while (session.CurrentState != FacilityGameState.LevelComplete && Time.realtimeSinceStartup < deadline) yield return null;
        receipt.finalSeatRoot = car.InverseTransformPoint(player.transform.position);
        receipt.finalSeatCamera = car.InverseTransformPoint(eyes.transform.position);
        Require(extraction.IsSeated && !extraction.IsBoarding && director.Stage == FacilityStoryStage.Completed &&
            session.CurrentState == FacilityGameState.LevelComplete && FacilityEconomy.Cash == 2500,
            "The completed boarding animation extracts both loot piles and saves exactly one $2,500 payout.");
        Require(Vector3.Distance(player.transform.position, extraction.seatPose.position) < .035f &&
            Vector3.Distance(eyes.transform.position, extraction.eyePose.position) < .035f &&
            Vector3.Dot(eyes.transform.forward, extraction.eyePose.forward) > .99f,
            "The final player and camera actually occupy the authored driver's seat and look forward through the windshield.");
        Require(!FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed && session.completionMenu.activeInHierarchy,
            "The seated completion view retains its proper completion menu and gameplay lock.");
        yield return Capture("03_seated_extraction_complete");
        Require(!director.TryInteract(director.sedanExit, out _) && !FacilityEconomy.AwardHeist() && FacilityEconomy.Cash == 2500,
            "Repeated seat interaction and payout attempts cannot award the run twice.");
        previous = session; session.RestartGame(); yield return WaitForSession(previous);
        Require(!extraction.IsBoarding && !extraction.IsSeated && !session.VehicleInputLocked && motor.enabled &&
            Vector3.Distance(player.transform.position, director.spawnPoint.position) < .35f &&
            Vector3.Distance(eyes.transform.position, extraction.eyePose.position) > .8f && FacilityEconomy.Cash == 2500 &&
            !director.HasCash && !director.HasGold && !courtesy.driverDoor.IsOpen,
            "Restart returns to the normal standing spawn with closed doors, fresh mission state and the saved payout intact.");
        yield return Capture("04_restart_normal_spawn");
        Aim(new Vector3(42f, 1f, -26f));
        yield return Capture("06_runtime_grass_and_fog", false);
    }
    void CheckCarWords()
    {
        foreach (Text text in session.canvas.GetComponentsInChildren<Text>(true))
            Require(text.text.IndexOf("Challenger", StringComparison.OrdinalIgnoreCase) < 0, "Player-facing UI uses car wording: " + text.name);
        foreach (FacilityStoryInteractable item in car.GetComponentsInChildren<FacilityStoryInteractable>(true))
            Require(string.IsNullOrEmpty(item.label) || item.label.IndexOf("Challenger", StringComparison.OrdinalIgnoreCase) < 0,
                "Car interaction labels use the requested car wording: " + item.name);
    }
    IEnumerator WaitForSession(FacilityGameSession previous)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance == previous) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        session = FacilityGameSession.Instance;
        Require(session != null && session != previous && session.storyDirector != null && session.CurrentState == FacilityGameState.Playing,
            "A fresh story scene starts through the real session transition.");
        director = session.storyDirector; player = session.player; motor = player.GetComponent<CharacterController>(); eyes = player.GetComponentInChildren<Camera>();
        car = director.sedanExit.transform.parent; courtesy = car.GetComponent<FacilityVehicleCourtesyLights>(); extraction = director.vehicleExtraction;
        player.enabled = false;
    }
    IEnumerator Interact(FacilityStoryInteractable item)
    { Pose(FindPose(item), item.transform.position); yield return null; Require(director.TryInteract(item, out string reason), "Actual " + item.kind + " interaction: " + reason); }
    IEnumerator WalkTo(Vector3 goal)
    {
        var path = new NavMeshPath();
        Require(NavMesh.CalculatePath(player.transform.position, goal, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
            "Navigation provides a complete physical route to the driver's side.");
        float total = 0f; Vector3 previous = player.transform.position;
        foreach (Vector3 corner in path.corners) { total += Vector3.Distance(previous, corner); previous = corner; }
        float until = Time.realtimeSinceStartup + total / Mathf.Max(1f, player.walkSpeed) + 8f;
        foreach (Vector3 corner in path.corners)
        {
            Vector3 target = corner; target.y = player.transform.position.y;
            while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(target.x, target.z)) > .11f)
            {
                if (Time.realtimeSinceStartup >= until) throw new InvalidOperationException("The actual player became stuck while walking to the driver door/seat at " + player.transform.position + ".");
                Vector3 delta = target - player.transform.position; delta.y = 0f;
                Vector3 before = player.transform.position;
                motor.Move(delta.normalized * Mathf.Min(player.walkSpeed * Time.deltaTime, delta.magnitude) + Vector3.down * (2f * Time.deltaTime));
                receipt.walkedMeters += Vector3.Distance(before, player.transform.position); receipt.movementSteps++;
                yield return null;
            }
        }
        Require(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(goal.x, goal.z)) < .18f && ClearCapsule(player.transform.position),
            "The physically moved player reaches the supported, unobstructed driver-side interaction point.");
    }
    Vector3 FindPose(FacilityStoryInteractable target)
    {
        Vector3 aim = target.transform.position;
        for (int r = 0; r < 8; r++) for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI / 32f, radius = .8f + r * .2f;
            Vector3 candidate = aim + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius); candidate.y = .05f;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .4f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            if ((target.kind == FacilityStoryInteractionKind.Sedan || target.door == courtesy.driverDoor) && car.InverseTransformPoint(candidate).x >= -1.02f) continue;
            if (!ClearCapsule(candidate)) continue;
            Vector3 origin = candidate + Vector3.up * 1.65f, offset = aim - origin;
            if (offset.magnitude > director.interactionRange - .05f) continue;
            Collider nearest = FirstHit(origin, offset, offset.magnitude + .08f, true);
            FacilityStoryInteractable resolved = nearest != null ? nearest.GetComponentInParent<FacilityStoryInteractable>() : null;
            if (resolved == null && nearest != null) resolved = nearest.GetComponentInParent<FacilityDoor>()?.interaction;
            if (resolved == target) return candidate;
        }
        throw new InvalidOperationException("No collision-free supported sight line to " + target.name + " at " + aim + ".");
    }
    Vector3 FindActorPose(FacilityHumanoid actor)
    {
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI / 32f;
            Vector3 candidate = actor.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 2.1f;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .4f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            if (!ClearCapsule(candidate)) continue;
            Vector3 origin = candidate + Vector3.up * 1.65f, offset = actor.AimPoint - origin;
            Collider hit = FirstHit(origin, offset, offset.magnitude + .1f, false);
            if (hit != null && hit.GetComponentInParent<FacilityHumanoid>() == actor) return candidate;
        }
        throw new InvalidOperationException("No reachable nonfatal pistol pose for the receptionist.");
    }
    bool ClearCapsule(Vector3 feet)
    {
        int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * .36f, feet + Vector3.up * 1.5f, .30f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (overlaps[i] != null && !overlaps[i].transform.IsChildOf(player.transform)) return false;
        return true;
    }
    Collider FirstHit(Vector3 origin, Vector3 direction, float distance, bool interactions)
    {
        int count = Physics.RaycastNonAlloc(origin, direction.normalized, hits, distance, Physics.DefaultRaycastLayers, interactions ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
        Collider first = null; float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null || hit is CharacterController || hit.transform.IsChildOf(player.transform)) continue;
            if (hit.isTrigger && hit.GetComponentInParent<FacilityStoryInteractable>() == null) continue;
            if (hits[i].distance < nearest) { nearest = hits[i].distance; first = hit; }
        }
        return first;
    }
    void Pose(Vector3 feet, Vector3 aim)
    { motor.enabled = false; player.transform.position = feet; motor.enabled = true; Aim(aim); Physics.SyncTransforms(); }
    void Aim(Vector3 point)
    {
        player.ResetRecoil(); Vector3 offset = point - eyes.transform.position, flat = offset; flat.y = 0f;
        if (flat.sqrMagnitude > .001f) player.transform.rotation = Quaternion.LookRotation(flat);
        eyes.transform.rotation = Quaternion.LookRotation(offset); Physics.SyncTransforms();
    }
    IEnumerator Capture(string name, bool includeUI = true)
    {
        yield return new WaitForEndOfFrame();
        Canvas canvas = session.canvas; RenderMode mode = canvas.renderMode; Camera prior = canvas.worldCamera; float plane = canvas.planeDistance;
        bool canvasEnabled = canvas.enabled;
        RenderTexture active = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32); target.Create();
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            canvas.enabled = canvasEnabled && includeUI;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = eyes; canvas.planeDistance = .15f; Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(eyes, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG()); receipt.pictures++;
        }
        finally
        { canvas.enabled = canvasEnabled; canvas.renderMode = mode; canvas.worldCamera = prior; canvas.planeDistance = plane; RenderTexture.active = active; Destroy(pixels); target.Release(); Destroy(target); }
    }
    void Require(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); receipt.checks.Add(description); }
    void OnLog(string message, string trace, LogType kind)
    { if (collecting && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace); }
}
