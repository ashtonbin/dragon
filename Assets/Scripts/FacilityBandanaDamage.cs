using UnityEngine;

/// <summary>Chest damage tears away the overlapping cloth, preserving the visible wound beneath.</summary>
public sealed class FacilityBandanaDamage : MonoBehaviour
{
    public Renderer drape;
    public Renderer[] coveredPanels;

    void LateUpdate()
    {
        if(drape==null || !drape.enabled || coveredPanels==null)return;
        foreach(var panel in coveredPanels)
            if(panel!=null && !panel.enabled){drape.enabled=false;break;}
    }
}
