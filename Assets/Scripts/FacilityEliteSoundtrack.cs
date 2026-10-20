using System;
using UnityEngine;

/// <summary>A saved music voice that crossfades the existing ambience during the final response.</summary>
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class FacilityEliteSoundtrack : MonoBehaviour
{
    public AudioSource escalationSource;
    public AudioSource[] ambientSources = Array.Empty<AudioSource>();
    public float[] ambientBaseVolumes = Array.Empty<float>();
    [Min(.1f)] public float crossfadeSeconds = 3.6f;
    [Range(0f, 1f)] public float escalationVolume = .34f;
    [Range(0f, 1f)] public float ambientDuck = .18f;

    public bool IsEscalated { get; private set; }
    public AudioSource Source => escalationSource;
    public float TransitionProgress { get; private set; }
    bool locallyPaused, ownsAmbientMix;

    void Awake()
    {
        if (escalationSource == null) return;
        escalationSource.playOnAwake = false;
        escalationSource.volume = 0f;
        escalationSource.Stop();
    }

    public void BeginEscalation()
    {
        if (escalationSource == null || escalationSource.clip == null || IsEscalated) return;
        var session = FacilityGameSession.Instance;
        if (session != null && (session.CurrentMode != FacilityGameMode.Story ||
            session.CurrentState != FacilityGameState.Playing || (session.health != null && session.health.IsDead))) return;
        IsEscalated = true;
        ownsAmbientMix = true;
        if (!escalationSource.isPlaying && !locallyPaused)
        {
            escalationSource.volume = TransitionProgress * escalationVolume;
            escalationSource.Play();
        }
    }

    public void StopEscalation()
    {
        IsEscalated = false;
        // Session completion/death also pauses the listener, so dispose the voice immediately.
        var session = FacilityGameSession.Instance;
        if (session != null && session.CurrentState != FacilityGameState.Playing) Silence();
    }

    void LateUpdate()
    {
        var session = FacilityGameSession.Instance;
        bool story = session != null && session.CurrentMode == FacilityGameMode.Story;
        if (!story || (session.CurrentState != FacilityGameState.Playing && session.CurrentState != FacilityGameState.Paused) ||
            (session.health != null && session.health.IsDead))
        { Silence(); return; }
        bool temporarilyPaused = story && (session.CurrentState == FacilityGameState.Paused ||
            (session.shop != null && session.shop.IsOpen));
        if (temporarilyPaused || (story && Time.timeScale <= 0f))
        {
            if (escalationSource != null && escalationSource.isPlaying && !locallyPaused)
            { escalationSource.Pause(); locallyPaused = true; }
            return;
        }
        if (locallyPaused)
        {
            if (escalationSource != null && (IsEscalated || TransitionProgress > 0f)) escalationSource.UnPause();
            locallyPaused = false;
        }
        if (!ownsAmbientMix) return;
        TransitionProgress = Mathf.MoveTowards(TransitionProgress, IsEscalated ? 1f : 0f,
            Time.deltaTime / Mathf.Max(.1f, crossfadeSeconds));
        float smooth = Mathf.SmoothStep(0f, 1f, TransitionProgress);
        if (escalationSource != null) escalationSource.volume = escalationVolume * Mathf.Sin(smooth * Mathf.PI * .5f);
        SetAmbient(Mathf.Lerp(1f, ambientDuck, smooth));
        if (!IsEscalated && TransitionProgress <= 0f) Silence();
    }

    void SetAmbient(float multiplier)
    {
        int count = Mathf.Min(ambientSources == null ? 0 : ambientSources.Length,
            ambientBaseVolumes == null ? 0 : ambientBaseVolumes.Length);
        for (int i = 0; i < count; i++)
            if (ambientSources[i] != null) ambientSources[i].volume = ambientBaseVolumes[i] * multiplier;
    }

    void Silence()
    {
        IsEscalated = false;
        TransitionProgress = 0f;
        locallyPaused = false;
        if (escalationSource != null) { escalationSource.Stop(); escalationSource.volume = 0f; }
        if (ownsAmbientMix) SetAmbient(1f);
        ownsAmbientMix = false;
    }

    void OnDisable() => Silence();
}
