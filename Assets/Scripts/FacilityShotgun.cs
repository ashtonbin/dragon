using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Twelve independently swept buckshot pellets, one deliberate pump cycle per trigger press.</summary>
[DefaultExecutionOrder(100)]
public sealed class FacilityShotgun : MonoBehaviour
{
    public Camera aimCamera;
    public RetroViewmodel viewmodel;
    public Transform muzzle;
    public Transform pump;
    public Transform ejectionPort;
    public GameObject muzzleFlash;
    public Transform muzzleSmoke;
    public Light muzzleLight;
    public GameObject shellPrefab;
    public AudioSource reportSource;
    public AudioSource actionSource;
    public AudioClip reportClip;
    public AudioClip actionClip;
    public FacilitySurfaceImpacts surfaceImpacts;
    [Min(.6f)] public float fireInterval = .95f;
    [Range(.1f, 6f)] public float spreadDegrees = 3f;
    [Range(120f, 180f)] public float pelletSpeed = 160f;
    public float range = 42f;
    public bool showReticle = true;

    public int ShotsFired { get; private set; }
    public int PelletsPerShot => 12;
    public int PelletsLaunched { get; private set; }
    public int ActivePellets { get; private set; }
    public int PelletImpactCount { get; private set; }
    public int PumpCyclesPlayed { get; private set; }
    public float LastPumpStartedAt { get; private set; }
    public Vector3[] LastShotDirections { get; } = new Vector3[12];
    public float FireInterval => fireInterval;
    private const int PelletCapacity = 96;
    private struct Pellet
    {
        public Vector3 position, velocity;
        public float age, travelled;
        public int shotId;
        public bool active;
    }
    private static int nextShotId;
    private readonly Pellet[] pellets = new Pellet[PelletCapacity];
    private readonly RaycastHit[] rayHits = new RaycastHit[64];
    private readonly Rigidbody[] shellPool = new Rigidbody[8];
    private readonly float[] shellBirth = new float[8];
    private Transform playerRoot;
    private RetroFPSController controller;
    private float nextFireTime;
    private float cycleStart = -10f;
    private bool ejected = true;
    private bool actionStarted = true;
    private bool cursorWasLocked;
    private int shellIndex, nextPellet;
    private uint randomState = 941607u;
    private Vector3 flashScale;
    private FacilityWeaponInventory inventory;
    private Vector3 pumpHome;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponent<Camera>();
        if (viewmodel == null) viewmodel = GetComponent<RetroViewmodel>();
        controller = GetComponentInParent<RetroFPSController>();
        inventory = GetComponentInParent<FacilityWeaponInventory>();
        if (pump != null) pumpHome = pump.localPosition;
        CharacterController capsule = GetComponentInParent<CharacterController>();
        playerRoot = capsule != null ? capsule.transform : transform.root;
        if (muzzleFlash != null) { flashScale = muzzleFlash.transform.localScale; muzzleFlash.SetActive(false); }
        if (muzzleSmoke != null) muzzleSmoke.gameObject.SetActive(false);
        if (muzzleLight != null) muzzleLight.enabled = false;
        if (surfaceImpacts == null) surfaceImpacts = GetComponent<FacilitySurfaceImpacts>();
        if (shellPrefab == null) return;
        for (int i = 0; i < shellPool.Length; i++)
        {
            GameObject shell = Instantiate(shellPrefab);
            shell.name = "Spent shotgun shell " + (i + 1);
            shellPool[i] = shell.GetComponent<Rigidbody>();
            shell.SetActive(false);
        }
    }

    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (FacilityGameSession.GameplayWeaponAllowed && locked && cursorWasLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryFire();
        cursorWasLocked = locked;
        float age = Time.time - cycleStart;
        if (muzzleFlash != null && muzzleFlash.activeSelf)
        {
            if (age > .085f) muzzleFlash.SetActive(false);
            else muzzleFlash.transform.localScale = flashScale * Mathf.Lerp(.72f, 1.14f, Mathf.Clamp01(age / .085f));
        }
        if (muzzleLight != null && muzzleLight.enabled && age > .045f) muzzleLight.enabled = false;
        if (muzzleSmoke != null && muzzleSmoke.gameObject.activeSelf)
        {
            if (age > .34f) muzzleSmoke.gameObject.SetActive(false);
            else
            {
                float t = Mathf.Clamp01(age / .34f);
                muzzleSmoke.localPosition = new Vector3(0f, t * .09f, .09f + t * .20f);
                muzzleSmoke.localScale = Vector3.one * Mathf.Lerp(.35f, 1.45f, t);
            }
        }
        float travel = age < .24f ? 0f : age < .43f ? Mathf.SmoothStep(0f, 1f, (age - .24f) / .19f)
            : age < .53f ? 1f : age < .76f ? 1f - Mathf.SmoothStep(0f, 1f, (age - .53f) / .23f) : 0f;
        if (pump != null) pump.localPosition = pumpHome + Vector3.back * (.135f * travel);
        if (!actionStarted && age >= .24f)
        {
            actionStarted = true;
            if (actionSource != null && actionClip != null)
            {
                actionSource.PlayOneShot(actionClip);
                PumpCyclesPlayed++;
                LastPumpStartedAt = Time.time;
            }
        }
        if (!ejected && age >= .40f)
        {
            ejected = true;
            EjectShell();
        }
        for (int i = 0; i < shellPool.Length; i++)
            if (shellPool[i] != null && shellPool[i].gameObject.activeSelf && Time.time - shellBirth[i] > 18f)
                shellPool[i].gameObject.SetActive(false);
    }

    /// <summary>Launches physical flight records; collision and damage happen only when each pellet reaches a surface.</summary>
    public bool TryFire()
    {
        if (!FacilityGameSession.GameplayWeaponAllowed || !isActiveAndEnabled || aimCamera == null || Time.time < nextFireTime ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.Shotgun))) return false;
        nextFireTime = Time.time + fireInterval;
        cycleStart = Time.time;
        ejected = false;
        actionStarted = false;
        ShotsFired++;
        int shotId = ++nextShotId;
        Vector3 eye = aimCamera.transform.position;
        Vector3 aim = aimCamera.transform.forward;
        Vector3 origin = muzzle != null ? muzzle.position : eye;
        // A barrel pressed through a wall must never fire through it.
        Vector3 barrelOffset = origin - eye;
        if (barrelOffset.sqrMagnitude > .001f && FindFirstHit(eye, barrelOffset.normalized, barrelOffset.magnitude, out RaycastHit obstruction))
            origin = obstruction.point - barrelOffset.normalized * .025f;
        Vector3 target = FindFirstHit(eye, aim, range, out RaycastHit aimedHit) ? aimedHit.point : eye + aim * range;
        if (Vector3.Dot(target - origin, aim) < .08f) origin = eye;
        Vector3 forward = (target - origin).normalized;
        Vector3 right = Vector3.Cross(aimCamera.transform.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;
        float cone = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
        float phase = Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < PelletsPerShot; i++)
        {
            // Equal-area radial strata fill the disk. No empty center or fixed ring masquerading as buckshot.
            float radius = i == 0 ? 0f : Mathf.Sqrt((i - .65f + Range(0f, .3f)) / 11f) * cone;
            float angle = phase + i * 2.39996323f + Range(-.16f, .16f);
            Vector3 direction = (forward + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius)).normalized;
            LastShotDirections[i] = direction;
            int index = nextPellet++ % PelletCapacity;
            if (!pellets[index].active) ActivePellets++;
            pellets[index] = new Pellet { position = origin, velocity = direction * pelletSpeed, shotId = shotId, active = true };
            PelletsLaunched++;
        }
        // Capture aim before adding the kick. Mouse look owns base orientation; recoil is a temporary additive offset.
        if (viewmodel != null) viewmodel.Kick();
        if (controller != null) controller.AddRecoil(3.8f, Range(-.42f, .42f));
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Range(0f, 360f));
            muzzleFlash.transform.localScale = flashScale;
            muzzleFlash.SetActive(true);
        }
        if (muzzleSmoke != null) muzzleSmoke.gameObject.SetActive(true);
        if (muzzleLight != null) muzzleLight.enabled = true;
        if (reportSource != null && reportClip != null)
        {
            reportSource.pitch = Range(.97f, 1.025f);
            reportSource.PlayOneShot(reportClip);
        }
        return true;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < pellets.Length; i++)
        {
            if (!pellets[i].active) continue;
            Pellet pellet = pellets[i];
            Vector3 delta = pellet.velocity * dt + Physics.gravity * (.5f * dt * dt);
            float distance = Mathf.Min(delta.magnitude, Mathf.Max(0f, range - pellet.travelled));
            if (distance > 0f && FindFirstHit(pellet.position, delta.normalized, distance, out RaycastHit hit))
            {
                FacilityHumanoid humanoid = hit.collider.GetComponentInParent<FacilityHumanoid>();
                if (humanoid == null)
                {
                    FacilityHitRegion region = hit.collider.GetComponentInParent<FacilityHitRegion>();
                    if (region != null) humanoid = region.owner;
                }
                if (humanoid != null) humanoid.ReceivePellet(pellet.shotId, hit.point, pellet.velocity.normalized, hit.collider);
                else if (surfaceImpacts != null && hit.collider.attachedRigidbody == null) surfaceImpacts.Impact(hit);
                PelletImpactCount++;
                pellet.active = false;
            }
            else
            {
                pellet.position += delta.normalized * distance;
                pellet.velocity += Physics.gravity * dt;
                pellet.travelled += distance;
                pellet.age += dt;
                if (pellet.age >= .30f || pellet.travelled >= range) pellet.active = false;
            }
            if (!pellet.active) ActivePellets--;
            pellets[i] = pellet;
        }
    }

    private bool FindFirstHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, rayHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        closest = default;
        float nearest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = rayHits[i].collider;
            if (collider == null || collider is CharacterController) continue;
            if (collider.transform.IsChildOf(playerRoot) || rayHits[i].distance >= nearest) continue;
            closest = rayHits[i];
            nearest = closest.distance;
            found = true;
        }
        return found;
    }

    private void EjectShell()
    {
        if (ejectionPort == null) return;
        Rigidbody shell = shellPool[shellIndex];
        if (shell == null) return;
        shell.gameObject.SetActive(false);
        shell.transform.SetPositionAndRotation(ejectionPort.position, ejectionPort.rotation * Quaternion.Euler(10f, 0f, 88f));
        shell.gameObject.SetActive(true);
        shell.linearVelocity = ejectionPort.right * 2.2f + ejectionPort.up * 1.35f + ejectionPort.forward * .35f;
        shell.angularVelocity = new Vector3(9f, 16f, -11f);
        shellBirth[shellIndex] = Time.time;
        shellIndex = (shellIndex + 1) % shellPool.Length;
    }

    private float Range(float min, float max)
    {
        randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
        return min + (randomState & 0xffffff) / 16777216f * (max - min);
    }

    private void OnGUI()
    {
        if (!showReticle || !FacilityGameSession.GameplayWeaponAllowed || Cursor.lockState != CursorLockMode.Locked ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.Shotgun))) return;
        Color previous = GUI.color;
        GUI.color = new Color(.84f, .87f, .85f, .46f);
        GUI.DrawTexture(new Rect(Screen.width * .5f - 1f, Screen.height * .5f - 1f, 2f, 2f), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void OnDisable()
    {
        actionStarted = ejected = true;
        cursorWasLocked = false;
        if (muzzleFlash != null) muzzleFlash.SetActive(false);
        if (muzzleSmoke != null) muzzleSmoke.gameObject.SetActive(false);
        if (muzzleLight != null) muzzleLight.enabled = false;
        if (pump != null) pump.localPosition = pumpHome;
        for (int i = 0; i < pellets.Length; i++) pellets[i].active = false;
        ActivePellets = 0;
    }

    private void OnDestroy()
    {
        foreach (Rigidbody shell in shellPool) if (shell != null) Destroy(shell.gameObject);
    }
}
