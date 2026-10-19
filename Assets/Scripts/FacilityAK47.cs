using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A 600 RPM rifle with a moving action, a rocking magazine change and pooled physical brass.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class FacilityAK47 : MonoBehaviour
{
    public Camera aimCamera;
    public FacilityWeaponInventory inventory;
    public RetroViewmodel viewmodel;
    public FacilitySurfaceImpacts surfaceImpacts;
    public Transform muzzle, bolt, ejectionPort, reloadAssembly, magazine, supportHand;
    public GameObject muzzleFlash, casingPrefab;
    public Light muzzleLight;
    public AudioSource reportSource, actionSource;
    public AudioClip reportClip, reloadClip;
    [Min(.075f)] public float fireInterval = .10f;
    [Min(1f)] public float reloadDuration = 2.2f;
    public float range = 95f;
    public int MagazineCapacity => 30;
    public int RoundsRemaining { get; private set; } = 30;
    public int ShotsFired { get; private set; }
    public int Hits { get; private set; }
    public int ReloadsCompleted { get; private set; }
    public int CasingsEjected { get; private set; }
    public bool IsReloading { get; private set; }
    public float FireInterval => fireInterval;

    private static int nextShotId = 1200000000;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Rigidbody[] casings = new Rigidbody[24];
    private readonly float[] casingBirth = new float[24];
    private Transform playerRoot;
    private RetroFPSController controller;
    private Vector3 boltHome, magazineHome, handHome, flashScale;
    private Quaternion magazineRotation, handRotation, assemblyRotation;
    private float nextFire, shotAt = -10f, reloadAt, lastTriggerReleased;
    private bool cursorWasLocked, homesCaptured, triggerNeedsRelease;
    private int nextCasing, burstShots;
    private GUIStyle ammoStyle, labelStyle;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponent<Camera>();
        if (inventory == null) inventory = GetComponentInParent<FacilityWeaponInventory>();
        if (viewmodel == null) viewmodel = GetComponent<RetroViewmodel>();
        if (surfaceImpacts == null) surfaceImpacts = GetComponent<FacilitySurfaceImpacts>();
        controller = GetComponentInParent<RetroFPSController>();
        playerRoot = controller != null ? controller.transform : transform.root;
        CaptureHomes();
        ClearFlash();
        if (casingPrefab == null) return;
        for (int i = 0; i < casings.Length; i++)
        {
            GameObject casing = Instantiate(casingPrefab);
            casing.name = "Spent 7.62 brass " + (i + 1);
            casings[i] = casing.GetComponent<Rigidbody>();
            casing.SetActive(false);
        }
    }

    private void CaptureHomes()
    {
        if (homesCaptured) return;
        if (bolt != null) boltHome = bolt.localPosition;
        if (magazine != null) { magazineHome = magazine.localPosition; magazineRotation = magazine.localRotation; }
        if (supportHand != null) { handHome = supportHand.localPosition; handRotation = supportHand.localRotation; }
        if (reloadAssembly != null) assemblyRotation = reloadAssembly.localRotation;
        if (muzzleFlash != null) flashScale = muzzleFlash.transform.localScale;
        homesCaptured = true;
    }

    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (!locked || !cursorWasLocked) triggerNeedsRelease = true;
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed) triggerNeedsRelease = false;
        // A pause blocks firing but keeps this weapon selected, so its magazine change resumes in place.
        bool selected = inventory == null || (inventory.SelectedWeapon == FacilityWeaponKind.AK47 && inventory.IsVisible);
        if (locked && cursorWasLocked && selected && FacilityGameSession.GameplayWeaponAllowed)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryReload();
            if (!triggerNeedsRelease && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                if (RoundsRemaining == 0) TryReload();
                else TryFire();
            }
            else { lastTriggerReleased = Time.time; burstShots = 0; }
        }
        cursorWasLocked = locked;
        if (!selected && IsReloading) CancelReload();
        if (IsReloading) AnimateReload();
        float age = Time.time - shotAt;
        if (muzzleFlash != null && muzzleFlash.activeSelf && age > .032f) muzzleFlash.SetActive(false);
        if (muzzleLight != null && muzzleLight.enabled && age > .026f) muzzleLight.enabled = false;
        if (bolt != null)
        {
            float travel = age < .025f ? Mathf.Clamp01(age / .025f) : 1f - Mathf.Clamp01((age - .025f) / .052f);
            if (IsReloading)
            {
                float t = (Time.time - reloadAt) / reloadDuration;
                if (t > .78f && t < .96f) travel = Mathf.Sin((t - .78f) / .18f * Mathf.PI);
            }
            bolt.localPosition = boltHome + Vector3.back * (.082f * travel);
        }
        for (int i = 0; i < casings.Length; i++)
            if (casings[i] != null && casings[i].gameObject.activeSelf && Time.time - casingBirth[i] > 8f)
                casings[i].gameObject.SetActive(false);
    }

    public bool TryFire()
    {
        if (!isActiveAndEnabled || !FacilityGameSession.GameplayWeaponAllowed || aimCamera == null || IsReloading ||
            RoundsRemaining <= 0 || Time.time < nextFire || (inventory != null && !inventory.CanUse(FacilityWeaponKind.AK47))) return false;
        CaptureHomes();
        nextFire = Time.time + fireInterval;
        shotAt = Time.time;
        RoundsRemaining--;
        ShotsFired++;
        if (Time.time - lastTriggerReleased > .3f && burstShots == 0) lastTriggerReleased = Time.time;
        burstShots++;
        // The first round is accurate; a sustained burst opens gradually while the shoulder kick moves the sights.
        float spread = Mathf.Lerp(.10f, .65f, Mathf.Clamp01((burstShots - 1) / 10f));
        Vector2 deviation = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
        Vector3 direction = (aimCamera.transform.forward + aimCamera.transform.right * deviation.x + aimCamera.transform.up * deviation.y).normalized;
        Vector3 eye = aimCamera.transform.position;
        bool found = FindHit(eye, direction, range, out RaycastHit impact);
        if (muzzle != null)
        {
            Vector3 offset = muzzle.position - eye;
            if (offset.sqrMagnitude > .001f && FindHit(eye, offset.normalized, offset.magnitude, out RaycastHit blocked))
            { found = true; impact = blocked; }
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
        if (controller != null) controller.AddRecoil(1.55f, Random.Range(-.30f, .30f));
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            muzzleFlash.transform.localScale = flashScale * Random.Range(.78f, 1.16f);
            muzzleFlash.SetActive(true);
        }
        if (muzzleLight != null) muzzleLight.enabled = true;
        if (reportSource != null && reportClip != null)
        {
            reportSource.pitch = Random.Range(.975f, 1.025f);
            reportSource.PlayOneShot(reportClip);
        }
        EjectCasing();
        return true;
    }

    public bool TryReload()
    {
        if (!isActiveAndEnabled || !FacilityGameSession.GameplayWeaponAllowed || IsReloading || RoundsRemaining == MagazineCapacity ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.AK47))) return false;
        CaptureHomes();
        IsReloading = true;
        reloadAt = Time.time;
        burstShots = 0;
        ClearFlash();
        if (actionSource != null && reloadClip != null) actionSource.PlayOneShot(reloadClip);
        return true;
    }

    private void AnimateReload()
    {
        float t = Mathf.Clamp01((Time.time - reloadAt) / reloadDuration);
        float tilt = Mathf.Sin(t * Mathf.PI);
        if (reloadAssembly != null) reloadAssembly.localRotation = assemblyRotation * Quaternion.Euler(8f * tilt, -9f * tilt, -23f * tilt);
        float removed = t < .20f ? Mathf.SmoothStep(0f, 1f, t / .20f) : t < .55f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - .55f) / .23f);
        if (magazine != null)
        {
            magazine.localPosition = magazineHome + new Vector3(-.035f, -.27f, -.07f) * removed;
            magazine.localRotation = magazineRotation * Quaternion.Euler(-28f * removed, 0f, -8f * removed);
        }
        if (supportHand != null)
        {
            supportHand.localPosition = handHome + new Vector3(.035f, -.16f, -.22f) * removed;
            supportHand.localRotation = handRotation * Quaternion.Euler(12f * removed, 0f, 24f * removed);
        }
        if (t < 1f) return;
        RoundsRemaining = MagazineCapacity;
        ReloadsCompleted++;
        CancelReload();
        nextFire = Time.time + .09f;
    }

    public void CancelReload()
    {
        IsReloading = false;
        if (!homesCaptured) return;
        if (reloadAssembly != null) reloadAssembly.localRotation = assemblyRotation;
        if (magazine != null) { magazine.localPosition = magazineHome; magazine.localRotation = magazineRotation; }
        if (supportHand != null) { supportHand.localPosition = handHome; supportHand.localRotation = handRotation; }
        if (actionSource != null) actionSource.Stop();
    }

    public void ResetForRun()
    {
        CancelReload();
        RoundsRemaining = MagazineCapacity;
        nextFire = 0f;
        shotAt = -10f;
        burstShots = 0;
        triggerNeedsRelease = true;
        ClearFlash();
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
            if (collider == null || collider is CharacterController || collider.transform.IsChildOf(playerRoot) || hits[i].distance >= closest) continue;
            nearest = hits[i]; closest = nearest.distance; found = true;
        }
        return found;
    }

    private void EjectCasing()
    {
        if (ejectionPort == null || casings[nextCasing] == null) return;
        Rigidbody casing = casings[nextCasing];
        casing.gameObject.SetActive(false);
        casing.transform.SetPositionAndRotation(ejectionPort.position, ejectionPort.rotation * Quaternion.Euler(12f, 12f, 75f));
        casing.gameObject.SetActive(true);
        casing.linearVelocity = ejectionPort.right * Random.Range(2.5f, 3.9f) + ejectionPort.up * Random.Range(1.3f, 2.1f) + ejectionPort.forward * .6f;
        casing.angularVelocity = new Vector3(Random.Range(12f, 22f), Random.Range(18f, 27f), -13f);
        casingBirth[nextCasing] = Time.time;
        nextCasing = (nextCasing + 1) % casings.Length;
        CasingsEjected++;
    }

    private void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked || !FacilityGameSession.GameplayWeaponAllowed ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.AK47))) return;
        if (ammoStyle == null)
        {
            ammoStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 22, fontStyle = FontStyle.Bold };
            ammoStyle.normal.textColor = new Color(.88f, .86f, .77f, .85f);
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
            labelStyle.normal.textColor = new Color(.73f, .75f, .69f, .60f);
        }
        GUI.Label(new Rect(Screen.width - 162f, Screen.height - 70f, 128f, 31f), IsReloading ? "RELOADING" : RoundsRemaining.ToString("00") + " / 30", ammoStyle);
        GUI.Label(new Rect(Screen.width - 162f, Screen.height - 39f, 128f, 18f), IsReloading ? "7.62 × 39" : "AK-47    ·    R  RELOAD", labelStyle);
    }

    private void ClearFlash()
    {
        if (muzzleFlash != null) muzzleFlash.SetActive(false);
        if (muzzleLight != null) muzzleLight.enabled = false;
    }

    private void OnDisable() { cursorWasLocked = false; triggerNeedsRelease = true; CancelReload(); ClearFlash(); }
    private void OnDestroy() { foreach (Rigidbody casing in casings) if (casing != null) Destroy(casing.gameObject); }
}
