using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Authored heavy assault variant. Every armor layer follows the existing eleven-region anatomy.</summary>
public static class FacilityEliteSwatAuthoring
{
    public const string Root = "Assets/Generated/EliteSwat";
    public const string HeadGroup = "Elite SWAT - heavy segmented helmet";
    public const string ArmorPrefix = "Elite removable armor ";
    static Mesh block, round;
    static Material[] palette;
    static Material wound, bone, nightLens;

    public static GameObject BuildPrefab(GameObject policePrefab)
    {
        if (policePrefab == null || policePrefab.GetComponent<FacilityPoliceOfficer>() == null)
            throw new ArgumentException("The elite squad must inherit the actual saved police prefab.");
        if (Application.isPlaying) throw new InvalidOperationException("Author the elite prefab outside Play mode.");
        Prepare();
        GameObject clone = Object.Instantiate(policePrefab);
        try
        {
            clone.name = "Elite armored assault officer";
            clone.SetActive(false);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = Vector3.one;
            FacilityHumanoid actor = clone.GetComponent<FacilityHumanoid>();
            Apply(actor);
            clone.GetComponent<FacilityEliteSwat>().Configure();
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Prefabs/Elite Armored Assault Officer.prefab");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(clone, path);
            if (saved == null) throw new IOException("Could not save the elite assault officer.");
            AssetDatabase.SaveAssets();
            return saved;
        }
        finally { Object.DestroyImmediate(clone); }
    }

    /// <summary>Idempotently resets the regular SWAT baseline before adding only saved visual geometry.</summary>
    public static void Apply(FacilityHumanoid actor)
    {
        if (actor == null || actor.head == null || actor.hitRegions == null || actor.hitRegions.Length != 11)
            throw new ArgumentException("Elite armor requires all eleven existing damage regions.");
        if (Application.isPlaying) throw new InvalidOperationException("Author elite armor outside Play mode.");
        foreach (Transform t in actor.GetComponentsInChildren<Transform>(true).Reverse())
            if (t != null && t.name == HeadGroup) Object.DestroyImmediate(t.gameObject);
        FacilitySwatStyleAuthoring.Apply(actor);
        Prepare();
        Transform oldHelmet = actor.head.Find(FacilitySwatStyleAuthoring.HeadGroup);
        if (oldHelmet != null) Object.DestroyImmediate(oldHelmet.gameObject);
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            if (region == null || region.kind == FacilityBodyRegion.Head) continue;
            UpgradePanels(region);
        }
        RecolorAccessories(actor);
        FacilityEliteSwat elite = actor.GetComponent<FacilityEliteSwat>();
        if (elite == null) elite = actor.gameObject.AddComponent<FacilityEliteSwat>();
        BuildHelmet(actor, elite);
        var marker = actor.GetComponent<FacilityCharacterAppearance>();
        if (marker == null) marker = actor.gameObject.AddComponent<FacilityCharacterAppearance>();
        marker.style = FacilityCharacterStyle.EliteSwat;
        marker.revision = 3;
        marker.appearanceRenderers = actor.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.GetComponentInParent<FacilityHitRegion>(true) != null).ToArray();
        EditorUtility.SetDirty(elite); EditorUtility.SetDirty(marker); EditorUtility.SetDirty(actor);
    }

    static void Prepare()
    {
        Folder(Root + "/Meshes"); Folder(Root + "/Materials"); Folder(Root + "/Prefabs");
        block = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/Combat/Character/Meshes/Eight sided chamfered block.asset");
        round = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/Combat/Character/Meshes/Rounded low poly form.asset");
        if (block == null || round == null) throw new InvalidOperationException("The existing stylized character forms are missing.");
        palette = new[] {
            Mat("Elite black woven cloth", new Color(.060f,.072f,.075f), .035f),
            Mat("Elite deep charcoal rubber", new Color(.027f,.034f,.036f), .07f),
            Mat("Elite graphite armor", new Color(.090f,.112f,.117f), .18f),
            Mat("Elite gray black webbing", new Color(.16f,.18f,.17f), .045f),
            Mat("Elite worn gunmetal edge", new Color(.30f,.33f,.32f), .25f,.34f),
            Mat("Elite muted ivory identification", new Color(.57f,.60f,.53f), .025f),
            Mat("Elite dark blood scuffs", new Color(.23f,.016f,.022f), .15f)
        };
        wound = Mat("Elite helmet exposed tissue", new Color(.40f,.025f,.035f), .15f);
        bone = Mat("Elite helmet fracture bone", new Color(.56f,.44f,.31f), .09f);
        nightLens = Mat("Elite night vision deep crimson glass", new Color(.075f,.006f,.008f), .82f,.22f);
    }

    static void UpgradePanels(FacilityHitRegion region)
    {
        if (region.outerPatches == null) return;
        for (int i = 0; i < region.outerPatches.Length; i++)
        {
            Renderer renderer = region.outerPatches[i]; if (renderer == null) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Elite armor lost its base damage panel.");
            Geometry mesh = new Geometry(palette.Length);
            mesh.Add(filter.sharedMesh, Matrix4x4.identity);
            bool upper = region.kind == FacilityBodyRegion.UpperArmLeft || region.kind == FacilityBodyRegion.UpperArmRight;
            bool forearm = region.kind == FacilityBodyRegion.ForearmLeft || region.kind == FacilityBodyRegion.ForearmRight;
            bool thigh = region.kind == FacilityBodyRegion.ThighLeft || region.kind == FacilityBodyRegion.ThighRight;
            bool shin = region.kind == FacilityBodyRegion.ShinLeft || region.kind == FacilityBodyRegion.ShinRight;
            if (upper && i == 1)
            {
                float side = region.kind == FacilityBodyRegion.UpperArmLeft ? -1 : 1;
                mesh.Box(new Vector3(side*.018f,-.060f,.015f),new Vector3(.226f,.148f,.235f),2);
                mesh.Box(new Vector3(side*.022f,.012f,.012f),new Vector3(.211f,.036f,.205f),1);
                mesh.Box(new Vector3(side*.018f,-.052f,.142f),new Vector3(.205f,.102f,.024f),1);
                mesh.Box(new Vector3(side*.018f,-.085f,.158f),new Vector3(.170f,.012f,.008f),4);
                mesh.Box(new Vector3(side*.018f,-.016f,.158f),new Vector3(.104f,.017f,.007f),3);
            }
            else if (region.kind == FacilityBodyRegion.Torso)
            {
                if (i >= 3 && i <= 5)
                {
                    float x = (i-4)*(.44f/3);
                    mesh.Box(new Vector3(x,.205f,.207f),new Vector3(.139f,.041f,.042f),2);
                    mesh.Box(new Vector3(x,.222f,.230f),new Vector3(.116f,.009f,.007f),4);
                    mesh.Box(new Vector3(x + (i==3?-.061f:i==5?.061f:0),.075f,.210f),
                        new Vector3(i==4?.038f:.011f,i==4?.025f:.076f,.011f),1);
                }
                else if (i < 3)
                    mesh.Box(new Vector3((i-1)*(.44f/3),-.195f,.275f),new Vector3(.117f,.013f,.010f),4);
                else if (i >= 12)
                {
                    float side = i==12?-1:1;
                    mesh.Box(new Vector3(side*.244f,-.008f,0),new Vector3(.049f,.327f,.269f),2);
                    mesh.Box(new Vector3(side*.274f,-.015f,0),new Vector3(.009f,.290f,.207f),1);
                }
            }
            else if ((forearm || thigh || shin) && i == 1)
            {
                float y = thigh?-.085f:shin?-.062f:-.064f;
                float z = thigh?.137f:shin?.147f:.121f;
                Vector3 size = new Vector3(thigh?.177f:shin?.180f:.168f,thigh?.161f:shin?.145f:.128f,.044f);
                mesh.Box(new Vector3(0,y,z),size,2);
                mesh.Box(new Vector3(0,y-.025f,z+.027f),new Vector3(size.x*.81f,.012f,.010f),4);
                mesh.Box(new Vector3(0,y+.042f,z+.027f),new Vector3(size.x*.65f,.018f,.010f),1);
            }
            filter.sharedMesh = Save(region.kind + " removable heavy armor " + i + " V1", mesh.Build());
            renderer.sharedMaterials = palette; renderer.name = ArmorPrefix + region.kind + " " + i;
            Configure(renderer); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(filter);
        }
    }

    static void RecolorAccessories(FacilityHumanoid actor)
    {
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials; bool changed = false;
            for (int i=0;i<materials.Length;i++)
            {
                if(materials[i]==null)continue;
                string name=materials[i].name;
                int slot=name=="SWAT dark navy fabric"?0:name=="SWAT charcoal rubber"?1:
                    name=="SWAT blue charcoal armor"?2:name=="SWAT gray webbing"?3:
                    name=="SWAT worn armor edges"?4:name=="SWAT identification and eyes"?5:
                    name=="SWAT dried crimson scuffs"?6:-1;
                if(slot<0)continue;materials[i]=palette[slot];changed=true;
            }
            if(changed)renderer.sharedMaterials=materials;
        }
    }

    static void BuildHelmet(FacilityHumanoid actor, FacilityEliteSwat elite)
    {
        Transform head = actor.head;
        Transform root = new GameObject(HeadGroup).transform; root.SetParent(head,false);
        Geometry shell = new Geometry(palette.Length);
        shell.Sphere(new Vector3(0,.072f,-.018f),new Vector3(.352f,.358f,.330f),2);
        shell.Box(new Vector3(0,.205f,-.017f),new Vector3(.098f,.038f,.236f),1);
        shell.Box(new Vector3(0,-.090f,-.119f),new Vector3(.242f,.104f,.082f),2);
        shell.Box(new Vector3(0,.025f,.165f),new Vector3(.207f,.050f,.051f),1);
        for(int side=-1;side<=1;side+=2)
        {
            shell.Box(new Vector3(side*.171f,-.009f,-.015f),new Vector3(.059f,.131f,.131f),2);
            shell.Box(new Vector3(side*.202f,-.006f,-.015f),new Vector3(.011f,.089f,.075f),1);
            shell.Box(new Vector3(side*.180f,.091f,-.005f),new Vector3(.032f,.056f,.145f),1);
            shell.Box(new Vector3(side*.200f,.093f,-.005f),new Vector3(.012f,.019f,.099f),4);
            shell.Box(new Vector3(side*.133f,-.112f,.090f),new Vector3(.032f,.082f,.054f),1,new Vector3(18,0,-side*12));
        }
        Piece(root,"Elite faceted helmet shell and ear armor",Save("Elite helmet shell V1",shell.Build()),palette);

        Geometry brow = new Geometry(palette.Length);
        brow.Box(new Vector3(0,.091f,.132f),new Vector3(.300f,.090f,.088f),2);
        brow.Box(new Vector3(0,.051f,.174f),new Vector3(.232f,.031f,.045f),1);
        brow.Box(new Vector3(0,.129f,.174f),new Vector3(.205f,.010f,.009f),4);
        for(int side=-1;side<=1;side+=2)
        {
            brow.Sphere(new Vector3(side*.125f,.086f,.179f),new Vector3(.025f,.025f,.013f),4);
            brow.Box(new Vector3(side*.080f,.074f,.182f),new Vector3(.014f,.046f,.010f),1,new Vector3(0,0,side*17));
        }
        GameObject first=Piece(root,"Elite helmet damage stage 1 - armored brow",Save("Elite breakaway armored brow V1",brow.Build()),palette).gameObject;

        Geometry mask=new Geometry(palette.Length);
        mask.Box(new Vector3(0,-.067f,.153f),new Vector3(.238f,.132f,.082f),2);
        mask.Box(new Vector3(0,-.139f,.127f),new Vector3(.230f,.042f,.102f),1);
        mask.Box(new Vector3(0,-.072f,.207f),new Vector3(.109f,.078f,.040f),1);
        mask.Box(new Vector3(0,-.043f,.229f),new Vector3(.075f,.013f,.011f),4);
        for(int side=-1;side<=1;side+=2)
        {
            mask.Box(new Vector3(side*.121f,-.059f,.126f),new Vector3(.067f,.132f,.111f),2,new Vector3(0,side*13,side*-6));
            mask.Box(new Vector3(side*.128f,-.070f,.189f),new Vector3(.012f,.084f,.011f),4,new Vector3(0,0,side*14));
            mask.Box(new Vector3(side*.078f,-.063f,.203f),new Vector3(.014f,.065f,.012f),1,new Vector3(0,0,side*15));
            mask.Sphere(new Vector3(side*.095f,-.105f,.200f),new Vector3(.015f,.015f,.009f),4);
        }
        for(int i=0;i<4;i++)mask.Box(new Vector3(-.025f+i*.017f,-.080f,.230f),new Vector3(.007f,.031f,.006f),4);
        // A simple painted skull motif, kept angular and readable at the game's original detail level.
        for(int side=-1;side<=1;side+=2)
            mask.Box(new Vector3(side*.080f,-.047f,.214f),new Vector3(.040f,.052f,.006f),5,new Vector3(0,0,side*13));
        mask.Box(new Vector3(0,-.030f,.234f),new Vector3(.033f,.030f,.006f),5);
        mask.Box(new Vector3(-.005f,-.033f,.238f),new Vector3(.008f,.020f,.003f),1,new Vector3(0,0,-22));
        mask.Box(new Vector3(.005f,-.033f,.238f),new Vector3(.008f,.020f,.003f),1,new Vector3(0,0,22));
        for(int i=0;i<5;i++)
            mask.Box(new Vector3(-.034f+i*.017f,-.111f,.232f),new Vector3(.010f,.023f,.006f),5);
        GameObject second=Piece(root,"Elite helmet damage stage 2 - face guard",Save("Elite breakaway metal face guard V1",mask.Build()),palette).gameObject;

        // Four physical panoramic night-vision tubes on the breakaway brow. Losing the
        // first armor plate removes the whole rig and its optical glare together.
        Renderer[] optics=BuildQuadGoggles(first.transform);

        Geometry damaged=new Geometry(3);
        damaged.Box(new Vector3(0,-.060f,.176f),new Vector3(.166f,.124f,.031f),0);
        damaged.Box(new Vector3(-.045f,-.053f,.197f),new Vector3(.053f,.072f,.017f),1,new Vector3(0,0,-16));
        damaged.Box(new Vector3(-.031f,-.074f,.209f),new Vector3(.011f,.057f,.012f),2,new Vector3(0,0,-16));
        damaged.Box(new Vector3(.029f,-.064f,.197f),new Vector3(.038f,.013f,.014f),1,new Vector3(0,0,19));
        GameObject reveal=Piece(root,"Elite damaged inner mask and exposed wound",Save("Elite exposed broken helmet wound V1",damaged.Build()),new[]{palette[1],wound,bone}).gameObject;
        reveal.SetActive(false);
        elite.helmetPlates=new[]{first,second};elite.eyeRenderer=optics[0];elite.opticRenderers=optics;elite.helmetDamageReveal=reveal;
        foreach(Transform t in head.GetComponentsInChildren<Transform>(true))
        {
            if(t.name=="Left black eye"||t.name=="Right black eye"||t.name=="Left expressive eyebrow"||t.name=="Right expressive eyebrow")
            {var r=t.GetComponent<Renderer>();if(r!=null)r.enabled=false;}
        }
    }

    static Renderer Piece(Transform parent,string name,Mesh mesh,Material[] materials)
    {
        GameObject go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;Renderer renderer=go.GetComponent<Renderer>();renderer.sharedMaterials=materials;
        Configure(renderer);return renderer;
    }

    static Renderer[] BuildQuadGoggles(Transform brow)
    {
        Transform rig=new GameObject("Elite quad night vision goggles - helmet mounted").transform;
        rig.SetParent(brow,false);
        Geometry housing=new Geometry(palette.Length);
        // Forehead shoe, articulated hinge, and rigid bridge behind all four tubes.
        housing.Box(new Vector3(0,.151f,.189f),new Vector3(.065f,.080f,.044f),1);
        housing.Box(new Vector3(0,.139f,.218f),new Vector3(.047f,.073f,.030f),2,new Vector3(-16,0,0));
        housing.Box(new Vector3(0,.077f,.228f),new Vector3(.039f,.080f,.034f),2);
        housing.Cylinder(new Vector3(0,.111f,.231f),.055f,.019f,.019f,4,new Vector3(0,90,0));
        housing.Box(new Vector3(0,.026f,.212f),new Vector3(.278f,.031f,.040f),1);
        housing.Box(new Vector3(0,.050f,.231f),new Vector3(.266f,.010f,.017f),4);
        Renderer[] optics=new Renderer[4];
        for(int i=0;i<4;i++)
        {
            float side=i<2?-1:1;
            bool outer=i==0||i==3;
            float yaw=side*(outer?16f:3f);
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            Vector3 center=new Vector3((i-1.5f)*.075f,.023f+(outer?.006f:0),.246f);
            Vector3 axis=rotation*Vector3.forward;
            housing.Cylinder(center,.108f,.031f,.034f,1,new Vector3(0,yaw,0));
            housing.Cylinder(center-axis*.037f,.021f,.035f,.035f,2,new Vector3(0,yaw,0));
            housing.Cylinder(center+axis*.038f,.017f,.036f,.036f,2,new Vector3(0,yaw,0));
            housing.Ring(center+axis*.057f,.0355f,.027f,.011f,4,new Vector3(0,yaw,0));
            housing.Ring(center+axis*.063f,.031f,.026f,.008f,1,new Vector3(0,yaw,0));
            // Two shallow front rim notches and a small rib on each ocular housing.
            housing.Box(center+new Vector3(0,.032f,-.006f),new Vector3(.020f,.008f,.036f),2,new Vector3(0,yaw,0));
            Geometry glass=new Geometry(1);
            Vector3 lensPosition=center+axis*.065f;
            glass.Cylinder(lensPosition,.003f,.026f,.026f,0,new Vector3(0,yaw,0));
            Piece(rig,"Elite night vision glass lens "+(i+1),Save("Elite quad glass lens "+(i+1)+" V1",glass.Build()),new[]{nightLens});
            optics[i]=AddOpticalHalo(rig,i+1,lensPosition+axis*.0035f,rotation);
        }
        Piece(rig,"Elite quad night vision housings and helmet mount",Save("Elite quad night vision rig V1",housing.Build()),palette);
        return optics;
    }

    static Renderer AddOpticalHalo(Transform parent,int index,Vector3 position,Quaternion rotation)
    {
        string path=Root+"/Materials/Elite compact red optical flare.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            var shader=Shader.Find("Facility/Elite Optical Glow");
            if(shader==null)throw new InvalidOperationException("The saved elite optic shader must be imported.");
            material=new Material(shader){name="Elite compact red optical flare"};AssetDatabase.CreateAsset(material,path);
        }
        material.SetFloat("_Intensity",2.8f);material.SetFloat("_Visibility",1f);
        material.SetFloat("_StreakStrength",.30f);material.SetFloat("_HaloStrength",.58f);
        material.SetFloat("_CoreRadius",.011f);EditorUtility.SetDirty(material);
        var mesh=new Mesh{name="Elite compact optical halo V1"};
        mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();mesh.RecalculateNormals();
        // Shader expands the billboard per camera. Bounds include its maximum extent so it
        // remains visible at screen edges, including when the helmet turns.
        mesh.bounds=new Bounds(Vector3.zero,Vector3.one*12f);
        var renderer=Piece(parent,"Elite night vision optical flare "+index,Save("Elite compact optical halo V1",mesh),new[]{material});
        renderer.transform.SetLocalPositionAndRotation(position,rotation);
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
        var optic=renderer.gameObject.AddComponent<FacilityEliteOptic>();optic.glowRenderer=renderer;
        return renderer;
    }
    static void Configure(Renderer renderer)
    {renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static Material Mat(string name,Color color,float smoothness,float metallic=0)
    {
        string path=Root+"/Materials/"+name+".mat";Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smoothness);mat.SetFloat("_Metallic",metallic);
        mat.SetColor("_EmissionColor",Color.black);mat.DisableKeyword("_EMISSION");mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh Save(string name,Mesh mesh)
    {
        mesh.name=name;string path=Root+"/Meshes/"+name+".asset";Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
        else{FacilityCharacterStyleAuthoring.CopyMesh(saved,mesh);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
        AssetDatabase.SaveAssetIfDirty(saved);return saved;
    }
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}

    sealed class Geometry
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int>[] indices;
        public Geometry(int count){indices=new List<int>[count];for(int i=0;i<count;i++)indices[i]=new List<int>();}
        public void Add(Mesh mesh,Matrix4x4 transform,int material=-1)
        {
            int start=vertices.Count;Vector3[] points=mesh.vertices;Vector2[] coords=mesh.uv;
            for(int i=0;i<points.Length;i++){vertices.Add(transform.MultiplyPoint3x4(points[i]));uv.Add(i<coords.Length?coords[i]:Vector2.zero);}
            for(int s=0;s<mesh.subMeshCount;s++)
            {int target=material>=0?material:s;if(target>=indices.Length)throw new InvalidOperationException("Elite armor material mapping changed.");foreach(int i in mesh.GetTriangles(s))indices[target].Add(start+i);}
        }
        public void Box(Vector3 p,Vector3 size,int material,Vector3 euler=default)
        {Add(block,Matrix4x4.TRS(p,Quaternion.Euler(euler),size),material);}
        public void Sphere(Vector3 p,Vector3 size,int material)
        {Add(round,Matrix4x4.TRS(p,Quaternion.identity,size),material);}
        public void Cylinder(Vector3 p,float length,float rearRadius,float frontRadius,int material,Vector3 euler=default)
        {
            Matrix4x4 matrix=Matrix4x4.TRS(p,Quaternion.Euler(euler),Vector3.one);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8f,b=(i+1)*Mathf.PI/8f;
                Vector3 backA=new Vector3(Mathf.Cos(a)*rearRadius,Mathf.Sin(a)*rearRadius,-length*.5f);
                Vector3 backB=new Vector3(Mathf.Cos(b)*rearRadius,Mathf.Sin(b)*rearRadius,-length*.5f);
                Vector3 frontA=new Vector3(Mathf.Cos(a)*frontRadius,Mathf.Sin(a)*frontRadius,length*.5f);
                Vector3 frontB=new Vector3(Mathf.Cos(b)*frontRadius,Mathf.Sin(b)*frontRadius,length*.5f);
                Quad(matrix,backA,backB,frontB,frontA,material);
                Triangle(matrix,new Vector3(0,0,length*.5f),frontA,frontB,material);
                Triangle(matrix,new Vector3(0,0,-length*.5f),backB,backA,material);
            }
        }
        public void Ring(Vector3 p,float outer,float inner,float depth,int material,Vector3 euler=default)
        {
            Matrix4x4 matrix=Matrix4x4.TRS(p,Quaternion.Euler(euler),Vector3.one);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8f,b=(i+1)*Mathf.PI/8f;
                Vector3 oa=new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,-depth*.5f);
                Vector3 ob=new Vector3(Mathf.Cos(b)*outer,Mathf.Sin(b)*outer,-depth*.5f);
                Vector3 ia=new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,-depth*.5f);
                Vector3 ib=new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner,-depth*.5f);
                Vector3 z=Vector3.forward*depth;
                Quad(matrix,oa,ob,ob+z,oa+z,material);
                Quad(matrix,ia+z,ib+z,ib,ia,material);
                Quad(matrix,ia+z,oa+z,ob+z,ib+z,material);
                Quad(matrix,ib,ob,oa,ia,material);
            }
        }
        void Quad(Matrix4x4 matrix,Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
        {
            int start=vertices.Count;
            vertices.Add(matrix.MultiplyPoint3x4(a));vertices.Add(matrix.MultiplyPoint3x4(b));
            vertices.Add(matrix.MultiplyPoint3x4(c));vertices.Add(matrix.MultiplyPoint3x4(d));
            uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.one);uv.Add(Vector2.up);
            indices[material].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        void Triangle(Matrix4x4 matrix,Vector3 a,Vector3 b,Vector3 c,int material)
        {
            int start=vertices.Count;
            vertices.Add(matrix.MultiplyPoint3x4(a));vertices.Add(matrix.MultiplyPoint3x4(b));vertices.Add(matrix.MultiplyPoint3x4(c));
            uv.Add(Vector2.one*.5f);uv.Add(Vector2.right);uv.Add(Vector2.up);
            indices[material].AddRange(new[]{start,start+1,start+2});
        }
        public Mesh Build()
        {
            Mesh mesh=new Mesh{indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=indices.Length;
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
