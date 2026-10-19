using UnityEngine;
using UnityEngine.AI;

/// <summary>A human knife attacker. Damage and dismemberment remain owned by the observer anatomy.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(FacilityHumanoid))]
public sealed class FacilityKnifeGuard : MonoBehaviour
{
    public FacilityHumanoid actor;
    public Transform knifeProp;
    public FacilityPlayerHealth targetHealth;
    public bool startAlerted;
    [Min(.1f)] public float walkSpeed = 2.35f;
    [Min(.5f)] public float attackRange = 1.3f;
    [Min(.2f)] public float attackInterval = 1.25f;
    public bool Alerted { get; private set; }
    public int SuccessfulAttacks { get; private set; }
    public int RepathCount { get; private set; }
    public int CornerCount => cornerCount;
    public Vector3 CurrentDestination => nextCorner < cornerCount ? corners[nextCorner] : transform.position;
    public bool HasNavigationPath => cornerCount > 1;
    public string NavigationStatus { get; private set; } = "Idle";
    public float AttackPulse => Mathf.Clamp01(1f - (Time.time - attackedAt) / .34f);
    public Vector3 Steering { get; private set; }
    private CharacterController playerMotor;
    private Transform resolvedPlayer;
    private FacilityHitRegion knifeRegion;
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[48];
    private readonly RaycastHit[] sightHits = new RaycastHit[48];
    private int cornerCount, nextCorner;
    private float nextRepathAt, nextAttackAt, attackedAt = float.NegativeInfinity;
    private Quaternion knifeRestRotation;

    private void Awake()
    {
        if (actor == null) actor = GetComponent<FacilityHumanoid>();
        path = new NavMeshPath();
        actor.RegisterGuard(this, walkSpeed);
        if (knifeProp != null)
        {
            knifeRestRotation = knifeProp.localRotation;
            knifeRegion = knifeProp.GetComponentInParent<FacilityHitRegion>();
        }
        ResolvePlayer();
        if (startAlerted) Activate();
    }

    private void Start() => ResolvePlayer();

    private void ResolvePlayer()
    {
        if (actor == null || actor.player == null) return;
        // Spawned actors and reloaded scenes can bind the player after Awake/Start.
        // Rebind on a target change as well, rather than retaining a previous player's health.
        if (resolvedPlayer != actor.player)
        {
            if (resolvedPlayer != null) targetHealth = null;
            nextRepathAt = 0f;
        }
        resolvedPlayer = actor.player;
        if (targetHealth == null) targetHealth = actor.player.GetComponentInParent<FacilityPlayerHealth>();
        playerMotor = actor.player.GetComponentInParent<CharacterController>();
    }

    public void Activate()
    {
        bool newlyAlerted = !Alerted;
        Alerted = true;
        if (actor == null) actor = GetComponent<FacilityHumanoid>();
        if (actor != null) { actor.IsHostile = true; actor.pursuitSpeed = walkSpeed; }
        ResolvePlayer();
        // Repeated alarm broadcasts or hits must not keep postponing a ready attack.
        if (newlyAlerted)
        {
            nextRepathAt = 0f;
            nextAttackAt = Mathf.Max(nextAttackAt, Time.time + .3f);
        }
    }

    private void Update()
    {
        if (actor != null && actor.player != null && (resolvedPlayer != actor.player || targetHealth == null)) ResolvePlayer();
        if (!Alerted || Time.timeScale <= 0f || actor == null || actor.IsDead || actor.IsKnockedDown || actor.IsRecovering ||
            actor.player == null || targetHealth == null || targetHealth.IsDead || Time.time < nextAttackAt) return;
        // Losing the knife arm prevents phantom knife damage from the remaining body.
        if (knifeProp != null && (!knifeProp.gameObject.activeInHierarchy || (knifeRegion != null && knifeRegion.IsDetached))) return;
        Vector3 offset = actor.player.position - transform.position;
        if (Mathf.Abs(offset.y) > 1.1f) return;
        offset.y = 0f;
        if (offset.sqrMagnitude > attackRange * attackRange || !HasClearAttack()) return;
        nextAttackAt = Time.time + attackInterval;
        attackedAt = Time.time;
        if (targetHealth.TryTakeHit(transform)) SuccessfulAttacks++;
    }

    private void LateUpdate()
    {
        if (knifeProp == null || actor == null || actor.IsDead || actor.IsKnockedDown || actor.IsRecovering ||
            (knifeRegion != null && knifeRegion.IsDetached) || Time.timeScale <= 0f) return;
        knifeProp.localRotation = knifeRestRotation * Quaternion.Euler(-26f * AttackPulse, 0f, 0f);
    }

    public Vector3 GetSteeringDirection()
    {
        if (actor != null && actor.player != null && (resolvedPlayer != actor.player || targetHealth == null)) ResolvePlayer();
        if (!Alerted || Time.timeScale <= 0f || actor == null || actor.player == null || (targetHealth != null && targetHealth.IsDead))
        { Steering = Vector3.zero; return Steering; }
        if (Time.time >= nextRepathAt) RefreshPath();
        while (nextCorner < cornerCount)
        {
            Vector3 offset = corners[nextCorner] - transform.position;
            float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            if (offset.sqrMagnitude > .01f || height > .65f)
            {
                Steering = offset.sqrMagnitude > .001f ? offset.normalized : Vector3.zero;
                FacilityDoor.TryOpenNear(transform.position, Steering);
                return Steering;
            }
            nextCorner++;
        }
        Steering = Vector3.zero;
        return Steering;
    }

    private void RefreshPath()
    {
        nextRepathAt = Time.time + .45f;
        RepathCount++;
        cornerCount = nextCorner = 0;
        if (!NavMesh.SamplePosition(transform.position + Vector3.up * .15f, out NavMeshHit start, 1f, NavMesh.AllAreas))
        { NavigationStatus = "No start surface"; return; }
        if (!NavMesh.SamplePosition(actor.player.position + Vector3.up * .15f, out NavMeshHit end, 1f, NavMesh.AllAreas))
        { NavigationStatus = "No target surface"; return; }
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid)
        { NavigationStatus = "PathInvalid"; return; }
        NavigationStatus = path.status == NavMeshPathStatus.PathComplete ? "PathComplete" : "PathPartial";
        cornerCount = path.GetCornersNonAlloc(corners);
        nextCorner = cornerCount > 1 ? 1 : 0;
    }

    private bool HasClearAttack()
    {
        Vector3 origin = actor.chest != null ? actor.chest.position : transform.position + Vector3.up;
        Vector3 target = playerMotor != null ? playerMotor.transform.TransformPoint(playerMotor.center) : actor.player.position + Vector3.up * .9f;
        Vector3 offset = target - origin;
        float distance = offset.magnitude;
        if (distance < .01f) return true;
        int count = Physics.RaycastNonAlloc(origin, offset / distance, sightHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == sightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider item = sightHits[i].collider;
            if (item == null || item.transform.IsChildOf(transform) || item == actor.motor) continue;
            if (item.transform.IsChildOf(actor.player) || actor.player.IsChildOf(item.transform)) continue;
            FacilityHitRegion region = item.GetComponentInParent<FacilityHitRegion>();
            if (region != null && region.owner == actor) continue;
            return false;
        }
        return true;
    }
}
