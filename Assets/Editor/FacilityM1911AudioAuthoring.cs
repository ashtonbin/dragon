using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Original .45 report and close mechanical Foley, saved as ordinary PCM assets.</summary>
public static class FacilityM1911AudioAuthoring
{
    public const string AudioRoot = "Assets/Generated/M1911/Audio";
    public const string ReportPath = AudioRoot + "/M1911 .45 ACP - Weighted Report V1.wav";
    public const string ReloadPath = AudioRoot + "/M1911 - Magazine Exchange 1_8s V1.wav";
    public const string EmptyPath = AudioRoot + "/M1911 - Empty Trigger V1.wav";
    public const string SlidePath = AudioRoot + "/M1911 - Steel Slide Release V1.wav";
    const int Rate = 48000;
    const double Tau = Math.PI * 2.0;

    public static void Configure(FacilityPistol pistol)
    {
        if (pistol == null) throw new ArgumentNullException(nameof(pistol));
        if (Application.isPlaying) throw new InvalidOperationException("Save M1911 audio outside Play mode.");
        EnsureFolder(AudioRoot);
        Transform parent = pistol.aimCamera != null ? pistol.aimCamera.transform : pistol.transform;
        pistol.reportClip = Report(); pistol.reloadClip = Reload(); pistol.emptyClip = Empty(); pistol.slideClip = Slide();
        pistol.reportSource = Source(parent,pistol.reportSource,"M1911 .45 report",.84f,25,pistol.reportClip);
        pistol.actionSource = Source(parent,pistol.actionSource,"M1911 magazine and steel mechanism",.52f,38,pistol.reloadClip);
        EditorUtility.SetDirty(pistol);
    }

    static AudioSource Source(Transform parent,AudioSource existing,string name,float volume,int priority,AudioClip clip)
    {
        AudioSource source = existing;
        // Keep one saved camera child for each voice, independently of the removable weapon model.
        if (source == null || source.transform == parent)
        {
            Transform node = parent.Find(name);
            if (node == null) { node = new GameObject(name).transform; node.SetParent(parent,false); }
            source = node.GetComponent<AudioSource>();
            if (source == null) source = node.gameObject.AddComponent<AudioSource>();
        }
        source.name = name;
        if (source.transform.parent != parent) source.transform.SetParent(parent,false);
        source.transform.localPosition = Vector3.zero;
        source.clip = clip; source.volume = volume; source.pitch = 1f; source.priority = priority;
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
        source.dopplerLevel = 0f; source.reverbZoneMix = 0f;
        source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
        source.bypassEffects = false; source.bypassListenerEffects = false;
        EditorUtility.SetDirty(source); return source;
    }

    public static AudioClip Report()
    {
        var saved = AssetDatabase.LoadAssetAtPath<AudioClip>(ReportPath); if (saved != null) return saved;
        const double duration = 1.15;
        int frames = (int)Math.Round(Rate * duration);
        var dry = new float[frames]; uint random = 0x45AC1911;
        double lp70=0,lp250=0,lp800=0,lp1800=0,lp4500=0,lp11000=0;
        double a70=Coefficient(70),a250=Coefficient(250),a800=Coefficient(800),a1800=Coefficient(1800),a4500=Coefficient(4500),a11000=Coefficient(11000);
        for (int i=0;i<frames;i++)
        {
            double t=i/(double)Rate,noise=Noise(ref random);
            lp70+=(noise-lp70)*a70;lp250+=(noise-lp250)*a250;lp800+=(noise-lp800)*a800;
            lp1800+=(noise-lp1800)*a1800;lp4500+=(noise-lp4500)*a4500;lp11000+=(noise-lp11000)*a11000;
            double attack=1-Math.Exp(-t*9500);
            // A short pressure crack sits above the body; it does not become a long hiss.
            double crack=(lp11000-lp1800)*(1.15*Math.Exp(-t*145)+.24*Math.Exp(-t*72))
                +(lp4500-lp800)*.38*Math.Exp(-t*90);
            double pressure=(lp800-lp70)*2.4*Math.Exp(-t*25)+(lp250-lp70)*1.8*Math.Exp(-t*19);
            double phase=Tau*(66*t+22*.018*(1-Math.Exp(-t/.018)));
            double weight=.68*Math.Sin(phase)*Math.Exp(-t*23)+.17*Math.Sin(Tau*137*t+.2)*Math.Exp(-t*35);
            double chamber=.09*Math.Sin(Tau*238*t)*Math.Exp(-t*45)+.05*Math.Sin(Tau*409*t+.8)*Math.Exp(-t*55);
            double tail=(lp1800-lp250)*.18*Math.Exp(-t*8)*(1-Math.Exp(-t*130));
            // A compact action slap belongs to the gun report, after the muzzle pressure.
            double action=Contact(t,.047,.17,1470,lp4500-lp800)+Contact(t,.072,.065,2180,lp4500-lp800);
            dry[i]=(float)Math.Tanh(((crack+pressure+weight+chamber+tail)*attack+action)*1.23);
        }
        var left=new float[frames];var right=new float[frames];
        for(int i=0;i<frames;i++)left[i]=right[i]=dry[i]*.88f;
        // Quiet, dark, asymmetric reflections give width while the decisive transient stays centered.
        double[] delays={.016,.027,.044,.069,.107,.163,.241,.347};
        double[] leftGain={.10,-.035,.065,-.05,.030,-.020,.011,-.005};
        double[] rightGain={.035,.10,-.04,.055,.025,.017,-.010,.005};
        for(int tap=0;tap<delays.Length;tap++)
        {
            int offset=(int)Math.Round(delays[tap]*Rate);double damped=0,filter=Coefficient(2100.0/(1+tap*.32));
            for(int i=offset;i<frames;i++)
            {damped+=(dry[i-offset]-damped)*filter;left[i]+=(float)(damped*leftGain[tap]);right[i]+=(float)(damped*rightGain[tap]);}
        }
        var interleaved=new float[frames*2];
        for(int i=0;i<frames;i++){interleaved[i*2]=left[i];interleaved[i*2+1]=right[i];}
        return Save(ReportPath,interleaved,2,.76f,18);
    }

    public static AudioClip Reload()
    {
        var saved=AssetDatabase.LoadAssetAtPath<AudioClip>(ReloadPath);if(saved!=null)return saved;
        var samples=new float[(int)(Rate*1.8)];uint random=0x19110AD;
        double lp220=0,lp1200=0,lp5200=0,a220=Coefficient(220),a1200=Coefficient(1200),a5200=Coefficient(5200);
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)Rate,noise=Noise(ref random);
            lp220+=(noise-lp220)*a220;lp1200+=(noise-lp1200)*a1200;lp5200+=(noise-lp5200)*a5200;
            double rail=lp1200-lp220,contact=lp5200-lp1200;
            double value=Contact(t,.100,.19,1680,contact)+Contact(t,.146,.15,760,contact);
            value+=Drag(t,.165,.285,rail,contact)*.15;
            value+=Contact(t,.196,.050,1320,contact)+Contact(t,.262,.035,1130,contact)+Contact(t,.337,.025,890,contact);
            value+=Drag(t,.950,.205,rail,contact)*.19;
            value+=Contact(t,.976,.12,910,contact)+Contact(t,1.083,.14,720,contact);
            value+=Contact(t,1.160,.53,480,contact)+Contact(t,1.184,.18,1730,contact)+Contact(t,1.222,.045,2210,contact);
            double seated=t-1.160;
            if(seated>=0)value+=(.13*Math.Sin(Tau*156*seated)+.05*Math.Sin(Tau*313*seated))*Math.Exp(-seated*55)*(1-Math.Exp(-seated*3000));
            // No slide motion here: a tactical reload keeps its chambered round. Empty reloads
            // play the separate slide-release clip at 1.57 seconds on the same action voice.
            samples[i]=(float)value;
        }
        return Save(ReloadPath,samples,1,.66f,55);
    }

    public static AudioClip Empty()
    {
        var saved=AssetDatabase.LoadAssetAtPath<AudioClip>(EmptyPath);if(saved!=null)return saved;
        var samples=new float[(int)(Rate*.12)];uint random=0xD1911E;
        double low=0,high=0,aLow=Coefficient(850),aHigh=Coefficient(7500);
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)Rate,noise=Noise(ref random);low+=(noise-low)*aLow;high+=(noise-high)*aHigh;
            samples[i]=(float)(Contact(t,.002,.7,2170,high-low)+Contact(t,.016,.16,1320,high-low));
        }
        return Save(EmptyPath,samples,1,.48f,100);
    }

    public static AudioClip Slide()
    {
        var saved=AssetDatabase.LoadAssetAtPath<AudioClip>(SlidePath);if(saved!=null)return saved;
        var samples=new float[(int)(Rate*.23)];uint random=0x51DE1911;
        double lp280=0,lp1900=0,lp6500=0,a280=Coefficient(280),a1900=Coefficient(1900),a6500=Coefficient(6500);
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)Rate,noise=Noise(ref random);lp280+=(noise-lp280)*a280;
            lp1900+=(noise-lp1900)*a1900;lp6500+=(noise-lp6500)*a6500;
            double contact=lp6500-lp1900;
            double value=Contact(t,.003,.14,2310,contact)+Drag(t,.005,.055,lp1900-lp280,contact)*.12;
            value+=Contact(t,.052,.65,980,contact)+Contact(t,.067,.25,1830,contact)+Contact(t,.098,.04,2410,contact);
            double age=t-.052;
            if(age>=0)value+=.16*Math.Sin(Tau*192*age)*Math.Exp(-age*58)*(1-Math.Exp(-age*3800));
            samples[i]=(float)value;
        }
        return Save(SlidePath,samples,1,.72f,65);
    }

    static double Contact(double t,double start,double strength,double frequency,double noise)
    {
        double age=t-start;if(age<0||age>.24)return 0;
        double attack=1-Math.Exp(-age*7000);
        double metal=.30*Math.Sin(Tau*frequency*age)*Math.Exp(-age*92)
            +.12*Math.Sin(Tau*frequency*1.87*age+.4)*Math.Exp(-age*145)
            +.055*Math.Sin(Tau*frequency*.43*age)*Math.Exp(-age*62);
        return strength*attack*(noise*1.9*Math.Exp(-age*135)+metal);
    }
    static double Drag(double t,double start,double duration,double rail,double contact)
    {
        double age=t-start;if(age<=0||age>=duration)return 0;
        double envelope=Math.Sin(Math.PI*age/duration);
        return (rail*1.5+contact*.18)*envelope*(.8+.2*Math.Sin(Tau*47*age));
    }
    static double Coefficient(double frequency)=>1-Math.Exp(-Tau*frequency/Rate);
    static double Noise(ref uint state)
    {state^=state<<13;state^=state>>17;state^=state<<5;return (state&0xFFFFFF)/8388607.5-1;}

    static AudioClip Save(string path,float[] samples,int channels,float peakTarget,double highpass)
    {
        EnsureFolder(AudioRoot);
        int frames=samples.Length/channels;
        for(int channel=0;channel<channels;channel++)
        {
            double low=0,coefficient=Coefficient(highpass);
            for(int frame=0;frame<frames;frame++)
            {
                int index=frame*channels+channel;double sample=samples[index];low+=(sample-low)*coefficient;
                double fadeIn=Math.Min(1,frame/(Rate*.0003)),fadeOut=Math.Min(1,(frames-1-frame)/(Rate*.030));
                samples[index]=(float)((sample-low)*fadeIn*fadeOut);
            }
        }
        float peak=0f;
        foreach(float value in samples)
        {if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Non-finite M1911 audio.");peak=Mathf.Max(peak,Mathf.Abs(value));}
        float gain=peakTarget/Mathf.Max(.000001f,peak);
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            int bytes=samples.Length*2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(Rate);writer.Write(Rate*channels*2);
            writer.Write((short)(channels*2));writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
            for(int i=0;i<samples.Length;i++)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i]*gain,-.98f,.98f)*32767));
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(AudioImporter)AssetImporter.GetAtPath(path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
        importer.defaultSampleSettings=settings;importer.forceToMono=channels==1;importer.loadInBackground=false;
        var serialized=new SerializedObject(importer);var normalize=serialized.FindProperty("m_Normalize");
        if(normalize!=null){normalize.boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();}
        importer.SaveAndReimport();
        AudioClip clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if(clip==null)throw new IOException("Failed to import original M1911 audio: "+path);
        Debug.Log("[M1911 audio] "+Path.GetFileName(path)+": "+(frames/(double)Rate).ToString("F2")+"s / "+channels+"ch / 48kHz PCM16 / peak "+(20*Math.Log10(peakTarget)).ToString("F2")+"dBFS");
        return clip;
    }
    static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
}
