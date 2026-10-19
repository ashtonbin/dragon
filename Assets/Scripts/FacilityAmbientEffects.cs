using System;
using UnityEngine;

/// <summary>Animates authored lights and plays authored audio; it never builds scene content.</summary>
[DisallowMultipleComponent]
public sealed class FacilityAmbientEffects : MonoBehaviour
{
    public Light[] emergencyLights = Array.Empty<Light>();
    public Renderer[] emergencyPanels = Array.Empty<Renderer>();
    public AudioSource[] loopingSources = Array.Empty<AudioSource>();
    public AudioSource intermittentSource;

    [SerializeField] private int variationSeed = 4127;
    [SerializeField, Range(0f, 1f)] private float intermittentVolumeScale = 0.35f;

    private float[] originalIntensities;
    private uint randomState;
    private float nextSoundTime;
    private bool running;
    private float[] loopVolumes;
    private float loopFadeStarted;
    private bool fadingLoops;
    private MaterialPropertyBlock panelProperties;
    private Color[] panelColors;
    private static readonly int PanelColor = Shader.PropertyToID("_BaseColor");

    private void Start()
    {
        // Child AudioSources can finish enabling after this parent's OnEnable during a cold scene load.
        // Retry once after the scene is enabled; already-playing loops keep their current playback.
        if (!running) OnEnable();
        if (loopingSources == null) return;
        foreach (AudioSource source in loopingSources)
            if (source != null && source != intermittentSource && source.clip != null && !source.isPlaying) source.Play();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        int count = emergencyLights == null ? 0 : emergencyLights.Length;
        if (originalIntensities == null || originalIntensities.Length != count)
            originalIntensities = new float[count];

        for (int i = 0; i < count; i++)
            originalIntensities[i] = emergencyLights[i] != null ? emergencyLights[i].intensity : 0f;

        int panelCount = emergencyPanels == null ? 0 : emergencyPanels.Length;
        panelColors = new Color[panelCount];
        panelProperties = new MaterialPropertyBlock();
        for (int i = 0; i < panelCount; i++)
            if (emergencyPanels[i] != null && emergencyPanels[i].sharedMaterial != null)
                panelColors[i] = emergencyPanels[i].sharedMaterial.GetColor(PanelColor);

        randomState = unchecked((uint)variationSeed);
        if (randomState == 0)
            randomState = 1;

        if (loopingSources != null)
        {
            loopVolumes = new float[loopingSources.Length];
            for (int i = 0; i < loopingSources.Length; i++)
            {
                AudioSource source = loopingSources[i];
                if (source != null && source != intermittentSource && source.clip != null)
                {
                    loopVolumes[i] = source.volume;
                    source.volume = 0f;
                    source.Play();
                }
            }
        }

        loopFadeStarted = Time.unscaledTime;
        fadingLoops = true;

        nextSoundTime = Time.time + NextSoundDelay();
        running = true;
    }

    private void Update()
    {
        if (!running)
            return;

        if (fadingLoops && loopingSources != null && loopVolumes != null)
        {
            // The periodic hum has no seam dip. Fade only its first onset so it never starts with a click.
            float fade = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - loopFadeStarted) / .8f);
            for (int i = 0; i < Mathf.Min(loopingSources.Length, loopVolumes.Length); i++)
                if (loopingSources[i] != null) loopingSources[i].volume = loopVolumes[i] * fade;
            if (fade >= 1f) fadingLoops = false;
        }

        float time = Time.time;
        for (int i = 0; i < panelColors.Length; i++)
        {
            Renderer panel = emergencyPanels[i];
            if (panel == null) continue;
            // Only the lens breathes; the baked room illumination stays stable at corners.
            float phase = variationSeed * .0137f + i * 17.31f;
            float variation = Mathf.PerlinNoise(time * .08f + phase, phase + 4.7f);
            panel.GetPropertyBlock(panelProperties);
            Color color = panelColors[i];
            float brightness = Mathf.Lerp(.85f, 1f, variation);
            panelProperties.SetColor(PanelColor, new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a));
            panel.SetPropertyBlock(panelProperties);
        }
        int count = emergencyLights == null ? 0 : Mathf.Min(emergencyLights.Length, originalIntensities.Length);
        for (int i = 0; i < count; i++)
        {
            Light light = emergencyLights[i];
            if (light == null)
                continue;

            float phase = variationSeed * 0.0137f + i * 17.31f;
            float variation = Mathf.PerlinNoise(time * 0.18f + phase, phase + 4.7f);
            light.intensity = originalIntensities[i] * Mathf.Lerp(0.82f, 1f, variation);
        }

        if (time < nextSoundTime)
            return;

        if (intermittentSource != null && intermittentSource.clip != null && !intermittentSource.isPlaying)
            intermittentSource.PlayOneShot(intermittentSource.clip, intermittentVolumeScale);

        nextSoundTime = time + NextSoundDelay();
    }

    private float NextSoundDelay()
    {
        // A private generator keeps the sound timing independent of gameplay's random sequence.
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        return 12f + (randomState & 0x00ffffffu) / 16777215f * 8f;
    }

    private void OnDisable()
    {
        if (!running)
            return;

        int count = emergencyLights == null ? 0 : Mathf.Min(emergencyLights.Length, originalIntensities.Length);
        for (int i = 0; i < count; i++)
        {
            if (emergencyLights[i] != null)
                emergencyLights[i].intensity = originalIntensities[i];
        }

        if (loopingSources != null)
        {
            for (int i = 0; i < loopingSources.Length; i++)
            {
                AudioSource source = loopingSources[i];
                if (source != null)
                {
                    source.Stop();
                    if (loopVolumes != null && i < loopVolumes.Length) source.volume = loopVolumes[i];
                }
            }
        }

        if (intermittentSource != null)
            intermittentSource.Stop();

        if (emergencyPanels != null)
            foreach (Renderer panel in emergencyPanels)
                if (panel != null) panel.SetPropertyBlock(null);

        running = false;
    }
}
