using UnityEngine;

/// <summary>The final assault's armored variant. Anatomy and gun behavior remain shared with police.</summary>
[DefaultExecutionOrder(-50), DisallowMultipleComponent, RequireComponent(typeof(FacilityHumanoid), typeof(FacilityPoliceOfficer))]
public sealed class FacilityEliteSwat : MonoBehaviour
{
    public GameObject[] helmetPlates;
    public GameObject helmetDamageReveal;
    public Renderer eyeRenderer;
    public Renderer[] opticRenderers;
    public int HelmetHitsTaken { get; private set; }
    public bool HelmetBroken => HelmetHitsTaken >= 2;

    void Awake()
    {
        Configure();
        if (helmetDamageReveal != null) helmetDamageReveal.SetActive(false);
    }

    public void Configure()
    {
        var actor = GetComponent<FacilityHumanoid>();
        actor.hitsToKill = 6; actor.hitsToKnockDown = 3;
        actor.helmetHitsToBreak = 2; actor.hitsToSeverLimb = 2;
        actor.pursuitSpeed = 3.5f;
        var officer = GetComponent<FacilityPoliceOfficer>();
        officer.walkSpeed = 3.5f; officer.aimWindup = .26f; officer.shotInterval = .9f;
    }

    public void RegisterHelmetHit()
    {
        HelmetHitsTaken++;
        if (helmetPlates != null && helmetPlates.Length > 0)
        {
            int index = Mathf.Min(HelmetHitsTaken - 1, helmetPlates.Length - 1);
            if (helmetPlates[index] != null) helmetPlates[index].SetActive(false);
            if (HelmetBroken) foreach (var plate in helmetPlates) if (plate != null) plate.SetActive(false);
        }
        if (HelmetBroken && helmetDamageReveal != null) helmetDamageReveal.SetActive(true);
    }
}
