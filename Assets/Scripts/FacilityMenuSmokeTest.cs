using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>Opt-in UI/gameplay integration check: -facilityMenuTest "output directory".</summary>
/// <remarks>No runner is created in an ordinary game or editor session.</remarks>
public sealed class FacilityMenuSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class Receipt
    {
        public bool success, startsInMainMenu, menuBlocksMovementAndFire, startButtonWorks,
            hudShowsThreeHp, gameplayAudioStarts, spawnMenuUnlocksCursor, spawnMenuBlocksMovementAndFire,
            safePlacementWorks, unsafePlacementRejected, observerIsPassive,
            zombiePursuesAroundObstacle, zombieAttackDamagesOnce, attackCooldownWorks,
            redFlashVisible, thirdAttackKillsPlayer, deathBlocksMovementAndFire,
            deathRestartResetsState, spawnedZombieTakesShotgunDamage, pauseFreezesSimulation, resumeRestoresGameplay,
            pauseRestartResetsState, quitButtonDispatches;
        public string utc, unity, gpu, scene;
        public string coverage = "Authored menu buttons and gameplay state, safe runtime NPC placement, passive observer, hostile zombie pursuit around a temporary obstruction, natural melee attacks and three-hit player death, shotgun head damage on a separately spawned zombie, restart cleanup, pause/resume, quit dispatch, and rendered GUI captures. Explicit CLI only; the authored scene is never saved by this check.";
        public int width, height, finalHealth, zombieAttacks, spawnedActors;
        public float zombieTravelMetres, pursuitSeconds;
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private string output;
    private bool collectingErrors, interceptQuit;
    private FacilityGameSession session;
    private RetroFPSController player;
    private CharacterController playerMotor;
    private FacilityShotgun shotgun;
    private Camera eye;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(arguments, "-facilityMenuTest");
        if (flag < 0) return;
        if (flag + 1 >= arguments.Length || arguments[flag + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityMenuTest requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Facility menu diagnostics (explicit CLI only)").AddComponent<FacilityMenuSmokeTest>();
        runner.output = arguments[flag + 1];
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        Application.logMessageReceived += OnLog;
        Application.wantsToQuit += OnWantsToQuit;
        collectingErrors = true;
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
        interceptQuit = false;
        Application.logMessageReceived -= OnLog;
        Application.wantsToQuit -= OnWantsToQuit;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        try
        {
            receipt.success = receipt.failures.Count == 0;
            File.WriteAllText(Path.Combine(output, "menu-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Facility menu integration check " + (receipt.success ? "passed: " : "failed: ") + output);
        }
        catch (Exception exception) { Debug.LogException(exception); Application.Quit(2); yield break; }
        Application.Quit(receipt.success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(.6f);
        receipt.utc = DateTime.UtcNow.ToString("O");
        receipt.unity = Application.unityVersion;
        receipt.gpu = SystemInfo.graphicsDeviceName;
        receipt.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        receipt.width = Screen.width; receipt.height = Screen.height;
        Check(receipt.width == 1280 && receipt.height == 720, "Window did not reach 1280 x 720.");
        AcquireSession();
        receipt.startsInMainMenu = session.CurrentState == FacilityGameState.MainMenu && session.mainMenu.activeInHierarchy &&
            !session.hud.activeInHierarchy && VisibleLabel(session.mainMenu, "rob the bank");
        Check(receipt.startsInMainMenu, "The startup scene must show the game title, Start and Quit before playing.");
        Vector3 heldPosition = player.transform.position;
        int oldShots = shotgun.ShotsFired;
        bool menuFireAccepted = shotgun.TryFire();
        yield return new WaitForSecondsRealtime(.2f);
        receipt.menuBlocksMovementAndFire = !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed &&
            Time.timeScale == 0f && !menuFireAccepted &&
            shotgun.ShotsFired == oldShots && Vector3.Distance(heldPosition, player.transform.position) < .005f;
        Check(receipt.menuBlocksMovementAndFire, "The start menu must freeze movement, NPC simulation and weapon input.");
        Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "The startup menu cursor is not available.");
        Check(session.startButton.gameObject.activeInHierarchy && session.mainQuitButton.gameObject.activeInHierarchy,
            "Start and Quit buttons must both be visible.");
        yield return Capture("01_start_menu");

        session.startButton.onClick.Invoke();
        Check(session.CurrentState == FacilityGameState.ModeSelect && session.storyButton.gameObject.activeInHierarchy && session.sandboxButton.gameObject.activeInHierarchy,
            "Start must open the Story / Sandbox selector.");
        session.sandboxButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        receipt.startButtonWorks = session.CurrentState == FacilityGameState.Playing && !session.mainMenu.activeInHierarchy &&
            session.hud.activeInHierarchy && player.enabled && Time.timeScale > 0f;
        Check(receipt.startButtonWorks, "Start / Sandbox did not enter controllable gameplay.");
        FacilityAmbientEffects ambience = FindFirstObjectByType<FacilityAmbientEffects>();
        receipt.gameplayAudioStarts = ambience != null && !AudioListener.pause && ambience.loopingSources.Length == 4;
        if (ambience != null)
            foreach (AudioSource source in ambience.loopingSources) receipt.gameplayAudioStarts &= source != null && source.isPlaying;
        Check(receipt.gameplayAudioStarts, "The four ambient loops did not start after entering gameplay.");
        receipt.hudShowsThreeHp = session.health.CurrentHealth == 3 && session.health.MaxHealth == 3 &&
            session.hpText.gameObject.activeInHierarchy && session.hpText.text.Contains("3") && session.healthFill.fillAmount > .99f;
        Check(receipt.hudShowsThreeHp, "The small health HUD did not start at three HP with a full bar.");
        yield return Capture("02_gameplay_hud");

        SetPlayerPose(new Vector3(-2f, .06f, 2f), 90f);
        session.ToggleSpawnMenu();
        yield return null;
        receipt.spawnMenuUnlocksCursor = session.CurrentState == FacilityGameState.SpawnMenu &&
            session.spawnMenu.activeInHierarchy && Cursor.lockState == CursorLockMode.None && Cursor.visible &&
            session.observerSpawnButton.gameObject.activeInHierarchy && session.zombieSpawnButton.gameObject.activeInHierarchy;
        Check(receipt.spawnMenuUnlocksCursor, "Tab's spawn menu state did not expose both character choices and a usable cursor.");
        heldPosition = player.transform.position; oldShots = shotgun.ShotsFired;
        bool spawnMenuShot = shotgun.TryFire();
        yield return new WaitForSecondsRealtime(.15f);
        receipt.spawnMenuBlocksMovementAndFire = !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed &&
            !spawnMenuShot && shotgun.ShotsFired == oldShots &&
            Vector3.Distance(heldPosition, player.transform.position) < .005f;
        Check(receipt.spawnMenuBlocksMovementAndFire, "Browsing the spawn menu allowed movement or firing.");
        yield return Capture("03_spawn_browser");

        session.observerSpawnButton.onClick.Invoke();
        yield return null;
        Check(session.CurrentState == FacilityGameState.Placing && session.spawner.HasSelection &&
            session.spawner.Selection == FacilitySpawnKind.Observer, "The Guy card did not arm an observer placement.");
        // Hold a downward view solely for a deterministic preview capture. The game continues to use
        // its unmodified player controller; cancelling placement reapplies its normal enabled state.
        player.enabled = false;
        player.ResetRecoil();
        eye.transform.rotation = Quaternion.LookRotation(new Vector3(-2f, .05f, -2f) - eye.transform.position, Vector3.up);
        yield return new WaitForSecondsRealtime(.1f);
        bool floorHit = session.spawner.TryGetPlacement(new Ray(new Vector3(-2f, 3f, -2f), Vector3.down), out Vector3 observerFeet, out string placementReason);
        Check(floorHit, "An unobstructed arrival floor could not produce a placement: " + placementReason);
        Check(session.placementHud.activeInHierarchy && session.spawner.PlacementValid,
            "Selecting a character did not show a valid floor-supported placement preview.");
        yield return Capture("04_observer_placement_preview");
        bool wallRejected = !session.spawner.TryPlaceSelected(new Vector3(5f, .04f, 0f), out string wallReason);
        bool voidRejected = !session.spawner.TryPlaceSelected(new Vector3(100f, 0f, 100f), out string voidReason);
        receipt.unsafePlacementRejected = wallRejected && voidRejected && session.spawner.RuntimeActorCount == 0;
        Check(receipt.unsafePlacementRejected, "Placement accepted an east-wall capsule or a position outside the facility. " + wallReason + "; " + voidReason);
        receipt.safePlacementWorks = floorHit && session.spawner.TryPlaceSelected(observerFeet, out placementReason) && session.spawner.RuntimeActorCount == 1;
        Check(receipt.safePlacementWorks, "A valid observer placement failed: " + placementReason);
        GameObject observerObject = session.spawner.LastSpawned;
        FacilityHumanoid observer = observerObject != null ? observerObject.GetComponent<FacilityHumanoid>() : null;
        session.CancelPlacement();
        player.enabled = true;
        SetPlayerPose(new Vector3(-2f, .06f, 2f), 180f);
        Vector3 observerStart = observer != null ? observer.transform.position : Vector3.zero;
        yield return new WaitForSecondsRealtime(.5f);
        Vector3 observerTravel = observer != null ? observer.transform.position - observerStart : Vector3.one * 100f;
        observerTravel.y = 0f;
        receipt.observerIsPassive = observer != null && observer.GetComponent<FacilityZombie>() == null && !observer.IsHostile &&
            observer.HitsTaken == 0 && !observer.IsDead && observerTravel.magnitude < .08f;
        Check(receipt.observerIsPassive, "The Guy spawn moved toward or attacked an untouched player.");
        yield return Capture("05_spawned_observer");

        SetPlayerPose(new Vector3(-2f, .06f, 2f), 90f);
        session.ToggleSpawnMenu();
        session.zombieSpawnButton.onClick.Invoke();
        bool zombieFloor = session.spawner.TryGetPlacement(new Ray(new Vector3(2f, 3f, 2f), Vector3.down), out Vector3 zombieFeet, out placementReason);
        bool zombiePlaced = zombieFloor && session.spawner.TryPlaceSelected(zombieFeet, out placementReason);
        Check(zombiePlaced, "A valid zombie placement failed: " + placementReason);
        FacilityZombie zombie = session.spawner.LastSpawned != null ? session.spawner.LastSpawned.GetComponent<FacilityZombie>() : null;
        if (zombie == null) throw new InvalidOperationException("Zombie card did not create a hostile zombie component.");
        Check(zombie.GetComponent<FacilityHumanoid>().IsHostile && zombie.targetHealth == session.health,
            "The spawned zombie did not bind to the current player health.");
        receipt.spawnedActors = session.spawner.RuntimeActorCount;
        Check(receipt.spawnedActors == 2, "Runtime spawn count did not include exactly the observer and zombie.");

        // Temporary test-only solid and carved navigation obstruction. It is destroyed by the restart.
        GameObject obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstruction.name = "Menu diagnostic navigation obstruction (runtime only)";
        obstruction.transform.position = new Vector3(0f, .8f, 2f);
        obstruction.transform.localScale = new Vector3(1.2f, 1.6f, 2f);
        obstruction.GetComponent<Renderer>().sharedMaterial = session.spawner.observerPrefab.GetComponentInChildren<Renderer>(true).sharedMaterial;
        var obstacle = obstruction.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        Physics.SyncTransforms();
        session.CancelPlacement();
        yield return new WaitForSecondsRealtime(.8f);
        Vector3 previousZombie = zombie.transform.position;
        float started = Time.realtimeSinceStartup, maximumSideStep = 0f;
        int initialHealth = session.health.CurrentHealth;
        while (session.health.CurrentHealth == initialHealth && Time.realtimeSinceStartup - started < 22f)
        {
            Vector3 current = zombie.transform.position;
            receipt.zombieTravelMetres += Vector3.Distance(previousZombie, current);
            maximumSideStep = Mathf.Max(maximumSideStep, Mathf.Abs(current.z - 2f));
            previousZombie = current;
            yield return null;
        }
        receipt.pursuitSeconds = Time.realtimeSinceStartup - started;
        receipt.zombiePursuesAroundObstacle = session.health.CurrentHealth == 2 && receipt.zombieTravelMetres > 2.5f && maximumSideStep > 1f;
        Check(receipt.zombiePursuesAroundObstacle, "The zombie did not route around the carved solid obstacle and reach the player within 22 seconds. " +
            "Position=" + zombie.transform.position + "; status=" + zombie.NavigationStatus + "; reason=" + zombie.PlanFailureReason +
            "; corner=" + zombie.NextCorner + "/" + zombie.CornerCount + "; destination=" + zombie.CurrentDestination +
            "; steering=" + zombie.Steering + "; blocker=" + zombie.BlockingCollider + "; motor=" + zombie.MotorCollisionFlags);
        receipt.zombieAttackDamagesOnce = session.health.CurrentHealth == 2 && zombie.SuccessfulAttacks == 1 && session.health.LastAttacker == zombie.transform;
        Check(receipt.zombieAttackDamagesOnce, "The first zombie attack must remove exactly one of the three HP.");
        receipt.attackCooldownWorks = receipt.zombieAttackDamagesOnce && !session.health.TryTakeHit(zombie.transform) && session.health.CurrentHealth == 2;
        Check(receipt.attackCooldownWorks, "The player accepted an additional hit inside the shared invulnerability window.");
        yield return null;
        receipt.redFlashVisible = receipt.zombieAttackDamagesOnce && session.health.RedFlash > .2f && session.damageOverlay.color.a > .01f;
        Check(receipt.redFlashVisible, "A zombie hit did not produce a visible red damage flash.");
        yield return Capture("06_zombie_attack_red_flash");
        float lastHealthAt = Time.realtimeSinceStartup;
        while (!session.health.IsDead && Time.realtimeSinceStartup - lastHealthAt < 6f) yield return null;
        receipt.zombieAttacks = zombie.SuccessfulAttacks;
        receipt.thirdAttackKillsPlayer = session.health.IsDead && session.health.CurrentHealth == 0 &&
            session.health.HitsTaken == 3 && zombie.SuccessfulAttacks == 3 && session.CurrentState == FacilityGameState.Dead && session.deathMenu.activeInHierarchy;
        Check(receipt.thirdAttackKillsPlayer, "Three natural zombie attacks did not enter the player death menu at zero HP.");
        oldShots = shotgun.ShotsFired;
        bool deathShot = shotgun.TryFire();
        receipt.deathBlocksMovementAndFire = !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed &&
            Time.timeScale == 0f && !deathShot && oldShots == shotgun.ShotsFired &&
            Cursor.lockState == CursorLockMode.None && Cursor.visible;
        Check(receipt.deathBlocksMovementAndFire, "The death menu did not freeze gameplay and expose its buttons.");
        yield return Capture("07_death_menu");

        FacilityGameSession previous = session;
        session.deathRestartButton.onClick.Invoke();
        yield return WaitForRestart(previous);
        receipt.deathRestartResetsState = FreshGameplay();
        Check(receipt.deathRestartResetsState, "Restart from death did not restore three HP and remove runtime-spawned characters.");
        yield return Capture("08_restarted_gameplay");

        // Exercise the complete saved-prefab spawn -> live player binding -> actual weapon path,
        // independently of the earlier zombie's melee-only scenario.
        SetPlayerPose(new Vector3(-2f, .06f, 2f), 90f);
        session.ToggleSpawnMenu();
        session.zombieSpawnButton.onClick.Invoke();
        bool shotZombieFloor = session.spawner.TryGetPlacement(new Ray(new Vector3(2f, 3f, 2f), Vector3.down),
            out Vector3 shotZombieFeet, out string shotPlacementReason);
        bool shotZombiePlaced = shotZombieFloor && session.spawner.TryPlaceSelected(shotZombieFeet, out shotPlacementReason);
        FacilityHumanoid shotZombie = shotZombiePlaced && session.spawner.LastSpawned != null
            ? session.spawner.LastSpawned.GetComponent<FacilityHumanoid>() : null;
        Check(shotZombie != null && shotZombie.GetComponent<FacilityZombie>() != null,
            "The separate shotgun scenario could not place a zombie: " + shotPlacementReason);
        if (shotZombie == null) throw new InvalidOperationException("A separately spawned zombie is required for the shotgun integration check.");
        session.CancelPlacement();
        yield return new WaitForSecondsRealtime(.2f);
        player.enabled = false;
        player.ResetRecoil();
        eye.transform.rotation = Quaternion.LookRotation(shotZombie.head.position - eye.transform.position, Vector3.up);
        Physics.SyncTransforms();
        bool zombieShotFired = shotgun.TryFire();
        yield return new WaitForSecondsRealtime(.25f);
        receipt.spawnedZombieTakesShotgunDamage = zombieShotFired && shotZombie.IsDead && shotZombie.HeadExploded &&
            shotZombie.gore != null && shotZombie.gore.HeadBurstCount == 1 && session.health.CurrentHealth == 3 &&
            session.CurrentState == FacilityGameState.Playing;
        Check(receipt.spawnedZombieTakesShotgunDamage,
            "A head-aimed real shotgun discharge did not kill the spawned zombie with one head burst while preserving three player HP.");
        yield return Capture("10_spawned_zombie_shotgun");
        player.enabled = true;

        session.PauseGame();
        yield return null;
        float pausedTime = Time.time;
        heldPosition = player.transform.position; oldShots = shotgun.ShotsFired;
        bool pauseShot = shotgun.TryFire();
        yield return new WaitForSecondsRealtime(.2f);
        receipt.pauseFreezesSimulation = session.CurrentState == FacilityGameState.Paused && session.pauseMenu.activeInHierarchy &&
            !FacilityGameSession.GameplayInputAllowed && !FacilityGameSession.GameplayWeaponAllowed &&
            Time.timeScale == 0f && Time.time == pausedTime && !pauseShot && shotgun.ShotsFired == oldShots &&
            Vector3.Distance(heldPosition, player.transform.position) < .005f;
        Check(receipt.pauseFreezesSimulation, "Pause did not freeze gameplay simulation and weapon input.");
        yield return Capture("09_pause_menu");
        session.resumeButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.2f);
        receipt.resumeRestoresGameplay = session.CurrentState == FacilityGameState.Playing && !session.pauseMenu.activeInHierarchy &&
            player.enabled && Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;
        Check(receipt.resumeRestoresGameplay, "Resume did not restore gameplay and the locked mouse.");
        session.PauseGame();
        previous = session;
        session.restartButton.onClick.Invoke();
        yield return WaitForRestart(previous);
        receipt.pauseRestartResetsState = FreshGameplay();
        Check(receipt.pauseRestartResetsState, "Restart from pause did not load a fresh playable state.");
        receipt.finalHealth = session.health.CurrentHealth;
        session.PauseGame();
        interceptQuit = true;
        session.pauseQuitButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.2f);
        Check(receipt.quitButtonDispatches, "The Quit button did not dispatch an application quit request.");
        interceptQuit = false;
    }

    private void AcquireSession()
    {
        session = FindFirstObjectByType<FacilityGameSession>();
        player = FindFirstObjectByType<RetroFPSController>();
        shotgun = FindFirstObjectByType<FacilityShotgun>();
        if (session == null || player == null || shotgun == null || session.spawner == null || session.health == null)
            throw new InvalidOperationException("The authored session, spawner, player, health and shotgun must all exist.");
        playerMotor = player.GetComponent<CharacterController>();
        eye = player.GetComponentInChildren<Camera>();
        if (playerMotor == null || eye == null) throw new InvalidOperationException("The original player capsule/camera are missing.");
    }

    private void SetPlayerPose(Vector3 feet, float yaw)
    {
        bool wasEnabled = playerMotor.enabled;
        playerMotor.enabled = false;
        player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
        player.ResetRecoil();
        eye.transform.localRotation = Quaternion.identity;
        playerMotor.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    private IEnumerator WaitForRestart(FacilityGameSession previous)
    {
        float timeout = Time.realtimeSinceStartup + 8f;
        FacilityGameSession candidate;
        do
        {
            yield return null;
            candidate = FindFirstObjectByType<FacilityGameSession>();
        } while ((candidate == null || candidate == previous) && Time.realtimeSinceStartup < timeout);
        if (candidate == null || candidate == previous) throw new InvalidOperationException("Restart did not reload the active scene within eight seconds.");
        yield return new WaitForSecondsRealtime(.3f);
        AcquireSession();
    }

    private bool FreshGameplay()
    {
        return session.CurrentState == FacilityGameState.Playing && session.health.CurrentHealth == 3 && !session.health.IsDead &&
            session.spawner.RuntimeActorCount == 0 && player.enabled && Time.timeScale > 0f &&
            FindObjectsByType<FacilityZombie>(FindObjectsSortMode.None).Length == 0;
    }

    private static bool VisibleLabel(GameObject root, string expected)
    {
        foreach (Text label in root.GetComponentsInChildren<Text>())
            if (label.gameObject.activeInHierarchy && string.Equals(label.text.Trim(), expected, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private IEnumerator Capture(string filename)
    {
        // Screen-space overlay canvases are rendered after the camera. A camera render request
        // omits the actual UI, so capture the completed standalone frame instead.
        yield return new WaitForEndOfFrame();
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        if (image == null) throw new InvalidOperationException("Standalone screen capture returned no image.");
        try { File.WriteAllBytes(Path.Combine(output, filename + ".png"), image.EncodeToPNG()); }
        finally { Destroy(image); }
    }

    private void Check(bool condition, string failure)
    { if (!condition) receipt.failures.Add(failure); }

    private bool OnWantsToQuit()
    {
        if (!interceptQuit) return true;
        receipt.quitButtonDispatches = true;
        return false;
    }

    private void OnLog(string message, string trace, LogType kind)
    {
        if (collectingErrors && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert))
            receipt.failures.Add(message + "\n" + trace);
    }
}
