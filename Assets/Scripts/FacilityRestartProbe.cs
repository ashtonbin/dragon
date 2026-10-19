using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in graphics/scene teardown regression: -facilityRestartProbe "output directory".</summary>
/// <remarks>Direct diagnostic setup creates saved police prefabs and starts the drill; it never changes production mission timing.</remarks>
public sealed class FacilityRestartProbe : MonoBehaviour
{
    [Serializable] private sealed class Reload
    {
        public int pass, spawnedPolice;
        public string scenario;
        public bool completed, freshMission;
    }

    [Serializable] private sealed class Receipt
    {
        public bool success, finished;
        public string utc, unity, gpu, graphicsApi, checkpoint;
        public string coverage = "Three ordinary Story restarts interleaved with three stressed Story restarts. Stress setup directly instantiates eight saved police prefabs, holds their combat brains, physically opens ordinary doors and starts the real drill/audio. This isolates scene and graphics teardown without waiting for waves or changing production mission or timer rules. The full Story and police diagnostics separately cover progression and combat.";
        public List<Reload> reloads = new List<Reload>();
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private readonly Collider[] overlaps = new Collider[96];
    private string output;
    private FacilityGameSession session;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-facilityRestartProbe");
        if (flag < 0) return;
        if (flag + 1 >= args.Length || args[flag + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilityRestartProbe requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Facility repeated restart diagnostics (explicit CLI only)").AddComponent<FacilityRestartProbe>();
        runner.output = Path.GetFullPath(args[flag + 1]);
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        Directory.CreateDirectory(output);
        receipt.utc = DateTime.UtcNow.ToString("O");
        receipt.unity = Application.unityVersion;
        receipt.gpu = SystemInfo.graphicsDeviceName;
        receipt.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
        Application.logMessageReceived += OnLog;
        var routines = new Stack<IEnumerator>();
        routines.Push(Run());
        while (routines.Count > 0)
        {
            IEnumerator routine = routines.Peek();
            bool more;
            object current = null;
            try { more = routine.MoveNext(); if (more) current = routine.Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { routines.Pop(); continue; }
            if (current is IEnumerator nested) routines.Push(nested);
            else yield return current;
        }
        Application.logMessageReceived -= OnLog;
        receipt.finished = true;
        receipt.success = receipt.failures.Count == 0 && receipt.reloads.Count == 6;
        Checkpoint(receipt.success ? "complete" : "failed");
        Application.Quit(receipt.success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(.4f);
        session = FindFirstObjectByType<FacilityGameSession>();
        Require(session != null, "The startup scene has no game session.");
        if (session.CurrentMode != FacilityGameMode.Story)
        {
            FacilityGameSession old = session;
            session.StartStory();
            if (old.storyDirector == null) yield return WaitForReload(old);
            else yield return new WaitForSecondsRealtime(.2f);
        }
        session = FindFirstObjectByType<FacilityGameSession>();
        Require(session != null && session.storyDirector != null && session.CurrentMode == FacilityGameMode.Story,
            "The diagnostic could not enter the authored Story scene.");

        for (int pass = 1; pass <= 3; pass++)
        {
            yield return Restart(pass, false);
            yield return Restart(pass, true);
        }
    }

    private IEnumerator Restart(int pass, bool stressed)
    {
        FacilityStoryDirector director = session.storyDirector;
        var reload = new Reload { pass = pass, scenario = stressed ? "eight police, moving doors and running drill" : "ordinary fresh Story" };
        receipt.reloads.Add(reload);
        if (stressed)
        {
            FacilityPoliceWaves waves = director.policeWaves;
            Require(waves != null && waves.policePrefab != null && waves.spawnPoints != null && waves.spawnPoints.Length > 0,
                "Stress setup requires the saved police prefab and navigation spawn markers.");
            Transform root = new GameObject("Restart probe police setup (diagnostic only)").transform;
            root.SetParent(waves.transform, false);
            for (int index = 0; index < 8; index++)
            {
                GameObject instance = SpawnPolice(waves, root, index);
                Require(instance != null, "Could not find a clear diagnostic police position for officer " + index + ".");
                reload.spawnedPolice++;
            }
            Require(director.vaultDrill != null && director.vaultDrill.BeginDrilling(), "The stress setup could not start the real drill/audio.");
            // Keep pooled graphics, active UI typing and the positional drill loop alive across a rendered frame.
            yield return new WaitForSecondsRealtime(.9f);
            foreach (FacilityDoor door in FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None))
                if (!door.IsLocked) door.ForceOpen();
            yield return new WaitForSecondsRealtime(.1f);
        }
        else yield return new WaitForSecondsRealtime(.25f);

        session.PauseGame();
        yield return null;
        Checkpoint("before restart " + pass + " / " + reload.scenario);
        FacilityGameSession old = session;
        session.RestartGame();
        yield return WaitForReload(old);
        FacilityStoryDirector fresh = session.storyDirector;
        reload.freshMission = fresh != null && fresh.Stage == FacilityStoryStage.RetrieveDrill && !fresh.HasDrill && !fresh.HasCash &&
            !fresh.AlarmActive && fresh.vaultDrill != null && fresh.vaultDrill.ElapsedSeconds == 0f && !session.inventory.HasPistol &&
            FindObjectsByType<FacilityPoliceOfficer>(FindObjectsSortMode.None).Length == 0;
        Require(reload.freshMission, "Restart retained mission, police, drill or weapon state.");
        reload.completed = true;
        Checkpoint("after restart " + pass + " / " + reload.scenario);
        yield return new WaitForEndOfFrame();
    }

    private GameObject SpawnPolice(FacilityPoliceWaves waves, Transform parent, int officerIndex)
    {
        for (int attempt = 0; attempt < waves.spawnPoints.Length * 6; attempt++)
        {
            Transform marker = waves.spawnPoints[(officerIndex + attempt) % waves.spawnPoints.Length];
            if (marker == null) continue;
            int ring = attempt / waves.spawnPoints.Length;
            Vector3 offset = ring == 0 ? Vector3.zero : new Vector3((ring % 2 == 0 ? -1f : 1f) * .75f, 0f, ring * .45f);
            if (!NavMesh.SamplePosition(marker.position + offset, out NavMeshHit hit, .9f, NavMesh.AllAreas)) continue;
            Vector3 feet = hit.position + Vector3.up * .035f;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * .3f, feet + Vector3.up * 1.48f,
                .28f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlapCount > 0) continue;
            GameObject instance = Instantiate(waves.policePrefab, parent, false);
            instance.SetActive(false);
            instance.name = "Restart probe police " + officerIndex;
            instance.transform.SetPositionAndRotation(feet, marker.rotation);
            FacilityHumanoid actor = instance.GetComponent<FacilityHumanoid>();
            FacilityPoliceOfficer officer = instance.GetComponent<FacilityPoliceOfficer>();
            if (actor == null || officer == null) { Destroy(instance); return null; }
            actor.player = session.player.transform;
            actor.IsHostile = true;
            officer.actor = actor;
            officer.projectileSystem = waves.projectiles;
            officer.target = session.health;
            foreach (SubtleEnemyHealthBar bar in instance.GetComponentsInChildren<SubtleEnemyHealthBar>(true)) bar.player = session.player.transform;
            if (actor.gore != null)
            {
                Collider[] ignored = new Collider[actor.bodyColliders.Length + 2];
                Array.Copy(actor.bodyColliders, ignored, actor.bodyColliders.Length);
                ignored[ignored.Length - 2] = actor.motor;
                ignored[ignored.Length - 1] = session.player.GetComponent<CharacterController>();
                actor.gore.ownerColliders = ignored;
            }
            instance.SetActive(true);
            officer.enabled = false;
            actor.enabled = false;
            Physics.SyncTransforms();
            return instance;
        }
        return null;
    }

    private IEnumerator WaitForReload(FacilityGameSession previous)
    {
        float timeout = Time.realtimeSinceStartup + 12f;
        do { yield return null; session = FindFirstObjectByType<FacilityGameSession>(); }
        while ((session == null || session == previous) && Time.realtimeSinceStartup < timeout);
        Require(session != null && session != previous, "A fresh game session was not created within twelve seconds.");
        yield return new WaitForSecondsRealtime(.25f);
    }

    private void Checkpoint(string stage)
    {
        receipt.checkpoint = stage;
        File.WriteAllText(Path.Combine(output, "restart-receipt.json"), JsonUtility.ToJson(receipt, true));
        Debug.Log("Facility restart probe: " + stage);
    }

    private void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) receipt.failures.Add(message + "\n" + trace);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
