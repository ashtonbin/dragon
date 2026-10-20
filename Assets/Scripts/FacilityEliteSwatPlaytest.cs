using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Explicit CLI only: -facilityEliteTest outputDir. Finite final-squad escalation, escape gates and armored anatomy.</summary>
public sealed class FacilityEliteSwatPlaytest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "Two actual loot orders through the original 45-second drill at 4x simulation speed; held AI isolates progression. Physical spawn blockers prove retry, finite deployment and no premature escape. Real final squad kills unlock actual driver-seat extraction. Natural elite gun cadence and existing regional damage API prove helmet/knockdown/limb resistance while normal police retain original gore. Pause, death and extraction cancel pending waves/music. All temporary objects are runtime-only; wallet isolated.";
        public List<string> checks = new List<string>(), failures = new List<string>();
        public int regularBeforeEscalation, finalEliteSpawned, finalEliteKilled, pictures;
        public float warningMinAlpha = 1f, warningMaxAlpha, eliteShotSpacing;
    }
    readonly Receipt receipt = new Receipt();
    readonly Collider[] overlaps = new Collider[96];
    readonly RaycastHit[] hits = new RaycastHit[128];
    FacilityGameSession session;
    FacilityStoryDirector director;
    FacilityPoliceWaves waves;
    FacilityEliteSoundtrack music;
    RetroFPSController player;
    CharacterController motor;
    Camera eyes;
    FacilityHumanoid subject;
    GameObject blockers, stage, damageCase;
    Material floorMaterial;
    string output;
    bool collecting;
    int shotId = 1850000000;
    IDisposable account;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-facilityEliteTest");
        if (index < 0) return;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal)) { Application.Quit(2); return; }
        var runner = new GameObject("Final elite squad regression - CLI only").AddComponent<FacilityEliteSwatPlaytest>();
        runner.output = args[index + 1]; DontDestroyOnLoad(runner.gameObject);
    }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        Directory.CreateDirectory(output); Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
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
        collecting = false; Application.logMessageReceived -= OnLog; account?.Dispose(); Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "elite-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.6f); session = FacilityGameSession.Instance;
        Require(session != null, "The real saved title session loads.");
        FacilityGameSession previous = session; session.StartStory(); yield return WaitForSession(previous);
        Require(waves.elitePrefab != null && !waves.elitePrefab.activeSelf && waves.elitePrefab.GetComponent<FacilityEliteSwat>() != null &&
            waves.eliteTeamSize == 4 && Mathf.Abs(waves.eliteDelay - 5f) < .001f && music != null,
            "The saved mission has one finite four-member elite source, five-second arrival delay and escalation soundtrack.");
        CheckBandana(waves.policePrefab.GetComponent<FacilityHumanoid>(), false);
        CheckBandana(session.spawner.observerPrefab.GetComponent<FacilityHumanoid>(), true);
        yield return PrepareFirstLoot(false);
        QuarantineRegularPolice(); BlockSpawnPoints();
        yield return Interact(director.goldPickup);
        Require(waves.EliteAssaultActive && !waves.AssaultActive && waves.SecondsUntilNextWave == 0f && !director.ExtractionUnlocked &&
            !director.vehicleExtraction.CanBoard(out _) && !director.TryInteract(director.sedanExit, out _),
            "Both loot piles permanently stop regular scheduling and lock extraction while the final squad is incoming.");
        Require(!waves.BeginEliteAssault(), "A second escalation request cannot schedule a second elite squad.");
        yield return new WaitForSeconds(.15f);
        Require(music.IsEscalated && music.Source != null && music.Source.clip != null && music.Source.isPlaying,
            "Both loot piles start the actual tense escalation music source.");
        float pulseUntil = Time.time + 2.15f; bool brightSaved = false, dimSaved = false;
        while (Time.time < pulseUntil)
        {
            HoldActors(); Color color = director.heistStatusText.color;
            receipt.warningMinAlpha = Mathf.Min(receipt.warningMinAlpha, color.a); receipt.warningMaxAlpha = Mathf.Max(receipt.warningMaxAlpha, color.a);
            if (!brightSaved && color.a > .95f) { brightSaved = true; yield return Capture("01_swat_warning_bright", true); }
            if (!dimSaved && brightSaved && color.a < .65f) { dimSaved = true; yield return Capture("02_swat_warning_dim", true); }
            yield return null;
        }
        Require(director.heistStatusPanel.activeInHierarchy && director.heistStatusText.text.Contains("SWAT TEAM INCOMING") &&
            !director.heistStatusText.text.Contains("POLICE IN") && director.heistStatusText.color.r > director.heistStatusText.color.g * 2f &&
            receipt.warningMaxAlpha - receipt.warningMinAlpha > .15f,
            "The real HUD replaces the ordinary timer with a visible red warning whose alpha pulses through distinct phases.");
        session.PauseGame(); yield return null;
        float heldArrival = waves.SecondsUntilEliteArrival, heldTransition = music.TransitionProgress; int heldSample = music.Source.timeSamples;
        yield return new WaitForSecondsRealtime(.3f);
        Require(Mathf.Abs(waves.SecondsUntilEliteArrival - heldArrival) < .0001f && Mathf.Abs(music.TransitionProgress - heldTransition) < .0001f &&
            Mathf.Abs(music.Source.timeSamples - heldSample) <= 1024 && waves.EliteSpawnedCount == 0,
            "Pause freezes the incoming squad clock, music transition and actual audio playback without spawning anyone.");
        session.ResumeGame();
        float until = Time.realtimeSinceStartup + waves.SecondsUntilEliteArrival + 1.2f;
        while (Time.realtimeSinceStartup < until) { HoldActors(); yield return null; }
        Require(waves.EliteSpawnedCount == 0 && !waves.EliteDeploymentComplete && !waves.EliteTeamDefeated && !director.ExtractionUnlocked &&
            !director.vehicleExtraction.CanBoard(out _) && FacilityEconomy.Cash == 0,
            "Actual physical obstructions block deployment and cannot be mistaken for a defeated squad or unlocked exit.");
        ClearBlockers(); until = Time.realtimeSinceStartup + 4f;
        while (!waves.EliteDeploymentComplete && Time.realtimeSinceStartup < until) { HoldActors(); yield return null; }
        HoldActors();
        Require(waves.EliteDeploymentComplete && waves.EliteTeamArrived && waves.EliteSpawnedCount == 4 && waves.ActiveEliteCount == 4 &&
            waves.TotalPoliceSpawned == receipt.regularBeforeEscalation && !waves.AssaultActive,
            "Removing the blockers lets the same one-shot squad retry and deploy exactly four members without new regular police.");
        List<FacilityHumanoid> team = Team(); Require(team.Count == 4, "The final squad consists of four actual live tracked elite actors.");
        foreach (FacilityHumanoid actor in team) CheckElite(actor);
        Pose(new Vector3(-6f, .05f, -18f), team[0].AimPoint);
        yield return CaptureActor("03_actual_elite_team_member", team[0]);
        Time.timeScale = 4f; float finiteUntil = Time.time + waves.interval + 1f;
        while (Time.time < finiteUntil) { HoldActors(); yield return null; }
        Time.timeScale = 1f;
        Require(waves.EliteSpawnedCount == 4 && waves.TotalPoliceSpawned == receipt.regularBeforeEscalation && !waves.BeginEliteAssault(),
            "Waiting past another ordinary wave deadline cannot create a fifth elite or restart ordinary waves.");
        yield return CombatChecks();
        for (int i = 0; i < 3; i++) Kill(team[i]);
        yield return null;
        Require(waves.EliteKilledCount == 3 && waves.ActiveEliteCount == 1 && !waves.EliteTeamDefeated && !director.ExtractionUnlocked,
            "Three dead elites and the final living member keep extraction locked.");
        yield return ReadyAtDriverSeat();
        Require(!director.vehicleExtraction.CanBoard(out _) && !director.IsInteractionAvailable(director.sedanExit, out _) &&
            !director.TryInteract(director.sedanExit, out _) && !director.vehicleExtraction.IsBoarding && FacilityEconomy.Cash == 0,
            "Even at the visible seat through an open driver door, the last living elite prevents entering or paying out.");
        Kill(team[3]); yield return null;
        receipt.finalEliteSpawned = waves.EliteSpawnedCount; receipt.finalEliteKilled = waves.EliteKilledCount;
        Require(waves.EliteTeamDefeated && waves.EliteKilledCount == 4 && waves.ActiveEliteCount == 0 && director.ExtractionUnlocked &&
            director.vehicleExtraction.CanBoard(out _), "Only deployment and all four real kills unlock the actual driver's-seat extraction.");
        yield return Capture("09_squad_defeated_exit_unlocked", true);
        Require(director.TryInteract(director.sedanExit, out string leaveReason), "The unlocked real seat begins extraction: " + leaveReason);
        until = Time.realtimeSinceStartup + 5f;
        while (session.CurrentState != FacilityGameState.LevelComplete && Time.realtimeSinceStartup < until) yield return null;
        Require(session.CurrentState == FacilityGameState.LevelComplete && director.vehicleExtraction.IsSeated && FacilityEconomy.Cash == 2500 &&
            !waves.EliteAssaultActive && !waves.AssaultActive && !music.IsEscalated && !music.Source.isPlaying,
            "The cleared-squad seated extraction pays exactly $2,500 and stops all reinforcement and escalation audio.");
        int completedCount = waves.EliteSpawnedCount; Time.timeScale = 4f; yield return new WaitForSeconds(8f); Time.timeScale = 1f;
        Require(waves.EliteSpawnedCount == completedCount && !waves.BeginEliteAssault() && FacilityEconomy.Cash == 2500,
            "Advancing the clock after completion cannot spawn more SWAT or award another payout.");
        previous = session; session.RestartGame(); yield return WaitForSession(previous);
        yield return PrepareFirstLoot(true);
        yield return Interact(director.cashPickup);
        Require(waves.EliteAssaultActive && music.IsEscalated && !director.ExtractionUnlocked,
            "The opposite gold-first collection order also escalates only after collecting the second required loot pile.");
        for (int hit = 0; hit < 3; hit++)
        {
            Require(session.health.TryTakeHit(transform), "The death-cancellation scenario applies a real health hit.");
            if (hit < 2) { float wait = Time.time + .95f; while (Time.time < wait) { HoldActors(); yield return null; } }
        }
        yield return null;
        Require(session.CurrentState == FacilityGameState.Dead && !waves.EliteAssaultActive && !waves.AssaultActive &&
            !music.IsEscalated && !music.Source.isPlaying && waves.EliteSpawnedCount == 0,
            "Death during the incoming countdown cancels the pending elite deployment and actual escalation source.");
        Time.timeScale = 4f; yield return new WaitForSeconds(10f); Time.timeScale = 1f;
        Require(waves.EliteSpawnedCount == 0 && !director.ExtractionUnlocked && FacilityEconomy.Cash == 2500,
            "Even a deliberately advanced post-death clock cannot deliver late SWAT, unlock extraction or pay the failed run.");
    }

    IEnumerator PrepareFirstLoot(bool goldFirst)
    {
        HoldActors(); Require(!waves.EliteAssaultActive && !waves.BeginEliteAssault() && !music.IsEscalated,
            "A fresh run cannot manually escalate before both loot piles are secured.");
        yield return Interact(director.drillPickup.door.interaction); yield return new WaitForSeconds(director.drillPickup.door.travelSeconds + .1f);
        yield return Interact(director.drillPickup);
        FacilityHitRegion chest = Region(director.receptionist, FacilityBodyRegion.Torso);
        director.receptionist.ReceiveMelee(++shotId, chest.transform.position, Vector3.forward, chest.GetComponent<Collider>());
        Require(director.AlarmActive && waves.AssaultActive, "The original receptionist attack starts ordinary alarm reinforcements.");
        Transform car = director.sedanExit.transform.parent;
        foreach (FacilityDoor door in FindObjectsByType<FacilityDoor>(FindObjectsSortMode.None)) if (!door.IsLocked && !door.transform.IsChildOf(car)) door.ForceOpen();
        yield return new WaitForSeconds(1f); yield return Interact(director.vaultInteraction);
        Time.timeScale = 4f; float until = Time.realtimeSinceStartup + 22f;
        while (!director.VaultOpen && Time.realtimeSinceStartup < until) { HoldActors(); yield return null; }
        Require(director.VaultOpen && director.vaultDrill.ElapsedSeconds == 45f, "The actual vault still requires forty-five simulated seconds of drilling.");
        yield return new WaitForSeconds(1f); Time.timeScale = 1f; HoldActors();
        yield return Interact(goldFirst ? director.goldPickup : director.cashPickup);
        Require(!waves.EliteAssaultActive && !waves.BeginEliteAssault() && !music.IsEscalated && !director.ExtractionUnlocked,
            (goldFirst ? "Gold" : "Cash") + " alone cannot start the elite squad, music or extraction.");
        receipt.regularBeforeEscalation = waves.TotalPoliceSpawned;
        Require(receipt.regularBeforeEscalation > 0 && waves.ActivePoliceCount > 0, "The escalation replaces a real existing ordinary reinforcement schedule.");
    }
    void HoldActors()
    {
        foreach (FacilityHumanoid actor in FindObjectsByType<FacilityHumanoid>(FindObjectsSortMode.None))
        {
            if (actor == subject) continue;
            actor.enabled = false; var officer = actor.GetComponent<FacilityPoliceOfficer>(); if (officer != null) officer.enabled = false;
            var guard = actor.GetComponent<FacilityKnifeGuard>(); if (guard != null) guard.enabled = false;
        }
    }
    void QuarantineRegularPolice()
    {
        int index = 0;
        foreach (FacilityPoliceOfficer officer in waves.GetComponentsInChildren<FacilityPoliceOfficer>(true))
        {
            if (officer.GetComponent<FacilityEliteSwat>() != null) continue;
            officer.enabled = false; officer.actor.enabled = false; officer.actor.motor.enabled = false;
            officer.transform.position = new Vector3(-90f + index++ * 2f, .05f, 70f);
        }
        Physics.SyncTransforms();
    }
    void BlockSpawnPoints()
    {
        blockers = new GameObject("Temporary physical elite-spawn blockers");
        foreach (Transform point in waves.spawnPoints)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.transform.SetParent(blockers.transform);
            box.transform.position = point.position + Vector3.up * 1.3f; box.transform.localScale = new Vector3(8f, 3f, 8f); box.GetComponent<Renderer>().enabled = false;
        }
        Physics.SyncTransforms();
    }
    void ClearBlockers()
    { if (blockers == null) return; foreach (Collider collider in blockers.GetComponentsInChildren<Collider>()) collider.enabled = false; Destroy(blockers); blockers = null; Physics.SyncTransforms(); }
    List<FacilityHumanoid> Team()
    { var result = new List<FacilityHumanoid>(); foreach (FacilityEliteSwat elite in waves.GetComponentsInChildren<FacilityEliteSwat>(true)) result.Add(elite.GetComponent<FacilityHumanoid>()); return result; }
    void CheckElite(FacilityHumanoid actor)
    {
        var elite = actor.GetComponent<FacilityEliteSwat>(); var officer = actor.GetComponent<FacilityPoliceOfficer>();
        Require(elite != null && actor.hitsToKill == 6 && actor.hitsToKnockDown == 3 && actor.helmetHitsToBreak == 2 && actor.hitsToSeverLimb == 2 &&
            Mathf.Abs(officer.walkSpeed - 3.5f) < .001f && Mathf.Abs(officer.aimWindup - .26f) < .001f && Mathf.Abs(officer.shotInterval - .9f) < .001f,
            "The actual elite variant retains its six-hit health, resistant anatomy and faster existing gun AI.");
        Require(actor.hitRegions.Length == 11 && elite.helmetPlates != null && elite.helmetPlates.Length == 2 && elite.eyeRenderer != null,
            "The elite kit uses the original eleven physical regions with two destructible helmet plates and its night vision rig.");
        Material eye = elite.eyeRenderer.sharedMaterial; var optic = elite.eyeRenderer.GetComponent<FacilityEliteOptic>();
        Require(elite.opticRenderers != null && elite.opticRenderers.Length == 4 &&
            actor.GetComponentsInChildren<FacilityEliteOptic>(true).Length == 4 &&
            optic != null && optic.glowRenderer == elite.eyeRenderer && eye != null &&
            eye.shader.name == "Facility/Elite Optical Glow" && eye.GetFloat("_Intensity") > 2f,
            "The armored helmet has four round night vision optics with white-hot red cores and depth-tested horizontal glare.");
    }
    IEnumerator CombatChecks()
    {
        stage = new GameObject("Elite anatomy diagnostic stage - runtime only");
        floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); floorMaterial.SetColor("_BaseColor", new Color(.13f,.15f,.17f));
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(stage.transform); floor.transform.position = new Vector3(100f,-.1f,108f); floor.transform.localScale = new Vector3(44f,.2f,44f); floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        var lamp = new GameObject("Temporary elite photograph light"); lamp.transform.SetParent(stage.transform); lamp.transform.position = new Vector3(98f,4f,103f);
        Light light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.intensity = 12f; light.range = 18f; light.shadows = LightShadows.Soft;
        yield return NewSubject(waves.elitePrefab); CheckElite(subject);
        var officer = subject.GetComponent<FacilityPoliceOfficer>(); float deadline = Time.realtimeSinceStartup + 4f;
        while (officer.ShotsFired < 1 && Time.realtimeSinceStartup < deadline) yield return null;
        Require(officer.ShotsFired == 1, "The intact elite naturally commits and fires its existing finite-flight gun.");
        float first = Time.time;
        while (officer.ShotsFired < 2 && Time.realtimeSinceStartup < deadline) yield return null;
        receipt.eliteShotSpacing = Time.time - first;
        Require(officer.ShotsFired >= 2 && receipt.eliteShotSpacing < 1.4f && waves.projectiles.ActiveProjectiles > 0,
            "Natural elite follow-up fire is faster than ordinary police and still launches actual finite-flight rounds.");
        officer.enabled = false; waves.projectiles.Clear(); session.health.ResetHealth(); yield return CaptureActor("04_elite_helmet_intact", subject);
        FacilityHitRegion hand = officer.gunTip.GetComponentInParent<FacilityHitRegion>(true); int shots = officer.ShotsFired;
        Damage(subject, hand); Require(!hand.IsDetached && !subject.IsKnockedDown && subject.HitsTaken == 1, "The first elite gun-arm hit wounds armor without severing or knocking down.");
        officer.enabled = true; Damage(subject, hand); yield return null;
        Require(hand.IsDetached && officer.gunTip.IsChildOf(hand.transform) && !officer.gunTip.IsChildOf(subject.transform), "The second elite arm hit severs the real armor and gun together.");
        yield return new WaitForSeconds(1.5f);
        Require(officer.ShotsFired == shots && !officer.IsAiming && !subject.IsDead, "A surviving elite cannot fire its detached gun.");
        yield return DisposeSubject(); yield return NewSubject(waves.elitePrefab); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        FacilityHitRegion head = Region(subject, FacilityBodyRegion.Head); var marker = subject.GetComponent<FacilityEliteSwat>();
        Damage(subject, head); Require(marker.HelmetHitsTaken == 1 && !subject.HeadExploded && !subject.IsDead, "The first elite head hit damages a real helmet plate without exploding the head.");
        Damage(subject, head); Require(marker.HelmetBroken && marker.HelmetHitsTaken == 2 && !subject.HeadExploded && !subject.IsDead && marker.helmetDamageReveal.activeInHierarchy,
            "The second unique head hit removes the remaining helmet plating and exposes its damaged inner mask.");
        yield return CaptureActor("05_elite_broken_helmet", subject);
        Damage(subject, head); Require(subject.HeadExploded && subject.IsDead && subject.gore.HeadBurstCount == 1 && !subject.head.gameObject.activeInHierarchy,
            "A third unique elite head hit retains complete head destruction and its original dedicated gore burst.");
        yield return DisposeSubject(); yield return NewSubject(waves.elitePrefab); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        FacilityHitRegion torso = Region(subject, FacilityBodyRegion.Torso);
        Damage(subject, torso); Damage(subject, torso); Require(subject.HitsTaken == 2 && !subject.IsKnockedDown && !subject.IsDead, "Two elite torso hits retain standing armored resistance.");
        Damage(subject, torso); Require(subject.HitsTaken == 3 && subject.IsKnockedDown && !subject.IsDead, "The third elite torso hit finally causes the original physical knockdown.");
        Damage(subject, torso); Damage(subject, torso); Damage(subject, torso); Require(subject.IsDead && subject.HitsTaken == 6, "The armored torso still dies after its configured sixth unique hit.");
        yield return CaptureActor("06_elite_torso_damage", subject);
        var scarf=subject.GetComponentInChildren<FacilityBandanaDamage>(true);
        Require(scarf!=null && scarf.coveredPanels.Length>=3 && !scarf.drape.enabled && scarf.transform.parent.gameObject.activeInHierarchy,
            "Upper chest wounds tear away the overlapping scarf drape while the full neck collar remains.");
        yield return DisposeSubject(); yield return NewSubject(waves.policePrefab); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        Require(subject.hitsToKill == 3 && subject.hitsToKnockDown == 1 && subject.helmetHitsToBreak == 0 && subject.hitsToSeverLimb == 1,
            "Ordinary police preserve their original three-hit health and first-hit anatomical damage defaults.");
        CheckBandana(subject, false); yield return CaptureActor("07_regular_black_neck_bandana", subject);
        Damage(subject, Region(subject, FacilityBodyRegion.Head)); Require(subject.HeadExploded && subject.IsDead, "An ordinary unarmored head still explodes on its first regional hit.");
        yield return DisposeSubject(); yield return NewSubject(waves.policePrefab); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        Damage(subject, Region(subject, FacilityBodyRegion.Torso)); Require(subject.HitsTaken == 1 && subject.IsKnockedDown && !subject.IsDead, "Ordinary torso damage still knocks down immediately without killing.");
        yield return DisposeSubject(); Destroy(stage); stage = null; Destroy(floorMaterial); floorMaterial = null;
    }
    IEnumerator NewSubject(GameObject prefab)
    {
        damageCase = new GameObject("Fresh armored damage case"); damageCase.transform.SetParent(stage.transform);
        GameObject clone = Instantiate(prefab, damageCase.transform, false); clone.SetActive(false); clone.transform.SetPositionAndRotation(new Vector3(100f,.045f,108f), Quaternion.Euler(0f,180f,0f));
        subject = clone.GetComponent<FacilityHumanoid>(); subject.player = player.transform;
        var officer = clone.GetComponent<FacilityPoliceOfficer>(); officer.actor = subject; officer.target = session.health; officer.projectileSystem = waves.projectiles;
        var ignored = new Collider[subject.bodyColliders.Length + 2]; Array.Copy(subject.bodyColliders, ignored, subject.bodyColliders.Length); ignored[ignored.Length - 2] = subject.motor; ignored[ignored.Length - 1] = motor; subject.gore.ownerColliders = ignored;
        Pose(new Vector3(100f,.05f,100f), new Vector3(100f,1.2f,108f)); session.health.ResetHealth(); waves.projectiles.Clear(); clone.SetActive(true); Physics.SyncTransforms(); yield return null;
    }
    IEnumerator DisposeSubject() { waves.projectiles.Clear(); if (damageCase != null) Destroy(damageCase); subject = null; damageCase = null; yield return null; yield return null; }
    static FacilityHitRegion Region(FacilityHumanoid actor, FacilityBodyRegion kind) { foreach (FacilityHitRegion region in actor.hitRegions) if (region.kind == kind) return region; throw new InvalidOperationException("Missing damage region " + kind); }
    void Damage(FacilityHumanoid actor, FacilityHitRegion region) { Collider collider = region.GetComponent<Collider>(); actor.ReceivePellet(++shotId, collider.bounds.center, Vector3.forward, collider); }
    void Kill(FacilityHumanoid actor) { for (int i = 0; i < 3 && !actor.IsDead; i++) Damage(actor, Region(actor, FacilityBodyRegion.Head)); Require(actor.IsDead, "The final squad kill is recorded from actual armored-head damage."); }
    void CheckBandana(FacilityHumanoid actor, bool red)
    {
        Renderer cloth = null; foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true)) if (renderer.name.IndexOf("Bandana - small wrapped neck cloth", StringComparison.OrdinalIgnoreCase) >= 0) cloth = renderer;
        Require(cloth != null && cloth.GetComponentInParent<FacilityHitRegion>(true) != null, "The " + (red ? "observer" : "police") + " bandana is real cloth attached to existing neck anatomy.");
        Color color = cloth.sharedMaterial.HasProperty("_BaseColor") ? cloth.sharedMaterial.GetColor("_BaseColor") : cloth.sharedMaterial.color;
        Require(red ? color.r > .08f && color.r > color.g * 1.5f && color.r > color.b * 1.5f : color.maxColorComponent < .15f,
            "The small neck bandana has the requested " + (red ? "red observer" : "black police") + " cloth color.");
        Bounds wrap=cloth.bounds;
        foreach(var part in cloth.GetComponentsInChildren<Renderer>(true))wrap.Encapsulate(part.bounds);
        Require(wrap.size.y > .21f && wrap.size.y < .33f && wrap.size.x > .27f,
            "The folded neck wrap fully covers the thin neck and drapes over the upper chest.");
    }
    IEnumerator ReadyAtDriverSeat()
    {
        var door = director.vehicleExtraction.driverDoor;
        if (!door.IsOpen) { yield return Interact(door.interaction); yield return new WaitForSeconds(door.travelSeconds + .15f); }
        Pose(FindPose(director.sedanExit), director.sedanExit.transform.position); yield return null;
    }
    IEnumerator Interact(FacilityStoryInteractable item) { Pose(FindPose(item), item.transform.position); yield return null; Require(director.TryInteract(item, out string reason), "Actual " + item.kind + " interaction: " + reason); }
    Vector3 FindPose(FacilityStoryInteractable item)
    {
        Vector3 aim = item.transform.position; Transform car = director.sedanExit.transform.parent;
        for (int r = 0; r < 8; r++) for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI / 32f; Vector3 candidate = aim + new Vector3(Mathf.Sin(a),0f,Mathf.Cos(a)) * (.8f + r * .2f); candidate.y = .05f;
            if (!NavMesh.SamplePosition(candidate + Vector3.up * .2f, out NavMeshHit floor, .4f, NavMesh.AllAreas)) continue;
            candidate = floor.position + Vector3.up * .05f;
            if ((item.kind == FacilityStoryInteractionKind.Sedan || item.door == director.vehicleExtraction.driverDoor) && car.InverseTransformPoint(candidate).x >= -1.02f) continue;
            int count = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * .36f, candidate + Vector3.up * 1.5f, .30f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool clear = count < overlaps.Length; for (int n = 0; n < count && clear; n++) if (overlaps[n] != null && !overlaps[n].transform.IsChildOf(player.transform)) clear = false;
            if (!clear) continue; Vector3 origin = candidate + Vector3.up * 1.65f, offset = aim - origin; if (offset.magnitude > director.interactionRange - .05f) continue;
            count = Physics.RaycastNonAlloc(origin, offset.normalized, hits, offset.magnitude + .08f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Collider first = null; float nearest = float.PositiveInfinity;
            for (int n = 0; n < count; n++) { Collider hit = hits[n].collider; if (hit == null || hit is CharacterController || hit.transform.IsChildOf(player.transform)) continue; if (hit.isTrigger && hit.GetComponentInParent<FacilityStoryInteractable>() == null) continue; if (hits[n].distance < nearest) { nearest = hits[n].distance; first = hit; } }
            var resolved = first != null ? first.GetComponentInParent<FacilityStoryInteractable>() : null; if (resolved == null && first != null) resolved = first.GetComponentInParent<FacilityDoor>()?.interaction; if (resolved == item) return candidate;
        }
        throw new InvalidOperationException("No supported unobstructed interaction pose for " + item.name);
    }
    IEnumerator WaitForSession(FacilityGameSession previous)
    {
        float until = Time.realtimeSinceStartup + 15f; while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance == previous) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(.3f); session = FacilityGameSession.Instance;
        Require(session != null && session != previous && session.storyDirector != null && session.CurrentState == FacilityGameState.Playing, "A fresh actual story session loads.");
        director = session.storyDirector; waves = director.policeWaves; music = director.eliteSoundtrack; player = session.player; motor = player.GetComponent<CharacterController>(); eyes = player.GetComponentInChildren<Camera>(); player.enabled = false;
    }
    void Pose(Vector3 feet, Vector3 aim) { motor.enabled = false; player.transform.position = feet; motor.enabled = true; player.ResetRecoil(); Vector3 flat = aim - eyes.transform.position; flat.y = 0f; player.transform.rotation = Quaternion.LookRotation(flat); eyes.transform.rotation = Quaternion.LookRotation(aim - eyes.transform.position); Physics.SyncTransforms(); }
    IEnumerator CaptureActor(string name, FacilityHumanoid actor)
    {
        Vector3 centre = actor.HeadExploded ? actor.chest.position : actor.head.position - Vector3.up * .18f;
        Vector3 position = centre + actor.transform.forward * 1.7f + actor.transform.right * .2f + Vector3.up * .05f;
        yield return Capture(name, false, position, centre);
    }
    IEnumerator Capture(string name, bool ui, Vector3? position = null, Vector3? aim = null)
    {
        yield return new WaitForEndOfFrame();
        var node = new GameObject("Temporary elite proof camera"); Camera camera = node.AddComponent<Camera>(); camera.CopyFrom(eyes); camera.enabled = false;
        if (position.HasValue) { camera.transform.SetPositionAndRotation(position.Value, Quaternion.LookRotation(aim.Value - position.Value)); camera.fieldOfView = 38f; }
        else camera.transform.SetPositionAndRotation(eyes.transform.position, eyes.transform.rotation);
        camera.aspect = 1.6f; var data = camera.GetUniversalAdditionalCameraData(); data.renderShadows = true; data.renderPostProcessing = false;
        Canvas canvas = session.canvas; bool enabled = canvas.enabled; RenderMode mode = canvas.renderMode; Camera prior = canvas.worldCamera; float plane = canvas.planeDistance;
        Transform[] weapons = { session.inventory.fistsModel, session.inventory.pistolModel, session.inventory.shotgunModel, session.inventory.ak47Model }; var shown = new bool[weapons.Length];
        RenderTexture active = RenderTexture.active; var target = new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32); target.Create(); var pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try
        {
            canvas.enabled = enabled && ui; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .15f; Canvas.ForceUpdateCanvases();
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) { shown[i] = weapons[i].gameObject.activeSelf; weapons[i].gameObject.SetActive(false); }
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG()); receipt.pictures++;
        }
        finally { canvas.enabled = enabled; canvas.renderMode = mode; canvas.worldCamera = prior; canvas.planeDistance = plane; for (int i=0;i<weapons.Length;i++) if(weapons[i]!=null)weapons[i].gameObject.SetActive(shown[i]); RenderTexture.active=active; Destroy(pixels);target.Release();Destroy(target);Destroy(node); }
    }
    void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); receipt.checks.Add(description); }
    void OnLog(string message, string trace, LogType kind) { if (collecting && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace); }
}
