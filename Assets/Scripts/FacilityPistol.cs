using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Semi-automatic handgun. Damage follows the camera ray, including any barrel obstruction.</summary>
[DefaultExecutionOrder(100)]
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
    public float fireInterval = .26f;
    public float range = 55f;
    public int ShotsFired { get; private set; }
    public int Hits { get; private set; }
    public float FireInterval => fireInterval;
    private static int nextShotId = 1000000000;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private Transform playerRoot;
    private RetroFPSController controller;
    private Vector3 slideHome;
    private float nextFire, shotAt = -10f;
    private bool cursorWasLocked;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponent<Camera>();
        if (viewmodel == null) viewmodel = GetComponent<RetroViewmodel>();
        if (inventory == null) inventory = GetComponentInParent<FacilityWeaponInventory>();
        controller = GetComponentInParent<RetroFPSController>();
        playerRoot = controller != null ? controller.transform : transform.root;
        if (slide != null) slideHome = slide.localPosition;
        ClearFlash();
    }

    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (locked && cursorWasLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryFire();
        cursorWasLocked = locked;
        float age = Time.time - shotAt;
        if (muzzleFlash != null && muzzleFlash.activeSelf && age > .045f) muzzleFlash.SetActive(false);
        if (muzzleLight != null && muzzleLight.enabled && age > .028f) muzzleLight.enabled = false;
        if (slide != null)
        {
            float movement = age < .028f ? Mathf.Clamp01(age / .028f) : 1f - Mathf.Clamp01((age - .028f) / .090f);
            slide.localPosition = slideHome + Vector3.back * (.055f * movement);
        }
    }

    public bool TryFire()
    {
        if (!isActiveAndEnabled || !FacilityGameSession.GameplayWeaponAllowed || aimCamera == null ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.Pistol)) || Time.time < nextFire) return false;
        nextFire = Time.time + fireInterval;
        shotAt = Time.time;
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
        if (controller != null) controller.AddRecoil(1.85f, Random.Range(-.17f, .17f));
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            muzzleFlash.SetActive(true);
        }
        if (muzzleLight != null) muzzleLight.enabled = true;
        if (reportSource != null && reportClip != null)
        { reportSource.pitch = Random.Range(.98f, 1.02f); reportSource.PlayOneShot(reportClip); }
        return true;
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

    private void ClearFlash()
    {
        if (muzzleFlash != null) muzzleFlash.SetActive(false);
        if (muzzleLight != null) muzzleLight.enabled = false;
    }
    private void OnDisable() { cursorWasLocked = false; ClearFlash(); }
}
