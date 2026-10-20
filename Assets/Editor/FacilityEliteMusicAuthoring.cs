using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Authors an original understated final-response cue, stored as an ordinary stereo WAV.</summary>
public static class FacilityEliteMusicAuthoring
{
    public const string ClipPath = "Assets/Generated/Audio/Final Response - Eerie Pressure V1.wav";
    const int Rate = 44100, Seconds = 32;
    const double Tau = Math.PI * 2.0;
    static readonly double[] MetalEntrances = {1.8,9.3,16.7,25.2};

    public static void Configure(FacilityEliteSoundtrack soundtrack)
    {
        if(soundtrack == null) throw new ArgumentNullException(nameof(soundtrack));
        AudioSource source = soundtrack.escalationSource;
        if(source == null)
        {
            var node = new GameObject("Final response - eerie pressure soundtrack");
            node.transform.SetParent(soundtrack.transform,false); source = node.AddComponent<AudioSource>();
        }
        source.clip = Cue(); source.volume = 0f; source.loop = true; source.playOnAwake = false;
        source.spatialBlend = 0f; source.dopplerLevel = 0f; source.reverbZoneMix = 0f;
        source.priority = 75; source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
        soundtrack.escalationSource = source; soundtrack.crossfadeSeconds = 3.6f;
        soundtrack.escalationVolume = .34f; soundtrack.ambientDuck = .18f;
        var sources = new List<AudioSource>(); var volumes = new List<float>();
        foreach(var root in soundtrack.gameObject.scene.GetRootGameObjects())
            foreach(var ambient in root.GetComponentsInChildren<FacilityAmbientEffects>(true))
                if(ambient.loopingSources != null)
                    foreach(var bed in ambient.loopingSources)
                        if(bed != null && bed != source && bed.spatialBlend < .5f && !sources.Contains(bed))
                        {sources.Add(bed);volumes.Add(bed.volume);}
        soundtrack.ambientSources = sources.ToArray(); soundtrack.ambientBaseVolumes = volumes.ToArray();
        EditorUtility.SetDirty(source); EditorUtility.SetDirty(soundtrack);
    }

    public static AudioClip Cue()
    {
        var saved = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath); if(saved != null)return saved;
        Directory.CreateDirectory(Path.GetDirectoryName(ClipPath));
        int frames = Rate * Seconds; var samples = new float[frames * 2]; double peak = 0;
        for(int i=0;i<frames;i++)
        {
            double t=i/(double)Rate;
            double breath=.82+.12*Math.Sin(Tau*t/16)+.06*Math.Cos(Tau*t/8);
            double sway=.12*Math.Sin(Tau*t/8)+.035*Math.Sin(Tau*t/4);
            double drone=(.22*Math.Sin(Tau*55*t+sway)+.12*Math.Sin(Tau*55.125*t+.7-sway)
                +.075*Math.Sin(Tau*65.40625*t+.4)+.04*Math.Sin(Tau*82.40625*t+1.1))*breath;
            // Rounded, low heartbeat-shaped energy: no alarm bleeps or hard attack transients.
            double pulse=Math.Pow(.5+.5*Math.Cos(Tau*1.125*t),9);
            double throb=pulse*(.16*Math.Sin(Tau*55*t)+.055*Math.Sin(Tau*110*t+.18));
            double left=drone+throb,right=drone+throb;
            for(int note=0;note<4;note++)
            {
                double start=MetalEntrances[note];
                double age=Wrap(t-start,Seconds);
                double ring=Metal(age,note);
                double echo=Metal(Wrap(t-start-.39,Seconds),note)*.24+Metal(Wrap(t-start-.83,Seconds),note)*.13;
                left+=(note%2==0?.075:.035)*ring+.04*echo;
                right+=(note%2==0?.035:.075)*ring+.04*echo;
            }
            // Slowly moving upper air is a quiet pitched texture, not broadband hiss.
            double air=(.012*Math.Sin(Tau*174.625*t+.5*Math.Sin(Tau*t/16))
                +.009*Math.Sin(Tau*233.09375*t+1.7))*(.6+.4*Math.Sin(Tau*t/32));
            left+=air;right+=air*.73;
            samples[i*2]=(float)left; samples[i*2+1]=(float)right;
            peak=Math.Max(peak,Math.Max(Math.Abs(left),Math.Abs(right)));
        }
        double gain=.68/Math.Max(.001,peak);
        using(var writer=new BinaryWriter(File.Create(ClipPath)))
        {
            int bytes=samples.Length*2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
            writer.Write((short)1);writer.Write((short)2);writer.Write(Rate);writer.Write(Rate*4);
            writer.Write((short)4);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
            foreach(float value in samples)writer.Write((short)(Math.Max(-.99,Math.Min(.99,value*gain))*short.MaxValue));
        }
        AssetDatabase.ImportAsset(ClipPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(AudioImporter)AssetImporter.GetAtPath(ClipPath);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;
        settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=settings;
        importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
    }

    static double Wrap(double value,double length) => value-Math.Floor(value/length)*length;
    static double Metal(double age,int note)
    {
        double rise=1-Math.Exp(-age*15),decay=Math.Exp(-age*1.2);
        double shift=note%2==0?1:1.1224489795918366;
        return rise*decay*(.6*Math.Sin(Tau*196*shift*age)+.25*Math.Sin(Tau*311.125*shift*age+.6)
            +.13*Math.Sin(Tau*479*shift*age+1.2)+.06*Math.Sin(Tau*717*shift*age));
    }
}
