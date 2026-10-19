using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit, editor-only sound authoring. The resulting WAVs are ordinary persistent project assets.</summary>
public static class FacilitySoundDesign
{
    private const string Folder = "Assets/Generated/Audio";
    private const int Rate = 44100;
    private const double Tau = Math.PI * 2.0;
    private const float SafePeak = .7943282f; // -2 dBFS; the mix still has its own source-level controls.

    public static AudioClip CreepyHum()
    {
        const string name = "Facility Creepy Hum V3";
        AudioClip cached = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + name + ".wav");
        if (cached != null) return cached;

        // Every carrier and modulation period fits exactly into twenty seconds. There is no
        // fade-to-silence at the loop boundary, and no random filter state to click or reset.
        const int duration = 20;
        var samples = new float[Rate * duration];
        var random = new System.Random(219735);
        const int textureCount = 64;
        var textureFrequency = new double[textureCount];
        var texturePhase = new double[textureCount];
        var textureWeight = new double[textureCount];
        for (int p = 0; p < textureCount; p++)
        {
            textureFrequency[p] = random.Next(640, 6800) / (double)duration;
            texturePhase[p] = random.NextDouble() * Tau;
            textureWeight[p] = .0060 / (1.0 + Math.Pow(textureFrequency[p] / 100.0, 1.65));
        }

        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)Rate;
            double slowMotion = .10 * Math.Sin(Tau * .10 * t) + .035 * Math.Sin(Tau * .20 * t);
            double breath = .88 + .065 * Math.Cos(Tau * .05 * t) + .035 * Math.Sin(Tau * .15 * t);
            double bass = .115 * Math.Sin(Tau * 26.4 * t + .32)
                        + .340 * Math.Sin(Tau * 52.8 * t + slowMotion)
                        + .158 * Math.Sin(Tau * 53.0 * t + 1.18 - slowMotion * .55);
            double resonance = .045 * Math.Sin(Tau * 79.2 * t + .73)
                             + .100 * Math.Sin(Tau * 105.6 * t + .92 + slowMotion * .4)
                             + .038 * Math.Sin(Tau * 106.05 * t + 2.15)
                             + .030 * Math.Sin(Tau * 158.4 * t + .24)
                             + .017 * Math.Sin(Tau * 211.2 * t + 1.57)
                             + .009 * Math.Sin(Tau * 264.0 * t + 2.0)
                             + .006 * Math.Sin(Tau * 316.8 * t + .45);
            double texture = 0.0;
            for (int p = 0; p < textureCount; p++)
                texture += textureWeight[p] * Math.Sin(Tau * textureFrequency[p] * t + texturePhase[p]);
            samples[i] = (float)((bass + resonance) * breath + texture * (1.0 + .13 * Math.Sin(Tau * .1 * t)));
        }
        Prepare(samples, true, .205f);
        return Save(name, samples, true);
    }

    public static AudioClip ShotgunReport()
    {
        const string name = "Shotgun Deep Concussion V4";
        AudioClip cached = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + name + ".wav");
        if (cached != null) return cached;

        int count = Mathf.RoundToInt(Rate * 1.85f);
        var dry = new float[count];
        var samples = new float[count];
        uint random = 0xE187C9A5;
        double lp25 = 0, lp160 = 0, lp700 = 0, lp1300 = 0, lp9000 = 0;
        double a25 = Coefficient(25), a160 = Coefficient(160), a700 = Coefficient(700);
        double a1300 = Coefficient(1300), a9000 = Coefficient(9000);
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)Rate;
            double noise = Noise(ref random);
            lp25 += (noise - lp25) * a25;
            lp160 += (noise - lp160) * a160;
            lp700 += (noise - lp700) * a700;
            lp1300 += (noise - lp1300) * a1300;
            lp9000 += (noise - lp9000) * a9000;

            // The fast crack remains brief. Weight and fear come from pressure beneath it,
            // rather than a long burst of bright white noise or a clipped, flat waveform.
            double crack = (lp9000 - lp1300) * (1.3 * Math.Exp(-t * 105) + .25 * Math.Exp(-t * 330));
            double pressure = (lp700 - lp25) * 2.3 * Math.Exp(-t * 14)
                            + (lp160 - lp25) * 3.0 * Math.Exp(-t * 7.5);
            // A rapidly settling resonance, not an audible sci-fi pitch sweep.
            double boomPhase = Tau * (68 * t + 28 * .055 * (1.0 - Math.Exp(-t / .055)));
            double boom = .90 * Math.Sin(boomPhase) * Math.Exp(-t * 12)
                        + .35 * Math.Sin(Tau * 65 * t + .3) * Math.Exp(-t * 9)
                        + .24 * Math.Sin(Tau * 109 * t + .1) * Math.Exp(-t * 20);
            double chamber = .105 * Math.Sin(Tau * 219 * t + .5) * Math.Exp(-t * 28)
                           + .070 * Math.Sin(Tau * 347 * t + .2) * Math.Exp(-t * 31);
            double metal = (.010 * Math.Sin(Tau * 790 * t)
                          + .006 * Math.Sin(Tau * 1310 * t + .4)) * Math.Exp(-t * 25);
            double tail = (lp700 - lp25) * .31 * Math.Exp(-t * 5.6) * (1.0 - Math.Exp(-t * 130));
            double attack = 1.0 - Math.Exp(-t * 10500);
            dry[i] = (float)((crack + pressure + boom + chamber + metal + tail) * attack);
            samples[i] = dry[i];
        }

        // Dispersed, dark early reflections suggest concrete without obscuring the next shell.
        double[] seconds = { .027, .041, .069, .083, .118, .157, .211, .293, .371 };
        double[] gain = { .16, -.12, .11, .085, -.067, .045, -.030, .018, -.009 };
        for (int tap = 0; tap < seconds.Length; tap++)
        {
            int delay = Mathf.RoundToInt((float)(seconds[tap] * Rate));
            double coefficient = Coefficient(1300.0 / (1.0 + tap * .30));
            double damped = 0;
            for (int i = delay; i < count; i++)
            {
                damped += (dry[i - delay] - damped) * coefficient;
                samples[i] += (float)(damped * gain[tap]);
            }
        }
        // A single transient must not dictate the entire report's listening level.
        // Smoothly round the highest pressure peaks, bringing the sustained body forward
        // without hard clipping, a bright noise bed, or an artificially long echo.
        float unshapedPeak = 0f;
        for (int i = 0; i < samples.Length; i++)
            unshapedPeak = Mathf.Max(unshapedPeak, Mathf.Abs(samples[i]));
        double drive = SafePeak * 3.0 / Math.Max(unshapedPeak, .000001f);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Tanh(samples[i] * drive);
        Prepare(samples, false, 0f);
        return Save(name, samples, false);
    }

    private static double Coefficient(double cutoff) => 1.0 - Math.Exp(-Tau * cutoff / Rate);

    private static float Noise(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / 8388607.5f - 1f;
    }

    private static void Prepare(float[] samples, bool loop, float targetRms)
    {
        double sum = 0;
        for (int i = 0; i < samples.Length; i++) sum += samples[i];
        double mean = sum / samples.Length;
        double windowSum = 0;
        double windowedSum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float window = loop ? 1f : ShotWindow(i, samples.Length);
            samples[i] = (float)((samples[i] - mean) * window);
            windowSum += window;
            windowedSum += samples[i];
        }
        // Weighted correction preserves exact silence at both one-shot endpoints.
        double residual = windowedSum / windowSum;
        double squares = 0;
        float peak = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] -= (float)(residual * (loop ? 1f : ShotWindow(i, samples.Length)));
            if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                throw new InvalidOperationException("Non-finite synthesized facility audio.");
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            squares += samples[i] * (double)samples[i];
        }
        float rms = (float)Math.Sqrt(squares / samples.Length);
        float scale = SafePeak / Mathf.Max(peak, .000001f);
        if (targetRms > 0) scale = Mathf.Min(scale, targetRms / Mathf.Max(rms, .000001f));
        for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
    }

    private static float ShotWindow(int i, int count)
    {
        float attack = Mathf.Clamp01(i / (Rate * .0003f));
        float tail = Mathf.Clamp01((count - 1 - i) / (Rate * .18f));
        return Mathf.SmoothStep(0f, 1f, attack) * Mathf.SmoothStep(0f, 1f, tail);
    }

    private static AudioClip Save(string name, float[] samples, bool loop)
    {
        EnsureFolder(Folder);
        string path = Folder + "/" + name + ".wav";
        using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
            writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            for (int i = 0; i < samples.Length; i++) writer.Write((short)Mathf.RoundToInt(samples[i] * 32767f));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = Rate;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.loadInBackground = false;
        // Unity exposes the mono-normalization toggle through its serialized importer,
        // not the public AudioImporter API. Preserve the authored headroom.
        var serialized = new SerializedObject(importer);
        SerializedProperty normalize = serialized.FindProperty("m_Normalize");
        if (normalize != null)
        {
            normalize.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        importer.SaveAndReimport();
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException("Failed to import " + path);
        Debug.Log("[Facility audio] " + name + ": " + Describe(samples, loop));
        return clip;
    }

    private static string Describe(float[] samples, bool loop)
    {
        double peak = 0, sum = 0, squares = 0, openingSquares = 0;
        int openingCount = Mathf.Min(samples.Length, Rate / 5);
        for (int i = 0; i < samples.Length; i++)
        {
            peak = Math.Max(peak, Math.Abs(samples[i]));
            sum += samples[i];
            squares += samples[i] * (double)samples[i];
            if (i < openingCount) openingSquares += samples[i] * (double)samples[i];
        }
        double rms = Math.Sqrt(squares / samples.Length);
        double openingRms = Math.Sqrt(openingSquares / openingCount);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F2} s, mono {1} Hz PCM16, peak {2:F2} dBFS, RMS {3:F2} dBFS, first 200 ms RMS {4:F2} dBFS, DC {5:E2}, {6} step {7:E2}",
            samples.Length / (double)Rate, Rate, 20 * Math.Log10(Math.Max(peak, 1e-10)),
            20 * Math.Log10(Math.Max(rms, 1e-10)), 20 * Math.Log10(Math.Max(openingRms, 1e-10)),
            sum / samples.Length, loop ? "loop seam" : "end", Math.Abs(samples[samples.Length - 1] - samples[0]));
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
