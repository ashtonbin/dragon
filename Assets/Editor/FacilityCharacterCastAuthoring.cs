using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicitly upgrades the live cast and the exact saved prefabs referenced by both game modes.</summary>
public static class FacilityCharacterCastAuthoring
{
    public static string Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Character authoring requires Edit mode outside a lighting bake.");
        string original=SceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        string backup="Assets/_Recovery/Characters_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);AssetDatabase.Refresh();
        var paths=new HashSet<string>();int count=0;
        foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
        {
            Scene scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
            if(!EditorSceneManager.SaveScene(scene,backup+"/"+Path.GetFileName(entry.path),true))throw new IOException("Character recovery save failed.");
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var actor in root.GetComponentsInChildren<FacilityHumanoid>(true)){Apply(actor);count++;}
                foreach(var spawner in root.GetComponentsInChildren<FacilitySpawnController>(true))
                    foreach(var prefab in new[]{spawner.observerPrefab,spawner.zombiePrefab,spawner.eliteSwatPrefab})AddPrefab(paths,prefab);
                foreach(var waves in root.GetComponentsInChildren<FacilityPoliceWaves>(true))foreach(var prefab in new[]{waves.policePrefab,waves.elitePrefab})AddPrefab(paths,prefab);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save upgraded cast.");
        }
        foreach(string path in paths)
        {
            if(!AssetDatabase.CopyAsset(path,backup+"/"+Path.GetFileName(path)))throw new IOException("Could not preserve character prefab "+path);
            GameObject contents=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var actor in contents.GetComponentsInChildren<FacilityHumanoid>(true))Apply(actor);if(PrefabUtility.SaveAsPrefabAsset(contents,path)==null)throw new IOException("Could not save "+path);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        AssetDatabase.SaveAssets();
        if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
        return "Stylized cast saved: "+count+" scene actors and "+paths.Count+" live spawn prefabs; existing anatomy, AI, lighting and navigation retained. "+Validate(SceneManager.GetActiveScene());
    }

    public static void Apply(FacilityHumanoid actor)
    {
        if(actor==null)return;
        FacilityCharacterStyle style;
        if(actor.GetComponent<FacilityEliteSwat>()!=null)
        { FacilityEliteSwatAuthoring.Apply(actor); actor.GetComponent<FacilityEliteSwat>().Configure(); style=FacilityCharacterStyle.EliteSwat; }
        else if(actor.GetComponent<FacilityPoliceOfficer>()!=null){FacilitySwatStyleAuthoring.Apply(actor);style=FacilityCharacterStyle.Swat;}
        else if(actor.GetComponent<FacilityZombie>()!=null){FacilityZombieStyleAuthoring.Apply(actor);style=FacilityCharacterStyle.Infected;}
        else
        {
            bool receptionist=actor.name.IndexOf("Receptionist",StringComparison.OrdinalIgnoreCase)>=0;
            FacilityCharacterStyleAuthoring.Apply(actor,receptionist);
            style=!receptionist && actor.GetComponent<FacilityKnifeGuard>()!=null?FacilityCharacterStyle.Security:FacilityCharacterStyle.Staff;
        }
        if(actor.GetComponent<FacilityZombie>()==null)FacilityBandanaAuthoring.Apply(actor,actor.GetComponent<FacilityPoliceOfficer>()!=null);
        var marker=actor.GetComponent<FacilityCharacterAppearance>();
        if(marker==null)marker=actor.gameObject.AddComponent<FacilityCharacterAppearance>();
        marker.style=style;marker.revision=1;
        marker.appearanceRenderers=actor.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<FacilityHitRegion>(true)!=null).ToArray();
        EditorUtility.SetDirty(actor);EditorUtility.SetDirty(marker);
    }

    public static string Validate(Scene scene)
    {
        int actors=0;var paths=new HashSet<string>();
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var actor in root.GetComponentsInChildren<FacilityHumanoid>(true)){Check(actor);actors++;}
            foreach(var s in root.GetComponentsInChildren<FacilitySpawnController>(true))foreach(var p in new[]{s.observerPrefab,s.zombiePrefab,s.eliteSwatPrefab})AddPrefab(paths,p);
            foreach(var w in root.GetComponentsInChildren<FacilityPoliceWaves>(true))foreach(var prefab in new[]{w.policePrefab,w.elitePrefab})AddPrefab(paths,prefab);
        }
        foreach(string path in paths)Check(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FacilityHumanoid>());
        return "Character validation passed: "+actors+" scene actors and "+paths.Count+" saved prefabs, complete 11-region anatomy, removable armor and persistent materials/meshes.";
    }

    static void Check(FacilityHumanoid actor)
    {
        if(actor==null || actor.head==null || actor.gore==null || actor.hitRegions==null || actor.hitRegions.Length!=11)
            throw new InvalidOperationException("Character anatomy is incomplete.");
        var style=actor.GetComponent<FacilityCharacterAppearance>();
        if(style==null || style.revision<1 || style.appearanceRenderers==null || style.appearanceRenderers.Length<10)
            throw new InvalidOperationException("Missing saved character style: "+actor.name);
        foreach(var region in actor.hitRegions)
        {
            if(region.owner!=actor || region.body==null || region.outerPatches==null || region.outerPatches.Length!=region.woundBeds.Length)
                throw new InvalidOperationException("Broken anatomical damage wiring: "+actor.name+"/"+region.name);
            foreach(var r in region.outerPatches)
                if(r==null || r.GetComponent<MeshFilter>()==null || !EditorUtility.IsPersistent(r.GetComponent<MeshFilter>().sharedMesh))
                    throw new InvalidOperationException("A wound panel has no saved mesh.");
        }
        foreach(var r in style.appearanceRenderers)
        {
            if(r==null)throw new InvalidOperationException("Missing appearance renderer.");
            var f=r.GetComponent<MeshFilter>();
            if(f!=null && !EditorUtility.IsPersistent(f.sharedMesh))throw new InvalidOperationException("Unsaved character mesh "+r.name);
            foreach(var m in r.sharedMaterials)if(m==null || !EditorUtility.IsPersistent(m))throw new InvalidOperationException("Unsaved character material "+r.name);
        }
        if(style.style==FacilityCharacterStyle.Swat && actor.GetComponent<FacilityPoliceOfficer>()==null)
            throw new InvalidOperationException("SWAT must keep its existing police behavior.");
    }

    static void AddPrefab(HashSet<string> paths,GameObject prefab)
    {if(prefab!=null){string path=AssetDatabase.GetAssetPath(prefab);if(!string.IsNullOrEmpty(path))paths.Add(path);}}

    public static void Capture(Scene scene)
    {
        string dir=Path.GetFullPath(".codex/character-previews");Directory.CreateDirectory(dir);
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FacilityHumanoid>(true)).ToArray();
        var sources=new List<GameObject>();
        var receptionist=all.FirstOrDefault(a=>a.name.Contains("Receptionist"));if(receptionist!=null)sources.Add(receptionist.gameObject);
        var guard=all.FirstOrDefault(a=>a.GetComponent<FacilityCharacterAppearance>()?.style==FacilityCharacterStyle.Security);if(guard!=null)sources.Add(guard.gameObject);
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var waves in root.GetComponentsInChildren<FacilityPoliceWaves>(true))
                foreach(var prefab in new[]{waves.policePrefab,waves.elitePrefab})if(prefab!=null && !sources.Contains(prefab))sources.Add(prefab);
            foreach(var spawner in root.GetComponentsInChildren<FacilitySpawnController>(true))if(spawner.zombiePrefab!=null && !sources.Contains(spawner.zombiePrefab))sources.Add(spawner.zombiePrefab);
        }
        var stage=new GameObject("Temporary character style preview");
        var target=new RenderTexture(1400,850,24,RenderTextureFormat.ARGB32);target.Create();var previous=RenderTexture.active;
        try
        {
            stage.transform.position=new Vector3(0,100,0);
            var camera=new GameObject("Style preview camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);
            camera.fieldOfView=35;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.052f,.06f);camera.nearClipPlane=.03f;camera.farClipPlane=15;
            camera.GetUniversalAdditionalCameraData().renderShadows=true;
            var models=new List<FacilityHumanoid>();
            for(int i=0;i<sources.Count;i++)
            {
                var clone=Object.Instantiate(sources[i],stage.transform);clone.name="Preview "+sources[i].name;clone.transform.localPosition=new Vector3((i-(sources.Count-1)*.5f)*.92f,0,0);clone.transform.localRotation=Quaternion.identity;clone.SetActive(true);
                var actor=clone.GetComponent<FacilityHumanoid>();actor.player=null;models.Add(actor);
                foreach(var b in clone.GetComponentsInChildren<SubtleEnemyHealthBar>(true))foreach(var r in b.barRenderers)if(r!=null)r.enabled=false;
                if(actor.contactShadow!=null)actor.contactShadow.gameObject.SetActive(false);
            }
            Key(stage.transform,new Vector3(-2.5f,3f,3f),new Color(.8f,.87f,1f),45f);
            Key(stage.transform,new Vector3(3f,2f,1f),new Color(1f,.71f,.45f),22f);
            Key(stage.transform,new Vector3(0,3f,-2f),new Color(.4f,.65f,.8f),30f);
            camera.transform.localPosition=new Vector3(.7f,1.65f,6.2f);camera.transform.LookAt(stage.transform.position+Vector3.up*.95f);
            Render(camera,target,Path.Combine(dir,"01_stylized_cast.png"));
            for(int i=0;i<models.Count;i++)
            {
                for(int j=0;j<models.Count;j++)models[j].gameObject.SetActive(i==j);
                var actor=models[i];camera.transform.position=actor.transform.position+new Vector3(.55f,1.4f,3.6f);camera.transform.LookAt(actor.transform.position+Vector3.up*.96f);
                Render(camera,target,Path.Combine(dir,"0"+(i+2)+"_"+actor.GetComponent<FacilityCharacterAppearance>().style+".png"));
                if(actor.GetComponent<FacilityZombie>()==null)
                {
                    camera.transform.position=actor.transform.position+new Vector3(.34f,1.64f,1.75f);
                    camera.transform.LookAt(actor.transform.position+Vector3.up*1.43f);
                    Render(camera,target,Path.Combine(dir,"0"+(i+2)+"_"+actor.GetComponent<FacilityCharacterAppearance>().style+"_NeckWrap.png"));
                }
            }
        }
        finally{RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(stage);}
    }

    static void Key(Transform parent,Vector3 p,Color color,float power)
    {var light=new GameObject("Preview softbox").AddComponent<Light>();light.transform.SetParent(parent,false);light.transform.localPosition=p;light.type=LightType.Point;light.range=10;light.intensity=power;light.color=color;light.shadows=LightShadows.None;}
    static void Render(Camera camera,RenderTexture target,string path)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());Object.DestroyImmediate(pixels);
    }
}
