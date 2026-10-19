using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Existing values stay stable for authored scenes and saved diagnostics.
public enum FacilityStoryStage { EnterFacility, EliminateReceptionist, TakePistol, FindVault, ReturnToSedan, Completed, RetrieveDrill, DrillingVault }

/// <summary>The first heist's explicit, one-way objectives. No procedural world generation happens here.</summary>
[DisallowMultipleComponent]
public sealed class FacilityStoryDirector : MonoBehaviour
{
    public FacilityHumanoid receptionist;
    public FacilityKnifeGuard[] guards;
    public RetroFPSController player;
    public FacilityWeaponInventory inventory;
    public FacilityTypedObjective typedObjective;
    public AudioSource alarmSource;
    public Light[] alarmLights;
    public Transform spawnPoint, lobbyEntry;
    public Text interactionText, heistStatusText;
    public GameObject heistStatusPanel;
    public Text pistolAlarmWarning;
    public Renderer[] alarmLenses;
    public FacilityStoryInteractable pistolPickup, cashPickup, goldPickup, sedanExit;
    [Tooltip("Place at the reception counter centre, with its blue forward axis toward the receptionist's open side.")]
    public Transform pistolAccessPlane;
    [Min(0f)] public float pistolAccessMinDistance = .65f;
    [Tooltip("A point on the visible pistol, rather than the larger pickup trigger.")]
    public Transform pistolVisibilityPoint;
    public FacilityStoryInteractable drillPickup, vaultInteraction;
    public FacilityVehicleExtraction vehicleExtraction;
    public FacilityVaultDrill vaultDrill;
    public FacilityPoliceWaves policeWaves;
    [Min(.5f)] public float lobbyEntryRadius = 4f;
    [Min(1f)] public float interactionRange = 2.7f;
    public FacilityStoryStage Stage { get; private set; }
    public bool HasCash { get; private set; }
    public bool HasGold { get; private set; }
    public bool HasDrill { get; private set; }
    public bool VaultOpen => vaultDrill != null && vaultDrill.IsComplete;
    public bool AlarmActive { get; private set; }
    public bool HasBegun { get; private set; }
    public FacilityStoryInteractable FocusedInteraction { get; private set; }
    public string InteractionPrompt { get; private set; } = "";
    private Camera eyes;
    private FacilityPlayerHealth health;
    private float[] lightLevels;
    private readonly RaycastHit[] sightHits = new RaycastHit[64];
    private string shownPrompt, shownStatus;
    private float nextStatusRefreshAt;
    private float alarmStartedAt;
    private MaterialPropertyBlock alarmProperties;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<RetroFPSController>();
        if (player != null)
        {
            eyes = player.GetComponentInChildren<Camera>();
            health = player.GetComponent<FacilityPlayerHealth>();
            if (inventory == null) inventory = player.GetComponentInChildren<FacilityWeaponInventory>();
        }
        if (vehicleExtraction == null && sedanExit != null)
            vehicleExtraction = sedanExit.GetComponentInParent<FacilityVehicleExtraction>();
        if (guards == null) guards = new FacilityKnifeGuard[0];
        if (alarmLights == null) alarmLights = new Light[0];
        lightLevels = new float[alarmLights.Length];
        for (int i = 0; i < alarmLights.Length; i++)
            if (alarmLights[i] != null) { lightLevels[i] = alarmLights[i].intensity; alarmLights[i].enabled = false; }
        if (alarmSource != null) alarmSource.Stop();
        if (receptionist != null) receptionist.Attacked += OnReceptionistAttacked;
        alarmProperties = new MaterialPropertyBlock();
        if (heistStatusPanel != null) heistStatusPanel.SetActive(false);
        UpdateAlarmVisuals();
        SetInteractionPrompt("");
    }

    private void OnDestroy()
    {
        if (receptionist != null) receptionist.Attacked -= OnReceptionistAttacked;
    }

    private void OnReceptionistAttacked(FacilityHumanoid actor)
    {
        if (!HasBegun || AlarmActive || Stage == FacilityStoryStage.Completed) return;
        ActivateAlarm();
        if (HasDrill) Advance(FacilityStoryStage.FindVault);
    }

    public void BeginStory()
    {
        if (HasBegun) return;
        HasBegun = true;
        Stage = FacilityStoryStage.RetrieveDrill;
        UpdateObjective();
    }

    private void Update()
    {
        if (!HasBegun) return;
        bool playing = FacilityGameSession.GameplayInputAllowed &&
            (FacilityGameSession.Instance == null || FacilityGameSession.Instance.CurrentState == FacilityGameState.Playing) &&
            Time.timeScale > 0f && (health == null || !health.IsDead);
        if (!playing || Stage == FacilityStoryStage.Completed) { SetInteractionPrompt(""); return; }
        if (Stage == FacilityStoryStage.EnterFacility)
        {
            Vector3 target = lobbyEntry != null ? lobbyEntry.position : receptionist != null ? receptionist.transform.position : Vector3.positiveInfinity;
            if (player != null && Vector3.Distance(player.transform.position, target) <= lobbyEntryRadius)
                Advance(FacilityStoryStage.EliminateReceptionist);
        }
        if (!AlarmActive && receptionist != null && (receptionist.HitsTaken > 0 || receptionist.IsDead))
            OnReceptionistAttacked(receptionist);
        if (Stage == FacilityStoryStage.DrillingVault && VaultOpen)
            Advance(FacilityStoryStage.FindVault);
        UpdateAlarmVisuals();
        UpdateHeistStatus();
        FocusedInteraction = FindFocusedInteraction();
        string prompt = "";
        if (FocusedInteraction != null)
        {
            bool available = IsInteractionAvailable(FocusedInteraction, out string reason);
            prompt = available ? "[ E ]  " + GetAction(FocusedInteraction) : reason;
            Keyboard keyboard = Keyboard.current;
            if (available && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                TryInteract(FocusedInteraction, out _);
                prompt = "";
            }
        }
        SetInteractionPrompt(prompt);
        if (pistolAlarmWarning != null) pistolAlarmWarning.gameObject.SetActive(false);
    }

    public bool IsInteractionAvailable(FacilityStoryInteractable item, out string reason)
    {
        reason = "";
        if (!HasBegun || item == null || item.Consumed || Stage == FacilityStoryStage.Completed) return false;
        if (vehicleExtraction != null && (vehicleExtraction.IsBoarding || vehicleExtraction.IsSeated)) return false;
        if (item.director != null && item.director != this) return false;
        switch (item.kind)
        {
            case FacilityStoryInteractionKind.Door:
                if (item.door == null) return false;
                if (item.door.IsLocked) { reason = "LOCKED  //  Use the drill on the vault."; return false; }
                return !item.door.IsMoving;
            case FacilityStoryInteractionKind.Drill:
                if (HasDrill) return false;
                if (item.door != null && !item.door.IsOpen) { reason = "Open the car's trunk first."; return false; }
                return true;
            case FacilityStoryInteractionKind.Pistol:
                return false; // The pistol is part of the sedan equipment pickup.
            case FacilityStoryInteractionKind.Vault:
                if (!HasDrill) { reason = "The drill is in the car's trunk."; return false; }
                if (!AlarmActive) { reason = "Deal with the receptionist first."; return false; }
                FacilityVaultDrill drill = item.vault != null ? item.vault : vaultDrill;
                if (drill == null) { reason = "The vault drill is unavailable."; return false; }
                if (drill.IsComplete) { reason = "VAULT UNLOCKED"; return false; }
                if (drill.IsRunning) { reason = "DRILLING  //  " + Mathf.CeilToInt(drill.SecondsRemaining).ToString("00") + "s remaining"; return false; }
                return true;
            case FacilityStoryInteractionKind.Cash:
            case FacilityStoryInteractionKind.Gold:
                if (!VaultOpen) { reason = "The vault is locked. Complete the drill first."; return false; }
                return item.kind == FacilityStoryInteractionKind.Cash ? !HasCash : !HasGold;
            case FacilityStoryInteractionKind.Sedan:
                if (!HasCash || !HasGold)
                { reason = !HasCash && !HasGold ? "Retrieve both the cash and gold before leaving." : !HasCash ? "Retrieve the vault cash before leaving." : "Retrieve the vault gold before leaving."; return false; }
                if (item != sedanExit || vehicleExtraction == null)
                { reason = "Get into the car's driver seat to leave."; return false; }
                return vehicleExtraction.CanBoard(out reason);
        }
        return false;
    }

    public bool TryInteract(FacilityStoryInteractable item, out string reason)
    {
        if (!IsInteractionAvailable(item, out reason)) return false;
        if (Time.timeScale <= 0f || (health != null && health.IsDead) || !FacilityGameSession.GameplayInputAllowed)
        { reason = "Resume the mission to interact."; return false; }
        if (FindFocusedInteraction() != item)
        { reason = "Move closer and look directly at it."; return false; }
        switch (item.kind)
        {
            case FacilityStoryInteractionKind.Door:
                if (!item.door.TryToggle()) { reason = "Clear the doorway before closing it."; return false; }
                break;
            case FacilityStoryInteractionKind.Drill:
                if (inventory == null) { reason = "The car equipment is unavailable."; return false; }
                HasDrill = true;
                inventory.RetrieveSedanLoadout();
                item.Consume();
                Advance(AlarmActive ? FacilityStoryStage.FindVault : FacilityStoryStage.EnterFacility);
                break;
            case FacilityStoryInteractionKind.Pistol:
                return false;
            case FacilityStoryInteractionKind.Vault:
                FacilityVaultDrill drill = item.vault != null ? item.vault : vaultDrill;
                if (drill == null || !drill.BeginDrilling()) { reason = "The drill cannot be started."; return false; }
                if (vaultDrill == null) vaultDrill = drill;
                Advance(FacilityStoryStage.DrillingVault);
                break;
            case FacilityStoryInteractionKind.Cash:
                HasCash = true; item.Consume(); Advance(HasGold ? FacilityStoryStage.ReturnToSedan : FacilityStoryStage.FindVault);
                break;
            case FacilityStoryInteractionKind.Gold:
                HasGold = true; item.Consume(); Advance(HasCash ? FacilityStoryStage.ReturnToSedan : FacilityStoryStage.FindVault);
                break;
            case FacilityStoryInteractionKind.Sedan:
                if (vehicleExtraction == null || !vehicleExtraction.BeginBoarding(this, item, out reason)) return false;
                break;
        }
        reason = "";
        return true;
    }

    /// <summary>Only the completed physical driver-seat entry may finish and pay the mission.</summary>
    public bool CompleteVehicleExtraction(FacilityVehicleExtraction source, FacilityStoryInteractable item)
    {
        FacilityGameSession session = FacilityGameSession.Instance;
        if (source == null || source != vehicleExtraction || !source.IsSeated || item == null || item != sedanExit || item.Consumed ||
            !HasBegun || !HasCash || !HasGold || Stage != FacilityStoryStage.ReturnToSedan || Time.timeScale <= 0f ||
            (health != null && health.IsDead) || (session != null && (session.CurrentState != FacilityGameState.Playing || session.CurrentMode != FacilityGameMode.Story)))
            return false;
        item.Consume(); Advance(FacilityStoryStage.Completed);
        SetInteractionPrompt(""); FocusedInteraction = null;
        if (alarmSource != null) alarmSource.Stop();
        if (policeWaves != null) policeWaves.StopAssault();
        session?.CompleteStoryLevel();
        return true;
    }

    public bool IsOnPistolAccessSide()
    {
        // A saved access plane keeps this rule correct if the whole desk is moved or rotated in the scene.
        if (player == null || pistolAccessPlane == null) return false;
        Vector3 forward = pistolAccessPlane.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < .001f) return false;
        Vector3 offset = player.transform.position - pistolAccessPlane.position;
        offset.y = 0f;
        return Vector3.Dot(offset, forward.normalized) >= pistolAccessMinDistance;
    }

    public bool CanSeePistol()
    {
        if (eyes == null || player == null || pistolPickup == null || pistolPickup.Consumed) return false;
        Transform visual = pistolPickup.visualRoot != null ? pistolPickup.visualRoot.transform : null;
        Vector3 target = pistolVisibilityPoint != null ? pistolVisibilityPoint.position : visual != null ? visual.position : pistolPickup.transform.position;
        Vector3 offset = target - eyes.transform.position;
        float distance = offset.magnitude;
        if (distance < .01f) return true;
        if (distance > interactionRange) return false;
        // Ignore all trigger volumes: a broad pickup sensor must never grant reach through the wooden front or worktop.
        int count = Physics.RaycastNonAlloc(eyes.transform.position, offset / distance, sightHits,
            Mathf.Max(0f, distance - .015f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == sightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = sightHits[i].collider;
            if (collider == null || collider.transform.IsChildOf(player.transform)) continue;
            if (visual != null && collider.transform.IsChildOf(visual)) continue;
            return false;
        }
        return true;
    }

    private FacilityStoryInteractable FindFocusedInteraction()
    {
        if (eyes == null || player == null) return null;
        int count = Physics.RaycastNonAlloc(eyes.transform.position, eyes.transform.forward, sightHits,
            interactionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        if (count == sightHits.Length) return null;
        Collider nearest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider item = sightHits[i].collider;
            if (item == null || item.transform.IsChildOf(player.transform) || item is CharacterController) continue;
            // Unrelated sensor volumes should not hide a physical pickup, but doors and walls must.
            FacilityStoryInteractable interaction = item.GetComponentInParent<FacilityStoryInteractable>();
            if (item.isTrigger && interaction == null) continue;
            if (sightHits[i].distance < distance) { nearest = item; distance = sightHits[i].distance; }
        }
        if (nearest == null) return null;
        FacilityStoryInteractable focused = nearest.GetComponentInParent<FacilityStoryInteractable>();
        if (focused != null) return focused;
        FacilityDoor door = nearest.GetComponentInParent<FacilityDoor>();
        return door != null ? door.interaction : null;
    }

    private void ActivateAlarm()
    {
        if (AlarmActive) return;
        AlarmActive = true;
        alarmStartedAt = Time.time;
        if (alarmSource != null) { alarmSource.loop = true; alarmSource.Play(); }
        foreach (Light light in alarmLights) if (light != null) light.enabled = true;
        foreach (FacilityKnifeGuard guard in guards) if (guard != null && guard.actor != null && !guard.actor.IsDead) guard.Activate();
        if (policeWaves != null) policeWaves.BeginAssault();
    }

    private void Advance(FacilityStoryStage stage) { Stage = stage; UpdateObjective(); }

    private void UpdateObjective()
    {
        if (typedObjective == null) return;
        string text;
        switch (Stage)
        {
            case FacilityStoryStage.RetrieveDrill: text = "Open the car's trunk. Collect your weapons and vault drill."; break;
            case FacilityStoryStage.EnterFacility: text = "Enter the bank."; break;
            case FacilityStoryStage.EliminateReceptionist: text = "Deal with the receptionist. Attacking triggers the alarm."; break;
            case FacilityStoryStage.TakePistol: text = "Collect your weapons and vault drill from the car."; break;
            case FacilityStoryStage.FindVault: text = VaultOpen ? (HasCash ? "Cash secured. Retrieve the gold." : HasGold ? "Gold secured. Retrieve the cash." : "Vault open. Retrieve both the cash and gold.") : "ALARM ACTIVE. Find the vault and install the drill."; break;
            case FacilityStoryStage.DrillingVault: text = "Defend the drill. The vault opens in 45 seconds. Police reinforcements arrive every 15 seconds."; break;
            case FacilityStoryStage.ReturnToSedan: text = "Cash and gold secured. Open the driver door and get into the driver's seat. Payout: $2,500."; break;
            default: text = "Cash and gold recovered. Extraction complete. $2,500 banked."; break;
        }
        int step = Stage == FacilityStoryStage.RetrieveDrill || Stage == FacilityStoryStage.TakePistol ? 1 : Stage == FacilityStoryStage.EnterFacility ? 2 :
            Stage == FacilityStoryStage.EliminateReceptionist ? 3 : Stage == FacilityStoryStage.DrillingVault ? 5 :
            Stage == FacilityStoryStage.FindVault ? (VaultOpen ? 6 : 4) : 7;
        typedObjective.SetObjective("NIGHT DEPOSIT / OBJECTIVE", text, step, 7);
    }

    private static string GetAction(FacilityStoryInteractable item)
    {
        switch (item.kind)
        {
            case FacilityStoryInteractionKind.Door:
                string doorLabel = string.IsNullOrEmpty(item.label) ? "DOOR" : item.label.ToUpperInvariant();
                if (doorLabel.StartsWith("OPEN ")) doorLabel = doorLabel.Substring(5);
                return (item.door != null && item.door.WantsOpen ? "CLOSE " : "OPEN ") + doorLabel;
            case FacilityStoryInteractionKind.Drill: return "COLLECT WEAPONS + VAULT DRILL";
            case FacilityStoryInteractionKind.Vault: return "INSTALL DRILL  //  45 SECONDS";
            case FacilityStoryInteractionKind.Pistol: return "TAKE PISTOL";
            case FacilityStoryInteractionKind.Cash: return "TAKE CASH";
            case FacilityStoryInteractionKind.Gold: return "TAKE GOLD";
            default: return "GET INTO DRIVER'S SEAT / LEAVE";
        }
    }

    private void UpdateHeistStatus()
    {
        if (heistStatusPanel != null && heistStatusPanel.activeSelf != AlarmActive) heistStatusPanel.SetActive(AlarmActive);
        if (!AlarmActive) return;
        if (heistStatusText == null || Time.time < nextStatusRefreshAt) return;
        nextStatusRefreshAt = Time.time + .1f;
        string assaultStatus = AlarmActive && policeWaves != null ? "POLICE IN  //  " +
            Mathf.CeilToInt(policeWaves.SecondsUntilNextWave).ToString("00") + "s   WAVE " + policeWaves.WaveNumber.ToString("00") +
            "   ACTIVE " + policeWaves.ActivePoliceCount : "ALARM  //  STANDBY";
        string status = assaultStatus;
        if (status == shownStatus) return;
        shownStatus = status;
        heistStatusText.text = status;
    }

    private void SetInteractionPrompt(string prompt)
    {
        if (string.IsNullOrEmpty(prompt) && pistolAlarmWarning != null) pistolAlarmWarning.gameObject.SetActive(false);
        InteractionPrompt = prompt;
        if (shownPrompt == prompt) return;
        shownPrompt = prompt;
        if (interactionText != null) interactionText.text = prompt;
    }

    private void UpdateAlarmVisuals()
    {
        float phase = Mathf.Repeat((Time.time - alarmStartedAt) * 1.4f, 1f);
        bool lit = AlarmActive && (phase < .22f || (phase >= .36f && phase < .48f));
        for (int i = 0; i < alarmLights.Length; i++)
            if (alarmLights[i] != null)
            {
                alarmLights[i].enabled = lit;
                alarmLights[i].intensity = lightLevels[i];
            }
        if (alarmLenses == null || alarmProperties == null) return;
        alarmProperties.SetColor("_BaseColor", lit ? new Color(1f, .055f, .015f) : new Color(.055f, .003f, .001f));
        foreach (Renderer lens in alarmLenses) if (lens != null) lens.SetPropertyBlock(alarmProperties);
    }
}
