using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Fixed-size blood and debris pools; shots recycle objects rather than allocating new ones.</summary>
public sealed class FacilityGorePool : MonoBehaviour
{
    public Mesh dropletMesh;
    public Mesh chunkMesh;
    public Mesh stainMesh;
    public Material bloodMaterial;
    public Material fleshMaterial;
    public Material boneMaterial;
    public AudioSource impactSource;
    public AudioClip impactClip;
    public Collider[] ownerColliders;
    public int ActiveDroplets { get; private set; }
    public int ActiveChunks { get; private set; }
    public int ActiveStains { get; private set; }

    public int DropletCapacity => DropCount;
    public int ChunkCapacity => ChunkCount;
    public int StainCapacity => StainCount;
    public int TotalDropsEmitted { get; private set; }
    public int TotalChunksEmitted { get; private set; }
    public int HeadBurstCount { get; private set; }
    public int SeverBurstCount { get; private set; }

    private const int DropCount = 220;
    private const int ChunkCount = 36;
    private const int StainCount = 96;
    private sealed class Drop
    {
        public Transform transform;
        public Vector3 velocity;
        public float life;
        public float width;
    }
    private sealed class Chunk
    {
        public Transform transform;
        public Rigidbody body;
        public float life;
        public bool sleeping;
    }
    private Drop[] drops;
    private Chunk[] chunks;
    private Transform[] stains;
    private float[] stainLives;
    private int nextDrop, nextChunk, nextStain;
    private Transform poolRoot;
    private uint randomState = 426017u;
    private readonly RaycastHit[] queryHits = new RaycastHit[12];

    private void Awake()
    {
        GameObject root = new GameObject("Blood and debris pool (bounded)");
        poolRoot = root.transform;
        // Deliberately outside the animated character: splashes stay on the facility surfaces.
        poolRoot.SetParent(transform.parent, false);
        drops = new Drop[DropCount];
        chunks = new Chunk[ChunkCount];
        stains = new Transform[StainCount];
        stainLives = new float[StainCount];
        for (int i = 0; i < drops.Length; i++)
        {
            Transform visual = Visual("Blood streak " + i, dropletMesh, bloodMaterial);
            drops[i] = new Drop { transform = visual };
        }
        for (int i = 0; i < chunks.Length; i++)
        {
            Transform visual = Visual("Physical tissue " + i, chunkMesh, i % 5 == 0 ? boneMaterial : fleshMaterial);
            BoxCollider collider = visual.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(.7f, .75f, .7f);
            Rigidbody body = visual.gameObject.AddComponent<Rigidbody>();
            body.mass = .12f;
            body.linearDamping = .22f;
            body.angularDamping = .55f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.maxAngularVelocity = 24f;
            body.isKinematic = true;
            if (ownerColliders != null)
                foreach (Collider other in ownerColliders)
                    if (other != null) Physics.IgnoreCollision(collider, other);
            for (int previous = 0; previous < i; previous++)
                Physics.IgnoreCollision(collider, chunks[previous].body.GetComponent<Collider>());
            chunks[i] = new Chunk { transform = visual, body = body };
        }
        for (int i = 0; i < stains.Length; i++)
            stains[i] = Visual("Surface blood " + i, stainMesh, bloodMaterial);
    }

    private Transform Visual(string label, Mesh mesh, Material material)
    {
        GameObject visual = new GameObject(label);
        visual.layer = 2; // Ignore Raycast: debris never absorbs a later shotgun discharge.
        visual.transform.SetParent(poolRoot, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        visual.SetActive(false);
        return visual.transform;
    }

    public void Burst(Vector3 point, Vector3 direction, bool lethal, bool playImpact = true)
        => Emit(point, direction, lethal ? 170 : 90, lethal ? 22 : 10, lethal ? 1.1f : .65f, lethal, playImpact);

    public void BurstHead(Vector3 point, Vector3 direction)
    {
        HeadBurstCount++;
        Emit(point, direction, 210, 32, 1.4f, true, true);
    }

    public void BurstSever(Vector3 point, Vector3 direction)
    {
        SeverBurstCount++;
        Emit(point, direction, 120, 16, .85f, true, false);
    }

    private void Emit(Vector3 point, Vector3 direction, int count, int debris, float splashSize, bool lethal, bool playImpact)
    {
        if (drops == null) return;
        if (playImpact && impactSource != null && impactClip != null)
        {
            impactSource.transform.position = point;
            impactSource.pitch = Range(.91f, 1.055f);
            impactSource.PlayOneShot(impactClip, lethal ? .95f : .72f);
        }
        direction = direction.sqrMagnitude > .01f ? direction.normalized : transform.forward;
        TotalDropsEmitted += count;
        TotalChunksEmitted += debris;
        for (int i = 0; i < count; i++)
        {
            Drop drop = drops[nextDrop++ % drops.Length];
            drop.transform.position = point + Sphere() * .075f;
            Vector3 scatter = Sphere();
            // Most spray follows the strike; the fan and back spatter make the impact legible from the gun.
            drop.velocity = direction * Range(3f, lethal ? 10f : 7f) + scatter * Range(2f, 6f) + Vector3.up * Range(1f, 4.5f);
            if (i % 5 == 0) drop.velocity = -direction * Range(1.5f, 4f) + scatter * 2f + Vector3.up * 2f;
            drop.life = Range(.8f, 1.7f);
            drop.width = Range(.022f, .060f);
            // A hit can arrive after this pool's Update. Initialize the visible streak now,
            // otherwise the first use renders a unit-size blood ball for one whole frame.
            if (drop.velocity.sqrMagnitude > .01f) drop.transform.rotation = Quaternion.LookRotation(drop.velocity);
            drop.transform.localScale = new Vector3(drop.width, drop.width, Mathf.Min(.3f, .055f + drop.velocity.magnitude * .014f));
            drop.transform.gameObject.SetActive(true);
        }
        for (int i = 0; i < debris; i++)
        {
            Chunk chunk = chunks[nextChunk++ % chunks.Length];
            chunk.body.isKinematic = true;
            chunk.transform.position = point + Sphere() * .12f;
            chunk.transform.rotation = Quaternion.Euler(Range(0, 360), Range(0, 360), Range(0, 360));
            chunk.transform.localScale = new Vector3(Range(.07f, .20f), Range(.06f, .18f), Range(.06f, .16f));
            chunk.transform.gameObject.SetActive(true);
            chunk.body.isKinematic = false;
            chunk.body.linearVelocity = direction * Range(3f, lethal ? 9f : 6f) + Sphere() * 3f + Vector3.up * Range(2f, 5f);
            chunk.body.angularVelocity = Sphere() * 20f;
            chunk.life = 45f;
            chunk.sleeping = false;
        }
        // Place a broad splash on actual nearby architecture, never at a guessed floor height.
        if (FindSurface(point, direction + Vector3.down * .12f, 6f, out RaycastHit hit))
            Stain(hit, splashSize);
        if (FindSurface(point, Vector3.down, 4f, out hit))
            Stain(hit, splashSize * 1.1f);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, .04f);
        ActiveDroplets = ActiveChunks = ActiveStains = 0;
        if (drops == null) return;
        foreach (Drop drop in drops)
        {
            if (drop.life <= 0f) continue;
            drop.life -= dt;
            Vector3 old = drop.transform.position;
            drop.velocity += Vector3.down * (15f * dt);
            Vector3 delta = drop.velocity * dt;
            if (FindSurface(old, delta, delta.magnitude + .02f, out RaycastHit hit))
            {
                Stain(hit, Range(.10f, .30f));
                drop.life = 0;
            }
            if (drop.life <= 0f) { drop.transform.gameObject.SetActive(false); continue; }
            drop.transform.position = old + delta;
            if (drop.velocity.sqrMagnitude > .01f) drop.transform.rotation = Quaternion.LookRotation(drop.velocity);
            drop.transform.localScale = new Vector3(drop.width, drop.width, Mathf.Min(.3f, .055f + drop.velocity.magnitude * .014f));
            ActiveDroplets++;
        }
        foreach (Chunk chunk in chunks)
        {
            if (chunk.life <= 0f) continue;
            chunk.life -= dt;
            if (chunk.life <= 0f)
            {
                chunk.body.isKinematic = true;
                chunk.transform.gameObject.SetActive(false);
                continue;
            }
            if (!chunk.sleeping && chunk.life < 43f && chunk.body.linearVelocity.sqrMagnitude < .04f)
            {
                chunk.body.Sleep();
                chunk.sleeping = true;
            }
            ActiveChunks++;
        }
        for (int i = 0; i < stains.Length; i++)
        {
            if (stainLives[i] <= 0f) continue;
            stainLives[i] -= dt;
            if (stainLives[i] <= 0f) stains[i].gameObject.SetActive(false);
            else ActiveStains++;
        }
    }

    private bool FindSurface(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default;
        if (direction.sqrMagnitude < .000001f) return false;
        int count = Physics.RaycastNonAlloc(origin, direction.normalized, queryHits, distance, ~(1 << 2), QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = queryHits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform) || candidate is CharacterController || candidate.attachedRigidbody != null) continue;
            if (queryHits[i].distance < best) { best = queryHits[i].distance; closest = queryHits[i]; found = true; }
        }
        return found;
    }

    private void Stain(RaycastHit surface, float size)
    {
        int index = nextStain++ % stains.Length;
        Transform stain = stains[index];
        stain.position = surface.point + surface.normal * .0018f;
        stain.rotation = Quaternion.LookRotation(surface.normal) * Quaternion.Euler(0, 0, Range(0, 360));
        float aspect = Range(.7f, 1.25f);
        bool supported = false;
        for (int fit = 0; fit < 6; fit++)
        {
            stain.localScale = new Vector3(size * aspect, size, 1);
            supported = true;
            for (int edge = 0; edge < 8 && supported; edge++)
            {
                float angle = edge * Mathf.PI * .25f;
                Vector3 rim = stain.position + stain.right * (Mathf.Cos(angle) * size * aspect * .5f)
                    + stain.up * (Mathf.Sin(angle) * size * .5f);
                Ray ray = new Ray(rim + surface.normal * .02f, -surface.normal);
                supported = surface.collider.Raycast(ray, out RaycastHit backing, .045f)
                    && Mathf.Abs(Vector3.Dot(backing.point - surface.point, surface.normal)) < .004f;
            }
            if (supported) break;
            size *= .55f;
        }
        if (!supported)
        {
            stain.gameObject.SetActive(false);
            stainLives[index] = 0f;
            return;
        }
        stain.gameObject.SetActive(true);
        stainLives[index] = 120f;
    }

    private float Range(float min, float max)
    {
        randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
        return min + (randomState & 0xffffff) / 16777216f * (max - min);
    }
    private Vector3 Sphere()
    {
        Vector3 value = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
        return value.sqrMagnitude > .001f ? value.normalized * Range(.35f, 1f) : Vector3.up;
    }

    private void OnDestroy()
    {
        if (poolRoot != null) Destroy(poolRoot.gameObject);
    }
}
