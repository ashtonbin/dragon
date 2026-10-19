using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Explicit correction of the existing reception desk; never runs during gameplay.</summary>
public static class FacilityReceptionAuthoring
{
    const string DeskName = "Reception counter - open central knee space";
    const string PanelName = "Visitor-facing walnut modesty panel";

    [MenuItem("Tools/Facility/Repair Reception Desk")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Repair reception outside Play mode and baking.");
        Scene initial = SceneManager.GetActiveScene();
        if (!initial.IsValid() || !initial.isLoaded) throw new InvalidOperationException("Open the working scene first.");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Recovery"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Recovery");
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeReception_" + stamp + ".unity");
        if (!EditorSceneManager.SaveScene(initial, recovery, true))
            throw new IOException("Reception recovery save failed; no objects were changed.");
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(initial);
        if (director == null)
        {
            FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(initial);
            if (session == null || session.storySceneNames == null || session.storySceneNames.Length == 0)
                throw new InvalidOperationException("The working scene has no authored Story entry.");
            if (!EditorSceneManager.SaveScene(initial)) throw new IOException("Working scene save failed; repair stopped.");
            Scene story = EditorSceneManager.OpenScene("Assets/Scenes/" + session.storySceneNames[0] + ".unity", OpenSceneMode.Single);
            recovery = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/StoryBeforeReception_" + stamp + ".unity");
            if (!EditorSceneManager.SaveScene(story, recovery, true)) throw new IOException("Story recovery save failed; repair stopped.");
            director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(story);
        }
        if (director == null) throw new InvalidOperationException("The authored reception is missing.");
        Transform desk = director.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == DeskName);
        if (desk == null) desk = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == director.gameObject.scene && t.name == DeskName);
        if (desk == null || desk.name != DeskName) throw new InvalidOperationException("The pistol is no longer beneath the named reception desk; inspect it manually.");
        ConfigureDesk(desk, director);
        FacilityStoryInstaller.RebuildNavigation(director.gameObject.scene);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        if (!EditorSceneManager.SaveScene(director.gameObject.scene)) throw new IOException("Reception save failed. Recovery: " + recovery);
        Debug.Log("Reception repaired: staff-facing drawers and solid walnut counter. Recovery: " + recovery);
    }

    /// <summary>Shared by new-level authoring and the explicit repair, preserving all unrelated objects.</summary>
    public static void ConfigureDesk(Transform desk, FacilityStoryDirector director)
    {
        var library = new FacilityAssetLibrary();
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Story/Materials/Reception walnut.mat");
        if (wood == null) throw new InvalidOperationException("The persistent reception walnut material is missing.");
        foreach (Transform item in desk.Cast<Transform>())
        {
            Vector3 at = item.localPosition;
            if (item.name == "Cabinet drawer face") { at.z = 3.9375f; item.localPosition = at; }
            if (item.name == "Drawer pull") { at.z = 3.9675f; item.localPosition = at; }
            if (item.name == "Undershelf small lamp")
            { at.y = .9325f; at.z = 3.70f; item.localPosition = at; }
            if (item.name == "Abandoned CRT terminal")
            {
                // The screen/keyboard face the employee (+Z); the case back faces the lobby.
                item.localPosition = new Vector3(-1.38f, 1.07f, 3.48f);
                item.localRotation = Quaternion.Euler(0, 180, 0);
            }
        }
        Box(desk, PanelName, new Vector3(0, .50f, 3.28f), new Vector3(4.24f, .88f, .10f), wood, library);
        Box(desk, "Walnut front panel floor plinth", new Vector3(0, .035f, 3.28f), new Vector3(4.24f, .07f, .14f), wood, library);
        // Span into both cabinet carcasses, with the front edge against the panel.
        Box(desk, "Under-counter pistol shelf", new Vector3(0, .41f, 3.62f), new Vector3(2.60f, .065f, .58f), wood, library);

        FacilityStoryInteractable pickup = director.pistolPickup;
        if (pickup == null || pickup.visualRoot == null) return;
        Transform pistol = pickup.visualRoot.transform;
        pistol.localPosition = new Vector3(0, .49f, 3.72f);
        pistol.localRotation = Quaternion.Euler(0, 65, 90);
        // Rest the actual lowest vertex on the shelf, including the grip and sights.
        float lowest = float.PositiveInfinity;
        Bounds bounds = new Bounds(); bool hasBounds = false;
        foreach (MeshFilter filter in pistol.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = desk.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                lowest = Mathf.Min(lowest, point.y);
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!hasBounds) throw new InvalidOperationException("The reception pistol has no mesh vertices.");
        float lift = .444f - lowest;
        pistol.localPosition += Vector3.up * lift;
        Vector3 aim = bounds.center + Vector3.up * lift;
        // A snug target sits entirely behind the wooden front; it cannot extend through the panel.
        pickup.transform.localPosition = aim;
        BoxCollider trigger = pickup.GetComponent<BoxCollider>();
        if (trigger != null) { trigger.center = Vector3.zero; trigger.size = new Vector3(.48f, .16f, .26f); trigger.isTrigger = true; }
        director.pistolAccessPlane = Marker(desk, "Pistol access plane - employee side faces north", new Vector3(0, 0, 3.6f));
        director.pistolAccessMinDistance = .65f;
        director.pistolVisibilityPoint = Marker(pickup.transform, "Pistol visibility point", Vector3.zero);
        EditorUtility.SetDirty(pickup); EditorUtility.SetDirty(director);
        Physics.SyncTransforms();
    }

    static Transform Marker(Transform parent, string name, Vector3 position)
    {
        Transform item = parent.Find(name);
        if (item == null) { item = new GameObject(name).transform; item.SetParent(parent, false); }
        item.localPosition = position; item.localRotation = Quaternion.identity; item.localScale = Vector3.one;
        return item;
    }

    static void Box(Transform desk, string name, Vector3 position, Vector3 size, Material material, FacilityAssetLibrary library)
    {
        Transform item = Marker(desk, name, position);
        MeshFilter filter = item.GetComponent<MeshFilter>(); if (filter == null) filter = item.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = library.BoxMesh(size);
        MeshRenderer renderer = item.GetComponent<MeshRenderer>(); if (renderer == null) renderer = item.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true; renderer.receiveGI = ReceiveGI.Lightmaps; renderer.lightProbeUsage = LightProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(item.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        BoxCollider collider = item.GetComponent<BoxCollider>(); if (collider == null) collider = item.gameObject.AddComponent<BoxCollider>();
        collider.center = Vector3.zero; collider.size = size; collider.isTrigger = false;
    }
}
