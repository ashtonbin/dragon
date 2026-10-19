using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicitly installs editable combat content; does not rebuild the facility or its bake.</summary>
public static class FacilityCombatInstaller
{
    [MenuItem("Tools/Facility/Install Shotgun and Subject")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Install combat outside Play mode and baking.");
        Scene scene = SceneManager.GetActiveScene();
        RetroFPSController player = FacilitySceneGenerator.FindInScene<RetroFPSController>(scene);
        if (player == null) throw new InvalidOperationException("The open scene needs its existing Player.");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/CombatRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true))
            throw new IOException("Recovery save failed. No combat content was changed.");

        RemoveAreaSigns(scene);
        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera == null) throw new InvalidOperationException("The Player has no camera.");
        FacilityShotgun weapon = ShotgunModelAuthoring.Build(camera);
        GameObject subjects = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Combat Subjects") subjects = root;
        if (subjects == null)
        {
            subjects = new GameObject("Combat Subjects");
            SceneManager.MoveGameObjectToScene(subjects, scene);
        }
        // Reinstallation replaces only the previous subject, leaving unrelated authored content alone.
        foreach (FacilityHumanoid previous in subjects.GetComponentsInChildren<FacilityHumanoid>(true))
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
        HumanoidModelAuthoring.Build(new Vector3(10, .01f, 21), player.transform, subjects.transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save combat scene. Recovery: " + recovery);
        Selection.activeGameObject = weapon.gameObject;
        Debug.Log("Shotgun and staring subject installed. Area labels removed. Environment bake retained. Recovery: " + recovery);
    }

    public static int RemoveAreaSigns(Scene scene)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != FacilitySceneGenerator.RootName) continue;
            foreach (Transform area in root.transform)
            {
                if (!FacilitySceneGenerator.IsGeneratedArea(area.name)) continue;
                Transform signage = area.Find("Signage");
                if (signage == null) continue;
                for (int i = signage.childCount - 1; i >= 0; i--)
                {
                    Transform sign = signage.GetChild(i);
                    // This small abandoned-samples notice is environmental storytelling, not an area label.
                    if (sign.name == "Specimen Warning") continue;
                    UnityEngine.Object.DestroyImmediate(sign.gameObject);
                    removed++;
                }
            }
        }
        return removed;
    }
}
