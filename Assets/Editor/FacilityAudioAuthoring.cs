using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit sound-only authoring. Keeps the facility, player, menus, navigation and baked lighting intact.</summary>
public static class FacilityAudioAuthoring
{
    private const string VoiceRoot = "Assets/Audio/Voices/";
    private static readonly Dictionary<string, AudioClip> Voices = new Dictionary<string, AudioClip>();

    public static AudioClip PumpAction() => ImportRecording("Assets/Audio/Weapons/Heavy Shotgun Pump.wav");

    [MenuItem("Tools/Facility/Replace Shotgun Pump Sound")]
    public static void ApplyPump()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Replace pump audio outside Play mode and baking.");
        Scene scene = SceneManager.GetActiveScene();
        FacilityShotgun weapon = FacilitySceneGenerator.FindInScene<FacilityShotgun>(scene);
        if (weapon == null || weapon.actionSource == null) throw new InvalidOperationException("The authored shotgun and its pump source are required.");
        AudioClip clip = PumpAction();
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/PumpAudioRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true)) throw new IOException("Recovery save failed; pump audio was not changed.");
        ConfigurePump(weapon, clip);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save pump audio. Recovery: " + recovery);
    }

    private static void ConfigurePump(FacilityShotgun weapon, AudioClip clip)
    {
        weapon.actionClip = clip;
        AudioSource source = weapon.actionSource;
        if (source != null)
        {
            source.clip = clip; source.volume = .55f; source.pitch = 1f; source.priority = 40;
            source.spatialBlend = 0f; source.playOnAwake = false; source.loop = false; source.dopplerLevel = 0f;
            EditorUtility.SetDirty(source);
        }
        EditorUtility.SetDirty(weapon);
    }

    [MenuItem("Tools/Facility/Apply Horror Audio")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Apply audio outside Play mode and baking.");
        Scene scene = SceneManager.GetActiveScene();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/AudioRecovery_" + stamp + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true)) throw new IOException("Recovery save failed; audio changes stopped.");
        // Resolve every source before changing the scene or either live spawn prefab.
        FacilitySoundDesign.CreepyHum(); FacilitySoundDesign.ShotgunReport();
        Voice("Observer Pain 01"); Voice("Observer Pain 02"); Voice("Zombie Hurt 01");
        for (int i = 1; i <= 3; i++) Voice("Zombie Moan 0" + i);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (FacilityAmbientEffects effects in root.GetComponentsInChildren<FacilityAmbientEffects>(true)) ConfigureAmbient(effects);
            foreach (FacilityShotgun weapon in root.GetComponentsInChildren<FacilityShotgun>(true)) ConfigureWeapon(weapon);
            foreach (FacilityHumanoid actor in root.GetComponentsInChildren<FacilityHumanoid>(true)) ConfigureActor(actor);
        }
        FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
        if (session != null && session.spawner != null)
        {
            string backupFolder = "Assets/Generated/Interface/Prefabs/AudioRecovery_" + stamp;
            AssetDatabase.CreateFolder("Assets/Generated/Interface/Prefabs", Path.GetFileName(backupFolder));
            var done = new HashSet<string>();
            foreach (GameObject prefab in new[] { session.spawner.observerPrefab, session.spawner.zombiePrefab })
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(path) || !done.Add(path)) continue;
                if (!AssetDatabase.CopyAsset(path, backupFolder + "/" + Path.GetFileName(path)))
                    throw new IOException("Could not preserve spawn prefab " + path);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (FacilityHumanoid actor in contents.GetComponentsInChildren<FacilityHumanoid>(true)) ConfigureActor(actor);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Audio save failed; recovery is " + recovery);
        Debug.Log("Horror audio saved: low hum, deep shotgun, recorded observer screams and quiet zombie moans. Recovery: " + recovery);
    }

    public static void ConfigureAmbient(FacilityAmbientEffects effects)
    {
        if (effects == null || effects.loopingSources == null) return;
        AudioClip hum = FacilitySoundDesign.CreepyHum();
        foreach (AudioSource source in effects.loopingSources)
        {
            if (source == null) continue;
            string name = source.name;
            if (source.spatialBlend < .5f)
            {
                source.name = "Low disturbing hum"; source.clip = hum; source.volume = .14f; source.pitch = 1f; source.priority = 110;
            }
            else if (name.Contains("Duct") || name.Contains("Service resonance"))
            {
                source.name = "Service resonance"; source.clip = hum; source.volume = .055f; source.pitch = .993f; source.maxDistance = 13f;
            }
            else if (name.Contains("Research"))
            {
                source.name = "Research resonance"; source.clip = hum; source.volume = .045f; source.pitch = 1.007f; source.maxDistance = 12f;
            }
            else if (name.Contains("Pump")) source.volume = .18f;
            source.loop = true; source.playOnAwake = false; source.dopplerLevel = 0f;
            EditorUtility.SetDirty(source);
        }
        EditorUtility.SetDirty(effects);
    }

    public static void ConfigureWeapon(FacilityShotgun weapon)
    {
        if (weapon == null) return;
        weapon.reportClip = FacilitySoundDesign.ShotgunReport();
        ConfigurePump(weapon, PumpAction());
        if (weapon.reportSource != null)
        {
            weapon.reportSource.clip = weapon.reportClip; weapon.reportSource.volume = .82f;
            weapon.reportSource.priority = 20; weapon.reportSource.spatialBlend = 0f;
            weapon.reportSource.playOnAwake = false; weapon.reportSource.loop = false;
            EditorUtility.SetDirty(weapon.reportSource);
        }
        EditorUtility.SetDirty(weapon);
    }

    public static void ConfigureActor(FacilityHumanoid actor)
    {
        if (actor == null) return;
        FacilityZombie zombie = actor.GetComponent<FacilityZombie>();
        bool infected = zombie != null;
        AudioSource oldVoice = actor.voice;
        Transform anchor = actor.chest != null ? actor.chest : actor.transform;
        Transform voiceNode = anchor.Find("Character voice");
        if (voiceNode == null)
        {
            voiceNode = new GameObject("Character voice").transform;
            voiceNode.SetParent(anchor, false);
        }
        AudioSource voice = voiceNode.GetComponent<AudioSource>();
        if (voice == null) voice = voiceNode.gameObject.AddComponent<AudioSource>();
        actor.voice = voice;
        actor.hurtSound = Voice(infected ? "Zombie Hurt 01" : "Observer Pain 01");
        actor.deathSound = Voice(infected ? "Zombie Hurt 01" : "Observer Pain 02");
        SetSpatial(voice, infected ? .34f : .66f, 2f, infected ? 16f : 24f, 45);
        voice.clip = actor.hurtSound;
        if (actor.gore != null && actor.gore.impactSource != null)
        {
            actor.gore.impactSource.volume = .64f;
            EditorUtility.SetDirty(actor.gore.impactSource);
        }
        if (oldVoice != null && oldVoice != voice && oldVoice.gameObject == actor.gameObject) Object.DestroyImmediate(oldVoice);
        if (infected)
        {
            zombie.moanClips = new[] { Voice("Zombie Moan 01"), Voice("Zombie Moan 02"), Voice("Zombie Moan 03") };
            Transform node = anchor.Find("Quiet zombie moans");
            if (node == null) { node = new GameObject("Quiet zombie moans").transform; node.SetParent(anchor, false); }
            zombie.moanSource = node.GetComponent<AudioSource>();
            if (zombie.moanSource == null) zombie.moanSource = node.gameObject.AddComponent<AudioSource>();
            SetSpatial(zombie.moanSource, .13f, 1.25f, 14f, 170);
            zombie.moanSource.clip = zombie.moanClips[0];
            EditorUtility.SetDirty(zombie);
        }
        EditorUtility.SetDirty(actor); EditorUtility.SetDirty(voice);
    }

    private static void SetSpatial(AudioSource source, float volume, float minDistance, float maxDistance, int priority)
    {
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 1f; source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = minDistance; source.maxDistance = maxDistance;
        source.volume = volume; source.pitch = 1f; source.priority = priority;
    }

    private static AudioClip Voice(string name) => ImportRecording(VoiceRoot + name + ".wav");

    private static AudioClip ImportRecording(string path)
    {
        if (Voices.TryGetValue(path, out AudioClip cached) && cached != null) return cached;
        if (!File.Exists(path)) throw new FileNotFoundException("Recorded audio asset missing", path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new InvalidOperationException("Voice import failed: " + path);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.PCM; settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.preloadAudioData = true; settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = 44100;
        importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.loadInBackground = false;
        var serialized = new SerializedObject(importer);
        SerializedProperty normalize = serialized.FindProperty("m_Normalize");
        if (normalize != null) { normalize.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        importer.SaveAndReimport();
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException("Voice clip is missing after import: " + path);
        Voices[path] = clip;
        return clip;
    }
}
