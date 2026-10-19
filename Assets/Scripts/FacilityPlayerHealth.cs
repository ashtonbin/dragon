using System;
using UnityEngine;

/// <summary>The player survives two attacks; the third is fatal. Invulnerability is shared by all attackers.</summary>
[DisallowMultipleComponent]
public sealed class FacilityPlayerHealth : MonoBehaviour
{
    public const int HitLimit = 3;
    [SerializeField, Min(.1f)] private float invulnerabilitySeconds = .9f;
    public int MaxHits => HitLimit;
    public int HitsTaken { get; private set; }
    public int CurrentHits => HitLimit - HitsTaken;
    public int CurrentHealth => CurrentHits;
    public int MaxHealth => HitLimit;
    public bool IsDead => HitsTaken >= HitLimit;
    public float RedFlash { get; private set; }
    public Transform LastAttacker { get; private set; }
    public event Action<Transform> Damaged;
    public event Action Died;

    private float nextDamageAt = float.NegativeInfinity;

    private void Update()
    {
        RedFlash = Mathf.MoveTowards(RedFlash, 0f, Time.unscaledDeltaTime * 1.8f);
    }

    public bool TryTakeHit(Transform source)
    {
        if (!isActiveAndEnabled || IsDead || Time.timeScale <= 0f || Time.time < nextDamageAt) return false;
        nextDamageAt = Time.time + invulnerabilitySeconds;
        LastAttacker = source;
        HitsTaken++;
        RedFlash = 1f;
        Damaged?.Invoke(source);
        if (IsDead) Died?.Invoke();
        return true;
    }

    public void ResetHealth()
    {
        HitsTaken = 0;
        RedFlash = 0f;
        LastAttacker = null;
        nextDamageAt = float.NegativeInfinity;
    }
}
