using UnityEngine;
using UnityEngine.AI;

/// <summary>Physical clearance shared by pursuit motors, including moving doors and nearby characters.</summary>
public sealed class FacilityNpcNavigation
{
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly Vector3[] candidates = new Vector3[8];
    static readonly float[] Angles = { 40, -40, 70, -70, 100, -100, 135, -135 };
    float keepDirectionUntil;
    Vector3 avoiding;
    public string Status { get; private set; } = "Idle";
    public Collider LastBlockingCollider { get; private set; }
    public Vector3 LastBlockingNormal { get; private set; }

    public Vector3 Steer(FacilityHumanoid actor, Vector3 desired, float waypointDistance)
    {
        LastBlockingCollider = null;
        LastBlockingNormal = Vector3.zero;
        if (actor.motor == null || desired.sqrMagnitude < .001f) { Status = "No steering"; return Vector3.zero; }
        if (FacilityDoor.WaitForPassage(actor.transform.position, desired)) { Status = "Waiting for door"; return Vector3.zero; }
        if (Clear(actor, desired, Mathf.Clamp(waypointDistance,.1f,.65f))) { avoiding = Vector3.zero; Status = "Direct"; return desired; }
        if (Time.time < keepDirectionUntil && avoiding.sqrMagnitude > .01f && Clear(actor, avoiding, .65f)) { Status = "Avoiding"; return avoiding; }
        for (int i = 0; i < candidates.Length; i++) candidates[i] = Quaternion.Euler(0, Angles[i], 0) * desired;
        float best = float.NegativeInfinity;
        Vector3 result = Vector3.zero;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 direction = candidates[i];
            if (!Clear(actor, direction, .7f)) continue;
            Vector3 end = actor.transform.position + direction * .7f;
            if (!NavMesh.SamplePosition(end + Vector3.up * .15f, out NavMeshHit onMesh, .3f, NavMesh.AllAreas)) continue;
            if (NavMesh.Raycast(actor.transform.position, onMesh.position, out _, NavMesh.AllAreas)) continue;
            float score = Vector3.Dot(desired, direction);
            if (avoiding.sqrMagnitude > .01f) score += Vector3.Dot(avoiding, direction) * .3f;
            if (score > best) { best = score; result = direction; }
        }
        avoiding = result; keepDirectionUntil = Time.time + .35f;
        Status = result.sqrMagnitude > .01f ? "Avoiding" : "Blocked";
        return result;
    }

    bool Clear(FacilityHumanoid actor, Vector3 direction, float distance)
    {
        CharacterController motor = actor.motor;
        Vector3 centre = motor.transform.TransformPoint(motor.center);
        float radius = motor.radius + .025f;
        // Widen sideways without making the capsule taller. An enlarged radius with the old endpoints
        // buried the probe in the floor and every cast returned an initial-overlap hit, stopping low-set actors.
        float half = Mathf.Max(0, motor.height * .5f - radius);
        // CharacterController permits skin-width floor contact. Keep the predictive probe above that contact;
        // gravity, slope limits and the real motor still own all walking-floor and step collisions.
        Vector3 lower = centre - Vector3.up * half + Vector3.up * (motor.skinWidth + .01f);
        int count = Physics.CapsuleCastNonAlloc(lower, centre + Vector3.up * half,
            radius, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null || collider == motor || collider.transform.IsChildOf(actor.transform)) continue;
            if (actor.player != null && collider.transform.IsChildOf(actor.player)) continue;
            // The walking floor is already handled by gravity and the capsule's step offset.
            if (hit.normal.y > .65f) continue;
            LastBlockingCollider = collider;
            LastBlockingNormal = hit.normal;
            return false;
        }
        return true;
    }
}
