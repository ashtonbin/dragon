using UnityEngine;

/// <summary>Enter the actual driver's seat before completing the heist. The vehicle remains parked.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class FacilityVehicleExtraction : MonoBehaviour
{
    public FacilityStoryDirector director;
    public FacilityDoor driverDoor;
    public Transform seatPose;
    public Transform eyePose;
    [Min(.25f)] public float boardingSeconds = .75f;

    public bool IsBoarding { get; private set; }
    public bool IsSeated { get; private set; }
    public float BoardingProgress { get; private set; }

    private RetroFPSController player;
    private CharacterController motor;
    private FacilityPlayerHealth health;
    private FacilityWeaponInventory inventory;
    private Transform eyes;
    private FacilityStoryInteractable extractionTarget;
    private Vector3 startRootPosition, startEyePosition, startEyeLocalPosition;
    private Quaternion startRootRotation, startEyeRotation, startEyeLocalRotation;
    private bool playerWasEnabled, motorWasEnabled, inventoryWasVisible, controlsCaptured;
    private bool closingDoor, completionReported;
    private float elapsed, doorElapsed;

    public bool CanBoard(out string reason)
    {
        reason = "";
        if (director == null || driverDoor == null || seatPose == null || eyePose == null)
        { reason = "The driver's seat is unavailable."; return false; }
        if (IsBoarding || IsSeated) return false;
        if (!director.HasBegun || director.Stage == FacilityStoryStage.Completed) return false;
        if (!director.HasCash || !director.HasGold)
        {
            reason = !director.HasCash && !director.HasGold ? "Retrieve both the cash and gold before leaving." :
                !director.HasCash ? "Retrieve the vault cash before leaving." : "Retrieve the vault gold before leaving.";
            return false;
        }
        if (director.Stage != FacilityStoryStage.ReturnToSedan) return false;
        if (!director.ExtractionUnlocked)
        { reason = "Eliminate the entire SWAT team before leaving."; return false; }
        if (!driverDoor.IsOpen || driverDoor.IsMoving)
        { reason = "Open the driver door, then get into the driver's seat."; return false; }
        RetroFPSController candidate = director.player;
        FacilityPlayerHealth candidateHealth = candidate != null ? candidate.GetComponent<FacilityPlayerHealth>() : null;
        if (candidate == null || (candidateHealth != null && candidateHealth.IsDead)) return false;
        // The passenger window, roof and trunk must never be alternate extraction entrances.
        Vector3 local = transform.InverseTransformPoint(candidate.transform.position);
        Vector3 seatLocal = transform.InverseTransformPoint(seatPose.position);
        if (local.x >= -.9f || Mathf.Abs(local.z - seatLocal.z) > 1.15f || local.y < -.35f || local.y > .8f ||
            Vector3.Distance(candidate.transform.position, seatPose.position) > 2.5f)
        { reason = "Enter through the open driver door."; return false; }
        return true;
    }

    public bool BeginBoarding(FacilityStoryDirector source, FacilityStoryInteractable item, out string reason)
    {
        reason = "";
        if (director == null || source != director || item == null || item != director.sedanExit || item.kind != FacilityStoryInteractionKind.Sedan || item.Consumed)
            return false;
        if (!CanBoard(out reason)) return false;
        FacilityGameSession session = FacilityGameSession.Instance;
        if (Time.timeScale <= 0f || !FacilityGameSession.GameplayInputAllowed ||
            (session != null && (session.CurrentMode != FacilityGameMode.Story || session.CurrentState != FacilityGameState.Playing)))
        { reason = "Resume the mission to enter the car."; return false; }

        player = director.player;
        motor = player.GetComponent<CharacterController>();
        health = player.GetComponent<FacilityPlayerHealth>();
        inventory = director.inventory;
        Camera camera = player.GetComponentInChildren<Camera>(true);
        eyes = camera != null ? camera.transform : player.cameraPivot;
        if (eyes == null) { reason = "The driver's view is unavailable."; return false; }

        extractionTarget = item;
        startRootPosition = player.transform.position; startRootRotation = player.transform.rotation;
        startEyePosition = eyes.position; startEyeRotation = eyes.rotation;
        startEyeLocalPosition = eyes.localPosition; startEyeLocalRotation = eyes.localRotation;
        playerWasEnabled = player.enabled; motorWasEnabled = motor != null && motor.enabled;
        inventoryWasVisible = inventory != null && inventory.IsVisible;
        controlsCaptured = true;
        IsBoarding = true; IsSeated = false; BoardingProgress = 0f;
        elapsed = doorElapsed = 0f; closingDoor = completionReported = false;
        player.ResetRecoil(); player.enabled = false;
        if (motor != null) motor.enabled = false;
        if (inventory != null) inventory.SetVisible(false);
        session?.SetVehicleInputLocked(true);
        return true;
    }

    private void Update()
    {
        if (!IsBoarding) return;
        FacilityGameSession session = FacilityGameSession.Instance;
        // Death wins over a paused simulation, including a fatal hit on the final boarding frame.
        if (player == null || director == null || (health != null && health.IsDead) ||
            (session != null && session.CurrentState == FacilityGameState.Dead))
        { AbortBoarding(); return; }
        if (Time.deltaTime <= 0f || (session != null && session.CurrentState == FacilityGameState.Paused)) return;
        if (session != null && session.CurrentState != FacilityGameState.Playing)
        { AbortBoarding(); return; }

        if (!closingDoor)
        {
            elapsed += Time.deltaTime;
            BoardingProgress = Mathf.Clamp01(elapsed / Mathf.Max(.25f, boardingSeconds));
            PoseBoarding(BoardingProgress);
            if (BoardingProgress < 1f) return;
            IsSeated = true;
            // The player capsule is disabled inside the seat; another person in the swing still blocks closing.
            closingDoor = driverDoor != null && driverDoor.IsOpen && !driverDoor.IsMoving && driverDoor.TryToggle();
            if (closingDoor) return;
        }
        else
        {
            PoseBoarding(1f);
            doorElapsed += Time.deltaTime;
            if (driverDoor != null && driverDoor.IsMoving && doorElapsed < driverDoor.travelSeconds + .25f) return;
        }
        FinishBoarding();
    }

    private void PoseBoarding(float progress)
    {
        // Lower the viewpoint through the clear door aperture, then slide into the bucket seat.
        Vector3 seatLocal = transform.InverseTransformPoint(seatPose.position);
        Vector3 eyeLocal = transform.InverseTransformPoint(eyePose.position);
        Vector3 thresholdRoot = transform.TransformPoint(new Vector3(-1.015f, seatLocal.y, seatLocal.z));
        Vector3 thresholdEye = transform.TransformPoint(new Vector3(-1.015f, eyeLocal.y - .035f, eyeLocal.z));
        float first = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .45f));
        float second = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - .45f) / .55f));
        Vector3 rootPosition = progress < .45f ? Vector3.Lerp(startRootPosition, thresholdRoot, first) : Vector3.Lerp(thresholdRoot, seatPose.position, second);
        Vector3 eyePosition = progress < .45f ? Vector3.Lerp(startEyePosition, thresholdEye, first) : Vector3.Lerp(thresholdEye, eyePose.position, second);
        float turn = Mathf.SmoothStep(0f, 1f, progress);
        player.transform.SetPositionAndRotation(rootPosition, Quaternion.Slerp(startRootRotation, seatPose.rotation, turn));
        eyes.SetPositionAndRotation(eyePosition, Quaternion.Slerp(startEyeRotation, eyePose.rotation, turn));
        Physics.SyncTransforms();
    }

    private void FinishBoarding()
    {
        if (completionReported) return;
        if (health != null && health.IsDead) { AbortBoarding(); return; }
        completionReported = true;
        IsBoarding = false;
        if (!director.CompleteVehicleExtraction(this, extractionTarget)) AbortBoarding();
    }

    private void AbortBoarding()
    {
        IsBoarding = IsSeated = false; BoardingProgress = 0f;
        if (!controlsCaptured) return;
        controlsCaptured = false;
        if (player != null)
        {
            player.transform.SetPositionAndRotation(startRootPosition, startRootRotation);
            if (eyes != null) { eyes.localPosition = startEyeLocalPosition; eyes.localRotation = startEyeLocalRotation; }
            player.ResetRecoil(); player.enabled = playerWasEnabled;
            if (motor != null) motor.enabled = motorWasEnabled;
        }
        FacilityGameSession session = FacilityGameSession.Instance;
        if (session != null) session.SetVehicleInputLocked(false);
        else if (inventory != null) inventory.SetVisible(inventoryWasVisible && (health == null || !health.IsDead));
        Physics.SyncTransforms();
    }

    private void OnDisable() { if (IsBoarding) AbortBoarding(); }
}
