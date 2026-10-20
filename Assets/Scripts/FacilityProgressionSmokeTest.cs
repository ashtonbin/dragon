using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Explicit CLI only: -facilityProgressionTest outputDir. Uses a disposable wallet.</summary>
public sealed class FacilityProgressionSmokeTest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "Real saved scenes and mission interactions, sedan weapon retrieval, first-hit alarm, both loot orders, extraction and duplicate payout prevention, account reload, shop purchases, restart/scene persistence, AK firing and reloading. AI is held during mission assertions; natural guard combat has its own diagnostic. Drilling is accelerated to four times simulation speed after pause verification. Wallet is disposable.";
        public List<string> checks = new List<string>();
        public List<string> failures = new List<string>();
    }
    readonly Receipt receipt = new Receipt();
    readonly RaycastHit[] hits = new RaycastHit[96];
    readonly Collider[] poseOverlaps = new Collider[96];
    FacilityGameSession session;
    FacilityStoryDirector director;
    RetroFPSController player;
    Camera eye;
    CharacterController motor;
    string output;
    bool collecting;
    IDisposable accountScope;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-facilityProgressionTest");
        if (index < 0) return;
        if (index + 1 >= args.Length) { Application.Quit(2); return; }
        var runner = new GameObject("Progression checks - CLI only").AddComponent<FacilityProgressionSmokeTest>();
        runner.output = args[index + 1]; DontDestroyOnLoad(runner.gameObject);
    }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        Directory.CreateDirectory(output);
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        accountScope = FacilityEconomy.UseIsolatedAccount();
        collecting = true; Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object current = null;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception error) { receipt.failures.Add(error.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        collecting = false; Application.logMessageReceived -= OnLog;
        accountScope.Dispose(); Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "progression-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.7f);
        session = FacilityGameSession.Instance;
        Require(session != null && session.shop != null, "Saved main menu loads with shop.");
        session.shop.Show(); yield return null;
        Require(session.shop.IsOpen && !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed,
            "Shop blocks gameplay and displays from title.");
        Require(!session.startButton.IsInteractable(), "Shop excludes underlying menu controls from keyboard navigation.");
        yield return Capture("01_shop_new_account"); session.shop.Hide();
        Require(session.startButton.IsInteractable(), "Closing shop restores underlying menu controls.");
        FacilityGameSession previous = session; session.StartStory(); yield return WaitForSession(previous);
        yield return Heist(false);
        Require(FacilityEconomy.Cash == 2500, "First extraction saves exactly $2,500.");
        session.CompleteStoryLevel();
        Require(FacilityEconomy.Cash == 2500 && !FacilityEconomy.AwardHeist(), "Duplicate completion cannot pay twice.");
        FacilityEconomy.ReloadSavedAccount();
        Require(FacilityEconomy.Cash == 2500, "Extraction cash survives account reload.");
        previous = session; session.RestartGame(); yield return WaitForSession(previous);
        Require(FacilityEconomy.Cash == 2500 && !director.HasCash && !director.HasGold && !director.AlarmActive,
            "Restart resets mission and preserves wallet.");
        yield return Heist(true);
        Require(FacilityEconomy.Cash == 5000, "A second run accumulates the saved payout.");
        session.shop.Show(); yield return null;
        Require(session.shop.IsOpen, "Shop opens from successful extraction.");
        Require(!session.returnMainButton.IsInteractable(), "Completion controls cannot activate behind the shop.");
        Button shotgunButton = FindPurchaseButton("SHOTGUN");
        Require(shotgunButton != null && shotgunButton.interactable, "Affordable shotgun has a shop purchase control.");
        shotgunButton.onClick.Invoke(); yield return null;
        Require(FacilityEconomy.OwnsShotgun && FacilityEconomy.Cash == 0, "$5,000 shop purchase saves debit and ownership.");
        Require(!FacilityEconomy.TryPurchase(FacilityWeaponKind.Shotgun, out _) && FacilityEconomy.Cash == 0,
            "Owned weapons cannot be charged twice.");
        for (int i = 0; i < 4; i++) { FacilityEconomy.BeginRun(); FacilityEconomy.AwardHeist(); }
        yield return null;
        Button rifleButton = FindPurchaseButton("AK-47");
        Require(rifleButton != null && rifleButton.interactable, "Affordable AK-47 has a shop purchase control.");
        rifleButton.onClick.Invoke(); yield return null;
        Require(FacilityEconomy.OwnsAK47 && FacilityEconomy.Cash == 0, "$10,000 shop purchase saves debit and ownership.");
        FacilityEconomy.ReloadSavedAccount();
        Require(FacilityEconomy.OwnsPistol && FacilityEconomy.OwnsShotgun && FacilityEconomy.OwnsAK47 && FacilityEconomy.Cash == 0,
            "All purchased ownership survives account reload.");
        yield return Capture("05_shop_owned_arsenal"); session.shop.Hide();
        previous = session; session.RestartGame(); yield return WaitForSession(previous);
        yield return CollectEquipment();
        Require(session.inventory.HasPistol && session.inventory.HasShotgun && session.inventory.HasAK47,
            "Purchased arsenal is retrieved together with the drill on the next run.");
        FacilityAK47 rifle = session.inventory.ak47;
        Pose(new Vector3(-6f, .05f, -17f), new Vector3(-6f, 2f, -7f));
        yield return new WaitForSecondsRealtime(.2f);
        Vector3 home = session.inventory.ak47Model.localPosition;
        Require(rifle.TryFire() && rifle.ShotsFired == 1 && rifle.RoundsRemaining == 29 && rifle.CasingsEjected == 1 &&
            player.CameraRecoilDegrees > 1f && Vector3.Distance(home, session.inventory.ak47Model.localPosition) > .06f &&
            rifle.muzzleFlash.activeInHierarchy && rifle.reportSource.isPlaying, "AK-47 fires with camera/model kick, flash, brass and sound.");
        Require(!rifle.TryFire(), "AK automatic cadence rejects immediate duplicate shots.");
        yield return Capture("06_ak47_firing");
        yield return new WaitForSeconds(.15f);
        Require(rifle.TryReload(), "AK-47 begins magazine reload.");
        session.PauseGame(); yield return new WaitForSecondsRealtime(.3f);
        Require(rifle.IsReloading && rifle.RoundsRemaining == 29, "Pause freezes reload and blocks fire.");
        session.ResumeGame(); yield return new WaitForSeconds(rifle.reloadDuration + .2f);
        Require(!rifle.IsReloading && rifle.RoundsRemaining == 30 && rifle.ReloadsCompleted == 1, "Animated reload replenishes the magazine.");
        yield return Capture("07_ak47_ready");
    }

    IEnumerator Heist(bool goldFirst)
    {
        Require(director.pistolPickup == null && director.pistolAlarmWarning == null, "Counter pistol and obsolete warning are removed.");
        Require(!session.inventory.HasPistol && !session.inventory.HasShotgun && !session.inventory.HasAK47,
            "Story weapons remain in the sedan before equipment collection.");
        Require(!director.IsInteractionAvailable(director.sedanExit, out _), "Extraction is locked without both loot piles.");
        Require(!director.IsInteractionAvailable(director.cashPickup, out _) && !director.IsInteractionAvailable(director.goldPickup, out _),
            "Both loot piles require a drilled vault.");
        yield return CollectEquipment();
        Require(session.inventory.HasPistol && session.inventory.SelectedWeapon == FacilityWeaponKind.Pistol && !director.AlarmActive,
            "Sedan grants and equips the default pistol without triggering the alarm.");
        foreach (FacilityKnifeGuard guard in director.guards) { guard.enabled = false; guard.actor.enabled = false; }
        FacilityHumanoid receptionist = director.receptionist;
        Pose(FindActorPose(receptionist), receptionist.AimPoint);
        yield return new WaitForSecondsRealtime(.2f);
        Require(session.inventory.pistol.TryFire() && receptionist.HitsTaken == 1 && !receptionist.IsDead && director.AlarmActive,
            "First nonfatal pistol hit on receptionist immediately starts alarm.");
        Require(director.Stage == FacilityStoryStage.FindVault && Array.TrueForAll(director.guards, g => g.Alerted),
            "First-hit alarm advances vault objective and alerts every guard.");
        director.policeWaves.StopAssault();
        foreach (FacilityDoor door in FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None))
            if (!door.IsLocked && !door.transform.IsChildOf(director.sedanExit.transform.parent)) door.ForceOpen();
        yield return new WaitForSeconds(1f);
        yield return Interact(director.vaultInteraction);
        session.PauseGame(); float elapsed = director.vaultDrill.ElapsedSeconds;
        yield return new WaitForSecondsRealtime(.25f);
        Require(director.vaultDrill.ElapsedSeconds == elapsed, "Pause freezes the vault drill.");
        session.ResumeGame(); Time.timeScale = 4f;
        float until = Time.realtimeSinceStartup + 22f;
        while (!director.VaultOpen && Time.realtimeSinceStartup < until) yield return null;
        Require(director.VaultOpen && director.vaultDrill.ElapsedSeconds == 45f, "Vault opens after exactly 45 simulated seconds.");
        yield return new WaitForSeconds(1f); Time.timeScale = 1f;
        yield return Interact(goldFirst ? director.goldPickup : director.cashPickup);
        Require(!director.IsInteractionAvailable(director.sedanExit, out _) && director.Stage == FacilityStoryStage.FindVault,
            (goldFirst ? "Gold" : "Cash") + " alone cannot complete extraction.");
        yield return Interact(goldFirst ? director.cashPickup : director.goldPickup);
        Require(director.HasCash && director.HasGold && director.Stage == FacilityStoryStage.ReturnToSedan,
            "Both loot orders advance to the final encounter only after both are collected.");
        Require(!director.ExtractionUnlocked && !director.IsInteractionAvailable(director.sedanExit,out _),
            "Both loot items trigger the squad and keep extraction locked until it is killed.");
        yield return FacilityEliteDiagnosticCombat.DefeatSquad(director);
        FacilityVehicleExtraction extraction = director.vehicleExtraction;
        Require(extraction != null && extraction.driverDoor != null, "Extraction uses the saved physical driver's seat and door.");
        if (!extraction.driverDoor.IsOpen)
        {
            yield return Interact(extraction.driverDoor.interaction);
            yield return new WaitForSeconds(extraction.driverDoor.travelSeconds + .15f);
        }
        yield return Interact(director.sedanExit);
        float completedBy = Time.realtimeSinceStartup + 5f;
        while (session.CurrentState != FacilityGameState.LevelComplete && Time.realtimeSinceStartup < completedBy) yield return null;
        Require(director.Stage == FacilityStoryStage.Completed && session.CurrentState == FacilityGameState.LevelComplete &&
            extraction.IsSeated && !FacilityGameSession.GameplayWeaponAllowed, "Both loot piles and the defeated final squad permit animated driver-seat extraction and freeze the completed run.");
        yield return Capture(goldFirst ? "04_gold_first_extraction" : "03_cash_first_extraction");
    }

    IEnumerator CollectEquipment()
    {
        Require(!director.IsInteractionAvailable(director.drillPickup, out _), "Closed trunk blocks equipment pickup.");
        director.drillPickup.door.TryToggle();
        yield return new WaitForSeconds(director.drillPickup.door.travelSeconds + .2f);
        yield return Interact(director.drillPickup);
        Require(director.HasDrill && session.inventory.HasPistol && director.drillPickup.Consumed,
            "Open trunk equipment pickup grants drill and pistol together.");
    }

    IEnumerator Interact(FacilityStoryInteractable item)
    {
        Vector3 aim = item.transform.position;
        Pose(FindInteractionPose(item, aim), aim); yield return null;
        Require(director.TryInteract(item, out string reason), "Interact " + item.kind + ": " + reason);
    }

    IEnumerator WaitForSession(FacilityGameSession previous)
    {
        float until = Time.realtimeSinceStartup + 15f;
        while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance == previous) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        session = FacilityGameSession.Instance;
        Require(session != null && session != previous && session.storyDirector != null, "Fresh story session loads.");
        director = session.storyDirector; player = session.player; motor = player.GetComponent<CharacterController>();
        eye = player.GetComponentInChildren<Camera>(); player.enabled = false;
    }

    void Pose(Vector3 feet, Vector3 aim)
    {
        motor.enabled = false; player.transform.position = feet; motor.enabled = true;
        player.ResetRecoil(); Vector3 facing = aim - eye.transform.position; Vector3 flat = facing; flat.y = 0f;
        if (flat.sqrMagnitude > .001f) player.transform.rotation = Quaternion.LookRotation(flat);
        eye.transform.rotation = Quaternion.LookRotation(facing); Physics.SyncTransforms();
    }

    Vector3 FindInteractionPose(FacilityStoryInteractable target, Vector3 aim)
    {
        for (int r = 0; r < 5; r++) for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI / 24f; float radius = 1f + r * .3f;
            Vector3 candidate = aim + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius); candidate.y = .05f;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .45f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            FacilityVehicleExtraction extraction = director.vehicleExtraction;
            if (extraction != null && (target.kind == FacilityStoryInteractionKind.Sedan || target.door == extraction.driverDoor))
            {
                Vector3 carLocal = director.sedanExit.transform.parent.InverseTransformPoint(candidate);
                if (carLocal.x >= -1.02f) continue;
                if (target.kind == FacilityStoryInteractionKind.Sedan && Mathf.Abs(carLocal.z - director.sedanExit.transform.localPosition.z) > 1.1f) continue;
            }
            int overlapCount = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * .36f, candidate + Vector3.up * 1.5f, .30f,
                poseOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool clear = overlapCount < poseOverlaps.Length;
            for (int n = 0; n < overlapCount && clear; n++) if (poseOverlaps[n] != null && !poseOverlaps[n].transform.IsChildOf(player.transform)) clear = false;
            if (!clear) continue;
            Vector3 offset = aim - (candidate + Vector3.up * 1.65f);
            if (offset.magnitude > director.interactionRange - .05f) continue;
            Collider first = FirstHit(candidate + Vector3.up * 1.65f, offset, offset.magnitude + .08f, true);
            if (first == null) continue;
            FacilityStoryInteractable found = first.GetComponentInParent<FacilityStoryInteractable>();
            if (found == null) found = first.GetComponentInParent<FacilityDoor>()?.interaction;
            if (found == target) return candidate;
        }
        throw new InvalidOperationException("No visible interaction pose for " + target.name);
    }

    Vector3 FindActorPose(FacilityHumanoid actor)
    {
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI / 24f;
            Vector3 candidate = actor.transform.position + new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * 2.1f;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .45f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            Vector3 offset = actor.AimPoint - (candidate + Vector3.up * 1.65f);
            Collider first = FirstHit(candidate + Vector3.up * 1.65f, offset, offset.magnitude + .1f, false);
            if (first != null && first.GetComponentInParent<FacilityHumanoid>() == actor) return candidate;
        }
        throw new InvalidOperationException("No visible firing pose for receptionist.");
    }

    Collider FirstHit(Vector3 origin, Vector3 direction, float distance, bool interactions)
    {
        int count = Physics.RaycastNonAlloc(origin, direction.normalized, hits, distance, Physics.DefaultRaycastLayers,
            interactions ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
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

    Button FindPurchaseButton(string weapon)
    {
        foreach (Button button in session.canvas.GetComponentsInChildren<Button>(true))
        {
            if (!button.gameObject.activeInHierarchy) continue;
            Transform card = button.transform.parent;
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
                if (text.text.ToUpperInvariant().Contains(weapon) && button.GetComponentInChildren<Text>().text.ToUpperInvariant().Contains("BUY")) return button;
        }
        return null;
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Camera camera = session.player.GetComponentInChildren<Camera>();
        Canvas canvas = session.canvas;
        RenderMode mode = canvas.renderMode; Camera priorCamera = canvas.worldCamera;
        float plane = canvas.planeDistance;
        RenderTexture previous = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); target.Create();
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            // Hidden standalone windows do not present a backbuffer. Render the actual camera and UI explicitly.
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .15f;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = priorCamera; canvas.planeDistance = plane;
            RenderTexture.active = previous; Destroy(pixels); target.Release(); Destroy(target);
        }
    }
    void Require(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); receipt.checks.Add(description); }
    void OnLog(string message, string trace, LogType kind)
    { if (collecting && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace); }
}
