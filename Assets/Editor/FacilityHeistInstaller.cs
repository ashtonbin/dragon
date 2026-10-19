using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Updates the existing authored scenes explicitly, after saving recoveries. Never runs on Play or import.</summary>
public static class FacilityHeistInstaller
{
    [MenuItem("Tools/Facility/Upgrade Heist Doors Drill and Police")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Upgrade the heist outside Play mode and baking.");
        Scene initial = SceneManager.GetActiveScene();
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/HeistRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(initial, recovery, true)) throw new IOException("Recovery save failed; heist upgrade stopped.");
        FacilityGameSession originalSession = FacilitySceneGenerator.FindInScene<FacilityGameSession>(initial);
        string storyName = originalSession != null && originalSession.storySceneNames.Length > 0 ? originalSession.storySceneNames[0] : "StoryLevel01";
        string storyPath = "Assets/Scenes/" + storyName + ".unity";
        Scene story = initial.path == storyPath ? initial : EditorSceneManager.OpenScene(storyPath, OpenSceneMode.Single);
        FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(story);
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(story);
        if (session == null || director == null) throw new InvalidOperationException("The saved Level 1 session or director is missing.");
        string report = FacilityHeistUpgrade.Apply(session, director);
        GameObject level = null;
        foreach (GameObject root in story.GetRootGameObjects()) if (root.name == "Story Level 1 - Night Deposit") level = root;
        if (level == null) throw new InvalidOperationException("The editable story root is missing.");
        FacilityPoliceProjectiles rounds = session.GetComponent<FacilityPoliceProjectiles>();
        if (rounds == null) rounds = session.gameObject.AddComponent<FacilityPoliceProjectiles>();
        FacilityPoliceAuthoring.ConfigureProjectiles(rounds);
        FacilityPoliceWaves waves = session.GetComponent<FacilityPoliceWaves>();
        if (waves == null) waves = session.gameObject.AddComponent<FacilityPoliceWaves>();
        waves.policePrefab = FacilityPoliceAuthoring.BuildPrefab();
        waves.projectiles = rounds; waves.player = session.player;
        waves.interval = 15f; waves.maxActivePolice = 8;
        waves.spawnPoints = FacilityHeistUpgrade.PoliceSpawnPoints(level.transform);
        director.policeWaves = waves;
        FacilityHeistInterfaceAuthoring.Install(session);
        FacilityStoryInstaller.RebuildNavigation(story);
        EditorUtility.SetDirty(waves); EditorUtility.SetDirty(rounds); EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(story);
        if (!EditorSceneManager.SaveScene(story)) throw new IOException("Story save failed; recovery: " + recovery);

        Scene sandbox = EditorSceneManager.OpenScene("Assets/Scenes/" + session.sandboxSceneName + ".unity", OpenSceneMode.Single);
        string sandboxRecovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/HeistUIRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(sandbox, sandboxRecovery, true)) throw new IOException("Sandbox UI recovery failed; font update stopped.");
        FacilityGameSession sandboxSession = FacilitySceneGenerator.FindInScene<FacilityGameSession>(sandbox);
        FacilityHeistInterfaceAuthoring.Install(sandboxSession);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(sandbox);
        if (!EditorSceneManager.SaveScene(sandbox)) throw new IOException("Could not save the Sandbox interface.");
        EditorSceneManager.OpenScene(storyPath, OpenSceneMode.Single);
        Debug.Log("Heist upgrade saved. " + report + " Police interval 15s, exact vault drill 45s; rebake the active story scene.");
    }
}
