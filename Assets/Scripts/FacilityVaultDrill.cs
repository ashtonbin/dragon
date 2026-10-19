using UnityEngine;
using UnityEngine.UI;

/// <summary>The vault has a real forty-five-second drilling wait, frozen with the game's simulation.</summary>
[DisallowMultipleComponent]
public sealed class FacilityVaultDrill : MonoBehaviour
{
    public const float RequiredDrillingSeconds = 45f;
    public FacilityDoor vaultDoor;
    public GameObject deployedDrill;
    public AudioSource drillLoop;
    public Text progressText;
    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float SecondsRemaining => Mathf.Max(0f, RequiredDrillingSeconds - ElapsedSeconds);
    public float Progress => Mathf.Clamp01(ElapsedSeconds / RequiredDrillingSeconds);
    public int StartCount { get; private set; }
    private int shownSecond = -1;

    private void Awake()
    {
        if (vaultDoor != null) vaultDoor.locked = true;
        if (deployedDrill != null) deployedDrill.SetActive(false);
        if (drillLoop != null) drillLoop.Stop();
        UpdateText();
    }

    public bool BeginDrilling()
    {
        if (IsRunning || IsComplete || vaultDoor == null) return false;
        IsRunning = true;
        StartCount++;
        if (deployedDrill != null) deployedDrill.SetActive(true);
        if (drillLoop != null) { drillLoop.loop = true; drillLoop.Play(); }
        UpdateText();
        return true;
    }

    private void Update()
    {
        if (!IsRunning || Time.deltaTime <= 0f) return;
        if (FacilityGameSession.Instance != null && FacilityGameSession.Instance.CurrentState != FacilityGameState.Playing) return;
        ElapsedSeconds = Mathf.Min(RequiredDrillingSeconds, ElapsedSeconds + Time.deltaTime);
        if (ElapsedSeconds >= RequiredDrillingSeconds)
        {
            IsRunning = false;
            IsComplete = true;
            if (drillLoop != null) drillLoop.Stop();
            vaultDoor.ForceOpen();
        }
        UpdateText();
    }

    private void UpdateText()
    {
        if (progressText == null) return;
        int second = Mathf.CeilToInt(SecondsRemaining);
        if (shownSecond == second && !IsComplete) return;
        shownSecond = second;
        progressText.text = IsComplete ? "VAULT UNLOCKED" : IsRunning ? "DRILLING  //  " + second.ToString("00") + "s" : "DRILL REQUIRED  //  45s";
    }

    private void OnDisable() { if (drillLoop != null) drillLoop.Stop(); }
}
