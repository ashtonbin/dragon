using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FacilityGameState { MainMenu, Playing, SpawnMenu, Placing, Paused, Dead, ModeSelect, LevelComplete }
public enum FacilityGameMode { None, Sandbox, Story }

/// <summary>Owns menu state, cursor ownership and gameplay input, including the modal account shop.</summary>
[DefaultExecutionOrder(-400), DisallowMultipleComponent]
public sealed class FacilityGameSession : MonoBehaviour
{
    public static FacilityGameSession Instance { get; private set; }
    public static bool GameplayInputAllowed => Instance == null ||
        (!Instance.VehicleInputLocked && (Instance.shop == null || !Instance.shop.IsOpen) && (Instance.CurrentState == FacilityGameState.Playing || Instance.CurrentState == FacilityGameState.Placing));
    public static bool GameplayWeaponAllowed => Instance == null ||
        (!Instance.VehicleInputLocked && (Instance.shop == null || !Instance.shop.IsOpen) && Instance.CurrentState == FacilityGameState.Playing && Time.unscaledTime >= Instance.weaponReadyAt);
    public static bool SimulationPaused => Instance != null && ((Instance.shop != null && Instance.shop.IsOpen) || (Instance.CurrentState != FacilityGameState.Playing &&
        Instance.CurrentState != FacilityGameState.SpawnMenu && Instance.CurrentState != FacilityGameState.Placing));

    public RetroFPSController player;
    public FacilityPlayerHealth health;
    public FacilitySpawnController spawner;
    public FacilityWeaponInventory inventory;
    public FacilityStoryDirector storyDirector;
    public FacilityShop shop;
    public string sandboxSceneName = "FacilityPrototype";
    public string[] storySceneNames = { "StoryLevel01" };
    public Canvas canvas;
    public GameObject mainMenu, pauseMenu, deathMenu, spawnMenu, hud, placementHud;
    public GameObject modeMenu, completionMenu, objectiveHud;
    public Text hpText, spawnStatusText, spawnSelectionText, placementText;
    public Image healthFill, damageOverlay;
    public Text weaponNameText, completionTitleText, completionBodyText, controlHintText;
    public Button startButton, mainQuitButton, resumeButton, restartButton, pauseQuitButton,
        deathRestartButton, deathQuitButton, observerSpawnButton, zombieSpawnButton;
    public Button storyButton, sandboxButton, modeBackButton, nextLevelButton, returnMainButton, completionQuitButton;
    public Button eliteSwatSpawnButton;
    public FacilityGameState CurrentState { get; private set; }
    public FacilityGameMode CurrentMode { get; private set; }
    public int StoryLevelIndex { get; private set; }
    public bool VehicleInputLocked { get; private set; }

    private static bool enterRequestedGame;
    private static FacilityGameMode requestedMode;
    private static int requestedStoryLevel;
    private float weaponReadyAt;
    private int lastHealth = -1, lastActorCount = -1;
    private string lastPlacementHint;
    private Transform weaponModel;
    private bool reloading;
    private string lastWeaponName;
    private float targetHealthFill = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (player == null) player = FindFirstObjectByType<RetroFPSController>();
        if (health == null && player != null) health = player.GetComponent<FacilityPlayerHealth>();
        if (spawner == null) spawner = GetComponent<FacilitySpawnController>();
        if (inventory == null && player != null) inventory = player.GetComponent<FacilityWeaponInventory>();
        if (storyDirector == null) storyDirector = FindFirstObjectByType<FacilityStoryDirector>();
        RetroViewmodel motion = player != null ? player.GetComponentInChildren<RetroViewmodel>() : null;
        weaponModel = motion != null ? motion.authoredModel : null;
        if (health != null) { health.Damaged += OnDamage; health.Died += OnDeath; }
        Bind(startButton, StartGame); Bind(mainQuitButton, QuitGame);
        Bind(resumeButton, ResumeGame); Bind(restartButton, RestartGame); Bind(pauseQuitButton, QuitGame);
        Bind(deathRestartButton, RestartGame); Bind(deathQuitButton, QuitGame);
        Bind(observerSpawnButton, () => SelectCharacter(FacilitySpawnKind.Observer));
        Bind(zombieSpawnButton, () => SelectCharacter(FacilitySpawnKind.Zombie));
        Bind(eliteSwatSpawnButton, () => SelectCharacter(FacilitySpawnKind.EliteSwat));
        Bind(storyButton, StartStory); Bind(sandboxButton, StartSandbox);
        Bind(modeBackButton, BackToTitle); Bind(nextLevelButton, NextStoryLevel); Bind(returnMainButton, ReturnToMainMenu);
        Bind(completionQuitButton, QuitGame);
        if (shop == null) shop = GetComponent<FacilityShop>();
        if (shop == null) shop = gameObject.AddComponent<FacilityShop>();
        shop.Bind(this);
        bool direct = enterRequestedGame || IsLegacyDiagnostic();
        FacilityGameMode mode = requestedMode == FacilityGameMode.None ? FacilityGameMode.Sandbox : requestedMode;
        int level = requestedStoryLevel;
        enterRequestedGame = false; requestedMode = FacilityGameMode.None;
        if (direct) BeginMode(mode, level);
        else
        {
            CurrentMode = FacilityGameMode.None;
            if (inventory != null) inventory.ConfigureLoadout(false);
            ApplyState(FacilityGameState.MainMenu);
        }
        RefreshHealth();
        if (healthFill != null) healthFill.fillAmount = targetHealthFill;
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    { if (button != null) button.onClick.AddListener(action); }

    private void Start()
    {
        if (CurrentState == FacilityGameState.MainMenu && EventSystem.current != null && startButton != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private static bool IsLegacyDiagnostic()
    {
        string[] args = Environment.GetCommandLineArgs();
        return Array.IndexOf(args, "-facilitySmokeTest") >= 0 || Array.IndexOf(args, "-facilityImpactTest") >= 0 || Array.IndexOf(args, "-facilityCombatTest") >= 0;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && !reloading)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (shop != null && shop.IsOpen) shop.Hide();
                else if (CurrentState == FacilityGameState.SpawnMenu || CurrentState == FacilityGameState.Placing) CancelPlacement();
                else if (CurrentState == FacilityGameState.Playing) PauseGame();
                else if (CurrentState == FacilityGameState.Paused) ResumeGame();
                else if (CurrentState == FacilityGameState.ModeSelect) BackToTitle();
            }
            else if (keyboard.tabKey.wasPressedThisFrame) ToggleSpawnMenu();
        }
        RefreshHealth();
        if (healthFill != null)
            healthFill.fillAmount = Mathf.MoveTowards(healthFill.fillAmount, targetHealthFill, Time.unscaledDeltaTime * 1.5f);
        if (inventory != null && weaponNameText != null && inventory.CurrentName != lastWeaponName)
        {
            lastWeaponName = inventory.CurrentName;
            weaponNameText.text = lastWeaponName + (inventory.HasPistol ? "   /   2  PISTOL" : "") + (inventory.HasShotgun ? "   3  SHOTGUN" : "") + (inventory.HasAK47 ? "   4  AK-47" : "");
        }
        if (damageOverlay != null && health != null)
            damageOverlay.color = new Color(.72f, .008f, .012f, health.RedFlash * .42f);
        if (CurrentState == FacilityGameState.SpawnMenu && spawner != null && spawner.RuntimeActorCount != lastActorCount)
        {
            lastActorCount = spawner.RuntimeActorCount;
            if (spawnStatusText != null) spawnStatusText.text = lastActorCount + " / " + spawner.maxCharacters + " characters placed";
        }
        if (CurrentState == FacilityGameState.Placing && spawner != null && placementText != null)
        {
            string hint = (spawner.Selection == FacilitySpawnKind.EliteSwat ? "Elite SWAT" : spawner.Selection == FacilitySpawnKind.Zombie ? "Zombie" : "The Observer") + "  /  " + spawner.PlacementReason;
            if (hint != lastPlacementHint)
            {
                lastPlacementHint = hint;
                placementText.text = hint;
                placementText.color = spawner.PlacementValid ? new Color(.65f, .86f, .78f) : new Color(.94f, .55f, .39f);
            }
        }
    }

    public void StartGame()
    {
        if (CurrentState != FacilityGameState.MainMenu) return;
        ApplyState(FacilityGameState.ModeSelect);
    }

    public void StartSandbox()
    {
        if (reloading) return;
        if (SceneManager.GetActiveScene().name != sandboxSceneName)
        { LoadModeScene(sandboxSceneName, FacilityGameMode.Sandbox, 0); return; }
        BeginMode(FacilityGameMode.Sandbox, 0);
    }

    public void StartStory()
    {
        if (reloading || storySceneNames == null || storySceneNames.Length == 0) return;
        if (SceneManager.GetActiveScene().name != storySceneNames[0])
        { LoadModeScene(storySceneNames[0], FacilityGameMode.Story, 0); return; }
        BeginMode(FacilityGameMode.Story, 0);
    }

    private void BeginMode(FacilityGameMode mode, int level)
    {
        VehicleInputLocked = false;
        CurrentMode = mode;
        if (mode == FacilityGameMode.Story) FacilityEconomy.BeginRun();
        StoryLevelIndex = level;
        if (health != null) health.ResetHealth();
        lastHealth = -1;
        RefreshHealth();
        if (healthFill != null) healthFill.fillAmount = targetHealthFill;
        if (inventory != null) inventory.ConfigureLoadout(mode == FacilityGameMode.Sandbox);
        if (mode == FacilityGameMode.Story && storyDirector != null)
        {
            if (storyDirector.spawnPoint != null && player != null)
            {
                CharacterController capsule = player.GetComponent<CharacterController>();
                bool enabled = capsule != null && capsule.enabled;
                if (capsule != null) capsule.enabled = false;
                player.transform.SetPositionAndRotation(storyDirector.spawnPoint.position, storyDirector.spawnPoint.rotation);
                player.ResetRecoil();
                if (player.cameraPivot != null) player.cameraPivot.localRotation = Quaternion.identity;
                if (capsule != null) capsule.enabled = enabled;
                Physics.SyncTransforms();
            }
            storyDirector.BeginStory();
        }
        ApplyState(FacilityGameState.Playing);
    }

    public void BackToTitle()
    { if (CurrentState == FacilityGameState.ModeSelect) ApplyState(FacilityGameState.MainMenu); }

    private void LoadModeScene(string sceneName, FacilityGameMode mode, int level)
    {
        if (reloading) return;
        reloading = true;
        enterRequestedGame = true; requestedMode = mode; requestedStoryLevel = level;
        Time.timeScale = 1f; AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }

    public void CompleteStoryLevel()
    {
        if (CurrentMode != FacilityGameMode.Story || storyDirector == null || !storyDirector.ExtractionUnlocked ||
            storyDirector.Stage != FacilityStoryStage.Completed || storyDirector.vehicleExtraction == null || !storyDirector.vehicleExtraction.IsSeated ||
            CurrentState != FacilityGameState.Playing || (health != null && health.IsDead)) return;
        FacilityEconomy.AwardHeist();
        bool hasNext = storySceneNames != null && StoryLevelIndex + 1 < storySceneNames.Length;
        if (completionTitleText != null) completionTitleText.text = "LEVEL " + (StoryLevelIndex + 1).ToString("00") + " COMPLETE";
        if (completionBodyText != null) completionBodyText.text = "Cash + gold secured.  +$2,500 BANKED\n" +
            "Saved balance: " + FacilityEconomy.Cash.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "\n" +
            (hasNext ? "The next job is waiting." : "You got back into the car.");
        if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(hasNext);
        ApplyState(FacilityGameState.LevelComplete);
    }

    public void NextStoryLevel()
    {
        if (CurrentState != FacilityGameState.LevelComplete || storySceneNames == null || StoryLevelIndex + 1 >= storySceneNames.Length) return;
        int next = StoryLevelIndex + 1;
        LoadModeScene(storySceneNames[next], FacilityGameMode.Story, next);
    }

    public void ReturnToMainMenu()
    {
        if (reloading) return;
        reloading = true;
        enterRequestedGame = false; requestedMode = FacilityGameMode.None; requestedStoryLevel = 0;
        Time.timeScale = 1f; AudioListener.pause = false;
        SceneManager.LoadScene(sandboxSceneName);
    }

    public void ToggleSpawnMenu()
    {
        if (CurrentMode != FacilityGameMode.Sandbox) return;
        if (CurrentState == FacilityGameState.SpawnMenu) { CancelPlacement(); return; }
        if (CurrentState != FacilityGameState.Playing && CurrentState != FacilityGameState.Placing) return;
        spawner?.CancelSelection();
        lastActorCount = -1;
        if (spawnSelectionText != null) spawnSelectionText.text = "Select a character to preview placement.";
        ApplyState(FacilityGameState.SpawnMenu);
    }

    public void SelectCharacter(FacilitySpawnKind kind)
    {
        if (CurrentMode != FacilityGameMode.Sandbox) return;
        if (CurrentState != FacilityGameState.SpawnMenu || spawner == null) return;
        spawner.Select(kind);
        lastPlacementHint = null;
        ApplyState(FacilityGameState.Placing);
    }

    public void CancelPlacement()
    {
        if (CurrentState != FacilityGameState.Placing && CurrentState != FacilityGameState.SpawnMenu) return;
        spawner?.CancelSelection();
        ApplyState(FacilityGameState.Playing);
    }

    public void PauseGame()
    {
        if (CurrentState != FacilityGameState.Playing && CurrentState != FacilityGameState.SpawnMenu && CurrentState != FacilityGameState.Placing) return;
        spawner?.CancelSelection();
        ApplyState(FacilityGameState.Paused);
    }

    public void ResumeGame()
    { if (CurrentState == FacilityGameState.Paused) ApplyState(FacilityGameState.Playing); }

    /// <summary>Seat entry blocks player control without pausing guards or the physical door animation.</summary>
    public void SetVehicleInputLocked(bool locked)
    {
        VehicleInputLocked = locked;
        bool worldVisible = CurrentState == FacilityGameState.Playing || CurrentState == FacilityGameState.SpawnMenu ||
            CurrentState == FacilityGameState.Placing || CurrentState == FacilityGameState.Paused;
        if (inventory != null) inventory.SetVisible(worldVisible && !locked);
        else if (weaponModel != null) weaponModel.gameObject.SetActive(worldVisible && !locked);
        weaponReadyAt = Time.unscaledTime + .15f;
    }

    public void RestartGame()
    {
        if (reloading) return;
        reloading = true;
        enterRequestedGame = true; requestedMode = CurrentMode; requestedStoryLevel = StoryLevelIndex;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDamage(Transform source) => RefreshHealth();
    private void OnDeath()
    {
        spawner?.CancelSelection();
        ApplyState(FacilityGameState.Dead);
    }

    private void RefreshHealth()
    {
        if (health == null || health.CurrentHealth == lastHealth) return;
        lastHealth = health.CurrentHealth;
        if (hpText != null) hpText.text = lastHealth + " / " + health.MaxHealth;
        if (healthFill != null)
        {
            targetHealthFill = (float)lastHealth / health.MaxHealth;
            healthFill.color = lastHealth >= 3 ? new Color(.42f, .7f, .75f) : lastHealth == 2 ? new Color(.91f, .63f, .32f) : new Color(.86f, .23f, .19f);
        }
    }

    private void ApplyState(FacilityGameState state)
    {
        CurrentState = state;
        Time.timeScale = SimulationPaused ? 0f : 1f;
        AudioListener.pause = state == FacilityGameState.Paused || state == FacilityGameState.Dead || state == FacilityGameState.LevelComplete;
        if (controlHintText != null) controlHintText.text = CurrentMode == FacilityGameMode.Story ? "E  INTERACT     ESC  PAUSE" : "TAB  SPAWN     ESC  PAUSE";
        SetActive(mainMenu, state == FacilityGameState.MainMenu);
        SetActive(modeMenu, state == FacilityGameState.ModeSelect);
        SetActive(completionMenu, state == FacilityGameState.LevelComplete);
        SetActive(pauseMenu, state == FacilityGameState.Paused);
        SetActive(deathMenu, state == FacilityGameState.Dead);
        SetActive(spawnMenu, state == FacilityGameState.SpawnMenu);
        SetActive(placementHud, state == FacilityGameState.Placing);
        bool showWorldHud = state == FacilityGameState.Playing || state == FacilityGameState.SpawnMenu || state == FacilityGameState.Placing || state == FacilityGameState.Paused;
        SetActive(hud, showWorldHud);
        SetActive(objectiveHud, CurrentMode == FacilityGameMode.Story && state == FacilityGameState.Playing);
        bool showWeapon = showWorldHud && !VehicleInputLocked;
        if (inventory != null) inventory.SetVisible(showWeapon);
        else if (weaponModel != null) weaponModel.gameObject.SetActive(showWeapon);
        bool locked = state == FacilityGameState.Playing || state == FacilityGameState.Placing;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        weaponReadyAt = Time.unscaledTime + .15f;
        if (EventSystem.current != null)
        {
            Button selected = state == FacilityGameState.MainMenu ? startButton : state == FacilityGameState.Paused ? resumeButton :
                state == FacilityGameState.Dead ? deathRestartButton : state == FacilityGameState.SpawnMenu ? observerSpawnButton :
                state == FacilityGameState.ModeSelect ? storyButton : state == FacilityGameState.LevelComplete ? returnMainButton : null;
            EventSystem.current.SetSelectedGameObject(selected != null ? selected.gameObject : null);
        }
    }

    private static void SetActive(GameObject panel, bool active) { if (panel != null) panel.SetActive(active); }

    private void OnDestroy()
    {
        if (health != null) { health.Damaged -= OnDamage; health.Died -= OnDeath; }
        if (Instance != this) return;
        Instance = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
