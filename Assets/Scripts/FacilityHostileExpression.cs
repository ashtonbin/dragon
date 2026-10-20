using UnityEngine;

/// <summary>Saved head-mounted brows follow hostility and stay attached to the anatomical head.</summary>
[DisallowMultipleComponent]
public sealed class FacilityHostileExpression : MonoBehaviour
{
    public FacilityHumanoid actor;
    public Transform leftBrow, rightBrow;
    [Range(0f, 24f)] public float restingTilt;
    public bool Angry { get; private set; }
    float expression;
    void LateUpdate()
    {
        if (actor == null) return;
        Angry = !actor.IsDead && (actor.IsHostile || actor.HitsTaken > 0);
        expression = Mathf.MoveTowards(expression, Angry ? 1 : 0, Time.deltaTime * 7f);
        float tilt = Mathf.Lerp(restingTilt, 24f, expression);
        if (leftBrow != null) leftBrow.localRotation = Quaternion.Euler(0, 0, -tilt);
        if (rightBrow != null) rightBrow.localRotation = Quaternion.Euler(0, 0, tilt);
        bool visible = !actor.HeadExploded;
        if (leftBrow != null && leftBrow.gameObject.activeSelf != visible) leftBrow.gameObject.SetActive(visible);
        if (rightBrow != null && rightBrow.gameObject.activeSelf != visible) rightBrow.gameObject.SetActive(visible);
    }
}
