using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Builds one editable, conventional pump shotgun and saves every generated resource as an asset.</summary>
public static class ShotgunModelAuthoring
{
    public const string AssetRoot = "Assets/Generated/Combat/Weapon";
    private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

    public static FacilityShotgun Build(Camera camera)
    {
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        EnsureFolder(AssetRoot);
        foreach (string folder in new[] { "Meshes", "Materials", "Textures", "Audio", "Prefabs" })
            EnsureFolder(AssetRoot + "/" + folder);

        // Replacing only our previous authored weapon is intentional; the camera and player keep their settings.
        for (int i = camera.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = camera.transform.GetChild(i);
            if (child.name == "Retro Blaster" || child.name == "Pump Shotgun") Object.DestroyImmediate(child.gameObject);
        }
        RetroViewmodel motion = camera.GetComponent<RetroViewmodel>();
        if (motion == null) motion = camera.gameObject.AddComponent<RetroViewmodel>();
        FacilityShotgun weapon = camera.GetComponent<FacilityShotgun>();
        if (weapon == null) weapon = camera.gameObject.AddComponent<FacilityShotgun>();

        Material steel = Material("Parkerized Steel", new Color(.20f, .225f, .235f), .24f, .14f);
        Material edge = Material("Worn Steel", new Color(.36f, .39f, .40f), .3f, .12f);
        Material dark = Material("Black Recess", new Color(.014f, .018f, .018f), .02f, .025f);
        Material wood = Material("Oiled Walnut", new Color(.43f, .205f, .085f), .10f, .09f, true);
        Material rubber = Material("Buttpad Rubber", new Color(.046f, .040f, .035f), .01f, .035f);
        Material brass = Material("Shell Brass", new Color(.67f, .44f, .16f), .36f, .10f);
        Material red = Material("Shell Hull", new Color(.46f, .045f, .028f), .07f, .045f);
        Material fire = Material("Muzzle Flame V2", new Color(1f, .46f, .055f), 0f, 0f, false, true);
        Material fireCore = Material("Muzzle White Core V2", new Color(1f, .95f, .72f), 0f, 0f, false, true);
        Material glove = Material("Worn Leather Gloves", new Color(.25f,.205f,.145f), .06f, .12f);
        Material sleeve = Material("Olive Canvas Sleeves", new Color(.19f,.225f,.17f), .025f, .10f);
        Material cuff = Material("Dark Glove Cuffs", new Color(.10f,.12f,.09f), .025f, .10f);

        GameObject root = new GameObject("Pump Shotgun");
        root.transform.SetParent(camera.transform, false);
        motion.authoredModel = root.transform;
        motion.restingPosition = new Vector3(.29f, -.28f, .43f);
        motion.restingEuler = new Vector3(0f, -4f, 0f);
        root.transform.localPosition = motion.restingPosition;
        root.transform.localRotation = Quaternion.Euler(motion.restingEuler);

        Mesh receiver = Extrusion("Milled receiver", .118f, new[] {
            new Vector2(-.068f,-.17f), new Vector2(-.068f,.17f), new Vector2(.042f,.21f),
            new Vector2(.077f,.13f), new Vector2(.077f,-.13f), new Vector2(.035f,-.19f) });
        Piece(root.transform, "Receiver", receiver, steel);
        Box(root.transform, "Receiver top rib", new Vector3(0f,.080f,-.025f), new Vector3(.052f,.013f,.275f), edge);
        // A dark inset plus a metal bolt reads as an actual opening rather than another floating block.
        Box(root.transform, "Right ejection port recess", new Vector3(.0598f,.016f,.018f), new Vector3(.0024f,.047f,.124f), dark);
        Box(root.transform, "Closed bolt visible in port", new Vector3(.0613f,.009f,.003f), new Vector3(.0012f,.024f,.070f), edge);
        Box(root.transform, "Port upper lip", new Vector3(.061f,.042f,.018f), new Vector3(.004f,.005f,.128f), steel);
        Box(root.transform, "Action bar right", new Vector3(.043f,-.041f,.265f), new Vector3(.011f,.014f,.25f), edge);
        Box(root.transform, "Action bar left", new Vector3(-.043f,-.041f,.265f), new Vector3(.011f,.014f,.25f), edge);

        Transform barrel = Piece(root.transform, "Long blued barrel", Tube("Barrel hollow 16", .034f, .025f, .79f, 16), steel).transform;
        barrel.localPosition = new Vector3(0f,.030f,.575f);
        Transform muzzleCollar = Piece(root.transform, "Muzzle crown", Tube("Muzzle crown 16", .0365f, .025f, .022f, 16), edge).transform;
        muzzleCollar.localPosition = new Vector3(0f,.030f,.970f);
        Transform bore = Piece(root.transform, "Dark recessed bore", Cylinder("Bore disk", .0249f, .002f, 16), dark).transform;
        bore.localPosition = new Vector3(0f,.030f,.917f);
        Transform tube = Piece(root.transform, "Magazine tube below barrel", Cylinder("Magazine tube", .028f,.63f,14), steel).transform;
        tube.localPosition = new Vector3(0f,-.053f,.493f);
        Transform cap = Piece(root.transform, "Magazine end cap", Cylinder("Magazine cap", .030f,.025f,14), edge).transform;
        cap.localPosition = new Vector3(0f,-.053f,.817f);
        Box(root.transform, "Front barrel band", new Vector3(0f,-.012f,.786f), new Vector3(.018f,.077f,.023f), steel);
        Box(root.transform, "Front sight base", new Vector3(0f,.071f,.917f), new Vector3(.018f,.016f,.048f), steel);
        Box(root.transform, "Brass front bead", new Vector3(0f,.083f,.931f), new Vector3(.012f,.009f,.012f), brass);

        Transform pump = new GameObject("Sliding ribbed wooden fore-end").transform;
        pump.SetParent(root.transform, false);
        pump.localPosition = new Vector3(0f,-.053f,.466f);
        Transform foreEnd = Piece(pump, "Walnut fore-end body", Cylinder("Fore-end twelve facets", .057f,.265f,12), wood).transform;
        foreEnd.localScale = new Vector3(1.08f, .92f, 1f);
        for (int i = 0; i < 11; i++)
        {
            Transform rib = Piece(pump, "Grip rib " + (i + 1), Cylinder("Fore-end grip rib", .060f,.007f,12), wood).transform;
            rib.localPosition = new Vector3(0f,0f,-.112f + i * .022f);
            rib.localScale = new Vector3(1.08f,.92f,1f);
        }
        // The wooden stock has a comb, a narrow wrist, a dropped heel and a separate buttpad.
        Mesh stock = Extrusion("Walnut stock profile", .108f, new[] {
            new Vector2(.039f,-.177f), new Vector2(.022f,-.252f), new Vector2(.006f,-.35f),
            new Vector2(-.012f,-.57f), new Vector2(-.205f,-.57f), new Vector2(-.19f,-.47f),
            new Vector2(-.111f,-.306f), new Vector2(-.19f,-.254f), new Vector2(-.158f,-.213f),
            new Vector2(-.049f,-.166f) });
        Piece(root.transform, "Shaped walnut buttstock and wrist", stock, wood);
        Box(root.transform, "Recoil buttpad", new Vector3(0f,-.11f,-.582f), new Vector3(.115f,.204f,.026f), rubber);
        Transform guard = Piece(root.transform, "Open trigger guard", EllipseLoop("Open oval trigger guard", .027f,.052f,.074f,.009f,16), steel).transform;
        guard.localPosition = new Vector3(0f,-.111f,-.094f);
        Transform trigger = Piece(root.transform, "Curved trigger", Extrusion("Trigger profile", .012f, new[] {
            new Vector2(-.058f,-.073f),new Vector2(-.100f,-.078f),new Vector2(-.123f,-.098f),
            new Vector2(-.121f,-.112f),new Vector2(-.100f,-.092f),new Vector2(-.058f,-.087f) }), edge).transform;
        for (int i = 0; i < 2; i++)
        {
            Transform pin = Piece(root.transform, "Receiver retaining pin " + (i + 1), Cylinder("Receiver pin", .007f,.121f,8), edge).transform;
            pin.localPosition = new Vector3(0f,-.034f,-.102f + i * .16f);
            pin.localRotation = Quaternion.Euler(0f,90f,0f);
        }
        AddHands(root.transform,pump,glove,sleeve,cuff);

        Transform muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root.transform,false);
        muzzle.localPosition = new Vector3(0f,.03f,1.002f);
        GameObject flash = new GameObject("Explosive muzzle flash");
        flash.transform.SetParent(muzzle, false);
        Piece(flash.transform, "Orange star and pressure flame", FlashMesh(), fire);
        Transform core = Piece(flash.transform, "White hot flame core", FlashMesh(), fireCore).transform;
        core.localScale = new Vector3(.58f, .58f, .67f);
        core.localPosition = new Vector3(0f, 0f, -.012f);
        flash.SetActive(false);
        Material smokeMaterial = ImpactDustMaterial();
        Transform smoke = Piece(muzzle, "Brief expanding muzzle smoke", SmokeMesh(), smokeMaterial).transform;
        smoke.gameObject.SetActive(false);
        GameObject lightObject = new GameObject("Shot muzzle light (45 ms, no shadows)", typeof(Light));
        lightObject.transform.SetParent(muzzle, false);
        lightObject.transform.localPosition = new Vector3(0f, .10f, -.12f);
        Light muzzleLight = lightObject.GetComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = new Color(1f, .66f, .24f);
        muzzleLight.range = 4f;
        muzzleLight.intensity = 2.5f;
        muzzleLight.shadows = LightShadows.None;
        muzzleLight.renderMode = LightRenderMode.ForcePixel;
        muzzleLight.enabled = false;
        Transform ejection = new GameObject("Ejection port").transform;
        ejection.SetParent(root.transform,false);
        ejection.localPosition = new Vector3(.093f,.015f,.022f);

        motion.pump = pump;
        weapon.aimCamera = camera;
        weapon.viewmodel = motion;
        weapon.muzzle = muzzle;
        weapon.pump = pump;
        weapon.ejectionPort = ejection;
        weapon.muzzleFlash = flash;
        weapon.muzzleSmoke = smoke;
        weapon.muzzleLight = muzzleLight;
        weapon.spreadDegrees = 3f;
        weapon.pelletSpeed = 160f;
        weapon.shellPrefab = ShellPrefab(red,brass);
        weapon.reportClip = FacilitySoundDesign.ShotgunReport();
        weapon.actionClip = FacilityAudioAuthoring.PumpAction();
        weapon.reportSource = Source(camera.transform,"Shotgun report",.82f,32);
        weapon.actionSource = Source(camera.transform,"Shotgun pump action",.55f,40);
        weapon.reportSource.clip = weapon.reportClip;
        weapon.actionSource.clip = weapon.actionClip;
        FacilitySurfaceImpacts impacts = camera.GetComponent<FacilitySurfaceImpacts>();
        if (impacts == null) impacts = camera.gameObject.AddComponent<FacilitySurfaceImpacts>();
        impacts.holeMesh = BuckshotHoleMesh();
        impacts.holeMaterial = Material("Buckshot Charcoal Cavity", new Color(.008f,.007f,.006f),0f,0f,false,true);
        impacts.chippedRimMaterial = Material("Buckshot Chipped Rim", new Color(.35f,.32f,.28f), .01f, .08f);
        impacts.dustMaterial = smokeMaterial;
        weapon.surfaceImpacts = impacts;
        EditorUtility.SetDirty(impacts);
        EditorUtility.SetDirty(motion);
        EditorUtility.SetDirty(weapon);
        AssetDatabase.SaveAssets();
        return weapon;
    }

    private static AudioSource Source(Transform parent,string name,float volume,int priority)
    {
        Transform old = parent.Find(name);
        GameObject go = old != null ? old.gameObject : new GameObject(name);
        go.transform.SetParent(parent,false);
        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null) source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.priority = priority;
        return source;
    }

    private static GameObject ShellPrefab(Material hull,Material brass)
    {
        string path = AssetRoot + "/Prefabs/Spent 12 Gauge Shell.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject root = new GameObject("Spent 12 Gauge Shell") { layer = 2 };
        Piece(root.transform,"Red plastic hull",Cylinder("Shell hull",.0095f,.047f,10),hull);
        Transform rim = Piece(root.transform,"Brass head",Cylinder("Shell brass head",.0105f,.012f,10),brass).transform;
        rim.localPosition = new Vector3(0f,0f,-.0275f);
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.radius = .0105f;
        collider.height = .061f;
        collider.center = new Vector3(0f,0f,-.005f);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = .014f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root,path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Material Material(string name,Color color,float smoothness,float fill,bool wood=false,bool unlit=false)
    {
        string path = AssetRoot + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            ReadabilityFill(material,fill,unlit);
            return material;
        }
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP weapon shader missing.");
        material = new Material(shader) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor",color);
        if (!unlit)
        {
            material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_Metallic",wood ? 0f : .12f);
            material.SetColor("_EmissionColor",color * fill);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        if (wood) material.SetTexture("_BaseMap",WoodTexture());
        AssetDatabase.CreateAsset(material,path);
        // URP's first material import may reset keywords. Apply the readability floor after importing too.
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        material=AssetDatabase.LoadAssetAtPath<Material>(path);
        ReadabilityFill(material,fill,unlit);
        return material;
    }

    private static void ReadabilityFill(Material material,float fill,bool unlit)
    {
        if(unlit)return;
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
        material.SetColor("_EmissionColor",material.GetColor("_BaseColor")*fill);
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
    }

    private static void AddHands(Transform weapon,Transform pump,Material glove,Material sleeve,Material cuff)
    {
        Transform left=new GameObject("Left support hand and sleeve").transform;
        left.SetParent(pump,false);
        Piece(left,"Contoured support palm",Loft("Left glove palm",new[] {
            new Vector3(-.020f,-.082f,-.077f),new Vector3(-.016f,-.077f,-.024f),new Vector3(-.008f,-.069f,.046f)
        },new[]{.028f,.047f,.040f},new[]{.019f,.024f,.022f},8),glove);
        for(int i=0;i<4;i++)
        {
            float z=-.038f+i*.025f;
            Piece(left,"Curled support finger "+(i+1),Loft("Left curved finger fit2 "+i,new[] {
                new Vector3(-.052f,-.063f,z),new Vector3(-.002f,-.080f,z+.004f),
                new Vector3(.043f,-.063f,z+.004f),new Vector3(.064f,-.028f,z),new Vector3(.067f,.009f,z-.006f)
            },new[]{.010f,.010f,.010f,.009f,.007f},new[]{.010f,.010f,.010f,.009f,.007f},7),glove);
        }
        Piece(left,"Support thumb along fore-end",Loft("Left curved thumb fit2",new[] {
            new Vector3(-.048f,-.067f,-.052f),new Vector3(-.069f,-.033f,-.038f),
            new Vector3(-.063f,.011f,-.018f),new Vector3(-.058f,.023f,.013f)
        },new[]{.013f,.012f,.010f,.0075f},new[]{.012f,.012f,.010f,.0075f},7),glove);
        Piece(left,"Support canvas forearm",Loft("Left tapered sleeve",new[] {
            new Vector3(-.150f,-.310f,-.270f),new Vector3(-.073f,-.146f,-.140f),new Vector3(-.027f,-.100f,-.081f)
        },new[]{.050f,.043f,.034f},new[]{.046f,.039f,.031f},9),sleeve);
        Piece(left,"Support wrist cuff",Loft("Left glove cuff",new[] {
            new Vector3(-.040f,-.115f,-.096f),new Vector3(-.018f,-.083f,-.065f)
        },new[]{.038f,.035f},new[]{.034f,.030f},9),cuff);

        Transform right=new GameObject("Right trigger hand and sleeve").transform;
        right.SetParent(weapon,false);
        Piece(right,"Contoured trigger palm",Loft("Right glove palm",new[] {
            new Vector3(.069f,-.079f,-.244f),new Vector3(.077f,-.122f,-.232f),new Vector3(.062f,-.171f,-.233f)
        },new[]{.024f,.029f,.025f},new[]{.026f,.035f,.028f},8),glove);
        for(int i=0;i<3;i++)
        {
            float y=-.115f-i*.022f;
            float front=-.196f-i*.008f;
            Piece(right,"Stock gripping finger "+(i+1),Loft("Right curved grip finger fit2 "+i,new[] {
                new Vector3(.079f,y,-.218f-i*.003f),new Vector3(.052f,y-.004f,front-.004f),
                new Vector3(.004f,y-.005f,front),new Vector3(-.042f,y,front-.014f),new Vector3(-.051f,y+.003f,front-.039f)
            },new[]{.010f,.010f,.010f,.009f,.007f},new[]{.010f,.010f,.010f,.009f,.007f},7),glove);
        }
        Piece(right,"Index finger on trigger",Loft("Right trigger index finger",new[] {
            new Vector3(.075f,-.089f,-.207f),new Vector3(.068f,-.083f,-.156f),
            new Vector3(.033f,-.091f,-.097f),new Vector3(.013f,-.106f,-.090f)
        },new[]{.010f,.010f,.0085f,.007f},new[]{.010f,.010f,.0085f,.007f},7),glove);
        Piece(right,"Thumb over stock wrist",Loft("Right curved thumb",new[] {
            new Vector3(.080f,-.083f,-.234f),new Vector3(.056f,-.062f,-.250f),new Vector3(.006f,-.050f,-.264f)
        },new[]{.013f,.012f,.008f},new[]{.012f,.011f,.008f},7),glove);
        Piece(right,"Trigger canvas forearm",Loft("Right tapered sleeve",new[] {
            new Vector3(.250f,-.480f,-.440f),new Vector3(.160f,-.280f,-.320f),new Vector3(.090f,-.185f,-.255f)
        },new[]{.060f,.048f,.035f},new[]{.052f,.042f,.031f},9),sleeve);
        Piece(right,"Trigger wrist cuff",Loft("Right glove cuff",new[] {
            new Vector3(.106f,-.210f,-.273f),new Vector3(.083f,-.175f,-.248f)
        },new[]{.038f,.033f},new[]{.034f,.030f},9),cuff);
    }

    private static Mesh Loft(string name,Vector3[] centres,float[] widths,float[] heights,int sides)
    {
        if(TryMesh(name,out Mesh existing))return existing;
        Builder builder=new Builder();
        Vector3[,] rings=new Vector3[centres.Length,sides];
        for(int ring=0;ring<centres.Length;ring++)
        {
            Vector3 tangent=(centres[Mathf.Min(ring+1,centres.Length-1)]-centres[Mathf.Max(ring-1,0)]).normalized;
            Vector3 across=Vector3.Cross(tangent,Vector3.up);
            if(across.sqrMagnitude<.001f)across=Vector3.right;
            across.Normalize();
            Vector3 depth=Vector3.Cross(tangent,across).normalized;
            for(int segment=0;segment<sides;segment++)
            {
                float angle=segment*Mathf.PI*2f/sides;
                rings[ring,segment]=centres[ring]+across*(Mathf.Cos(angle)*widths[ring])+depth*(Mathf.Sin(angle)*heights[ring]);
            }
        }
        for(int ring=0;ring<centres.Length-1;ring++)
            for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;
                builder.Quad(rings[ring,side],rings[ring,next],rings[ring+1,next],rings[ring+1,side]);
            }
        for(int side=0;side<sides;side++)
        {
            int next=(side+1)%sides;
            builder.Triangle(centres[0],rings[0,next],rings[0,side]);
            int last=centres.Length-1;
            builder.Triangle(centres[last],rings[last,side],rings[last,next]);
        }
        return SaveMesh(name,builder.Build(name));
    }

    private static Texture2D WoodTexture()
    {
        string path = AssetRoot + "/Textures/Walnut Grain.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        Texture2D texture = new Texture2D(64,64,TextureFormat.RGB24,true);
        uint seed=17803;
        for (int y=0;y<64;y++)
            for(int x=0;x<64;x++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                float grain = .87f + .10f*Mathf.Sin(y*.85f + Mathf.Sin(x*.12f)*.8f) + (seed%100)*.0005f;
                texture.SetPixel(x,y,new Color(grain,grain,grain));
            }
        texture.Apply();
        File.WriteAllBytes(path,texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode=FilterMode.Point;
        importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        GameObject go=Piece(parent,name,Cube(),material);
        go.transform.localPosition=position;
        go.transform.localScale=size;
        return go;
    }

    private static GameObject Piece(Transform parent,string name,Mesh mesh,Material material)
    {
        GameObject go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        MeshRenderer renderer=go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
        Camera camera=parent.GetComponentInParent<Camera>();
        renderer.probeAnchor=camera!=null?camera.transform:parent;
        renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        return go;
    }

    private static Mesh Cube()
    {
        return Extrusion("Unit box",1f,new[] {new Vector2(-.5f,-.5f),new Vector2(-.5f,.5f),
            new Vector2(.5f,.5f),new Vector2(.5f,-.5f)});
    }

    // Profile uses x=height, y=forward distance; each edge is hard-normal, preserving the low-poly finish.
    private static Mesh Extrusion(string name,float width,Vector2[] profile)
    {
        if(TryMesh(name,out Mesh existing))return existing;
        Builder b=new Builder();
        for(int i=0;i<profile.Length;i++)
        {
            Vector2 a=profile[i],c=profile[(i+1)%profile.Length];
            b.Quad(new Vector3(-width*.5f,a.x,a.y),new Vector3(width*.5f,a.x,a.y),
                new Vector3(width*.5f,c.x,c.y),new Vector3(-width*.5f,c.x,c.y));
        }
        // Triangulate potentially concave side profiles with an ear clip rather than a fan through empty space.
        List<int> polygon=new List<int>();for(int i=0;i<profile.Length;i++)polygon.Add(i);
        float area=0f;for(int i=0;i<profile.Length;i++){Vector2 a=profile[i],c=profile[(i+1)%profile.Length];area+=a.x*c.y-c.x*a.y;}
        float winding=Mathf.Sign(area);
        int guard=profile.Length*profile.Length;
        while(polygon.Count>2&&guard-->0)
        {
            bool clipped=false;
            for(int i=0;i<polygon.Count;i++)
            {
                int a=polygon[(i+polygon.Count-1)%polygon.Count],c=polygon[i],d=polygon[(i+1)%polygon.Count];
                if(Cross(profile[c]-profile[a],profile[d]-profile[c])*winding<=0f)continue;
                bool inside=false;
                foreach(int q in polygon)
                {
                    if(q==a||q==c||q==d)continue;
                    Vector2 p=profile[q];
                    if(Cross(profile[c]-profile[a],p-profile[a])*winding>=0f&&
                       Cross(profile[d]-profile[c],p-profile[c])*winding>=0f&&
                       Cross(profile[a]-profile[d],p-profile[d])*winding>=0f){inside=true;break;}
                }
                if(inside)continue;
                Vector3 pa=new Vector3(width*.5f,profile[a].x,profile[a].y);
                Vector3 pc=new Vector3(width*.5f,profile[c].x,profile[c].y);
                Vector3 pd=new Vector3(width*.5f,profile[d].x,profile[d].y);
                if(winding>0f)b.Triangle(pa,pc,pd);else b.Triangle(pd,pc,pa);
                pa.x=pc.x=pd.x=-width*.5f;
                if(winding>0f)b.Triangle(pd,pc,pa);else b.Triangle(pa,pc,pd);
                polygon.RemoveAt(i);clipped=true;break;
            }
            if(!clipped)break;
        }
        return SaveMesh(name,b.Build(name));
    }

    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

    private static Mesh Tube(string name,float outer,float inner,float length,int sides)
    {
        if(TryMesh(name,out Mesh existing))return existing;
        Builder b=new Builder();
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2f/sides,c=(i+1)*Mathf.PI*2f/sides;
            Vector3 oa=new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,-length*.5f);
            Vector3 ob=new Vector3(Mathf.Cos(c)*outer,Mathf.Sin(c)*outer,-length*.5f);
            Vector3 ia=new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,-length*.5f);
            Vector3 ib=new Vector3(Mathf.Cos(c)*inner,Mathf.Sin(c)*inner,-length*.5f);
            Vector3 forward=Vector3.forward*length;
            b.Quad(oa,ob,ob+forward,oa+forward);
            b.Quad(ia+forward,ib+forward,ib,ia);
            b.Quad(oa+forward,ob+forward,ib+forward,ia+forward);
            b.Quad(oa,ia,ib,ob);
        }
        return SaveMesh(name,b.Build(name));
    }

    private static Mesh Cylinder(string name,float radius,float length,int sides)
    {
        if(TryMesh(name,out Mesh existing))return existing;
        Builder b=new Builder();
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2f/sides,c=(i+1)*Mathf.PI*2f/sides;
            Vector3 p=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-length*.5f);
            Vector3 q=new Vector3(Mathf.Cos(c)*radius,Mathf.Sin(c)*radius,-length*.5f);
            Vector3 step=Vector3.forward*length;
            b.Quad(p,q,q+step,p+step);
            b.Triangle(new Vector3(0f,0f,length*.5f),p+step,q+step);
            b.Triangle(new Vector3(0f,0f,-length*.5f),q,p);
        }
        return SaveMesh(name,b.Build(name));
    }

    private static Mesh EllipseLoop(string name,float width,float vertical,float length,float thickness,int sides)
    {
        if(TryMesh(name,out Mesh existing))return existing;
        Builder b=new Builder();
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2f/sides,c=(i+1)*Mathf.PI*2f/sides;
            Vector3 p=new Vector3(-width*.5f,Mathf.Sin(a)*vertical,Mathf.Cos(a)*length);
            Vector3 q=new Vector3(-width*.5f,Mathf.Sin(c)*vertical,Mathf.Cos(c)*length);
            Vector3 ip=new Vector3(-width*.5f,Mathf.Sin(a)*(vertical-thickness),Mathf.Cos(a)*(length-thickness));
            Vector3 iq=new Vector3(-width*.5f,Mathf.Sin(c)*(vertical-thickness),Mathf.Cos(c)*(length-thickness));
            Vector3 x=Vector3.right*width;
            b.Quad(p+x,q+x,q,p);b.Quad(ip,iq,iq+x,ip+x);b.Quad(p+x,ip+x,iq+x,q+x);b.Quad(p,q,iq,ip);
        }
        return SaveMesh(name,b.Build(name));
    }

    private static Mesh FlashMesh()
    {
        const string name="Muzzle pressure flame and star V2";
        if(TryMesh(name,out Mesh existing))return existing;
        Builder b=new Builder();
        // A transverse irregular star is visible from first person; long crossed tongues retain a side silhouette.
        Vector3 centre = new Vector3(0f,0f,.075f);
        for(int i=0;i<14;i++)
        {
            float a=i*Mathf.PI*2f/14f, c=(i+1)*Mathf.PI*2f/14f;
            float r1=i%2==0?.19f:.065f, r2=(i+1)%2==0?.19f:.065f;
            Vector3 p=centre+new Vector3(Mathf.Cos(a)*r1,Mathf.Sin(a)*r1,0f);
            Vector3 q=centre+new Vector3(Mathf.Cos(c)*r2,Mathf.Sin(c)*r2,0f);
            b.Triangle(centre,p,q);b.Triangle(centre,q,p);
        }
        for(int i=0;i<3;i++)
        {
            float angle=i*Mathf.PI/3f;
            Vector3 side=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f)*.10f;
            Vector3 tip=new Vector3(0f,0f,.40f);
            b.Triangle(-side,side,tip);b.Triangle(tip,side,-side);
        }
        return SaveMesh(name,b.Build(name));
    }

    private static Material ImpactDustMaterial()
    {
        string path=AssetRoot+"/Materials/Soft Impact Dust.mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        Shader shader=Shader.Find("Facility/Soft Impact Dust");
        if(shader==null)throw new InvalidOperationException("Facility impact dust shader missing.");
        material=new Material(shader){name="Soft Impact Dust",enableInstancing=true};
        material.SetColor("_BaseColor",new Color(.65f,.62f,.58f,.26f));
        AssetDatabase.CreateAsset(material,path);
        return material;
    }

    private static Mesh SmokeMesh()
    {
        const string name="Soft muzzle smoke card";
        if(TryMesh(name,out Mesh existing))return existing;
        Mesh mesh=new Mesh{name=name};
        mesh.vertices=new[]{new Vector3(-.17f,-.17f,0f),new Vector3(.17f,-.17f,0f),new Vector3(.17f,.17f,0f),new Vector3(-.17f,.17f,0f)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
        mesh.triangles=new[]{0,1,2,0,2,3};
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        return SaveMesh(name,mesh);
    }

    private static Mesh BuckshotHoleMesh()
    {
        const string name="Ragged buckshot cavity and chip rim";
        if(TryMesh(name,out Mesh existing))return existing;
        const int sides=14;
        Vector3[] vertices=new Vector3[1+sides*2];
        int[] cavity=new int[sides*3], rim=new int[sides*6];
        vertices[0]=new Vector3(0f,0f,.0004f);
        for(int i=0;i<sides;i++)
        {
            float angle=i*Mathf.PI*2f/sides;
            float irregular=.91f+.09f*Mathf.Sin(i*7.17f);
            vertices[1+i]=new Vector3(Mathf.Cos(angle)*.31f*irregular,Mathf.Sin(angle)*.31f*irregular,.0004f);
            vertices[1+sides+i]=new Vector3(Mathf.Cos(angle)*.5f*irregular,Mathf.Sin(angle)*.5f*irregular,0f);
            int next=(i+1)%sides;
            cavity[i*3]=0;cavity[i*3+1]=1+i;cavity[i*3+2]=1+next;
            int n=i*6;
            rim[n]=1+i;rim[n+1]=1+sides+i;rim[n+2]=1+sides+next;
            rim[n+3]=1+i;rim[n+4]=1+sides+next;rim[n+5]=1+next;
        }
        Mesh mesh=new Mesh{name=name,subMeshCount=2};
        mesh.vertices=vertices;mesh.SetTriangles(cavity,0);mesh.SetTriangles(rim,1);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        return SaveMesh(name,mesh);
    }

    private static bool TryMesh(string name,out Mesh mesh)
    {
        if(MeshCache.TryGetValue(name,out mesh)&&mesh!=null)return true;
        mesh=AssetDatabase.LoadAssetAtPath<Mesh>(AssetRoot+"/Meshes/"+name+".asset");
        if(mesh!=null)MeshCache[name]=mesh;
        return mesh!=null;
    }

    private static Mesh SaveMesh(string name,Mesh mesh)
    {
        AssetDatabase.CreateAsset(mesh,AssetRoot+"/Meshes/"+name+".asset");
        MeshCache[name]=mesh;
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }

    private sealed class Builder
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        private readonly List<Vector2> uv=new List<Vector2>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            uv.Add(new Vector2(a.z*3f,a.y*3f));uv.Add(new Vector2(b.z*3f,b.y*3f));uv.Add(new Vector2(c.z*3f,c.y*3f));
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        public Mesh Build(string name)
        {
            Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
