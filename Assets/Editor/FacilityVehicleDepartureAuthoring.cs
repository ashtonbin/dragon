using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Preserves the installed vehicle and moves its extraction interaction onto the actual driver's seat.</summary>
public static class FacilityVehicleDepartureAuthoring
{
    public static string Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Install the driver seat outside Play mode and baking.");
        string original=SceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        string report="";
        foreach(var entry in EditorBuildSettings.scenes.Where(item=>item.enabled))
        {
            Scene scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
            string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeDriverSeat_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not preserve the scene before driver-seat authoring.");
            bool story=Apply(scene);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save car prompts and seat interaction.");
            if(story)report=Validate(scene);
        }
        if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
        return report+" Player-facing vehicle wording updated in both enabled scenes; existing geometry, lighting and navigation preserved.";
    }

    public static bool Apply(Scene scene)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            foreach(Text text in root.GetComponentsInChildren<Text>(true))
            { string clean=CarWording(text.text);if(clean!=text.text){text.text=clean;EditorUtility.SetDirty(text);} }
            foreach(FacilityStoryInteractable item in root.GetComponentsInChildren<FacilityStoryInteractable>(true))
            { item.label=CarWording(item.label);EditorUtility.SetDirty(item); }
        }
        var director=FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if(director==null)return false;
        if(director.sedanExit==null || director.sedanExit.transform.parent==null)
            throw new InvalidOperationException("The saved car extraction reference is required.");
        Transform car=director.sedanExit.transform.parent;
        var courtesy=car.GetComponent<FacilityVehicleCourtesyLights>();
        if(courtesy==null || courtesy.driverDoor==null)
            throw new InvalidOperationException("The actual opening driver door is required for boarding.");
        var extraction=car.GetComponent<FacilityVehicleExtraction>();
        if(extraction==null)extraction=car.gameObject.AddComponent<FacilityVehicleExtraction>();
        extraction.director=director;extraction.driverDoor=courtesy.driverDoor;
        extraction.boardingSeconds=.85f;
        extraction.seatPose=Pose(car,"Car driver seat - seated body pose",new Vector3(-.422f,.46f,-.18f),Quaternion.identity);
        extraction.eyePose=Pose(car,"Car driver seat - seated eye pose",new Vector3(-.422f,1.215f,-.18f),Quaternion.Euler(3f,0f,0f));
        FacilityStoryInteractable seat=director.sedanExit;
        seat.name="Car driver seat - enter to leave";
        seat.transform.localPosition=new Vector3(-.422f,.87f,-.18f);
        seat.transform.localRotation=Quaternion.identity;seat.transform.localScale=Vector3.one;
        seat.kind=FacilityStoryInteractionKind.Sedan;seat.director=director;seat.door=courtesy.driverDoor;
        seat.label="Get into the driver's seat";
        BoxCollider target=seat.GetComponent<BoxCollider>();if(target==null)target=seat.gameObject.AddComponent<BoxCollider>();
        target.isTrigger=true;target.center=Vector3.zero;target.size=new Vector3(.50f,.38f,.56f);
        director.vehicleExtraction=extraction;
        if(director.drillPickup!=null)director.drillPickup.label="Collect car equipment";
        if(director.typedObjective!=null)
            director.typedObjective.SetObjective("NIGHT DEPOSIT / OBJECTIVE","Open the car's trunk. Collect your weapons and vault drill.",1,7);
        EditorUtility.SetDirty(extraction);EditorUtility.SetDirty(director);EditorUtility.SetDirty(seat);EditorUtility.SetDirty(target);
        Physics.SyncTransforms();return true;
    }

    public static string Validate(Scene scene)
    {
        var director=FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if(director==null || director.vehicleExtraction==null)throw new InvalidOperationException("The story requires its driver-seat extraction component.");
        var departure=director.vehicleExtraction;Transform car=director.sedanExit.transform.parent;
        if(departure.director!=director || departure.driverDoor==null || departure.seatPose==null || departure.eyePose==null ||
            !departure.driverDoor.transform.IsChildOf(car) || !departure.seatPose.IsChildOf(car) || !departure.eyePose.IsChildOf(car))
            throw new InvalidOperationException("The seat, eye pose and driver door must be assigned to this car.");
        Vector3 target=car.InverseTransformPoint(director.sedanExit.transform.position);
        Vector3 eye=car.InverseTransformPoint(departure.eyePose.position);
        if(target.x>-.2f || Mathf.Abs(target.z)>.6f || eye.y<1.1f || eye.y>1.4f)
            throw new InvalidOperationException("Extraction must be in the actual left-hand driver seat, below the roof.");
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            foreach(FacilityStoryInteractable item in root.GetComponentsInChildren<FacilityStoryInteractable>(true))
                if(Regex.IsMatch(item.label??"","challenger",RegexOptions.IgnoreCase))throw new InvalidOperationException("An old vehicle prompt remains: "+item.label);
            foreach(Text text in root.GetComponentsInChildren<Text>(true))
                if(Regex.IsMatch(text.text??"","challenger",RegexOptions.IgnoreCase))throw new InvalidOperationException("Old vehicle wording remains in "+text.name);
        }
        return "Driver-seat extraction validated: actual left seat, opening driver door, seated camera, saved references and car wording.";
    }

    static Transform Pose(Transform car,string name,Vector3 position,Quaternion rotation)
    {
        Transform pose=car.Find(name);
        if(pose==null){pose=new GameObject(name).transform;pose.SetParent(car,false);}
        pose.localPosition=position;pose.localRotation=rotation;pose.localScale=Vector3.one;
        return pose;
    }

    static string CarWording(string value)
    {
        if(string.IsNullOrEmpty(value))return value;
        return Regex.Replace(value,@"(?:silver\s+)?challenger",match=>match.Value==match.Value.ToUpperInvariant()?"CAR":"car",RegexOptions.IgnoreCase);
    }
}
