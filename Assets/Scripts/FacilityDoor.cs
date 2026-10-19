using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>A physically closed authored door. Its colliders follow the leaf through its full travel.</summary>
[DisallowMultipleComponent]
public sealed class FacilityDoor : MonoBehaviour
{
    public Transform leaf;
    public Vector3 openLocalEuler = new Vector3(0f, -100f, 0f);
    public Vector3 openLocalOffset;
    public bool locked;
    [Tooltip("Allow guards to open this door automatically while navigating. Disable for vehicle doors and trunks.")]
    public bool npcCanOpen = true;
    [Min(.1f)] public float travelSeconds = .72f;
    [Min(.5f)] public float npcOpenDistance = 2.1f;
    public Collider[] leafColliders;
    public FacilityStoryInteractable interaction;
    public bool IsOpen => travel >= .98f;
    public bool IsMoving => !Mathf.Approximately(travel, targetTravel);
    public bool WantsOpen => targetTravel > .5f;
    public bool IsLocked => locked;
    public float OpenFraction => travel;
    private static readonly List<FacilityDoor> doors = new List<FacilityDoor>();
    private readonly Collider[] closingHits = new Collider[24];
    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private Bounds closedBounds;
    private bool hasClosedBounds;
    private float travel, targetTravel;
    private NavMeshObstacle navigationLeaf;

    private void Awake()
    {
        if (leaf == null) leaf = transform;
        closedPosition = leaf.localPosition;
        closedRotation = leaf.localRotation;
        if (leafColliders == null || leafColliders.Length == 0) leafColliders = leaf.GetComponentsInChildren<Collider>();
        foreach (Collider collider in leafColliders)
        {
            if (collider == null || collider.isTrigger || !collider.enabled) continue;
            if (!hasClosedBounds) { closedBounds = collider.bounds; hasClosedBounds = true; }
            else closedBounds.Encapsulate(collider.bounds);
        }
        // Scene editing stores the closed position. Runtime never persists an open transform.
        travel = targetTravel = 0f;
        ApplyPose();
        navigationLeaf = leaf.GetComponent<NavMeshObstacle>();
        if (navigationLeaf != null) navigationLeaf.enabled = false;
    }

    private void OnEnable() { if (!doors.Contains(this)) doors.Add(this); }
    private void OnDisable() => doors.Remove(this);

    public bool TryToggle()
    {
        if (locked) return false;
        if (targetTravel > .5f && !CanClose()) return false;
        targetTravel = targetTravel > .5f ? 0f : 1f;
        return true;
    }

    public void ForceOpen()
    {
        locked = false;
        targetTravel = 1f;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f || Mathf.Approximately(travel, targetTravel)) return;
        // A character stepping into a closing doorway makes it reopen rather than being crushed.
        if (targetTravel < .5f && !CanClose()) targetTravel = 1f;
        travel = Mathf.MoveTowards(travel, targetTravel, Time.deltaTime / Mathf.Max(.1f, travelSeconds));
        ApplyPose();
        if (navigationLeaf != null) navigationLeaf.enabled = IsOpen;
        // Raycasts and swept projectiles must see the visible leaf immediately, even between physics ticks.
        Physics.SyncTransforms();
    }

    private void ApplyPose()
    {
        if (leaf == null) return;
        float smooth = travel * travel * (3f - 2f * travel);
        leaf.localPosition = closedPosition + openLocalOffset * smooth;
        leaf.localRotation = closedRotation * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Euler(openLocalEuler), smooth);
    }

    private bool CanClose()
    {
        if (leaf == null) return true;
        // Cover the leaf's current and closed positions, including the far end of a wide door.
        Bounds sweep = hasClosedBounds ? closedBounds : new Bounds(transform.position + Vector3.up, new Vector3(2f, 2f, .4f));
        foreach (Collider collider in leafColliders)
            if (collider != null && collider.enabled && !collider.isTrigger) sweep.Encapsulate(collider.bounds);
        Vector3 extents = sweep.extents + Vector3.one * .12f;
        int count = Physics.OverlapBoxNonAlloc(sweep.center, extents, closingHits,
            Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // A detailed vehicle may fill the fast query with harmless fixed colliders. In that rare
        // case inspect the complete overlap, rather than mistaking buffer saturation for a person.
        Collider[] hits = closingHits;
        if (count == closingHits.Length)
        {
            hits = Physics.OverlapBox(sweep.center, extents, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            if (hit is CharacterController || hit.GetComponentInParent<FacilityHumanoid>() != null) return false;
        }
        return true;
    }

    /// <summary>AI opens nearby ordinary doors; a locked vault is never opened by this route.</summary>
    public static bool TryOpenNear(Vector3 position, Vector3 direction)
    {
        direction.y = 0f;
        bool opened = false;
        for (int i = doors.Count - 1; i >= 0; i--)
        {
            FacilityDoor door = doors[i];
            if (door == null || !door.isActiveAndEnabled || !door.npcCanOpen || door.locked || door.targetTravel > .5f) continue;
            Vector3 nearest = door.hasClosedBounds ? door.closedBounds.ClosestPoint(position + Vector3.up) : door.transform.position;
            Vector3 offset = nearest - position;
            if (Mathf.Abs(offset.y) > 2.2f) continue;
            offset.y = 0f;
            if (offset.sqrMagnitude > door.npcOpenDistance * door.npcOpenDistance) continue;
            if (direction.sqrMagnitude > .01f && Vector3.Dot(direction.normalized, offset.normalized) < -.25f) continue;
            door.targetTravel = 1f;
            opened = true;
        }
        return opened;
    }

    public static bool WaitForPassage(Vector3 position, Vector3 direction)
    {
        TryOpenNear(position, direction);
        foreach (FacilityDoor door in doors)
        {
            if (door == null || !door.isActiveAndEnabled || !door.npcCanOpen || door.locked || !door.IsMoving || !door.hasClosedBounds) continue;
            Vector3 toHinge = door.transform.position - position; toHinge.y = 0;
            float radius = new Vector2(door.closedBounds.size.x,door.closedBounds.size.z).magnitude + .55f;
            // Wait outside the complete swing, including wide leaves opening toward this character.
            if (toHinge.sqrMagnitude < radius * radius) return true;
        }
        return false;
    }
}
