using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit CLI-only integration check for authored desktop menus and their actual button events.</summary>
/// <remarks>Use -facilityDesktopMenuTest "output directory". Never edits or saves authored scene assets.</remarks>
public sealed class FacilityDesktopMenuSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class Receipt
    {
        public bool success, startupTitle, styledButtons, authoredAudio, disabledButtonIgnored,
            titleToMode, submitBackToTitle, storyStartsUnarmed, pauseFreezesSimulation,
            resumeButtonWorks, deathMenu, deathRestartWorks, pauseRestartWorks,
            returnToTitle, sandboxLoadout, observerCardWorks, zombieCardWorks,
            placementCancellationWorks, quitButtonDispatches, clicksWhileListenerPaused,
            modeCloseReturnsToTitle, pauseCloseResumes, spawnCloseReturnsToGameplay, normalControlsEnabled;
        public int styledButtonCount, pointerActivations, submitActivations, verifiedAudibleActivations;
        public string utc, unity, graphicsApi, startupScene;
        public string coverage = "Real EventSystem pointer and submit events through the saved title, mode, pause, death and Sandbox spawn menus. Checks shared persisted style sprites, JetBrains font, one sound per enabled activation, no sound on disabled controls, unscaled audio while the listener is paused, Story/Sandbox loadouts and scene reloads. Death is produced through three shared player-health hits. This check does not substitute for visual inspection or natural NPC combat checks, and never saves a scene.";
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private string output;
    private FacilityGameSession session;
    private bool collectingErrors, interceptQuit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-facilityDesktopMenuTest");
        if (flag < 0) return;
        if (flag + 1 >= args.Length || args[flag + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityDesktopMenuTest requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Desktop menu diagnostics (explicit CLI only)").AddComponent<FacilityDesktopMenuSmokeTest>();
        runner.output = args[flag + 1];
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        collectingErrors = true;
        Application.logMessageReceived += OnLog;
        Application.wantsToQuit += OnWantsToQuit;
        var routines = new Stack<IEnumerator>();
        routines.Push(Run());
        while (routines.Count > 0)
        {
            IEnumerator currentRoutine = routines.Peek();
            bool more;
            object current = null;
            try { more = currentRoutine.MoveNext(); if (more) current = currentRoutine.Current; }
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
        receipt.success = receipt.failures.Count == 0;
        try
        {
            File.WriteAllText(Path.Combine(output, "desktop-menu-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Facility desktop menu check " + (receipt.success ? "passed: " : "failed: ") + output);
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
        yield return new WaitForSecondsRealtime(.7f);
        AcquireSession();
        receipt.utc = DateTime.UtcNow.ToString("O");
        receipt.unity = Application.unityVersion;
        receipt.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
        receipt.startupScene = SceneManager.GetActiveScene().path;
        receipt.startupTitle = session.CurrentState == FacilityGameState.MainMenu &&
            session.mainMenu.activeInHierarchy && HasVisibleLabel(session.mainMenu, "rob the bank") && Time.timeScale == 0f;
        Require(receipt.startupTitle, "The startup desktop does not show the expected title window.");
        CheckStyles();
        FacilityMenuAudio audio = session.GetComponent<FacilityMenuAudio>();
        int before = audio.ActivationCount;
        session.startButton.interactable = false;
        DispatchPointer(session.startButton);
        receipt.disabledButtonIgnored = session.CurrentState == FacilityGameState.MainMenu && audio.ActivationCount == before;
        session.startButton.interactable = true;
        Require(receipt.disabledButtonIgnored, "A disabled Start button activated or played a menu click.");

        Activate(session.startButton);
        receipt.titleToMode = session.CurrentState == FacilityGameState.ModeSelect && session.modeMenu.activeInHierarchy &&
            session.storyButton.gameObject.activeInHierarchy && session.sandboxButton.gameObject.activeInHierarchy;
        Require(receipt.titleToMode, "Start did not open the Story / Sandbox desktop window.");
        yield return new WaitForSecondsRealtime(.12f);
        Activate(session.modeBackButton, true);
        receipt.submitBackToTitle = session.CurrentState == FacilityGameState.MainMenu && session.mainMenu.activeInHierarchy;
        Require(receipt.submitBackToTitle, "Keyboard Submit on Back did not return to the title.");
        yield return new WaitForSecondsRealtime(.12f);
        Activate(session.startButton);
        Activate(CloseButton(session.modeMenu));
        receipt.modeCloseReturnsToTitle = session.CurrentState == FacilityGameState.MainMenu && session.mainMenu.activeInHierarchy;
        Require(receipt.modeCloseReturnsToTitle, "The mode window's X did not return to the title.");
        Activate(session.startButton);
        FacilityGameSession previous = session;
        Activate(session.storyButton, true);
        yield return WaitForSession(previous, FacilityGameMode.Story);
        receipt.storyStartsUnarmed = session.CurrentState == FacilityGameState.Playing && session.storyDirector != null &&
            session.storyDirector.HasBegun && !session.inventory.HasPistol && !session.inventory.HasShotgun;
        Require(receipt.storyStartsUnarmed, "The desktop Story choice did not start the authored unarmed mission.");
        CheckStyles();

        session.PauseGame();
        yield return null;
        receipt.pauseFreezesSimulation = session.CurrentState == FacilityGameState.Paused && session.pauseMenu.activeInHierarchy &&
            Time.timeScale == 0f && AudioListener.pause && !FacilityGameSession.GameplayInputAllowed;
        Require(receipt.pauseFreezesSimulation, "The pause desktop does not pause world simulation and audio.");
        Activate(CloseButton(session.pauseMenu));
        receipt.pauseCloseResumes = session.CurrentState == FacilityGameState.Playing && !session.pauseMenu.activeInHierarchy &&
            Time.timeScale > 0f && !AudioListener.pause;
        Require(receipt.pauseCloseResumes, "The pause window's X did not resume gameplay.");
        session.PauseGame();
        yield return null;
        Activate(session.resumeButton);
        receipt.resumeButtonWorks = session.CurrentState == FacilityGameState.Playing && Time.timeScale > 0f && !AudioListener.pause;
        Require(receipt.resumeButtonWorks, "The desktop Resume button did not restore gameplay.");

        // Use the production shared health path, without waiting for or modifying NPC combat brains.
        for (int i = 0; i < FacilityPlayerHealth.HitLimit; i++)
        {
            Require(session.health.TryTakeHit(transform), "The test hit was rejected before the death menu.");
            if (i + 1 < FacilityPlayerHealth.HitLimit) yield return new WaitForSecondsRealtime(1f);
        }
        yield return null;
        receipt.deathMenu = session.CurrentState == FacilityGameState.Dead && session.deathMenu.activeInHierarchy &&
            HasVisibleLabel(session.deathMenu, "you arent finished yet") && Time.timeScale == 0f && AudioListener.pause;
        Require(receipt.deathMenu, "The red death message did not appear in the desktop death menu.");
        previous = session;
        Activate(session.deathRestartButton);
        yield return WaitForSession(previous, FacilityGameMode.Story);
        receipt.deathRestartWorks = FreshStory();
        Require(receipt.deathRestartWorks, "The desktop death Restart did not restore a clean mission and full health.");
        session.PauseGame();
        yield return null;
        previous = session;
        Activate(session.restartButton, true);
        yield return WaitForSession(previous, FacilityGameMode.Story);
        receipt.pauseRestartWorks = FreshStory();
        Require(receipt.pauseRestartWorks, "Keyboard Submit on pause Restart did not reload a clean mission.");

        previous = session;
        session.ReturnToMainMenu();
        yield return WaitForSession(previous, FacilityGameMode.None);
        receipt.returnToTitle = session.CurrentState == FacilityGameState.MainMenu && session.mainMenu.activeInHierarchy;
        Require(receipt.returnToTitle, "Returning from the mission did not restore the title desktop.");
        Activate(session.startButton);
        Activate(session.sandboxButton);
        yield return new WaitForSecondsRealtime(.15f);
        receipt.sandboxLoadout = session.CurrentMode == FacilityGameMode.Sandbox && session.CurrentState == FacilityGameState.Playing &&
            session.inventory.HasPistol && session.inventory.HasShotgun;
        Require(receipt.sandboxLoadout, "The desktop Sandbox choice did not restore both guns.");
        session.ToggleSpawnMenu();
        yield return null;
        Require(session.CurrentState == FacilityGameState.SpawnMenu && session.spawnMenu.activeInHierarchy,
            "The Sandbox spawn desktop did not open.");
        Activate(CloseButton(session.spawnMenu));
        receipt.spawnCloseReturnsToGameplay = session.CurrentState == FacilityGameState.Playing && !session.spawnMenu.activeInHierarchy &&
            !session.spawner.HasSelection && Time.timeScale > 0f;
        Require(receipt.spawnCloseReturnsToGameplay, "The spawn window's X did not return to gameplay.");
        session.ToggleSpawnMenu();
        yield return null;
        Activate(session.observerSpawnButton);
        receipt.observerCardWorks = session.CurrentState == FacilityGameState.Placing && session.spawner.HasSelection &&
            session.spawner.Selection == FacilitySpawnKind.Observer;
        Require(receipt.observerCardWorks, "The Observer desktop card did not start character placement.");
        session.CancelPlacement();
        bool observerCancelled = session.CurrentState == FacilityGameState.Playing && !session.spawner.HasSelection;
        session.ToggleSpawnMenu();
        yield return null;
        Activate(session.zombieSpawnButton, true);
        receipt.zombieCardWorks = session.CurrentState == FacilityGameState.Placing && session.spawner.HasSelection &&
            session.spawner.Selection == FacilitySpawnKind.Zombie;
        Require(receipt.zombieCardWorks, "Keyboard Submit on the Zombie card did not start character placement.");
        session.CancelPlacement();
        receipt.placementCancellationWorks = observerCancelled && session.CurrentState == FacilityGameState.Playing && !session.spawner.HasSelection;
        Require(receipt.placementCancellationWorks, "Cancelling either desktop spawn card left a selection or blocked gameplay.");

        session.PauseGame();
        yield return null;
        interceptQuit = true;
        Activate(session.pauseQuitButton);
        yield return new WaitForSecondsRealtime(.02f);
        interceptQuit = false;
        Require(receipt.quitButtonDispatches, "The pause desktop Quit button did not dispatch Application.Quit.");
        Require(receipt.clicksWhileListenerPaused, "No click was verified while world audio was paused.");
        Require(receipt.verifiedAudibleActivations == receipt.pointerActivations + receipt.submitActivations,
            "An enabled activation failed to emit exactly one click.");
    }

    private void CheckStyles()
    {
        FacilityMenuAudio audio = session.GetComponent<FacilityMenuAudio>();
        Require(audio != null, "The scene has no authored FacilityMenuAudio component.");
        receipt.authoredAudio = audio.Source != null && audio.Click != null && audio.Click.length >= .04f && audio.Click.length <= .3f &&
            audio.Source.ignoreListenerPause && audio.Source.spatialBlend == 0f && !audio.Source.playOnAwake;
        Require(receipt.authoredAudio, "The authored desktop activation sound or its unpaused 2D source is missing.");
        GameObject[] panels = { session.mainMenu, session.modeMenu, session.pauseMenu, session.deathMenu, session.spawnMenu, session.completionMenu };
        foreach (GameObject panel in panels)
        {
            Require(panel != null, "An expected desktop menu root is missing.");
            Transform skin = panel.transform.Find("Desktop Skin");
            Require(skin != null, "Missing desktop window skin on " + panel.name);
            Transform frame = skin.Find("Window Frame");
            Image image = frame != null ? frame.GetComponent<Image>() : null;
            Require(image != null && image.sprite != null && image.type == Image.Type.Sliced,
                "Missing persisted nine-slice window frame on " + panel.name);
        }
        Button[] buttons = session.canvas.GetComponentsInChildren<Button>(true);
        Require(buttons.Length >= 18, "The authored menus are missing expected controls.");
        foreach (Button button in buttons)
        {
            // Parent menu roots are deliberately inactive until selected. Their ordinary controls
            // must still be locally enabled, including those under the inactive completion window.
            // Next Level alone is conditionally hidden by the campaign sequence.
            if (button != session.nextLevelButton)
                Require(button.gameObject.activeSelf && button.interactable,
                    "An ordinary menu control was disabled by style cleanup: " + button.name);
            FacilityDesktopButton feedback = button.GetComponent<FacilityDesktopButton>();
            Require(feedback != null && feedback.MenuAudio == audio, "Missing shared desktop feedback on " + button.name);
            Image face = button.targetGraphic as Image;
            Require(face != null && face.sprite != null && face.sprite.texture != null,
                "Missing persisted desktop button surface on " + button.name);
            Require(feedback.face == face && feedback.raisedSprite != null && feedback.pressedSprite != null && feedback.hoverSprite != null,
                "Missing pressed or hover desktop button artwork on " + button.name);
            Text[] labels = button.GetComponentsInChildren<Text>(true);
            Require(labels.Length > 0, "Desktop button has no text: " + button.name);
            foreach (Text label in labels)
                Require(label.font != null && label.font.name.IndexOf("JetBrains", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Desktop button did not retain JetBrains Mono: " + button.name + "/" + label.name);
        }
        receipt.styledButtonCount += buttons.Length;
        receipt.styledButtons = true;
        receipt.normalControlsEnabled = true;
    }

    private static Button CloseButton(GameObject menu)
    {
        Transform close = menu != null ? menu.transform.Find("Desktop Skin/Close Window") : null;
        Button button = close != null ? close.GetComponent<Button>() : null;
        Require(button != null, "A desktop window is missing its real X button.");
        return button;
    }

    private void Activate(Button button, bool submit = false)
    {
        Require(button != null && button.isActiveAndEnabled && button.IsInteractable(), "Attempted to activate an unavailable desktop button.");
        Require(EventSystem.current != null, "The desktop has no EventSystem.");
        FacilityMenuAudio audio = session.GetComponent<FacilityMenuAudio>();
        Require(audio != null, "The current desktop has no click player.");
        int before = audio.ActivationCount;
        bool listenerPaused = AudioListener.pause;
        string buttonName = button.name;
        int previousScene = SceneManager.GetActiveScene().handle;
        if (submit)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            receipt.submitActivations++;
        }
        else { DispatchPointer(button); receipt.pointerActivations++; }
        // ActivationCount is managed state and remains readable even when a synchronous scene reload
        // destroys the old component. PlaybackSource is the persistent, short-lived audio-only voice.
        Require(audio.ActivationCount == before + 1, "Desktop activation did not emit exactly one click: " + buttonName);
        AudioSource voice = audio.PlaybackSource;
        bool sceneChanged = SceneManager.GetActiveScene().handle != previousScene;
        Require(voice != null && (sceneChanged || voice.isPlaying) && voice.ignoreListenerPause && voice.clip != null,
            "The desktop click did not remain audible through the activation.");
        receipt.verifiedAudibleActivations++;
        if (listenerPaused) receipt.clicksWhileListenerPaused = true;
    }

    private static void DispatchPointer(Button button)
    {
        var pointer = new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Left, pointerId = -1, eligibleForClick = true };
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private IEnumerator WaitForSession(FacilityGameSession previous, FacilityGameMode mode)
    {
        float until = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
            FacilityGameSession candidate = FacilityGameSession.Instance;
            if (candidate == null || candidate == previous || candidate.CurrentMode != mode) continue;
            yield return new WaitForSecondsRealtime(.15f);
            AcquireSession();
            yield break;
        }
        throw new InvalidOperationException("The desktop scene transition did not produce a new " + mode + " session within twelve seconds.");
    }

    private bool FreshStory() => session.CurrentState == FacilityGameState.Playing && session.CurrentMode == FacilityGameMode.Story &&
        session.health.CurrentHealth == FacilityPlayerHealth.HitLimit && !session.health.IsDead && session.storyDirector.HasBegun &&
        !session.storyDirector.HasDrill && !session.storyDirector.AlarmActive && !session.inventory.HasPistol && !session.inventory.HasShotgun;

    private void AcquireSession()
    {
        session = FacilityGameSession.Instance;
        Require(session != null && session.health != null && session.inventory != null && session.canvas != null,
            "The active scene has no complete authored game session.");
    }

    private static bool HasVisibleLabel(GameObject root, string expected)
    {
        if (root == null) return false;
        foreach (Text label in root.GetComponentsInChildren<Text>())
            if (label.gameObject.activeInHierarchy && string.Equals(label.text.Trim(), expected, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private bool OnWantsToQuit()
    {
        if (!interceptQuit) return true;
        receipt.quitButtonDispatches = true;
        return false;
    }

    private void OnLog(string message, string stack, LogType kind)
    {
        if (collectingErrors && (kind == LogType.Error || kind == LogType.Assert || kind == LogType.Exception))
            receipt.failures.Add(message + "\n" + stack);
    }
}
