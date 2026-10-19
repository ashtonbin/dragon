using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds a persistent-material variant of the existing subject, retaining every anatomical joint and hit region.</summary>
public static class FacilityZombieAuthoring
{
    private const string MaterialRoot = "Assets/Generated/Combat/Zombie/Materials";
    private const string MeshRoot = "Assets/Generated/Combat/Character/Meshes/";

    public static GameObject Build(FacilityHumanoid authoredObserver, Transform temporaryParent)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author zombie prefabs outside Play mode.");
        if (authoredObserver == null) throw new ArgumentNullException(nameof(authoredObserver));
        EnsureFolder(MaterialRoot);

        GameObject root = UnityEngine.Object.Instantiate(authoredObserver.gameObject, temporaryParent, false);
        root.name = "The Infected - reanimated facility worker";
        root.SetActive(false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        FacilityHumanoid actor = root.GetComponent<FacilityHumanoid>();
        actor.player = null;
        actor.IsHostile = true;
        actor.pursuitSpeed = .95f;
        FacilityZombie zombie = root.GetComponent<FacilityZombie>();
        if (zombie == null) zombie = root.AddComponent<FacilityZombie>();
        zombie.targetHealth = null;
        zombie.walkSpeed = .95f;
        zombie.attackRange = 1.25f;
        zombie.attackInterval = 1.15f;

        Material cloth = MakeMaterial("Water damaged charcoal coverall", new Color(.23f, .285f, .27f));
        Material trim = MakeMaterial("Worn black leather", new Color(.065f, .075f, .067f));
        Material skin = MakeMaterial("Cold ashen skin", new Color(.46f, .51f, .415f));
        Material hair = MakeMaterial("Dusty cropped hair", new Color(.115f, .13f, .11f));
        Material eye = MakeMaterial("Clouded dead eyes", new Color(.59f, .66f, .53f));
        Material shadow = MakeMaterial("Sunken sockets and open mouth", new Color(.035f, .045f, .03f));
        Material driedBlood = MakeMaterial("Old dark blood", new Color(.19f, .028f, .022f));
        Material teeth = MakeMaterial("Dull exposed teeth", new Color(.57f, .56f, .40f));
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                Material replacement = null;
                switch (materials[i].name)
                {
                    case "Coverall - faded sage": replacement = cloth; break;
                    case "Warm skin": replacement = skin; break;
                    case "Boots and utility belt": replacement = trim; break;
                    case "Cropped dark hair": replacement = hair; break;
                    case "Black eyes": replacement = eye; break;
                }
                if (replacement == null) continue;
                materials[i] = replacement;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }

        // Keep the physical head, its rest position and joint anchors intact. Only the face's render geometry slumps.
        if (actor.head != null)
        {
            Transform face = new GameObject("Slumped infected face").transform;
            face.SetParent(actor.head, false);
            for (int i = actor.head.childCount - 1; i >= 0; i--)
            {
                Transform part = actor.head.GetChild(i);
                if (part != face) part.SetParent(face, false);
            }
            face.localPosition = new Vector3(0, -.009f, .008f);
            face.localRotation = Quaternion.Euler(10, 0, -6);
            Transform mouth = face.Find("Quiet mouth");
            if (mouth != null)
            {
                mouth.name = "Slack open mouth";
                mouth.localPosition = new Vector3(0, -.072f, .116f);
                mouth.localScale = new Vector3(.074f, .032f, .014f);
                mouth.GetComponent<Renderer>().sharedMaterial = shadow;
            }
            Mesh rounded = AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot + "Rounded low poly form.asset");
            Mesh chamfered = AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot + "Eight sided chamfered block.asset");
            AddPiece("Sunken left eye socket", face, new Vector3(-.055f, .022f, .113f), new Vector3(.05f, .057f, .009f), rounded, shadow);
            AddPiece("Sunken right eye socket", face, new Vector3(.055f, .022f, .113f), new Vector3(.05f, .057f, .009f), rounded, shadow);
            AddPiece("Slack lower lip", face, new Vector3(0, -.092f, .117f), new Vector3(.069f, .016f, .021f), rounded, skin);
            AddPiece("Upper tooth left", face, new Vector3(-.013f, -.062f, .125f), new Vector3(.012f, .012f, .008f), chamfered, teeth);
            AddPiece("Upper tooth right", face, new Vector3(.009f, -.062f, .125f), new Vector3(.011f, .01f, .008f), chamfered, teeth);
            Transform cut = AddPiece("Old split on left cheek", face, new Vector3(-.095f, -.035f, .084f), new Vector3(.018f, .047f, .009f), chamfered, driedBlood);
            if (cut != null) cut.localRotation = Quaternion.Euler(0, -43, -18);
            Transform hairCap = face.Find("Cropped hair cap");
            if (hairCap != null)
            {
                hairCap.localPosition = new Vector3(0, .111f, -.021f);
                hairCap.localScale = new Vector3(.263f, .105f, .23f);
            }
        }

        // These are actual existing removable wound panels: future pellet damage still opens the same body core.
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            if (region.kind == FacilityBodyRegion.Torso) OpenExistingWound(region, 3, driedBlood);
            else if (region.kind == FacilityBodyRegion.ForearmRight) OpenExistingWound(region, 0, driedBlood);
        }
        foreach (SubtleEnemyHealthBar bar in root.GetComponentsInChildren<SubtleEnemyHealthBar>(true))
        {
            bar.owner = actor;
            bar.player = null;
            foreach (Renderer renderer in bar.barRenderers) if (renderer != null) renderer.enabled = false;
        }
        // Do not leave the source scene's Player collider embedded in a project prefab.
        if (actor.gore != null)
        {
            Collider[] owned = new Collider[actor.bodyColliders.Length + (actor.motor != null ? 1 : 0)];
            Array.Copy(actor.bodyColliders, owned, actor.bodyColliders.Length);
            if (actor.motor != null) owned[owned.Length - 1] = actor.motor;
            actor.gore.ownerColliders = owned;
        }
        FacilityAudioAuthoring.ConfigureActor(actor);
        AssetDatabase.SaveAssets();
        return root;
    }

    private static void OpenExistingWound(FacilityHitRegion region, int index, Material driedBlood)
    {
        if (region.outerPatches == null || region.woundBeds == null || index >= region.outerPatches.Length || index >= region.woundBeds.Length) return;
        if (region.outerPatches[index] != null) region.outerPatches[index].enabled = false;
        if (region.woundBeds[index] != null)
        {
            region.woundBeds[index].SetActive(true);
            Renderer rim = region.woundBeds[index].GetComponent<Renderer>();
            if (rim != null) rim.sharedMaterial = driedBlood;
        }
    }

    private static Transform AddPiece(string name, Transform parent, Vector3 position, Vector3 scale, Mesh mesh, Material material)
    {
        if (mesh == null) return null;
        GameObject piece = new GameObject(name);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return piece.transform;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = MaterialRoot + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", .015f);
        material.SetFloat("_EnvironmentReflections", 0f);
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.SetColor("_EmissionColor", color * .035f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }
}
