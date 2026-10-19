using UnityEngine;
using UnityEngine.UI;

/// <summary>One authored menu sound, with an audio-only tail that can finish across scene loads.</summary>
[DefaultExecutionOrder(-460), DisallowMultipleComponent]
public sealed class FacilityMenuAudio : MonoBehaviour
{
    public static FacilityMenuAudio Instance { get; private set; }
    public AudioSource menuSource;
    public AudioClip activationClip;

    public AudioSource Source => menuSource;
    public AudioClip Click => activationClip;
    public AudioSource PlaybackSource => voice != null ? voice : menuSource;
    public int ActivationCount { get; private set; }
    public int PlayedClickCount => ActivationCount;
    public float LastActivationUnscaledTime { get; private set; } = -1f;
    public Button LastActivatedButton { get; private set; }

    static AudioSource voice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        voice = null;
    }

    void Awake()
    {
        Instance = this;
        if (menuSource == null) menuSource = GetComponent<AudioSource>();
        if (menuSource != null)
        {
            menuSource.ignoreListenerPause = true;
            menuSource.spatialBlend = 0f;
            menuSource.playOnAwake = false;
        }
    }

    public void Activate(Button button)
    {
        if (!Application.isPlaying || activationClip == null || menuSource == null) return;
        // Only this tiny voice survives. The session, buttons and gameplay state stay scene-owned.
        if (voice == null)
        {
            var playback = new GameObject("Desktop Menu Click Tail", typeof(AudioSource));
            DontDestroyOnLoad(playback);
            voice = playback.GetComponent<AudioSource>();
        }
        voice.clip = activationClip;
        voice.outputAudioMixerGroup = menuSource.outputAudioMixerGroup;
        voice.playOnAwake = false;
        voice.loop = false;
        voice.spatialBlend = 0f;
        voice.dopplerLevel = 0f;
        voice.reverbZoneMix = 0f;
        voice.ignoreListenerPause = true;
        voice.ignoreListenerVolume = false;
        voice.volume = menuSource.volume;
        voice.pitch = menuSource.pitch;
        voice.priority = menuSource.priority;
        voice.PlayOneShot(activationClip);
        ActivationCount++;
        LastActivationUnscaledTime = Time.unscaledTime;
        LastActivatedButton = button;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
