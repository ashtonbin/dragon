using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

/// <summary>Explicit standalone bank UI and physical door pursuit regression.</summary>
public sealed class FacilityBankSmokeTest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success, title, statusHiddenBeforeAlarm, neutralBrows, angryBrows, smoothHealth, deathText;
        public List<string> crossings = new List<string>();
        public List<string> failures = new List<string>();
    }
    string output;
    Receipt receipt = new Receipt();
    FacilityGameSession session;
    FacilityStoryDirector director;
    FacilityHumanoid actor;
    FacilityKnifeGuard guard;
    FacilityDoor[] doors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-facilityBankTest");
        if (index < 0 || index + 1 >= args.Length) return;
        var runner = new GameObject("Explicit bank checks").AddComponent<FacilityBankSmokeTest>();
        runner.output = args[index+1]; DontDestroyOnLoad(runner.gameObject);
    }
    IEnumerator Start()
    {
        Directory.CreateDirectory(output); Application.targetFrameRate = 120; Application.runInBackground = true; QualitySettings.vSyncCount = 0;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            object current = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output,"bank-receipt.json"),JsonUtility.ToJson(receipt,true));
        Application.Quit(receipt.success ? 0 : 1);
    }
    void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.8f);
        session = FacilityGameSession.Instance;
        foreach (Text label in session.mainMenu.GetComponentsInChildren<Text>()) if (label.text == "rob the bank") receipt.title = true;
        Require(receipt.title,"Title was not renamed.");
        session.StartGame(); session.StartStory();
        float until = Time.realtimeSinceStartup + 12;
        while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance.CurrentMode != FacilityGameMode.Story) && Time.realtimeSinceStartup < until) yield return null;
        session = FacilityGameSession.Instance; director = session.storyDirector;
        Require(director != null,"Story did not load.");
        receipt.statusHiddenBeforeAlarm = !director.AlarmActive && director.heistStatusPanel != null && !director.heistStatusPanel.activeSelf;
        Require(receipt.statusHiddenBeforeAlarm,"Reinforcement box appeared before attacking the receptionist.");
        actor = director.guards[1].actor; guard = director.guards[1];
        FacilityHostileExpression expression = actor.GetComponent<FacilityHostileExpression>();
        yield return null;
        receipt.neutralBrows = expression != null && !expression.Angry;
        Require(receipt.neutralBrows,"An unprovoked employee already has angry brows.");
        foreach (FacilityKnifeGuard other in director.guards) if (other != guard) other.gameObject.SetActive(false);
        session.player.enabled = false; guard.Activate();
        yield return new WaitForSecondsRealtime(.3f);
        receipt.angryBrows = expression.Angry && Quaternion.Angle(expression.leftBrow.localRotation,Quaternion.identity) > 20;
        Require(receipt.angryBrows,"Provoked eyebrows did not lower.");
        doors = FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None);
        Vector3[] from = { new Vector3(0,0,-6.8f), new Vector3(4.5f,0,5.4f),new Vector3(-4,0,13.2f),new Vector3(-1.5f,0,10.4f) };
        Vector3[] to = { new Vector3(0,0,-1.2f), new Vector3(4.5f,0,10.6f),new Vector3(-4,0,18.7f),new Vector3(3.2f,0,10.4f) };
        for (int i=0;i<from.Length;i++)
        {
            yield return Cross("Guard route "+i+" outward",from[i],to[i]);
            yield return Cross("Guard route "+i+" inward",to[i],from[i]);
        }
        yield return Cross("Full bent security pursuit",new Vector3(-7.5f,0,22.4f),new Vector3(3,0,6));
        guard.enabled = false; actor.enabled = false;
        actor.gameObject.SetActive(false);
        yield return PoliceCrossing();
        session.health.ResetHealth(); yield return null;
        float full = session.healthFill.fillAmount;
        Require(session.health.TryTakeHit(null),"Could not damage player.");
        yield return new WaitForSecondsRealtime(.08f);
        float middle = session.healthFill.fillAmount;
        yield return new WaitForSecondsRealtime(.3f);
        receipt.smoothHealth = full > .99f && middle < .99f && middle > .667f && Mathf.Abs(session.healthFill.fillAmount-2f/3f)<.01f;
        Require(receipt.smoothHealth,"Health did not smoothly drain to two thirds.");
        yield return new WaitForSecondsRealtime(1f); session.health.TryTakeHit(null);
        yield return new WaitForSecondsRealtime(1f); session.health.TryTakeHit(null);
        yield return null;
        foreach (Text text in session.deathMenu.GetComponentsInChildren<Text>())
            if (text.text == "you arent finished yet" && text.color.r > .8f && text.color.g < .3f) receipt.deathText = true;
        Require(receipt.deathText && session.CurrentState == FacilityGameState.Dead,"Death text/state is incorrect.");
    }
    IEnumerator Cross(string name,Vector3 from,Vector3 to)
    {
        actor.enabled = false; guard.enabled = false;
        PlaceActor(new Vector3(-13,0,-22)); PlacePlayer(new Vector3(-10,0,-22));
        foreach (FacilityDoor door in doors) if (!door.IsLocked && door.WantsOpen) door.TryToggle();
        yield return new WaitForSecondsRealtime(1f);
        PlaceActor(from); PlacePlayer(to); session.health.ResetHealth();
        actor.enabled = true; guard.enabled = true;
        float until = Time.realtimeSinceStartup + (Flat(from,to)>10 ? 35f : 14f);
        while (Flat(actor.transform.position,to)>1.45f && Time.realtimeSinceStartup<until) yield return null;
        if (Flat(actor.transform.position,to)>1.45f)
        {
            string detail = "";
            foreach (FacilityDoor door in doors)
            {
                if (Vector3.Distance(door.transform.position,actor.transform.position)>8) continue;
                NavMeshObstacle obstacle=door.leaf.GetComponent<NavMeshObstacle>();
                detail += " | "+door.name+" open="+door.IsOpen+" moving="+door.IsMoving+" leaf="+door.leaf.position+" yaw="+door.leaf.eulerAngles+" obstacle="+(obstacle!=null?obstacle.size.ToString():"none");
            }
            var path=new NavMeshPath();
            NavMesh.SamplePosition(actor.transform.position,out NavMeshHit start,1.2f,NavMesh.AllAreas);
            NavMesh.SamplePosition(to,out NavMeshHit end,1.2f,NavMesh.AllAreas);
            NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path);
            foreach(Vector3 corner in path.corners) detail += " corner="+corner;
            Require(false,name+" stuck at "+actor.transform.position+"; navigation="+guard.NavigationStatus+" target="+end.position+detail);
        }
        receipt.crossings.Add(name+" reached "+actor.transform.position);
    }
    void PlaceActor(Vector3 at)
    { actor.motor.enabled=false; actor.transform.position=at+Vector3.up*.04f; actor.motor.enabled=true; Physics.SyncTransforms(); }
    void PlacePlayer(Vector3 at)
    { var motor=session.player.GetComponent<CharacterController>(); motor.enabled=false; session.player.transform.position=at+Vector3.up*.05f; motor.enabled=true; Physics.SyncTransforms(); }
    static float Flat(Vector3 a,Vector3 b) { a.y=b.y=0; return Vector3.Distance(a,b); }

    IEnumerator PoliceCrossing()
    {
        PlacePlayer(new Vector3(-10,0,-22));
        foreach (FacilityDoor door in doors) if (!door.IsLocked && door.WantsOpen) door.TryToggle();
        yield return new WaitForSecondsRealtime(1f);
        GameObject copy = Instantiate(director.policeWaves.policePrefab);
        copy.SetActive(false); copy.transform.position = new Vector3(0,.04f,-6.8f);
        FacilityHumanoid cop = copy.GetComponent<FacilityHumanoid>(); cop.player = session.player.transform;
        FacilityPoliceOfficer officer = copy.GetComponent<FacilityPoliceOfficer>();
        officer.target = session.health; officer.holdDistance = 1.4f;
        // Pursuit uses the actual police motor/path; omit only the independent projectile system during this route check.
        officer.projectileSystem = null;
        PlacePlayer(new Vector3(0,0,-1.2f)); copy.SetActive(true);
        float until = Time.realtimeSinceStartup+14;
        while (Flat(cop.transform.position,session.player.transform.position)>1.45f && Time.realtimeSinceStartup<until) yield return null;
        Require(Flat(cop.transform.position,session.player.transform.position)<=1.45f,"Police failed to pass the closed entrance: "+cop.transform.position+" / "+officer.NavigationStatus);
        receipt.crossings.Add("Armed police crossed closed entrance at "+cop.transform.position);
        copy.SetActive(false); Destroy(copy);
    }
}
