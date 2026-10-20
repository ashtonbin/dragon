using UnityEngine;

public enum FacilityBodyRegion
{
    Pelvis, Torso, Head,
    UpperArmLeft, ForearmLeft, UpperArmRight, ForearmRight,
    ThighLeft, ShinLeft, ThighRight, ShinRight
}

/// <summary>A body collider and the small, individually removable outer layers around it.</summary>
public sealed class FacilityHitRegion : MonoBehaviour
{
    public FacilityHumanoid owner;
    public FacilityBodyRegion kind;
    public FacilityHitRegion parentRegion;
    public CharacterJoint joint;
    public GameObject parentStump;
    public GameObject detachedStump;
    public GameObject[] cutCovers;
    public Rigidbody body;
    public Renderer[] outerPatches;
    public GameObject[] woundBeds;
    public string regionName;
    public bool IsDetached { get; internal set; }
    public bool JointSevered { get; internal set; }
    internal int lastPelletShot = int.MinValue;
    internal int limbHits;

    public bool IsLimb => kind != FacilityBodyRegion.Head && kind != FacilityBodyRegion.Torso && kind != FacilityBodyRegion.Pelvis;

    public int Expose(Vector3 point, int count)
    {
        int removed = 0;
        for (int pass = 0; pass < count && outerPatches != null; pass++)
        {
            int best = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < outerPatches.Length; i++)
            {
                Renderer patch = outerPatches[i];
                if (patch == null || !patch.enabled) continue;
                float candidate = (patch.bounds.center - point).sqrMagnitude;
                if (candidate < distance) { distance = candidate; best = i; }
            }
            if (best < 0) break;
            outerPatches[best].enabled = false;
            if (woundBeds != null && best < woundBeds.Length && woundBeds[best] != null)
                woundBeds[best].SetActive(true);
            removed++;
        }
        return removed;
    }
}
