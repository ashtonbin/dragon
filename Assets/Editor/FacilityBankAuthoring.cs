using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, recovery-first corrections to the existing bank and interface.</summary>
public static class FacilityBankAuthoring
{
    const string Root = "Assets/Generated/Bank";
    static readonly FacilityAssetLibrary library = new FacilityAssetLibrary();
    static Font Font => AssetDatabase.LoadAssetAtPath<Font>(FacilityHeistInterfaceAuthoring.FontPath);

    [MenuItem("Tools/Facility/Apply Bank Polish")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play mode and baking first.");
        Scene initial = SceneManager.GetActiveScene();
        Recovery(initial);
        if (!EditorSceneManager.SaveScene(initial)) throw new IOException("Saving the working scene failed; stopped.");
        Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
        foreach (string path in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray())
        {
            Scene scene = path == SceneManager.GetActiveScene().path ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(path);
            Recovery(scene);
            FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
            if (session == null) continue;
            ApplyInterface(session);
            foreach (GameObject root in scene.GetRootGameObjects()) foreach (FacilityHumanoid actor in root.GetComponentsInChildren<FacilityHumanoid>(true)) Expression(actor);
            if (session.spawner != null)
                foreach (GameObject prefab in new[] { session.spawner.observerPrefab, session.spawner.zombiePrefab }) PatchPrefab(prefab);
            if (session.storyDirector != null)
            {
                FacilityStoryDirector director = session.storyDirector;
                ApplyExterior(director);
                director.alarmLenses = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name == "Red alarm lens").Cast<Renderer>().ToArray();
                foreach (FacilityDoor door in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FacilityDoor>(true))) ConfigureDoor(door);
                // This employee used to spawn between the watch desk and its chair.
                if (director.guards.Length > 1) director.guards[1].transform.position = new Vector3(-2f, .04f, 11.4f);
                // Keep the locked vault's keeper outside its closed door so the initial alarm can reach them.
                if (director.guards.Length > 4) director.guards[4].transform.position = new Vector3(3.2f, .04f, 27.3f);
                if (director.policeWaves != null) PatchPrefab(director.policeWaves.policePrefab);
                FacilityStoryInstaller.RebuildNavigation(scene);
                EditorUtility.SetDirty(director);
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/StoryLevel01.unity");
        Debug.Log("Bank polish saved. Rebake the active bank scene.");
    }

    static void Recovery(Scene scene)
    {
        string copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeBankPolish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, copy, true)) throw new IOException("Recovery save failed; bank changes stopped.");
    }

    public static void RepairOfficeDoor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play mode and baking first.");
        Scene scene = SceneManager.GetActiveScene(); Recovery(scene);
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        director.guards[1].transform.position = new Vector3(-2f,.04f,11.4f);
        foreach (GameObject root in scene.GetRootGameObjects()) foreach (FacilityDoor door in root.GetComponentsInChildren<FacilityDoor>(true))
        {
            if (door.name != "Watch office door") continue;
            door.transform.SetPositionAndRotation(new Vector3(1,0,11.53f),Quaternion.Euler(0,90,0));
            door.openLocalEuler = new Vector3(0,90,0);
            EditorUtility.SetDirty(door);
        }
        FacilityStoryInstaller.RebuildNavigation(scene); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Office door save failed.");
    }

    public static void ApplyInterface(FacilityGameSession session)
    {
        foreach (Text text in session.canvas.GetComponentsInChildren<Text>(true))
        {
            if (text.gameObject.name == "Game Title") text.text = "rob the bank";
            if (text.gameObject.name == "Subtitle") text.gameObject.SetActive(false);
            if (text.gameObject.name == "Prototype Tag") text.text = "NIGHT DEPOSIT / PROTOTYPE";
            if (text.transform.IsChildOf(session.deathMenu.transform) && text.gameObject.name == "Hint")
            { text.text = "you arent finished yet"; text.color = new Color(.95f, .12f, .10f); }
            EditorUtility.SetDirty(text);
        }
        foreach (string name in new[] { "Health Divider One", "Health Divider Two" })
        { Transform divider = session.hud.transform.Find(name); if (divider != null) Object.DestroyImmediate(divider.gameObject); }
        if (session.healthFill != null)
        { session.healthFill.type = Image.Type.Filled; session.healthFill.fillMethod = Image.FillMethod.Horizontal; session.healthFill.fillOrigin = 0; }
        FacilityStoryDirector director = session.storyDirector;
        if (director != null)
        {
            if (director.heistStatusText != null)
            {
                director.heistStatusPanel = director.heistStatusText.transform.parent.gameObject;
                director.heistStatusText.text = "";
                director.heistStatusPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(386, 66);
                director.heistStatusPanel.SetActive(false);
            }
            Transform parent = director.interactionText.transform.parent;
            Transform warning = parent.Find("Pistol alarm warning");
            if (warning != null) Object.DestroyImmediate(warning.gameObject);
            director.pistolAlarmWarning = null; EditorUtility.SetDirty(director);
        }
        EditorUtility.SetDirty(session);
    }

    public static void Expression(FacilityHumanoid actor)
    {
        if (actor == null || actor.head == null) return;
        FacilityHostileExpression expression = actor.GetComponent<FacilityHostileExpression>();
        if (expression == null) expression = actor.gameObject.AddComponent<FacilityHostileExpression>();
        Material hair = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Combat/Character/Materials/Cropped dark hair.mat");
        expression.actor = actor;
        expression.leftBrow = Box(actor.head, "Left expressive eyebrow", new Vector3(-.055f, .065f, .106f), new Vector3(.065f,.014f,.018f), hair, false, false);
        expression.rightBrow = Box(actor.head, "Right expressive eyebrow", new Vector3(.055f,.065f,.106f), new Vector3(.065f,.014f,.018f), hair, false, false);
        foreach (TextMesh label in actor.GetComponentsInChildren<TextMesh>(true))
        {
            if (label.text != "POLICE") continue;
            label.transform.localRotation = label.gameObject.name.StartsWith("Front", StringComparison.Ordinal) ? Quaternion.Euler(0,180,0) : Quaternion.identity;
            label.GetComponent<MeshRenderer>().sharedMaterial = WorldMaterial("Police depth tested text", Color.white);
            if (label.GetComponent<FacilityWorldText>() == null) label.gameObject.AddComponent<FacilityWorldText>();
        }
        EditorUtility.SetDirty(expression);
    }

    static void PatchPrefab(GameObject prefab)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try { Expression(contents.GetComponent<FacilityHumanoid>()); PrefabUtility.SaveAsPrefabAsset(contents, path); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void ConfigureDoor(FacilityDoor door)
    {
        if (door.leaf == null) return;
        door.npcOpenDistance = 2.8f;
        Bounds bounds = new Bounds(); bool found = false;
        foreach (BoxCollider collider in door.leaf.GetComponentsInChildren<BoxCollider>())
        {
            if (collider.isTrigger) continue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 vertex = collider.center + Vector3.Scale(collider.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = door.leaf.InverseTransformPoint(collider.transform.TransformPoint(vertex));
                if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; } else bounds.Encapsulate(local);
            }
        }
        if (!found) return;
        NavMeshObstacle obstacle = door.leaf.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = door.leaf.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = bounds.center; obstacle.size = bounds.size;
        obstacle.carving = true; obstacle.carveOnlyStationary = false; obstacle.enabled = false;
        EditorUtility.SetDirty(door);
    }

    public static void ApplyExterior(FacilityStoryDirector director)
    {
        Transform level = director.gameObject.scene.GetRootGameObjects().Select(r => r.transform).First(t => t.name == "Story Level 1 - Night Deposit");
        Transform parking = level.Find("01 Night parking - sedan and entrance");
        Material asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Story/Materials/Coarse asphalt.mat");
        Material steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Story/Materials/Black enamel.mat");
        // The old asphalt, pavement and lobby floor occupied the same y=0 plane.
        Box(parking, "Continuous parking surface", new Vector3(0,-.16f,-17.75f), new Vector3(34,.32f,21.5f), asphalt, true, true);
        for (int side = -1; side <= 1; side += 2)
            Box(parking, side < 0 ? "West parking shoulder" : "East parking shoulder", new Vector3(side*12.5f,-.16f,-5.25f), new Vector3(9,.32f,3.5f), asphalt, true, true);
        Transform sign = parking.Find("NATIONAL BANK - roof mounted sign");
        if (sign == null) { sign = new GameObject("NATIONAL BANK - roof mounted sign").transform; sign.SetParent(parking, false); }
        for (int side = -1; side <= 1; side += 2)
        {
            Box(sign, "Roof foot plate " + side, new Vector3(side*1.8f,4.94f,-3.8f), new Vector3(.45f,.12f,.55f), steel, true, true);
            Box(sign, "Steel roof post " + side, new Vector3(side*1.8f,5.53f,-3.8f), new Vector3(.13f,1.12f,.13f), steel, true, true);
        }
        Box(sign, "Enclosed sign cabinet", new Vector3(0,5.97f,-3.92f), new Vector3(5.2f,1.78f,.28f), steel, true, true);
        WorldLabel(sign, "NATIONAL", new Vector3(0,6.43f,-4.071f), .30f, new Color(.15f,1f,.35f));
        WorldLabel(sign, "BANK", new Vector3(0,5.78f,-4.071f), .83f, new Color(.15f,1f,.35f));
        // A saved emissive face supplies the restrained green glow without another realtime light.
        Box(sign, "Green lower sign strip", new Vector3(0,5.14f,-4.069f), new Vector3(4.9f,.035f,.018f), UnlitGreen(), false, false);
    }

    static Material UnlitGreen()
    {
        string path = Root + "/Materials/Green neon trim.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.SetColor("_BaseColor", new Color(.06f,.7f,.17f)); AssetDatabase.CreateAsset(mat,path); }
        return mat;
    }

    static void WorldLabel(Transform parent, string value, Vector3 at, float height, Color tint)
    {
        Transform holder = parent.Find(value);
        if (holder == null) { holder = new GameObject(value, typeof(TextMesh)).transform; holder.SetParent(parent, false); }
        holder.localPosition = at; holder.localRotation = Quaternion.identity;
        TextMesh text = holder.GetComponent<TextMesh>(); text.font = Font; text.fontSize = 64; text.characterSize = 1;
        text.text = value; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = Color.white;
        Font.RequestCharactersInTexture(value,64);
        Renderer renderer = holder.GetComponent<Renderer>(); renderer.sharedMaterial = WorldMaterial("Green bank lettering",tint);
        Bounds bounds = renderer.localBounds; holder.localScale = Vector3.one * (height / Mathf.Max(.01f,bounds.size.y));
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        if (holder.GetComponent<FacilityWorldText>() == null) holder.gameObject.AddComponent<FacilityWorldText>();
    }

    public static Material WorldMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Generated", "Bank");
        if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
        string path = Root + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Facility/Sign Text")) { name = name }; AssetDatabase.CreateAsset(mat,path); }
        mat.SetColor("_BaseColor",color); mat.SetTexture("_MainTex",Font.material.mainTexture); EditorUtility.SetDirty(mat); return mat;
    }

    static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid, bool baked)
    {
        Transform item = parent.Find(name);
        if (item == null) { item = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform; item.SetParent(parent,false); }
        item.localPosition = at; item.localRotation = Quaternion.identity; item.localScale = Vector3.one;
        item.GetComponent<MeshFilter>().sharedMesh = library.BoxMesh(size);
        MeshRenderer renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = baked ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveGI = baked ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
        GameObjectUtility.SetStaticEditorFlags(item.gameObject,baked ? StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic : 0);
        if (solid) { BoxCollider collider = item.GetComponent<BoxCollider>(); if (collider == null) collider = item.gameObject.AddComponent<BoxCollider>(); collider.size = size; }
        return item;
    }
}
