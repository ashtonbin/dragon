using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>An eight-round M1911 with a working action, deliberate magazine changes and pooled .45 brass.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class FacilityPistol : MonoBehaviour
{
    public Camera aimCamera;
    public FacilityWeaponInventory inventory;
    public RetroViewmodel viewmodel;
    public Transform muzzle, slide;
    public GameObject muzzleFlash;
    public Light muzzleLight;
    public AudioSource reportSource;
    public AudioClip reportClip;
    public FacilitySurfaceImpacts surfaceImpacts;
    public Transform hammer, trigger, magazine, supportHand, reloadAssembly, ejectionPort;
    public GameObject casingPrefab;
    public AudioSource actionSource;
    public AudioClip reloadClip, emptyClip, slideClip;
    [Min(.15f)] public float fireInterval = .26f;
    [Min(1f)] public float reloadDuration = 1.8f;
    public float range = 55f;
    public int MagazineCapacity => 8;
    public int RoundsRemaining { get; private set; } = 8;
    public bool IsReloading { get; private set; }
    public bool IsSlideLocked => RoundsRemaining == 0 && !IsReloading;
    public int ShotsFired { get; private set; }
    public int Hits { get; private set; }
    public int ReloadsCompleted { get; private set; }
    public int CasingsEjected { get; private set; }
    public int EmptyClicks { get; private set; }
    public float FireInterval => fireInterval;
    public float AimBlend { get; private set; }
    public bool IsAiming => aimRequested && !IsReloading && FacilityGameSession.GameplayWeaponAllowed &&
        inventory != null && inventory.CanUse(FacilityWeaponKind.Pistol);
    private static readonly Vector3 HipPosition = new Vector3(.18f, -.21f, .42f);
    private static readonly Vector3 HipEuler = new Vector3(0f, -5f, 0f);
    private static readonly Vector3 AimPosition = new Vector3(0f, -.076f, .39f);
    private static int nextShotId = 1000000000;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Rigidbody[] casings = new Rigidbody[16];
    private readonly float[] casingBirth = new float[16];
    private Transform playerRoot;
    private RetroFPSController controller;
    private Vector3 slideHome, triggerHome, magazineHome, handHome, assemblyHome, flashScale;
    private Quaternion hammerHome, triggerRotation, magazineRotation, handRotation, assemblyRotation;
    private float nextFire, shotAt = -10f;
    private float reloadElapsed;
    private bool cursorWasLocked, wasSelected, homesCaptured, triggerNeedsRelease = true;
    private bool reloadWasEmpty, slideSoundPlayed, reloadAudioPaused;
    private bool aimRequested;
    private int nextCasing;
    private GUIStyle ammoStyle, labelStyle;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponent<Camera>();
        if (viewmodel == null) viewmodel = GetComponent<RetroViewmodel>();
        if (inventory == null) inventory = GetComponentInParent<FacilityWeaponInventory>();
        if (surfaceImpacts == null) surfaceImpacts = GetComponent<FacilitySurfaceImpacts>();
        controller = GetComponentInParent<RetroFPSController>();
        playerRoot = controller != null ? controller.transform : transform.root;
        CaptureHomes();
        ClearFlash();
        if (casingPrefab == null) return;
        for (int i = 0; i < casings.Length; i++)
        {
            GameObject casing = Instantiate(casingPrefab);
            casing.name = "Spent .45 ACP brass " + (i + 1);
            casings[i] = casing.GetComponent<Rigidbody>();
            foreach (Transform part in casing.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            foreach (Collider collider in casing.GetComponentsInChildren<Collider>(true))
                foreach (Collider owned in playerRoot.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(collider, owned);
            casing.SetActive(false);
        }
    }

    private void CaptureHomes()
    {
        if (homesCaptured) return;
        if (slide != null) slideHome = slide.localPosition;
        if (hammer != null) hammerHome = hammer.localRotation;
        if (trigger != null) { triggerHome = trigger.localPosition; triggerRotation = trigger.localRotation; }
        if (magazine != null) { magazineHome = magazine.localPosition; magazineRotation = magazine.localRotation; }
        if (supportHand != null) { handHome = supportHand.localPosition; handRotation = supportHand.localRotation; }
        if (reloadAssembly != null) { assemblyHome = reloadAssembly.localPosition; assemblyRotation = reloadAssembly.localRotation; }
        if (muzzleFlash != null) flashScale = muzzleFlash.transform.localScale;
        homesCaptured = true;
    }

    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        bool selected = inventory == null || (inventory.SelectedWeapon == FacilityWeaponKind.Pistol && inventory.IsVisible);
        bool allowed = FacilityGameSession.GameplayWeaponAllowed && Time.timeScale > 0f;
        bool held = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (!locked || !cursorWasLocked || !selected || !wasSelected || !allowed) triggerNeedsRelease = true;
        if (!held) triggerNeedsRelease = false;
        if (!selected || !allowed || !locked || IsReloading) aimRequested = false;
        else if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.isPressed) SetAiming(true);
            else if (Mouse.current.rightButton.wasReleasedThisFrame) SetAiming(false);
        }
        AnimateAim();
        if (locked && cursorWasLocked && selected && wasSelected && allowed)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryReload();
            if (!triggerNeedsRelease && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryFire();
        }
        cursorWasLocked = locked;
        wasSelected = selected;
        if (!selected)
        {
            if (IsReloading) CancelReload();
            ClearFlash();
        }
        if (IsReloading)
        {
            if (allowed && selected)
            {
                if (reloadAudioPaused && actionSource != null) actionSource.UnPause();
                reloadAudioPaused = false;
                reloadElapsed += Time.deltaTime;
            }
            else if (!reloadAudioPaused && actionSource != null)
            { actionSource.Pause(); reloadAudioPaused = true; }
            AnimateReload();
        }
        AnimateAction();
        float age = Time.time - shotAt;
        if (muzzleFlash != null && muzzleFlash.activeSelf && age > .045f) muzzleFlash.SetActive(false);
        if (muzzleLight != null && muzzleLight.enabled && age > .028f) muzzleLight.enabled = false;
        for (int i = 0; i < casings.Length; i++)
            if (casings[i] != null && casings[i].gameObject.activeSelf && Time.time - casingBirth[i] > 8f)
                casings[i].gameObject.SetActive(false);
    }

    public bool TryFire()
    {
        if (!isActiveAndEnabled || !FacilityGameSession.GameplayWeaponAllowed || Time.timeScale <= 0f || aimCamera == null || IsReloading ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.Pistol)) || Time.time < nextFire) return false;
        if (RoundsRemaining <= 0)
        {
            nextFire = Time.time + .18f;
            EmptyClicks++;
            if (actionSource != null && emptyClip != null) actionSource.PlayOneShot(emptyClip);
            return false;
        }
        CaptureHomes();
        nextFire = Time.time + fireInterval;
        shotAt = Time.time;
        RoundsRemaining--;
        ShotsFired++;
        Vector3 eye = aimCamera.transform.position;
        Vector3 direction = aimCamera.transform.forward;
        bool found = FindHit(eye, direction, range, out RaycastHit impact);
        if (muzzle != null)
        {
            Vector3 offset = muzzle.position - eye;
            if (offset.sqrMagnitude > .001f && FindHit(eye, offset.normalized, offset.magnitude, out RaycastHit blocked))
            { impact = blocked; found = true; }
        }
        if (found)
        {
            FacilityHumanoid actor = impact.collider.GetComponentInParent<FacilityHumanoid>();
            if (actor == null)
            {
                FacilityHitRegion region = impact.collider.GetComponentInParent<FacilityHitRegion>();
                if (region != null) actor = region.owner;
            }
            if (actor != null) actor.ReceivePellet(++nextShotId, impact.point, direction, impact.collider);
            else if (surfaceImpacts != null && impact.collider.attachedRigidbody == null) surfaceImpacts.Impact(impact);
            Hits++;
        }
        if (viewmodel != null) viewmodel.Kick();
        if (controller != null) controller.AddRecoil(Mathf.Lerp(2.25f, 1.7f, AimBlend), Random.Range(-.14f, .14f));
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            muzzleFlash.transform.localScale = flashScale * Random.Range(.88f, 1.12f);
            muzzleFlash.SetActive(true);
        }
        if (muzzleLight != null) muzzleLight.enabled = true;
        if (reportSource != null && reportClip != null)
        { reportSource.pitch = Random.Range(.98f, 1.02f); reportSource.PlayOneShot(reportClip); }
        EjectCasing();
        AnimateAction();
        return true;
    }

    public bool TryReload()
    {
        if (!isActiveAndEnabled || !FacilityGameSession.GameplayWeaponAllowed || Time.timeScale <= 0f || IsReloading ||
            RoundsRemaining == MagazineCapacity || (inventory != null && !inventory.CanUse(FacilityWeaponKind.Pistol))) return false;
        CaptureHomes();
        IsReloading = true;
        aimRequested = false;
        reloadElapsed = 0f;
        reloadWasEmpty = RoundsRemaining == 0;
        slideSoundPlayed = reloadAudioPaused = false;
        triggerNeedsRelease = true;
        ClearFlash();
        if (actionSource != null && reloadClip != null) { actionSource.pitch = 1f; actionSource.PlayOneShot(reloadClip); }
        return true;
    }

    private void AnimateReload()
    {
        float t = Mathf.Clamp01(reloadElapsed / reloadDuration);
        float tilt = Mathf.Sin(t * Mathf.PI);
        if (reloadAssembly != null)
        {
            reloadAssembly.localPosition = assemblyHome + new Vector3(-.065f, .075f, .025f) * tilt;
            reloadAssembly.localRotation = assemblyRotation * Quaternion.Euler(-9f * tilt, -18f * tilt, -30f * tilt);
        }
        float removed = Smooth(.085f, .25f, t) * (1f - Smooth(.53f, .67f, t));
        if (magazine != null)
        {
            magazine.localPosition = magazineHome + new Vector3(-.024f, -.18f, -.035f) * removed;
            magazine.localRotation = magazineRotation * Quaternion.Euler(-15f * removed, 0f, -8f * removed);
        }
        float reach = Smooth(.035f, .14f, t) * (1f - Smooth(.66f, .77f, t));
        float rack = reloadWasEmpty ? Smooth(.74f, .82f, t) * (1f - Smooth(.88f, .97f, t)) : 0f;
        if (supportHand != null)
        {
            supportHand.localPosition = handHome + new Vector3(.035f, -.115f, -.115f) * reach +
                new Vector3(-.010f, -.16f, -.040f) * removed + new Vector3(.030f, .070f, -.075f) * rack;
            supportHand.localRotation = handRotation * Quaternion.Euler(10f * reach - 24f * rack, 0f, 20f * reach - 12f * rack);
        }
        if (reloadWasEmpty && !slideSoundPlayed && t >= .872f)
        {
            slideSoundPlayed = true;
            if (actionSource != null && slideClip != null) actionSource.PlayOneShot(slideClip);
        }
        if (t < 1f || Time.deltaTime <= 0f || !FacilityGameSession.GameplayWeaponAllowed) return;
        RoundsRemaining = MagazineCapacity;
        ReloadsCompleted++;
        RestoreReloadPose();
        IsReloading = false;
        nextFire = Time.time + .10f;
        triggerNeedsRelease = true;
    }

    private void AnimateAction()
    {
        if (!homesCaptured) return;
        float age = Time.time - shotAt;
        float travel = age < .025f ? Mathf.Clamp01(age / .025f) : 1f - Mathf.Clamp01((age - .025f) / .075f);
        if (IsReloading && reloadWasEmpty)
        {
            float t = reloadElapsed / reloadDuration;
            travel = 1f - Smooth(.872f, .91f, t);
            travel += .10f * Smooth(.77f, .81f, t) * (1f - Smooth(.85f, .872f, t));
        }
        else if (RoundsRemaining == 0) travel = Mathf.Clamp01(age / .025f);
        if (slide != null) slide.localPosition = slideHome + Vector3.back * (.052f * travel);
        float hammerFall = IsReloading ? 0f : 1f - Smooth(.012f, .060f, age);
        if (age < 0f) hammerFall = 0f;
        if (hammer != null) hammer.localRotation = hammerHome * Quaternion.Euler(36f * hammerFall, 0f, 0f);
        if (trigger != null)
        {
            float press = IsReloading ? 0f : 1f - Smooth(.045f, .11f, age);
            if (age < 0f) press = 0f;
            trigger.localPosition = triggerHome + Vector3.back * (.0035f * press);
            trigger.localRotation = triggerRotation;
        }
    }

    public void CancelReload()
    {
        IsReloading = false;
        reloadElapsed = 0f;
        reloadAudioPaused = false;
        RestoreReloadPose();
        if (actionSource != null) actionSource.Stop();
        RequireFreshTrigger();
        ResetAim();
        ClearFlash();
        AnimateAction();
    }

    private void RestoreReloadPose()
    {
        if (!homesCaptured) return;
        if (reloadAssembly != null) { reloadAssembly.localPosition = assemblyHome; reloadAssembly.localRotation = assemblyRotation; }
        if (magazine != null) { magazine.localPosition = magazineHome; magazine.localRotation = magazineRotation; }
        if (supportHand != null) { supportHand.localPosition = handHome; supportHand.localRotation = handRotation; }
    }

    public void ResetForRun()
    {
        CaptureHomes();
        CancelReload();
        RoundsRemaining = MagazineCapacity;
        ShotsFired = Hits = ReloadsCompleted = CasingsEjected = EmptyClicks = 0;
        nextFire = 0f; shotAt = -10f; nextCasing = 0;
        reloadWasEmpty = slideSoundPlayed = false;
        foreach (Rigidbody casing in casings) if (casing != null) casing.gameObject.SetActive(false);
        AnimateAction();
    }

    /// <summary>Switching or resuming with a held button must never turn that held click into a new shot.</summary>
    public void RequireFreshTrigger()
    { cursorWasLocked = wasSelected = false; triggerNeedsRelease = true; }

    public bool SetAiming(bool value)
    {
        if (!value) { aimRequested = false; return true; }
        if (!isActiveAndEnabled || IsReloading || !FacilityGameSession.GameplayWeaponAllowed || Time.timeScale <= 0f ||
            inventory == null || !inventory.CanUse(FacilityWeaponKind.Pistol) || !OwnsActiveViewmodel()) return false;
        aimRequested = true;
        return true;
    }

    private bool OwnsActiveViewmodel()
        => viewmodel != null && inventory != null && inventory.pistolModel != null && viewmodel.authoredModel == inventory.pistolModel;

    private void AnimateAim()
    {
        if (!OwnsActiveViewmodel()) { aimRequested = false; AimBlend = 0f; return; }
        if (Time.deltaTime <= 0f) return;
        AimBlend = Mathf.Lerp(AimBlend, IsAiming ? 1f : 0f, 1f - Mathf.Exp(-12f * Time.deltaTime));
        if (AimBlend < .001f) AimBlend = 0f;
        else if (AimBlend > .999f) AimBlend = 1f;
        viewmodel.restingPosition = Vector3.Lerp(HipPosition, AimPosition, AimBlend);
        viewmodel.restingEuler = Vector3.Lerp(HipEuler, Vector3.zero, AimBlend);
    }

    private void ResetAim()
    {
        aimRequested = false; AimBlend = 0f;
        if (!OwnsActiveViewmodel()) return;
        viewmodel.restingPosition = HipPosition; viewmodel.restingEuler = HipEuler;
    }

    private static float Smooth(float from, float to, float value)
        => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

    private void EjectCasing()
    {
        if (ejectionPort == null || casings[nextCasing] == null) return;
        Rigidbody casing = casings[nextCasing];
        casing.gameObject.SetActive(false);
        casing.transform.SetPositionAndRotation(ejectionPort.position, ejectionPort.rotation * Quaternion.Euler(15f, 8f, 78f));
        casing.gameObject.SetActive(true);
        casing.isKinematic = false;
        casing.linearVelocity = ejectionPort.right * Random.Range(2.0f, 3.1f) + ejectionPort.up * Random.Range(1.1f, 1.7f) + ejectionPort.forward * .35f;
        casing.angularVelocity = new Vector3(Random.Range(14f, 24f), Random.Range(18f, 29f), -13f);
        casing.WakeUp(); casingBirth[nextCasing] = Time.time;
        nextCasing = (nextCasing + 1) % casings.Length;
        CasingsEjected++;
    }

    private bool FindHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        nearest = default;
        float closest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider is CharacterController || (playerRoot != null && collider.transform.IsChildOf(playerRoot)) || hits[i].distance >= closest) continue;
            nearest = hits[i]; closest = nearest.distance; found = true;
        }
        return found;
    }

    private void ClearFlash()
    {
        if (muzzleFlash != null) muzzleFlash.SetActive(false);
        if (muzzleLight != null) muzzleLight.enabled = false;
    }
    private void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked || !FacilityGameSession.GameplayWeaponAllowed ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.Pistol))) return;
        if (ammoStyle == null)
        {
            ammoStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 22, fontStyle = FontStyle.Bold };
            ammoStyle.normal.textColor = new Color(.88f, .86f, .77f, .85f);
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
            labelStyle.normal.textColor = new Color(.73f, .75f, .69f, .60f);
        }
        GUI.Label(new Rect(Screen.width - 162f, Screen.height - 70f, 128f, 31f), IsReloading ? "RELOADING" : RoundsRemaining.ToString("00") + " / 08", ammoStyle);
        GUI.Label(new Rect(Screen.width - 242f, Screen.height - 39f, 208f, 18f), IsReloading ? ".45 ACP" : "M1911 | RMB AIM | R RELOAD", labelStyle);
    }
    private void OnDisable() { RequireFreshTrigger(); CancelReload(); ClearFlash(); }
    private void OnDestroy() { foreach (Rigidbody casing in casings) if (casing != null) Destroy(casing.gameObject); }
}
