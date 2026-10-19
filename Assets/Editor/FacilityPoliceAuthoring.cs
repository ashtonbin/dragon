using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicitly saves the police anatomy, uniform, firearm and shared projectile resources.</summary>
public static class FacilityPoliceAuthoring
{
    public const string Root = "Assets/Generated/Police";

    public static GameObject BuildPrefab()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Police authoring requires Edit mode.");
        Prepare();
        GameObject temporary = new GameObject("Police prefab authoring - temporary");
        try
        {
            FacilityHumanoid actor = HumanoidModelAuthoring.Build(Vector3.zero, null, temporary.transform);
            GameObject root = actor.gameObject;
            root.name = "Armed police officer";
            root.SetActive(false);
            actor.IsHostile = true; actor.pursuitSpeed = 2.6f;
            Material cloth = Mat("Police navy woven uniform", new Color(.095f, .135f, .205f), false, true);
            Material vest = Mat("Police ballistic vest", new Color(.055f, .065f, .072f), false, true);
            Material steel = Mat("Police pistol worn steel", new Color(.15f, .17f, .185f));
            Material grip = Mat("Police pistol polymer grip", new Color(.048f, .047f, .044f), false, true);
            Material badge = Mat("Police brass insignia", new Color(.57f, .48f, .24f));
            Material bright = Mat("Police sight ivory", new Color(.80f, .82f, .72f));
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial == null) continue;
                if (renderer.sharedMaterial.name == "Coverall - faded sage") renderer.sharedMaterial = cloth;
                else if (renderer.sharedMaterial.name == "Boots and utility belt") renderer.sharedMaterial = vest;
            }
            Box(actor.chest, "Fitted ballistic vest front", new Vector3(0, -.015f, .155f), new Vector3(.36f,.35f,.045f), vest);
            Box(actor.chest, "Fitted ballistic vest back", new Vector3(0,-.015f,-.15f), new Vector3(.36f,.35f,.035f), vest);
            for (int side=-1;side<=1;side+=2)
            {
                Box(actor.chest,"Vest shoulder webbing",new Vector3(side*.14f,.16f,.145f),new Vector3(.065f,.12f,.025f),vest);
                Box(actor.chest,"Radio and magazine pouch",new Vector3(side*.11f,-.09f,.19f),new Vector3(.10f,.12f,.045f),vest);
            }
            Label(actor.chest, "Front police identification", new Vector3(0,.08f,.184f), Quaternion.identity);
            Label(actor.chest, "Rear police identification", new Vector3(0,.07f,-.171f), Quaternion.Euler(0,180,0));
            Box(actor.head,"Navy patrol cap crown",new Vector3(0,.13f,-.006f),new Vector3(.275f,.10f,.255f),cloth);
            Box(actor.head,"Cap visor",new Vector3(0,.095f,.132f),new Vector3(.255f,.025f,.135f),vest);
            Box(actor.head,"Cap shield badge",new Vector3(0,.14f,.125f),new Vector3(.035f,.043f,.008f),badge);
            FacilityHitRegion hand=null;
            foreach(FacilityHitRegion region in actor.hitRegions)
                if(region!=null&&region.kind==FacilityBodyRegion.ForearmRight){hand=region;break;}
            if(hand==null)throw new InvalidOperationException("The police anatomy has no gun hand.");
            Transform gun = new GameObject("Held police service pistol").transform;
            gun.SetParent(hand.transform,false);gun.localPosition=new Vector3(0,-.322f,.035f);
            gun.localRotation=Quaternion.Euler(90,0,0);
            Box(gun,"Service pistol receiver",new Vector3(0,0,.055f),new Vector3(.045f,.045f,.205f),steel);
            Box(gun,"Squared steel slide",new Vector3(0,.031f,.062f),new Vector3(.046f,.034f,.22f),steel);
            Transform butt=Box(gun,"Angled polymer grip",new Vector3(0,-.062f,-.023f),new Vector3(.043f,.112f,.060f),grip);
            butt.localRotation=Quaternion.Euler(-13,0,0);
            Box(gun,"Magazine base plate",new Vector3(0,-.12f,-.035f),new Vector3(.050f,.012f,.066f),steel);
            Box(gun,"Trigger guard lower",new Vector3(0,-.043f,.054f),new Vector3(.020f,.010f,.077f),steel);
            Box(gun,"Trigger guard front",new Vector3(0,-.026f,.090f),new Vector3(.020f,.043f,.010f),steel);
            Box(gun,"Rear iron sight",new Vector3(0,.054f,-.027f),new Vector3(.041f,.012f,.010f),steel);
            Box(gun,"Front iron sight",new Vector3(0,.054f,.145f),new Vector3(.008f,.009f,.013f),bright);
            Box(gun,"Black muzzle bore",new Vector3(0,.028f,.174f),new Vector3(.023f,.023f,.004f),grip);
            Transform tip=new GameObject("Police pistol muzzle").transform;tip.SetParent(gun,false);tip.localPosition=new Vector3(0,.028f,.180f);
            Material amber=Mat("Police muzzle amber",new Color(1f,.55f,.12f),true);
            Transform warning=Box(gun,"Aim warning indicator",new Vector3(.024f,.013f,.14f),new Vector3(.015f,.017f,.024f),amber);
            warning.gameObject.SetActive(false);
            Transform flash=Box(tip,"Police shot flash",new Vector3(0,0,.045f),new Vector3(.08f,.06f,.12f),amber);flash.gameObject.SetActive(false);
            FacilityPoliceOfficer officer=root.AddComponent<FacilityPoliceOfficer>();
            officer.actor=actor;officer.gunTip=tip;officer.muzzleFlash=flash.gameObject;officer.aimWarning=warning.gameObject;
            officer.walkSpeed=2.6f;officer.aimWindup=.42f;officer.shotInterval=1.65f;
            AudioSource source=gun.gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=1f;source.minDistance=2f;source.maxDistance=32f;
            source.rolloffMode=AudioRolloffMode.Linear;source.dopplerLevel=0f;source.volume=.45f;
            officer.gunSource=source;
            officer.gunReport=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Generated/Arsenal/Audio/Service Pistol Concussion V1.wav");
            foreach(SubtleEnemyHealthBar bar in root.GetComponentsInChildren<SubtleEnemyHealthBar>(true))bar.player=null;
            FacilityBankAuthoring.Expression(actor);
            string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/Prefabs/Armed Police Officer.prefab");
            GameObject prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
            if(prefab==null)throw new InvalidOperationException("Could not save the armed police prefab.");
            AssetDatabase.SaveAssets();return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }
    }

    public static void ConfigureProjectiles(FacilityPoliceProjectiles projectiles)
    {
        if(projectiles==null)throw new ArgumentNullException(nameof(projectiles));
        Prepare();
        projectiles.projectileMesh=new FacilityAssetLibrary().BoxMesh(Vector3.one);
        projectiles.projectileMaterial=Mat("Police visible amber projectile",new Color(1f,.37f,.045f),true);
        projectiles.projectileSpeed=13f;projectiles.projectileRadius=.055f;
        EditorUtility.SetDirty(projectiles);AssetDatabase.SaveAssets();
    }

    private static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        GameObject go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
        go.GetComponent<MeshFilter>().sharedMesh=new FacilityAssetLibrary().BoxMesh(Vector3.one);
        MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        return go.transform;
    }

    private static void Label(Transform parent,string name,Vector3 position,Quaternion rotation)
    {
        GameObject label=new GameObject(name,typeof(TextMesh));label.transform.SetParent(parent,false);
        label.transform.localPosition=position;label.transform.localRotation=rotation;
        TextMesh text=label.GetComponent<TextMesh>();text.text="POLICE";text.anchor=TextAnchor.MiddleCenter;
        text.alignment=TextAlignment.Center;text.characterSize=.025f;text.fontSize=40;text.color=new Color(.71f,.74f,.68f);
        string[] fonts=AssetDatabase.FindAssets("JetBrainsMono-Regular t:Font");
        text.font=fonts.Length>0?AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(fonts[0])):Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
        label.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
    }

    private static Material Mat(string name,Color tint,bool unlit=false,bool woven=false)
    {
        string path=Root+"/Materials/"+name+".mat";
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat!=null)return mat;
        mat=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
        mat.SetColor("_BaseColor",tint);mat.SetFloat("_Smoothness",unlit?0f:.12f);
        if(woven)
        {
            string texturePath=Root+"/Textures/"+name+".png";
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {
                Texture2D generated=new Texture2D(64,64,TextureFormat.RGB24,false){filterMode=FilterMode.Point};
                Color[] pixels=new Color[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float value=((x+y)%3==0?.92f:1f)*(1f+((x*31+y*17)%11-5)*.007f);
                    if(x<2||x>61)value*=.78f;
                    if((x==3||x==60)&&y%4<2)value*=1.18f;
                    pixels[y*64+x]=Color.white*value;
                }
                generated.SetPixels(pixels);generated.Apply();File.WriteAllBytes(texturePath,generated.EncodeToPNG());UnityEngine.Object.DestroyImmediate(generated);
                AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
            mat.SetTexture("_BaseMap",texture);
        }
        if(!unlit){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",tint*.045f);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;}
        AssetDatabase.CreateAsset(mat,path);return mat;
    }

    private static void Prepare()
    {
        foreach(string directory in new[]{Root,Root+"/Prefabs",Root+"/Materials",Root+"/Textures"})
        {
            if(AssetDatabase.IsValidFolder(directory))continue;
            string parent=Path.GetDirectoryName(directory).Replace('\\','/');
            AssetDatabase.CreateFolder(parent,Path.GetFileName(directory));
        }
    }
}
