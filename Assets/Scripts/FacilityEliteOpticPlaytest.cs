using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in saved-elite optical proof: -facilityOpticTest outputDir. Never saves diagnostic objects.</summary>
public sealed class FacilityEliteOpticPlaytest : MonoBehaviour
{
    [Serializable] sealed class Shot
    {
        public string file;
        public float visibility;
        public float[] lensVisibility;
        public int[] redPixelsAtEachLens;
        public int redPixelsNearOptic, hotPixelsNearOptic;
    }
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "The actual saved quad-goggle elite wave prefab, photographed on a temporary dim neutral stage through a dedicated URP camera. Front head at 1.7 m, full body at 4 m, gameplay distance at 10 m, side, rear, 35-degree oblique helmet/mount view, a real opaque collider wall, and head destruction through three distinct existing regional pellet hits. All four optical controllers are checked individually for front visibility, cover/rear suppression and complete removal with the head. Actual VisibilityFor values and rendered red pixels accompany each image. No gameplay assets or player account are saved.";
        public List<string> checks = new List<string>(), failures = new List<string>();
        public List<Shot> images = new List<Shot>();
        public float frontVisibility, sideVisibility, rearVisibility, wallVisibility;
        public int opticCount;
        public int roundPhysicalLensCount;
        public Vector3[] lensHeadLocalPositions;
        public bool gogglesBreakAwayOnFirstHit, headDestroyed, glowDisabledAfterHeadDestruction;
    }
    readonly Receipt receipt = new Receipt();
    FacilityGameSession session;
    FacilityHumanoid actor;
    FacilityEliteOptic[] optics;
    Transform goggleRig;
    GameObject stage, wall;
    Camera camera;
    string output;
    bool collecting;
    IDisposable account;
    int shotId = 1920000000;
    Vector3 faceCentre, bodyCentre, front, right;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-facilityOpticTest");
        if (index < 0) return;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal)) { Application.Quit(2); return; }
        var runner = new GameObject("Elite optical regression - CLI only").AddComponent<FacilityEliteOpticPlaytest>();
        runner.output = args[index + 1]; DontDestroyOnLoad(runner.gameObject);
    }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        Directory.CreateDirectory(output); Application.runInBackground = true;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        account = FacilityEconomy.UseIsolatedAccount(); collecting = true; Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object next = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error) { receipt.failures.Add(error.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        collecting = false; Application.logMessageReceived -= OnLog; account?.Dispose();
        Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "optic-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.6f); session = FacilityGameSession.Instance;
        Require(session != null, "The saved title session loads.");
        FacilityGameSession previous = session; session.StartStory();
        float until = Time.realtimeSinceStartup + 15f;
        while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance == previous) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(.3f); session = FacilityGameSession.Instance;
        Require(session != null && session != previous && session.CurrentMode == FacilityGameMode.Story &&
            session.CurrentState == FacilityGameState.Playing && session.storyDirector != null,
            "The real story session loads through its normal title action.");
        var waves = session.storyDirector.policeWaves;
        Require(waves != null && waves.elitePrefab != null, "The final squad retains an actual saved elite wave source.");
        waves.StopAssault();
        foreach (FacilityHumanoid existing in FindObjectsByType<FacilityHumanoid>(FindObjectsSortMode.None))
        {
            existing.enabled = false;
            var gun = existing.GetComponent<FacilityPoliceOfficer>(); if (gun != null) gun.enabled = false;
            var guard = existing.GetComponent<FacilityKnifeGuard>(); if (guard != null) guard.enabled = false;
        }
        session.player.enabled = false; session.canvas.enabled = false;
        foreach (Transform model in new[] { session.inventory.fistsModel, session.inventory.pistolModel,
            session.inventory.shotgunModel, session.inventory.ak47Model }) if (model != null) model.gameObject.SetActive(false);
        BuildStage(waves.elitePrefab);
        yield return null;
        var elite = actor.GetComponent<FacilityEliteSwat>();
        receipt.opticCount = optics.Length;
        Require(optics.Length == 4 && elite.opticRenderers != null && elite.opticRenderers.Length == 4,
            "The actual saved elite helmet contains exactly four optical controllers and four saved quad-goggle renderer references.");
        receipt.lensHeadLocalPositions = new Vector3[4];
        for (int i = 0; i < optics.Length; i++)
        {
            var optic = optics[i];
            Require(optic != null && optic.glowRenderer != null && optic.glowRenderer.gameObject.activeInHierarchy,
                "Quad lens " + (i + 1) + " has an authored active glow renderer.");
            Require(optic.transform.IsChildOf(actor.head), "Quad lens " + (i + 1) + " follows the real damageable head.");
            Require(Array.IndexOf(elite.opticRenderers,optic.glowRenderer) >= 0,
                "Quad lens " + (i + 1) + " is one of the elite variant's saved optical renderer references.");
            receipt.lensHeadLocalPositions[i] = actor.head.InverseTransformPoint(optic.transform.position);
            for(int j=0;j<i;j++) Require(Vector3.Distance(optic.transform.position,optics[j].transform.position) > .015f,
                "Quad lens " + (i + 1) + " has its own physical position distinct from lens " + (j + 1) + ".");
        }
        foreach(Transform node in actor.head.GetComponentsInChildren<Transform>(true))
            if(node.name=="Elite quad night vision goggles - helmet mounted")goggleRig=node;
        Require(goggleRig != null && goggleRig.gameObject.activeInHierarchy && elite.helmetPlates != null &&
            elite.helmetPlates.Length > 0 && goggleRig.IsChildOf(elite.helmetPlates[0].transform),
            "The physical quad-goggle rig and helmet mount follow the first breakaway helmet plate.");
        bool housingFound=false;
        foreach(var filter in goggleRig.GetComponentsInChildren<MeshFilter>(true))
            if(filter.sharedMesh != null && filter.name=="Elite quad night vision housings and helmet mount")housingFound=true;
        Require(housingFound,"The quad goggles have a saved physical tube-housing and helmet-mount mesh.");
        foreach(var lens in goggleRig.GetComponentsInChildren<Renderer>(true))
        {
            if(!lens.name.StartsWith("Elite night vision glass lens ",StringComparison.Ordinal))continue;
            var filter=lens.GetComponent<MeshFilter>();Vector3 size=filter!=null&&filter.sharedMesh!=null?filter.sharedMesh.bounds.size:Vector3.zero;
            Require(filter!=null && filter.sharedMesh!=null && filter.sharedMesh.vertexCount>=12 && size.y>.015f &&
                size.x/size.y>.75f && size.x/size.y<1.3f,
                lens.name+" uses a physical round inset glass surface with comparable horizontal and vertical diameter.");
            receipt.roundPhysicalLensCount++;
        }
        Require(receipt.roundPhysicalLensCount==4,"The helmet-mounted rig has four distinct physical round lens surfaces.");

        Pose(faceCentre + front * 1.7f, faceCentre, 38f);
        receipt.frontVisibility = MinimumVisibility();
        Require(receipt.frontVisibility > .8f, "An unobstructed frontal camera receives strong visibility from all four optical lenses.");
        yield return Capture("01_front_head_1_7m");
        Require(receipt.images[receipt.images.Count - 1].redPixelsNearOptic > 2,
            "The actual frontal URP image contains visible red optical pixels around the physical eye position.");

        Pose(bodyCentre + front * 4f + Vector3.up * .08f, bodyCentre, 38f);
        yield return Capture("02_full_body_4m");
        Pose(actor.transform.position + Vector3.up * 1.65f + front * 10f, actor.transform.position + Vector3.up * 1.10f, 60f);
        yield return Capture("03_gameplay_distance_10m");

        Pose(faceCentre + right * 1.7f, faceCentre, 38f);
        receipt.sideVisibility = MaximumVisibility(); yield return Capture("04_side_profile");
        Pose(faceCentre - front * 1.7f, faceCentre, 38f);
        receipt.rearVisibility = MaximumVisibility();
        Require(receipt.rearVisibility <= .001f, "A camera behind the helmet receives zero bloom visibility from all four forward-facing lenses.");
        yield return Capture("05_rear_no_optic");
        Require(receipt.images[receipt.images.Count - 1].redPixelsNearOptic <= 2,
            "The actual rear URP image contains no visible red bloom through the solid helmet.");

        Pose(faceCentre + front * 1.7f, faceCentre, 38f);
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Temporary opaque optic occlusion wall";
        wall.transform.SetParent(stage.transform, true); wall.transform.position = faceCentre + front * .75f;
        wall.transform.rotation = Quaternion.LookRotation(front); wall.transform.localScale = new Vector3(2.6f,2.8f,.20f);
        wall.GetComponent<Renderer>().sharedMaterial = Material("Temporary neutral occlusion wall",new Color(.045f,.052f,.061f));
        Physics.SyncTransforms(); yield return null;
        receipt.wallVisibility = MaximumVisibility();
        Require(receipt.wallVisibility <= .001f, "A real opaque wall collider between the quad goggles and frontal camera suppresses all four optical sources.");
        yield return Capture("06_wall_occludes_optic");
        Require(receipt.images[receipt.images.Count - 1].redPixelsNearOptic <= 2,
            "The actual wall URP image contains no optical glow leaking through the occluder.");
        wall.SetActive(false); Physics.SyncTransforms();
        Require(MinimumVisibility() > .8f, "Removing that same wall immediately restores frontal visibility for all four lenses.");

        Vector3 obliqueDirection=front*Mathf.Cos(35f*Mathf.Deg2Rad)+right*Mathf.Sin(35f*Mathf.Deg2Rad);
        Pose(faceCentre + obliqueDirection*1.7f + Vector3.up*.07f,faceCentre,38f);
        yield return Capture("08_oblique_35deg_goggle_tubes_and_mount");

        FacilityHitRegion head = actor.head.GetComponent<FacilityHitRegion>(); Collider headCollider = head.GetComponent<Collider>();
        Require(head != null && headCollider != null && actor.helmetHitsToBreak == 2,
            "The diagnostic targets the saved physical armored head with two resistant helmet hits.");
        for (int i = 0; i < 3; i++)
        {
            actor.ReceivePellet(++shotId, headCollider.bounds.center, -front, headCollider);
            if(i==0)
            {
                receipt.gogglesBreakAwayOnFirstHit=!goggleRig.gameObject.activeInHierarchy;
                foreach(var optic in optics)receipt.gogglesBreakAwayOnFirstHit &= !optic.glowRenderer.gameObject.activeInHierarchy;
                Require(receipt.gogglesBreakAwayOnFirstHit && MaximumVisibility()<=.001f,
                    "The first real armored-head hit breaks away the mounted quad goggles and disables all four optical flares together.");
            }
            if (i < 2) Require(!actor.HeadExploded && actor.head.gameObject.activeInHierarchy,
                "Helmet hit " + (i + 1) + " preserves the live head before the final penetrating headshot.");
        }
        FreezeBodies();
        receipt.headDestroyed = actor.HeadExploded && actor.IsDead && !actor.head.gameObject.activeInHierarchy;
        receipt.glowDisabledAfterHeadDestruction = true;
        foreach(var optic in optics) receipt.glowDisabledAfterHeadDestruction &= !optic.glowRenderer.gameObject.activeInHierarchy;
        Require(receipt.headDestroyed && receipt.glowDisabledAfterHeadDestruction,
            "The third actual regional headshot destroys the head and removes all four optical sources with the goggles and helmet.");
        Require(MaximumVisibility() <= .001f, "A destroyed head cannot report residual visibility from any of its four optical sources.");
        Pose(faceCentre + front * 1.7f, faceCentre - Vector3.up * .12f, 38f);
        yield return Capture("07_head_destroyed_no_floating_glow");
    }

    void BuildStage(GameObject prefab)
    {
        stage = new GameObject("Optic diagnostic stage - runtime only");
        Vector3 origin = new Vector3(250f,25f,250f);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Temporary neutral dark floor";
        floor.transform.SetParent(stage.transform, false); floor.transform.position = origin - Vector3.up * .12f;
        floor.transform.localScale = new Vector3(32f,.24f,32f);
        floor.GetComponent<Renderer>().sharedMaterial = Material("Temporary charcoal diagnostic floor",new Color(.045f,.052f,.06f));
        var lamp = new GameObject("Temporary dim neutral portrait light"); lamp.transform.SetParent(stage.transform,false);
        lamp.transform.position = origin + new Vector3(-2.4f,3.6f,3.8f);
        Light light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.intensity = 4.2f; light.range = 13f;
        light.color = new Color(.83f,.88f,1f); light.shadows = LightShadows.Soft;
        RenderSettings.fog = false;
        GameObject copy = Instantiate(prefab,origin + Vector3.up * .03f,Quaternion.identity,stage.transform);
        copy.name = "Actual saved elite optical subject"; actor = copy.GetComponent<FacilityHumanoid>();
        actor.player = session.player.transform; actor.gore = FindFirstObjectByType<FacilityGorePool>(); copy.SetActive(true);
        actor.enabled = false; actor.GetComponent<FacilityPoliceOfficer>().enabled = false;
        if (actor.motor != null) actor.motor.enabled = false; FreezeBodies();
        optics = actor.GetComponentsInChildren<FacilityEliteOptic>(true);
        Require(optics.Length == 4, "The saved elite prefab instantiates four optical controllers without generating goggle equipment at runtime.");
        faceCentre = Vector3.zero;
        foreach(var optic in optics)
        {
            Require(optic.glowRenderer != null,"Every saved quad optical controller references its physical glow renderer.");
            faceCentre += optic.transform.position;
        }
        faceCentre /= optics.Length; bodyCentre = origin + Vector3.up * .99f;
        front = actor.head.forward; right = actor.head.right;
        var node = new GameObject("Temporary optical URP camera"); node.transform.SetParent(stage.transform,false);
        camera = node.AddComponent<Camera>(); camera.CopyFrom(session.player.GetComponentInChildren<Camera>());
        camera.enabled = false; camera.aspect = 1.6f; camera.nearClipPlane = .04f; camera.farClipPlane = 40f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.012f,.016f,.022f);
        var data = camera.GetUniversalAdditionalCameraData(); data.renderShadows = true; data.renderPostProcessing = false;
        Physics.SyncTransforms();
    }

    void FreezeBodies()
    {
        if (actor.bodies == null) return;
        foreach (Rigidbody body in actor.bodies) if (body != null) body.isKinematic = true;
    }
    static Material Material(string name,Color color)
    {var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.06f);return material;}
    void Pose(Vector3 position,Vector3 aim,float fieldOfView)
    {camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(aim-position));camera.fieldOfView=fieldOfView;Physics.SyncTransforms();}
    float MinimumVisibility()
    {float result=1f;foreach(var optic in optics)result=Mathf.Min(result,optic.VisibilityFor(camera));return result;}
    float MaximumVisibility()
    {float result=0f;foreach(var optic in optics)result=Mathf.Max(result,optic.VisibilityFor(camera));return result;}

    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var shot = new Shot {file=name+".png",visibility=MaximumVisibility(),lensVisibility=new float[optics.Length],redPixelsAtEachLens=new int[optics.Length]};
        for(int i=0;i<optics.Length;i++)shot.lensVisibility[i]=optics[i].VisibilityFor(camera);
        RenderTexture active = RenderTexture.active;
        var target = new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32); target.Create();
        var pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
            Vector3 projected=camera.WorldToViewportPoint(faceCentre); int cx=Mathf.RoundToInt(projected.x*1600),cy=Mathf.RoundToInt(projected.y*1000);
            Color32[] data=pixels.GetPixels32();
            for(int y=Mathf.Max(0,cy-75);y<Mathf.Min(1000,cy+75);y++)
                for(int x=Mathf.Max(0,cx-150);x<Mathf.Min(1600,cx+150);x++)
                {
                    Color32 color=data[y*1600+x];
                    if(color.r>=120 && color.g<color.r*.42f && color.b<color.r*.42f)shot.redPixelsNearOptic++;
                    if(color.r>=225 && color.g>=100 && color.b<200)shot.hotPixelsNearOptic++;
                }
            for(int i=0;i<optics.Length;i++)
            {
                Vector3 lensPoint=camera.WorldToViewportPoint(optics[i].transform.position);
                int lx=Mathf.RoundToInt(lensPoint.x*1600),ly=Mathf.RoundToInt(lensPoint.y*1000);
                for(int y=Mathf.Max(0,ly-18);y<Mathf.Min(1000,ly+18);y++)
                    for(int x=Mathf.Max(0,lx-18);x<Mathf.Min(1600,lx+18);x++)
                    {Color32 color=data[y*1600+x];if(color.r>=120&&color.g<color.r*.42f&&color.b<color.r*.42f)shot.redPixelsAtEachLens[i]++;}
            }
            File.WriteAllBytes(Path.Combine(output,shot.file),pixels.EncodeToPNG());receipt.images.Add(shot);
        }
        finally {RenderTexture.active=active;Destroy(pixels);target.Release();Destroy(target);}
    }
    void Require(bool condition,string description)
    {if(!condition)throw new InvalidOperationException(description);receipt.checks.Add(description);}
    void OnLog(string message,string trace,LogType kind)
    {if(collecting && (kind==LogType.Error || kind==LogType.Exception || kind==LogType.Assert))receipt.failures.Add(message+"\n"+trace);}
}
