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
using Object=UnityEngine.Object;

/// <summary>Saved stainless Government-pattern sidearm; all moving parts are authored before play.</summary>
public static class FacilityM1911Authoring
{
    public const string Root="Assets/Generated/Arsenal/M1911";
    static Material steel,edge,matte,dark,walnut,grain,ivory,brass,leather,seam,canvas;
    static bool held;

    public static string Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Author the pistol outside Play mode.");
        EditorSceneManager.SaveOpenScenes();string original=SceneManager.GetActiveScene().path;
        int count=0;
        foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
        {
            Scene scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
            string backup="Assets/_Recovery/BeforeM1911_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Path.GetFileName(entry.path);
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not preserve pistol scene.");
            var session=FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
            Build(session.inventory);
            if(session.storyDirector!=null && session.storyDirector.drillPickup!=null)
            {
                Transform equipment=session.storyDirector.drillPickup.visualRoot.transform;
                Transform old=equipment.Find("Service Pistol Pickup");
                Vector3 p=old!=null?old.localPosition:new Vector3(.39f,.81f,-1.36f);
                Quaternion q=old!=null?old.localRotation:Quaternion.Euler(0,78,90);
                Vector3 scale=old!=null?old.localScale:Vector3.one*.85f;
                if(old!=null)Object.DestroyImmediate(old.gameObject);
                GameObject world=BuildWorld(equipment);world.transform.SetLocalPositionAndRotation(p,q);world.transform.localScale=scale;
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save M1911.");count++;
        }
        if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
        return "Saved silver M1911, detailed walnut grips, actual moving action, magazine/reload hands, original .45 audio and pooled brass in "+count+" scenes and the car's equipment.";
    }

    public static FacilityPistol Build(FacilityWeaponInventory inventory)
    {
        Prepare();held=true;
        Camera camera=inventory.player.GetComponentInChildren<Camera>(true);
        if(inventory.pistolModel!=null)Object.DestroyImmediate(inventory.pistolModel.gameObject);
        Transform root=Group(camera.transform,"Service Pistol");
        root.localPosition=new Vector3(.18f,-.21f,.42f);root.localRotation=Quaternion.Euler(0,-5,0);root.localScale=Vector3.one*1.35f;
        Transform assembly=Group(root,"M1911 pistol and hands - reload tilt");
        var pistol=inventory.pistol!=null?inventory.pistol:camera.gameObject.AddComponent<FacilityPistol>();
        Geometry(assembly,out Transform slide,out Transform hammer,out Transform trigger,out Transform magazine,out Transform muzzle);
        Transform support=Hands(assembly);
        Transform eject=Group(assembly,"M1911 chamber ejection point");eject.localPosition=new Vector3(.024f,.029f,.004f);
        Transform flash=Group(muzzle,"M1911 brief pressure flash");flash.localScale=Vector3.one*.58f;
        Part(flash,"M1911 amber flash",FacilityPlayerArsenalAuthoring.Flash(),Mat("M1911 Amber Flame",new Color(1,.55f,.13f),0,0,true));
        Transform core=Part(flash,"M1911 white hot flame core",FacilityPlayerArsenalAuthoring.Flash(),Mat("M1911 White Flame",new Color(1,.94f,.76f),0,0,true));core.localScale=new Vector3(.45f,.45f,.65f);
        flash.gameObject.SetActive(false);
        var light=Group(muzzle,"M1911 momentary muzzle illumination").gameObject.AddComponent<Light>();
        light.type=LightType.Point;light.range=3.6f;light.intensity=2.5f;light.color=new Color(1,.69f,.32f);light.shadows=LightShadows.None;light.enabled=false;
        // A restrained soft reflection on the held metal, scoped to the viewmodel's rendering
        // layer. Neither this light nor an emissive material brightens the world pistol.
        var key=Group(root,"M1911 soft metal reflection").gameObject.AddComponent<Light>();key.type=LightType.Point;
        key.transform.localPosition=new Vector3(-.18f,.22f,-.26f);key.range=1.1f;key.intensity=.14f;
        key.color=new Color(.88f,.94f,1f);key.shadows=LightShadows.None;key.renderingLayerMask=128;
        pistol.aimCamera=camera;pistol.inventory=inventory;pistol.viewmodel=camera.GetComponent<RetroViewmodel>();
        pistol.surfaceImpacts=inventory.shotgun!=null?inventory.shotgun.surfaceImpacts:camera.GetComponent<FacilitySurfaceImpacts>();pistol.slide=slide;pistol.hammer=hammer;pistol.trigger=trigger;
        pistol.reloadAssembly=assembly;pistol.magazine=magazine;pistol.supportHand=support;pistol.muzzle=muzzle;pistol.ejectionPort=eject;
        pistol.muzzleFlash=flash.gameObject;pistol.muzzleLight=light;pistol.casingPrefab=Casing();pistol.fireInterval=.22f;pistol.range=70f;
        FacilityM1911AudioAuthoring.Configure(pistol);
        inventory.pistol=pistol;inventory.pistolModel=root;root.gameObject.SetActive(false);
        EditorUtility.SetDirty(pistol);EditorUtility.SetDirty(inventory);return pistol;
    }

    public static GameObject BuildWorld(Transform parent)
    {
        Prepare();held=false;Transform root=Group(parent,"Service Pistol Pickup");
        Geometry(root,out _,out _,out _,out _,out _);return root.gameObject;
    }

    public static void Capture(Scene scene)
    {
        string output=Path.GetFullPath(".codex/m1911-previews");Directory.CreateDirectory(output);
        var inventory=FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene).inventory;
        var stage=new GameObject("Temporary M1911 studio");stage.transform.position=new Vector3(0,200,0);
        var target=new RenderTexture(1500,950,24,RenderTextureFormat.ARGB32);target.Create();var previous=RenderTexture.active;
        try
        {
            var copy=Object.Instantiate(inventory.pistolModel.gameObject,stage.transform);copy.SetActive(true);
            copy.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
            foreach(var t in copy.GetComponentsInChildren<Transform>(true))
                if(t.name=="M1911 right hand and sleeve" || t.name=="M1911 left support hand and sleeve")t.gameObject.SetActive(false);
            var camera=new GameObject("M1911 studio camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.034f,.043f);camera.fieldOfView=35;camera.nearClipPlane=.02f;camera.farClipPlane=5;
            camera.GetUniversalAdditionalCameraData().renderShadows=true;
            Vector3[] lights={new Vector3(-.4f,.6f,.2f),new Vector3(.4f,.2f,-.4f),new Vector3(.1f,.4f,.7f)};
            for(int i=0;i<lights.Length;i++){var light=new GameObject("M1911 studio softbox").AddComponent<Light>();light.transform.SetParent(stage.transform,false);light.transform.localPosition=lights[i];light.type=LightType.Point;light.intensity=i==0?.35f:.17f;light.range=3;light.shadows=LightShadows.None;}
            for(int shot=0;shot<3;shot++)
            {
                camera.transform.localPosition=shot==0?new Vector3(-.41f,.11f,.31f):shot==1?new Vector3(-.50f,.035f,.005f):new Vector3(.40f,.15f,.30f);
                camera.transform.LookAt(stage.transform.position+new Vector3(0,-.042f,.005f));
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,"0"+(shot+1)+"_Silver_M1911.png"),image.EncodeToPNG());Object.DestroyImmediate(image);
            }
        }
        finally{RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(stage);}
    }

    static void Geometry(Transform root,out Transform slide,out Transform hammer,out Transform trigger,out Transform mag,out Transform muzzle)
    {
        Part(root,"M1911 forged stainless frame",Profile("M1911 forged slim frame V1",.030f,new[]{
            new Vector2(.004f,-.094f),new Vector2(.004f,.129f),new Vector2(-.016f,.127f),new Vector2(-.023f,.041f),
            new Vector2(-.023f,-.020f),new Vector2(-.140f,-.058f),new Vector2(-.151f,-.096f),new Vector2(-.139f,-.109f),
            new Vector2(-.020f,-.073f),new Vector2(-.010f,-.098f)}),steel);
        // Rounded dust cover and a true circular barrel bushing preserve the 1911 silhouette.
        Rod(root,"M1911 rounded dust cover",.013f,.120f,new Vector3(0,-.008f,.067f),steel,18);
        Transform barrel=Part(root,"M1911 visible tilting barrel",FacilityPlayerArsenalAuthoring.Tube("M1911 hollow .45 barrel V1",.0087f,.0055f,.171f,24),edge);
        barrel.localPosition=new Vector3(0,.026f,.047f);
        Rod(root,"M1911 deeply recessed dark bore",.0054f,.003f,new Vector3(0,.026f,.116f),dark,20);
        Part(root,"M1911 long swept beavertail",Profile("M1911 swept grip safety V1",.023f,new[]{
            new Vector2(-.005f,-.068f),new Vector2(-.008f,-.103f),new Vector2(-.001f,-.120f),new Vector2(.005f,-.119f),
            new Vector2(.001f,-.096f),new Vector2(.001f,-.070f)}),edge);
        Part(root,"M1911 arched mainspring housing",Profile("M1911 mainspring housing V1",.023f,new[]{
            new Vector2(-.038f,-.074f),new Vector2(-.119f,-.104f),new Vector2(-.143f,-.099f),new Vector2(-.142f,-.106f),
            new Vector2(-.109f,-.111f),new Vector2(-.037f,-.082f)}),matte);
        for(int side=-1;side<=1;side+=2)
        {
            Transform panel=Part(root,"M1911 "+(side<0?"left":"right")+" double diamond walnut grip",Profile("M1911 walnut panel V1",.0045f,new[]{
                new Vector2(-.031f,-.066f),new Vector2(-.030f,-.035f),new Vector2(-.135f,-.069f),
                new Vector2(-.142f,-.092f),new Vector2(-.127f,-.101f)}),walnut);
            panel.localPosition=Vector3.right*side*.017f;
            var checking=new MeshData();
            for(int row=0;row<18;row++)for(int column=0;column<5;column++)
            {
                float y=-.041f-row*.0050f,z=-.043f-row*.00175f-column*.0053f;
                float dy=Mathf.Min(Mathf.Abs(y+.049f),Mathf.Abs(y+.121f));
                if(dy<.012f && column>1 && column<4)continue;
                float x=side*.0194f;
                checking.Quad(new Vector3(x,y-.0019f,z),new Vector3(x,y,z-.0019f),new Vector3(x,y+.0019f,z),new Vector3(x,y,z+.0019f),side<0);
            }
            Part(root,"M1911 cut diamond grip checkering "+side,Save("M1911 fine diamond checkering "+side+" V1",checking.Build()),grain);
            for(int i=0;i<2;i++)
            {
                float y=i==0?-.049f:-.121f,z=i==0?-.054f:-.080f;
                Transform diamond=Part(root,"M1911 smooth walnut screw diamond "+side+" "+i,Profile("M1911 smooth grip diamond V1",.001f,new[]{
                    new Vector2(-.011f,0),new Vector2(0,.007f),new Vector2(.011f,0),new Vector2(0,-.007f)}),walnut);
                diamond.localPosition=new Vector3(side*.0202f,y,z);
                Transform screw=Rod(root,"M1911 slotted grip screw "+side+" "+i,.0032f,.002f,new Vector3(side*.021f,y,z),edge,16);screw.localRotation=Quaternion.Euler(0,90,0);
                Box(root,"M1911 screw head slot "+side+" "+i,new Vector3(side*.0221f,y,z),new Vector3(.00035f,.00065f,.0045f),dark);
            }
        }
        // Continuous oval guard, with an open center rather than solid block geometry.
        Part(root,"M1911 smoothly curved open trigger guard",FacilityPlayerArsenalAuthoring.Loft("M1911 rounded trigger guard V1",new[]{
            new Vector3(0,-.020f,-.023f),new Vector3(0,-.050f,-.025f),new Vector3(0,-.063f,-.014f),
            new Vector3(0,-.066f,.012f),new Vector3(0,-.060f,.033f),new Vector3(0,-.046f,.041f),new Vector3(0,-.021f,.040f)
        },new[]{.007f,.007f,.007f,.007f,.007f,.007f,.007f},new[]{.0038f,.0038f,.0038f,.0038f,.0038f,.0038f,.0038f},10),steel);
        trigger=Group(root,"M1911 pivoting curved trigger");trigger.localPosition=new Vector3(0,-.018f,.006f);
        Part(trigger,"M1911 silver trigger shoe",Profile("M1911 curved trigger shoe V1",.009f,new[]{
            new Vector2(0,-.003f),new Vector2(0,.008f),new Vector2(-.018f,.007f),new Vector2(-.032f,.013f),
            new Vector2(-.034f,.008f),new Vector2(-.020f,.001f)}),edge);
        Box(root,"M1911 slide stop lever",new Vector3(-.018f,-.009f,-.009f),new Vector3(.006f,.006f,.036f),edge);
        Box(root,"M1911 thumb safety shelf",new Vector3(-.019f,-.006f,-.079f),new Vector3(.009f,.005f,.027f),edge);
        Transform release=Rod(root,"M1911 textured magazine catch",.0042f,.003f,new Vector3(-.018f,-.034f,-.031f),matte,16);release.localRotation=Quaternion.Euler(0,90,0);
        mag=Group(root,"M1911 removable eight round magazine");mag.localPosition=new Vector3(0,-.106f,-.072f);
        Part(mag,"M1911 polished magazine body",Profile("M1911 slim magazine V1",.024f,new[]{
            new Vector2(.067f,.026f),new Vector2(.065f,-.005f),new Vector2(-.046f,-.035f),new Vector2(-.047f,.001f)}),matte);
        Box(mag,"M1911 silver magazine baseplate",new Vector3(0,-.045f,-.017f),new Vector3(.031f,.006f,.044f),edge);
        for(int i=0;i<5;i++)
        {Transform hole=Rod(mag,"M1911 magazine witness hole "+i,.0019f,.0008f,new Vector3(.0123f,.038f-i*.015f,.014f-i*.0042f),dark,10);hole.localRotation=Quaternion.Euler(0,90,0);}
        slide=Group(root,"M1911 reciprocating rounded stainless slide");
        Part(slide,"M1911 rounded slide with open ejection cut",SlideMesh(),steel);
        Box(slide,"M1911 matte sighting rib",new Vector3(0,.0502f,.004f),new Vector3(.0105f,.001f,.179f),matte);
        // Open cutaway exposes the actual barrel and dark chamber inside the moving slide.
        Box(root,"M1911 chamber recess under cutaway",new Vector3(.007f,.024f,.003f),new Vector3(.017f,.009f,.051f),dark);
        Rod(root,"M1911 polished chamber hood",.0088f,.046f,new Vector3(0,.026f,.001f),edge,18);
        for(int side=-1;side<=1;side+=2)for(int i=0;i<9;i++)
        {
            Transform cut=Box(slide,"M1911 rear cocking serration "+side+" "+i,new Vector3(side*.0177f,.024f,-.088f+i*.0036f),new Vector3(.0009f,.026f,.00135f),dark);
            cut.localRotation=Quaternion.Euler(-11,0,0);
        }
        Transform bushing=Part(slide,"M1911 polished muzzle bushing",FacilityPlayerArsenalAuthoring.Tube("M1911 barrel bushing V1",.0127f,.0090f,.006f,24),edge);
        bushing.localPosition=new Vector3(0,.026f,.133f);
        Rod(slide,"M1911 recoil spring plug",.0068f,.006f,new Vector3(0,.005f,.132f),matte,18);
        Transform plug=Part(slide,"M1911 recoil plug polished rim",FacilityPlayerArsenalAuthoring.Tube("M1911 spring plug rim V1",.0074f,.0058f,.002f,18),edge);plug.localPosition=new Vector3(0,.005f,.135f);
        Box(slide,"M1911 low front sight",new Vector3(0,.0555f,.113f),new Vector3(.004f,.011f,.010f),dark);
        Box(slide,"M1911 front sight ivory insert",new Vector3(0,.056f,.1075f),new Vector3(.0022f,.0035f,.0008f),ivory);
        for(int side=-1;side<=1;side+=2)
        {
            Box(slide,"M1911 rear notch ear "+side,new Vector3(side*.0067f,.056f,-.086f),new Vector3(.0055f,.012f,.011f),dark);
            Box(slide,"M1911 rear sight dot "+side,new Vector3(side*.0067f,.0565f,-.092f),new Vector3(.0025f,.0025f,.0008f),ivory);
        }
        Box(slide,"M1911 rear firing pin stop plate",new Vector3(0,.025f,-.103f),new Vector3(.013f,.030f,.0015f),matte);
        Rod(slide,"M1911 recessed firing pin",.0021f,.001f,new Vector3(0,.027f,-.104f),edge,14);
        Stamp(slide,"M1911 .45 AUTO",new Vector3(-.01815f,.024f,-.036f),-.00062f);
        Stamp(root,"ND1911",new Vector3(-.0156f,-.009f,.026f),-.00048f);
        hammer=Group(root,"M1911 articulated spur hammer");hammer.localPosition=new Vector3(0,.016f,-.101f);
        Part(hammer,"M1911 cocked hammer spur",Profile("M1911 hammer spur V1",.010f,new[]{
            new Vector2(-.005f,.001f),new Vector2(.012f,-.009f),new Vector2(.028f,-.013f),new Vector2(.030f,-.006f),new Vector2(.012f,.004f)}),edge);
        for(int i=0;i<4;i++)Box(hammer,"M1911 hammer traction notch "+i,new Vector3(0,.023f+i*.0017f,-.012f),new Vector3(.0105f,.0006f,.0015f),dark);
        muzzle=Group(root,"M1911 muzzle");muzzle.localPosition=new Vector3(0,.026f,.141f);
    }

    static Mesh SlideMesh()
    {
        var b=new MeshData();
        Vector2[] cross={new Vector2(-.0163f,.005f),new Vector2(-.0175f,.014f),new Vector2(-.0175f,.032f),new Vector2(-.015f,.041f),new Vector2(-.010f,.048f),new Vector2(0,.050f),new Vector2(.010f,.048f),new Vector2(.015f,.041f),new Vector2(.0175f,.032f),new Vector2(.0175f,.014f),new Vector2(.0163f,.005f)};
        float[] slices={-.102f,-.019f,.035f,.131f};
        for(int z=0;z<3;z++)for(int i=0;i<cross.Length;i++)
        {
            if(z==1 && i>=5 && i<=8)continue;
            int next=(i+1)%cross.Length;
            Vector3 a=new Vector3(cross[i].x,cross[i].y,slices[z]),c=new Vector3(cross[next].x,cross[next].y,slices[z]);
            b.Quad(a,c,new Vector3(c.x,c.y,slices[z+1]),new Vector3(a.x,a.y,slices[z+1]),true);
        }
        for(int i=0;i<cross.Length;i++)
        {
            int next=(i+1)%cross.Length;
            b.Tri(new Vector3(0,.025f,slices[0]),new Vector3(cross[i].x,cross[i].y,slices[0]),new Vector3(cross[next].x,cross[next].y,slices[0]),false);
            Vector2 center=new Vector2(0,.026f);
            Vector2 innerA=center+(cross[i]-center).normalized*.0092f,innerB=center+(cross[next]-center).normalized*.0092f;
            b.Quad(new Vector3(cross[i].x,cross[i].y,slices[3]),new Vector3(cross[next].x,cross[next].y,slices[3]),
                new Vector3(innerB.x,innerB.y,slices[3]),new Vector3(innerA.x,innerA.y,slices[3]),true);
        }
        return Save("M1911 rounded open port slide V1",b.Build());
    }

    static Transform Hands(Transform assembly)
    {
        Transform right=Group(assembly,"M1911 right hand and sleeve"),left=Group(assembly,"M1911 left support hand and sleeve");
        for(int side=-1;side<=1;side+=2)
        {
            Transform hand=side<0?left:right;
            Part(hand,"M1911 "+side+" molded leather palm",FacilityPlayerArsenalAuthoring.Loft("M1911 palm "+side+" V1",new[]{new Vector3(side*.033f,-.043f,-.070f),new Vector3(side*.041f,-.086f,-.070f),new Vector3(side*.031f,-.140f,-.087f)},new[]{.021f,.025f,.023f},new[]{.020f,.026f,.023f},10),leather);
            for(int i=0;i<3;i++)
            {
                float y=-.073f-i*.022f,z=-.035f-i*.007f;
                Part(hand,"M1911 "+side+" curved gripping finger "+i,FacilityPlayerArsenalAuthoring.Loft("M1911 finger "+side+" "+i+" V1",new[]{
                    new Vector3(side*.045f,y,z-.025f),new Vector3(side*.027f,y,z),new Vector3(-side*.010f,y,z+.001f),new Vector3(-side*.025f,y,z-.016f)
                },new[]{.009f,.010f,.009f,.0065f},new[]{.009f,.010f,.009f,.0065f},8),leather);
                Box(hand,"M1911 leather knuckle seam "+side+" "+i,new Vector3(side*.045f,y+.006f,z-.014f),new Vector3(.0012f,.0015f,.018f),seam);
            }
            Part(hand,"M1911 "+side+" tapered canvas forearm",FacilityPlayerArsenalAuthoring.Loft("M1911 forearm "+side+" V1",new[]{new Vector3(side*.25f,-.37f,-.25f),new Vector3(side*.105f,-.225f,-.164f),new Vector3(side*.028f,-.150f,-.100f)},new[]{.049f,.040f,.030f},new[]{.044f,.036f,.027f},10),canvas);
            Part(hand,"M1911 "+side+" wrist cuff",FacilityPlayerArsenalAuthoring.Loft("M1911 wrist cuff "+side+" V1",new[]{new Vector3(side*.046f,-.177f,-.120f),new Vector3(side*.028f,-.148f,-.099f)},new[]{.033f,.030f},new[]{.029f,.026f},10),seam);
        }
        Part(right,"M1911 trigger index finger",FacilityPlayerArsenalAuthoring.Loft("M1911 trigger finger V1",new[]{new Vector3(.041f,-.043f,-.058f),new Vector3(.029f,-.039f,-.006f),new Vector3(.011f,-.044f,.010f),new Vector3(.004f,-.044f,.005f)},new[]{.009f,.008f,.007f,.005f},new[]{.009f,.008f,.007f,.005f},8),leather);
        Part(right,"M1911 right thumb along frame",FacilityPlayerArsenalAuthoring.Loft("M1911 right thumb V1",new[]{new Vector3(.026f,-.037f,-.096f),new Vector3(-.019f,-.023f,-.080f),new Vector3(-.028f,-.030f,-.040f)},new[]{.011f,.010f,.007f},new[]{.010f,.010f,.007f},8),leather);
        Part(left,"M1911 forward support thumb",FacilityPlayerArsenalAuthoring.Loft("M1911 support thumb V1",new[]{new Vector3(-.042f,-.057f,-.058f),new Vector3(-.034f,-.034f,-.018f),new Vector3(-.028f,-.031f,.014f)},new[]{.011f,.010f,.006f},new[]{.010f,.010f,.006f},8),leather);
        return left;
    }

    static GameObject Casing()
    {
        string path=Root+"/Prefabs/Spent .45 ACP brass.prefab";
        GameObject source=new GameObject("Spent .45 ACP brass");bool previous=held;held=false;
        try
        {
            Part(source.transform,"Hollow .45 brass case",FacilityPlayerArsenalAuthoring.Tube("M1911 hollow brass V1",.006f,.0049f,.023f,14),brass);
            Rod(source.transform,".45 primer",.0044f,.001f,Vector3.back*.0115f,dark,12);
            var collider=source.AddComponent<CapsuleCollider>();collider.direction=2;collider.radius=.006f;collider.height=.024f;
            var body=source.AddComponent<Rigidbody>();body.mass=.012f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            source.layer=2;source.SetActive(false);return PrefabUtility.SaveAsPrefabAsset(source,path);
        }
        finally{Object.DestroyImmediate(source);held=previous;}
    }

    static void Prepare()
    {
        foreach(string sub in new[]{"Meshes","Materials","Textures","Prefabs"})Folder(Root+"/"+sub);
        steel=Mat("M1911 Brushed Stainless",new Color(.66f,.69f,.72f),.62f,.47f);
        edge=Mat("M1911 Polished Edge",new Color(.79f,.81f,.83f),.76f,.62f);
        matte=Mat("M1911 Matte Top",new Color(.32f,.36f,.39f),.58f,.34f);
        dark=Mat("M1911 Deep Recess",new Color(.015f,.019f,.021f),.10f,.20f);
        walnut=Mat("M1911 Oiled Walnut",new Color(.36f,.155f,.065f),0,.30f);
        grain=Mat("M1911 Cut Walnut Checkering",new Color(.115f,.048f,.024f),0,.18f);
        ivory=Mat("M1911 Ivory Sight Inserts",new Color(.78f,.80f,.73f),0,.12f);
        brass=Mat("M1911 Spent Cartridge Brass",new Color(.58f,.39f,.13f),.75f,.43f);
        leather=Mat("M1911 Worn Leather",new Color(.22f,.185f,.14f),0,.18f);
        seam=Mat("M1911 Glove Seams",new Color(.105f,.09f,.070f),0,.12f);
        canvas=Mat("M1911 Olive Sleeves",new Color(.17f,.20f,.15f),0,.10f);
        steel.SetTexture("_BaseMap",Finish(false));walnut.SetTexture("_BaseMap",Finish(true));
    }

    static Texture2D Finish(bool wood)
    {
        string path=Root+"/Textures/"+(wood?"Oiled walnut grain":"Fine brushed stainless")+".png";
        var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing!=null)return existing;
        const int size=256;var pixels=new Color[size*size];var random=new System.Random(191145);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float noise=(float)random.NextDouble();
            float tone=wood?.78f+.17f*Mathf.Sin(x*.30f+Mathf.Sin(y*.08f)*1.4f)+noise*.07f:.94f+noise*.05f+Mathf.Sin(y*2.1f)*.01f;
            pixels[y*size+x]=new Color(tone,tone,tone,1);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGB24,true);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Mat(string name,Color color,float metallic,float smoothness,bool unlit=false)
    {
        string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);
        mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);return mat;
    }
    static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static Transform Part(Transform parent,string name,Mesh mesh,Material material)
    {
        Transform t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=held?ShadowCastingMode.Off:ShadowCastingMode.On;
        r.receiveShadows=!held;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
        r.renderingLayerMask=held?129u:1u;var camera=parent.GetComponentInParent<Camera>();if(camera!=null)r.probeAnchor=camera.transform;
        return t;
    }
    static Transform Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
    {
        Transform t=Part(parent,name,Profile("M1911 unit detail block V1",1,new[]{new Vector2(-.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,-.5f)}),mat);
        t.localPosition=p;t.localScale=size;return t;
    }
    static Transform Rod(Transform parent,string name,float radius,float length,Vector3 p,Material mat,int sides)
    {var t=Part(parent,name,FacilityPlayerArsenalAuthoring.Cylinder("M1911 "+name+" geometry",radius,length,sides),mat);t.localPosition=p;return t;}
    static Mesh Profile(string name,float width,Vector2[] profile)=>FacilityPlayerArsenalAuthoring.Extrusion(name,width,profile);
    static Mesh Save(string name,Mesh mesh)
    {
        mesh.name=name;string path=Root+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
        else{FacilityCharacterStyleAuthoring.CopyMesh(saved,mesh);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
        AssetDatabase.SaveAssetIfDirty(saved);return saved;
    }
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}

    static void Stamp(Transform parent,string text,Vector3 origin,float pixel)
    {
        var b=new MeshData();int column=0;
        foreach(char ch in text)
        {
            string glyph=Glyph(ch);
            for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(glyph[y*5+x]=='1')
            {
                float z=origin.z+(column*6+x)*pixel,yy=origin.y-y*Mathf.Abs(pixel);
                float s=Mathf.Abs(pixel)*.76f;
                b.Quad(new Vector3(origin.x,yy,z),new Vector3(origin.x,yy-s,z),new Vector3(origin.x,yy-s,z+Mathf.Abs(pixel)*.76f),new Vector3(origin.x,yy,z+Mathf.Abs(pixel)*.76f),false);
            }
            column++;
        }
        Part(parent,"M1911 recessed marking "+text,Save("M1911 engraved "+text.Replace('.','_')+" V1",b.Build()),matte);
    }
    static string Glyph(char c)
    {
        switch(c)
        {
            case 'M':return "10001110111010110101100011000110001";
            case 'N':return "10001110011010110011100011000110001";
            case 'D':return "11110100011000110001100011000111110";
            case '1':return "00100011000010000100001000010001110";
            case '9':return "01110100011000101111000010000101110";
            case '4':return "00010001100101010010111110001000010";
            case '5':return "11111100001111000001000011000101110";
            case 'A':return "01110100011000111111100011000110001";
            case 'U':return "10001100011000110001100011000101110";
            case 'T':return "11111001000010000100001000010000100";
            case 'O':return "01110100011000110001100011000101110";
            case '.':return "00000000000000000000000000011000110";
            default:return new string('0',35);
        }
    }
    sealed class MeshData
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
        public void Tri(Vector3 a,Vector3 b,Vector3 c,bool reverse=false)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);uv.Add(new Vector2(a.z*8,a.y*8));uv.Add(new Vector2(b.z*8,b.y*8));uv.Add(new Vector2(c.z*8,c.y*8));
            indices.Add(n);indices.Add(n+(reverse?2:1));indices.Add(n+(reverse?1:2));
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool reverse=false){Tri(a,b,c,reverse);Tri(a,c,d,reverse);}
        public Mesh Build(){var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
}
