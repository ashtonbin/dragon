using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Opt-in standalone regression: -facilityGuardTest "output directory".</summary>
public sealed class FacilityGuardSmokeTest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success, receptionistAttackAlertsGuards, transferWorkerAttacks, vaultKeeperAttacks, lateBoundTargetAttacks, repeatedAlertsAllowAttack,
            solidCoverBlocksAttack, pauseFreezesAttack, lostKnifePreventsDamage;
        public string coverage = "Natural movement and damaging attacks from the two saved pre-vault guards in the actual counting room; a late-bound target; solid cover, pause and missing-knife damage rejection. No scene or asset is saved.";
        public List<string> encounters = new List<string>();
        public List<string> failures = new List<string>();
    }

    readonly Receipt receipt = new Receipt();
    FacilityGameSession session;
    FacilityStoryDirector director;
    FacilityKnifeGuard guard;
    FacilityHumanoid actor;
    CharacterController playerMotor;
    string output;
    bool collecting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-facilityGuardTest");
        if (index < 0) return;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityGuardTest requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Guard regression - explicit CLI only").AddComponent<FacilityGuardSmokeTest>();
        runner.output = args[index + 1]; DontDestroyOnLoad(runner.gameObject);
    }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        Application.runInBackground = true; Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0;
        collecting = true; Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object current = null;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        collecting = false; Application.logMessageReceived -= OnLog; Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "guard-receipt.json"), JsonUtility.ToJson(receipt, true));
        Debug.Log("Facility guard diagnostics " + (receipt.success ? "passed: " : "failed: ") + output);
        Application.Quit(receipt.success ? 0 : 1);
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.5f);
        session = FacilityGameSession.Instance; Require(session != null, "No startup game session.");
        session.StartStory();
        float until = Time.realtimeSinceStartup + 12f;
        while ((FacilityGameSession.Instance == session || FacilityGameSession.Instance == null ||
            FacilityGameSession.Instance.CurrentMode != FacilityGameMode.Story) && Time.realtimeSinceStartup < until) yield return null;
        session = FacilityGameSession.Instance; director = session != null ? session.storyDirector : null;
        Require(director != null && director.guards.Length >= 5, "The authored bank guards did not load.");
        Require(director.receptionist != null && director.receptionist.chest != null, "The saved receptionist is missing.");
        director.receptionist.ReceiveMelee(947117, director.receptionist.AimPoint, Vector3.forward, director.receptionist.chest.GetComponent<Collider>());
        yield return null;
        receipt.receptionistAttackAlertsGuards = director.AlarmActive;
        foreach (FacilityKnifeGuard item in director.guards)
            receipt.receptionistAttackAlertsGuards &= item.Alerted && item.actor.IsHostile;
        Check(receipt.receptionistAttackAlertsGuards, "The first valid receptionist attack did not alert every authored bank guard.");
        director.policeWaves.StopAssault(); session.player.enabled = false;
        playerMotor = session.player.GetComponent<CharacterController>();
        foreach (FacilityKnifeGuard item in director.guards) item.gameObject.SetActive(false);
        for (int i = 3; i <= 4; i++)
        {
            guard = director.guards[i]; actor = guard.actor;
            PlacePlayer(new Vector3(0f, .05f, 26.8f)); session.health.ResetHealth();
            guard.gameObject.SetActive(true);
            Vector3 start = actor.transform.position;
            until = Time.realtimeSinceStartup + 10f;
            while (session.health.HitsTaken == 0 && Time.realtimeSinceStartup < until) yield return null;
            bool attacked = session.health.HitsTaken == 1 && session.health.LastAttacker == guard.transform && guard.SuccessfulAttacks == 1;
            if (i == 3) receipt.transferWorkerAttacks = attacked; else receipt.vaultKeeperAttacks = attacked;
            receipt.encounters.Add(actor.name + ": from " + start + " to " + actor.transform.position + "; attacks=" +
                guard.SuccessfulAttacks + "; path=" + guard.NavigationStatus + "; corners=" + guard.CornerCount +
                "; clearance=" + actor.NavigationClearanceStatus + "; blocker=" +
                (actor.NavigationBlocker != null ? actor.NavigationBlocker.name + " at " + actor.NavigationBlocker.bounds.center : "none") +
                "; blockerNormal=" + actor.NavigationBlockingNormal);
            Check(attacked, "The saved " + actor.name + " never naturally reached and attacked the player: " + guard.NavigationStatus + " at " + actor.transform.position);
            guard.gameObject.SetActive(false);
        }

        GameObject clone = Instantiate(director.guards[4].gameObject); clone.name = "Late-bound diagnostic guard";
        clone.transform.position = new Vector3(0f, .04f, 27.4f);
        guard = clone.GetComponent<FacilityKnifeGuard>(); actor = guard.actor;
        actor.player = null; guard.targetHealth = null;
        PlacePlayer(new Vector3(0f, .05f, 23.4f)); session.health.ResetHealth();
        clone.SetActive(true); yield return null;
        actor.player = session.player.transform; guard.Activate(); Physics.SyncTransforms();
        until = Time.realtimeSinceStartup + 5f;
        while (session.health.HitsTaken == 0 && Time.realtimeSinceStartup < until)
        { guard.Activate(); yield return null; }
        receipt.lateBoundTargetAttacks = session.health.HitsTaken == 1 && session.health.LastAttacker == guard.transform;
        receipt.repeatedAlertsAllowAttack = receipt.lateBoundTargetAttacks;
        Check(receipt.lateBoundTargetAttacks, "An active guard with a late-bound player could not approach and attack.");
        Check(receipt.repeatedAlertsAllowAttack, "Repeated alerts prevented a ready knife attack.");

        actor.enabled = false; guard.enabled = false;
        actor.motor.enabled = false; actor.transform.position = new Vector3(0f, .04f, 24.45f); actor.motor.enabled = true;
        PlacePlayer(new Vector3(0f, .05f, 23.4f)); session.health.ResetHealth();
        GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Solid guard diagnostic cover";
        cover.transform.position = new Vector3(0f, 1.1f, 23.92f); cover.transform.localScale = new Vector3(3f, 2.2f, .04f);
        Physics.SyncTransforms(); guard.enabled = true; int attacks = guard.SuccessfulAttacks;
        yield return new WaitForSeconds(1.6f);
        receipt.solidCoverBlocksAttack = session.health.HitsTaken == 0 && guard.SuccessfulAttacks == attacks;
        Check(receipt.solidCoverBlocksAttack, "A knife attack damaged the player through solid cover."); cover.SetActive(false);

        session.PauseGame(); yield return null; Vector3 frozen = guard.transform.position; float frozenTime = Time.time;
        yield return new WaitForSecondsRealtime(.3f);
        receipt.pauseFreezesAttack = Time.time == frozenTime && session.health.HitsTaken == 0 && guard.SuccessfulAttacks == attacks &&
            Vector3.Distance(frozen, guard.transform.position) < .001f;
        Check(receipt.pauseFreezesAttack, "Pause allowed guard movement or damage."); session.ResumeGame();
        guard.knifeProp.gameObject.SetActive(false);
        yield return new WaitForSeconds(1.6f);
        receipt.lostKnifePreventsDamage = session.health.HitsTaken == 0 && guard.SuccessfulAttacks == attacks;
        Check(receipt.lostKnifePreventsDamage, "A missing knife continued to damage the player.");
        clone.SetActive(false); Destroy(clone); Destroy(cover);
    }

    void PlacePlayer(Vector3 position)
    { playerMotor.enabled = false; session.player.transform.position = position; playerMotor.enabled = true; Physics.SyncTransforms(); }
    void Check(bool condition, string message) { if (!condition) receipt.failures.Add(message); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    void OnLog(string message, string trace, LogType type)
    { if (collecting && (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)) receipt.failures.Add(message + "\n" + trace); }
}
