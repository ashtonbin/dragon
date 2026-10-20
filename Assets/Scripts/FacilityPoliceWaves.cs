using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Alarm-driven reinforcements. No police spawn before the alarm or after extraction.</summary>
[DisallowMultipleComponent]
public sealed class FacilityPoliceWaves : MonoBehaviour
{
    public GameObject policePrefab;
    public GameObject elitePrefab;
    public FacilityPoliceProjectiles projectiles;
    public Transform[] spawnPoints;
    public RetroFPSController player;
    [Min(1f)] public float interval = 15f;
    [Range(1, 12)] public int maxActivePolice = 8;
    [Min(0f)] public float eliteDelay = 5f;
    [Range(1, 8)] public int eliteTeamSize = 4;
    public bool EliteAssaultActive { get; private set; }
    public bool EliteTeamArrived => EliteSpawnedCount > 0;
    public bool EliteDeploymentComplete => eliteRequested && EliteSpawnedCount >= eliteTeamSize;
    public int EliteSpawnedCount { get; private set; }
    public int EliteKilledCount
    {
        get { int count = recordedEliteKills; foreach (Entry entry in actors)
            if (entry.elite && !entry.killRecorded && entry.actor != null && entry.actor.IsDead) count++; return count; }
    }
    public bool EliteTeamDefeated => EliteDeploymentComplete && EliteKilledCount >= eliteTeamSize;
    public int ActiveEliteCount
    {
        get { int count = 0; foreach (Entry entry in actors) if (entry.elite && entry.actor != null && !entry.actor.IsDead) count++; return count; }
    }
    public float SecondsUntilEliteArrival => EliteAssaultActive && !EliteTeamArrived ? Mathf.Max(0f, eliteArrivesAt - Time.time) : 0f;
    public bool AssaultActive { get; private set; }
    public int WaveNumber { get; private set; }
    public int TotalPoliceSpawned { get; private set; }
    public float AssaultStartedAt { get; private set; }
    public float LastWaveAt { get; private set; }
    public int ActivePoliceCount
    {
        get { int count = 0; foreach (Entry entry in actors) if (!entry.elite && entry.actor != null && !entry.actor.IsDead) count++; return count; }
    }
    public float SecondsUntilNextWave => AssaultActive ? Mathf.Max(0f, nextWaveAt - Time.time) : 0f;
    private sealed class Entry { public FacilityHumanoid actor; public float diedAt = -1f; public bool elite, killRecorded; }
    private readonly List<Entry> actors = new List<Entry>(16);
    private readonly Collider[] overlaps = new Collider[96];
    private Transform spawnedRoot;
    private float nextWaveAt;
    private int spawnCursor;
    private bool eliteRequested;
    private int recordedEliteKills;
    private float eliteArrivesAt, nextEliteAttempt;

    public void BeginAssault()
    {
        if (AssaultActive || eliteRequested || MissionEnded()) return;
        AssaultActive = true;
        WaveNumber = 0;
        AssaultStartedAt = Time.time;
        nextWaveAt = Time.time + interval;
        EnsureSpawnRoot();
    }

    /// <summary>One final squad replaces the endless regular wave schedule. Failed placements retry without increasing the team.</summary>
    public bool BeginEliteAssault()
    {
        if (eliteRequested || elitePrefab == null || player == null || MissionEnded()) return false;
        var session = FacilityGameSession.Instance;
        if (session != null && session.storyDirector != null && (!session.storyDirector.HasCash || !session.storyDirector.HasGold)) return false;
        eliteRequested = true; EliteAssaultActive = true; AssaultActive = false;
        eliteArrivesAt = Time.time + eliteDelay; nextEliteAttempt = eliteArrivesAt;
        EnsureSpawnRoot();
        return true;
    }

    void EnsureSpawnRoot()
    {
        if (spawnedRoot != null) return;
        spawnedRoot = new GameObject("Police reinforcements - bounded actors and corpses").transform;
        spawnedRoot.SetParent(transform, false);
    }

    bool MissionEnded()
    {
        var health = player != null ? player.GetComponent<FacilityPlayerHealth>() : null;
        if (health != null && health.IsDead) return true;
        var session = FacilityGameSession.Instance;
        return session != null && (session.CurrentState == FacilityGameState.Dead || session.CurrentState == FacilityGameState.LevelComplete);
    }

    public void StopAssault()
    {
        AssaultActive = false;
        EliteAssaultActive = false;
        if (projectiles != null) projectiles.Clear();
    }

    private void Update()
    {
        if (MissionEnded()) { StopAssault(); return; }
        if (Time.timeScale <= 0f || FacilityGameSession.SimulationPaused) return;
        CullCorpses();
        if (EliteAssaultActive)
        {
            if (!EliteDeploymentComplete && Time.time >= nextEliteAttempt)
            {
                nextEliteAttempt = Time.time + .4f;
                while (EliteSpawnedCount < eliteTeamSize) if (!SpawnPolice(true)) break;
            }
            return;
        }
        if (!AssaultActive || player == null || policePrefab == null) return;
        FacilityPlayerHealth health = player.GetComponent<FacilityPlayerHealth>();
        if (health != null && health.IsDead) { StopAssault(); return; }
        if (Time.time < nextWaveAt) return;
        // Keep the timetable anchored to the alarm, including periods when the active cap is full.
        nextWaveAt += interval;
        WaveNumber++;
        LastWaveAt = Time.time;
        int size = WaveNumber == 1 ? 2 : 3;
        for (int i = 0; i < size && ActivePoliceCount < maxActivePolice; i++)
            if (!SpawnPolice(false)) break;
    }

    private bool SpawnPolice(bool elite)
    {
        GameObject prefab = elite ? elitePrefab : policePrefab;
        if (prefab == null || player == null) return false;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;
        for (int attempt = 0; attempt < spawnPoints.Length * (elite ? 7 : 3); attempt++)
        {
            Transform marker = spawnPoints[spawnCursor++ % spawnPoints.Length];
            if (marker == null) continue;
            int ring = attempt / spawnPoints.Length;
            Vector3 offset = ring == 0 ? Vector3.zero : ring <= 2 ? new Vector3(ring == 1 ? .7f : -.7f, 0f, .55f) :
                new Vector3(Mathf.Sin(ring * 2.4f), 0f, Mathf.Cos(ring * 2.4f)) * (.75f + ring * .25f);
            if (!NavMesh.SamplePosition(marker.position + offset, out NavMeshHit hit, .9f, NavMesh.AllAreas)) continue;
            Vector3 feet = hit.position + Vector3.up * .035f;
            if (Vector3.Distance(player.transform.position, feet) < 2.5f) continue;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * .3f, feet + Vector3.up * 1.48f,
                .28f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count > 0) continue;
            GameObject instance = Instantiate(prefab, spawnedRoot, false);
            instance.SetActive(false);
            instance.name = elite ? "Elite SWAT - final squad " + (EliteSpawnedCount + 1) : "Police officer - wave " + WaveNumber;
            instance.transform.SetPositionAndRotation(feet, marker.rotation);
            FacilityHumanoid actor = instance.GetComponent<FacilityHumanoid>();
            FacilityPoliceOfficer officer = instance.GetComponent<FacilityPoliceOfficer>();
            if (actor == null || officer == null) { Destroy(instance); return false; }
            actor.player = player.transform;
            actor.IsHostile = true;
            officer.actor = actor;
            officer.projectileSystem = projectiles;
            officer.target = player.GetComponent<FacilityPlayerHealth>();
            foreach (SubtleEnemyHealthBar bar in instance.GetComponentsInChildren<SubtleEnemyHealthBar>(true)) bar.player = player.transform;
            if (actor.gore != null)
            {
                // Preserve ignored self anatomy and add the real player's collider before gore pool Awake runs.
                Collider[] ignored = new Collider[actor.bodyColliders.Length + 2];
                System.Array.Copy(actor.bodyColliders, ignored, actor.bodyColliders.Length);
                ignored[ignored.Length - 2] = actor.motor;
                ignored[ignored.Length - 1] = player.GetComponent<CharacterController>();
                actor.gore.ownerColliders = ignored;
            }
            actors.Add(new Entry { actor = actor, elite = elite });
            instance.SetActive(true);
            if (elite) EliteSpawnedCount++; else TotalPoliceSpawned++;
            return true;
        }
        return false;
    }

    private void CullCorpses()
    {
        int corpses = 0;
        foreach (Entry entry in actors)
            if (entry.actor != null && entry.actor.IsDead) corpses++;
        for (int i = 0; i < actors.Count; i++)
        {
            Entry entry = actors[i];
            if (entry.actor == null) { actors.RemoveAt(i--); continue; }
            if (!entry.actor.IsDead) continue;
            if (entry.elite && !entry.killRecorded) { recordedEliteKills++; entry.killRecorded = true; }
            if (entry.diedAt < 0f) entry.diedAt = Time.time;
            if (Time.time - entry.diedAt < 20f && corpses <= 4) continue;
            Destroy(entry.actor.gameObject); actors.RemoveAt(i--); corpses--;
        }
    }
}
