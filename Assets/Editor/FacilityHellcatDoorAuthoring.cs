using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Two real frameless coupe doors, cut from the body and hinged with their glass and upholstery.</summary>
public static class FacilityHellcatDoorAuthoring
{
    const string Root="Assets/Generated/Hellcat";
    const float Rear=-.886f,Front=.776f,Lower=.072f;

    public static void Build(Transform car,FacilityStoryDirector director,Material silver,Material glass,Material seal,Material black)
    {
        if(car==null||director==null)throw new ArgumentNullException("A mission car and its director are required.");
        Ensure(Root+"/Meshes");
        Transform exterior=car.Find("Challenger precision exterior details"),cabin=car.Find("Challenger cabin");
        Material upholstery=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Soft touch dashboard and seals.mat");
        if(upholstery==null)upholstery=black;
        for(int side=-1;side<=1;side+=2)
        {
            string label=side<0?"Challenger driver door - hinged":"Challenger passenger door - hinged";
            Transform previous=car.Find(label);if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            Transform oldFrame=car.Find("Challenger fixed door aperture "+side);if(oldFrame!=null)Object.DestroyImmediate(oldFrame.gameObject);
            Vector3 hinge=new Vector3(side*.890f,.800f,.780f);
            Transform frame=Group(car,"Challenger fixed door aperture "+side);
            Frame(frame,side,silver,seal,black);
            Transform pivot=Group(car,label);pivot.localPosition=hinge;
            Transform leaf=Group(pivot,"Closed curved door leaf");
            int sign=side;
            Node(leaf,"Separate crowned silver coupe door outer skin "+side,Grid("Moving curved silver long door "+side,92,28,
                (u,v)=>Outer(sign,Mathf.Lerp(Rear,Front,u),Mathf.Lerp(Lower,1,v))-hinge,side>0),silver);
            Node(leaf,"Moving upper shoulder and window sill "+side,Grid("Moving door shoulder crown "+side,92,12,(u,v)=>
            {
                Vector3 outside=Outer(sign,Mathf.Lerp(Rear,Front,u),1);
                float x=Mathf.Lerp(Mathf.Abs(outside.x),.836f,v);
                float y=Mathf.Lerp(outside.y,1.094f,v)+.008f*Mathf.Sin(v*Mathf.PI);
                return new Vector3(sign*x,y,outside.z)-hinge;
            },side>0),silver);
            Node(leaf,"Painted inner door shell behind upholstered card "+side,Grid("Moving door inner shell "+side,66,20,(u,v)=>
            {
                Vector3 p=Outer(sign,Mathf.Lerp(Rear,Front,u),Mathf.Lerp(Lower,1,v));p.x=sign*(.855f+.004f*Mathf.Sin(v*Mathf.PI));return p-hinge;
            },side<0),upholstery);
            ReturnEdges(leaf,side,hinge,silver);
            Window(leaf,side,hinge,glass,seal);

            // The trim is the actual visible trim from the exterior/cabin authoring, and moves with the skin.
            Move(exterior,leaf,"Sculpted body colour door mirror "+side);
            Move(exterior,leaf,"Door handle recessed pocket "+side);
            Move(exterior,leaf,"Flush silver door release handle "+side);
            Move(exterior,leaf,"Door handle key barrel "+side);
            Move(cabin,leaf,"Inner door card "+side);
            Move(cabin,leaf,"Padded door armrest "+side);
            Move(cabin,leaf,"Brushed interior release handle "+side);

            Transform solid=Group(leaf,"Door body and interior card physical collision");
            solid.localPosition=new Vector3(side*.910f,.690f,-.055f)-hinge;
            BoxCollider lowerCollider=solid.gameObject.AddComponent<BoxCollider>();lowerCollider.size=new Vector3(.100f,.750f,1.610f);
            Transform target=Group(leaf,"Door exterior handle interaction");target.localPosition=new Vector3(side*1.015f,.990f,-.655f)-hinge;
            BoxCollider trigger=target.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(.190f,.170f,.280f);
            FacilityDoor door=pivot.gameObject.AddComponent<FacilityDoor>();door.leaf=leaf;door.openLocalEuler=new Vector3(0,-side*65f,0);door.openLocalOffset=Vector3.zero;door.travelSeconds=.78f;door.locked=false;
            door.npcOpenDistance=.5f;
            var serialized=new SerializedObject(door);var npc=serialized.FindProperty("npcCanOpen");if(npc!=null){npc.boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();}
            FacilityStoryInteractable item=target.gameObject.AddComponent<FacilityStoryInteractable>();item.director=director;item.kind=FacilityStoryInteractionKind.Door;item.door=door;item.label=side<0?"Car driver door":"Car passenger door";door.interaction=item;
            // A second small target gives the same release to a player standing inside an opened door.
            Transform inside=Group(leaf,"Door interior release interaction");inside.localPosition=new Vector3(side*.796f,.932f,.287f)-hinge;
            BoxCollider insideTrigger=inside.gameObject.AddComponent<BoxCollider>();insideTrigger.isTrigger=true;insideTrigger.size=new Vector3(.110f,.130f,.230f);
            FacilityStoryInteractable innerItem=inside.gameObject.AddComponent<FacilityStoryInteractable>();innerItem.director=director;innerItem.kind=FacilityStoryInteractionKind.Door;innerItem.door=door;innerItem.label=item.label;
            door.leafColliders=leaf.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger).ToArray();
            var modifier=leaf.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();modifier.ignoreFromBuild=true;
            EditorUtility.SetDirty(door);
        }
    }

    static Vector3 Outer(float side,float z,float t)=>FacilityHellcatAuthoring.Side(side,z,t);

    static void Frame(Transform frame,int side,Material silver,Material seal,Material black)
    {
        foreach(float z in new[]{-.890f,.780f})
        {
            float edge=z;
            Node(frame,"Painted fixed aperture jamb "+z,Grid("Fixed painted door jamb "+side+" "+z,5,32,(u,v)=>
            {
                Vector3 outside=Outer(side,edge,Mathf.Lerp(.070f,1,v));outside.x=Mathf.Lerp(outside.x,side*.840f,u);return outside;
            },(side>0)==(z<0)),silver);
        }
        Node(frame,"Curved fixed aperture threshold",Grid("Fixed Challenger door threshold "+side,70,6,(u,v)=>
        {
            Vector3 p=Outer(side,Mathf.Lerp(-.89f,.78f,u),.070f);p.x=Mathf.Lerp(p.x,side*.823f,v);p.y-=.003f*Mathf.Sin(v*Mathf.PI);return p;
        },side>0),silver);
        ApertureSeal(frame,side,seal);
        foreach(float y in new[]{.470f,.905f})
        {
            Transform hinge=Group(frame,"Visible fixed door hinge "+y);hinge.localPosition=new Vector3(side*.880f,y,.754f);
            Node(hinge,"Forged painted door hinge bracket",BoxMesh("Door hinge bracket geometry",new Vector3(.045f,.071f,.058f)),silver);
            Transform pin=Group(hinge,"Steel hinge pin");pin.localPosition=new Vector3(-side*.014f,0,-.004f);
            Node(pin,"Steel hinge pin geometry",BoxMesh("Steel door hinge pin geometry",new Vector3(.013f,.081f,.013f)),black);
        }
    }

    /// <summary>Repairs only the two fixed aperture seals on an already installed car.</summary>
    public static void RefreshApertureSeals(Transform car)
    {
        if(car==null)throw new ArgumentNullException(nameof(car));
        Ensure(Root+"/Meshes");
        Material seal=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Challenger - window and panel rubber.mat");
        if(seal==null)throw new InvalidOperationException("The installed Challenger weather seal material is missing.");
        for(int side=-1;side<=1;side+=2)
        {
            Transform frame=car.Find("Challenger fixed door aperture "+side);
            if(frame==null)continue;
            ApertureSeal(frame,side,seal);
        }
    }

    static void ApertureSeal(Transform frame,int side,Material material)
    {
        string name="Continuous fixed door aperture weather seal "+side;
        Transform previous=frame.Find(name);if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        // Follow the physical aperture: front jamb, A pillar, roof rail, rear window edge and sill.
        var weather=new List<Vector3>{
            new Vector3(side*.842f,.315f,.767f),
            new Vector3(side*.886f,1.068f,.834f),
            new Vector3(side*.728f,1.420f,.414f),
            new Vector3(side*.735f,1.420f,-.540f),
            new Vector3(side*.900f,1.074f,-.865f),
            new Vector3(side*.842f,.315f,-.878f),
            new Vector3(side*.842f,.315f,.767f)};
        Tube(frame,name,weather,.0060f,material);
        Transform created=frame.Find(name);created.gameObject.layer=29;
        MeshRenderer renderer=created.GetComponent<MeshRenderer>();renderer.receiveShadows=true;
        GameObjectUtility.SetStaticEditorFlags(created.gameObject,StaticEditorFlags.OccludeeStatic);
        EditorUtility.SetDirty(created.gameObject);EditorUtility.SetDirty(renderer);
    }

    static void ReturnEdges(Transform leaf,int side,Vector3 hinge,Material silver)
    {
        foreach(float z in new[]{Rear,Front})
        {
            float edge=z;
            Node(leaf,"Rounded moving door edge return "+z,Grid("Moving painted door edge return "+side+" "+z,6,32,(u,v)=>
            {
                Vector3 p=Outer(side,edge,Mathf.Lerp(Lower,1,v));float outside=p.x;p.x=Mathf.Lerp(outside,side*.855f,u);p.z+=Mathf.Sin(u*Mathf.PI)*.003f*(z>0?1:-1);return p-hinge;
            },(side>0)==(z>0)),silver);
        }
        Node(leaf,"Rounded painted door bottom hem",Grid("Moving rounded coupe door bottom hem "+side,76,6,(u,v)=>
        {
            Vector3 p=Outer(side,Mathf.Lerp(Rear,Front,u),Lower);p.x=Mathf.Lerp(p.x,side*.855f,v);p.y-=Mathf.Sin(v*Mathf.PI)*.003f;return p-hinge;
        },side<0),silver);
        var seam=new List<Vector3>();
        for(int n=0;n<=40;n++)seam.Add(Outer(side,Rear,Mathf.Lerp(Lower,1,n/40f))-hinge);
        for(int n=0;n<=72;n++)seam.Add(Outer(side,Mathf.Lerp(Rear,Front,n/72f),1)-hinge);
        for(int n=0;n<=40;n++)seam.Add(Outer(side,Front,Mathf.Lerp(1,Lower,n/40f))-hinge);
        for(int n=0;n<=72;n++)seam.Add(Outer(side,Mathf.Lerp(Front,Rear,n/72f),Lower)-hinge);
        seam.Add(seam[0]);
        Material seal=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Challenger - window and panel rubber.mat");
        Tube(leaf,"Fine moving door perimeter shut line "+side,seam,.0016f,seal!=null?seal:silver);
    }

    static void Window(Transform leaf,int side,Vector3 hinge,Material glass,Material seal)
    {
        Vector3 rearBottom=new Vector3(side*.900f,1.074f,-.865f),frontBottom=new Vector3(side*.886f,1.068f,.834f);
        Vector3 rearTop=new Vector3(side*.735f,1.420f,-.540f),frontTop=new Vector3(side*.728f,1.420f,.414f);
        GameObject surface=Node(leaf,"Frameless opening door glass "+side,Grid("Moving curved frameless Challenger glass "+side,48,22,(u,v)=>
        {
            Vector3 p=Vector3.Lerp(Vector3.Lerp(rearBottom,frontBottom,u),Vector3.Lerp(rearTop,frontTop,u),v);p.x+=side*.003f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI);return p-hinge;
        },side>0),glass);
        surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        Vector3[] corners={rearBottom-hinge,frontBottom-hinge,frontTop-hinge,rearTop-hinge};
        MeshCollider physicalGlass=surface.AddComponent<MeshCollider>();physicalGlass.sharedMesh=Prism("Physical moving tempered door glass "+side,corners,new Vector3(side*.007f,0,0));physicalGlass.convex=true;
        var rim=corners.ToList();rim.Add(corners[0]);Tube(leaf,"Fine black edge on moving frameless glass "+side,rim,.0050f,seal);
        Tube(leaf,"Moving lower window belt weather strip "+side,new[]{frontBottom-hinge,rearBottom-hinge},.0110f,seal);
    }

    static void Move(Transform from,Transform to,string name){if(from==null)return;Transform item=from.Find(name);if(item!=null)item.SetParent(to,true);}
    static Transform Group(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
    static GameObject Node(Transform p,string name,Mesh mesh,Material material)
    {
        var g=Group(p,name).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;return g;
    }
    static Mesh Grid(string name,int nx,int ny,Func<float,float,Vector3> point,bool reverse)
    {
        var v=new Vector3[(nx+1)*(ny+1)];var uv=new Vector2[v.Length];var tri=new int[nx*ny*6];int n=0;
        for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){int at=y*(nx+1)+x;float u=x/(float)nx,w=y/(float)ny;v[at]=point(u,w);uv[at]=new Vector2(u,w);}
        for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int a=y*(nx+1)+x,b=a+1,c=a+nx+2,d=a+nx+1;tri[n++]=a;tri[n++]=reverse?c:b;tri[n++]=reverse?b:c;tri[n++]=a;tri[n++]=reverse?d:c;tri[n++]=reverse?c:d;}
        return Save(name,v,uv,tri);
    }
    static Mesh BoxMesh(string name,Vector3 size)
    {
        Vector3 s=size*.5f;Vector3[] p={new Vector3(-s.x,-s.y,-s.z),new Vector3(s.x,-s.y,-s.z),new Vector3(s.x,s.y,-s.z),new Vector3(-s.x,s.y,-s.z),new Vector3(-s.x,-s.y,s.z),new Vector3(s.x,-s.y,s.z),new Vector3(s.x,s.y,s.z),new Vector3(-s.x,s.y,s.z)};
        return Save(name,p,p.Select(v=>new Vector2(v.x,v.y)).ToArray(),new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4});
    }
    static Mesh Prism(string name,Vector3[] outline,Vector3 thickness)
    {
        var v=new Vector3[8];for(int n=0;n<4;n++){v[n]=outline[n]-thickness*.5f;v[n+4]=outline[n]+thickness*.5f;}
        return Save(name,v,v.Select(p=>new Vector2(p.z,p.y)).ToArray(),new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7});
    }
    static void Tube(Transform parent,string name,IList<Vector3> path,float radius,Material material)
    {
        const int sides=8;var v=new Vector3[path.Count*sides];var uv=new Vector2[v.Length];var tri=new List<int>();
        for(int i=0;i<path.Count;i++)
        {
            Vector3 tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;Vector3 u=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.forward:Vector3.up).normalized,w=Vector3.Cross(tangent,u);
            for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;v[i*sides+j]=path[i]+(u*Mathf.Cos(a)+w*Mathf.Sin(a))*radius;uv[i*sides+j]=new Vector2(j/(float)sides,i/(float)path.Count);if(i>0){int k=i*sides+j,b=i*sides+(j+1)%sides;tri.AddRange(new[]{k,b,b-sides,k,b-sides,k-sides});}}
        }
        Node(parent,name,Save(name,v,uv,tri.ToArray()),material);
    }
    static Mesh Save(string name,Vector3[] vertices,Vector2[] uv,int[] triangles)
    {
        string safe=string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));string path=Root+"/Meshes/"+safe+".asset";Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);return mesh;
    }
    static void Ensure(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Ensure(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
}
