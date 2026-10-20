using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Chunky, deliberately simple faces and clothing built on the existing removable anatomy.</summary>
public static class FacilityCharacterStyleAuthoring
{
    const string Root = "Assets/Generated/CharacterStyle";
    const string GroupName = "Character style - ";
    static Mesh block, round;
    static Material dark, shadow, skin, cheek, metal, vest, lapel, tie;

    public static void ClearStaff(FacilityHumanoid actor)
    {
        if (actor == null) return;
        var appearance = actor.GetComponent<FacilityCharacterAppearance>();
        if (appearance != null && appearance.basePanels != null)
            foreach (var source in appearance.basePanels)
            {
                if (source == null || source.renderer == null) continue;
                var filter = source.renderer.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = source.mesh;
                source.renderer.sharedMaterials = source.materials;
            }
        foreach (Transform child in actor.GetComponentsInChildren<Transform>(true).Reverse())
            if (child != null && child.name.StartsWith(GroupName, StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        if (appearance != null) appearance.basePanels = null;
    }

    public static void Apply(FacilityHumanoid actor, bool receptionist = false)
    {
        if (actor == null || actor.head == null) return;
        ClearStaff(actor); Prepare();
        bool security = !receptionist && actor.GetComponent<FacilityKnifeGuard>() != null;
        var appearance = actor.GetComponent<FacilityCharacterAppearance>();
        if (appearance == null) appearance = actor.gameObject.AddComponent<FacilityCharacterAppearance>();
        appearance.style = security ? FacilityCharacterStyle.Security : FacilityCharacterStyle.Staff;
        appearance.revision = 1;
        var sources = new List<FacilityCharacterPanelSource>();
        Transform face = Group(actor.head, "stern face");
        Reposition(actor.head, "Left black eye", new Vector3(-.055f,.024f,.125f), new Vector3(.034f,.023f,.014f));
        Reposition(actor.head, "Right black eye", new Vector3(.055f,.024f,.125f), new Vector3(.034f,.023f,.014f));
        Reposition(actor.head, "Nose", new Vector3(0,-.016f,.132f), new Vector3(.047f,.055f,.049f));
        Reposition(actor.head, "Quiet mouth", new Vector3(0,-.081f,.116f), new Vector3(.069f,.009f,.009f));
        // Large planes and tiny eyes retain the game's simple, toy-like vocabulary.
        Piece(face,"Heavy left brow ridge",new Vector3(-.057f,.049f,.116f),new Vector3(.087f,.031f,.031f),skin,block, new Vector3(0,0,-12));
        Piece(face,"Heavy right brow ridge",new Vector3(.057f,.049f,.116f),new Vector3(.087f,.031f,.031f),skin,block,new Vector3(0,0,12));
        Piece(face,"Left under-eye plane",new Vector3(-.064f,.004f,.120f),new Vector3(.052f,.012f,.012f),cheek,block,new Vector3(0,0,-7));
        Piece(face,"Right under-eye plane",new Vector3(.064f,.004f,.120f),new Vector3(.052f,.012f,.012f),cheek,block,new Vector3(0,0,7));
        Piece(face,"Left angular cheek",new Vector3(-.087f,-.038f,.095f),new Vector3(.064f,.048f,.038f),skin,block,new Vector3(0,-24,-12));
        Piece(face,"Right angular cheek",new Vector3(.087f,-.038f,.095f),new Vector3(.064f,.048f,.038f),skin,block,new Vector3(0,24,12));
        Piece(face,"Broad squared chin",new Vector3(0,-.108f,.086f),new Vector3(.12f,.047f,.055f),skin,block);
        Piece(face,"Lower lip plane",new Vector3(0,-.091f,.115f),new Vector3(.066f,.012f,.013f),cheek,block);
        Piece(face,"Left mouth corner",new Vector3(-.036f,-.087f,.116f),new Vector3(.009f,.018f,.010f),dark,block,new Vector3(0,0,-24));
        Piece(face,"Right mouth corner",new Vector3(.036f,-.087f,.116f),new Vector3(.009f,.018f,.010f),dark,block,new Vector3(0,0,24));
        if (security)
        {
            Piece(face,"Cropped left sideburn",new Vector3(-.119f,.031f,.030f),new Vector3(.018f,.074f,.058f),dark,block);
            Piece(face,"Cropped right sideburn",new Vector3(.119f,.031f,.030f),new Vector3(.018f,.074f,.058f),dark,block);
            Piece(face,"Blunt goatee",new Vector3(0,-.112f,.117f),new Vector3(.069f,.026f,.008f),dark,block);
        }
        else
        {
            Piece(face,"Swept hair left",new Vector3(-.060f,.116f,.063f),new Vector3(.15f,.047f,.12f),dark,block,new Vector3(0,-12,-9));
            Piece(face,"Swept hair right",new Vector3(.063f,.126f,.035f),new Vector3(.10f,.04f,.13f),dark,block,new Vector3(0,15,8));
        }
        var expression=actor.GetComponent<FacilityHostileExpression>();
        if(expression!=null)
        {
            expression.restingTilt=12f;
            foreach(Transform brow in new[]{expression.leftBrow,expression.rightBrow})
                if(brow!=null)
                {
                    // The legacy brows use sized meshes, while this style uses shared unit forms.
                    brow.GetComponent<MeshFilter>().sharedMesh=block;
                    brow.localScale=new Vector3(.073f,.017f,.020f);
                    brow.localPosition=new Vector3(brow==expression.leftBrow?-.055f:.055f,.062f,.137f);
                }
            if(expression.leftBrow!=null)expression.leftBrow.localRotation=Quaternion.Euler(0,0,-12);
            if(expression.rightBrow!=null)expression.rightBrow.localRotation=Quaternion.Euler(0,0,12);
            EditorUtility.SetDirty(expression);
        }

        // Clothes/accessories are fused to the original wound panels, never placed over the tissue core.
        foreach(var region in actor.hitRegions)
        {
            if(region==null || region.kind!=FacilityBodyRegion.Torso || region.outerPatches==null)continue;
            for(int i=0;i<region.outerPatches.Length;i++)
            {
                Renderer panel=region.outerPatches[i];
                if(panel==null || i>=12)continue;
                var filter=panel.GetComponent<MeshFilter>();if(filter==null)continue;
                sources.Add(new FacilityCharacterPanelSource{renderer=panel,mesh=filter.sharedMesh,materials=panel.sharedMaterials});
                var kit=new List<Detail>();
                float sign=i<6?1:-1;
                Vector3 p=panel.transform.localPosition;
                if(receptionist)
                {
                    kit.Add(new Detail(new Vector3(p.x,p.y,sign*.149f),new Vector3(.139f,.225f,.026f),vest));
                    if(i<6 && i%3!=1)
                        kit.Add(new Detail(new Vector3(p.x*.64f,p.y+.017f,.169f),new Vector3(.038f,.21f,.018f),lapel, new Vector3(0,0,i%3==0?-15:15)));
                    if(i<6 && i%3==1)
                    {
                        kit.Add(new Detail(new Vector3(0,p.y,.172f),new Vector3(.027f,.216f,.014f),tie));
                        kit.Add(new Detail(new Vector3(.037f,p.y-.025f,.176f),new Vector3(.009f,.009f,.007f),metal));
                    }
                    if(i==2)kit.Add(new Detail(new Vector3(.143f,.113f,.178f),new Vector3(.073f,.031f,.009f),metal));
                }
                else if(security)
                {
                    // Lighter guard harness keeps them distinct from the armored response officers.
                    if(i%3!=1)
                        kit.Add(new Detail(new Vector3(p.x,p.y,sign*.153f),new Vector3(.088f,.231f,.033f),vest));
                    if(i<3 && i%3!=1)
                    {
                        kit.Add(new Detail(new Vector3(p.x,-.123f,.175f),new Vector3(.104f,.112f,.052f),vest));
                        kit.Add(new Detail(new Vector3(p.x,-.102f,.204f),new Vector3(.098f,.018f,.012f),lapel));
                    }
                    if(i==3)kit.Add(new Detail(new Vector3(-.144f,.120f,.185f),new Vector3(.045f,.063f,.017f),metal));
                    if(i==5)kit.Add(new Detail(new Vector3(.146f,.129f,.181f),new Vector3(.063f,.080f,.033f),dark));
                }
                else
                {
                    if(i==3||i==5)kit.Add(new Detail(new Vector3(p.x,.121f,.153f),new Vector3(.10f,.10f,.028f),panel.sharedMaterial));
                }
                if(kit.Count>0)Composite(panel,filter,kit,(receptionist?"Reception":security?"Guard":"Worker")+" panel "+i);
            }
        }
        appearance.basePanels=sources.ToArray();
        appearance.appearanceRenderers=actor.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<FacilityHitRegion>(true)!=null).ToArray();
        EditorUtility.SetDirty(appearance);EditorUtility.SetDirty(actor);
    }

    static void Composite(Renderer renderer,MeshFilter filter,List<Detail> details,string name)
    {
        var combines=new List<CombineInstance>();var materials=new List<Material>();
        for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
        { combines.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=Matrix4x4.identity});materials.Add(renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)]); }
        Matrix4x4 inverse=Matrix4x4.TRS(renderer.transform.localPosition,renderer.transform.localRotation,renderer.transform.localScale).inverse;
        foreach(var d in details)
        {combines.Add(new CombineInstance{mesh=block,transform=inverse*Matrix4x4.TRS(d.position,Quaternion.Euler(d.rotation),d.size)});materials.Add(d.material);}
        var mesh=new Mesh{name=name};mesh.CombineMeshes(combines.ToArray(),false,true);mesh.RecalculateBounds();
        string path=Root+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{CopyMesh(saved,mesh);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
        filter.sharedMesh=saved;renderer.sharedMaterials=materials.ToArray();
    }

    /// <summary>Updates both serialized geometry and the live GPU buffers used by editor previews.</summary>
    public static void CopyMesh(Mesh destination, Mesh source)
    {
        // CopySerialized alone leaves an already-rendered Mesh's native vertex buffer stale.
        destination.Clear();destination.indexFormat=source.indexFormat;destination.name=source.name;
        destination.vertices=source.vertices;destination.normals=source.normals;
        destination.uv=source.uv;destination.tangents=source.tangents;
        destination.subMeshCount=source.subMeshCount;
        for(int i=0;i<source.subMeshCount;i++)destination.SetIndices(source.GetIndices(i),source.GetTopology(i),i,false);
        destination.bounds=source.bounds;destination.UploadMeshData(false);
    }

    static void Prepare()
    {
        Folder(Root+"/Meshes");Folder(Root+"/Materials");
        block=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/Combat/Character/Meshes/Eight sided chamfered block.asset");
        round=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/Combat/Character/Meshes/Rounded low poly form.asset");
        if(block==null||round==null)throw new InvalidOperationException("The original stylized anatomy meshes are required.");
        dark=Mat("Style charcoal hair",new Color(.055f,.047f,.043f));
        shadow=Mat("Style facial shadow",new Color(.16f,.103f,.078f));
        skin=AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Combat/Character/Materials/Warm skin.mat");
        cheek=Mat("Style muted facial planes",new Color(.49f,.315f,.24f));
        metal=Mat("Style dull brass",new Color(.47f,.42f,.27f));
        vest=Mat("Style charcoal work vest",new Color(.105f,.118f,.112f));
        lapel=Mat("Style raised cloth edges",new Color(.20f,.225f,.207f));
        tie=Mat("Style oxblood tie",new Color(.255f,.07f,.075f));
    }

    static void Reposition(Transform parent,string name,Vector3 p,Vector3 s)
    {Transform item=parent.Find(name);if(item==null)return;item.localPosition=p;item.localScale=s;}
    static Transform Group(Transform parent,string role)
    {var group=new GameObject(GroupName+role).transform;group.SetParent(parent,false);return group;}
    static Transform Piece(Transform parent,string name,Vector3 position,Vector3 size,Material material,Mesh mesh,Vector3 euler=default)
    {
        var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);
        t.localPosition=position;t.localRotation=Quaternion.Euler(euler);t.localScale=size;
        t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;
        r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;return t;
    }
    static Material Mat(string name,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.035f);m.SetFloat("_Metallic",0);
        m.SetColor("_EmissionColor",color*.035f);m.EnableKeyword("_EMISSION");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;EditorUtility.SetDirty(m);return m;
    }
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    struct Detail
    {
        public Vector3 position,size,rotation;public Material material;
        public Detail(Vector3 position,Vector3 size,Material material,Vector3 rotation=default){this.position=position;this.size=size;this.material=material;this.rotation=rotation;}
    }
}
