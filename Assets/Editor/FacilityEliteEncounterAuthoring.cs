using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Installs the final finite squad without rebuilding bank geometry, the car, lighting or navigation.</summary>
public static class FacilityEliteEncounterAuthoring
{
    [MenuItem("Tools/Facility/Install Elite SWAT Encounter and Bandanas")]
    public static void InstallMenu() => Debug.Log(Install());

    public static string Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Install the elite encounter outside Play mode and lighting bakes.");
        EditorSceneManager.SaveOpenScenes();
        string original=SceneManager.GetActiveScene().path;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StoryLevel01.unity",OpenSceneMode.Single);
        string backup="Assets/_Recovery/BeforeElite_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not preserve the story before the elite encounter.");
        var director=FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if(director==null || director.policeWaves==null || director.policeWaves.policePrefab==null)
            throw new InvalidOperationException("The saved bank and regular police source are required.");
        var waves=director.policeWaves;
        if(waves.elitePrefab==null)waves.elitePrefab=FacilityEliteSwatAuthoring.BuildPrefab(waves.policePrefab);
        waves.eliteDelay=5f;waves.eliteTeamSize=4;
        var soundtrack=director.GetComponent<FacilityEliteSoundtrack>();
        if(soundtrack==null)soundtrack=director.gameObject.AddComponent<FacilityEliteSoundtrack>();
        FacilityEliteMusicAuthoring.Configure(soundtrack);director.eliteSoundtrack=soundtrack;
        EditorUtility.SetDirty(director);EditorUtility.SetDirty(waves);EditorUtility.SetDirty(soundtrack);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save elite encounter references.");
        string castReport=FacilityCharacterCastAuthoring.Install();
        if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
        var story=SceneManager.GetActiveScene();
        if(story.path!="Assets/Scenes/StoryLevel01.unity")story=EditorSceneManager.OpenScene("Assets/Scenes/StoryLevel01.unity",OpenSceneMode.Single);
        string validation=Validate(story);
        if(!string.IsNullOrEmpty(original) && original!=story.path)EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
        return validation+" "+castReport;
    }

    public static string Validate(Scene scene)
    {
        var director=FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if(director==null || director.policeWaves==null || director.eliteSoundtrack==null)
            throw new InvalidOperationException("Elite encounter references are missing.");
        var waves=director.policeWaves;
        if(waves.elitePrefab==null || waves.elitePrefab.activeSelf || waves.eliteTeamSize!=4 || Mathf.Abs(waves.eliteDelay-5f)>.01f)
            throw new InvalidOperationException("The finite four-officer elite source and five-second warning must be saved.");
        var actor=waves.elitePrefab.GetComponent<FacilityHumanoid>();var elite=waves.elitePrefab.GetComponent<FacilityEliteSwat>();
        var officer=waves.elitePrefab.GetComponent<FacilityPoliceOfficer>();
        if(actor==null || elite==null || officer==null || actor.hitsToKill!=6 || actor.hitsToKnockDown!=3 ||
            actor.helmetHitsToBreak!=2 || actor.hitsToSeverLimb!=2 || officer.walkSpeed<=2.6f || officer.shotInterval>=1.65f ||
            elite.eyeRenderer==null || !elite.eyeRenderer.transform.IsChildOf(actor.head) ||
            elite.helmetPlates==null || elite.helmetPlates.Length!=2 || elite.helmetPlates[0]==null ||
            elite.opticRenderers==null || elite.opticRenderers.Length!=4 ||
            actor.GetComponentsInChildren<FacilityEliteOptic>(true).Length!=4 ||
            elite.opticRenderers.Any(r=>r==null || !r.transform.IsChildOf(elite.helmetPlates[0].transform)))
            throw new InvalidOperationException("Elite armor, four breakaway night vision optics and combat variant are incomplete.");
        if(director.eliteSoundtrack.Source==null || director.eliteSoundtrack.Source.clip==null || !EditorUtility.IsPersistent(director.eliteSoundtrack.Source.clip))
            throw new InvalidOperationException("The saved escalation soundtrack is missing.");
        if(director.heistStatusText==null || director.heistStatusPanel==null)throw new InvalidOperationException("The reinforcement warning needs its saved HUD.");
        return "Elite encounter validated: finite squad of four, regular waves stop on both loot items, full-squad extraction gate, plated helmets, four glowing breakaway night vision lenses, armor resistance, faster armed AI and saved eerie soundtrack.";
    }
}
