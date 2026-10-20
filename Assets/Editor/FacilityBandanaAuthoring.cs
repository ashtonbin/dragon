using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Full layered cloth around the neck, with a folded chest drape and existing severed-neck reveal.</summary>
public static class FacilityBandanaAuthoring
{
    const string Root = "Assets/Generated/CharacterStyle/Bandanas";
    const string GroupName = "Bandana - small wrapped neck cloth";

    public static void Apply(FacilityHumanoid actor, bool police)
    {
        if (actor == null || actor.chest == null || actor.head == null) return;
        var head = actor.head.GetComponent<FacilityHitRegion>();
        if (head == null) throw new InvalidOperationException("Bandanas require the original head damage region.");
        Transform existing = actor.chest.Find(GroupName);
        if (existing != null)
        {
            head.cutCovers = (head.cutCovers ?? Array.Empty<GameObject>()).Where(c => c != null && c != existing.gameObject).ToArray();
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }
        Folder(Root + "/Meshes"); Folder(Root + "/Materials");
        var group = new GameObject(GroupName).transform;
        group.SetParent(actor.chest, false);
        group.localPosition = new Vector3(0f, .30f, 0f);
        var filter = group.gameObject.AddComponent<MeshFilter>();
        var renderer = group.gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = ClothMesh(police,false);
        renderer.sharedMaterials = new[]{ClothMaterial(police),ClothMaterial(police,true)};
        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var drape=new GameObject("Folded triangular chest drape",typeof(MeshFilter),typeof(MeshRenderer));
        drape.transform.SetParent(group,false);
        drape.GetComponent<MeshFilter>().sharedMesh=ClothMesh(police,true);
        var drapeRenderer=drape.GetComponent<MeshRenderer>();drapeRenderer.sharedMaterials=renderer.sharedMaterials;
        drapeRenderer.shadowCastingMode=ShadowCastingMode.On;drapeRenderer.receiveShadows=true;
        var damage=drape.AddComponent<FacilityBandanaDamage>();damage.drape=drapeRenderer;
        var torso=actor.chest.GetComponent<FacilityHitRegion>();
        damage.coveredPanels=torso.outerPatches.Where(p=>UpperFrontPanel(p,actor.chest)).ToArray();
        var covers = new List<GameObject>(head.cutCovers ?? Array.Empty<GameObject>());
        covers.RemoveAll(c => c == null); covers.Add(group.gameObject); head.cutCovers = covers.ToArray();
        EditorUtility.SetDirty(head); EditorUtility.SetDirty(actor);
    }

    static Material ClothMaterial(bool police,bool fold=false)
    {
        string name = police ? "Small black neck cloth" : "Small muted red neck cloth";
        if(fold)name+=" - raised folds";
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true }; AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", police ? (fold?new Color(.075f,.081f,.084f):new Color(.043f,.048f,.051f)) :
            (fold?new Color(.36f,.066f,.071f):new Color(.27f,.038f,.044f)));
        material.SetFloat("_Smoothness", .025f); material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Cull",0f);
        material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material); return material;
    }

    static Mesh ClothMesh(bool police,bool drape)
    {
        string path = Root + "/Meshes/"+(police?"Charcoal armored":"Red observer")+(drape?" triangular chest drape":" layered neck collar")+".asset";
        var vertices = new List<Vector3>(); var triangles = new[]{new List<int>(),new List<int>()};
        const int sides = 20;
        // Broad at the shoulders, tucked beneath the jaw. Alternating ridges and recesses
        // give the wrap thickness and the overlapping folds of gathered cloth.
        float[] height={-.062f,-.046f,-.024f,-.010f,.011f,.024f,.047f,.069f,.077f};
        float[] radiusX={.143f,.155f,.148f,.157f,.142f,.149f,.132f,.127f,.111f};
        float[] radiusZ={.124f,.137f,.128f,.140f,.122f,.132f,.113f,.110f,.094f};
        if(!drape)for(int level=0;level<height.Length-1;level++)for(int i=0;i<sides;i++)
        {
            float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 a0=FoldRing(a,height[level],radiusX[level],radiusZ[level]);
            Vector3 b0=FoldRing(b,height[level],radiusX[level],radiusZ[level]);
            Vector3 a1=FoldRing(a,height[level+1],radiusX[level+1],radiusZ[level+1]);
            Vector3 b1=FoldRing(b,height[level+1],radiusX[level+1],radiusZ[level+1]);
            Quad(vertices,triangles[level==1||level==4?1:0],a0,b0,b1,a1);
        }
        if(drape)
        {
        // Front triangle drapes over the vest instead of exposing a narrow neck cylinder.
        // The armored variant sits farther forward to follow the plate carrier's surface.
        float front=police?.235f:.190f;
        Vector3 left=new Vector3(-.145f,-.018f,.125f),right=new Vector3(.145f,-.018f,.125f);
        Vector3 upper=new Vector3(-.012f,-.039f,.171f);
        Vector3 ml=new Vector3(-.102f,-.093f,front-.020f),mr=new Vector3(.103f,-.093f,front-.025f);
        Vector3 ridge=new Vector3(.006f,-.089f,front+.008f),tip=new Vector3(.009f,-.178f,front-.002f);
        Quad(vertices,triangles[0],left,ml,ridge,upper);
        Quad(vertices,triangles[0],upper,ridge,mr,right);
        Tri(vertices,triangles[0],ml,tip,ridge);Tri(vertices,triangles[1],ridge,tip,mr);
        // A second diagonal cloth fold crosses the triangle, with a restrained folded hem.
        Quad(vertices,triangles[1],new Vector3(-.128f,-.044f,.158f),new Vector3(-.093f,-.102f,front-.009f),
            new Vector3(.088f,-.083f,front-.011f),new Vector3(.129f,-.033f,.153f));
        Tri(vertices,triangles[0],new Vector3(-.103f,-.094f,front-.017f),new Vector3(.009f,-.187f,front-.001f),tip);
        Tri(vertices,triangles[1],tip,new Vector3(.009f,-.187f,front-.001f),new Vector3(.103f,-.094f,front-.022f));
        }
        var generated=new Mesh{name=(police?"Charcoal":"Red")+" full layered neck wrap"};
        generated.SetVertices(vertices);generated.subMeshCount=2;
        generated.SetTriangles(triangles[0],0);generated.SetTriangles(triangles[1],1);
        generated.RecalculateNormals();generated.RecalculateBounds();
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){AssetDatabase.CreateAsset(generated,path);mesh=generated;}
        else{FacilityCharacterStyleAuthoring.CopyMesh(mesh,generated);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(mesh);}
        AssetDatabase.SaveAssetIfDirty(mesh);return mesh;
    }
    static Vector3 FoldRing(float a,float y,float rx,float rz)
    {
        float fold=1f+Mathf.Sin(a*3f+.8f)*.024f;
        return new Vector3(Mathf.Sin(a)*rx*fold,y-.013f*Mathf.Max(0f,Mathf.Cos(a))-.006f*Mathf.Sin(a),Mathf.Cos(a)*rz*fold);
    }
    static bool UpperFrontPanel(Renderer panel,Transform chest)
    {
        if(panel==null)return false;
        var filter=panel.GetComponent<MeshFilter>();if(filter==null || filter.sharedMesh==null)return false;
        Vector3 center=chest.InverseTransformPoint(panel.transform.TransformPoint(filter.sharedMesh.bounds.center));
        return center.y>.08f && center.z>.10f;
    }
    static void Tri(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c)
    {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.AddRange(new[]{n,n+1,n+2});}
    static void Quad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
}
