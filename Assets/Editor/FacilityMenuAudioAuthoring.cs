using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicitly saves the original mechanical UI click and wires every authored canvas button.</summary>
public static class FacilityMenuAudioAuthoring
{
    public const string ClickPath = "Assets/Generated/Interface/Audio/Desktop Button Clack.wav";

    public static void Apply(FacilityGameSession session)
    {
        if (session == null || session.canvas == null) throw new ArgumentException("An authored session canvas is required.");
        FacilityMenuAudio audio = session.GetComponent<FacilityMenuAudio>();
        if (audio == null) audio = session.gameObject.AddComponent<FacilityMenuAudio>();
        // Use a dedicated source; other sources on the systems object may belong to gameplay.
        AudioSource source = audio.menuSource;
        if (source == null) source = session.gameObject.AddComponent<AudioSource>();
        source.clip = Click();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.reverbZoneMix = 0f;
        source.volume = .66f;
        source.pitch = 1f;
        source.priority = 30;
        source.ignoreListenerPause = true;
        source.ignoreListenerVolume = false;
        audio.menuSource = source;
        audio.activationClip = source.clip;
        foreach (Button button in session.canvas.GetComponentsInChildren<Button>(true))
        {
            FacilityDesktopButton feedback = button.GetComponent<FacilityDesktopButton>();
            if (feedback == null) feedback = button.gameObject.AddComponent<FacilityDesktopButton>();
            feedback.MenuAudio = audio;
            Text label = button.GetComponentInChildren<Text>(true);
            feedback.content = label != null ? label.rectTransform : null;
            bool wired = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == feedback && button.onClick.GetPersistentMethodName(i) == nameof(FacilityDesktopButton.Activate))
                    wired = true;
            if (!wired) UnityEventTools.AddPersistentListener(button.onClick, feedback.Activate);
            EditorUtility.SetDirty(feedback);
            EditorUtility.SetDirty(button);
        }
        EditorUtility.SetDirty(audio);
        EditorUtility.SetDirty(source);
    }

    public static AudioClip Click()
    {
        AudioClip existing = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickPath);
        if (existing != null) return existing;
        Directory.CreateDirectory(Path.GetDirectoryName(ClickPath));
        const int rate = 44100, count = 4410;
        var random = new System.Random(47031);
        using (var writer = new BinaryWriter(File.Create(ClickPath)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            double lowNoise = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)rate;
                lowNoise = lowNoise * .87 + (random.NextDouble() * 2 - 1) * .13;
                double attack = Math.Min(1, t / .00065);
                double body = Math.Sin(2 * Math.PI * (205 * t - 420 * t * t)) * Math.Exp(-t * 58) * .68;
                body += Math.Sin(2 * Math.PI * 346 * t) * Math.Exp(-t * 84) * .23;
                double contact = (Math.Sin(2 * Math.PI * 1120 * t) * .15 + lowNoise * .8) * Math.Exp(-t * 220);
                // A second hard contact gives a substantial switch closure without a hiss or drum tail.
                double second = Math.Max(0, t - .0105);
                double latch = t >= .0105 ? (Math.Sin(2 * Math.PI * 740 * second) * .27 + Math.Sin(2 * Math.PI * 1510 * second) * .09) * Math.Exp(-second * 195) * Math.Min(1, second / .0003) : 0;
                double fade = Math.Min(1, (count - 1 - i) / 160.0);
                double sample = Math.Tanh((body * attack + contact * attack + latch) * 1.3) * .75 * fade;
                writer.Write((short)(sample * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(ClickPath, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(ClickPath);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.preloadAudioData = true;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ClickPath);
    }
}
