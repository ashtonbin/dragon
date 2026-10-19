using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicitly upgrades the saved scenes without rebuilding the bank or its lighting.</summary>
public static class FacilityProgressionAuthoring
{
    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before authoring.");
        string original = SceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeProgression_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not preserve scene before progression upgrade.");
            FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
            if (session == null) continue;
            FacilityAK47Authoring.Apply(scene);
            if (session.storyDirector != null) UpgradeStory(session.storyDirector);
            foreach (Text text in session.canvas.GetComponentsInChildren<Text>(true))
            {
                text.text = text.text.Replace("Use your fists.", "").Replace("Use fists.", "")
                    .Replace("Bonus gold secured.", "Gold secured.").Replace("Gold is a bonus.", "Retrieve both cash and gold.")
                    .Replace("TAKE BONUS GOLD", "TAKE GOLD");
                EditorUtility.SetDirty(text);
            }
            if (session.completionBodyText != null) session.completionBodyText.text = "Cash and gold secured. +$2,500 banked.";
            if (session.weaponNameText != null) session.weaponNameText.text = "PISTOL";
            EditorUtility.SetDirty(session);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save progression scene " + entry.path);
        }
        EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        return "Saved sedan weapon pickup, mandatory cash + gold, owned arsenal and detailed AK-47 to both enabled scenes. " + FacilityEconomy.ValidateIsolatedAccount();
    }

    public static void UpgradeStory(FacilityStoryDirector director)
    {
        FacilityStoryInteractable old = director.pistolPickup;
        if (old != null)
        {
            if (old.visualRoot != null) Object.DestroyImmediate(old.visualRoot);
            Object.DestroyImmediate(old.gameObject);
        }
        if (director.pistolAccessPlane != null) Object.DestroyImmediate(director.pistolAccessPlane.gameObject);
        if (director.pistolAlarmWarning != null) Object.DestroyImmediate(director.pistolAlarmWarning.gameObject);
        director.pistolPickup = null; director.pistolVisibilityPoint = null;
        director.pistolAccessPlane = null; director.pistolAlarmWarning = null;
        if (director.alarmSource != null) director.alarmSource.name = "Security alarm - receptionist attacked";
        FacilityStoryInteractable pickup = director.drillPickup;
        if (pickup == null || pickup.visualRoot == null) throw new InvalidOperationException("The sedan drill is missing.");
        GameObject equipment = pickup.visualRoot;
        if (equipment.name != "Sedan equipment - weapons and vault drill")
        {
            Transform drill = equipment.transform;
            equipment = new GameObject("Sedan equipment - weapons and vault drill");
            equipment.transform.SetParent(drill.parent, false);
            drill.SetParent(equipment.transform, true);
            pickup.visualRoot = equipment;
        }
        if (equipment.transform.Find("Service Pistol Pickup") == null)
        {
            GameObject pistol = FacilityPlayerArsenalAuthoring.BuildWorldPistol(equipment.transform, new Vector3(.39f, .81f, -1.36f));
            pistol.transform.localRotation = Quaternion.Euler(0, 78, 90);
            pistol.transform.localScale = Vector3.one * .85f;
        }
        pickup.name = "Sedan equipment pickup - pistol and drill";
        pickup.label = "Collect weapons and vault drill";
        if (director.goldPickup != null) director.goldPickup.label = "Take the gold";
        if (director.typedObjective != null)
            director.typedObjective.SetObjective("NIGHT DEPOSIT / OBJECTIVE", "Open the car's trunk. Collect your weapons and vault drill.", 1, 7);
        EditorUtility.SetDirty(pickup); EditorUtility.SetDirty(director);
    }
}
