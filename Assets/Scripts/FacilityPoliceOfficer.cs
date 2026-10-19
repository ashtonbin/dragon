using UnityEngine;
using UnityEngine.AI;

/// <summary>Approaches through the navigation mesh, then visibly commits to an avoidable straight shot.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(FacilityHumanoid))]
public sealed class FacilityPoliceOfficer : MonoBehaviour
{
    public FacilityHumanoid actor;
    public Transform gunTip;
    public GameObject muzzleFlash, aimWarning;
    public FacilityPoliceProjectiles projectileSystem;
    public FacilityPlayerHealth target;
    public AudioSource gunSource;
    public AudioClip gunReport;
    [Min(.1f)] public float walkSpeed = 2.6f;
    [Min(2f)] public float engagementRange = 18f;
    [Min(1f)] public float holdDistance = 7f;
    [Min(.2f)] public float aimWindup = .42f;
    [Min(.4f)] public float shotInterval = 1.65f;
    public bool IsAiming { get; private set; }
    public int ShotsFired { get; private set; }
    public int RepathCount { get; private set; }
    public int CornerCount => cornerCount;
    public Vector3 CurrentDestination => cornerIndex < cornerCount ? corners[cornerIndex] : transform.position;
    public string NavigationStatus { get; private set; } = "Idle";
    public float ShotPulse => Mathf.Clamp01(1f - (Time.time - firedAt) / .2f);
    public Vector3 LockedAimPoint { get; private set; }

    private readonly RaycastHit[] sightHits = new RaycastHit[96];
    private readonly Vector3[] corners = new Vector3[48];
    private NavMeshPath path;
    private FacilityHitRegion gunHand;
    private int cornerCount, cornerIndex;
    private float nextRepath, nextShot, fireAt, firedAt = float.NegativeInfinity;

    private void Awake()
    {
        if (actor == null) actor = GetComponent<FacilityHumanoid>();
        path = new NavMeshPath();
        actor.RegisterPolice(this, walkSpeed);
        gunHand = gunTip != null ? gunTip.GetComponentInParent<FacilityHitRegion>() : null;
        if (target == null && actor.player != null) target = actor.player.GetComponentInParent<FacilityPlayerHealth>();
        if (aimWarning != null) aimWarning.SetActive(false);
        if (muzzleFlash != null) muzzleFlash.SetActive(false);
        nextShot = Time.time + 1.1f;
    }

    private void Update()
    {
        bool valid = actor != null && actor.player != null && target != null && !target.IsDead && !actor.IsDead &&
            !actor.IsKnockedDown && !actor.IsRecovering && gunTip != null && gunTip.gameObject.activeInHierarchy &&
            (gunHand == null || !gunHand.IsDetached);
        if (muzzleFlash != null) muzzleFlash.SetActive(valid && Time.time - firedAt < .045f);
        if (!valid)
        {
            IsAiming = false;
            if (aimWarning != null) aimWarning.SetActive(false);
            return;
        }
        if (Time.timeScale <= 0f) return;
        if (IsAiming)
        {
            if (Time.time < fireAt) return;
            IsAiming = false;
            if (aimWarning != null) aimWarning.SetActive(false);
            nextShot = Time.time + shotInterval;
            // Recheck cover at release. The aim remains locked even if the player strafes away.
            if (!ClearLine(gunTip.position, LockedAimPoint)) return;
            if (projectileSystem != null && projectileSystem.Fire(gunTip.position, LockedAimPoint - gunTip.position, transform, target))
            {
                ShotsFired++; firedAt = Time.time;
                if (gunSource != null && gunReport != null) gunSource.PlayOneShot(gunReport, .65f);
            }
            return;
        }
        Vector3 aim = actor.player.position + Vector3.up * 1.05f;
        if (Time.time < nextShot || Vector3.Distance(transform.position, actor.player.position) > engagementRange ||
            !ClearLine(actor.AimPoint, aim) || !ClearLine(gunTip.position, aim)) return;
        Vector3 flat = aim - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude > .01f && Vector3.Angle(transform.forward, flat) > 22f) return;
        LockedAimPoint = aim;
        fireAt = Time.time + aimWindup;
        IsAiming = true;
        if (aimWarning != null) aimWarning.SetActive(true);
    }

    public Vector3 GetSteeringDirection()
    {
        if (actor == null || actor.player == null || target == null || target.IsDead || IsAiming) return Vector3.zero;
        Vector3 aim = actor.player.position + Vector3.up * 1.05f;
        if (Vector3.Distance(transform.position, actor.player.position) <= holdDistance && ClearLine(actor.AimPoint, aim)) return Vector3.zero;
        if (Time.time >= nextRepath) RefreshPath();
        while (cornerIndex < cornerCount)
        {
            Vector3 delta = corners[cornerIndex] - transform.position; delta.y = 0f;
            if (delta.sqrMagnitude > .04f)
            {
                Vector3 steering = delta.normalized;
                FacilityDoor.TryOpenNear(transform.position, steering);
                return steering;
            }
            cornerIndex++;
        }
        return Vector3.zero;
    }

    private void RefreshPath()
    {
        nextRepath = Time.time + .5f; RepathCount++; cornerCount = cornerIndex = 0;
        if (!NavMesh.SamplePosition(transform.position + Vector3.up * .15f, out NavMeshHit start, 1.2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(actor.player.position + Vector3.up * .15f, out NavMeshHit end, 1.2f, NavMesh.AllAreas))
        { NavigationStatus = "No navigation surface"; return; }
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid)
        { NavigationStatus = "PathInvalid"; return; }
        NavigationStatus = path.status == NavMeshPathStatus.PathComplete ? "PathComplete" : "PathPartial";
        cornerCount = path.GetCornersNonAlloc(corners); cornerIndex = cornerCount > 1 ? 1 : 0;
    }

    private bool ClearLine(Vector3 origin, Vector3 destination)
    {
        Vector3 difference = destination - origin;
        float length = difference.magnitude;
        if (length < .01f) return true;
        int count = Physics.RaycastNonAlloc(origin, difference / length, sightHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count >= sightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider item = sightHits[i].collider;
            if (item == null || item.transform.IsChildOf(transform)) continue;
            if (item.GetComponentInParent<FacilityPlayerHealth>() == target) continue;
            if (item.GetComponentInParent<FacilityHumanoid>() != null || item.GetComponentInParent<FacilityHitRegion>() != null) continue;
            return false;
        }
        return true;
    }
}
