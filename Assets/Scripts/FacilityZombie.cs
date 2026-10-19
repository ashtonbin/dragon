using UnityEngine;
using UnityEngine.AI;

/// <summary>Uses the ordinary subject's motor and damage anatomy, with navigation and a close melee attack.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(FacilityHumanoid))]
public sealed class FacilityZombie : MonoBehaviour
{
    [Min(.1f)] public float walkSpeed = .95f;
    [Min(.5f)] public float attackRange = 1.25f;
    [Min(.1f)] public float attackInterval = 1.15f;
    public FacilityPlayerHealth targetHealth;
    public AudioSource moanSource;
    public AudioClip[] moanClips;
    public int MoansPlayed { get; private set; }
    public int SuccessfulAttacks { get; private set; }
    public bool HasNavigationPath => cornerCount > 1;
    public float AttackPulse => Mathf.Clamp01(1f - (Time.time - attackedAt) / .3f);
    public string NavigationStatus { get; private set; } = "Not planned";
    public string PlanFailureReason { get; private set; } = "";
    public int CornerCount => cornerCount;
    public int NextCorner => nextCorner;
    public Vector3 Steering { get; private set; }
    public Vector3 LastStartPosition { get; private set; }
    public Vector3 LastTargetPosition { get; private set; }
    public Vector3 CurrentDestination => nextCorner < cornerCount ? corners[nextCorner] : transform.position;
    public int RepathCount { get; private set; }
    public string BlockingCollider { get; private set; } = "";
    public CollisionFlags MotorCollisionFlags => actor != null && actor.motor != null ? actor.motor.collisionFlags : CollisionFlags.None;

    private FacilityHumanoid actor;
    private CharacterController playerMotor;
    private NavMeshPath navigationPath;
    private readonly Vector3[] corners = new Vector3[48];
    private readonly RaycastHit[] sightHits = new RaycastHit[48];
    private int cornerCount, nextCorner;
    private float nextRepathAt, nextAttackAt, attackedAt = float.NegativeInfinity;
    private float nextMoanAt;
    private uint moanRandom;
    private bool moansSuspended;

    private void Awake()
    {
        actor = GetComponent<FacilityHumanoid>();
        actor.RegisterZombie(this, walkSpeed);
        navigationPath = new NavMeshPath();
        moanRandom = unchecked((uint)GetInstanceID() * 747796405u + 2891336453u);
        if (moanRandom == 0) moanRandom = 1;
        nextMoanAt = Time.time + Mathf.Lerp(.8f, 3f, NextMoanRandom());
        ResolvePlayer();
    }

    private void Start() => ResolvePlayer();

    private void ResolvePlayer()
    {
        if (actor.player == null) return;
        if (targetHealth == null) targetHealth = actor.player.GetComponentInParent<FacilityPlayerHealth>();
        playerMotor = actor.player.GetComponentInParent<CharacterController>();
    }

    private void Update()
    {
        UpdateMoan();
        if (Time.timeScale <= 0f || actor.IsDead || actor.IsKnockedDown || actor.IsRecovering ||
            actor.player == null || targetHealth == null || targetHealth.IsDead || Time.time < nextAttackAt) return;
        Vector3 offset = actor.player.position - transform.position;
        if (Mathf.Abs(offset.y) > 1.2f) return;
        offset.y = 0f;
        if (offset.sqrMagnitude > attackRange * attackRange || !HasClearAttack()) return;
        // Even an invulnerable target consumes this attack, preventing a rapid retry each frame.
        nextAttackAt = Time.time + attackInterval;
        attackedAt = Time.time;
        if (targetHealth.TryTakeHit(transform)) SuccessfulAttacks++;
    }

    private void UpdateMoan()
    {
        // Menus pause the existing clip through AudioListener.pause rather than restarting it.
        if (Time.timeScale <= 0f || AudioListener.pause) return;
        bool suppressed = actor == null || actor.IsDead || actor.HeadExploded || actor.IsKnockedDown || actor.IsRecovering ||
            (targetHealth != null && targetHealth.IsDead) || (actor.voice != null && actor.voice.isPlaying);
        if (suppressed)
        {
            if (!moansSuspended) SilenceMoan();
            else if (moanSource != null && moanSource.isPlaying) moanSource.Stop();
            moansSuspended = true;
            return;
        }
        moansSuspended = false;
        if (moanSource == null || !moanSource.isActiveAndEnabled || moanSource.isPlaying ||
            moanClips == null || moanClips.Length == 0 || Time.time < nextMoanAt) return;
        int index = Mathf.Min(moanClips.Length - 1, (int)(NextMoanRandom() * moanClips.Length));
        AudioClip clip = moanClips[index];
        nextMoanAt = Time.time + Mathf.Lerp(5f, 11f, NextMoanRandom());
        if (clip == null) return;
        moanSource.clip = clip;
        moanSource.pitch = Mathf.Lerp(.9f, 1.02f, NextMoanRandom());
        moanSource.Play();
        MoansPlayed++;
        nextMoanAt += clip.length / moanSource.pitch;
    }

    /// <summary>Damage owns the voice channel immediately; an idle moan must never cover a hurt response.</summary>
    internal void SilenceMoan()
    {
        if (moanSource != null) moanSource.Stop();
        nextMoanAt = Time.time + Mathf.Lerp(5f, 11f, NextMoanRandom());
    }

    private float NextMoanRandom()
    {
        // A small per-instance generator keeps a group of newly spawned zombies out of chorus.
        moanRandom ^= moanRandom << 13;
        moanRandom ^= moanRandom >> 17;
        moanRandom ^= moanRandom << 5;
        return (moanRandom & 0x00ffffffu) / 16777216f;
    }

    private void OnDisable()
    {
        if (moanSource != null) moanSource.Stop();
    }

    public Vector3 GetSteeringDirection()
    {
        if (actor == null || actor.player == null || (targetHealth != null && targetHealth.IsDead) || Time.timeScale <= 0f)
        {
            Steering = Vector3.zero;
            return Steering;
        }
        if (Time.time >= nextRepathAt) RefreshPath();
        while (nextCorner < cornerCount)
        {
            Vector3 offset = corners[nextCorner] - transform.position;
            float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            // A broad arrival radius cuts the inside of a carved corner and pushes the capsule into its obstacle.
            if (offset.sqrMagnitude > .01f || height > .65f)
            {
                Steering = offset.sqrMagnitude > .001f ? offset.normalized : Vector3.zero;
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
        PlanFailureReason = "";
        Vector3 feet = transform.position + Vector3.up * .15f;
        Vector3 target = actor.player.position + Vector3.up * .15f;
        LastStartPosition = feet;
        LastTargetPosition = target;
        if (!NavMesh.SamplePosition(feet, out NavMeshHit start, 1f, NavMesh.AllAreas))
        { NavigationStatus = "No start surface"; PlanFailureReason = "No walking surface within one metre of the zombie"; return; }
        LastStartPosition = start.position;
        if (!NavMesh.SamplePosition(target, out NavMeshHit end, 1f, NavMesh.AllAreas))
        { NavigationStatus = "No target surface"; PlanFailureReason = "No walking surface within one metre of the player"; return; }
        LastTargetPosition = end.position;
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, navigationPath) ||
            navigationPath.status == NavMeshPathStatus.PathInvalid)
        { NavigationStatus = "PathInvalid"; PlanFailureReason = "The walking surfaces do not have a usable route"; return; }
        NavigationStatus = navigationPath.status == NavMeshPathStatus.PathComplete ? "PathComplete" : "PathPartial";
        // Partial paths can approach a player standing on a non-walkable prop, but never cross a wall or a gap.
        cornerCount = navigationPath.GetCornersNonAlloc(corners);
        nextCorner = cornerCount > 1 ? 1 : 0;
        if (cornerCount < 2) PlanFailureReason = "The route has fewer than two corners";
        ProbeObstruction();
    }

    private void ProbeObstruction()
    {
        BlockingCollider = "";
        if (nextCorner >= cornerCount) return;
        Vector3 direction = corners[nextCorner] - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .001f) return;
        int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .7f, direction.normalized,
            sightHits, .8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider item = sightHits[i].collider;
            if (item == null || item.transform.IsChildOf(transform) || item == actor.motor || sightHits[i].distance >= nearest) continue;
            if (actor.player != null && (item.transform.IsChildOf(actor.player) || actor.player.IsChildOf(item.transform))) continue;
            nearest = sightHits[i].distance;
            BlockingCollider = item.name;
        }
    }

    private bool HasClearAttack()
    {
        Vector3 origin = actor.chest != null ? actor.chest.position : transform.position + Vector3.up;
        Vector3 target = playerMotor != null ? playerMotor.transform.TransformPoint(playerMotor.center) : actor.player.position + Vector3.up * .9f;
        Vector3 offset = target - origin;
        float length = offset.magnitude;
        if (length < .01f) return true;
        int count = Physics.RaycastNonAlloc(origin, offset / length, sightHits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
