using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Standalone checks: -facilityStoryTest or targeted -facilityReceptionTest "output directory".</summary>
/// <remarks>Loads real scenes and invokes the authored weapons and interactions. Never runs in an ordinary session.</remarks>
public sealed class FacilityStorySmokeTest : MonoBehaviour
{
    [Serializable] private sealed class Receipt
    {
        public bool success, mainMenu, modeSelection, storySceneLoads, storyStartsBeforeEquipment, startsBesideSedan, nightLighting,
            spawnMenuBlockedInStory, counterPistolRemoved, extractionLockedBeforeLoot,
            receptionistDiesInThreePunches, fistsPreserveAnatomy, pistolRetrievedWithDrill,
            alarmStartsOnReceptionistAttack, knifeGuardsAlertAndNavigate, coloredShirtsAndKnives,
            objectiveTypesAndPauses, pistolFiresAndKicks, firstHitGoreScaleValid, goldRequired, cashRequired, bothLootUnlockExtraction,
            storyRestartResetsMission, extractionCompletesLevel, completionFreezesInput,
            returnToMainMenuWorks, sandboxHasBothGuns, sandboxRetainsFacilityAndLightmaps,
            sandboxSpawnMenuWorks, sandboxObserverHasKnife, knifeDamagesPlayer, redFlashOnStab, threeKnifeHitsKillPlayer,
            doorsStartClosed, trunkLocksEquipment, trunkOpensPhysically, drillRetrievedFromTrunk, vaultStartsLocked,
            cashBlockedBeforeDrilling, vaultWaitsFortyFiveSeconds, drillPauses, drillIsVisibleAndAudible,
            policeCountdownStartsWithAlarm, policeArriveEveryFifteenSeconds, heistStatusVisible,
            jetBrainsMonoFont, typedObjectiveClicks, statusHiddenBeforeAttack, counterAlarmWarningRemoved, alarmFlashes;
        public string utc, unity, gpu, startupScene, storyScene, sandboxScene;
        public string coverage = "Real title/mode selection and scenes; closed colliding doors; physically opened sedan trunk with pistol and drill collected together; optional fist receptionist combat and alarm on the first hit; removed counter pistol; actual 45-second vault drilling twice, paused timer and live 15-second police wave arrivals; required cash and gold before extraction; restart resets; fonts/typing audio; Sandbox weapons/spawning and natural knife death. Story knife guards are held only after initial pursuit is measured. Spawned police brains/actors are held immediately during deterministic mission/timer assertions, while their production wave clock and spawns continue. Natural police projectile, obstruction and dodge behavior is covered by the separate police diagnostic. A disposable account protects the player's saved wallet; authored scenes are never saved.";
        public int width, height, screenshotCount, punches, pistolShots, guardCount, shirtColors, playerHealthAfterReceptionist, sandboxLightmaps, knifeAttacks;
        public float pistolCameraKick, pistolModelTravel, guardTravel, largestFirstHitBloodStreak, actualDrillSeconds;
        public List<float> policeArrivalSeconds = new List<float>();
        public List<int> policeSpawnTotals = new List<int>();
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[64];
    private string output;
    private bool collectingErrors;
    private bool receptionOnly;
    private FacilityGameSession session;
    private FacilityStoryDirector director;
    private FacilityWeaponInventory inventory;
    private RetroFPSController player;
    private CharacterController motor;
    private Camera eye;
    private float assaultStartedAt;
    private int observedWaves;
    private bool holdStoryPolice;
    private readonly List<float> currentPoliceArrivals = new List<float>();
    private readonly List<int> currentPoliceTotals = new List<int>();

    private void Update()
    {
        if (!holdStoryPolice || director == null) return;
        foreach (FacilityPoliceOfficer officer in FindObjectsByType<FacilityPoliceOfficer>(FindObjectsSortMode.None))
        {
            officer.enabled = false;
            if (officer.actor != null) officer.actor.enabled = false;
        }
        FacilityPoliceWaves waves = director.policeWaves;
        if (waves == null || waves.WaveNumber <= observedWaves) return;
        observedWaves = waves.WaveNumber;
        currentPoliceArrivals.Add(waves.LastWaveAt - waves.AssaultStartedAt);
        currentPoliceTotals.Add(waves.TotalPoliceSpawned);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-facilityStoryTest");
        bool reception = false;
        if (flag < 0) { flag = Array.IndexOf(args, "-facilityReceptionTest"); reception = flag >= 0; }
        if (flag < 0) return;
        if (flag + 1 >= args.Length || args[flag + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityStoryTest requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Facility story diagnostics (explicit CLI only)").AddComponent<FacilityStorySmokeTest>();
        runner.output = args[flag + 1];
        runner.receptionOnly = reception;
        if (reception) runner.receipt.coverage = "Targeted reception regression through the real main menu and Story scene: open the sedan trunk, retrieve the pistol and drill together without an alarm, verify the old counter pickup is removed, select optional fists and defeat the receptionist with three actual punches, and verify the first hit immediately starts the alarm and guard pursuit. A disposable account protects the player's saved wallet. No vault wait, extraction, restart or Sandbox claims are made by this targeted run.";
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        IDisposable diagnosticAccount = FacilityEconomy.UseIsolatedAccount();
        collectingErrors = true;
        Application.logMessageReceived += OnLog;
        var routines = new Stack<IEnumerator>();
        routines.Push(Run());
        while (routines.Count > 0)
        {
            IEnumerator routine = routines.Peek();
            bool more;
            object current = null;
            try { more = routine.MoveNext(); if (more) current = routine.Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { routines.Pop(); continue; }
            if (current is IEnumerator nested) routines.Push(nested);
            else yield return current;
        }
        collectingErrors = false;
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        diagnosticAccount.Dispose();
        try
        {
            receipt.success = receipt.failures.Count == 0;
            File.WriteAllText(Path.Combine(output, receptionOnly ? "reception-receipt.json" : "story-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Facility story integration check " + (receipt.success ? "passed: " : "failed: ") + output);
        }
        catch (Exception exception) { Debug.LogException(exception); Application.Quit(2); yield break; }
        Application.Quit(receipt.success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(.6f);
        AcquireSession();
        receipt.utc = DateTime.UtcNow.ToString("O"); receipt.unity = Application.unityVersion;
        receipt.gpu = SystemInfo.graphicsDeviceName; receipt.width = Screen.width; receipt.height = Screen.height;
        receipt.startupScene = SceneManager.GetActiveScene().path;
        receipt.mainMenu = session.CurrentState == FacilityGameState.MainMenu && session.mainMenu.activeInHierarchy &&
            VisibleLabel(session.mainMenu, "rob the bank") && !FacilityGameSession.GameplayWeaponAllowed;
        Check(receipt.mainMenu, "The game must begin at the authored title menu.");
        Check(Screen.width == 1280 && Screen.height == 720, "The standalone window did not reach 1280 x 720.");
        yield return Capture("01_title_menu");
        session.startButton.onClick.Invoke();
        yield return null;
        receipt.modeSelection = session.CurrentState == FacilityGameState.ModeSelect && session.modeMenu.activeInHierarchy &&
            session.storyButton.gameObject.activeInHierarchy && session.sandboxButton.gameObject.activeInHierarchy &&
            !FacilityGameSession.GameplayInputAllowed;
        Check(receipt.modeSelection, "Start must offer separate Story and Sandbox choices.");
        yield return Capture("02_story_and_sandbox");
        FacilityGameSession previous = session;
        session.storyButton.onClick.Invoke();
        yield return WaitForScene(previous);
        receipt.storyScene = SceneManager.GetActiveScene().path;
        receipt.storySceneLoads = session.CurrentMode == FacilityGameMode.Story && session.CurrentState == FacilityGameState.Playing && director != null;
        Require(receipt.storySceneLoads, "Choosing Story did not load a playable mission with its director.");
        yield return VerifyFreshStory(false);
        yield return RetrieveDrill(true);
        yield return FirstReceptionistEncounter(true);
        yield return InspectAlarmAndGuardPursuit(true);
        if (receptionOnly) yield break;
        yield return FirePistol();
        yield return DrillVault(true);
        yield return CollectLoot(true);

        // Use the actual visible pause Restart button after changing mission state and acquiring a gun.
        session.PauseGame();
        yield return null;
        string heldText = director.typedObjective != null && director.typedObjective.bodyText != null ? director.typedObjective.bodyText.text : "";
        float heldTime = Time.time;
        yield return new WaitForSecondsRealtime(.2f);
        Check(session.CurrentState == FacilityGameState.Paused && Time.time == heldTime &&
            (director.typedObjective == null || director.typedObjective.bodyText == null || director.typedObjective.bodyText.text == heldText),
            "Pause must freeze the mission, its objective typing and guard simulation.");
        previous = session;
        session.restartButton.onClick.Invoke();
        yield return WaitForScene(previous);
        yield return VerifyFreshStory(true);
        yield return RetrieveDrill(false);
        yield return FirstReceptionistEncounter(false);
        yield return InspectAlarmAndGuardPursuit(false);
        yield return DrillVault(false);
        yield return CollectLoot(false);
        yield return Extract();

        previous = session;
        session.returnMainButton.onClick.Invoke();
        yield return WaitForScene(previous);
        receipt.returnToMainMenuWorks = session.CurrentState == FacilityGameState.MainMenu && session.mainMenu.activeInHierarchy;
        Check(receipt.returnToMainMenuWorks, "The completion menu did not return to the main menu scene.");
        session.startButton.onClick.Invoke();
        yield return null;
        session.sandboxButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.3f);
        AcquireSession();
        holdStoryPolice = false;
        yield return VerifySandbox();
    }

    private IEnumerator VerifyFreshStory(bool afterRestart)
    {
        Require(director != null && director.receptionist != null &&
            director.cashPickup != null && director.goldPickup != null && director.sedanExit != null &&
            director.drillPickup != null && director.vaultInteraction != null && director.vaultDrill != null && director.policeWaves != null,
            "The story scene is missing its authored mission references.");
        bool awaitingEquipment = !inventory.HasPistol && !inventory.HasShotgun && !inventory.HasAK47 && inventory.SelectedWeapon == FacilityWeaponKind.Fists &&
            inventory.fists != null && inventory.fistsModel != null && inventory.fistsModel.gameObject.activeInHierarchy &&
            !inventory.Equip(FacilityWeaponKind.Pistol) && !inventory.Equip(FacilityWeaponKind.Shotgun) &&
            !inventory.pistol.TryFire() && !inventory.shotgun.TryFire();
        Check(awaitingEquipment, "A new Story run must leave the owned firearms in the sedan until the equipment pickup.");
        bool fresh = awaitingEquipment && director.Stage == FacilityStoryStage.RetrieveDrill && !director.HasCash && !director.HasGold && !director.HasDrill &&
            !director.VaultOpen && !director.vaultDrill.IsRunning && director.vaultDrill.ElapsedSeconds == 0f &&
            !director.AlarmActive && !director.receptionist.IsDead && director.receptionist.HitsTaken == 0 && session.health.CurrentHealth == 3;
        if (afterRestart)
        {
            receipt.storyRestartResetsMission = fresh;
            Check(fresh, "Restart carried weapons, health damage, loot, deaths or alarm state into a fresh story mission.");
        }
        else
        {
            receipt.storyStartsBeforeEquipment = awaitingEquipment;
            receipt.startsBesideSedan = director.spawnPoint != null && Vector3.Distance(player.transform.position, director.spawnPoint.position) < .4f &&
                Vector3.Distance(player.transform.position, director.sedanExit.transform.position) < 3f;
            Check(receipt.startsBesideSedan, "The story player did not spawn beside the black sedan's driver door.");
            receipt.nightLighting = RenderSettings.ambientLight.maxColorComponent < .2f && RenderSettings.fog &&
                RenderSettings.fogMode == FogMode.Linear && RenderSettings.fogColor.maxColorComponent < .1f;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.enabled && light.type == LightType.Directional && light.intensity > .15f) receipt.nightLighting = false;
            Check(receipt.nightLighting, "The mission lost its dark outdoor ambient/fog settings or has strong directional daylight.");
            session.ToggleSpawnMenu();
            receipt.spawnMenuBlockedInStory = session.CurrentState == FacilityGameState.Playing && (session.spawnMenu == null || !session.spawnMenu.activeInHierarchy);
            Check(receipt.spawnMenuBlockedInStory, "Story allowed the sandbox character browser.");
            receipt.counterPistolRemoved = director.pistolPickup == null;
            foreach (FacilityStoryInteractable item in FindObjectsByType<FacilityStoryInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                receipt.counterPistolRemoved &= item.kind != FacilityStoryInteractionKind.Pistol;
            receipt.extractionLockedBeforeLoot = !director.IsInteractionAvailable(director.sedanExit, out _) && !director.TryInteract(director.sedanExit, out _);
            Check(receipt.counterPistolRemoved, "The obsolete counter pistol pickup still exists in the authored Story scene.");
            Check(receipt.extractionLockedBeforeLoot, "The sedan allowed extraction before both cash and gold were retrieved.");
            if (director.typedObjective != null && director.typedObjective.bodyText != null)
            {
                FacilityTypedObjective objective = director.typedObjective;
                string actualObjective = objective.Objective;
                int clicksBefore = objective.TypingClicksPlayed;
                objective.SetObjective(objective.titleText != null ? objective.titleText.text : "OBJECTIVE", actualObjective, 1, 7);
                yield return new WaitForSecondsRealtime(.12f);
                int early = objective.bodyText.text.Length;
                yield return Capture("03_night_parking_typing");
                float typingDeadline = Time.realtimeSinceStartup + actualObjective.Length / Mathf.Max(8f, objective.charactersPerSecond) + .5f;
                while (!objective.FinishedTyping && Time.realtimeSinceStartup < typingDeadline) yield return null;
                receipt.objectiveTypesAndPauses = early > 0 && early < actualObjective.Length && objective.FinishedTyping && objective.bodyText.text == actualObjective;
                Check(receipt.objectiveTypesAndPauses, "The top-right objective did not type progressively into its complete text.");
                receipt.typedObjectiveClicks = objective.typingSource != null && objective.typingClick != null && objective.TypingClicksPlayed > clicksBefore;
                receipt.jetBrainsMonoFont = objective.bodyText.font != null && objective.bodyText.font.name.Replace(" ", "").IndexOf("JetBrainsMono", StringComparison.OrdinalIgnoreCase) >= 0;
                Check(receipt.typedObjectiveClicks, "The typing objective did not play its assigned click audio as characters appeared.");
                Check(receipt.jetBrainsMonoFont, "The objective font is not the saved JetBrains Mono font.");
            }
            else Check(false, "The story's assigned typewriter objective UI is missing.");
        }
        FacilityDoor[] doors = FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None);
        bool closed = doors.Length >= 4;
        foreach (FacilityDoor door in doors)
        {
            closed &= !door.IsOpen && !door.IsMoving;
            bool collider = false;
            foreach (Collider leaf in door.leafColliders) collider |= leaf != null && leaf.enabled && !leaf.isTrigger;
            closed &= collider;
        }
        receipt.doorsStartClosed = closed;
        receipt.vaultStartsLocked = director.vaultDrill.vaultDoor != null && director.vaultDrill.vaultDoor.IsLocked && !director.VaultOpen;
        Check(closed, "Authored doors must begin closed with colliding physical leaves.");
        Check(receipt.vaultStartsLocked, "The vault must begin physically closed and locked.");
        yield return null;
    }

    private IEnumerator RetrieveDrill(bool captures)
    {
        FacilityStoryInteractable pickup = director.drillPickup;
        FacilityDoor trunk = pickup.door;
        Require(trunk != null && trunk.leaf != null, "The drill pickup is missing its closed sedan trunk reference.");
        FacilityStoryInteractable trunkInteraction = trunk.GetComponent<FacilityStoryInteractable>();
        if (trunkInteraction == null) trunkInteraction = trunk.GetComponentInParent<FacilityStoryInteractable>();
        if (trunkInteraction == null)
            foreach (FacilityStoryInteractable candidate in trunk.GetComponentsInChildren<FacilityStoryInteractable>())
                if (candidate.kind == FacilityStoryInteractionKind.Door && candidate.door == trunk) { trunkInteraction = candidate; break; }
        Require(trunkInteraction != null && trunkInteraction.kind == FacilityStoryInteractionKind.Door,
            "The sedan trunk has no player door interaction.");
        receipt.trunkLocksEquipment = !director.IsInteractionAvailable(pickup, out _) && !director.TryInteract(pickup, out _) &&
            !director.HasDrill && !inventory.HasPistol && !inventory.HasShotgun && !inventory.HasAK47;
        Check(receipt.trunkLocksEquipment, "The drill or firearm equipment was available through the closed trunk.");
        Vector3 trunkAim = trunkInteraction.transform.position;
        SetPose(FindInteractionPose(trunkInteraction, trunkAim), trunkAim);
        yield return null;
        Quaternion closedRotation = trunk.leaf.localRotation;
        Vector3 closedPosition = trunk.leaf.localPosition;
        bool opened = director.TryInteract(trunkInteraction, out string doorReason);
        Require(opened, "Looking at the sedan trunk did not open it: " + doorReason);
        yield return new WaitForSecondsRealtime(trunk.travelSeconds + .15f);
        receipt.trunkOpensPhysically = trunk.IsOpen && (Quaternion.Angle(closedRotation, trunk.leaf.localRotation) > 20f ||
            Vector3.Distance(closedPosition, trunk.leaf.localPosition) > .3f);
        Check(receipt.trunkOpensPhysically, "The trunk interaction did not physically move its colliding lid out of the way.");
        SetPose(FindInteractionPose(pickup, pickup.transform.position), pickup.transform.position);
        yield return null;
        bool retrieved = director.TryInteract(pickup, out string pickupReason);
        receipt.drillRetrievedFromTrunk = retrieved && director.HasDrill && pickup.Consumed && director.Stage == FacilityStoryStage.EnterFacility;
        Require(receipt.drillRetrievedFromTrunk, "The drill could not be physically retrieved from the opened trunk: " + pickupReason);
        receipt.pistolRetrievedWithDrill = inventory.HasPistol && inventory.HasShotgun == FacilityEconomy.OwnsShotgun &&
            inventory.HasAK47 == FacilityEconomy.OwnsAK47 && inventory.Equip(FacilityWeaponKind.Pistol) &&
            inventory.pistolModel != null && inventory.pistolModel.gameObject.activeInHierarchy &&
            director.receptionist.HitsTaken == 0 && !director.AlarmActive;
        Require(receipt.pistolRetrievedWithDrill, "Collecting the sedan drill did not retrieve and equip the already owned pistol with the saved loadout before any receptionist attack.");
        if (captures) yield return Capture("03b_trunk_pistol_and_drill_retrieved");

        // Door interaction and travel are exercised above; open the ordinary route leaves for the independent combat/mission checks.
        foreach (FacilityDoor door in FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None))
            if (!door.IsLocked && !door.WantsOpen) door.TryToggle();
        yield return new WaitForSecondsRealtime(.9f);
    }

    private IEnumerator FirstReceptionistEncounter(bool captures)
    {
        FacilityHumanoid receptionist = director.receptionist;
        Require(inventory.Equip(FacilityWeaponKind.Fists), "The optional fists could not be selected for their combat regression.");
        SetPose(new Vector3(0f, .05f, 2.85f), receptionist.AimPoint);
        yield return new WaitForSecondsRealtime(.08f);
        Check(director.Stage == FacilityStoryStage.EliminateReceptionist, "Entering reception did not produce the receptionist objective.");
        receipt.statusHiddenBeforeAttack = !director.AlarmActive && director.heistStatusPanel != null && !director.heistStatusPanel.activeSelf;
        Check(receipt.statusHiddenBeforeAttack, "Police countdown appeared before attacking the receptionist.");
        if (captures) yield return Capture("04_reception_optional_fists");
        observedWaves = 0;
        currentPoliceArrivals.Clear();
        currentPoliceTotals.Clear();
        holdStoryPolice = true;
        int punchesBefore = inventory.fists.AttacksMade;
        int contactsBefore = inventory.fists.Hits;
        for (int i = 0; i < 3; i++)
        {
            Aim(receptionist.AimPoint);
            bool punched = inventory.fists.TryPunch();
            Require(punched, "The actual fists rejected punch " + (i + 1) + " while equipped in story.");
            yield return new WaitForSecondsRealtime(.14f);
            Check(receptionist.HitsTaken == i + 1, "Punch " + (i + 1) + " did not cause exactly one receptionist health loss (actual " + receptionist.HitsTaken + ").");
            if (i == 0)
            {
                receipt.alarmStartsOnReceptionistAttack = director.AlarmActive && director.alarmSource != null &&
                    director.alarmSource.isPlaying && director.Stage == FacilityStoryStage.FindVault && !receptionist.IsDead;
                Check(receipt.alarmStartsOnReceptionistAttack, "The first connected receptionist attack did not immediately start the alarm and vault objective while the receptionist was alive.");
                assaultStartedAt = director.policeWaves.AssaultStartedAt;
                receipt.policeCountdownStartsWithAlarm = director.policeWaves.AssaultActive && director.policeWaves.WaveNumber == 0 &&
                    director.policeWaves.ActivePoliceCount == 0 && director.policeWaves.SecondsUntilNextWave > 14.5f &&
                    Time.time - assaultStartedAt >= 0f && Time.time - assaultStartedAt < .15f;
                Check(receipt.policeCountdownStartsWithAlarm, "The first attack must start a fifteen-second countdown without instantly spawning police.");
            }
            if (i < 2) yield return new WaitForSecondsRealtime(Mathf.Max(.01f, inventory.fists.AttackInterval - .13f));
        }
        bool threePunches = inventory.fists.AttacksMade - punchesBefore == 3 && inventory.fists.Hits - contactsBefore == 3 && receptionist.IsDead && receptionist.HitsTaken == 3;
        bool anatomy = !receptionist.HeadExploded && receptionist.SeveredLimbCount == 0;
        receipt.punches += inventory.fists.AttacksMade - punchesBefore;
        receipt.receptionistDiesInThreePunches = threePunches;
        receipt.fistsPreserveAnatomy = anatomy;
        receipt.playerHealthAfterReceptionist = session.health.CurrentHealth;
        Check(threePunches, "Three connected punches did not kill the receptionist.");
        Check(anatomy, "Fists incorrectly exploded the receptionist's head or severed a limb.");
        Check(!session.health.IsDead, "The receptionist could not be beaten with three timely punches.");
        yield return new WaitForSecondsRealtime(.1f);
        Check(director.Stage == FacilityStoryStage.FindVault && director.AlarmActive && inventory.HasPistol,
            "Receptionist defeat must leave the vault objective and alarm active with the sedan pistol already available.");
        if (captures) yield return Capture("05_receptionist_defeated");
    }

    private IEnumerator InspectAlarmAndGuardPursuit(bool captures)
    {
        receipt.counterAlarmWarningRemoved = director.pistolAlarmWarning == null || !director.pistolAlarmWarning.gameObject.activeInHierarchy;
        Check(receipt.counterAlarmWarningRemoved, "The removed counter pickup still displays its obsolete alarm warning.");
        Require(inventory.Equip(FacilityWeaponKind.Pistol), "The pistol retrieved with the sedan drill is no longer usable.");
        Check(director.AlarmActive && director.Stage == FacilityStoryStage.FindVault, "The receptionist attack did not leave an active alarm and vault objective.");
        var startPositions = new Vector3[director.guards.Length];
        var colors = new HashSet<Color32>();
        bool guardsValid = director.guards.Length >= 3, knives = true;
        for (int i = 0; i < director.guards.Length; i++)
        {
            FacilityKnifeGuard guard = director.guards[i];
            if (guard == null || guard.actor == null || guard.actor.IsDead) continue;
            startPositions[i] = guard.transform.position;
            guardsValid &= guard.Alerted && guard.actor.IsHostile && guard.walkSpeed >= 2f;
            knives &= guard.knifeProp != null && guard.knifeProp.GetComponentInChildren<Renderer>() != null;
            if (guard.actor.chest != null)
            {
                FacilityHitRegion torso = guard.actor.chest.GetComponent<FacilityHitRegion>();
                if (torso != null && torso.outerPatches != null)
                    foreach (Renderer renderer in torso.outerPatches)
                        if (renderer != null && renderer.sharedMaterial != null)
                        { colors.Add((Color32)renderer.sharedMaterial.color); break; }
            }
            guard.GetSteeringDirection();
            guardsValid &= guard.HasNavigationPath && guard.NavigationStatus == "PathComplete";
        }
        yield return new WaitForSecondsRealtime(.35f);
        float travel = 0f;
        for (int i = 0; i < director.guards.Length; i++)
        {
            FacilityKnifeGuard guard = director.guards[i];
            if (guard == null || guard.actor == null || guard.actor.IsDead) continue;
            travel = Mathf.Max(travel, Vector3.Distance(startPositions[i], guard.transform.position));
            // Stop the already verified combat controllers while testing independent mission states.
            guard.enabled = false;
            guard.actor.enabled = false;
        }
        receipt.guardCount = director.guards.Length;
        receipt.guardTravel = Mathf.Max(receipt.guardTravel, travel);
        receipt.knifeGuardsAlertAndNavigate = guardsValid && travel > .12f;
        receipt.shirtColors = colors.Count;
        receipt.coloredShirtsAndKnives = knives && colors.Count >= 3;
        Check(receipt.knifeGuardsAlertAndNavigate, "Alarm guards did not alert, plan complete routes and start moving toward the player.");
        Check(receipt.coloredShirtsAndKnives, "Guards need visible knives and at least three different authored shirt colors.");
        foreach (Light light in director.alarmLights)
            Check(light != null && light.intensity > 0f && light.shadows == LightShadows.None, "An alarm beacon has zero authored intensity or realtime shadows.");
        if (captures) yield return Capture("06_reception_alarm_and_sedan_pistol");
        bool sawOn = false, sawOff = false;
        float sampleUntil = Time.realtimeSinceStartup + 1.2f;
        while (Time.realtimeSinceStartup < sampleUntil)
        {
            foreach (Light lamp in director.alarmLights)
            { sawOn |= lamp.enabled; sawOff |= !lamp.enabled; }
            yield return null;
        }
        receipt.alarmFlashes = sawOn && sawOff;
        Check(receipt.alarmFlashes, "Emergency beacons must visibly switch off and on during the alarm.");
    }

    private IEnumerator FirePistol()
    {
        FacilityHumanoid target = null;
        foreach (FacilityKnifeGuard guard in director.guards) if (guard != null && guard.actor != null && !guard.actor.IsDead) { target = guard.actor; break; }
        Require(target != null, "No living knife guard was available for the pistol test.");
        Vector3 feet = FindFiringPose(target, 2.1f);
        SetPose(feet, target.AimPoint);
        yield return new WaitForSecondsRealtime(.18f);
        int shots = inventory.pistol.ShotsFired, oldHealth = target.HitsTaken;
        Vector3 modelPosition = inventory.pistolModel.localPosition;
        bool fired = inventory.pistol.TryFire();
        receipt.pistolCameraKick = player.CameraRecoilDegrees;
        receipt.pistolModelTravel = Vector3.Distance(modelPosition, inventory.pistolModel.localPosition);
        receipt.pistolShots++;
        receipt.pistolFiresAndKicks = fired && inventory.pistol.ShotsFired == shots + 1 && target.HitsTaken == oldHealth + 1 &&
            receipt.pistolCameraKick > 1.4f && receipt.pistolModelTravel > .06f && inventory.pistol.muzzleFlash != null && inventory.pistol.muzzleFlash.activeInHierarchy;
        Check(receipt.pistolFiresAndKicks, "The actual pistol did not hit a guard and produce immediate model/camera kick plus flash.");
        int streaks = 0;
        foreach (MeshRenderer blood in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!blood.name.StartsWith("Blood streak ", StringComparison.Ordinal)) continue;
            streaks++;
            Vector3 size = blood.bounds.size;
            receipt.largestFirstHitBloodStreak = Mathf.Max(receipt.largestFirstHitBloodStreak, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }
        receipt.firstHitGoreScaleValid = streaks > 0 && receipt.largestFirstHitBloodStreak < .35f;
        Check(receipt.firstHitGoreScaleValid, "A first-use blood streak rendered at unit size on its actual hit frame.");
        yield return Capture("07_pistol_firing");
        yield return new WaitForSecondsRealtime(.2f);
    }

    private IEnumerator DrillVault(bool captures)
    {
        FacilityVaultDrill drill = director.vaultDrill;
        FacilityStoryInteractable interaction = director.vaultInteraction;
        Require(drill != null && interaction != null && drill.vaultDoor != null,
            "The vault door, drill and installation interaction must be saved references.");
        receipt.cashBlockedBeforeDrilling = !director.IsInteractionAvailable(director.cashPickup, out _) &&
            !director.IsInteractionAvailable(director.goldPickup, out _) && !drill.IsRunning && !drill.IsComplete;
        Check(receipt.cashBlockedBeforeDrilling, "The cash or gold can be collected before the vault finishes drilling.");
        SetPose(FindInteractionPose(interaction, interaction.transform.position), interaction.transform.position);
        yield return null;
        float startedAt = Time.time;
        bool began = director.TryInteract(interaction, out string reason);
        Require(began && drill.IsRunning && director.Stage == FacilityStoryStage.DrillingVault,
            "Looking at the vault did not install and start the drill: " + reason);
        Check(!drill.BeginDrilling() && drill.StartCount == 1, "Repeated interaction restarted or duplicated the vault drill.");
        yield return new WaitForSecondsRealtime(.12f);
        receipt.drillIsVisibleAndAudible = drill.deployedDrill != null && drill.deployedDrill.activeInHierarchy &&
            drill.deployedDrill.GetComponentInChildren<Renderer>() != null && drill.drillLoop != null &&
            drill.drillLoop.clip != null && drill.drillLoop.isPlaying;
        Check(receipt.drillIsVisibleAndAudible, "The planted drill needs its persistent visible model and running positional motor loop.");
        yield return new WaitForSecondsRealtime(.25f);
        receipt.heistStatusVisible = director.heistStatusText != null && director.heistStatusText.gameObject.activeInHierarchy &&
            director.heistStatusText.text.Contains("POLICE IN") && !director.heistStatusText.text.Contains("STANDBY");
        Check(receipt.heistStatusVisible, "The live HUD is missing the drilling and reinforcement countdowns.");
        if (captures) yield return Capture("07b_vault_drilling_and_countdown");

        session.PauseGame();
        yield return null;
        float heldElapsed = drill.ElapsedSeconds;
        float heldWaveRemaining = director.policeWaves.SecondsUntilNextWave;
        int heldWave = director.policeWaves.WaveNumber;
        yield return new WaitForSecondsRealtime(.3f);
        receipt.drillPauses = session.CurrentState == FacilityGameState.Paused && drill.ElapsedSeconds == heldElapsed &&
            director.policeWaves.SecondsUntilNextWave == heldWaveRemaining && director.policeWaves.WaveNumber == heldWave;
        Check(receipt.drillPauses, "Pause advanced the forty-five-second drill or the police reinforcement clock.");
        session.ResumeGame();
        float timeout = Time.realtimeSinceStartup + 53f;
        bool neverOpenedEarly = true;
        while (!drill.IsComplete && Time.realtimeSinceStartup < timeout)
        {
            neverOpenedEarly &= !director.VaultOpen && !director.IsInteractionAvailable(director.cashPickup, out _) &&
                !director.IsInteractionAvailable(director.goldPickup, out _) && drill.vaultDoor.IsLocked;
            yield return null;
        }
        receipt.actualDrillSeconds = drill.ElapsedSeconds;
        receipt.vaultWaitsFortyFiveSeconds = neverOpenedEarly && drill.IsComplete && !drill.IsRunning &&
            Mathf.Abs(drill.ElapsedSeconds - 45f) < .001f && Time.time - startedAt >= 44.98f && Time.time - startedAt < 45.6f;
        Require(receipt.vaultWaitsFortyFiveSeconds, "The vault opened early, failed to open after exactly forty-five simulated seconds, or allowed loot through its lock.");
        yield return new WaitForSecondsRealtime(drill.vaultDoor.travelSeconds + .12f);
        Check(drill.vaultDoor.IsOpen && !drill.vaultDoor.IsLocked && !drill.drillLoop.isPlaying,
            "The completed drill did not physically open its vault door and stop the motor loop.");
        bool cadence = currentPoliceArrivals.Count >= 3 && director.policeWaves.WaveNumber >= 3 && director.policeWaves.TotalPoliceSpawned >= 5;
        for (int i = 0; i < Mathf.Min(3, currentPoliceArrivals.Count); i++)
        {
            cadence &= Mathf.Abs(currentPoliceArrivals[i] - 15f * (i + 1)) < .2f;
            cadence &= currentPoliceTotals[i] > (i > 0 ? currentPoliceTotals[i - 1] : 0);
        }
        receipt.policeArriveEveryFifteenSeconds = cadence;
        Check(cadence, "Police did not physically spawn in waves at fifteen, thirty and forty-five simulated seconds after the alarm.");
        if (captures)
        {
            receipt.policeArrivalSeconds.AddRange(currentPoliceArrivals);
            receipt.policeSpawnTotals.AddRange(currentPoliceTotals);
            yield return Capture("07c_vault_unlocked_after_forty_five_seconds");
        }
    }

    private IEnumerator CollectLoot(bool captures)
    {
        // The two real forty-five-second runs exercise opposite collection orders.
        FacilityStoryInteractable first = captures ? director.cashPickup : director.goldPickup;
        FacilityStoryInteractable second = captures ? director.goldPickup : director.cashPickup;
        SetPose(new Vector3(0f, .05f, 34.5f), first.transform.position);
        yield return null;
        bool collectedFirst = director.TryInteract(first, out string firstReason);
        bool missingLootBlocksExit = collectedFirst && director.HasCash == captures && director.HasGold != captures &&
            first.Consumed && director.Stage == FacilityStoryStage.FindVault && !director.IsInteractionAvailable(director.sedanExit, out _);
        if (captures) receipt.goldRequired = missingLootBlocksExit; else receipt.cashRequired = missingLootBlocksExit;
        Check(missingLootBlocksExit, "Collecting only " + (captures ? "cash" : "gold") + " must keep extraction locked until the other required loot is secured: " + firstReason);
        Aim(second.transform.position);
        Physics.SyncTransforms();
        yield return null;
        bool collectedSecond = director.TryInteract(second, out string secondReason);
        receipt.bothLootUnlockExtraction = collectedSecond && director.HasCash && director.HasGold && director.Stage == FacilityStoryStage.ReturnToSedan &&
            director.cashPickup.Consumed && director.goldPickup.Consumed && director.IsInteractionAvailable(director.sedanExit, out _);
        Check(receipt.bothLootUnlockExtraction, "Collecting both cash and gold did not unlock sedan extraction: " + secondReason);
        if (captures) yield return Capture("08_vault_loot_secured");
    }

    private IEnumerator Extract()
    {
        SetPose(new Vector3(-6f, .05f, -18f), director.sedanExit.transform.position);
        yield return null;
        bool extracted = director.TryInteract(director.sedanExit, out string reason);
        yield return null;
        receipt.extractionCompletesLevel = extracted && director.Stage == FacilityStoryStage.Completed && session.CurrentState == FacilityGameState.LevelComplete &&
            session.completionMenu != null && session.completionMenu.activeInHierarchy;
        receipt.completionFreezesInput = Time.timeScale == 0f && !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed &&
            Cursor.visible && Cursor.lockState == CursorLockMode.None;
        Check(receipt.extractionCompletesLevel, "Returning both cash and gold to the sedan did not complete Level 1: " + reason);
        Check(receipt.completionFreezesInput, "The completion menu did not freeze gameplay and release the cursor.");
        yield return Capture("09_sedan_extraction");
    }

    private IEnumerator VerifySandbox()
    {
        receipt.sandboxScene = SceneManager.GetActiveScene().path;
        receipt.sandboxHasBothGuns = session.CurrentMode == FacilityGameMode.Sandbox && session.CurrentState == FacilityGameState.Playing &&
            inventory.HasPistol && inventory.HasShotgun && inventory.Equip(FacilityWeaponKind.Fists) &&
            inventory.Equip(FacilityWeaponKind.Shotgun) && inventory.Equip(FacilityWeaponKind.Pistol);
        Check(receipt.sandboxHasBothGuns, "Sandbox must offer fists, pistol and shotgun immediately.");
        receipt.sandboxLightmaps = LightmapSettings.lightmaps.Length;
        bool environmentFound = false;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Facility Exploration") environmentFound = true;
        receipt.sandboxRetainsFacilityAndLightmaps = environmentFound && director == null && receipt.sandboxLightmaps > 0;
        Check(receipt.sandboxRetainsFacilityAndLightmaps, "Sandbox did not retain the existing authored facility and baked lighting.");
        session.ToggleSpawnMenu();
        yield return null;
        receipt.sandboxSpawnMenuWorks = session.CurrentState == FacilityGameState.SpawnMenu && session.spawnMenu.activeInHierarchy;
        Check(receipt.sandboxSpawnMenuWorks, "The sandbox Tab character browser is unavailable.");
        session.observerSpawnButton.onClick.Invoke();
        yield return null;
        bool placed = session.spawner.TryPlaceSelected(new Vector3(-2f, .05f, -2f), out string reason);
        GameObject observer = session.spawner.LastSpawned;
        FacilityKnifeGuard guard = observer != null ? observer.GetComponent<FacilityKnifeGuard>() : null;
        receipt.sandboxObserverHasKnife = placed && guard != null && guard.knifeProp != null && !guard.Alerted;
        Check(receipt.sandboxObserverHasKnife, "The sandbox observer was not placed with a passive, functional knife guard: " + reason);
        session.CancelPlacement();
        inventory.Equip(FacilityWeaponKind.Pistol);
        SetPose(new Vector3(-2f, .05f, 2f), observer != null ? observer.transform.position + Vector3.up * 1.3f : new Vector3(0f, 1.3f, -2f));
        yield return new WaitForSecondsRealtime(.2f);
        yield return Capture("10_sandbox_pistol_and_observer");
        yield return NaturalKnifeEncounter(guard);
    }

    private IEnumerator NaturalKnifeEncounter(FacilityKnifeGuard guard)
    {
        Require(guard != null && guard.actor != null && !guard.actor.IsDead && session.health.CurrentHealth == 3,
            "The natural knife encounter needs the newly spawned observer and an untouched sandbox player.");
        guard.actor.enabled = true;
        guard.enabled = true;
        SetPose(new Vector3(-2f, .05f, 2f), guard.actor.AimPoint);
        guard.Activate();
        float firstHitDeadline = Time.realtimeSinceStartup + 6f;
        while (session.health.CurrentHealth == 3 && Time.realtimeSinceStartup < firstHitDeadline) yield return null;
        receipt.knifeDamagesPlayer = session.health.CurrentHealth == 2 && guard.SuccessfulAttacks == 1 &&
            session.health.LastAttacker == guard.transform;
        Check(receipt.knifeDamagesPlayer, "The active knife observer did not naturally reach and stab the player once within six seconds.");
        bool firstFlash = session.health.RedFlash > .7f;
        // Session UI updates before ordinary actor behavior; allow its next frame to display the new damage event.
        yield return null;
        receipt.redFlashOnStab = firstFlash && session.damageOverlay != null && session.damageOverlay.color.a > .2f;
        Check(receipt.redFlashOnStab, "The first natural knife stab did not show the red screen flash.");
        yield return Capture("11_knife_hit_red_flash");
        session.PauseGame();
        yield return null;
        int heldHealth = session.health.CurrentHealth, heldAttacks = guard.SuccessfulAttacks;
        Vector3 heldGuard = guard.transform.position;
        yield return new WaitForSecondsRealtime(.25f);
        Check(session.CurrentState == FacilityGameState.Paused && Time.timeScale == 0f &&
            session.health.CurrentHealth == heldHealth && guard.SuccessfulAttacks == heldAttacks &&
            Vector3.Distance(heldGuard, guard.transform.position) < .005f,
            "Pausing did not freeze the active knife attacker's movement, damage and attack count.");
        session.ResumeGame();
        float deathDeadline = Time.realtimeSinceStartup + 10f;
        while (!session.health.IsDead && Time.realtimeSinceStartup < deathDeadline) yield return null;
        receipt.knifeAttacks = guard.SuccessfulAttacks;
        receipt.threeKnifeHitsKillPlayer = session.health.IsDead && session.health.CurrentHealth == 0 && session.health.HitsTaken == 3 &&
            guard.SuccessfulAttacks == 3 && session.CurrentState == FacilityGameState.Dead && !FacilityGameSession.GameplayWeaponAllowed;
        Check(receipt.threeKnifeHitsKillPlayer, "Three natural knife attacks did not kill the player and enter the death menu.");
        yield return Capture("12_three_knife_hits_death");
    }

    private void AcquireSession()
    {
        session = FindFirstObjectByType<FacilityGameSession>();
        Require(session != null && session.player != null && session.health != null && session.inventory != null,
            "The active scene lacks its authored session, player, health or weapon inventory.");
        player = session.player; inventory = session.inventory;
        director = session.storyDirector;
        motor = player.GetComponent<CharacterController>();
        eye = player.GetComponentInChildren<Camera>();
        Require(motor != null && eye != null && inventory.fists != null && inventory.pistol != null && inventory.shotgun != null,
            "The preserved player, camera or one of the three authored weapon components is missing.");
    }

    private IEnumerator WaitForScene(FacilityGameSession previous)
    {
        float until = Time.realtimeSinceStartup + 12f;
        FacilityGameSession candidate;
        do { yield return null; candidate = FindFirstObjectByType<FacilityGameSession>(); }
        while ((candidate == null || candidate == previous) && Time.realtimeSinceStartup < until);
        Require(candidate != null && candidate != previous, "Scene transition did not create a fresh session within twelve seconds.");
        yield return new WaitForSecondsRealtime(.3f);
        AcquireSession();
    }

    private void SetPose(Vector3 feet, Vector3 aim)
    {
        player.enabled = false;
        bool wasEnabled = motor.enabled;
        motor.enabled = false;
        player.transform.position = feet;
        motor.enabled = wasEnabled;
        Aim(aim);
        Physics.SyncTransforms();
    }

    private void Aim(Vector3 point)
    {
        player.ResetRecoil();
        Vector3 facing = point - eye.transform.position;
        Vector3 horizontal = facing; horizontal.y = 0f;
        if (horizontal.sqrMagnitude > .001f) player.transform.rotation = Quaternion.LookRotation(horizontal, Vector3.up);
        if (facing.sqrMagnitude > .001f) eye.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
    }

    private Vector3 FindInteractionPose(FacilityStoryInteractable target, Vector3 aim)
    {
        for (int radiusIndex = 0; radiusIndex < 5; radiusIndex++)
        {
            float radius = 1f + radiusIndex * .3f;
            for (int angleIndex = 0; angleIndex < 48; angleIndex++)
            {
                float angle = angleIndex * Mathf.PI / 24f;
                Vector3 candidate = new Vector3(aim.x + Mathf.Sin(angle) * radius, .05f, aim.z + Mathf.Cos(angle) * radius);
                if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .45f, NavMesh.AllAreas)) continue;
                candidate = floor.position + Vector3.up * .05f;
                int overlapCount = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * .35f, candidate + Vector3.up * 1.5f,
                    .32f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool clear = overlapCount < overlaps.Length;
                for (int i = 0; i < overlapCount && clear; i++)
                    if (overlaps[i] != null && !overlaps[i].transform.IsChildOf(player.transform)) clear = false;
                if (!clear) continue;
                Vector3 origin = candidate + Vector3.up * 1.65f;
                Vector3 offset = aim - origin;
                float distance = offset.magnitude;
                if (distance > director.interactionRange - .05f) continue;
                int count = Physics.RaycastNonAlloc(origin, offset.normalized, hits, distance + .08f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                if (count == hits.Length) continue;
                float nearest = float.PositiveInfinity;
                Collider first = null;
                for (int i = 0; i < count; i++)
                {
                    Collider hit = hits[i].collider;
                    if (hit == null || hit is CharacterController || hit.transform.IsChildOf(player.transform)) continue;
                    if (hit.isTrigger && hit.GetComponentInParent<FacilityStoryInteractable>() == null) continue;
                    if (hits[i].distance >= nearest) continue;
                    nearest = hits[i].distance;
                    first = hit;
                }
                if (first == null) continue;
                FacilityStoryInteractable resolved = first.GetComponentInParent<FacilityStoryInteractable>();
                if (resolved == null)
                {
                    FacilityDoor door = first.GetComponentInParent<FacilityDoor>();
                    if (door != null) resolved = door.interaction;
                }
                if (resolved == target) return candidate;
            }
        }
        throw new InvalidOperationException("No unobstructed reachable interaction pose for " + target.name + " around " + aim + ".");
    }

    private Vector3 FindFiringPose(FacilityHumanoid target, float radius)
    {
        for (int angleIndex = 0; angleIndex < 24; angleIndex++)
        {
            float angle = angleIndex * Mathf.PI / 12f;
            Vector3 candidate = target.transform.position + new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * radius;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .15f, out NavMeshHit floor, .45f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * .35f, candidate + Vector3.up * 1.5f,
                .32f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool clear = overlapCount < overlaps.Length;
            for (int i = 0; i < overlapCount && clear; i++)
            {
                Collider item = overlaps[i];
                if (item != null && !item.transform.IsChildOf(player.transform)) clear = false;
            }
            if (!clear) continue;
            Vector3 from = candidate + Vector3.up * 1.65f;
            Vector3 direction = target.AimPoint - from;
            int count = Physics.RaycastNonAlloc(from, direction.normalized, hits, direction.magnitude + .1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider first = null;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && !(hits[i].collider is CharacterController) && !hits[i].collider.transform.IsChildOf(player.transform) && hits[i].distance < nearest)
                { nearest = hits[i].distance; first = hits[i].collider; }
            if (first != null && first.GetComponentInParent<FacilityHumanoid>() == target) return candidate;
        }
        throw new InvalidOperationException("No supported, unobstructed pistol pose around " + target.name + ".");
    }

    private static bool VisibleLabel(GameObject root, string expected)
    {
        foreach (Text label in root.GetComponentsInChildren<Text>())
            if (label.gameObject.activeInHierarchy && string.Equals(label.text.Trim(), expected, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        Require(screenshot != null, "The standalone GUI screenshot returned no texture.");
        try { File.WriteAllBytes(Path.Combine(output, name + ".png"), screenshot.EncodeToPNG()); receipt.screenshotCount++; }
        finally { Destroy(screenshot); }
    }

    private void Check(bool condition, string failure) { if (!condition) receipt.failures.Add(failure); }
    private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
    private void OnLog(string message, string trace, LogType kind)
    {
        if (collectingErrors && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace);
    }
}
