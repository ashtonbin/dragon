using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public enum FacilitySpawnKind { Observer, Zombie, EliteSwat }

/// <summary>Places saved character prefabs on reachable floors after testing the real character clearance.</summary>
[DisallowMultipleComponent]
public sealed class FacilitySpawnController : MonoBehaviour
{
    public GameObject observerPrefab, zombiePrefab;
    public GameObject eliteSwatPrefab;
    public FacilityPoliceProjectiles policeProjectiles;
    public Material validPreviewMaterial, invalidPreviewMaterial;
    public Camera aimCamera;
    public RetroFPSController player;
    [Min(1)] public int maxCharacters = 8;
    [Min(2)] public float placementRange = 18f;
    public FacilitySpawnKind Selection { get; private set; }
    public bool HasSelection { get; private set; }
    public GameObject LastSpawned { get; private set; }
    public bool PlacementValid { get; private set; }
    public Vector3 PlacementFeet { get; private set; }
    public string PlacementReason { get; private set; } = "Aim at an open floor";
    public int RuntimeActorCount
    {
        get { int count = 0; foreach (GameObject actor in spawned) if (actor != null) count++; return count; }
    }

    private readonly List<GameObject> spawned = new List<GameObject>(8);
    private readonly Collider[] overlaps = new Collider[64];
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private Transform spawnedRoot, previewRoot;
    private MeshRenderer[] previewRenderers;
    private bool previewWasValid;
    private float clickReadyAt;
    private FacilityGameSession session;
    private const float CapsuleHeight = 1.78f, CapsuleRadius = .29f;

    private void Awake()
    {
        session = GetComponent<FacilityGameSession>();
        if (player == null) player = FindFirstObjectByType<RetroFPSController>();
        if (aimCamera == null && player != null) aimCamera = player.GetComponentInChildren<Camera>();
        spawnedRoot = new GameObject("Spawned Characters").transform;
        BuildPreview();
    }

    public void Select(FacilitySpawnKind kind)
    {
        Selection = kind;
        HasSelection = true;
        clickReadyAt = Time.unscaledTime + .18f;
        UpdatePlacement();
    }

    public void CancelSelection()
    {
        HasSelection = PlacementValid = false;
        if (previewRoot != null) previewRoot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!HasSelection || session == null || session.CurrentState != FacilityGameState.Placing) return;
        UpdatePlacement();
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (mouse.rightButton.wasPressedThisFrame) { session.CancelPlacement(); return; }
        if (mouse.leftButton.wasPressedThisFrame && Time.unscaledTime >= clickReadyAt && PlacementValid)
        {
            if (TryPlaceSelected(PlacementFeet, out string reason)) session.CancelPlacement();
            else PlacementReason = reason;
        }
    }

    private void UpdatePlacement()
    {
        if (aimCamera == null) return;
        Ray ray = new Ray(aimCamera.transform.position, aimCamera.transform.forward);
        PlacementValid = TryGetPlacement(ray, out Vector3 feet, out string reason);
        PlacementFeet = feet; PlacementReason = reason;
        if (previewRoot == null) return;
        // No unsupported preview is left floating at the last valid location.
        bool hasSurface = feet != Vector3.positiveInfinity;
        previewRoot.gameObject.SetActive(hasSurface && HasSelection);
        if (hasSurface)
        {
            Vector3 towardPlayer = player != null ? player.transform.position - feet : Vector3.back;
            towardPlayer.y = 0f;
            previewRoot.SetPositionAndRotation(feet, towardPlayer.sqrMagnitude > .001f ? Quaternion.LookRotation(towardPlayer) : Quaternion.identity);
        }
        if (previewRenderers != null && previewWasValid != PlacementValid)
        {
            previewWasValid = PlacementValid;
            foreach (MeshRenderer renderer in previewRenderers)
                renderer.sharedMaterial = PlacementValid ? validPreviewMaterial : invalidPreviewMaterial;
        }
    }

    public bool TryGetPlacement(Ray ray, out Vector3 feet, out string reason)
    {
        feet = Vector3.positiveInfinity;
        int count = Physics.RaycastNonAlloc(ray, hits, placementRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        RaycastHit first = default;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider item = hits[i].collider;
            if (item == null || (player != null && item.transform.IsChildOf(player.transform)) || hits[i].distance >= nearest) continue;
            first = hits[i]; nearest = first.distance; found = true;
        }
        if (!found) { reason = "Aim at a floor within " + placementRange.ToString("0") + " m"; return false; }
        if (first.normal.y < .72f || first.collider.attachedRigidbody != null || first.collider is CharacterController)
        { reason = "Aim at an open floor"; return false; }
        feet = first.point + Vector3.up * .045f;
        return ValidatePlacement(feet, out reason);
    }

    public bool TryPlaceSelected(Vector3 feet, out string reason)
    {
        if (!HasSelection) { reason = "Choose a character first"; return false; }
        if (session != null && session.CurrentState != FacilityGameState.Placing) { reason = "Open the spawn menu first"; return false; }
        if (!ValidatePlacement(feet, out reason)) return false;
        GameObject prefab = Selection == FacilitySpawnKind.EliteSwat ? eliteSwatPrefab : Selection == FacilitySpawnKind.Zombie ? zombiePrefab : observerPrefab;
        if (prefab == null) { reason = "Character prefab is missing"; return false; }
        Vector3 facing = player != null ? player.transform.position - feet : Vector3.back;
        facing.y = 0;
        // Prefabs are deliberately saved inactive: bind the real player before any Awake or pool setup.
        GameObject actor = Instantiate(prefab, feet, facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : Quaternion.identity, spawnedRoot);
        actor.name = Selection == FacilitySpawnKind.EliteSwat ? "Spawned Elite SWAT" : Selection == FacilitySpawnKind.Zombie ? "Spawned Zombie" : "Spawned Observer";
        FacilityHumanoid humanoid = actor.GetComponent<FacilityHumanoid>();
        if (humanoid != null) humanoid.player = player != null ? player.transform : null;
        if (humanoid != null && humanoid.gore != null && humanoid.gore.ownerColliders != null && player != null)
        {
            CharacterController playerMotor = player.GetComponent<CharacterController>();
            // The authored prefab retains a null slot where its scene-only Player reference was cleared.
            for (int i = 0; i < humanoid.gore.ownerColliders.Length; i++)
                if (humanoid.gore.ownerColliders[i] == null) humanoid.gore.ownerColliders[i] = playerMotor;
        }
        FacilityZombie zombie = actor.GetComponent<FacilityZombie>();
        if (zombie != null) zombie.targetHealth = player != null ? player.GetComponent<FacilityPlayerHealth>() : null;
        FacilityPoliceOfficer officer = actor.GetComponent<FacilityPoliceOfficer>();
        if (officer != null)
        {
            humanoid.IsHostile = true;
            officer.actor = humanoid;
            officer.target = player != null ? player.GetComponent<FacilityPlayerHealth>() : null;
            officer.projectileSystem = policeProjectiles;
        }
        SubtleEnemyHealthBar bar = actor.GetComponentInChildren<SubtleEnemyHealthBar>(true);
        if (bar != null) bar.player = player != null ? player.transform : null;
        actor.SetActive(true);
        spawned.Add(actor);
        LastSpawned = actor;
        Physics.SyncTransforms();
        reason = "Placed";
        return true;
    }

    private bool ValidatePlacement(Vector3 feet, out string reason)
    {
        if (float.IsNaN(feet.x) || float.IsInfinity(feet.x) || float.IsNaN(feet.y) || float.IsInfinity(feet.y) || float.IsNaN(feet.z) || float.IsInfinity(feet.z))
        { reason = "Aim at an open floor"; return false; }
        if (RuntimeActorCount >= maxCharacters) { reason = "Character limit reached (" + maxCharacters + ")"; return false; }
        if (player != null)
        {
            float distance = Vector3.Distance(player.transform.position, feet);
            if (distance < 1.15f) { reason = "Too close to you"; return false; }
            if (distance > placementRange + 1f) { reason = "Too far away"; return false; }
        }
        if (!NavMesh.SamplePosition(feet, out NavMeshHit navigation, .3f, NavMesh.AllAreas) ||
            Vector3.Distance(navigation.position, feet) > .26f)
        { reason = "Choose a walkable floor"; return false; }
        int supportCount = Physics.RaycastNonAlloc(feet + Vector3.up * .18f, Vector3.down, hits, .4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool supported = false;
        for (int i = 0; i < supportCount; i++)
        {
            Collider item = hits[i].collider;
            if (item != null && item.attachedRigidbody == null && !(item is CharacterController) && hits[i].normal.y >= .72f && Mathf.Abs(hits[i].point.y - feet.y) <= .12f)
            { supported = true; break; }
        }
        if (!supported) { reason = "No floor beneath the character"; return false; }
        Vector3 lower = feet + Vector3.up * CapsuleRadius, upper = feet + Vector3.up * (CapsuleHeight - CapsuleRadius);
        int occupied = Physics.OverlapCapsuleNonAlloc(lower, upper, CapsuleRadius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (occupied > 0) { reason = "Not enough room here"; return false; }
        // A complete footprint prevents standing a capsule on a narrow machinery edge.
        for (int edge = 0; edge < 8; edge++)
        {
            float angle = edge * Mathf.PI * .25f;
            Vector3 rim = feet + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * CapsuleRadius * .8f;
            int count = Physics.RaycastNonAlloc(rim + Vector3.up * .12f, Vector3.down, hits, .3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
                if (hits[i].collider.attachedRigidbody == null && !(hits[i].collider is CharacterController) && hits[i].normal.y >= .72f && Mathf.Abs(hits[i].point.y - feet.y) <= .12f)
                { found = true; break; }
            if (!found) { reason = "Too close to an edge"; return false; }
        }
        reason = "Ready to place";
        return true;
    }

    private void BuildPreview()
    {
        if (observerPrefab == null || validPreviewMaterial == null || invalidPreviewMaterial == null) return;
        previewRoot = new GameObject("Character placement preview").transform;
        previewRoot.gameObject.layer = 2;
        var renderers = new List<MeshRenderer>();
        foreach (MeshRenderer source in observerPrefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!source.enabled) continue;
            Transform node = source.transform;
            bool visible = true;
            while (node != null && node != observerPrefab.transform)
            { if (!node.gameObject.activeSelf) { visible = false; break; } node = node.parent; }
            if (!visible) continue;
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 2 };
            part.transform.SetParent(previewRoot, false);
            part.transform.localPosition = observerPrefab.transform.InverseTransformPoint(source.transform.position);
            part.transform.localRotation = Quaternion.Inverse(observerPrefab.transform.rotation) * source.transform.rotation;
            part.transform.localScale = source.transform.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = invalidPreviewMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderers.Add(renderer);
        }
        previewRenderers = renderers.ToArray();
        previewRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (previewRoot != null) Destroy(previewRoot.gameObject);
        if (spawnedRoot != null) Destroy(spawnedRoot.gameObject);
    }
}
