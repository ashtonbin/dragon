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

/// <summary>Reference-led, editable Challenger bodywork. Authoring only; never generates a car in gameplay.</summary>
public static class FacilityHellcatAuthoring
{
    public const string Root = "Assets/Generated/Hellcat";
    static Material silver, striped, black, rubber, glass, seal;
    static int revision = 1;

    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Leave Play mode and baking before replacing the getaway car.");
        string original = SceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        Scene scene = SceneManager.GetActiveScene();
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if (director == null) scene = EditorSceneManager.OpenScene("Assets/Scenes/StoryLevel01.unity", OpenSceneMode.Single);
        director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if (director == null || director.sedanExit == null || director.drillPickup == null)
            throw new InvalidOperationException("The bank's extraction and equipment references are required.");
        string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeHellcat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not save the car recovery scene.");
        Build(director);
        FacilityStoryInstaller.RebuildNavigation(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the Challenger.");
        string report = FacilityHellcatValidation.Validate(scene);
        FacilityHellcatValidation.Capture(scene);
        if (original != scene.path) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        return report + " Reference-led silver widebody Challenger, functional trunk and persistent assets saved. Recovery: " + backup;
    }

    public static void Build(FacilityStoryDirector director)
    {
        Prepare();
        EnablePlayerShadows(director);
        Transform car = director.sedanExit.transform.parent;
        if (car == null) throw new InvalidOperationException("Extraction has no vehicle parent.");
        FacilityDoor trunk = director.drillPickup.door;
        if (trunk == null || trunk.leaf == null) throw new InvalidOperationException("The working trunk is missing.");
        Transform equipment = director.drillPickup.visualRoot.transform;
        FacilityStoryInteractable trunkTarget = trunk.interaction != null ? trunk.interaction :
            trunk.GetComponentsInChildren<FacilityStoryInteractable>(true).FirstOrDefault(i => i.kind == FacilityStoryInteractionKind.Door && i.door == trunk);
        if(trunkTarget==null)
        {
            Transform target=Group(trunk.leaf,"Challenger trunk latch - open or close");
            target.gameObject.AddComponent<BoxCollider>().isTrigger=true;
            trunkTarget=target.gameObject.AddComponent<FacilityStoryInteractable>();
        }
        trunkTarget.kind=FacilityStoryInteractionKind.Door;trunkTarget.director=director;trunkTarget.door=trunk;trunk.interaction=trunkTarget;
        Transform interaction = trunkTarget.transform;
        var kept = new HashSet<Transform> { director.sedanExit.transform, director.drillPickup.transform, equipment, trunk.transform };
        foreach (Transform child in car.Cast<Transform>().ToArray())
            if (!kept.Contains(child)) Object.DestroyImmediate(child.gameObject);
        foreach (Transform child in trunk.leaf.Cast<Transform>().ToArray())
            if (child != interaction) Object.DestroyImmediate(child.gameObject);
        foreach (var collider in trunk.leaf.GetComponents<Collider>()) Object.DestroyImmediate(collider);
        car.localScale = Vector3.one;
        Transform body = Group(car, "SRT Hellcat Challenger - sculpted silver widebody");
        BuildSideBody(body);
        BuildHood(body);
        BuildCabin(body);
        BuildRearDeck(body, trunk, interaction);
        BuildUnderside(body);
        FacilityHellcatWheels.Build(car);
        FacilityHellcatDetails.Build(car, silver, striped);
        FacilityHellcatDoorAuthoring.Build(car, director, silver, glass, seal, black);
        for(int side=-1;side<=1;side+=2)foreach(float axle in new[]{-1.48f,1.48f})
            Box(body,"Stationary tire collision "+side+" "+axle,new Vector3(side*.865f,.39f,axle),new Vector3(.315f,.73f,.74f),null,true);
        foreach (Transform item in car.GetComponentsInChildren<Transform>(true))
            if (item.name == "Hellcat rear deck spoiler") { item.SetParent(trunk.leaf, true); break; }
        if (director.drillPickup.visualRoot != null)
        {
            equipment.localPosition = Vector3.zero; equipment.localRotation = Quaternion.identity;
            // The existing pistol and drill remain together in the lined trunk, below the closed deck.
            director.drillPickup.transform.localPosition = new Vector3(0f, .79f, -1.84f);
            BoxCollider pickup = director.drillPickup.GetComponent<BoxCollider>();
            if (pickup != null) { pickup.center = Vector3.zero; pickup.size = new Vector3(.83f, .30f, .55f); }
            director.drillPickup.label = "Collect car equipment";
        }
        director.sedanExit.transform.localPosition = new Vector3(1.53f, .82f, -.15f);
        BoxCollider exitTarget = director.sedanExit.GetComponent<BoxCollider>();
        if (exitTarget != null) { exitTarget.center = Vector3.zero; exitTarget.size = new Vector3(.25f, .35f, .35f); }
        director.sedanExit.label = "Get into the driver's seat";
        if (director.spawnPoint != null) director.spawnPoint.position = car.TransformPoint(new Vector3(2.05f, .05f, 0f));
        AddSceneReflection(car);
        Transform lamps=Group(car,"Challenger warm projector headlight beams");
        for(int side=-1;side<=1;side+=2)
        {
            Transform lamp=Group(lamps,"Hellcat low beam "+side);lamp.localPosition=new Vector3(side*.82f,.795f,2.54f);lamp.localRotation=Quaternion.Euler(6f,side*2.5f,0);
            Light light=lamp.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.lightmapBakeType=LightmapBakeType.Realtime;light.color=new Color(1f,.94f,.82f);light.intensity=4.5f;light.range=19f;light.spotAngle=49f;light.innerSpotAngle=27f;light.shadows=LightShadows.Soft;light.shadowBias=.025f;light.shadowNormalBias=.12f;light.renderMode=LightRenderMode.ForcePixel;
        }
        BuildCourtesyLights(car, trunk);
        ConfigureParkingLamp(car);
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        {
            renderer.gameObject.layer=29;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            renderer.receiveShadows = true;
            renderer.probeAnchor = null;
            EditorUtility.SetDirty(renderer);
        }
        // This replacement remains an editable prop. Only its explicitly fitted collision hulls enter navigation.
        foreach (Transform child in car.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.OccludeeStatic);
        trunk.leafColliders = trunk.leaf.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
        trunk.npcCanOpen = false;
        FacilityVehicleDepartureAuthoring.Apply(director.gameObject.scene);
        EditorUtility.SetDirty(trunk); EditorUtility.SetDirty(director); EditorUtility.SetDirty(director.drillPickup);
        Physics.SyncTransforms();
    }

    static void Prepare()
    {
        foreach (string folder in new[] { "Meshes", "Materials", "Textures", "Reflections" }) Ensure(Root + "/" + folder);
        silver = Mat("Challenger - billet silver metallic clearcoat", new Color(.74f, .77f, .80f), .52f, .85f);
        striped = Mat("Challenger - silver with twin satin carbon stripes", new Color(.74f, .77f, .80f), .52f, .85f);
        striped.SetTexture("_BaseMap", StripeTexture());
        striped.SetTexture("_MetallicGlossMap", StripeMetallic()); striped.EnableKeyword("_METALLICSPECGLOSSMAP");
        black = Mat("Challenger - satin black aero composite", new Color(.012f, .016f, .020f), .22f, .38f);
        rubber = Mat("Challenger - charcoal trunk carpeting", new Color(.029f, .032f, .035f), 0f, .12f);
        seal = Mat("Challenger - window and panel rubber", new Color(.008f, .010f, .012f), .02f, .31f);
        glass = Mat("Challenger - smoked solar control glazing", new Color(.065f, .10f, .13f, .55f), .18f, .96f);
        glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 1f);
        glass.SetFloat("_SrcBlend", (float)BlendMode.One); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0f); glass.SetFloat("_Cull", 0f);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.EnableKeyword("_ALPHAPREMULTIPLY_ON"); glass.renderQueue = 3000;
        glass.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(glass); EditorUtility.SetDirty(striped);
    }

    static float Width(float z)
    {
        float cornerShoulder=.060f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.02f,2.18f,Mathf.Abs(z)));
        return .944f+cornerShoulder;
    }
    internal static float Shoulder(float z) => 1.014f + .035f * Mathf.Exp(-Mathf.Pow((z + 1.46f) / .62f, 2f)) + .018f * Mathf.Exp(-Mathf.Pow((z - 1.45f) / .65f, 2f));
    static float ArchBottom(float z)
    {
        float result = .255f;
        foreach (float axle in new[] { -1.48f, 1.48f })
        {
            float d = Mathf.Abs(z - axle);
            if (d < .454f) result = Mathf.Max(result, .39f + Mathf.Sqrt(.454f * .454f - d * d));
        }
        return result;
    }
    internal static Vector3 Side(float side, float z, float t)
    {
        float endRoll=.045f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.18f,2.50f,Mathf.Abs(z)));
        float y = Mathf.Lerp(ArchBottom(z), Shoulder(z)-endRoll, t);
        float heightT = Mathf.InverseLerp(.25f, 1.045f, y);
        float x = Width(z) - .036f * Mathf.Pow(1f - heightT, 2f);
        x += .030f * Mathf.Sin(heightT * Mathf.PI);
        x += .020f * Mathf.Exp(-Mathf.Pow((Mathf.Abs(z)-1.48f)/.62f,4f))*Mathf.Sin(heightT*Mathf.PI*.85f);
        x -= .016f * Mathf.Exp(-Mathf.Pow((heightT - .63f) / .095f, 2f));
        if(Mathf.Abs(z)>2.18f)
        {
            float angle=Mathf.Clamp01((Mathf.Abs(z)-2.18f)/.32f)*Mathf.PI*.5f;
            x-=.100f*(1-Mathf.Cos(angle));
            z=Mathf.Sign(z)*(2.18f+.270f*Mathf.Sin(angle));
        }
        return new Vector3(side * x, y, z);
    }

    static void BuildSideBody(Transform body)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            int sign = side;
            foreach(Vector2 range in new[]{new Vector2(-2.50f,-.89f),new Vector2(.78f,2.50f)})
                Node(body,"Fixed silver quarter outside real door aperture "+side+" "+range.x,
                    Grid("Fixed curved quarter with door cutout "+side+" "+range.x,96,28,
                    (u,v)=>Side(sign,Mathf.Lerp(range.x,range.y,u),v),(p,u,v)=>new Vector2(p.z*.2f,p.y),sign>0),silver);
            Node(body,"Fixed silver sill below opening "+side,Grid("Low door aperture sill "+side,84,4,
                (u,v)=>Side(sign,Mathf.Lerp(-.89f,.78f,u),v*.070f),null,sign>0),silver);
            // The top of the shoulder rolls into the glazing and the long hood rather than ending as a box edge.
            foreach(Vector2 range in new[]{new Vector2(-2.40f,-.89f),new Vector2(.78f,2.40f)})
            Node(body, "Fixed shoulder crown outside door " + side+" "+range.x, Grid("Fixed shoulder crown with door opening " + side+" "+range.x, 80, 12, (u, v) =>
            {
                Vector3 outside=Side(sign,Mathf.Lerp(range.x,range.y,u),1);
                float z=outside.z;
                float x = Mathf.Lerp(Mathf.Abs(outside.x), .836f, v);
                float inside=z>.91f?HoodHeight(.836f,z):z<-1.47f?Mathf.Lerp(1.036f,1.019f,Mathf.InverseLerp(-1.47f,-2.4f,z)):1.094f;
                if(z>2.387f)inside=Mathf.Lerp(HoodHeight(.836f,2.39f),.950f,Mathf.InverseLerp(2.387f,2.512f,z));
                if(z< -2.41f)inside=Mathf.Lerp(1.019f,.970f,Mathf.InverseLerp(2.41f,2.515f,-z));
                float y = Mathf.Lerp(outside.y,inside,v)+.008f*Mathf.Sin(v*Mathf.PI);
                return new Vector3(sign * x, y, z);
            }, null, sign > 0), silver);
            foreach (float axle in new[] { -1.48f, 1.48f })
            {
                float center = axle;
                Node(body, "Molded widebody arch flare " + side + " " + axle,
                    Grid("Swept widebody flare " + side + " " + axle, 92, 12, (u, v) =>
                    {
                        float angle = Mathf.Lerp(-17f, 197f, u) * Mathf.Deg2Rad;
                        float radius = Mathf.Lerp(.452f, .543f, v);
                        float z = center + Mathf.Cos(angle) * radius;
                        float y = .39f + Mathf.Sin(angle) * radius;
                        float bulge = .008f + .088f * Mathf.Sin(Mathf.Pow(1f - v, .65f) * Mathf.PI * .5f);
                        float x = Width(z) + bulge;
                        return new Vector3(sign * x, y, z);
                    }, null, sign < 0), silver);
                var lip = new List<Vector3>();
                for (int a = 0; a <= 92; a++)
                {
                    float angle = Mathf.Lerp(-17, 197, a / 92f) * Mathf.Deg2Rad;
                    lip.Add(new Vector3(sign * 1.028f, .39f + Mathf.Sin(angle) * .452f, center + Mathf.Cos(angle) * .452f));
                }
                Tube(body, "Rolled inner arch lip " + side + " " + axle, lip, .0065f, black, 6);
                Node(body, "Shadowed wheel well " + side + " " + axle,
                    Grid("Inner wheel well liner " + side + " " + axle, 76, 4, (u, v) =>
                    {
                        float a = Mathf.Lerp(-18, 198, u) * Mathf.Deg2Rad;
                        return new Vector3(sign * Mathf.Lerp(.79f, 1.025f, v), .39f + Mathf.Sin(a) * .449f, center + Mathf.Cos(a) * .449f);
                    }, null, sign < 0), seal);
            }
            // The separately hinged long door is authored after its interior and exterior trim.
            Node(body, "Contoured black rocker extension " + side,
                Grid("Rocker aero sill " + side, 48, 8, (u, v) =>
                {
                    float z = Mathf.Lerp(-.995f, .995f, u);
                    float x = sign * (.918f + .054f * Mathf.Sin(v * Mathf.PI * .75f));
                    return new Vector3(x, Mathf.Lerp(.20f, .305f, v), z);
                }, null, sign > 0), black);
        }
    }

    static float HoodHeight(float x, float z)
    {
        float t = Mathf.InverseLerp(.91f, 2.39f, z);
        float center = Mathf.Lerp(1.070f, 1.010f, t);
        return center + .018f * (1f - x*x/.74f) + .021f * Mathf.Exp(-Mathf.Pow((Mathf.Abs(x) - .46f) / .17f, 2f)) * Mathf.Sin(t * Mathf.PI);
    }

    static void BuildHood(Transform body)
    {
        Node(body, "Long twin-striped sculpted aluminum hood", Grid("Continuous crowned hood", 84, 96, (u, v) =>
        {
            float z = Mathf.Lerp(.93f, 2.39f, v); float width = Mathf.Lerp(.827f, .857f, v);
            float x = Mathf.Lerp(-width, width, u); return new Vector3(x, HoodHeight(x, z), z);
        }, StripeUV, true), striped);
        Node(body,"Rolled silver nose and striped brow",Grid("Continuous curved nose brow",80,20,(u,v)=>
        {
            float x=(u*2-1)*Mathf.Lerp(.857f,.948f,v);
            float y=Mathf.Lerp(HoodHeight(x,2.39f),.955f,v)-.009f*Mathf.Pow(u*2-1,4)*v;
            return new Vector3(x,y,Mathf.Lerp(2.387f,2.512f,v));
        },StripeUV,true),striped);
        for (int side = -1; side <= 1; side += 2)
        {
            int s = side;
            var shut = new List<Vector3>();
            for (int i = 0; i <= 60; i++)
            {
                float z = Mathf.Lerp(.94f, 2.385f, i / 60f); float x = s * Mathf.Lerp(.829f, .859f, i / 60f);
                shut.Add(new Vector3(x, HoodHeight(x, z) + .001f, z));
            }
            Tube(body, "Hood side panel gap " + side, shut, .0028f, seal, 5);
            float at = side * .435f;
            Node(body, "Dual nostril hood scoop formed shell " + side,
                Grid("Formed hood intake shell " + side, 36, 40, (u, v) =>
                {
                    float z = Mathf.Lerp(1.20f, 1.85f, v);
                    float width = Mathf.Lerp(.095f, .162f, Mathf.SmoothStep(0, 1, v));
                    float across = u * 2f - 1f;
                    float x = at + across * width;
                    float h = .079f * Mathf.SmoothStep(0, 1, v) * Mathf.Pow(Mathf.Max(0f, 1f - across * across), .42f);
                    return new Vector3(x, HoodHeight(x,z) + .002f + h, z);
                }, StripeUV, true), striped);
            var mouth = new List<Vector3>();
            for (int i = 0; i <= 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2;
                float x = at + Mathf.Cos(a) * .143f;
                float y = HoodHeight(at,1.848f) + .039f + Mathf.Sin(a) * .030f;
                mouth.Add(new Vector3(x,y,1.851f));
            }
            Tube(body, "Rolled scoop intake silver lip " + side, mouth, .005f, silver, 6);
            Node(body, "Recessed oval intake shadow " + side, Ellipse("Hood intake dark recess " + side, at, HoodHeight(at,1.848f)+.039f, 1.843f, .143f,.030f), seal);
            // Tiny three-dimensional intake perforations remain visible on close inspection.
            var slats = new List<Vector3[]>();
            for(int row=0;row<3;row++) for(int col=0;col<17;col++)
            {
                float x=at-.123f+col*.0153f, y=HoodHeight(at,1.848f)+.019f+row*.018f;
                if (Mathf.Pow((x-at)/.14f,2)+Mathf.Pow((y-(HoodHeight(at,1.848f)+.039f))/.03f,2)> .9f) continue;
                slats.Add(new[]{new Vector3(x-.004f,y-.0015f,1.845f),new Vector3(x+.004f,y-.0015f,1.845f),new Vector3(x+.004f,y+.0015f,1.845f),new Vector3(x-.004f,y+.0015f,1.845f)});
            }
            Node(body,"Hood intake mesh "+side,QuadMesh("Hood intake fine mesh "+side,slats),black);
        }
        var edge = new List<Vector3>();
        for(int i=0;i<=60;i++){float x=Mathf.Lerp(-.86f,.86f,i/60f);edge.Add(new Vector3(x,HoodHeight(x,2.392f),2.392f));}
        Tube(body,"Hood leading hem seam",edge,.0027f,seal,5);
    }

    static void BuildCabin(Transform body)
    {
        Node(body,"Twin-striped double-curvature roof",Grid("Smooth coupe roof skin",72,72,(u,v)=>
        {
            float z=Mathf.Lerp(-.825f,.49f,v);float width=Mathf.Lerp(.682f,.714f,Mathf.Sin(v*Mathf.PI*.5f));
            float a=Mathf.Abs(u*2-1);
            float x=(u*2-1)*width;float roll=.009f*a*a+.018f*(1-Mathf.Sqrt(Mathf.Max(0f,1-Mathf.Pow(a,8))));
            float y=1.463f-roll-.006f*Mathf.Pow(v*2-1,4);
            return new Vector3(x,y,z);
        },StripeUV,true),striped);
        Node(body,"Curved laminated windshield",Grid("Panoramic bowed Challenger windshield",64,32,(u,v)=>
        {
            float across=u*2f-1f;float width=Mathf.Lerp(.862f,.696f,v);
            return new Vector3(across*width,Mathf.Lerp(1.078f,1.435f,v)+(.007f+.014f*v)*(1-across*across),Mathf.Lerp(.965f,.475f,v)+.024f*(1-across*across)*Mathf.Sin(v*Mathf.PI));
        }),glass,false);
        Node(body,"Sloping rear coupe glass",Grid("Curved rear window",60,28,(u,v)=>
        {
            float a=u*2-1;float x=a*Mathf.Lerp(.855f,.658f,v);
            return new Vector3(x,Mathf.Lerp(1.065f,1.435f,v)+(.005f+.016f*v)*(1-a*a),Mathf.Lerp(-1.445f,-.825f,v)-.015f*(1-a*a)*Mathf.Sin(v*Mathf.PI));
        },null,true),glass,false);
        for(int side=-1;side<=1;side+=2)
        {
            int s=side;
            var apillar=new List<Vector3>();
            for(int i=0;i<=36;i++){float t=i/36f;apillar.Add(new Vector3(s*(Mathf.Lerp(.883f,.718f,t)+.007f*Mathf.Sin(t*Mathf.PI)),Mathf.Lerp(1.065f,1.44f,t)+.009f*Mathf.Sin(t*Mathf.PI),Mathf.Lerp(.977f,.482f,t)-.010f*Mathf.Sin(t*Mathf.PI)));}
            Tube(body,"Sculpted A pillar "+side,apillar,.031f,silver,10);
            Vector3[] window={new Vector3(s*.900f,1.074f,-.874f),new Vector3(s*.736f,1.423f,-.553f),new Vector3(s*.735f,1.427f,-.626f),new Vector3(s*.823f,1.261f,-.960f),new Vector3(s*.898f,1.071f,-1.129f)};
            Node(body,"Fixed rear quarter glazing behind opening door "+side,Polygon("Rear quarter glass behind separate door "+side,side>0?window.Reverse().ToArray():window),glass,false);
            Tube(body,"Rear quarter window rubber reveal "+side,Close(window),.012f,seal,7);
            var cpillar=new[]{new Vector3(s*.733f,1.433f,-.637f),new Vector3(s*.677f,1.426f,-.832f),new Vector3(s*.927f,1.040f,-1.497f),new Vector3(s*.912f,1.045f,-1.116f)};
            Node(body,"Broad flowing C pillar "+side,Patch("Sculpted coupe C pillar "+side,cpillar[0],cpillar[1],cpillar[2],cpillar[3],30,28,.025f,side>0),silver);
            var roofrail=new List<Vector3>();for(int i=0;i<=44;i++){float z=Mathf.Lerp(.46f,-.70f,i/44f),t=Mathf.InverseLerp(-.825f,.49f,z);float w=Mathf.Lerp(.682f,.714f,Mathf.Sin(t*Mathf.PI*.5f));roofrail.Add(new Vector3(s*(w+.003f),1.443f-.006f*Mathf.Pow(t*2-1,4),z));}
            Tube(body,"Roof side drip rail "+side,roofrail,.012f,silver,8);
            Tube(body,"Fixed narrow rear door aperture pillar "+side,new[]{new Vector3(s*.900f,1.074f,-.878f),new Vector3(s*.736f,1.423f,-.557f)},.014f,seal,6);
            Tube(body,"Rear quarter glass lower seal "+side,new[]{new Vector3(s*.900f,1.067f,-.881f),new Vector3(s*.910f,1.064f,-1.13f)},.014f,seal,6);
        }
        var topseal=new List<Vector3>();var bottomseal=new List<Vector3>();
        for(int i=0;i<=60;i++){float a=i/60f*2-1;topseal.Add(new Vector3(a*.70f,1.436f+.021f*(1-a*a),.480f));bottomseal.Add(new Vector3(a*.866f,1.077f,.971f));}
        Tube(body,"Windshield top bonded rubber",topseal,.011f,seal,6);Tube(body,"Windshield cowl seal",bottomseal,.015f,seal,6);
        Box(body,"Satin cowl plenum",new Vector3(0,1.056f,.919f),new Vector3(1.66f,.026f,.091f),black);
        for(int s=-1;s<=1;s+=2)
        {
            Tube(body,"Windshield articulated wiper arm "+s,new[]{new Vector3(s*.47f,1.09f,.937f),new Vector3(s*.38f,1.115f,.909f),new Vector3(s*.10f,1.123f,.891f)},.005f,black,5);
            Tube(body,"Windshield rubber wiper blade "+s,new[]{new Vector3(s*.13f,1.128f,.884f),new Vector3(s*.58f,1.123f,.895f)},.008f,seal,5);
        }
    }

    static void BuildRearDeck(Transform body, FacilityDoor trunk, Transform interaction)
    {
        trunk.transform.localPosition=new Vector3(0,1.036f,-1.47f);trunk.transform.localRotation=Quaternion.identity;
        trunk.leaf.localPosition=Vector3.zero;trunk.leaf.localRotation=Quaternion.identity;
        trunk.openLocalEuler=new Vector3(67f,0,0);trunk.openLocalOffset=Vector3.zero;
        trunk.name="Challenger trunk - hinged striped deck";
        Transform leaf=trunk.leaf;
        Node(leaf,"Sculpted striped trunk outer skin",Grid("Crowned Challenger trunk lid",64,50,(u,v)=>
        {
            float x=(u*2-1)*Mathf.Lerp(.847f,.878f,v);float localZ=Mathf.Lerp(0f,-.941f,v);
            return new Vector3(x,-.017f*v+.009f*(1-(u*2-1)*(u*2-1)),localZ);
        },StripeUV),striped);
        Node(leaf,"Rolled trunk rear hem and stripe continuation",Grid("Curved trunk trailing hem",64,16,(u,v)=>
        {
            float x=(u*2-1)*Mathf.Lerp(.878f,.903f,v);
            return new Vector3(x,Mathf.Lerp(-.017f,-.066f,v)+.004f*(1-(u*2-1)*(u*2-1)),Mathf.Lerp(-.940f,-1.045f,v));
        },StripeUV),striped);
        Box(leaf,"Trunk weatherproof inner liner",new Vector3(0,-.030f,-.46f),new Vector3(1.63f,.022f,.86f),rubber);
        Box(leaf,"Trunk lid physical collision",new Vector3(0,-.007f,-.475f),new Vector3(1.72f,.052f,.95f),null,true);
        for(int s=-1;s<=1;s+=2)
        {
            Tube(leaf,"Trunk lid curved hinge arm "+s,new[]{new Vector3(s*.62f,-.04f,.09f),new Vector3(s*.62f,-.12f,-.01f),new Vector3(s*.62f,-.04f,-.18f)},.017f,black,8);
            Box(body,"Trunk cavity silver quarter wall "+s,new Vector3(s*.872f,.797f,-1.94f),new Vector3(.095f,.43f,.91f),silver,true);
            Tube(body,"Rear deck quarter shut line "+s,new[]{new Vector3(s*.858f,1.039f,-1.47f),new Vector3(s*.891f,1.019f,-2.404f)},.0036f,seal,5);
        }
        Box(body,"Deep lined equipment trunk floor",new Vector3(0,.575f,-1.89f),new Vector3(1.65f,.055f,.97f),rubber,true);
        Box(body,"Trunk forward bulkhead",new Vector3(0,.79f,-1.426f),new Vector3(1.67f,.41f,.055f),rubber,true);
        for(int side=-1;side<=1;side+=2)
        {
            int s=side;
            Node(body,"Carpeted rounded trunk wheel housing "+side,
                Grid("Trimmed rear wheel house dome "+side,28,40,(u,v)=>
                {
                    float angle=v*1.156f;
                    float top=.39f+.46f*Mathf.Cos(angle);
                    float roll=Mathf.Pow(Mathf.Sin(u*Mathf.PI*.5f),.15f);
                    return new Vector3(s*Mathf.Lerp(.645f,.862f,u),Mathf.Lerp(.575f,top,roll),-1.48f-.46f*Mathf.Sin(angle));
                },null,side<0),rubber);
            Node(body,"Wheel housing closed carpeted front "+side,
                Grid("Rear wheel house front closure "+side,28,12,(u,v)=>
                {
                    float roll=Mathf.Pow(Mathf.Sin(u*Mathf.PI*.5f),.15f);
                    return new Vector3(s*Mathf.Lerp(.645f,.862f,u),Mathf.Lerp(.575f,.575f+.275f*roll,v),-1.48f);
                },null,side>0),rubber);
        }
        Box(body,"Trunk rear closure sill",new Vector3(0,.79f,-2.427f),new Vector3(1.72f,.42f,.055f),silver,true);
        var weather=new[]{new Vector3(-.831f,1.011f,-1.5f),new Vector3(.831f,1.011f,-1.5f),new Vector3(.862f,1.001f,-2.393f),new Vector3(-.862f,1.001f,-2.393f)};
        Tube(body,"Continuous trunk weather seal",Close(weather),.009f,seal,6);
        if(interaction!=null)
        {
            interaction.localPosition=new Vector3(0,-.004f,-.891f);interaction.localRotation=Quaternion.identity;
            var target=interaction.GetComponent<BoxCollider>();if(target!=null){target.center=Vector3.zero;target.size=new Vector3(.58f,.15f,.20f);}
            var item=interaction.GetComponent<FacilityStoryInteractable>();if(item!=null)item.label="Car trunk";
        }
        // The rear body remains solid to projectiles without placing a collider across the usable trunk opening.
        Box(body,"Engine compartment collision ahead of open doors",new Vector3(0,.66f,1.665f),new Vector3(1.76f,.83f,1.61f),null,true);
        Box(body,"Cabin floor collision below open door apertures",new Vector3(0,.414f,-.17f),new Vector3(1.74f,.100f,2.10f),null,true);
        Box(body,"Coupe roof physical collision above door openings",new Vector3(0,1.459f,-.17f),new Vector3(1.40f,.035f,1.29f),null,true);
        Box(body,"Rear bench and passenger bulkhead collision",new Vector3(0,.87f,-1.176f),new Vector3(1.57f,.46f,.11f),null,true);
        Box(body,"Front fascia physical collision",new Vector3(0,.59f,2.37f),new Vector3(1.81f,.66f,.30f),null,true);
        var nav=trunk.leaf.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
        if(nav==null)nav=trunk.leaf.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();nav.ignoreFromBuild=true;
    }

    static void BuildUnderside(Transform body)
    {
        Box(body,"Black aerodynamic undertray",new Vector3(0,.24f,0),new Vector3(1.65f,.055f,4.68f),black);
        for(int s=-1;s<=1;s+=2)Box(body,"Lower chassis subframe "+s,new Vector3(s*.58f,.205f,-.12f),new Vector3(.055f,.045f,3.90f),black);
    }

    static Vector2 StripeUV(Vector3 p,float u,float v)=>new Vector2((p.x+1.05f)/2.1f,v);
    static Texture2D StripeTexture()=>Texture("Factory twin carbon stripe albedo",false);
    static Texture2D StripeMetallic()=>Texture("Clearcoat and satin stripe metallic gloss",true);
    static Texture2D Texture(string name,bool packed)
    {
        string path=Root+"/Textures/"+name+".png";
        var tex=new Texture2D(2048,16,TextureFormat.RGBA32,false);var pixels=new Color[2048*16];
        for(int y=0;y<16;y++)for(int x=0;x<2048;x++)
        {
            float at=Mathf.Abs((x+.5f)/2048f*2.1f-1.05f);
            float mask=Mathf.SmoothStep(0,1,Mathf.Clamp01((at-.058f)/.002f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((.402f-at)/.002f));
            pixels[y*2048+x]=packed?new Color(Mathf.Lerp(.52f,.10f,mask),0,0,Mathf.Lerp(.85f,.47f,mask)):Color.Lerp(Color.white,new Color(.017f,.020f,.023f),mask);
        }
        tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=!packed;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void AddSceneReflection(Transform car)
    {
        var probe=Group(car,"Challenger actual parking environment reflection").gameObject.AddComponent<ReflectionProbe>();
        probe.transform.localPosition=new Vector3(0,1.6f,0);
        probe.mode=ReflectionProbeMode.Baked;probe.size=new Vector3(8f,5f,9f);probe.center=new Vector3(0,-.5f,0);
        probe.boxProjection=true;probe.blendDistance=1.5f;probe.importance=25;probe.intensity=1f;
        probe.resolution=128;probe.hdr=true;probe.nearClipPlane=.15f;probe.farClipPlane=50f;
        // Capture the real night parking lot, excluding the vehicle itself. No invented sky or studio highlights.
        probe.cullingMask=~(1<<29);
    }

    static void ConfigureParkingLamp(Transform car)
    {
        Transform level=car.root;
        Transform fixture=level.Find("01 Night parking - sedan and entrance/Parking lamp on concrete footing/Parking sodium lamp");
        Light parking=fixture!=null?fixture.GetComponentInChildren<Light>(true):null;
        if(parking==null)throw new InvalidOperationException("The actual parking lamp is required for vehicle lighting.");
        // Mixed lighting keeps baked indirect light while moving doors and people cast real direct shadows.
        parking.lightmapBakeType=LightmapBakeType.Mixed;parking.shadows=LightShadows.Soft;
        parking.shadowBias=.025f;parking.shadowNormalBias=.16f;parking.cullingMask=~0;
        parking.renderMode=LightRenderMode.ForcePixel;EditorUtility.SetDirty(parking);
        if(Lightmapping.lightingSettings!=null)
        { Lightmapping.lightingSettings.mixedBakeMode=MixedLightingMode.Shadowmask;EditorUtility.SetDirty(Lightmapping.lightingSettings); }
    }

    public static string FinishShadowRendering()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish vehicle shadow rendering outside Play mode and baking.");
        Scene scene=SceneManager.GetActiveScene();
        var director=FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if(director==null)throw new InvalidOperationException("Open the story scene first.");
        EnablePlayerShadows(director);
        FacilityHellcatDetails.UpdateLetteringMaterial();
        FacilityHellcatDoorAuthoring.RefreshApertureSeals(director.sedanExit.transform.parent);
        foreach(TextMesh label in director.sedanExit.transform.parent.GetComponentsInChildren<TextMesh>(true))
        {
            Renderer renderer=label.GetComponent<Renderer>();
            if(renderer!=null){renderer.receiveShadows=true;EditorUtility.SetDirty(renderer);}
        }
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        return FacilityHellcatValidation.Validate(scene)+" Player camera renders physical shadows; baked parking lighting retained.";
    }

    static void EnablePlayerShadows(FacilityStoryDirector director)
    {
        Camera eyes=director.player!=null?director.player.GetComponentInChildren<Camera>(true):null;
        if(eyes==null)throw new InvalidOperationException("The original player camera is required.");
        var data=eyes.GetUniversalAdditionalCameraData();data.renderShadows=true;EditorUtility.SetDirty(data);
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(pipeline==null)throw new InvalidOperationException("The active Universal Render Pipeline is required.");
        pipeline.shadowDistance=40f;pipeline.maxAdditionalLightsCount=8;
        var settings=new SerializedObject(pipeline);
        settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
        settings.FindProperty("m_AnyShadowsSupported").boolValue=true;
        settings.FindProperty("m_SoftShadowsSupported").boolValue=true;
        settings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=2048;
        settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
    }

    static void BuildCourtesyLights(Transform car,FacilityDoor trunk)
    {
        var control=car.GetComponent<FacilityVehicleCourtesyLights>();
        if(control==null)control=car.gameObject.AddComponent<FacilityVehicleCourtesyLights>();
        control.driverDoor=car.Find("Challenger driver door - hinged").GetComponent<FacilityDoor>();
        control.passengerDoor=car.Find("Challenger passenger door - hinged").GetComponent<FacilityDoor>();
        control.trunkDoor=trunk;
        Material housing=Mat("Challenger courtesy lamp recessed surround",new Color(.025f,.028f,.032f),.15f,.35f);
        Material lens=Mat("Challenger switched warm courtesy lamp lens",new Color(.30f,.27f,.19f),0f,.35f);
        lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",Color.black);
        lens.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;EditorUtility.SetDirty(lens);
        Transform cabin=car.Find("Challenger cabin");
        Box(cabin,"Recessed overhead dome lamp housing",new Vector3(0,1.424f,-.10f),new Vector3(.195f,.022f,.12f),housing);
        Renderer cabinLens=Box(cabin,"Switched warm overhead lamp lens",new Vector3(0,1.409f,-.10f),new Vector3(.144f,.009f,.077f),lens).GetComponent<Renderer>();
        Light dome=CourtesyLight(cabin,"Door switched warm cabin dome light",new Vector3(0,1.382f,-.10f),.65f,1.85f,100f);
        dome.transform.localRotation=Quaternion.Euler(90f,0,0);
        // Mounted on the inner rear trunk lip, aimed downward and forward at the stored drill and firearms.
        Transform fixture=Group(car,"Trunk opening switched courtesy fixture");
        Box(fixture,"Recessed trunk lamp housing",new Vector3(0,.976f,-2.374f),new Vector3(.17f,.023f,.060f),housing);
        Renderer trunkLens=Box(fixture,"Switched warm trunk lamp lens",new Vector3(0,.961f,-2.366f),new Vector3(.126f,.009f,.040f),lens).GetComponent<Renderer>();
        Light trunkLamp=CourtesyLight(fixture,"Trunk switched downward equipment light",new Vector3(0,.942f,-2.345f),.80f,1.35f,106f);
        trunkLamp.transform.localRotation=Quaternion.LookRotation(new Vector3(0,.64f,-1.86f)-trunkLamp.transform.localPosition);
        control.cabinLights=new[]{dome};control.trunkLight=trunkLamp;
        control.cabinLampLenses=new[]{cabinLens};control.trunkLampLenses=new[]{trunkLens};
        control.RefreshLights();EditorUtility.SetDirty(control);
    }

    static Light CourtesyLight(Transform parent,string name,Vector3 position,float intensity,float range,float angle)
    {
        Transform t=Group(parent,name);t.localPosition=position;Light lamp=t.gameObject.AddComponent<Light>();
        lamp.type=LightType.Spot;lamp.lightmapBakeType=LightmapBakeType.Realtime;lamp.color=new Color(1f,.78f,.40f);
        lamp.intensity=intensity;lamp.range=range;lamp.spotAngle=angle;lamp.innerSpotAngle=angle*.65f;
        lamp.shadows=LightShadows.Soft;lamp.shadowBias=.008f;lamp.shadowNormalBias=.025f;
        lamp.renderMode=LightRenderMode.ForcePixel;lamp.enabled=false;return lamp;
    }

    static Material Mat(string name,Color color,float metal,float gloss)
    {
        string path=Root+"/Materials/"+name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",gloss);m.SetFloat("_Cull",0);
        m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;m.DisableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(m);return m;
    }
    static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static GameObject Node(Transform parent,string name,Mesh mesh,Material material,bool shadows=true)
    {
        Transform t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
        r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;return t.gameObject;
    }
    static GameObject Box(Transform p,string name,Vector3 at,Vector3 size,Material mat,bool collision=false)
    {
        GameObject g;
        if(mat!=null){g=Node(p,name,BoxMesh(size),mat);g.transform.localPosition=at;}else{g=Group(p,name).gameObject;g.transform.localPosition=at;}
        if(collision){var c=g.AddComponent<BoxCollider>();c.size=size;}return g;
    }
    static Mesh Grid(string name,int nx,int ny,Func<float,float,Vector3> point,Func<Vector3,float,float,Vector2> uvMap=null,bool reverse=false)
    {
        var v=new Vector3[(nx+1)*(ny+1)];var uv=new Vector2[v.Length];var t=new int[nx*ny*6];int n=0;
        for(int j=0;j<=ny;j++)for(int i=0;i<=nx;i++){float u=i/(float)nx,w=j/(float)ny;int k=j*(nx+1)+i;v[k]=point(u,w);uv[k]=uvMap!=null?uvMap(v[k],u,w):new Vector2(u,w);}
        for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){int a=j*(nx+1)+i,b=a+1,c=a+nx+2,d=a+nx+1;t[n++]=a;t[n++]=b;t[n++]=c;t[n++]=a;t[n++]=c;t[n++]=d;}
        if(reverse)for(int i=0;i<t.Length;i+=3){int swap=t[i+1];t[i+1]=t[i+2];t[i+2]=swap;}
        return Save(name,v,uv,t);
    }
    static Mesh Patch(string name,Vector3 a,Vector3 b,Vector3 c,Vector3 d,int nx,int ny,float bulge,bool reverse)
        =>Grid(name,nx,ny,(u,v)=>Vector3.Lerp(Vector3.Lerp(a,b,u),Vector3.Lerp(d,c,u),v)+new Vector3(Mathf.Sign(a.x)*bulge*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI),0,0),null,reverse);
    static Mesh Ellipse(string name,float x,float y,float z,float rx,float ry)
    {
        var points=new Vector3[65];points[0]=new Vector3(x,y,z);for(int i=0;i<64;i++){float a=i/64f*Mathf.PI*2;points[i+1]=new Vector3(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry,z);}var tris=new int[64*3];for(int i=0;i<64;i++){tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=(i+1)%64+1;}return Save(name,points,points.Select(p=>new Vector2(p.x,p.y)).ToArray(),tris);
    }
    static Mesh Polygon(string name,Vector3[] outline)
    {
        var p=new Vector3[outline.Length+1];Vector3 center=Vector3.zero;foreach(var at in outline)center+=at;center/=outline.Length;p[0]=center;Array.Copy(outline,0,p,1,outline.Length);var tri=new int[outline.Length*3];for(int i=0;i<outline.Length;i++){tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=(i+1)%outline.Length+1;}return Save(name,p,p.Select(v=>new Vector2(v.z,v.y)).ToArray(),tri);
    }
    static Mesh QuadMesh(string name,List<Vector3[]> quads)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();foreach(var q in quads){int k=v.Count;v.AddRange(q);uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});tri.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}return Save(name,v.ToArray(),uv.ToArray(),tri.ToArray());
    }
    static Mesh BoxMesh(Vector3 size)
    {
        Vector3 a=size*.5f;var q=new List<Vector3[]> {
            new[]{new Vector3(-a.x,-a.y,a.z),new Vector3(a.x,-a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(-a.x,a.y,a.z)},
            new[]{new Vector3(a.x,-a.y,-a.z),new Vector3(-a.x,-a.y,-a.z),new Vector3(-a.x,a.y,-a.z),new Vector3(a.x,a.y,-a.z)},
            new[]{new Vector3(a.x,-a.y,a.z),new Vector3(a.x,-a.y,-a.z),new Vector3(a.x,a.y,-a.z),new Vector3(a.x,a.y,a.z)},
            new[]{new Vector3(-a.x,-a.y,-a.z),new Vector3(-a.x,-a.y,a.z),new Vector3(-a.x,a.y,a.z),new Vector3(-a.x,a.y,-a.z)},
            new[]{new Vector3(-a.x,a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,-a.z),new Vector3(-a.x,a.y,-a.z)},
            new[]{new Vector3(-a.x,-a.y,-a.z),new Vector3(a.x,-a.y,-a.z),new Vector3(a.x,-a.y,a.z),new Vector3(-a.x,-a.y,a.z)}};
        return QuadMesh("Body formed box "+size.ToString("F4"),q);
    }
    static void Tube(Transform parent,string name,IList<Vector3> path,float radius,Material material,int sides)
    {
        if(path.Count<2)return;var v=new Vector3[path.Count*sides];var uv=new Vector2[v.Length];var t=new List<int>();
        for(int i=0;i<path.Count;i++)
        {
            Vector3 tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;Vector3 axis=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.forward:Vector3.up).normalized;Vector3 other=Vector3.Cross(tangent,axis);
            for(int j=0;j<sides;j++){float a=j/(float)sides*Mathf.PI*2;v[i*sides+j]=path[i]+(axis*Mathf.Cos(a)+other*Mathf.Sin(a))*radius;uv[i*sides+j]=new Vector2(j/(float)sides,i/(float)path.Count);if(i>0){int k=i*sides+j,b=i*sides+(j+1)%sides;t.AddRange(new[]{k,b,b-sides,k,b-sides,k-sides});}}
        }
        Node(parent,name,Save(name,v,uv,t.ToArray()),material);
    }
    static List<Vector3> Close(IList<Vector3> points){var p=new List<Vector3>(points);p.Add(points[0]);return p;}
    static List<Vector3> BezierLoop(Vector3[] points,int steps)
    {
        var result=new List<Vector3>();for(int i=0;i<points.Length;i++){Vector3 previous=points[(i+points.Length-1)%points.Length],current=points[i],next=points[(i+1)%points.Length];Vector3 a=Vector3.Lerp(previous,current,.88f),b=Vector3.Lerp(current,next,.12f);for(int j=0;j<steps;j++){float t=j/(float)steps;result.Add((1-t)*(1-t)*a+2*(1-t)*t*current+t*t*b);}result.Add(b);}result.Add(result[0]);return result;
    }
    static Mesh Save(string name,Vector3[] v,Vector2[] uv,int[] t)
    {
        string safe=string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));string path=Root+"/Meshes/"+safe+" V"+revision+".asset";
        Mesh m=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(m==null){m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);}m.Clear();if(v.Length>65535)m.indexFormat=IndexFormat.UInt32;m.vertices=v;m.uv=uv;m.triangles=t;m.RecalculateNormals();m.RecalculateBounds();m.RecalculateTangents();EditorUtility.SetDirty(m);return m;
    }
    static void Ensure(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=path.Substring(0,path.LastIndexOf('/'));Ensure(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
}
