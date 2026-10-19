using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Opt-in Windows check: -facilityPoliceTest "output directory". Never runs in normal play.</summary>
public sealed class FacilityPoliceSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class Receipt
    {
        public bool success, savedPolicePrefab, aimTelegraphDelaysShot, visibleFiniteFlight, naturalPoliceRoundDamages,
            redFlashOnGunshot, walkingStrafeDodgesLockedAim, closedCoverPreventsShooting, thinWallStopsFastSweep,
            obstructedMuzzleCannotFireThroughWall, pauseFreezesProjectiles, projectilePoolBounded, projectilesExpire;
        public int shots, playerImpacts, worldImpacts, capacity, healthAfterFirstHit;
        public float actualWindupSeconds, projectileSpeed, strafeMetres;
        public string utc, unity, gpu;
        public string coverage = "The saved Story police prefab and its real gun brain, 0.42-second committed aim warning, visible 13 m/s projectile flight, natural one-hit damage/red flash, actual CharacterController walking strafe from the locked aim, live closed cover suppressing police fire, 25 mm cover blocking an 80 m/s swept test round, obstructed muzzle rejection, paused projectiles, 150-round bounded-pool saturation and expiry. A temporary remote firing stage keeps mission actors out of these deterministic combat assertions; mission reinforcement timing and scene traversal are checked separately. No authored scene or asset is saved.";
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private string output;
    private bool collecting;
    private FacilityGameSession session;
    private RetroFPSController player;
    private CharacterController motor;
    private FacilityPlayerHealth health;
    private FacilityPoliceProjectiles projectiles;
    private FacilityPoliceOfficer officer;
    private GameObject stage;
    private Material stageMaterial;
    private readonly Vector3 feet = new Vector3(100f,.05f,100f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-facilityPoliceTest");
        if(flag<0)return;
        if(flag+1>=args.Length||args[flag+1].StartsWith("-",StringComparison.Ordinal))
        {Debug.LogError("-facilityPoliceTest requires an output directory.");Application.Quit(2);return;}
        FacilityPoliceSmokeTest runner=new GameObject("Police diagnostics - explicit CLI only").AddComponent<FacilityPoliceSmokeTest>();
        runner.output=args[flag+1];DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if(string.IsNullOrEmpty(output)){enabled=false;yield break;}
        output=Path.GetFullPath(output);Directory.CreateDirectory(output);
        collecting=true;Application.logMessageReceived+=OnLog;
        Stack<IEnumerator> routines=new Stack<IEnumerator>();routines.Push(Run());
        while(routines.Count>0)
        {
            IEnumerator routine=routines.Peek();bool more;object current=null;
            try{more=routine.MoveNext();if(more)current=routine.Current;}
            catch(Exception exception){receipt.failures.Add(exception.ToString());break;}
            if(!more){routines.Pop();continue;}
            if(current is IEnumerator nested)routines.Push(nested);else yield return current;
        }
        collecting=false;Application.logMessageReceived-=OnLog;Time.timeScale=1f;AudioListener.pause=false;
        receipt.success=receipt.failures.Count==0;
        File.WriteAllText(Path.Combine(output,"police-receipt.json"),JsonUtility.ToJson(receipt,true));
        Debug.Log("Facility police diagnostics "+(receipt.success?"passed: ":"failed: ")+output);
        Application.Quit(receipt.success?0:1);
    }

    private IEnumerator Run()
    {
        Application.runInBackground=true;yield return new WaitForSecondsRealtime(.5f);
        session=FacilityGameSession.Instance;Require(session!=null,"No startup game session.");session.StartStory();
        yield return new WaitForSecondsRealtime(.7f);
        session=FacilityGameSession.Instance;
        Require(session!=null&&session.CurrentMode==FacilityGameMode.Story,"Story did not load for police diagnostics.");
        FacilityPoliceWaves waves=FindFirstObjectByType<FacilityPoliceWaves>();
        Require(waves!=null&&waves.policePrefab!=null&&waves.projectiles!=null,"Story lacks its saved police or projectile system.");
        waves.StopAssault();projectiles=waves.projectiles;player=session.player;health=session.health;motor=player.GetComponent<CharacterController>();
        receipt.savedPolicePrefab=waves.policePrefab.GetComponent<FacilityPoliceOfficer>()!=null&&!waves.policePrefab.activeSelf;
        Check(receipt.savedPolicePrefab,"The wave source is not a saved inactive armed police template.");
        receipt.utc=DateTime.UtcNow.ToString("O");receipt.unity=Application.unityVersion;receipt.gpu=SystemInfo.graphicsDeviceName;
        receipt.projectileSpeed=projectiles.projectileSpeed;receipt.capacity=FacilityPoliceProjectiles.Capacity;
        MeshRenderer authoredRenderer=session.inventory.pistolModel.GetComponentInChildren<MeshRenderer>(true);
        Require(authoredRenderer!=null&&authoredRenderer.sharedMaterial!=null,"The authored pistol has no persistent URP material for diagnostic geometry.");
        stageMaterial=authoredRenderer.sharedMaterial;
        stage=new GameObject("Police diagnostic firing stage - runtime only");
        Box("Temporary firing floor",new Vector3(100,-.1f,104),new Vector3(24,.2f,24));
        Box("Safe projectile backstop",new Vector3(100,1.6f,96),new Vector3(18,3.2f,.2f));
        Teleport(feet);player.enabled=false;player.ResetRecoil();
        Camera eye=player.GetComponentInChildren<Camera>();eye.transform.localRotation=Quaternion.identity;
        GameObject lighting=new GameObject("Diagnostic readability light",typeof(Light));lighting.transform.SetParent(stage.transform,false);
        lighting.transform.position=new Vector3(100,3.5f,104);Light point=lighting.GetComponent<Light>();point.type=LightType.Point;
        point.intensity=3f;point.range=13f;point.shadows=LightShadows.None;point.color=new Color(.7f,.8f,1f);
        GameObject cop=Instantiate(waves.policePrefab,stage.transform,false);cop.SetActive(false);
        cop.transform.SetPositionAndRotation(new Vector3(100,.035f,108),Quaternion.Euler(0,180,0));
        FacilityHumanoid actor=cop.GetComponent<FacilityHumanoid>();actor.player=player.transform;actor.IsHostile=true;
        officer=cop.GetComponent<FacilityPoliceOfficer>();officer.actor=actor;officer.target=health;officer.projectileSystem=projectiles;
        foreach(SubtleEnemyHealthBar bar in cop.GetComponentsInChildren<SubtleEnemyHealthBar>(true))bar.player=player.transform;
        cop.SetActive(true);Physics.SyncTransforms();yield return null;
        yield return Until(()=>officer.IsAiming,3f,"The natural police gun never began its aim telegraph.");
        float warnedAt=Time.time;int before=officer.ShotsFired;
        Require(officer.aimWarning!=null&&officer.aimWarning.activeInHierarchy,"The police gun's aim warning is invisible.");
        yield return new WaitForSeconds(.2f);
        Check(officer.ShotsFired==before,"A police round fired before its windup completed.");
        yield return Capture("01_police_committed_aim");
        yield return Until(()=>officer.ShotsFired>before,1f,"A committed police shot never fired.");
        receipt.actualWindupSeconds=Time.time-warnedAt;
        receipt.aimTelegraphDelaysShot=receipt.actualWindupSeconds>=officer.aimWindup-.035f;
        Check(receipt.aimTelegraphDelaysShot,"The authored police aim warning did not delay the round.");
        receipt.visibleFiniteFlight=projectiles.ActiveProjectiles>0&&health.HitsTaken==0;
        yield return new WaitForSeconds(.08f);
        receipt.visibleFiniteFlight&=projectiles.ActiveProjectiles>0&&health.HitsTaken==0;
        Check(receipt.visibleFiniteFlight,"A police round behaved like hitscan instead of remaining visibly in flight.");
        yield return Capture("02_visible_round_in_flight");
        yield return Until(()=>health.HitsTaken>0,1.2f,"The unobstructed natural police round never damaged the player.");
        receipt.naturalPoliceRoundDamages=health.HitsTaken==1&&health.MaxHits==3&&projectiles.PlayerImpacts==1;
        receipt.redFlashOnGunshot=health.RedFlash>.2f;receipt.healthAfterFirstHit=health.CurrentHealth;
        Check(receipt.naturalPoliceRoundDamages,"The natural police shot did not produce exactly one of the player's three hits.");
        Check(receipt.redFlashOnGunshot,"A police hit did not flash the damage overlay.");
        yield return Capture("03_natural_police_hit");health.ResetHealth();

        yield return Until(()=>officer.IsAiming,2.5f,"The second natural police aim never began.");
        Vector3 oldFeet=player.transform.position;float moved=0f;
        while(moved<2.35f)
        {
            float distance=Mathf.Min(6f*Time.deltaTime,2.35f-moved);motor.Move(Vector3.right*distance);moved+=distance;yield return null;
        }
        receipt.strafeMetres=Vector3.Distance(oldFeet,player.transform.position);
        yield return new WaitForSeconds(.8f);
        receipt.walkingStrafeDodgesLockedAim=health.HitsTaken==0&&receipt.strafeMetres>2.2f;
        Check(receipt.walkingStrafeDodgesLockedAim,"A walking strafe could not dodge the committed projectile.");
        yield return Capture("04_strafe_avoids_committed_round");

        GameObject cover=Box("25 mm closed steel cover",new Vector3(100,1.6f,104),new Vector3(8,3.2f,.025f));
        Teleport(feet);Physics.SyncTransforms();projectiles.Clear();
        before=officer.ShotsFired;yield return new WaitForSeconds(2.2f);
        receipt.closedCoverPreventsShooting=officer.ShotsFired==before&&health.HitsTaken==0;
        Check(receipt.closedCoverPreventsShooting,"Police kept firing through closed cover.");
        officer.enabled=false;
        int beforeWorld=projectiles.WorldImpacts;int beforeHits=projectiles.PlayerImpacts;
        projectiles.projectileSpeed=80f;
        Require(projectiles.Fire(new Vector3(100,1.05f,107),Vector3.back,officer.transform,health),"The thin-wall swept round did not launch.");
        yield return new WaitForSeconds(.14f);
        receipt.thinWallStopsFastSweep=projectiles.WorldImpacts==beforeWorld+1&&projectiles.PlayerImpacts==beforeHits&&health.HitsTaken==0;
        Check(receipt.thinWallStopsFastSweep,"An 80 m/s round crossed the 25 mm cover instead of sweeping into it.");
        beforeWorld=projectiles.WorldImpacts;
        bool firedInside=projectiles.Fire(new Vector3(100,1.05f,104),Vector3.back,officer.transform,health);
        receipt.obstructedMuzzleCannotFireThroughWall=!firedInside&&projectiles.WorldImpacts==beforeWorld+1;
        Check(receipt.obstructedMuzzleCannotFireThroughWall,"A muzzle embedded in cover spawned a projectile on its far side.");
        cover.SetActive(false);projectiles.projectileSpeed=13f;

        projectiles.Clear();Require(projectiles.Fire(new Vector3(100,2.2f,108),Vector3.right,officer.transform,health),"The pause round did not launch.");
        yield return null;Transform tracer=ActiveTracer();Require(tracer!=null,"No visible tracer existed for the pause test.");
        session.PauseGame();Vector3 frozen=tracer.position;float frozenTime=Time.time;
        yield return new WaitForSecondsRealtime(.2f);
        receipt.pauseFreezesProjectiles=Time.time==frozenTime&&Vector3.Distance(tracer.position,frozen)<.0001f;
        Check(receipt.pauseFreezesProjectiles,"Pause allowed a police projectile to continue moving.");session.ResumeGame();projectiles.Clear();
        for(int i=0;i<150;i++)Require(projectiles.Fire(new Vector3(100,4f,108),Vector3.up,officer.transform,health),"A pool saturation round failed unexpectedly.");
        receipt.projectilePoolBounded=projectiles.ActiveProjectiles==FacilityPoliceProjectiles.Capacity&&
            projectiles.GetComponentsInChildren<MeshRenderer>(true).Length==FacilityPoliceProjectiles.Capacity;
        Check(receipt.projectilePoolBounded,"Police projectile saturation allocated beyond the fixed 96 visuals.");
        yield return new WaitForSeconds(5.15f);
        receipt.projectilesExpire=projectiles.ActiveProjectiles==0;
        Check(receipt.projectilesExpire,"Police rounds did not expire after their finite lifetime.");
        receipt.shots=projectiles.ShotsFired;receipt.playerImpacts=projectiles.PlayerImpacts;receipt.worldImpacts=projectiles.WorldImpacts;
    }

    private Transform ActiveTracer()
    {
        foreach(MeshRenderer renderer in projectiles.GetComponentsInChildren<MeshRenderer>())if(renderer.gameObject.activeInHierarchy)return renderer.transform;
        return null;
    }
    private void Teleport(Vector3 position){motor.enabled=false;player.transform.position=position;motor.enabled=true;Physics.SyncTransforms();}
    private GameObject Box(string name,Vector3 position,Vector3 size)
    {
        GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(stage.transform,false);
        go.transform.position=position;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=stageMaterial;return go;
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();Texture2D picture=ScreenCapture.CaptureScreenshotAsTexture();Require(picture!=null,"Police capture returned no image.");
        try{File.WriteAllBytes(Path.Combine(output,name+".png"),picture.EncodeToPNG());}finally{Destroy(picture);}
    }
    private static IEnumerator Until(Func<bool> condition,float timeout,string error)
    {
        float limit=Time.time+timeout;while(!condition()&&Time.time<limit)yield return null;Require(condition(),error);
    }
    private void Check(bool condition,string message){if(!condition)receipt.failures.Add(message);}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private void OnLog(string message,string trace,LogType type){if(collecting&&(type==LogType.Error||type==LogType.Assert||type==LogType.Exception))receipt.failures.Add(message+"\n"+trace);}
}
