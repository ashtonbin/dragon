using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Explicit sandbox observer upgrade. Original prefab assets and recovery scenes stay intact.</summary>
public static class FacilityObserverLoadoutAuthoring
{
    private const string Root = "Assets/Generated/SandboxObservers";

    public static void Install(FacilityGameSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Observer authoring requires Edit mode.");
        Folder(Root); Folder(Root + "/Meshes"); Folder(Root + "/Materials"); Folder(Root + "/Prefabs");
        Material[] palette = Palette();
        int index = 0;
        foreach (FacilityHumanoid actor in Object.FindObjectsByType<FacilityHumanoid>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (actor.gameObject.scene != session.gameObject.scene || actor.GetComponent<FacilityZombie>() != null) continue;
            Configure(actor, palette, index++);
        }

        FacilitySpawnController spawner = session.spawner != null ? session.spawner : session.GetComponent<FacilitySpawnController>();
        if (spawner != null && spawner.observerPrefab != null)
        {
            // Load a detached prefab stage and save to a unique new asset. Never modify the previous prefab.
            string original = AssetDatabase.GetAssetPath(spawner.observerPrefab);
            if (string.IsNullOrEmpty(original)) throw new InvalidOperationException("Observer spawn source must be a saved prefab.");
            GameObject contents = PrefabUtility.LoadPrefabContents(original);
            try
            {
                FacilityHumanoid actor = contents.GetComponent<FacilityHumanoid>();
                if (actor == null) throw new InvalidOperationException("Observer spawn source has no anatomy.");
                Configure(actor, palette, 0);
                contents.name = "Observer - coloured shirt and knife";
                string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Prefabs/Observer Knife.prefab");
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(contents, path);
                if (prefab == null) throw new InvalidOperationException("Could not save the observer loadout prefab.");
                spawner.observerPrefab = prefab;
                EditorUtility.SetDirty(spawner);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        EditorUtility.SetDirty(session);
        EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    private static void Configure(FacilityHumanoid actor, Material[] palette, int index)
    {
        FacilityObserverOutfit outfit = actor.GetComponent<FacilityObserverOutfit>();
        if (outfit == null) outfit = actor.gameObject.AddComponent<FacilityObserverOutfit>();
        List<Renderer> shirt = new List<Renderer>();
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            if (region == null || (region.kind != FacilityBodyRegion.Torso && region.kind != FacilityBodyRegion.UpperArmLeft &&
                region.kind != FacilityBodyRegion.UpperArmRight && region.kind != FacilityBodyRegion.ForearmLeft && region.kind != FacilityBodyRegion.ForearmRight)) continue;
            foreach (Renderer renderer in region.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null) continue;
                // Preserve cuffs, hair, exposed tissue, inner cavities and all accessories.
                if (material.name.StartsWith("Coverall", StringComparison.Ordinal) || material.name.StartsWith("Observer Shirt", StringComparison.Ordinal))
                    shirt.Add(renderer);
            }
        }
        outfit.shirtPalette = palette; outfit.shirtRenderers = shirt.ToArray();
        outfit.paletteIndex = index; outfit.varyOnSpawn = true; outfit.ApplyPalette(index);
        FacilityKnifeGuard guard = actor.GetComponent<FacilityKnifeGuard>();
        if (guard == null) guard = actor.gameObject.AddComponent<FacilityKnifeGuard>();
        guard.actor = actor; guard.startAlerted = false; guard.walkSpeed = 2.35f;
        guard.attackRange = 1.3f; guard.attackInterval = 1.25f;
        actor.IsHostile = false; actor.pursuitSpeed = guard.walkSpeed;
        FacilityHitRegion hand = null;
        foreach (FacilityHitRegion region in actor.hitRegions)
            if (region != null && region.kind == FacilityBodyRegion.ForearmRight) { hand = region; break; }
        if (hand == null) throw new InvalidOperationException("Observer has no right forearm for his knife.");
        Transform old = hand.transform.Find("Held utility knife");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform knife = new GameObject("Held utility knife").transform;
        knife.SetParent(hand.transform, false);
        knife.localPosition = new Vector3(0f, -.322f, .046f);
        knife.localRotation = Quaternion.Euler(0f, 0f, 0f);
        Material grip = Mat("Observer Knife Dark Grip", new Color(.075f,.065f,.052f), .09f);
        Material steel = Mat("Observer Knife Brushed Steel", new Color(.48f,.49f,.46f), .23f);
        Material edge = Mat("Observer Knife Honed Edge", new Color(.70f,.70f,.64f), .29f);
        Piece(knife,"Shaped knife grip",Box(),grip,new Vector3(0f,0f,-.005f),new Vector3(.026f,.026f,.102f));
        for(int i=0;i<5;i++)
            Piece(knife,"Grip rib "+i,Box(),grip,new Vector3(0f,0f,-.043f+i*.019f),new Vector3(.029f,.029f,.004f));
        Piece(knife,"Steel crossguard",Box(),steel,new Vector3(0f,0f,.050f),new Vector3(.065f,.016f,.009f));
        Piece(knife,"Handle pommel",Box(),steel,new Vector3(0f,0f,-.060f),new Vector3(.030f,.030f,.007f));
        Piece(knife,"Pointed beveled blade",Blade(),steel,Vector3.zero,Vector3.one);
        Piece(knife,"Blade cutting edge",Edge(),edge,Vector3.zero,Vector3.one);
        guard.knifeProp=knife;
        EditorUtility.SetDirty(actor);EditorUtility.SetDirty(outfit);EditorUtility.SetDirty(guard);
    }

    private static Material[] Palette()
    {
        return new[] {
            Mat("Observer Shirt Muted Blue",new Color(.19f,.29f,.38f),.08f),
            Mat("Observer Shirt Dusty Burgundy",new Color(.37f,.17f,.18f),.08f),
            Mat("Observer Shirt Ocher",new Color(.44f,.35f,.18f),.08f),
            Mat("Observer Shirt Slate",new Color(.27f,.29f,.30f),.08f),
            Mat("Observer Shirt Olive",new Color(.30f,.34f,.19f),.08f),
            Mat("Observer Shirt Faded Cream",new Color(.47f,.44f,.35f),.08f)
        };
    }

    private static Material Mat(string name,Color color,float smoothness)
    {
        string path=Root+"/Materials/"+name+".mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            AssetDatabase.CreateAsset(material,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            material=AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smoothness);
        material.SetFloat("_Metallic",name.Contains("Steel")||name.Contains("Edge")?.25f:0f);
        material.SetColor("_EmissionColor",color*.065f);material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;EditorUtility.SetDirty(material);return material;
    }

    private static void Piece(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
    {
        GameObject go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<MeshFilter>().sharedMesh=mesh;
        MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
    }
    private static Mesh Box()
    {
        const string name="Observer knife unit box";Mesh cached=Load(name);if(cached!=null)return cached;
        Builder b=new Builder();Vector3[] v={new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        b.Q(v[0],v[3],v[2],v[1]);b.Q(v[4],v[5],v[6],v[7]);b.Q(v[0],v[4],v[7],v[3]);b.Q(v[1],v[2],v[6],v[5]);b.Q(v[3],v[7],v[6],v[2]);b.Q(v[0],v[1],v[5],v[4]);return Save(name,b.Build(name));
    }
    private static Mesh Blade()
    {
        const string name="Observer utility knife beveled blade";Mesh cached=Load(name);if(cached!=null)return cached;
        Builder b=new Builder();Vector3 l=new Vector3(-.016f,0f,.055f),r=new Vector3(.016f,0f,.055f),tip=new Vector3(0f,0f,.268f),top=new Vector3(0f,.0045f,.080f),bottom=new Vector3(0f,-.0045f,.080f);
        b.T(l,top,tip);b.T(top,r,tip);b.T(l,tip,bottom);b.T(bottom,tip,r);b.T(l,bottom,r);b.T(l,r,top);return Save(name,b.Build(name));
    }
    private static Mesh Edge()
    {
        const string name="Observer utility knife cutting edge";Mesh cached=Load(name);if(cached!=null)return cached;
        Builder b=new Builder();Vector3 a=new Vector3(-.016f,0f,.055f),inner=new Vector3(-.013f,.0015f,.057f),tip=new Vector3(0f,0f,.268f);
        b.T(a,inner,tip);b.T(tip,inner,a);return Save(name,b.Build(name));
    }
    private static Mesh Load(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+name+".asset");
    private static Mesh Save(string name,Mesh mesh){AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+name+".asset");return mesh;}
    private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private sealed class Builder
    {
        private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<int> triangles=new List<int>();
        public void T(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
        public void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d){T(a,b,c);T(a,c,d);}
        public Mesh Build(string name){Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
}
