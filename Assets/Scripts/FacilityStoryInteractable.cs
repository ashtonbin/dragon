using UnityEngine;

public enum FacilityStoryInteractionKind { Pistol, Cash, Gold, Sedan, Drill, Door, Vault }

/// <summary>A saved, editable pickup or extraction point. The director owns its mission rules.</summary>
[DisallowMultipleComponent]
public sealed class FacilityStoryInteractable : MonoBehaviour
{
    public FacilityStoryDirector director;
    public FacilityStoryInteractionKind kind;
    public string label;
    public GameObject visualRoot;
    public FacilityDoor door;
    public FacilityVaultDrill vault;
    public bool Consumed { get; private set; }

    private void Awake()
    {
        if (door != null && kind == FacilityStoryInteractionKind.Door) door.interaction = this;
    }

    public void Consume()
    {
        Consumed = true;
        if (visualRoot != null) visualRoot.SetActive(false);
        // A car remains visible after extraction; pickup renderers/colliders do not.
        if (kind != FacilityStoryInteractionKind.Sedan) gameObject.SetActive(false);
    }
}
