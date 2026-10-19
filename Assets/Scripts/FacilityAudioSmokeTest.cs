using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Explicit standalone audio integration check: -facilityAudioTest "output directory".</summary>
/// <remarks>Ordinary game and editor sessions never create this runner or change the authored scene.</remarks>
public sealed class FacilityAudioSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class ClipRecord
    {
        public string name;
        public int channels, frequency, sampleFrames;
        public float seconds, peak, rms;
        public bool readable;
    }

    [Serializable] private sealed class SourceRecord
    {
        public string name, clip;
        public bool loop, playing;
        public float volume, spatialBlend, minDistance, maxDistance, dopplerLevel;
    }

    [Serializable] private sealed class Receipt
    {
        public bool success, startedThroughMenu, allAmbientLoopsPlaying, noAirConditionClips,
            creepyHumBound, shotgunReportBound, heavyPumpBound, pumpSynchronized, pumpOncePerShot, zombiePlacedThroughMenu, zombieMoaned,
            zombieMoanPaused, zombieHeadShot, zombieMoanStoppedOnDeath, zombieUsesOwnVoice,
            corpseDidNotSpeakAgain, observerScreamedOnBodyShot, mixedOutputHasHeadroom, noRuntimeAudioErrors;
        public string utc, unity, gpu, scene;
        public string coverage = "Opt-in standalone check of saved ambient, shotgun and character audio bindings; ordinary Start/character placement/pause paths; actual shotgun head/body/corpse impacts; source gain/spatial settings; decoded clip metadata and output samples. The scene is never saved.";
        public double dspStarted, dspFinished;
        public float zombieFirstMoanSeconds, ambientOutputPeak, shotOutputPeak, pumpStartDelay;
        public int zombieMoans, zombieVoiceEvents, observerVoiceEvents, shotsFired;
        public List<ClipRecord> clips = new List<ClipRecord>();
        public List<SourceRecord> sources = new List<SourceRecord>();
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private readonly HashSet<AudioClip> inspectedClips = new HashSet<AudioClip>();
    private readonly float[] outputSamples = new float[2048];
    private string output;
    private bool collectingErrors;
    private FacilityGameSession session;
    private RetroFPSController player;
    private CharacterController playerMotor;
    private FacilityShotgun shotgun;
    private Camera eye;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(arguments, "-facilityAudioTest");
        if (flag < 0) return;
        if (flag + 1 >= arguments.Length || arguments[flag + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityAudioTest requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Facility audio diagnostics (explicit CLI only)").AddComponent<FacilityAudioSmokeTest>();
        runner.output = arguments[flag + 1];
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        Application.logMessageReceived += OnLog;
        collectingErrors = true;
        var routines = new Stack<IEnumerator>();
        routines.Push(Run());
        while (routines.Count > 0)
        {
            IEnumerator routine = routines.Peek();
            bool more;
            object current = null;
            try { more = routine.MoveNext(); if (more) current = routine.Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { routines.Pop(); continue; }
            if (current is IEnumerator nested) routines.Push(nested);
            else yield return current;
        }
        collectingErrors = false;
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        receipt.dspFinished = AudioSettings.dspTime;
        receipt.success = receipt.failures.Count == 0;
        try
        {
            File.WriteAllText(Path.Combine(output, "audio-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Facility audio integration check " + (receipt.success ? "passed: " : "failed: ") + output);
        }
        catch (Exception exception) { Debug.LogException(exception); Application.Quit(2); yield break; }
        Application.Quit(receipt.success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        yield return new WaitForSecondsRealtime(.6f);
        receipt.utc = DateTime.UtcNow.ToString("O");
        receipt.unity = Application.unityVersion;
        receipt.gpu = SystemInfo.graphicsDeviceName;
        receipt.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        receipt.dspStarted = AudioSettings.dspTime;
        session = FindFirstObjectByType<FacilityGameSession>();
        player = FindFirstObjectByType<RetroFPSController>();
        shotgun = FindFirstObjectByType<FacilityShotgun>();
        if (session == null || player == null || shotgun == null || session.spawner == null)
            throw new InvalidOperationException("The authored session, spawner, player and shotgun are required.");
        playerMotor = player.GetComponent<CharacterController>();
        eye = player.GetComponentInChildren<Camera>();
        if (playerMotor == null || eye == null) throw new InvalidOperationException("The original player camera/capsule is missing.");
        bool wasMainMenu = session.CurrentState == FacilityGameState.MainMenu;
        session.StartGame();
        session.StartSandbox();
        yield return new WaitForSecondsRealtime(.25f);
        receipt.startedThroughMenu = wasMainMenu && session.CurrentState == FacilityGameState.Playing && !AudioListener.pause;
        Check(receipt.startedThroughMenu, "The normal StartGame menu path did not enter audible gameplay.");
        // Keep input from competing with the deliberately aimed diagnostic shots.
        player.enabled = false;

        FacilityAmbientEffects ambience = FindFirstObjectByType<FacilityAmbientEffects>();
        if (ambience == null) throw new InvalidOperationException("No authored FacilityAmbientEffects was found.");
        receipt.allAmbientLoopsPlaying = ambience.loopingSources != null && ambience.loopingSources.Length == 4;
        receipt.noAirConditionClips = true;
        foreach (AudioSource source in ambience.loopingSources)
        {
            AddSource(source);
            receipt.allAmbientLoopsPlaying &= source != null && source.clip != null && source.isPlaying && source.loop;
            if (source == null || source.clip == null) continue;
            receipt.noAirConditionClips &= !Contains(source.clip.name, "Ventilation") && !Contains(source.clip.name, "Duct");
            receipt.creepyHumBound |= Contains(source.clip.name, "Creepy Hum");
        }
        AddSource(ambience.intermittentSource);
        if (ambience.intermittentSource != null && ambience.intermittentSource.clip != null)
            receipt.noAirConditionClips &= !Contains(ambience.intermittentSource.clip.name, "Ventilation") && !Contains(ambience.intermittentSource.clip.name, "Duct");
        Check(receipt.allAmbientLoopsPlaying, "All four persistent ambient loops must play after Start.");
        Check(receipt.noAirConditionClips, "An old ventilation/duct air-conditioning clip remains assigned to a world sound source.");
        Check(receipt.creepyHumBound, "The background bed is not assigned to the new Creepy Hum asset.");
        AddSource(shotgun.reportSource);
        InspectClip(shotgun.reportClip);
        receipt.shotgunReportBound = shotgun.reportClip != null && shotgun.reportSource != null &&
            (Contains(shotgun.reportClip.name, "V3") || Contains(shotgun.reportClip.name, "Deep"));
        Check(receipt.shotgunReportBound, "The shotgun does not reference the new deep report asset.");
        AddSource(shotgun.actionSource);
        InspectClip(shotgun.actionClip);
        receipt.heavyPumpBound = shotgun.actionClip != null && Contains(shotgun.actionClip.name, "Heavy Shotgun Pump") &&
            shotgun.actionSource != null && shotgun.actionSource.clip == shotgun.actionClip && shotgun.actionSource.volume >= .5f;
        Check(receipt.heavyPumpBound, "The pump still uses the old synthetic percussion or lacks the recorded heavy rack.");
        float sampleUntil = Time.realtimeSinceStartup + .2f;
        while (Time.realtimeSinceStartup < sampleUntil)
        { receipt.ambientOutputPeak = Mathf.Max(receipt.ambientOutputPeak, OutputPeak()); yield return null; }

        FacilityHumanoid observer = null;
        foreach (FacilityHumanoid actor in FindObjectsByType<FacilityHumanoid>(FindObjectsSortMode.None))
            if (!actor.IsHostile && actor.GetComponent<FacilityZombie>() == null) { observer = actor; break; }
        if (observer == null) throw new InvalidOperationException("The authored observer was not found.");
        InspectClip(observer.hurtSound); InspectClip(observer.deathSound); AddSource(observer.voice);
        Check(observer.hurtSound != null && Contains(observer.hurtSound.name, "Observer Pain") && observer.hurtSound.length >= 2.3f,
            "The observer needs the recorded scream rather than the old synthetic hurt tone.");

        SetPlayerPose(new Vector3(-2f, .05f, 2f), 90f);
        session.ToggleSpawnMenu();
        session.SelectCharacter(FacilitySpawnKind.Zombie);
        bool hasFloor = session.spawner.TryGetPlacement(new Ray(new Vector3(2f, 3f, 2f), Vector3.down), out Vector3 feet, out string reason);
        receipt.zombiePlacedThroughMenu = hasFloor && session.spawner.TryPlaceSelected(feet, out reason);
        Check(receipt.zombiePlacedThroughMenu, "The public spawn menu could not place the audio-test zombie: " + reason);
        FacilityZombie zombie = receipt.zombiePlacedThroughMenu && session.spawner.LastSpawned != null
            ? session.spawner.LastSpawned.GetComponent<FacilityZombie>() : null;
        if (zombie == null) throw new InvalidOperationException("Zombie placement did not create a FacilityZombie.");
        FacilityHumanoid zombieActor = zombie.GetComponent<FacilityHumanoid>();
        session.CancelPlacement();
        AddSource(zombie.moanSource); AddSource(zombieActor.voice);
        Check(zombie.moanClips != null && zombie.moanClips.Length == 3, "The zombie must retain its three recorded moan variations.");
        if (zombie.moanClips != null) foreach (AudioClip clip in zombie.moanClips) InspectClip(clip);
        Check(zombie.moanSource != null && zombie.moanSource.volume > 0f && zombie.moanSource.volume <= .18f &&
            zombie.moanSource.spatialBlend >= .99f && zombie.moanSource.maxDistance <= 16f && zombie.moanSource.dopplerLevel == 0f,
            "Zombie moans are missing their quiet, short-range spatial settings.");
        float moanStarted = Time.realtimeSinceStartup;
        while (zombie.MoansPlayed == 0 && Time.realtimeSinceStartup - moanStarted < 4f) yield return null;
        receipt.zombieFirstMoanSeconds = Time.realtimeSinceStartup - moanStarted;
        receipt.zombieMoaned = zombie.MoansPlayed > 0 && zombie.moanSource != null && zombie.moanSource.isPlaying;
        Check(receipt.zombieMoaned, "The spawned zombie did not audibly begin an intermittent moan within four seconds.");
        int moansBeforePause = zombie.MoansPlayed;
        session.PauseGame();
        yield return new WaitForSecondsRealtime(.3f);
        receipt.zombieMoanPaused = zombie.MoansPlayed == moansBeforePause && AudioListener.pause && Time.timeScale == 0f;
        Check(receipt.zombieMoanPaused, "Pause allowed zombie moan scheduling to advance.");
        session.ResumeGame();
        yield return new WaitForSecondsRealtime(.2f);
        Aim(zombieActor.head.position);
        float firedAt = Time.time;
        int pumpsBefore = shotgun.PumpCyclesPlayed;
        bool zombieFired = shotgun.TryFire();
        sampleUntil = Time.realtimeSinceStartup + .18f;
        while (Time.realtimeSinceStartup < sampleUntil)
        { receipt.shotOutputPeak = Mathf.Max(receipt.shotOutputPeak, OutputPeak()); yield return null; }
        receipt.zombieHeadShot = zombieFired && zombieActor.IsDead && zombieActor.HeadExploded;
        receipt.zombieMoanStoppedOnDeath = zombie.moanSource != null && !zombie.moanSource.isPlaying;
        receipt.zombieUsesOwnVoice = zombieActor.VoiceEvents == 1 && zombieActor.LastVoicePlayed != null &&
            Contains(zombieActor.LastVoicePlayed.name, "Zombie") && !Contains(zombieActor.LastVoicePlayed.name, "Observer Pain");
        Check(receipt.zombieHeadShot, "The actual head-aimed shotgun discharge did not kill the zombie.");
        Check(receipt.zombieMoanStoppedOnDeath, "A dead zombie continued its idle moan.");
        Check(receipt.zombieUsesOwnVoice, "Zombie damage played an observer scream or restarted its voice for individual pellets.");
        yield return new WaitForSecondsRealtime(.13f);
        receipt.pumpStartDelay = shotgun.LastPumpStartedAt - firedAt;
        receipt.pumpSynchronized = shotgun.PumpCyclesPlayed == pumpsBefore + 1 && receipt.pumpStartDelay >= .23f &&
            receipt.pumpStartDelay < .31f && shotgun.actionSource.isPlaying;
        Check(receipt.pumpSynchronized, "The recorded pump did not start once with the rearward animation stroke: " + receipt.pumpStartDelay);
        InspectClip(zombieActor.LastVoicePlayed);
        int deadVoiceEvents = zombieActor.VoiceEvents;
        yield return new WaitForSecondsRealtime(shotgun.FireInterval + .15f);
        int bloodBefore = zombieActor.gore.TotalDropsEmitted;
        Aim(zombieActor.pelvis.position);
        bool corpseFired = shotgun.TryFire();
        yield return new WaitForSecondsRealtime(.2f);
        receipt.corpseDidNotSpeakAgain = corpseFired && zombieActor.gore.TotalDropsEmitted > bloodBefore && zombieActor.VoiceEvents == deadVoiceEvents;
        Check(receipt.corpseDidNotSpeakAgain, "A real corpse impact failed to land or triggered a new hurt voice.");
        receipt.zombieMoans = zombie.MoansPlayed;
        receipt.zombieVoiceEvents = zombieActor.VoiceEvents;

        yield return new WaitForSecondsRealtime(shotgun.FireInterval + .05f);
        SetPlayerPose(new Vector3(10f, .05f, 18.3f), 0f);
        Aim(observer.AimPoint);
        bool observerFired = shotgun.TryFire();
        sampleUntil = Time.realtimeSinceStartup + .20f;
        while (Time.realtimeSinceStartup < sampleUntil)
        { receipt.shotOutputPeak = Mathf.Max(receipt.shotOutputPeak, OutputPeak()); yield return null; }
        receipt.observerScreamedOnBodyShot = observerFired && observer.HitsTaken == 1 && !observer.IsDead && !observer.HeadExploded &&
            observer.VoiceEvents == 1 && observer.LastVoicePlayed != null && Contains(observer.LastVoicePlayed.name, "Observer Pain") &&
            observer.voice != null && observer.voice.isPlaying && observer.voice.volume >= .6f && observer.voice.volume <= .85f;
        Check(receipt.observerScreamedOnBodyShot, "One body shell did not produce one full-volume recorded observer scream.");
        receipt.observerVoiceEvents = observer.VoiceEvents;
        receipt.mixedOutputHasHeadroom = receipt.shotOutputPeak > .001f && receipt.shotOutputPeak < .98f;
        Check(receipt.mixedOutputHasHeadroom, "The combined shotgun, blood impact and observer scream is silent or reaches full-scale clipping: " + receipt.shotOutputPeak);
        receipt.shotsFired = shotgun.ShotsFired;
        AddSource(observer.voice); AddSource(zombie.moanSource); AddSource(zombieActor.voice);
        yield return new WaitForSecondsRealtime(.2f);
        receipt.pumpOncePerShot = shotgun.PumpCyclesPlayed == shotgun.ShotsFired;
        Check(receipt.pumpOncePerShot, "Pump audio must play exactly once per fired shell.");
        receipt.noRuntimeAudioErrors = receipt.failures.Count == 0;
    }

    private void AddSource(AudioSource source)
    {
        if (source == null) { Check(false, "An expected authored AudioSource is missing."); return; }
        receipt.sources.Add(new SourceRecord { name = source.name, clip = source.clip != null ? source.clip.name : "<none>",
            loop = source.loop, playing = source.isPlaying, volume = source.volume, spatialBlend = source.spatialBlend,
            minDistance = source.minDistance, maxDistance = source.maxDistance, dopplerLevel = source.dopplerLevel });
        InspectClip(source.clip);
    }

    private void InspectClip(AudioClip clip)
    {
        if (clip == null || !inspectedClips.Add(clip)) return;
        var record = new ClipRecord { name = clip.name, channels = clip.channels, frequency = clip.frequency,
            sampleFrames = clip.samples, seconds = clip.length };
        var samples = new float[clip.samples * clip.channels];
        record.readable = clip.GetData(samples, 0);
        double squares = 0;
        if (record.readable)
        {
            foreach (float sample in samples)
            { record.peak = Mathf.Max(record.peak, Mathf.Abs(sample)); squares += sample * (double)sample; }
            if (samples.Length > 0) record.rms = (float)Math.Sqrt(squares / samples.Length);
        }
        receipt.clips.Add(record);
        Check(record.readable && record.peak > .005f && record.peak <= 1.0001f && record.rms > .0001f,
            "Audio clip is unreadable, silent or clipped beyond full scale: " + clip.name);
    }

    private void SetPlayerPose(Vector3 feet, float yaw)
    {
        bool wasEnabled = playerMotor.enabled;
        playerMotor.enabled = false;
        player.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
        player.ResetRecoil();
        eye.transform.localRotation = Quaternion.identity;
        playerMotor.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    private void Aim(Vector3 target)
    {
        player.ResetRecoil();
        Quaternion rotation = Quaternion.LookRotation(target - eye.transform.position, Vector3.up);
        player.transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        eye.transform.rotation = rotation;
        Physics.SyncTransforms();
    }

    private float OutputPeak()
    {
        AudioListener.GetOutputData(outputSamples, 0);
        float peak = 0f;
        foreach (float value in outputSamples) peak = Mathf.Max(peak, Mathf.Abs(value));
        return peak;
    }

    private static bool Contains(string value, string part) => value != null && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    private void Check(bool condition, string message) { if (!condition) receipt.failures.Add(message); }
    private void OnLog(string message, string trace, LogType kind)
    {
        if (collectingErrors && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace);
    }
}
