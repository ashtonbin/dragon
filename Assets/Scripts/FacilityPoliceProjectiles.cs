using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visible, finite-speed rounds. Every travelled segment is swept against real scene collision.</summary>
[DisallowMultipleComponent]
public sealed class FacilityPoliceProjectiles : MonoBehaviour
{
    public Mesh projectileMesh;
    public Material projectileMaterial;
    [Min(1f)] public float projectileSpeed = 13f;
    [Range(.025f, .12f)] public float projectileRadius = .055f;
    public int ShotsFired { get; private set; }
    public int WorldImpacts { get; private set; }
    public int PlayerImpacts { get; private set; }
    public int ActiveProjectiles { get; private set; }
    public const int Capacity = 96;

    private struct Round
    {
        public Transform visual, owner;
        public FacilityPlayerHealth target;
        public Vector3 position, direction;
        public float remaining;
    }
    private readonly Round[] rounds = new Round[Capacity];
    private readonly RaycastHit[] hits = new RaycastHit[96];
    private readonly Collider[] overlaps = new Collider[64];
    private Transform poolRoot;
    private int cursor;

    private void Awake()
    {
        poolRoot = new GameObject("Police projectiles - bounded swept pool").transform;
        poolRoot.SetParent(transform, false);
        for (int i = 0; i < rounds.Length; i++)
        {
            GameObject visual = new GameObject("Visible police round " + i, typeof(MeshFilter), typeof(MeshRenderer));
            visual.layer = 2;
            visual.transform.SetParent(poolRoot, false);
            visual.GetComponent<MeshFilter>().sharedMesh = projectileMesh;
            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = projectileMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            visual.SetActive(false);
            rounds[i].visual = visual.transform;
        }
    }

    public bool Fire(Vector3 origin, Vector3 direction, Transform owner, FacilityPlayerHealth target)
    {
        if (Time.timeScale <= 0f || poolRoot == null || target == null || target.IsDead || direction.sqrMagnitude < .001f) return false;
        ShotsFired++;
        // A muzzle pushed into a wall cannot spawn its projectile on the other side of that wall.
        int overlapCount = Physics.OverlapSphereNonAlloc(origin, projectileRadius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (overlapCount >= overlaps.Length) { WorldImpacts++; return false; }
        for (int i = 0; i < overlapCount; i++)
            if (Blocks(overlaps[i], owner, target)) { WorldImpacts++; return false; }
        int slot = cursor; cursor = (cursor + 1) % rounds.Length;
        if (rounds[slot].remaining <= 0f) ActiveProjectiles++;
        rounds[slot].owner = owner;
        rounds[slot].target = target;
        rounds[slot].position = origin;
        rounds[slot].direction = direction.normalized;
        rounds[slot].remaining = 5f;
        Transform tracer = rounds[slot].visual;
        tracer.SetPositionAndRotation(origin - direction.normalized * .14f, Quaternion.LookRotation(direction));
        tracer.localScale = new Vector3(.055f, .055f, .34f);
        tracer.gameObject.SetActive(true);
        return true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        float elapsed = Time.deltaTime;
        for (int i = 0; i < rounds.Length; i++)
        {
            if (rounds[i].remaining <= 0f) continue;
            Round round = rounds[i];
            float distance = projectileSpeed * Mathf.Min(elapsed, round.remaining);
            int count = Physics.SphereCastNonAlloc(round.position, projectileRadius, round.direction, hits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider closest = null;
            float nearest = float.PositiveInfinity;
            // A saturated query fails closed; it must never permit a round to tunnel through scenery.
            bool hit = count >= hits.Length;
            for (int j = 0; j < count; j++)
            {
                if (!Blocks(hits[j].collider, round.owner, round.target) || hits[j].distance >= nearest) continue;
                closest = hits[j].collider; nearest = hits[j].distance; hit = true;
            }
            if (hit)
            {
                if (closest != null && closest.GetComponentInParent<FacilityPlayerHealth>() == round.target)
                {
                    PlayerImpacts++;
                    round.target.TryTakeHit(round.owner);
                }
                else WorldImpacts++;
                round.remaining = 0f;
            }
            else
            {
                round.position += round.direction * distance;
                round.visual.position = round.position - round.direction * .14f;
                round.remaining = Mathf.Max(0f, round.remaining - elapsed);
            }
            if (round.remaining <= 0f) { round.visual.gameObject.SetActive(false); ActiveProjectiles--; }
            rounds[i] = round;
        }
    }

    private static bool Blocks(Collider item, Transform owner, FacilityPlayerHealth target)
    {
        if (item == null || item.isTrigger) return false;
        if (owner != null && item.transform.IsChildOf(owner)) return false;
        if (item.GetComponentInParent<FacilityPlayerHealth>() == target) return true;
        // Other enemies and their detached anatomy do not intercept police rounds or create random friendly fire.
        if (item.GetComponentInParent<FacilityHumanoid>() != null || item.GetComponentInParent<FacilityHitRegion>() != null) return false;
        return true;
    }

    public void Clear()
    {
        for (int i = 0; i < rounds.Length; i++)
        {
            rounds[i].remaining = 0f;
            if (rounds[i].visual != null) rounds[i].visual.gameObject.SetActive(false);
        }
        ActiveProjectiles = 0;
    }
}
