using UnityEngine;

/// <summary>Feeds the real cutting bit into the door while the magnetic foot remains attached.</summary>
[DisallowMultipleComponent]
public sealed class FacilityDrillMotion : MonoBehaviour
{
    public FacilityVaultDrill drilling;
    public Transform[] feedParts;
    public Transform[] spinningParts;
    public Transform feedCrank;
    [Min(0f)] public float feedDistance = .075f;
    [Min(.1f)] public float feedSeconds = .65f;
    [Min(0f)] public float revolutionsPerMinute = 780f;
    public float CurrentFeed { get; private set; }
    private Vector3[] restPositions;
    private Quaternion[] spinRest;
    private Quaternion crankRest;
    private float rotation;

    private void Awake()
    {
        int feeds = feedParts != null ? feedParts.Length : 0;
        int spins = spinningParts != null ? spinningParts.Length : 0;
        restPositions = new Vector3[feeds]; spinRest = new Quaternion[spins];
        for (int i = 0; i < feeds; i++) if (feedParts[i] != null) restPositions[i] = feedParts[i].localPosition;
        for (int i = 0; i < spins; i++) if (spinningParts[i] != null) spinRest[i] = spinningParts[i].localRotation;
        if (feedCrank != null) crankRest = feedCrank.localRotation;
    }

    private void Update()
    {
        if (drilling == null || !drilling.IsRunning || Time.deltaTime <= 0f) return;
        CurrentFeed = Mathf.MoveTowards(CurrentFeed, feedDistance, Time.deltaTime * feedDistance / Mathf.Max(.1f, feedSeconds));
        rotation = Mathf.Repeat(rotation + Time.deltaTime * revolutionsPerMinute * 6f, 360f);
        for (int i = 0; i < restPositions.Length; i++)
            if (feedParts[i] != null) feedParts[i].localPosition = restPositions[i] + Vector3.down * CurrentFeed;
        Quaternion spin = Quaternion.AngleAxis(rotation, Vector3.up);
        for (int i = 0; i < spinRest.Length; i++)
            if (spinningParts[i] != null) spinningParts[i].localRotation = spinRest[i] * spin;
        if (feedCrank != null && feedDistance > .0001f)
            feedCrank.localRotation = crankRest * Quaternion.AngleAxis(-55f * CurrentFeed / feedDistance, Vector3.right);
    }
}
