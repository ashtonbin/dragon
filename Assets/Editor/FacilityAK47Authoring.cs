using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Editable, asset-backed AK with a milled receiver, laminated timber furniture and curved steel magazine.</summary>
public static class FacilityAK47Authoring
{
    public const string AssetRoot = "Assets/Generated/Arsenal/AK47";

    public static void Apply(Scene scene)
    {
        RetroFPSController player = FacilitySceneGenerator.FindInScene<RetroFPSController>(scene);
        if (player == null) throw new InvalidOperationException("The scene has no player for the AK-47.");
        FacilityWeaponInventory inventory = player.GetComponent<FacilityWeaponInventory>();
        if (inventory == null) inventory = FacilityPlayerArsenalAuthoring.Build(player);
        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera == null) throw new InvalidOperationException("The player has no weapon camera.");
        Build(camera, inventory);
    }

    public static FacilityAK47 Build(Camera camera, FacilityWeaponInventory inventory)
    {
        foreach (string folder in new[] { "Meshes", "Materials", "Textures", "Audio", "Prefabs" }) EnsureFolder(AssetRoot + "/" + folder);
        Transform previous = camera.transform.Find("AK-47");
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        FacilityAK47 weapon = camera.GetComponent<FacilityAK47>();
        if (weapon == null) weapon = camera.gameObject.AddComponent<FacilityAK47>();
        Material steel = Material("AK Phosphated Steel", new Color(.105f, .124f, .130f), .65f, .35f);
        Material edge = Material("AK Burnished Edges", new Color(.29f, .31f, .30f), .78f, .44f);
        Material dark = Material("AK Deep Recess", new Color(.009f, .013f, .014f), .15f, .12f);
        Material wood = Material("AK Oiled Amber Walnut", new Color(.62f, .30f, .12f), .02f, .35f, true);
        Material gripWood = Material("AK Dark Walnut Grip", new Color(.36f, .16f, .07f), .02f, .26f, true);
        Material grain = Material("AK Laminate Endgrain", new Color(.27f, .102f, .037f), .01f, .22f);
        Material brass = Material("AK Cartridge Brass", new Color(.61f, .40f, .14f), .82f, .44f);
        Material glove = Material("AK Worn Leather Gloves", new Color(.25f, .205f, .145f), .02f, .17f);
        Material seams = Material("AK Glove Seam", new Color(.105f, .09f, .062f), .01f, .15f);
        Material canvas = Material("AK Olive Canvas", new Color(.17f, .20f, .145f), .01f, .13f);
        Material ivory = Material("AK Sight Markings", new Color(.70f, .70f, .59f), .05f, .15f);
        Transform root = Model(camera.transform, "AK-47");
        root.localPosition = new Vector3(.24f, -.275f, .36f);
        root.localRotation = Quaternion.Euler(0f, -5f, 0f);
        Transform assembly = Model(root, "Rifle and hands - reload tilt");

        Piece(assembly, "Milled receiver with bevelled shoulders", Extrusion("Milled receiver V1", .068f, new[] {
            new Vector2(-.045f,-.175f), new Vector2(-.045f,.187f), new Vector2(.017f,.205f), new Vector2(.056f,.176f),
            new Vector2(.067f,-.126f),new Vector2(.034f,-.178f) }), steel);
        Piece(assembly, "Rounded stamped dust cover", Loft("Fluted dust cover V1", new[] {
            new Vector3(0f,.054f,-.166f),new Vector3(0f,.073f,-.137f),new Vector3(0f,.073f,.112f),new Vector3(0f,.054f,.164f)
        }, new[] {.030f,.034f,.034f,.028f}, new[] {.010f,.026f,.026f,.012f}, 12), steel);
        for (int rib = 0; rib < 6; rib++)
        {
            Transform band = Piece(assembly, "Dust cover pressed reinforcement " + (rib + 1), Tube("Dust cover raised band", .036f, .032f, .003f, 12), edge);
            band.localPosition = new Vector3(0f,.071f,-.125f + rib * .047f); band.localScale = new Vector3(1f,.78f,1f);
        }
        Box(assembly, "Dust cover locking button", new Vector3(0f,.076f,-.177f), new Vector3(.019f,.017f,.012f), edge);
        Box(assembly, "Right receiver long recessed action window", new Vector3(.0346f,.034f,.075f), new Vector3(.002f,.036f,.155f), dark);
        Transform bolt = Model(assembly, "Reciprocating bolt carrier and charging handle");
        Box(bolt, "Bolt carrier visible polished face", new Vector3(.036f,.028f,.072f), new Vector3(.003f,.025f,.087f), edge);
        Box(bolt, "Curved charging handle stem", new Vector3(.054f,.030f,.043f), new Vector3(.035f,.010f,.016f), steel);
        Box(bolt, "Charging handle knurled tip", new Vector3(.074f,.032f,.043f), new Vector3(.012f,.015f,.026f), edge);
        for (int i = 0; i < 4; i++) Box(bolt, "Charging handle grip groove " + i, new Vector3(.081f,.032f,.034f + i*.006f), new Vector3(.001f,.012f,.0015f), dark);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(assembly, "Receiver lower machined edge " + side, new Vector3(side*.0346f,-.037f,.010f), new Vector3(.002f,.004f,.324f), edge);
            for (int i = 0; i < 6; i++)
            {
                Transform rivet = Piece(assembly, "Receiver rivet " + side + " " + i, Cylinder("Domed receiver rivet",.0037f,.0028f,10), edge);
                rivet.localPosition = new Vector3(side*.035f,-.016f + (i%2)*.026f,-.140f + (i/2)*.130f);
                rivet.localRotation = Quaternion.Euler(0f,90f,0f);
            }
        }
        Transform safety = Box(assembly, "Long stamped safety selector", new Vector3(.037f,.002f,-.019f), new Vector3(.003f,.012f,.170f), steel);
        safety.localRotation = Quaternion.Euler(-9f,0f,0f);
        Box(assembly, "Selector thumb shelf", new Vector3(.044f,-.012f,.060f), new Vector3(.018f,.008f,.021f), edge);
        for (int i = 0; i < 3; i++) Box(assembly, "Selector engraved detent " + i, new Vector3(.036f,.038f-i*.011f,-.049f),new Vector3(.001f,.0012f,.018f),dark);

        // The fore-end has a separate lower laminate, upper gas-tube cover and the characteristic cooling slots.
        Piece(assembly, "Shaped lower walnut handguard", Loft("AK lower handguard V1",new[]{
            new Vector3(0f,-.007f,.188f),new Vector3(0f,-.005f,.224f),new Vector3(0f,-.008f,.360f),new Vector3(0f,-.004f,.399f)
        },new[]{.032f,.041f,.041f,.030f},new[]{.032f,.035f,.032f,.025f},10),wood);
        Piece(assembly, "Upper laminated gas tube furniture", Loft("AK upper handguard V1",new[]{
            new Vector3(0f,.060f,.202f),new Vector3(0f,.064f,.235f),new Vector3(0f,.064f,.355f),new Vector3(0f,.059f,.393f)
        },new[]{.024f,.029f,.029f,.021f},new[]{.020f,.025f,.025f,.018f},10),wood);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i=0;i<5;i++) Box(assembly,"Handguard cooling vent " + side + " " + i,new Vector3(side*.028f,.047f,.231f+i*.029f),new Vector3(.002f,.011f,.018f),dark);
            for (int i=0;i<4;i++)
            {
                Transform line=Box(assembly,"Walnut laminate grain line " + side+" "+i,new Vector3(side*.040f,-.004f-i*.006f,.295f),new Vector3(.0007f,.0007f,.116f),grain);
                line.localRotation=Quaternion.Euler(1.5f*i,0f,0f);
            }
        }
        for(int i=0;i<2;i++)
        {
            Transform ferrule=Piece(assembly,"Fore-end steel ferrule "+i,Tube("Handguard retaining ferrule",.040f,.034f,.013f,12),steel);
            ferrule.localPosition=new Vector3(0f,.001f,i==0?.197f:.390f);ferrule.localScale=new Vector3(1f,.86f,1f);
        }
        TubeAt(assembly,"Exposed cold hammer forged barrel",.0125f,.0041f,.405f,new Vector3(0f,.019f,.586f),steel);
        TubeAt(assembly,"Gas piston tube",.016f,.011f,.182f,new Vector3(0f,.068f,.472f),steel);
        Box(assembly,"Gas block tower",new Vector3(0f,.047f,.561f),new Vector3(.035f,.065f,.029f),steel);
        Transform bridge=Box(assembly,"Angled gas return",new Vector3(0f,.064f,.548f),new Vector3(.030f,.035f,.048f),edge);bridge.localRotation=Quaternion.Euler(-28f,0f,0f);
        for(int side=-1;side<=1;side+=2)
            for(int i=0;i<3;i++) Box(assembly,"Gas block relief port "+side+" "+i,new Vector3(side*.0178f,.054f,.551f+i*.007f),new Vector3(.001f,.006f,.004f),dark);
        TubeAt(assembly,"Barrel stepped front collar",.017f,.012f,.026f,new Vector3(0f,.019f,.711f),edge);
        TubeAt(assembly,"Threaded slanted muzzle brake",.0205f,.006f,.050f,new Vector3(0f,.019f,.796f),steel);
        Transform brake=Piece(assembly,"Slanted brake crown",Tube("AK angled brake crown",.021f,.006f,.008f,18),edge);brake.localPosition=new Vector3(0f,.019f,.823f);brake.localRotation=Quaternion.Euler(-18f,0f,0f);
        TubeAt(assembly,"Recessed dark rifled bore",.006f,.004f,.010f,new Vector3(0f,.019f,.807f),dark);
        for(int i=0;i<3;i++)Box(assembly,"Muzzle brake gas cut "+i,new Vector3(.019f,.018f,.784f+i*.010f),new Vector3(.002f,.009f,.005f),dark);
        TubeAt(assembly,"Underbarrel cleaning rod",.003f,.001f,.410f,new Vector3(0f,-.013f,.597f),edge);
        Box(assembly,"Front sight tower",new Vector3(0f,.058f,.726f),new Vector3(.025f,.071f,.029f),steel);
        Transform sightRing=Piece(assembly,"Protected front sight hood with open bore",Tube("Front sight protective hood",.026f,.021f,.013f,16),steel);sightRing.localPosition=new Vector3(0f,.104f,.726f);
        Box(assembly,"Front sight adjustment post",new Vector3(0f,.093f,.726f),new Vector3(.004f,.021f,.004f),edge);
        Box(assembly,"Rear tangent sight base",new Vector3(0f,.090f,.157f),new Vector3(.030f,.025f,.084f),steel);
        Transform ramp=Box(assembly,"Graduated rear sight ladder",new Vector3(0f,.108f,.153f),new Vector3(.023f,.005f,.078f),edge);ramp.localRotation=Quaternion.Euler(8f,0f,0f);
        for(int i=0;i<7;i++)Box(assembly,"Range ladder graduation "+i,new Vector3(0f,.113f,.125f+i*.009f),new Vector3(i%2==0?.014f:.009f,.001f,.001f),ivory);
        for(int side=-1;side<=1;side+=2)Box(assembly,"Rear sight open notch "+side,new Vector3(side*.010f,.121f,.119f),new Vector3(.008f,.013f,.009f),steel);

        Piece(assembly,"Contoured walnut shoulder stock",Extrusion("AK walnut stock V1",.067f,new[]{
            new Vector2(.036f,-.177f),new Vector2(.029f,-.231f),new Vector2(.012f,-.302f),new Vector2(.021f,-.505f),
            new Vector2(-.138f,-.520f),new Vector2(-.135f,-.418f),new Vector2(-.076f,-.271f),new Vector2(-.042f,-.187f)
        }),wood);
        Box(assembly,"Stock steel tang",new Vector3(0f,.031f,-.215f),new Vector3(.019f,.005f,.095f),steel);
        Transform butt=Box(assembly,"Ribbed steel buttplate",new Vector3(0f,-.055f,-.518f),new Vector3(.072f,.161f,.012f),steel);butt.localRotation=Quaternion.Euler(-5f,0f,0f);
        for(int i=0;i<8;i++)Box(butt,"Buttplate traction rib "+i,new Vector3(0f,-.061f+i*.017f,-.007f),new Vector3(.061f,.004f,.003f),edge);
        Piece(assembly,"Angled walnut pistol grip",Extrusion("AK sculpted pistol grip V1",.044f,new[]{
            new Vector2(-.032f,-.105f),new Vector2(-.038f,-.051f),new Vector2(-.155f,-.077f),new Vector2(-.170f,-.132f),new Vector2(-.142f,-.145f)
        }),gripWood);
        for(int side=-1;side<=1;side+=2)
        {
            for(int i=0;i<8;i++){Transform cut=Box(assembly,"Grip checkering "+side+" "+i,new Vector3(side*.0225f,-.068f-i*.009f,-.082f-i*.0025f),new Vector3(.001f,.0015f,.031f),grain);cut.localRotation=Quaternion.Euler(-12f,0f,0f);}
            Transform screw=Piece(assembly,"Stock sling loop "+side,Tube("Sling swivel loop",.012f,.008f,.004f,12),steel);screw.localPosition=new Vector3(side*.036f,-.090f,-.451f);screw.localRotation=Quaternion.Euler(0f,90f,0f);
        }
        Box(assembly,"Open trigger guard rear",new Vector3(0f,-.070f,-.063f),new Vector3(.012f,.051f,.009f),steel);
        Box(assembly,"Open trigger guard base",new Vector3(0f,-.095f,-.017f),new Vector3(.012f,.008f,.097f),steel);
        Transform frontGuard=Box(assembly,"Open trigger guard curved front",new Vector3(0f,-.070f,.030f),new Vector3(.012f,.049f,.009f),steel);frontGuard.localRotation=Quaternion.Euler(-12f,0f,0f);
        Transform trigger=Box(assembly,"Curved steel trigger",new Vector3(0f,-.068f,-.014f),new Vector3(.007f,.037f,.007f),edge);trigger.localRotation=Quaternion.Euler(-18f,0f,0f);
        Box(assembly,"Magazine release paddle",new Vector3(0f,-.067f,.054f),new Vector3(.027f,.026f,.013f),edge);
        Transform mag=Model(assembly,"Rocking 30 round curved steel magazine");mag.localPosition=new Vector3(0f,-.036f,.110f);
        Piece(mag,"Continuous curved magazine body",Extrusion("AK curved magazine body V1",.048f,new[]{
            new Vector2(.012f,-.058f),new Vector2(.012f,.058f),new Vector2(-.076f,.069f),new Vector2(-.157f,.096f),
            new Vector2(-.231f,.136f),new Vector2(-.273f,.160f),new Vector2(-.304f,.079f),new Vector2(-.254f,.043f),
            new Vector2(-.180f,.006f),new Vector2(-.089f,-.035f)
        }),steel);
        for(int side=-1;side<=1;side+=2)
        {
            for(int rib=0;rib<3;rib++)
                Piece(mag,"Curved stamped magazine rib "+side+" "+rib,Loft("AK mag rib "+side+" "+rib,new[]{
                    new Vector3(side*.0246f,-.025f,-.035f+rib*.032f),new Vector3(side*.0246f,-.100f,-.014f+rib*.033f),new Vector3(side*.0246f,-.177f,.018f+rib*.033f),new Vector3(side*.0246f,-.259f,.066f+rib*.031f)
                },new[]{.0028f,.003f,.003f,.0028f},new[]{.003f,.003f,.003f,.003f},6),edge);
            for(int i=0;i<5;i++)Box(mag,"Magazine spot weld "+side+" "+i,new Vector3(side*.0252f,-.037f-i*.047f,-.045f+i*.019f),new Vector3(.001f,.003f,.004f),dark);
        }
        Transform baseplate=Box(mag,"Magazine reinforced curved floorplate",new Vector3(0f,-.287f,.120f),new Vector3(.054f,.012f,.091f),edge);baseplate.localRotation=Quaternion.Euler(-24f,0f,0f);
        Box(mag,"Magazine front locking lug",new Vector3(0f,.014f,.057f),new Vector3(.025f,.012f,.014f),edge);
        Transform hand=Model(assembly,"Left support hand and canvas sleeve");
        AddHands(assembly,hand,glove,seams,canvas);

        Transform muzzle=Model(assembly,"AK muzzle");muzzle.localPosition=new Vector3(0f,.019f,.838f);
        Transform flash=Model(muzzle,"AK high pressure flame");
        Piece(flash,"Orange pressure star",Flash(),Material("AK Orange Muzzle Flame",new Color(1f,.49f,.08f),0f,0f,false,true));
        Transform core=Piece(flash,"White hot pressure core",Flash(),Material("AK White Hot Flame",new Color(1f,.95f,.74f),0f,0f,false,true));core.localScale=new Vector3(.43f,.43f,.65f);
        flash.gameObject.SetActive(false);
        Transform lightObject=Model(muzzle,"AK 26ms muzzle illumination");lightObject.localPosition=new Vector3(0f,.04f,-.09f);
        Light light=lightObject.gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=4.6f;light.intensity=3.2f;light.color=new Color(1f,.67f,.28f);light.shadows=LightShadows.None;light.lightmapBakeType=LightmapBakeType.Realtime;light.enabled=false;
        Transform ejection=Model(assembly,"AK brass ejection port");ejection.localPosition=new Vector3(.060f,.044f,.107f);
        weapon.aimCamera=camera;weapon.inventory=inventory;weapon.viewmodel=camera.GetComponent<RetroViewmodel>();
        weapon.surfaceImpacts=camera.GetComponent<FacilitySurfaceImpacts>();weapon.muzzle=muzzle;weapon.bolt=bolt;weapon.ejectionPort=ejection;
        weapon.reloadAssembly=assembly;weapon.magazine=mag;weapon.supportHand=hand;weapon.muzzleFlash=flash.gameObject;weapon.muzzleLight=light;
        weapon.casingPrefab=Casing(brass,dark);weapon.reportClip=Sound(false);weapon.reloadClip=Sound(true);
        weapon.reportSource=Source(camera.transform,"AK rifle report",.91f,weapon.reportClip,24);
        weapon.actionSource=Source(camera.transform,"AK magazine and bolt action",.58f,weapon.reloadClip,38);
        weapon.fireInterval=.10f;weapon.reloadDuration=2.2f;weapon.range=95f;
        inventory.ak47=weapon;inventory.ak47Model=root;root.gameObject.SetActive(false);
        EditorUtility.SetDirty(weapon);EditorUtility.SetDirty(inventory);AssetDatabase.SaveAssets();
        return weapon;
    }

    private static void AddHands(Transform assembly,Transform support,Material glove,Material seams,Material canvas)
    {
        Piece(assembly,"Right gloved palm around walnut grip",Loft("AK right gloved palm",new[]{new Vector3(.038f,-.050f,-.109f),new Vector3(.039f,-.102f,-.107f),new Vector3(.030f,-.160f,-.119f)},new[]{.024f,.029f,.025f},new[]{.023f,.026f,.024f},9),glove);
        for(int finger=0;finger<3;finger++)
        {
            float y=-.080f-finger*.024f,z=-.068f-finger*.006f;
            Piece(assembly,"Right curled grip finger "+finger,Loft("AK right finger "+finger,new[]{new Vector3(.046f,y,z-.025f),new Vector3(.027f,y,z),new Vector3(-.013f,y,z),new Vector3(-.027f,y,z-.020f)},new[]{.010f,.011f,.010f,.007f},new[]{.011f,.011f,.010f,.007f},7),glove);
        }
        Piece(assembly,"Right index on trigger",Loft("AK trigger index",new[]{new Vector3(.046f,-.058f,-.087f),new Vector3(.035f,-.063f,-.029f),new Vector3(.013f,-.067f,-.008f),new Vector3(.005f,-.066f,-.014f)},new[]{.010f,.009f,.008f,.006f},new[]{.010f,.009f,.008f,.006f},7),glove);
        Piece(assembly,"Right thumb bracing receiver",Loft("AK right thumb",new[]{new Vector3(.027f,-.048f,-.135f),new Vector3(-.018f,-.042f,-.126f),new Vector3(-.030f,-.055f,-.085f)},new[]{.013f,.012f,.009f},new[]{.012f,.011f,.008f},8),glove);
        Piece(assembly,"Right tapered canvas forearm",Loft("AK right canvas forearm",new[]{new Vector3(.25f,-.38f,-.28f),new Vector3(.115f,-.235f,-.197f),new Vector3(.031f,-.167f,-.125f)},new[]{.058f,.047f,.032f},new[]{.051f,.041f,.029f},10),canvas);
        Piece(assembly,"Right glove cuff",Loft("AK right glove cuff",new[]{new Vector3(.052f,-.185f,-.144f),new Vector3(.029f,-.153f,-.119f)},new[]{.036f,.032f},new[]{.031f,.029f},9),seams);
        Piece(support,"Left palm under walnut fore-end",Loft("AK left support palm",new[]{new Vector3(-.035f,-.046f,.280f),new Vector3(-.023f,-.061f,.316f),new Vector3(.020f,-.051f,.328f)},new[]{.029f,.035f,.029f},new[]{.022f,.027f,.021f},9),glove);
        for(int finger=0;finger<4;finger++)
        {
            float z=.265f+finger*.023f;
            Piece(support,"Left hand curled support finger "+finger,Loft("AK support finger "+finger,new[]{new Vector3(-.048f,-.047f,z),new Vector3(-.047f,-.010f,z),new Vector3(-.031f,.018f,z),new Vector3(-.015f,.020f,z)},new[]{.010f,.011f,.009f,.006f},new[]{.010f,.010f,.009f,.006f},7),glove);
        }
        Piece(support,"Support thumb along handguard",Loft("AK support thumb",new[]{new Vector3(.020f,-.041f,.278f),new Vector3(.043f,-.017f,.298f),new Vector3(.043f,.010f,.330f)},new[]{.013f,.012f,.008f},new[]{.012f,.012f,.007f},8),glove);
        Piece(support,"Left tapered canvas forearm",Loft("AK left canvas forearm",new[]{new Vector3(-.29f,-.38f,-.15f),new Vector3(-.160f,-.196f,.088f),new Vector3(-.038f,-.063f,.279f)},new[]{.063f,.049f,.032f},new[]{.052f,.041f,.029f},10),canvas);
        Piece(support,"Left stitched glove cuff",Loft("AK left glove cuff",new[]{new Vector3(-.065f,-.093f,.238f),new Vector3(-.035f,-.059f,.280f)},new[]{.037f,.033f},new[]{.032f,.029f},9),seams);
        for(int i=0;i<4;i++)Box(support,"Support glove knuckle stitching "+i,new Vector3(-.052f,-.002f,.265f+i*.023f),new Vector3(.001f,.011f,.006f),seams);
    }

    private static GameObject Casing(Material brass,Material dark)
    {
        string path=AssetRoot+"/Prefabs/Spent 7.62x39 Casing.prefab";
        GameObject saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(saved!=null)return saved;
        Transform casing=Model(null,"Spent 7.62x39 brass");casing.gameObject.layer=2;
        Piece(casing,"Tapered cartridge body",Loft("AK brass casing body",new[]{new Vector3(0,0,-.019f),new Vector3(0,0,.007f),new Vector3(0,0,.014f),new Vector3(0,0,.019f)},new[]{.0056f,.0052f,.0038f,.0038f},new[]{.0056f,.0052f,.0038f,.0038f},10),brass);
        TubeAt(casing,"Open spent cartridge mouth",.0039f,.0032f,.004f,new Vector3(0,0,.020f),brass);
        TubeAt(casing,"Extractor rim",.0061f,.003f,.0025f,new Vector3(0,0,-.019f),brass);
        Transform primer=Piece(casing,"Dark struck primer",Cylinder("AK spent primer",.0019f,.001f,10),dark);primer.localPosition=new Vector3(0,0,-.0206f);
        CapsuleCollider collider=casing.gameObject.AddComponent<CapsuleCollider>();collider.direction=2;collider.radius=.006f;collider.height=.045f;
        Rigidbody body=casing.gameObject.AddComponent<Rigidbody>();body.mass=.008f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
        saved=PrefabUtility.SaveAsPrefabAsset(casing.gameObject,path);Object.DestroyImmediate(casing.gameObject);return saved;
    }

    private static AudioSource Source(Transform parent,string name,float volume,AudioClip clip,int priority)
    {
        Transform child=parent.Find(name);if(child==null)child=Model(parent,name);
        AudioSource source=child.GetComponent<AudioSource>();if(source==null)source=child.gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;source.spatialBlend=0f;source.volume=volume;source.priority=priority;source.clip=clip;return source;
    }

    private static AudioClip Sound(bool reload)
    {
        string path=AssetRoot+"/Audio/"+(reload?"AK Rock Lock Rack V1":"AK 762 Concussion V1")+".wav";
        AudioClip saved=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(saved!=null)return saved;
        const int rate=44100;float duration=reload?2.2f:1.12f;float[] samples=new float[Mathf.CeilToInt(rate*duration)];
        uint seed=reload?351891u:973901u;double low=0,mid=0;float peak=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)rate;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;double noise=(seed&0xffffff)/8388608.0-1.0;
            low+=(noise-low)*.018;mid+=(noise-mid)*.22;double value;
            if(!reload)
            {
                // An abrupt supersonic crack over a falling pressure body; short room reflections remain audible between rounds.
                double attack=1-Math.Exp(-t*6000);
                double crack=(noise-mid)*.43*Math.Exp(-t*145);
                double body=Math.Sin(2*Math.PI*(74*t+2.35*(1-Math.Exp(-t*61))))*.90*Math.Exp(-t*24);
                double pressure=(low*5.3+mid*.75)*Math.Exp(-t*29);
                value=(crack+body+pressure)*attack;
                if(t>.038)value+=mid*.64*Math.Exp(-(t-.038)*36);
                if(t>.083)value+=low*1.35*Math.Exp(-(t-.083)*18);
                if(t>.158)value+=low*.72*Math.Exp(-(t-.158)*11);
                // A dry steel action slap gives the automatic rhythm a mechanical layer.
                if(t>.053&&t<.095)value+=(noise*.12+Math.Sin(2*Math.PI*1760*t)*.08)*Math.Exp(-(t-.053)*160);
                value=Math.Tanh(value*1.15);
            }
            else
            {
                value=0;
                double[] times={.06,.24,1.16,1.48,1.79,1.93};
                for(int p=0;p<times.Length;p++)
                {
                    double age=t-times[p];if(age<0||age>.18)continue;
                    double strength=p==3?.88:p>=4?.82:.48;
                    double frequency=p>=4?1270:p==3?380:790;
                    value+=(mid*2.0+Math.Sin(2*Math.PI*frequency*age)*.31+low*1.3)*Math.Exp(-age*(p>=4?58:45))*(1-Math.Exp(-age*4000))*strength;
                }
                if(t>.24&&t<.55)value+=mid*.10*Math.Sin((t-.24)/.31*Math.PI);
                if(t>1.68&&t<1.89)value+=(mid*.25+noise*.025)*Math.Sin((t-1.68)/.21*Math.PI);
            }
            samples[i]=(float)value;peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
        }
        using(FileStream stream=File.Create(path))using(BinaryWriter writer=new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
            for(int i=0;i<samples.Length;i++)writer.Write((short)(Mathf.Clamp(samples[i]/Mathf.Max(.001f,peak)*.88f*Mathf.Clamp01((samples.Length-i)/300f),-1,1)*32767));
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);AudioImporter importer=(AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static Material Material(string name,Color color,float metallic,float smoothness,bool timber=false,bool unlit=false)
    {
        string path=AssetRoot+"/Materials/"+name+".mat";Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);if(!unlit){material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);material.SetColor("_EmissionColor",color*.085f);material.EnableKeyword("_EMISSION");material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;}
        if(timber){material.SetTexture("_BaseMap",Walnut());material.SetTextureScale("_BaseMap",new Vector2(1.4f,1.0f));}
        EditorUtility.SetDirty(material);return material;
    }

    private static Texture2D Walnut()
    {
        string path=AssetRoot+"/Textures/Amber Walnut Grain V1.png";Texture2D saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(saved!=null)return saved;
        Texture2D texture=new Texture2D(256,256,TextureFormat.RGB24,false);Color[] pixels=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float u=x/256f,v=y/256f;float warp=Mathf.PerlinNoise(u*3f,v*2f)*2f+Mathf.PerlinNoise(u*12f,v*4f)*.22f;
            float grain=Mathf.Pow(Mathf.Abs(Mathf.Sin((v*64f+warp)*Mathf.PI)),11f);float pores=Mathf.PerlinNoise(u*95f,v*170f);
            float light=.80f+Mathf.PerlinNoise(u*4f,v*9f)*.21f-grain*.27f-pores*.07f;
            pixels[y*256+x]=new Color(light,light*.90f,light*.75f);
        }
        texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Transform Model(Transform parent,string name){Transform transform=new GameObject(name).transform;transform.SetParent(parent,false);return transform;}
    private static Transform Piece(Transform parent,string name,Mesh mesh,Material material)
    {
        Transform piece=Model(parent,name);piece.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=piece.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;Camera camera=parent.GetComponentInParent<Camera>();if(camera!=null)renderer.probeAnchor=camera.transform;return piece;
    }
    private static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        Transform piece=Piece(parent,name,Extrusion("AK unit box",1f,new[]{new Vector2(-.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,-.5f)}),material);piece.localPosition=position;piece.localScale=size;return piece;
    }
    private static void TubeAt(Transform parent,string name,float outer,float inner,float length,Vector3 position,Material material){Transform piece=Piece(parent,name,Tube(name,outer,inner,length,16),material);piece.localPosition=position;}
    private static Mesh Extrusion(string name,float width,Vector2[] profile)
    {
        Mesh saved=Load(name);if(saved!=null)return saved;Builder builder=new Builder();
        for(int i=0;i<profile.Length;i++){Vector2 a=profile[i],b=profile[(i+1)%profile.Length];builder.Quad(new Vector3(-width*.5f,a.x,a.y),new Vector3(width*.5f,a.x,a.y),new Vector3(width*.5f,b.x,b.y),new Vector3(-width*.5f,b.x,b.y));}
        float area=0;for(int i=0;i<profile.Length;i++){Vector2 a=profile[i],b=profile[(i+1)%profile.Length];area+=a.x*b.y-b.x*a.y;}float winding=Mathf.Sign(area);List<int> polygon=new List<int>();for(int i=0;i<profile.Length;i++)polygon.Add(i);int safety=profile.Length*profile.Length;
        while(polygon.Count>2&&safety-->0)
        {
            bool clipped=false;
            for(int i=0;i<polygon.Count;i++)
            {
                int ia=polygon[(i+polygon.Count-1)%polygon.Count],ib=polygon[i],ic=polygon[(i+1)%polygon.Count];if(Cross(profile[ib]-profile[ia],profile[ic]-profile[ib])*winding<=0)continue;bool contains=false;
                foreach(int q in polygon){if(q==ia||q==ib||q==ic)continue;Vector2 p=profile[q];if(Cross(profile[ib]-profile[ia],p-profile[ia])*winding>=0&&Cross(profile[ic]-profile[ib],p-profile[ib])*winding>=0&&Cross(profile[ia]-profile[ic],p-profile[ic])*winding>=0){contains=true;break;}}
                if(contains)continue;Vector3 a=new Vector3(width*.5f,profile[ia].x,profile[ia].y),b=new Vector3(width*.5f,profile[ib].x,profile[ib].y),c=new Vector3(width*.5f,profile[ic].x,profile[ic].y);if(winding>0)builder.Triangle(a,b,c);else builder.Triangle(c,b,a);a.x=b.x=c.x=-width*.5f;if(winding>0)builder.Triangle(c,b,a);else builder.Triangle(a,b,c);polygon.RemoveAt(i);clipped=true;break;
            }
            if(!clipped)throw new InvalidOperationException("Could not triangulate AK shape "+name);
        }
        return Save(name,builder.Build(name));
    }
    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    private static Mesh Cylinder(string name,float radius,float length,int sides)
    {
        Mesh saved=Load(name);if(saved!=null)return saved;Builder builder=new Builder();for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Vector3 p=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-length*.5f),q=new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,-length*.5f),z=Vector3.forward*length;builder.Quad(p,q,q+z,p+z);builder.Triangle(Vector3.forward*length*.5f,p+z,q+z);builder.Triangle(Vector3.back*length*.5f,q,p);}return Save(name,builder.Build(name));
    }
    private static Mesh Tube(string name,float outer,float inner,float length,int sides)
    {
        Mesh saved=Load(name);if(saved!=null)return saved;Builder builder=new Builder();for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Vector3 p=new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,-length*.5f),q=new Vector3(Mathf.Cos(b)*outer,Mathf.Sin(b)*outer,-length*.5f),r=new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,-length*.5f),s=new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner,-length*.5f),z=Vector3.forward*length;builder.Quad(p,q,q+z,p+z);builder.Quad(r+z,s+z,s,r);builder.Quad(p+z,q+z,s+z,r+z);builder.Quad(p,r,s,q);}return Save(name,builder.Build(name));
    }
    private static Mesh Loft(string name,Vector3[] centres,float[] widths,float[] heights,int sides)
    {
        Mesh saved=Load(name);if(saved!=null)return saved;Builder builder=new Builder();Vector3[,] rings=new Vector3[centres.Length,sides];
        for(int ring=0;ring<centres.Length;ring++){Vector3 tangent=(centres[Mathf.Min(ring+1,centres.Length-1)]-centres[Mathf.Max(0,ring-1)]).normalized;Vector3 across=Vector3.Cross(tangent,Vector3.up);if(across.sqrMagnitude<.001f)across=Vector3.right;across.Normalize();Vector3 depth=Vector3.Cross(tangent,across).normalized;for(int i=0;i<sides;i++){float angle=i*Mathf.PI*2/sides;rings[ring,i]=centres[ring]+across*(Mathf.Cos(angle)*widths[ring])+depth*(Mathf.Sin(angle)*heights[ring]);}}
        for(int ring=0;ring<centres.Length-1;ring++)for(int i=0;i<sides;i++)builder.Quad(rings[ring,i],rings[ring,(i+1)%sides],rings[ring+1,(i+1)%sides],rings[ring+1,i]);for(int i=0;i<sides;i++){int next=(i+1)%sides,last=centres.Length-1;builder.Triangle(centres[0],rings[0,next],rings[0,i]);builder.Triangle(centres[last],rings[last,i],rings[last,next]);}return Save(name,builder.Build(name));
    }
    private static Mesh Flash()
    {
        const string name="AK pressure flash V1";Mesh saved=Load(name);if(saved!=null)return saved;Builder builder=new Builder();Vector3 centre=new Vector3(0,0,.020f);
        for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12,b=(i+1)*Mathf.PI*2/12;Vector3 p=centre+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(i%2==0?.125f:.029f),q=centre+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*((i+1)%2==0?.125f:.029f);builder.Triangle(centre,p,q);builder.Triangle(centre,q,p);}
        for(int i=0;i<2;i++){Vector3 side=(i==0?Vector3.up:Vector3.right)*.042f,tip=Vector3.forward*.24f;builder.Triangle(-side,side,tip);builder.Triangle(tip,side,-side);}return Save(name,builder.Build(name));
    }
    private static Mesh Load(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(AssetRoot+"/Meshes/"+name+".asset");
    private static Mesh Save(string name,Mesh mesh){AssetDatabase.CreateAsset(mesh,AssetRoot+"/Meshes/"+name+".asset");return mesh;}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private sealed class Builder
    {
        private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<int> triangles=new List<int>();private readonly List<Vector2> uv=new List<Vector2>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);uv.Add(new Vector2(a.z*5,a.y*5));uv.Add(new Vector2(b.z*5,b.y*5));uv.Add(new Vector2(c.z*5,c.y*5));triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        public Mesh Build(string name){Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
}
