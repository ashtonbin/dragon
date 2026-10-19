using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Alternating short punches, with a real contact frame and solid-wall occlusion.</summary>
[DefaultExecutionOrder(100)]
public sealed class FacilityFists : MonoBehaviour
{
    public Camera aimCamera;
    public FacilityWeaponInventory inventory;
    public Transform leftHand, rightHand;
    public AudioSource impactSource;
    public AudioClip swingClip, impactClip;
    public float attackInterval = .48f;
    public float reach = 1.8f;
    public int AttacksMade { get; private set; }
    public int Hits { get; private set; }
    public float AttackInterval => attackInterval;
    private readonly RaycastHit[] contacts = new RaycastHit[64];
    private Transform playerRoot;
    private Vector3 leftHome, rightHome;
    private Quaternion leftRotation, rightRotation;
    private float nextAttack, attackAt = -10f;
    private bool left, pending, cursorWasLocked;
    private static int nextAttackId = -100000;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = GetComponent<Camera>();
        if (inventory == null) inventory = GetComponentInParent<FacilityWeaponInventory>();
        RetroFPSController controller = GetComponentInParent<RetroFPSController>();
        playerRoot = controller != null ? controller.transform : transform.root;
        if (leftHand != null) { leftHome = leftHand.localPosition; leftRotation = leftHand.localRotation; }
        if (rightHand != null) { rightHome = rightHand.localPosition; rightRotation = rightHand.localRotation; }
    }

    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (locked && cursorWasLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryPunch();
        cursorWasLocked = locked;
        float age = Time.time - attackAt;
        if (pending && age >= .10f)
        {
            pending = false;
            if (FacilityGameSession.GameplayWeaponAllowed && (inventory == null || inventory.CanUse(FacilityWeaponKind.Fists))) ResolveContact();
        }
        float extend = age < .12f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .12f)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .12f) / .24f));
        Animate(leftHand, leftHome, leftRotation, left ? extend : 0f, -1f);
        Animate(rightHand, rightHome, rightRotation, left ? 0f : extend, 1f);
    }

    public bool TryPunch()
    {
        if (!isActiveAndEnabled || !FacilityGameSession.GameplayWeaponAllowed || aimCamera == null ||
            (inventory != null && !inventory.CanUse(FacilityWeaponKind.Fists)) || Time.time < nextAttack) return false;
        nextAttack = Time.time + attackInterval;
        attackAt = Time.time;
        left = !left; pending = true; AttacksMade++;
        if (impactSource != null && swingClip != null) impactSource.PlayOneShot(swingClip, .42f);
        return true;
    }

    public bool TryAttack() => TryPunch();

    private void ResolveContact()
    {
        Vector3 origin = aimCamera.transform.position;
        Vector3 direction = aimCamera.transform.forward;
        int count = Physics.SphereCastNonAlloc(origin, .16f, direction, contacts, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        RaycastHit chosen = default;
        for (int i = 0; i < count; i++)
        {
            Collider collider = contacts[i].collider;
            if (collider == null || collider is CharacterController || collider.transform.IsChildOf(playerRoot) || contacts[i].distance >= closest) continue;
            chosen = contacts[i]; closest = contacts[i].distance;
        }
        if (chosen.collider == null) return;
        FacilityHumanoid actor = chosen.collider.GetComponentInParent<FacilityHumanoid>();
        if (actor == null)
        {
            FacilityHitRegion region = chosen.collider.GetComponentInParent<FacilityHitRegion>();
            if (region != null) actor = region.owner;
        }
        if (actor == null) return;
        // The wider knuckle sweep cannot reach around an occluding wall beside the aim ray.
        if (Physics.Linecast(origin, chosen.point, out RaycastHit obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            obstruction.collider != null && obstruction.collider != chosen.collider &&
            !obstruction.collider.transform.IsChildOf(playerRoot) &&
            obstruction.collider.GetComponentInParent<FacilityHumanoid>() != actor) return;
        actor.ReceiveMelee(--nextAttackId, chosen.point, direction, chosen.collider);
        Hits++;
        if (impactSource != null && impactClip != null) impactSource.PlayOneShot(impactClip);
    }

    private static void Animate(Transform hand, Vector3 home, Quaternion rotation, float amount, float side)
    {
        if (hand == null) return;
        hand.localPosition = home + new Vector3(-side * .12f, .07f, .37f) * amount;
        hand.localRotation = rotation * Quaternion.Euler(-14f * amount, -side * 20f * amount, -side * 24f * amount);
    }

    private void OnDisable() { cursorWasLocked = false; pending = false; }
}
