using UnityEngine;

/// <summary>A small physical bar: depth tested, subdued, hidden until the first injury.</summary>
public sealed class SubtleEnemyHealthBar : MonoBehaviour
{
    public FacilityHumanoid owner;
    public Transform player;
    public Transform fill;
    public Renderer[] barRenderers;
    private float fillWidth;

    private void Awake()
    {
        fillWidth = fill != null ? fill.localScale.x : .36f;
    }

    private void LateUpdate()
    {
        if (owner == null || player == null) return;
        Transform anchor = owner.head != null && owner.head.gameObject.activeInHierarchy ? owner.head : owner.chest;
        if (anchor != null) transform.position = anchor.position + Vector3.up * .31f;
        bool visible = owner.HitsTaken > 0 && !owner.IsDead &&
            (player.position - transform.position).sqrMagnitude < 144f;
        foreach (Renderer item in barRenderers)
            if (item != null) item.enabled = visible;
        if (!visible || fill == null) return;
        Vector3 direction = transform.position - (player.position + Vector3.up * 1.65f);
        if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        float fraction = Mathf.Clamp01(1f - owner.HitsTaken / (float)Mathf.Max(1, owner.hitsToKill));
        Vector3 scale = fill.localScale;
        scale.x = fillWidth * fraction;
        fill.localScale = scale;
        Vector3 position = fill.localPosition;
        position.x = -fillWidth * (1f - fraction) * .5f;
        fill.localPosition = position;
    }
}
