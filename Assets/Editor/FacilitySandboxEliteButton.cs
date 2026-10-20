using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FacilitySandboxEliteButton
{
    public static string Install()
    {
        EditorSceneManager.SaveOpenScenes();
        string original=SceneManager.GetActiveScene().path;
        var story=EditorSceneManager.OpenScene("Assets/Scenes/StoryLevel01.unity",OpenSceneMode.Single);
        var waves=FacilitySceneGenerator.FindInScene<FacilityPoliceWaves>(story);
        GameObject prefab=waves.elitePrefab;
        Mesh round=waves.projectiles.projectileMesh;
        Material material=waves.projectiles.projectileMaterial;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/FacilityPrototype.unity",OpenSceneMode.Single);
        var session=FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
        session.spawner.eliteSwatPrefab=prefab;
        var projectiles=session.GetComponent<FacilityPoliceProjectiles>();
        if(projectiles==null)projectiles=session.gameObject.AddComponent<FacilityPoliceProjectiles>();
        projectiles.projectileMesh=round;projectiles.projectileMaterial=material;
        session.spawner.policeProjectiles=projectiles;
        if(session.eliteSwatSpawnButton==null)
        {
            var go=new GameObject("Spawn Elite SWAT",typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(session.observerSpawnButton.transform.parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(932,-180);rect.sizeDelta=new Vector2(240,44);
            var image=go.GetComponent<Image>();image.color=new Color(.20f,.22f,.25f);
            var button=go.GetComponent<Button>();button.targetGraphic=image;
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);
            var text=label.GetComponent<Text>();text.font=session.observerSpawnButton.GetComponentInChildren<Text>(true).font;
            text.text="Spawn Elite SWAT";text.fontSize=16;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
            var labelRect=label.GetComponent<RectTransform>();labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            session.eliteSwatSpawnButton=button;
        }
        if(prefab==null || prefab.GetComponent<FacilityEliteSwat>()==null || round==null || material==null)
            throw new InvalidOperationException("Sandbox elite equipment is missing.");
        EditorUtility.SetDirty(session);EditorUtility.SetDirty(session.spawner);EditorUtility.SetDirty(projectiles);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(!string.IsNullOrEmpty(original) && original!=scene.path)EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
        return "Saved simple sandbox Spawn Elite SWAT button, existing elite prefab and working gun projectile pool.";
    }
}
