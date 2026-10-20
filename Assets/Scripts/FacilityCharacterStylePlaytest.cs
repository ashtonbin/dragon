using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Explicit CLI only: -facilityCharacterTest outputDir. Appearance, armed AI and anatomical damage integration.</summary>
public sealed class FacilityCharacterStylePlaytest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "Actual saved story Staff/Security, active police wave source, and sandbox Observer/Infected spawn sources; original eleven-region anatomy, removable patch/wound alignment and combat stats. A real saved SWAT clone naturally fires its existing gun. Fresh clones then receive the existing regional pellet damage API against actual anatomical colliders, proving removable torso armor, complete helmet/head destruction, detached armored gun arm and suppression after recovery, and leg-loss crawl. This focused style diagnostic does not replace existing ballistic tests. All stage objects are runtime-only; wallet is isolated.";
        public List<string> checks = new List<string>(), failures = new List<string>();
        public int actorsInspected, regionsInspected, appearanceRenderersInspected, policeShotsBeforeSever, policeShotsAfterRecovery, pictures;
        public string swatTorsoMesh;
    }
    readonly Receipt receipt = new Receipt();
    readonly Vector3 stageOrigin = new Vector3(100f, .045f, 108f);
    FacilityGameSession session;
    FacilityStoryDirector director;
    RetroFPSController player;
    CharacterController motor;
    Camera eyes;
    FacilityPoliceProjectiles projectiles;
    GameObject policePrefab, stage, caseRoot;
    FacilityHumanoid subject;
    Material floorMaterial;
    string output;
    bool collecting;
    int shotId = 1800000000;
    IDisposable account;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "-facilityCharacterTest");
        if (flag < 0) return;
        if (flag + 1 >= args.Length || args[flag + 1].StartsWith("-", StringComparison.Ordinal)) { Application.Quit(2); return; }
        var runner = new GameObject("Character appearance and gore checks - CLI only").AddComponent<FacilityCharacterStylePlaytest>();
        runner.output = args[flag + 1]; DontDestroyOnLoad(runner.gameObject);
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
        collecting = false; Application.logMessageReceived -= OnLog;
        account?.Dispose(); Time.timeScale = 1f; AudioListener.pause = false;
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "character-style-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.6f);
        session = FacilityGameSession.Instance;
        Require(session != null && session.CurrentState == FacilityGameState.MainMenu, "The saved title menu loads for the appearance check.");
        FacilityGameSession previous = session; session.StartStory(); yield return WaitForSession(previous, FacilityGameMode.Story);
        director = session.storyDirector;
        Require(director != null && director.guards != null && director.guards.Length == 5 && director.receptionist != null,
            "The five original story observers and receptionist references are preserved.");
        Inspect(director.receptionist, FacilityCharacterStyle.Staff, "Story receptionist");
        FacilityHumanoid security = null;
        foreach (FacilityKnifeGuard guard in director.guards)
        {
            Require(guard != null && guard.actor != null && guard.knifeProp != null && guard.knifeProp.GetComponentInParent<FacilityHitRegion>(true) != null &&
                Mathf.Abs(guard.walkSpeed - 2.35f) < .001f && !guard.Alerted && !guard.startAlerted,
                "Story knife AI retains its initially passive state, original speed and dismemberable weapon attachment.");
            if (guard.actor == director.receptionist) continue;
            Inspect(guard.actor, FacilityCharacterStyle.Security, "Story security"); security = guard.actor;
        }
        yield return CaptureActor("01_story_staff_face", director.receptionist, true);
        if (security != null) yield return CaptureActor("02_story_security", security, false);
        Require(director.policeWaves != null && director.policeWaves.policePrefab != null && director.policeWaves.projectiles != null,
            "The actual alarm wave source retains its saved police prefab and projectile system.");
        director.policeWaves.StopAssault(); policePrefab = director.policeWaves.policePrefab; projectiles = director.policeWaves.projectiles;
        Require(!policePrefab.activeSelf && policePrefab.GetComponent<FacilityPoliceOfficer>() != null, "Police waves still use a saved inactive armed prefab.");
        Inspect(policePrefab.GetComponent<FacilityHumanoid>(), FacilityCharacterStyle.Swat, "Saved SWAT wave prefab");
        BuildStage();
        yield return NewPolice("Intact gun-hand scenario");
        Inspect(subject, FacilityCharacterStyle.Swat, "Active SWAT");
        FacilityPoliceOfficer officer = subject.GetComponent<FacilityPoliceOfficer>();
        Require(officer != null && officer.enabled && officer.actor == subject && officer.gunTip != null && officer.gunReport != null && officer.gunSource != null &&
            Mathf.Abs(officer.walkSpeed - 2.6f) < .001f && Mathf.Abs(officer.aimWindup - .42f) < .001f && Mathf.Abs(officer.shotInterval - 1.65f) < .001f,
            "The armored officer retains its original armed AI, sound, speed, aim windup and shot cadence.");
        float deadline = Time.realtimeSinceStartup + 4f;
        while (officer.ShotsFired == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Require(officer.ShotsFired > 0 && projectiles.ShotsFired > 0, "An intact SWAT officer naturally aims and fires its existing finite-flight weapon.");
        receipt.policeShotsBeforeSever = officer.ShotsFired;
        officer.enabled = false; projectiles.Clear(); session.health.ResetHealth();
        yield return CaptureActor("03_swat_intact", subject, false);
        FacilityHitRegion hand = officer.gunTip.GetComponentInParent<FacilityHitRegion>(true);
        Require(hand != null && hand.kind == FacilityBodyRegion.ForearmRight, "The SWAT weapon remains owned by its real severable right forearm.");
        Renderer[] armAppearance = AttachedAppearance(subject, hand);
        Require(armAppearance.Length > 0, "The armored gun arm carries actual authored appearance geometry on its hit region.");
        officer.enabled = true;
        Damage(subject, hand);
        yield return null;
        Require(hand.IsDetached && hand.JointSevered && hand.GetComponent<CharacterJoint>() == null && !subject.IsDead && subject.HitsTaken == 1 &&
            officer.gunTip.IsChildOf(hand.transform) && !officer.gunTip.IsChildOf(subject.transform),
            "A real anatomical forearm hit severs the armor and gun together while preserving a one-hit survivor.");
        foreach (Renderer renderer in armAppearance)
            Require(renderer == null || (renderer.transform.IsChildOf(hand.transform) && !renderer.transform.IsChildOf(subject.transform)),
                "Gun-arm appearance follows the detached physical forearm rather than floating on the standing body.");
        Require(subject.gore.SeverBurstCount == 1 && subject.gore.TotalDropsEmitted > 0, "Arm severing retains its dedicated physical gore burst.");
        yield return CaptureActor("04_swat_gun_arm_severed", subject, false);
        yield return Recovered(subject);
        int shotsAfterSever = officer.ShotsFired;
        yield return new WaitForSeconds(officer.shotInterval + officer.aimWindup + .35f);
        receipt.policeShotsAfterRecovery = officer.ShotsFired;
        Require(officer.enabled && !subject.IsDead && subject.motor.enabled && !subject.IsKnockedDown && !subject.IsRecovering &&
            officer.ShotsFired == shotsAfterSever && !officer.IsAiming,
            "After full recovery, a living disarmed SWAT officer cannot aim or fire its detached gun.");
        yield return CaptureActor("05_swat_recovered_disarmed", subject, false);
        CheckPool(subject);
        yield return DisposeCase();

        yield return NewPolice("Torso armor removal"); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        FacilityHitRegion torso = Region(subject, FacilityBodyRegion.Torso);
        int beforePatches = DisabledPatches(torso);
        Damage(subject, torso); yield return null;
        Require(subject.HitsTaken == 1 && !subject.IsDead && subject.IsKnockedDown && subject.RemovedPatchCount >= 2 && DisabledPatches(torso) == beforePatches + 2,
            "A torso pellet hit still removes two actual armor patches and causes the original nonfatal knockdown.");
        int openWounds = 0;
        for (int i = 0; i < torso.outerPatches.Length; i++)
            if (!torso.outerPatches[i].enabled && torso.woundBeds[i] != null && torso.woundBeds[i].activeInHierarchy)
            {
                openWounds++;
                Require(torso.woundBeds[i].GetComponentInChildren<MeshFilter>(true) != null,
                    "Removing torso armor exposes a real recessed wound mesh rather than a painted damage mark.");
            }
        Require(openWounds >= 2, "Removed SWAT torso armor exposes both aligned original tissue wound beds.");
        yield return CaptureActor("06_swat_torso_armor_open", subject, true); CheckPool(subject);
        yield return DisposeCase();

        yield return NewPolice("Helmet and head destruction"); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        FacilityHitRegion head = Region(subject, FacilityBodyRegion.Head);
        Renderer[] headAppearance = head.GetComponentsInChildren<Renderer>(true);
        Require(headAppearance.Length > 4, "The SWAT head carries its actual helmet and face geometry.");
        Damage(subject, head); yield return null;
        Require(subject.HeadExploded && subject.IsDead && subject.HitsTaken == 3 && subject.gore.HeadBurstCount == 1,
            "A head hit still destroys the armored head immediately and emits exactly one dedicated head burst.");
        foreach (Renderer renderer in headAppearance) Require(!renderer.enabled || !renderer.gameObject.activeInHierarchy, "Helmet/face geometry disappears with the exploded head.");
        foreach (Collider collider in head.GetComponentsInChildren<Collider>(true)) Require(!collider.enabled || !collider.gameObject.activeInHierarchy, "The exploded head cannot leave an active hidden collider.");
        yield return CaptureActor("07_swat_head_destroyed", subject, false); CheckPool(subject);
        yield return DisposeCase();

        yield return NewPolice("Armored leg loss and crawl"); subject.GetComponent<FacilityPoliceOfficer>().enabled = false;
        FacilityHitRegion leg = Region(subject, FacilityBodyRegion.ThighLeft), shin = Region(subject, FacilityBodyRegion.ShinLeft);
        Renderer[] legAppearance = AttachedAppearance(subject, leg);
        Damage(subject, leg); yield return null;
        Require(leg.IsDetached && shin.IsDetached && subject.IsCrawling && !subject.IsDead && subject.HitsTaken == 1,
            "An armored thigh hit still detaches the complete lower chain and selects surviving crawl locomotion.");
        foreach (Renderer renderer in legAppearance) Require(renderer == null || !renderer.transform.IsChildOf(subject.transform), "Leg armor leaves the actor with the severed physical anatomy.");
        yield return Recovered(subject);
        Require(subject.IsCrawling && subject.motor.enabled && Mathf.Abs(subject.motor.height - 1.1f) < .001f && !leg.transform.IsChildOf(subject.transform),
            "The armored survivor recovers into the original supported crawl capsule without regrowing the leg.");
        yield return CaptureActor("08_swat_leg_loss_crawl", subject, false); CheckPool(subject);
        yield return DisposeCase();
        Destroy(stage); stage = null; if (floorMaterial != null) Destroy(floorMaterial);
        previous = session; session.StartSandbox(); yield return WaitForSession(previous, FacilityGameMode.Sandbox);
        Require(session.spawner != null && session.spawner.observerPrefab != null && session.spawner.zombiePrefab != null,
            "Sandbox keeps both actual saved spawn sources.");
        Inspect(session.spawner.observerPrefab.GetComponent<FacilityHumanoid>(), FacilityCharacterStyle.Security, "Saved sandbox observer");
        Inspect(session.spawner.zombiePrefab.GetComponent<FacilityHumanoid>(), FacilityCharacterStyle.Infected, "Saved sandbox infected");
        yield return Spawn(FacilitySpawnKind.Observer);
        FacilityHumanoid observer = session.spawner.LastSpawned.GetComponent<FacilityHumanoid>();
        Inspect(observer, FacilityCharacterStyle.Security, "Actually spawned observer");
        FacilityKnifeGuard knife = observer.GetComponent<FacilityKnifeGuard>();
        Require(knife != null && !knife.Alerted && knife.knifeProp != null && knife.knifeProp.GetComponentInParent<FacilityHitRegion>(true) != null,
            "The restyled sandbox observer retains its passive knife AI and severable weapon.");
        yield return CaptureActor("09_sandbox_observer", observer, false);
        yield return Spawn(FacilitySpawnKind.Zombie);
        FacilityHumanoid infected = session.spawner.LastSpawned.GetComponent<FacilityHumanoid>();
        Inspect(infected, FacilityCharacterStyle.Infected, "Actually spawned infected");
        FacilityZombie zombie = infected.GetComponent<FacilityZombie>();
        Require(zombie != null && infected.IsHostile && Mathf.Abs(zombie.walkSpeed - .95f) < .001f && zombie.targetHealth == session.health &&
            zombie.moanSource != null && zombie.moanClips != null && zombie.moanClips.Length > 0,
            "The restyled infected retains hostile movement, original speed, real target and positional moan audio.");
        zombie.enabled = false; infected.enabled = false;
        yield return CaptureActor("10_sandbox_infected", infected, false);
        Require(FacilityEconomy.Cash == 0, "Appearance and gore diagnostics cannot award or spend the saved wallet.");
    }

    void Inspect(FacilityHumanoid actor, FacilityCharacterStyle expected, string label)
    {
        Require(actor != null && actor.motor != null && actor.head != null && actor.chest != null && actor.pelvis != null && actor.gore != null,
            label + " preserves its original motor, anatomical anchors and gore pool.");
        FacilityCharacterAppearance appearance = actor.GetComponent<FacilityCharacterAppearance>();
        Require(appearance != null && appearance.style == expected && appearance.revision >= 1 && appearance.appearanceRenderers != null && appearance.appearanceRenderers.Length > 0,
            label + " uses the saved, versioned " + expected + " appearance.");
        Require(actor.hitRegions.Length == 11 && actor.bodies.Length == 11 && actor.bodyColliders.Length == 11 &&
            actor.GetComponentsInChildren<FacilityHitRegion>(true).Length == 11 && actor.GetComponentsInChildren<Rigidbody>(true).Length == 11,
            label + " retains exactly eleven original physical damage regions and bodies.");
        var kinds = new HashSet<FacilityBodyRegion>();
        foreach (FacilityHitRegion region in actor.hitRegions)
        {
            Require(region != null && kinds.Add(region.kind) && region.body != null && region.owner == actor && region.GetComponent<Collider>() != null &&
                region.outerPatches != null && region.woundBeds != null && region.outerPatches.Length == region.woundBeds.Length,
                label + " has an intact uniquely bound region and aligned damage layers: " + (region != null ? region.kind.ToString() : "missing"));
            int expectedPatches = region.kind == FacilityBodyRegion.Head ? 4 : region.kind == FacilityBodyRegion.Torso ? 14 : 6;
            Require(region.outerPatches.Length == expectedPatches && Array.TrueForAll(region.outerPatches, patch => patch != null && patch.transform.IsChildOf(region.transform)),
                label + " preserves the original removable-patch count and regional attachment: " + region.kind);
            if (region.parentRegion != null) Require(region.joint != null && region.joint.connectedBody == region.parentRegion.body,
                label + " preserves the connected dismemberment joint: " + region.kind);
            receipt.regionsInspected++;
        }
        var materials = new HashSet<Material>();
        foreach (Renderer renderer in appearance.appearanceRenderers)
        {
            Require(renderer != null && renderer.transform.IsChildOf(actor.transform), label + " references real attached appearance geometry.");
            MeshFilter mesh = renderer.GetComponent<MeshFilter>();
            Require(mesh != null && mesh.sharedMesh != null && mesh.sharedMesh.vertexCount > 0, label + " appearance uses actual saved polygonal meshes.");
            FacilityHitRegion region = renderer.GetComponentInParent<FacilityHitRegion>(true);
            Require(region != null || (actor.contactShadow != null && renderer.transform.IsChildOf(actor.contactShadow)),
                label + " appearance follows a real anatomical region so it can detach or disappear with that body part: " + renderer.name);
            foreach (Material material in renderer.sharedMaterials)
            {
                Require(material != null && material.shader != null && material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader",
                    label + " appearance materials render correctly in the actual game pipeline."); materials.Add(material);
            }
            receipt.appearanceRenderersInspected++;
        }
        Require(materials.Count >= 2, label + " retains distinct actual material surfaces.");
        if (expected == FacilityCharacterStyle.Swat)
        {
            receipt.swatTorsoMesh = Region(actor, FacilityBodyRegion.Torso).outerPatches[0].GetComponent<MeshFilter>().sharedMesh.name;
            bool compositeArmor = false;
            foreach (Renderer patch in Region(actor, FacilityBodyRegion.Torso).outerPatches)
                if (patch.sharedMaterials.Length > 1) compositeArmor = true;
            Require(compositeArmor, label + " places its armor materials inside the actual removable torso patches.");
        }
        receipt.actorsInspected++;
    }
    void BuildStage()
    {
        stage = new GameObject("Character diagnostic physical stage - runtime only");
        floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); floorMaterial.SetColor("_BaseColor", new Color(.12f, .14f, .16f));
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Supported physical ragdoll floor"; floor.transform.SetParent(stage.transform);
        floor.transform.position = new Vector3(100f, -.1f, 108f); floor.transform.localScale = new Vector3(48f, .2f, 48f); floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        StageLight(new Vector3(97f, 4f, 103f), new Color(1f, .88f, .75f), 9f);
        StageLight(new Vector3(104f, 3.5f, 109f), new Color(.55f, .72f, 1f), 7f);
        Teleport(new Vector3(100f, .05f, 100f), stageOrigin + Vector3.up * 1.1f);
    }
    void StageLight(Vector3 position, Color color, float intensity)
    {
        var node = new GameObject("Temporary character photograph light"); node.transform.SetParent(stage.transform); node.transform.position = position;
        Light light = node.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = 18f; light.shadows = LightShadows.Soft;
    }
    IEnumerator NewPolice(string label)
    {
        caseRoot = new GameObject("Fresh SWAT damage case - " + label); caseRoot.transform.SetParent(stage.transform);
        GameObject clone = Instantiate(policePrefab, caseRoot.transform, false); clone.SetActive(false);
        clone.transform.SetPositionAndRotation(stageOrigin, Quaternion.Euler(0f, 180f, 0f));
        subject = clone.GetComponent<FacilityHumanoid>(); subject.player = player.transform;
        var officer = clone.GetComponent<FacilityPoliceOfficer>(); officer.actor = subject; officer.target = session.health; officer.projectileSystem = projectiles;
        var ignored = new Collider[subject.bodyColliders.Length + 2]; Array.Copy(subject.bodyColliders, ignored, subject.bodyColliders.Length);
        ignored[ignored.Length - 2] = subject.motor; ignored[ignored.Length - 1] = motor; subject.gore.ownerColliders = ignored;
        foreach (SubtleEnemyHealthBar bar in clone.GetComponentsInChildren<SubtleEnemyHealthBar>(true)) bar.player = player.transform;
        session.health.ResetHealth(); projectiles.Clear(); Teleport(new Vector3(100f, .05f, 100f), stageOrigin + Vector3.up * 1.1f);
        clone.SetActive(true); Physics.SyncTransforms(); yield return null;
        Require(!subject.IsDead && subject.HitsTaken == 0 && subject.SeveredLimbCount == 0 && !subject.HeadExploded, "Each destructive anatomy case begins with a fresh undamaged saved SWAT clone.");
    }
    IEnumerator DisposeCase()
    { projectiles.Clear(); if (caseRoot != null) Destroy(caseRoot); caseRoot = null; subject = null; yield return null; yield return null; }
    static FacilityHitRegion Region(FacilityHumanoid actor, FacilityBodyRegion kind)
    { foreach (FacilityHitRegion region in actor.hitRegions) if (region.kind == kind) return region; throw new InvalidOperationException("Missing anatomical region " + kind); }
    static Renderer[] AttachedAppearance(FacilityHumanoid actor, FacilityHitRegion region)
    {
        var result = new List<Renderer>();
        foreach (Renderer renderer in actor.GetComponent<FacilityCharacterAppearance>().appearanceRenderers)
            if (renderer != null && renderer.GetComponentInParent<FacilityHitRegion>(true) == region) result.Add(renderer);
        return result.ToArray();
    }
    void Damage(FacilityHumanoid actor, FacilityHitRegion region)
    {
        Collider collider = region.GetComponent<Collider>(); Require(collider != null && collider.enabled, "Regional damage uses the existing enabled physical anatomical collider.");
        Vector3 point = collider.bounds.center;
        foreach (Renderer patch in region.outerPatches) if (patch != null && patch.enabled) { point = patch.bounds.center; break; }
        actor.ReceivePellet(++shotId, point, Vector3.forward, collider); Physics.SyncTransforms();
    }
    static int DisabledPatches(FacilityHitRegion region)
    { int count = 0; foreach (Renderer patch in region.outerPatches) if (patch != null && !patch.enabled) count++; return count; }
    IEnumerator Recovered(FacilityHumanoid actor)
    {
        float until = Time.realtimeSinceStartup + 8f;
        while ((actor.IsKnockedDown || actor.IsRecovering) && Time.realtimeSinceStartup < until) yield return new WaitForFixedUpdate();
        Require(!actor.IsDead && !actor.IsKnockedDown && !actor.IsRecovering && actor.motor.enabled && actor.RecoveryCount > 0,
            "A damaged armored survivor physically recovers on its real supported ragdoll floor.");
    }
    void CheckPool(FacilityHumanoid actor)
    {
        Require(actor.gore.TotalDropsEmitted > 0 && actor.gore.ActiveDroplets <= actor.gore.DropletCapacity && actor.gore.ActiveChunks <= actor.gore.ChunkCapacity &&
            actor.gore.ActiveStains <= actor.gore.StainCapacity && actor.gore.DropletCapacity == 220 && actor.gore.ChunkCapacity == 36 && actor.gore.StainCapacity == 96,
            "Restyled anatomy retains physical blood/tissue output and the original bounded gore capacities.");
    }
    IEnumerator Spawn(FacilitySpawnKind kind)
    {
        session.ToggleSpawnMenu(); yield return null; session.SelectCharacter(kind); yield return null;
        bool placed = false; string reason = "No candidates";
        for (int ring = 0; ring < 4 && !placed; ring++) for (int i = 0; i < 12 && !placed; i++)
        {
            float angle = i * Mathf.PI / 6f; Vector3 candidate = player.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (2.5f + ring);
            candidate.y = .05f; placed = session.spawner.TryPlaceSelected(candidate, out reason);
        }
        Require(placed && session.spawner.LastSpawned != null, "The actual sandbox spawn menu places its saved " + kind + " prefab: " + reason);
        session.CancelPlacement(); yield return null;
    }
    IEnumerator WaitForSession(FacilityGameSession previous, FacilityGameMode mode)
    {
        float until = Time.realtimeSinceStartup + 15f;
        while ((FacilityGameSession.Instance == null || FacilityGameSession.Instance == previous) && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(.3f); session = FacilityGameSession.Instance;
        Require(session != null && session != previous && session.CurrentMode == mode && session.CurrentState == FacilityGameState.Playing, "The real " + mode + " scene loads its saved session.");
        player = session.player; motor = player.GetComponent<CharacterController>(); eyes = player.GetComponentInChildren<Camera>(); player.enabled = false;
    }
    void Teleport(Vector3 feet, Vector3 aim)
    { motor.enabled = false; player.transform.position = feet; motor.enabled = true; player.ResetRecoil(); player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(aim - eyes.transform.position, Vector3.up)); eyes.transform.rotation = Quaternion.LookRotation(aim - eyes.transform.position); Physics.SyncTransforms(); }
    IEnumerator CaptureActor(string name, FacilityHumanoid actor, bool close)
    {
        yield return new WaitForEndOfFrame();
        Vector3 centre = close && !actor.HeadExploded && actor.head != null ? actor.head.position : actor.transform.position + Vector3.up * (actor.IsCrawling ? .6f : 1f);
        if (name.Contains("torso")) centre = actor.chest.position;
        Vector3 forward = actor.transform.forward;
        Vector3 eye = centre + forward * (close ? 1.15f : 3.3f) + actor.transform.right * (close ? .15f : .8f) + Vector3.up * (close ? .025f : .22f);
        if (actor.IsKnockedDown || actor.IsDead) { centre = actor.chest.position; eye = centre + new Vector3(1.5f, 2f, -2.2f); }
        var node = new GameObject("Temporary actual-character camera"); Camera camera = node.AddComponent<Camera>(); camera.CopyFrom(eyes); camera.enabled = false;
        camera.fieldOfView = close ? 38f : 37f; camera.aspect = 1.6f; camera.nearClipPlane = .035f; camera.allowHDR = true;
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(centre - eye));
        var data = camera.GetUniversalAdditionalCameraData(); data.renderShadows = true; data.renderPostProcessing = false;
        Transform[] weapons = { session.inventory.fistsModel, session.inventory.pistolModel, session.inventory.shotgunModel, session.inventory.ak47Model };
        var shown = new bool[weapons.Length]; bool canvasEnabled = session.canvas.enabled; RenderTexture active = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            session.canvas.enabled = false; for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) { shown[i] = weapons[i].gameObject.activeSelf; weapons[i].gameObject.SetActive(false); }
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG()); receipt.pictures++;
        }
        finally
        { session.canvas.enabled = canvasEnabled; for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) weapons[i].gameObject.SetActive(shown[i]); RenderTexture.active = active; Destroy(pixels); target.Release(); Destroy(target); Destroy(node); }
    }
    void Require(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); receipt.checks.Add(description); }
    void OnLog(string message, string trace, LogType kind)
    { if (collecting && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace); }
}
