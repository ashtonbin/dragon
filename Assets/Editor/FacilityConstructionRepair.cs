using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>An explicit repair of the generated construction, with a recovery scene first.</summary>
public static class FacilityConstructionRepair
{
    [MenuItem("Tools/Facility/Repair Construction and Lighting")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Leave Play mode and finish baking before repairing construction.");
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = null;
        foreach (GameObject candidate in scene.GetRootGameObjects())
            if (candidate.name == FacilitySceneGenerator.RootName && candidate.activeSelf) root = candidate;
        if (root == null) throw new InvalidOperationException("Open the exploration scene first.");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ConstructionRecovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, recovery, true))
            throw new IOException("Recovery save failed. No construction was changed.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair facility construction");
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.transform.GetChild(i).gameObject;
            if (FacilitySceneGenerator.IsGeneratedArea(child.name) || child.name == "Lighting and Sound")
                Undo.DestroyObjectImmediate(child);
        }
        var assets = new FacilityAssetLibrary();
        var layout = new FacilityLayoutBuilder(root.transform, assets);
        layout.Build();
        FacilitySceneGenerator.ConfigureLightingAndAudio(root.transform, layout, assets);
        FacilitySceneGenerator.ConfigureSignFont(root, assets);
        // Tiny trim does not need to occlude the bake or occupy separate lightmap charts.
        FacilitySceneGenerator.ConfigureBakeParticipation(root);
        foreach (Transform child in root.transform)
            if (FacilitySceneGenerator.IsGeneratedArea(child.name) || child.name == "Lighting and Sound")
                Undo.RegisterCreatedObjectUndo(child.gameObject, "Repair facility area");
        Undo.CollapseUndoOperations(group);
        assets.ApplyPolishSettings();
        // Old indices and shadows refer to the old geometry and must never survive this repair.
        Lightmapping.Clear();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired construction. Recovery: " + recovery);
        Debug.Log("Construction repaired. Recovery: " + recovery + ". Bake lighting before judging the new illumination.");
    }

    public static void FinishNonBakedTrim(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish trim outside Play mode and baking.");
        FacilityAmbientEffects effects = FacilitySceneGenerator.FindInScene<FacilityAmbientEffects>(scene);
        if (effects == null) throw new InvalidOperationException("No facility found.");
        var assets = new FacilityAssetLibrary();
        foreach (MeshFilter part in effects.transform.root.GetComponentsInChildren<MeshFilter>())
        {
            bool plate = part.name.Contains("Guard Base Plate");
            bool rampPost = part.name.StartsWith("Ramp ") && part.name.Contains("Guard Post");
            if (!plate && !rampPost) continue;
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer.shadowCastingMode != ShadowCastingMode.Off ||
                (GameObjectUtility.GetStaticEditorFlags(part.gameObject) & StaticEditorFlags.ContributeGI) != 0)
                throw new InvalidOperationException("Trim unexpectedly contributes to the bake: " + part.name);
            Vector3 size = part.sharedMesh.bounds.size;
            if (plate) size.z = part.name.StartsWith("Stair ") ? .10f : .14f;
            else if (size.y < 1.32f)
            {
                size.y += .025f;
                part.transform.localPosition -= Vector3.up * .0125f;
            }
            part.sharedMesh = assets.BoxMesh(size);
            EditorUtility.SetDirty(part);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Trim save failed.");
    }
}
