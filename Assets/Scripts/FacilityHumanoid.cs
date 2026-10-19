using UnityEngine;

/// <summary>An observer with per-shell health, anatomical damage and a recoverable physical knockdown.</summary>
public sealed class FacilityHumanoid : MonoBehaviour
{
    public event System.Action<FacilityHumanoid> Attacked;
    public Transform player;
    public Transform head;
    public Transform chest;
    public Transform pelvis;
    public CharacterController motor;
    public Rigidbody[] bodies;
    public Collider[] bodyColliders;
    public FacilityHitRegion[] hitRegions;
    public Transform[] upperArms;
    public Transform[] forearms;
    public Transform[] thighs;
    public Transform[] shins;
    public CharacterJoint[] forearmJoints;
    public GameObject[] upperArmStumps;
    public GameObject[] forearmStumps;
    public FacilityGorePool gore;
    public AudioSource voice;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public Transform contactShadow;
    [Min(.1f)] public float pursuitSpeed = 1.65f;
    public bool IsHostile { get; set; }
    public int HitsTaken { get; private set; }
    public bool IsDead { get; private set; }
    public int RemovedPatchCount { get; private set; }
    public int SeveredLimbCount { get; private set; }
    public bool HeadExploded { get; private set; }
    public bool IsKnockedDown { get; private set; }
    public bool IsRecovering { get; private set; }
    public int RecoveryCount { get; private set; }
    public bool IsCrawling { get; private set; }
    public int VoiceEvents { get; private set; }
    public AudioClip LastVoicePlayed { get; private set; }
    public Vector3 AimPoint => chest != null ? chest.position : transform.position + Vector3.up * 1.2f;

    private readonly int[] seenShots = new int[32];
    private readonly RaycastHit[] floorHits = new RaycastHit[32];
    private readonly Collider[] clearanceHits = new Collider[64];
    private int shotCursor;
    private float nextShove, verticalSpeed, gait, settleUntil, recoveryStart, resumePursuitAt;
    private float lastVoiceAt = float.NegativeInfinity;
    private Quaternion initialHeadRotation;
    private Transform playerEyes, detachedRoot;
    private Vector3[] restPositions, recoveryPositions;
    private Quaternion[] restRotations, recoveryRotations;
    private FacilityHitRegion[] bodyRegions;
    private FacilityZombie zombie;
    private FacilityKnifeGuard knifeGuard;
    private FacilityPoliceOfficer police;
    private float flinchUntil;
    private FacilityPlayerHealth playerHealth;
    private CharacterController playerMotor;
    private float standingHeight, standingRadius;
    private Vector3 standingCentre;
    private readonly FacilityNpcNavigation navigationClearance = new FacilityNpcNavigation();
    public string NavigationClearanceStatus => navigationClearance.Status;
    public Collider NavigationBlocker => navigationClearance.LastBlockingCollider;
    public Vector3 NavigationBlockingNormal => navigationClearance.LastBlockingNormal;
    private const float KnockdownTime = 1.5f, GetUpTime = .72f;

    internal void RegisterZombie(FacilityZombie behaviour, float speed)
    {
        zombie = behaviour;
        IsHostile = true;
        pursuitSpeed = speed;
    }

    internal void RegisterGuard(FacilityKnifeGuard behaviour, float speed)
    {
        knifeGuard = behaviour;
        pursuitSpeed = speed;
    }

    internal void RegisterPolice(FacilityPoliceOfficer behaviour, float speed)
    {
        police = behaviour;
        IsHostile = true;
        pursuitSpeed = speed;
    }

    private void Awake()
    {
        for (int i = 0; i < seenShots.Length; i++) seenShots[i] = int.MinValue;
        if (bodies == null) bodies = new Rigidbody[0];
        if (bodyColliders == null) bodyColliders = new Collider[0];
        if (hitRegions == null) hitRegions = new FacilityHitRegion[0];
        initialHeadRotation = head != null ? head.localRotation : Quaternion.identity;
        Camera playerCamera = player != null ? player.GetComponentInChildren<Camera>() : null;
        playerEyes = playerCamera != null ? playerCamera.transform : null;
        if (motor != null)
        {
            standingHeight = motor.height; standingRadius = motor.radius; standingCentre = motor.center;
            // A slow walker must move at high frame rates too; Unity's 1 mm default can discard every step.
            motor.minMoveDistance = 0f;
        }
        restPositions = new Vector3[bodies.Length]; restRotations = new Quaternion[bodies.Length];
        recoveryPositions = new Vector3[bodies.Length]; recoveryRotations = new Quaternion[bodies.Length];
        bodyRegions = new FacilityHitRegion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            restPositions[i] = transform.InverseTransformPoint(bodies[i].position);
            restRotations[i] = Quaternion.Inverse(transform.rotation) * bodies[i].rotation;
            bodyRegions[i] = bodies[i].GetComponent<FacilityHitRegion>();
        }
        foreach (FacilityHitRegion region in hitRegions)
            if (region != null) region.owner = this;
        // Disable self collisions; every ragdoll part still collides with the facility.
        zombie = GetComponent<FacilityZombie>();
        knifeGuard = GetComponent<FacilityKnifeGuard>();
        police = GetComponent<FacilityPoliceOfficer>();
        playerMotor = player != null ? player.GetComponentInParent<CharacterController>() : null;
        playerHealth = player != null ? player.GetComponentInParent<FacilityPlayerHealth>() : null;
        for (int i = 0; i < bodyColliders.Length; i++)
        {
            if (bodyColliders[i] == null) continue;
            if (motor != null) Physics.IgnoreCollision(bodyColliders[i], motor);
            if (playerMotor != null) Physics.IgnoreCollision(bodyColliders[i], playerMotor);
            for (int j = 0; j < i; j++)
                if (bodyColliders[j] != null) Physics.IgnoreCollision(bodyColliders[i], bodyColliders[j]);
        }
    }

    private void Update()
    {
        if (IsDead || Time.timeScale <= 0f || (playerHealth != null && playerHealth.IsDead)) return;
        if (IsRecovering) { BlendRecovery(); return; }
        if (IsKnockedDown)
        {
            if (Time.time >= settleUntil) BeginRecovery();
            return;
        }
        if (player == null || motor == null || !motor.enabled) return;
        Vector3 toward = player.position - transform.position; toward.y = 0;
        float distance = toward.magnitude;
        bool pursuing = (IsHostile || HitsTaken > 0) && distance > .8f && Time.time >= resumePursuitAt;
        Vector3 steering = pursuing && IsHostile && police != null ? police.GetSteeringDirection() :
            pursuing && IsHostile && knifeGuard != null ? knifeGuard.GetSteeringDirection() :
            pursuing && IsHostile && zombie != null ? zombie.GetSteeringDirection() : distance > .01f ? toward / distance : Vector3.zero;
        if (pursuing)
        {
            Vector3 waypoint = police != null ? police.CurrentDestination : knifeGuard != null ? knifeGuard.CurrentDestination : zombie != null ? zombie.CurrentDestination : player.position;
            waypoint -= transform.position; waypoint.y = 0;
            steering = navigationClearance.Steer(this, steering, waypoint.magnitude);
        }
        Vector3 facing = pursuing && steering.sqrMagnitude > .01f ? steering : toward;
        if (police != null && police.IsAiming)
        {
            facing = police.LockedAimPoint - transform.position;
            facing.y = 0f;
        }
        if (facing.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), (IsHostile || HitsTaken > 0 ? 140f : 52f) * Time.deltaTime);
        bool moving = pursuing && steering.sqrMagnitude > .01f;
        Vector3 motion = moving ? steering * (IsCrawling ? .55f : pursuitSpeed) : Vector3.zero;
        if (motor.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
        else verticalSpeed = Mathf.Max(-16f, verticalSpeed - 20f * Time.deltaTime);
        motion.y = verticalSpeed;
        motor.Move(motion * Time.deltaTime);
        if (moving) gait += Time.deltaTime * (IsCrawling ? 4f : IsHostile ? 4f : 7f);
        PoseLocomotion(moving);
        if (!IsHostile && knifeGuard == null && HitsTaken > 0 && distance < 1.18f && Time.time > nextShove)
        {
            if (playerMotor != null && playerMotor.enabled) playerMotor.Move(toward.normalized * (IsCrawling ? .12f : .36f));
            nextShove = Time.time + 1.3f;
        }
    }

    private void PoseLocomotion(bool moving)
    {
        float amount = moving ? (IsCrawling ? 9f : 16f) : 0;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (!Attached(i)) continue;
            FacilityBodyRegion kind = bodyRegions[i] != null ? bodyRegions[i].kind : FacilityBodyRegion.Torso;
            Vector3 position = TargetPosition(i);
            Quaternion rotation = TargetRotation(i);
            if (IsCrawling)
            {
                if (kind == FacilityBodyRegion.UpperArmLeft || kind == FacilityBodyRegion.UpperArmRight)
                    rotation *= Quaternion.Euler(Mathf.Sin(gait + (kind == FacilityBodyRegion.UpperArmLeft ? 0 : Mathf.PI)) * amount, 0, 0);
            }
            else
            {
                float side = kind == FacilityBodyRegion.UpperArmLeft || kind == FacilityBodyRegion.ThighLeft ? 0 : Mathf.PI;
                if (kind == FacilityBodyRegion.UpperArmLeft || kind == FacilityBodyRegion.UpperArmRight)
                {
                    float armPose = IsHostile ? 64f + (zombie != null ? zombie.AttackPulse * 16f : 0f) : HitsTaken > 0 ? 18f : 0;
                    if (knifeGuard != null && IsHostile)
                        armPose = kind == FacilityBodyRegion.UpperArmRight ? 48f + knifeGuard.AttackPulse * 42f : 19f;
                    if (police != null)
                        armPose = kind == FacilityBodyRegion.UpperArmRight ? 74f + police.ShotPulse * 8f : 34f;
                    rotation *= Quaternion.Euler(-Mathf.Sin(gait + side) * amount * .7f - armPose, 0, kind == FacilityBodyRegion.UpperArmLeft ? -5 : 5);
                }
                else if (kind == FacilityBodyRegion.ThighLeft || kind == FacilityBodyRegion.ThighRight)
                    rotation *= Quaternion.Euler(Mathf.Sin(gait + side) * amount, 0, 0);
            }
            if (kind == FacilityBodyRegion.Torso && Time.time < flinchUntil)
                rotation *= Quaternion.Euler(10f * Mathf.Sin(Mathf.Clamp01((flinchUntil - Time.time) / .28f) * Mathf.PI), 0, 0);
            bodies[i].transform.SetPositionAndRotation(transform.TransformPoint(position), transform.rotation * rotation);
        }
        // The forearms and shins are separate physical bodies. Place them at their animated joint sockets.
        foreach (FacilityHitRegion region in hitRegions)
        {
            if (region == null || region.IsDetached || region.parentRegion == null || region.body == null) continue;
            bool forearm = region.kind == FacilityBodyRegion.ForearmLeft || region.kind == FacilityBodyRegion.ForearmRight;
            bool shin = region.kind == FacilityBodyRegion.ShinLeft || region.kind == FacilityBodyRegion.ShinRight;
            if (!forearm && !shin) continue;
            Transform parent = region.parentRegion.transform;
            region.transform.position = parent.TransformPoint(new Vector3(0, forearm ? -.29f : -.34f, 0));
            region.transform.rotation = parent.rotation * Quaternion.Euler(forearm ? -12f : (IsCrawling ? 30f : 0), 0, 0);
        }
    }

    private void LateUpdate()
    {
        if (IsDead || IsKnockedDown || IsRecovering || Time.timeScale <= 0f || head == null || player == null) return;
        Vector3 eye = playerEyes != null ? playerEyes.position : player.position + Vector3.up * 1.65f;
        Vector3 local = transform.InverseTransformDirection(eye - head.position);
        float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f);
        float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -24f, 24f);
        head.rotation = Quaternion.Slerp(head.rotation, transform.rotation * initialHeadRotation * Quaternion.Euler(pitch, yaw, 0), Time.deltaTime * 8f);
    }

    /// <summary>Health is charged once per shell; each anatomical region still processes its own pellets.</summary>
    public void ReceivePellet(int shotId, Vector3 point, Vector3 direction, Collider hitCollider)
    {
        FacilityHitRegion struck = FindRegion(hitCollider, point);
        if (struck == null || struck.body == null || !struck.gameObject.activeInHierarchy) return;
        direction = direction.sqrMagnitude > .001f ? direction.normalized : transform.forward;
        bool wasDetached = struck.IsDetached;
        bool newShell = !wasDetached && RememberShot(shotId);
        bool newRegion = struck.lastPelletShot != shotId;
        if (!newRegion) return;
        struck.lastPelletShot = shotId;
        RemovedPatchCount += struck.Expose(point, struck.IsLimb ? 3 : 2);
        bool wasDead = IsDead;
        if (!wasDead && !wasDetached) Attacked?.Invoke(this);
        if (struck.kind == FacilityBodyRegion.Head && !HeadExploded)
        {
            ExplodeHead(struck, point, direction);
            return;
        }
        if (struck.IsLimb && !struck.JointSevered) Sever(struck, point, direction);
        if (wasDead || struck.IsDetached)
        {
            // A detached body part retains its owner and can be severed further after recovery.
            ImpulseBody(struck.body, point, direction, 6f);
        }
        if (wasDead || wasDetached)
        {
            if (newShell || wasDetached) gore?.Burst(point, direction, false);
            return;
        }
        if (newShell)
        {
            knifeGuard?.Activate();
            HitsTaken = Mathf.Min(3, HitsTaken + 1);
            bool fatal = HitsTaken >= 3;
            gore?.Burst(point, direction, fatal);
            PlayVoice(fatal);
            if (fatal) Die(point, direction, struck);
            else KnockDown(point, direction, struck);
        }
        else if (!struck.IsDetached && !struck.body.isKinematic)
            ImpulseBody(struck.body, point, direction, 2.5f);
    }

    // Kept for existing tooling and authored callers; one shell can no longer collapse anatomy to a centroid.
    public void ReceiveShot(int shotId, Vector3 point, Vector3 direction, Collider hitCollider, int pellets)
        => ReceivePellet(shotId, point, direction, hitCollider);

    /// <summary>A fist connects once per swing. It bruises rather than severing shotgun anatomy.</summary>
    public void ReceiveMelee(int attackId, Vector3 point, Vector3 direction, Collider hitCollider)
    {
        FacilityHitRegion struck = FindRegion(hitCollider, point);
        if (struck == null || struck.IsDetached || struck.body == null || !struck.gameObject.activeInHierarchy || !RememberShot(attackId)) return;
        direction = direction.sqrMagnitude > .001f ? direction.normalized : transform.forward;
        if (IsDead) { ImpulseBody(struck.body, point, direction, 1.6f); return; }
        Attacked?.Invoke(this);
        knifeGuard?.Activate();
        HitsTaken = Mathf.Min(3, HitsTaken + 1);
        bool fatal = HitsTaken >= 3;
        PlayVoice(fatal);
        if (fatal) Die(point, direction, struck, 2.1f, .65f);
        else
        {
            flinchUntil = Time.time + .28f;
            resumePursuitAt = Time.time + .22f;
            if (IsKnockedDown || IsRecovering) ImpulseBody(struck.body, point, direction, 1.6f);
            else if (motor != null && motor.enabled)
            {
                Vector3 horizontal = direction; horizontal.y = 0f;
                motor.Move(horizontal.normalized * .09f);
            }
        }
    }

    public bool IsRegionSevered(FacilityHitRegion region) => region != null && region.IsDetached;

    private bool RememberShot(int shotId)
    {
        for (int i = 0; i < seenShots.Length; i++) if (seenShots[i] == shotId) return false;
        seenShots[shotCursor] = shotId; shotCursor = (shotCursor + 1) % seenShots.Length;
        return true;
    }

    private FacilityHitRegion FindRegion(Collider collider, Vector3 point)
    {
        FacilityHitRegion region = collider != null ? collider.GetComponentInParent<FacilityHitRegion>() : null;
        if (region != null && region.owner == this) return region;
        float distance = float.PositiveInfinity;
        FacilityHitRegion closest = null;
        foreach (FacilityHitRegion item in hitRegions)
        {
            if (item == null || item.body == null || !item.gameObject.activeInHierarchy) continue;
            float candidate = (item.body.worldCenterOfMass - point).sqrMagnitude;
            if (candidate < distance) { distance = candidate; closest = item; }
        }
        return closest;
    }

    private void Sever(FacilityHitRegion region, Vector3 point, Vector3 direction)
    {
        if (region.JointSevered) return;
        region.JointSevered = true;
        if (region.joint != null) Destroy(region.joint);
        if (region.parentStump != null) region.parentStump.SetActive(true);
        if (region.detachedStump != null) region.detachedStump.SetActive(true);
        if (region.cutCovers != null) foreach (GameObject cover in region.cutCovers) if (cover != null) cover.SetActive(false);
        if (detachedRoot == null)
        {
            detachedRoot = new GameObject(name + " - detached anatomy").transform;
            detachedRoot.SetParent(transform.parent, true);
        }
        foreach (FacilityHitRegion item in hitRegions)
        {
            if (item == null || !IsDescendantRegion(item, region)) continue;
            item.IsDetached = true;
            item.transform.SetParent(detachedRoot, true);
            MakeDynamic(item.body);
            float side = region.kind == FacilityBodyRegion.UpperArmLeft || region.kind == FacilityBodyRegion.ForearmLeft || region.kind == FacilityBodyRegion.ThighLeft || region.kind == FacilityBodyRegion.ShinLeft ? -1 : 1;
            item.body.linearVelocity = direction * 7f + transform.right * side * 1.8f + Vector3.up * 3.2f;
            item.body.angularVelocity = transform.right * 6f + transform.forward * side * 5f;
        }
        SeveredLimbCount++;
        IsCrawling |= region.kind == FacilityBodyRegion.ThighLeft || region.kind == FacilityBodyRegion.ShinLeft || region.kind == FacilityBodyRegion.ThighRight || region.kind == FacilityBodyRegion.ShinRight;
        gore?.BurstSever(region.parentStump != null ? region.parentStump.transform.position : point, direction);
    }

    private static bool IsDescendantRegion(FacilityHitRegion item, FacilityHitRegion ancestor)
    {
        FacilityHitRegion current = item;
        for (int depth = 0; current != null && depth < 12; depth++, current = current.parentRegion)
            if (current == ancestor) return true;
        return false;
    }

    private void ExplodeHead(FacilityHitRegion region, Vector3 point, Vector3 direction)
    {
        HeadExploded = true;
        region.JointSevered = true; region.IsDetached = true;
        if (region.joint != null) Destroy(region.joint);
        if (region.parentStump != null) region.parentStump.SetActive(true);
        if (region.cutCovers != null) foreach (GameObject cover in region.cutCovers) if (cover != null) cover.SetActive(false);
        gore?.BurstHead(region.transform.position, direction);
        // The complete head, including hair, eyes, ears and collider, becomes fragments rather than a floating face.
        region.gameObject.SetActive(false);
        HitsTaken = 3;
        if (!IsDead) { PlayVoice(true); Die(point, direction, null); }
    }

    private void KnockDown(Vector3 point, Vector3 direction, FacilityHitRegion struck)
    {
        IsKnockedDown = true; IsRecovering = false;
        settleUntil = Time.time + KnockdownTime;
        if (motor != null) motor.enabled = false;
        if (contactShadow != null) contactShadow.gameObject.SetActive(false);
        LaunchAttached(point, direction, struck, 4.6f, 2.45f);
    }

    private void Die(Vector3 point, Vector3 direction, FacilityHitRegion struck, float speed = 6.2f, float lift = 2.8f)
    {
        IsDead = true; IsKnockedDown = false; IsRecovering = false;
        if (motor != null) motor.enabled = false;
        if (contactShadow != null) contactShadow.gameObject.SetActive(false);
        LaunchAttached(point, direction, struck, speed, lift);
    }

    private void LaunchAttached(Vector3 point, Vector3 direction, FacilityHitRegion struck, float speed, float lift)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            if (!Attached(i)) continue;
            Rigidbody body = bodies[i];
            MakeDynamic(body);
            body.linearVelocity = direction * speed + Vector3.up * lift;
            body.angularVelocity = transform.right * -3.2f;
        }
        Rigidbody impact = struck != null && !struck.IsDetached ? struck.body : (chest != null ? chest.GetComponent<Rigidbody>() : null);
        if (impact != null && !impact.isKinematic) ImpulseBody(impact, point, direction, 9f);
    }

    private static void MakeDynamic(Rigidbody body)
    {
        if (body == null || !body.gameObject.activeInHierarchy) return;
        body.isKinematic = false; body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.WakeUp();
    }

    private void ImpulseBody(Rigidbody body, Vector3 point, Vector3 direction, float force)
    {
        if (body == null || !body.gameObject.activeInHierarchy || body.isKinematic) return;
        body.WakeUp();
        body.AddForceAtPosition(direction * force + Vector3.up * force * .15f, point, ForceMode.Impulse);
        body.AddTorque(transform.right * -force * .23f, ForceMode.Impulse);
    }

    private void PlayVoice(bool fatal)
    {
        if (zombie != null) zombie.SilenceMoan();
        if (voice == null) return;
        AudioClip clip = fatal ? deathSound : hurtSound;
        if (clip == null) return;
        // A shell can hit the torso and head in the same physics step. Keep its first scream intact.
        if (voice.isPlaying && Time.time - lastVoiceAt < .08f) return;
        voice.Stop();
        voice.pitch = IsHostile ? .92f : Random.Range(.96f, 1.04f);
        voice.clip = clip;
        voice.Play();
        lastVoiceAt = Time.time;
        LastVoicePlayed = clip;
        VoiceEvents++;
    }

    private bool Attached(int index)
        => bodies[index] != null && bodies[index].gameObject.activeInHierarchy && (bodyRegions[index] == null || !bodyRegions[index].IsDetached);

    private void BeginRecovery()
    {
        if (pelvis == null || motor == null || !FindRecoveryPose(out Vector3 feet))
        { settleUntil = Time.time + .25f; return; }
        for (int i = 0; i < bodies.Length; i++)
        {
            if (!Attached(i)) continue;
            recoveryPositions[i] = bodies[i].position; recoveryRotations[i] = bodies[i].rotation;
            bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero;
            bodies[i].collisionDetectionMode = CollisionDetectionMode.Discrete;
            bodies[i].isKinematic = true;
        }
        Vector3 facing = player != null ? player.position - feet : transform.forward;
        facing.y = 0;
        transform.SetPositionAndRotation(feet, facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : transform.rotation);
        // Restore physical world poses after moving the actor root; detached limbs live in their own hierarchy.
        for (int i = 0; i < bodies.Length; i++)
            if (Attached(i)) bodies[i].transform.SetPositionAndRotation(recoveryPositions[i], recoveryRotations[i]);
        recoveryStart = Time.time;
        IsRecovering = true;
    }

    private bool FindRecoveryPose(out Vector3 feet)
    {
        Vector3 centre = pelvis.position;
        float height = IsCrawling ? 1.1f : standingHeight;
        for (int attempt = 0; attempt < 17; attempt++)
        {
            float angle = attempt * 2.399963f;
            float radius = attempt == 0 ? 0 : .35f + .065f * attempt;
            Vector3 sample = centre + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            int count = Physics.RaycastNonAlloc(sample + Vector3.up * 1.5f, Vector3.down, floorHits, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float highest = float.NegativeInfinity;
            Vector3 floor = Vector3.zero;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = floorHits[i];
                if (!WorldCollider(hit.collider) || hit.normal.y < .72f || hit.point.y > centre.y + .45f || hit.point.y <= highest) continue;
                highest = hit.point.y; floor = hit.point; found = true;
            }
            if (!found) continue;
            // A supported centre is insufficient on a machinery lip or the end of a tread.
            // Require support beneath the capsule footprint before committing to a get-up.
            bool stableFooting = true;
            for (int edge = 0; edge < 8 && stableFooting; edge++)
            {
                float angleAround = edge * Mathf.PI * .25f;
                Vector3 rim = floor + new Vector3(Mathf.Cos(angleAround), 0f, Mathf.Sin(angleAround)) * standingRadius * .8f;
                int supports = Physics.RaycastNonAlloc(rim + Vector3.up * .16f, Vector3.down, floorHits, .32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                stableFooting = false;
                for (int support = 0; support < supports; support++)
                    if (WorldCollider(floorHits[support].collider) && floorHits[support].normal.y >= .72f && Mathf.Abs(floorHits[support].point.y - floor.y) <= .08f)
                    { stableFooting = true; break; }
            }
            if (!stableFooting) continue;
            floor += Vector3.up * .045f;
            float capsuleRadius = Mathf.Max(.1f, standingRadius - .01f);
            Vector3 lower = floor + Vector3.up * capsuleRadius, upper = floor + Vector3.up * (height - capsuleRadius);
            int overlaps = Physics.OverlapCapsuleNonAlloc(lower, upper, capsuleRadius, clearanceHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool clear = overlaps < clearanceHits.Length;
            for (int i = 0; i < overlaps && clear; i++) if (WorldCollider(clearanceHits[i])) clear = false;
            if (!clear) continue;
            feet = floor; return true;
        }
        feet = Vector3.zero; return false;
    }

    private bool WorldCollider(Collider item)
    {
        if (item == null || item.isTrigger || item == motor || item.attachedRigidbody != null) return false;
        if (item.transform.IsChildOf(transform) || (detachedRoot != null && item.transform.IsChildOf(detachedRoot))) return false;
        if (player != null && (item.transform.IsChildOf(player) || player.IsChildOf(item.transform))) return false;
        return true;
    }

    private void BlendRecovery()
    {
        float fraction = Mathf.Clamp01((Time.time - recoveryStart) / GetUpTime);
        float smooth = fraction * fraction * (3 - 2 * fraction);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (!Attached(i)) continue;
            bodies[i].transform.SetPositionAndRotation(
                Vector3.Lerp(recoveryPositions[i], transform.TransformPoint(TargetPosition(i)), smooth),
                Quaternion.Slerp(recoveryRotations[i], transform.rotation * TargetRotation(i), smooth));
        }
        if (fraction < 1) return;
        motor.height = IsCrawling ? 1.1f : standingHeight;
        motor.center = IsCrawling ? new Vector3(0, .55f, 0) : standingCentre;
        motor.enabled = true;
        verticalSpeed = -2f;
        resumePursuitAt = Time.time + .3f;
        IsKnockedDown = IsRecovering = false;
        RecoveryCount++;
        if (contactShadow != null) contactShadow.gameObject.SetActive(true);
        Physics.SyncTransforms();
    }

    private Vector3 TargetPosition(int index)
    {
        if (!IsCrawling || bodyRegions[index] == null) return restPositions[index];
        Vector3 position = restPositions[index];
        switch (bodyRegions[index].kind)
        {
            case FacilityBodyRegion.Pelvis: return new Vector3(0, .43f, 0);
            case FacilityBodyRegion.Torso: return new Vector3(0, .65f, .20f);
            case FacilityBodyRegion.Head: return new Vector3(0, .94f, .46f);
            case FacilityBodyRegion.UpperArmLeft: case FacilityBodyRegion.UpperArmRight: return new Vector3(position.x, .70f, .26f);
            case FacilityBodyRegion.ForearmLeft: case FacilityBodyRegion.ForearmRight: return new Vector3(position.x, .45f, .40f);
            case FacilityBodyRegion.ThighLeft: case FacilityBodyRegion.ThighRight: return new Vector3(position.x, .40f, -.04f);
            case FacilityBodyRegion.ShinLeft: case FacilityBodyRegion.ShinRight: return new Vector3(position.x, .32f, -.36f);
            default: return position;
        }
    }

    private Quaternion TargetRotation(int index)
    {
        if (!IsCrawling || bodyRegions[index] == null) return restRotations[index];
        float pitch;
        switch (bodyRegions[index].kind)
        {
            case FacilityBodyRegion.Pelvis: pitch = 25; break;
            case FacilityBodyRegion.Torso: pitch = 55; break;
            case FacilityBodyRegion.UpperArmLeft: case FacilityBodyRegion.UpperArmRight: pitch = -32; break;
            case FacilityBodyRegion.ForearmLeft: case FacilityBodyRegion.ForearmRight: pitch = -44; break;
            case FacilityBodyRegion.ThighLeft: case FacilityBodyRegion.ThighRight: pitch = 70; break;
            case FacilityBodyRegion.ShinLeft: case FacilityBodyRegion.ShinRight: pitch = 100; break;
            default: pitch = 0; break;
        }
        return restRotations[index] * Quaternion.Euler(pitch, 0, 0);
    }

    private void OnDestroy()
    {
        if (detachedRoot != null) Destroy(detachedRoot.gameObject);
    }
}
