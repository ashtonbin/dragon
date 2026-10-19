using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Explicit standalone regression check: -facilityArsenalTest "output directory".</summary>
/// <remarks>Creates a temporary firing wall and never saves or changes authored scene assets.</remarks>
public sealed class FacilityArsenalSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class Receipt
    {
        public bool success, sandboxLoadout, onlySelectedModelVisible, onlySelectedWeaponFires,
            midRackSwitchPreservesHome, rackFinishesAtHome, pistolHitsWall, pistolBarrelObstructionBlocks,
            allScarAttachmentsValid, pauseFreezesAndBlocksArsenal;
        public int switchCycles, shotgunShots, pistolShots, fistSwings, wallScars;
        public float rackTravelAtSwitch, worstPumpRestError, finalPumpRestError;
        public string utc, unity, gpu;
        public string coverage = "Actual authored Sandbox arsenal, one real shotgun discharge against a temporary static wall, 20 repeated weapon changes while its rack is extended, pump-zero/reset regression, actual pistol surface impact and off-axis near-muzzle obstruction, selected-only fire gates, fists swing, pause freezing and blocking. Test objects are runtime-only; no scene or asset is saved.";
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private string output;
    private bool collectingErrors;
    private FacilityGameSession session;
    private FacilityWeaponInventory inventory;
    private RetroFPSController player;
    private Camera eye;
    private GameObject stage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-facilityArsenalTest");
        if (flag < 0) return;
        if (flag + 1 >= args.Length || args[flag+1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityArsenalTest requires an output directory."); Application.Quit(2); return; }
        FacilityArsenalSmokeTest runner = new GameObject("Arsenal regression diagnostics (explicit CLI only)").AddComponent<FacilityArsenalSmokeTest>();
        runner.output = args[flag+1];
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        collectingErrors = true; Application.logMessageReceived += OnLog;
        Stack<IEnumerator> stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek(); bool more; object next = null;
            try { more = current.MoveNext(); if (more) next = current.Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        collectingErrors = false; Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        try { File.WriteAllText(Path.Combine(output,"arsenal-receipt.json"), JsonUtility.ToJson(receipt,true)); }
        catch (Exception exception) { Debug.LogException(exception); Application.Quit(2); yield break; }
        Debug.Log("Facility arsenal regression " + (receipt.success ? "passed: " : "failed: ") + output);
        Application.Quit(receipt.success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(.5f);
        session = FacilityGameSession.Instance;
        Require(session != null, "The startup scene has no game session.");
        session.StartSandbox();
        yield return new WaitForSecondsRealtime(.5f);
        session = FacilityGameSession.Instance;
        Require(session != null && session.CurrentMode == FacilityGameMode.Sandbox, "Sandbox did not begin.");
        player = session.player; inventory = session.inventory;
        Require(player != null && inventory != null && inventory.shotgun != null && inventory.pistol != null && inventory.fists != null, "Authored arsenal references are missing.");
        eye = player.GetComponentInChildren<Camera>(); Require(eye != null, "The player has no eye camera.");
        receipt.utc=DateTime.UtcNow.ToString("O");receipt.unity=Application.unityVersion;receipt.gpu=SystemInfo.graphicsDeviceName;
        receipt.sandboxLoadout = inventory.HasPistol && inventory.HasShotgun && inventory.SelectedWeapon == FacilityWeaponKind.Shotgun;
        Check(receipt.sandboxLoadout, "Sandbox did not give both guns and select the shotgun.");

        // Keep live NPCs and authored geometry out of this deterministic near-muzzle regression.
        stage = new GameObject("Arsenal diagnostic stage (runtime only)");
        Box("Firing floor",new Vector3(100f,-.1f,100f),new Vector3(10f,.2f,12f));
        GameObject wall=Box("Firing wall",new Vector3(100f,1.8f,104.5f),new Vector3(5f,4f,.3f));
        CharacterController motor=player.GetComponent<CharacterController>();
        if(motor!=null)motor.enabled=false;
        player.transform.SetPositionAndRotation(new Vector3(100f,.05f,100f),Quaternion.identity);
        player.enabled=false;ResetAim();
        if(motor!=null)motor.enabled=true;
        Physics.SyncTransforms();
        yield return null;
        FacilityShotgun shotgun=inventory.shotgun;
        FacilityPistol pistol=inventory.pistol;
        FacilityFists fists=inventory.fists;
        Vector3 home=shotgun.pump.localPosition;
        int beforeShots=shotgun.ShotsFired,beforePistol=pistol.ShotsFired,beforeFists=fists.AttacksMade;
        bool gates=!pistol.TryFire()&&!fists.TryPunch();
        int beforeMarks=shotgun.surfaceImpacts.TotalImpacts;
        Require(shotgun.TryFire(),"The selected shotgun could not fire.");
        yield return new WaitForSeconds(.43f);
        receipt.rackTravelAtSwitch=Vector3.Distance(shotgun.pump.localPosition,home);
        Require(receipt.rackTravelAtSwitch>.07f,"The regression did not reach the extended part of the real rack animation.");
        bool visibility=true;
        for(int i=0;i<20;i++)
        {
            inventory.Equip(FacilityWeaponKind.Pistol);visibility&=VisibleOnly(FacilityWeaponKind.Pistol);
            gates&=!shotgun.TryFire()&&!fists.TryPunch();
            inventory.Equip(FacilityWeaponKind.Fists);visibility&=VisibleOnly(FacilityWeaponKind.Fists);
            gates&=!shotgun.TryFire()&&!pistol.TryFire();
            inventory.Equip(FacilityWeaponKind.Shotgun);visibility&=VisibleOnly(FacilityWeaponKind.Shotgun);
            gates&=!pistol.TryFire()&&!fists.TryPunch();
            // This is the same zero-travel API used by the original pump animation. It must
            // still resolve to the original home after switching back from an extended rack.
            inventory.viewmodel.SetPumpTravel(0f);
            receipt.worstPumpRestError=Mathf.Max(receipt.worstPumpRestError,Vector3.Distance(shotgun.pump.localPosition,home));
            inventory.viewmodel.SetPumpTravel(1f);
            receipt.switchCycles++;
        }
        receipt.onlySelectedModelVisible=visibility;
        receipt.midRackSwitchPreservesHome=receipt.worstPumpRestError<.0001f;
        Check(visibility,"Switching exposed more than the selected authored model.");
        Check(receipt.midRackSwitchPreservesHome,"Switching during a rack changed its cached rest position.");
        yield return new WaitForSeconds(.50f);
        receipt.finalPumpRestError=Vector3.Distance(shotgun.pump.localPosition,home);
        receipt.rackFinishesAtHome=receipt.finalPumpRestError<.0001f;
        Check(receipt.rackFinishesAtHome,"The shotgun pump did not return to its original authored home.");
        Check(shotgun.surfaceImpacts.TotalImpacts-beforeMarks==12,"The shotgun discharge did not leave twelve distinct pellet impacts on the diagnostic wall.");
        yield return Capture("01_shotgun_after_switching");

        inventory.Equip(FacilityWeaponKind.Pistol);ResetAim();
        int pistolMarks=shotgun.surfaceImpacts.TotalImpacts;
        Require(pistol.TryFire(),"The selected pistol could not fire.");
        receipt.pistolHitsWall=shotgun.surfaceImpacts.TotalImpacts==pistolMarks+1;
        Check(receipt.pistolHitsWall,"The actual pistol ray did not leave one static-wall scar.");
        gates&=!shotgun.TryFire()&&!fists.TryPunch();
        yield return Capture("02_pistol_firing");
        yield return new WaitForSeconds(pistol.fireInterval+.08f);
        ResetAim();
        Vector3 offset=pistol.muzzle.position-eye.transform.position;
        GameObject blocker=Box("Near-muzzle off-axis blocker",eye.transform.position+offset*.62f,Vector3.one*.075f);
        Physics.SyncTransforms();
        Require(Physics.Raycast(eye.transform.position,eye.transform.forward,out RaycastHit aimed,10f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&aimed.collider==wall.GetComponent<Collider>(),
            "The obstruction case must leave the camera's centre aim on the distant wall.");
        Require(pistol.TryFire(),"The pistol near-muzzle obstruction case did not fire.");
        Collider blockerCollider=blocker.GetComponent<Collider>();
        bool blockerScar=false;
        foreach(MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            MeshFilter filter=renderer.GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh!=shotgun.surfaceImpacts.holeMesh||!renderer.gameObject.activeInHierarchy)continue;
            Vector3 point=renderer.transform.position;
            if(Vector3.Distance(point,blockerCollider.ClosestPoint(point))<.006f)blockerScar=true;
        }
        receipt.pistolBarrelObstructionBlocks=blockerScar;
        Check(blockerScar,"A pistol barrel obstructed off-axis fired through its near blocker instead of marking it.");
        receipt.allScarAttachmentsValid=shotgun.surfaceImpacts.ValidateAttachment(out string reason);
        Check(receipt.allScarAttachmentsValid,"An actual bullet scar detached from its surface: "+reason);

        inventory.Equip(FacilityWeaponKind.Fists);visibility&=VisibleOnly(FacilityWeaponKind.Fists);
        gates&=!pistol.TryFire()&&!shotgun.TryFire();
        Require(fists.TryPunch(),"The selected fists could not start a punch.");
        yield return new WaitForSeconds(.13f);
        yield return Capture("03_fists_contact_frame");
        receipt.onlySelectedWeaponFires=gates;
        Check(gates,"An unequipped weapon accepted a fire or melee command.");
        session.PauseGame();
        yield return null;
        Vector3 frozenHand=fists.leftHand.localPosition;
        float frozenTime=Time.time;
        yield return new WaitForSecondsRealtime(.12f);
        receipt.pauseFreezesAndBlocksArsenal=Time.time==frozenTime&&Vector3.Distance(frozenHand,fists.leftHand.localPosition)<.0001f&&
            !shotgun.TryFire()&&!pistol.TryFire()&&!fists.TryPunch();
        Check(receipt.pauseFreezesAndBlocksArsenal,"Pause did not freeze the punch and block every weapon.");
        receipt.shotgunShots=shotgun.ShotsFired-beforeShots;receipt.pistolShots=pistol.ShotsFired-beforePistol;
        receipt.fistSwings=fists.AttacksMade-beforeFists;receipt.wallScars=shotgun.surfaceImpacts.TotalImpacts-beforeMarks;
        Check(receipt.shotgunShots==1&&receipt.pistolShots==2&&receipt.fistSwings==1,"Fire counters include an unexpected unequipped shot or punch.");
    }

    private bool VisibleOnly(FacilityWeaponKind selected)=>
        inventory.shotgunModel.gameObject.activeInHierarchy==(selected==FacilityWeaponKind.Shotgun)&&
        inventory.pistolModel.gameObject.activeInHierarchy==(selected==FacilityWeaponKind.Pistol)&&
        inventory.fistsModel.gameObject.activeInHierarchy==(selected==FacilityWeaponKind.Fists);
    private void ResetAim(){player.ResetRecoil();eye.transform.localRotation=Quaternion.identity;}
    private GameObject Box(string name,Vector3 position,Vector3 scale)
    {
        GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(stage.transform,false);
        go.transform.SetPositionAndRotation(position,Quaternion.identity);go.transform.localScale=scale;return go;
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();Texture2D picture=ScreenCapture.CaptureScreenshotAsTexture();
        Require(picture!=null,"Arsenal screenshot returned no image.");
        try{File.WriteAllBytes(Path.Combine(output,name+".png"),picture.EncodeToPNG());}finally{Destroy(picture);}
    }
    private void Check(bool condition,string message){if(!condition)receipt.failures.Add(message);}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private void OnLog(string message,string trace,LogType type){if(collectingErrors&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))receipt.failures.Add(message+"\n"+trace);}
}
