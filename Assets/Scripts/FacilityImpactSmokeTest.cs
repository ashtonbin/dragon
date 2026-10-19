using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in standalone integration check. Never runs without -facilityImpactTest "output directory".</summary>
public sealed class FacilityImpactSmokeTest : MonoBehaviour
{
    [Serializable] private sealed class Case
    {
        public string name;
        public int hits, recoveries, severedRegions, droplets, chunks, stains, bloodStreaksEmitted, tissueChunksEmitted, headBursts, severBursts;
        public bool fired, knockedDown, airborne, recovered, grounded, dead, crawling, headExploded, severed, jointRemoved, descendantDetached;
        public float backwardSpeed, upwardSpeed, recoverySeconds;
    }

    [Serializable] private sealed class Receipt
    {
        public bool success, untouchedObserverPassive, muzzleFlashVisible, muzzleLightEnabled, smokeVisible, twelveDistinctPellets,
            flightDelayed, wallBlocked, multipleWallHoles, marksAttached, boundedPools, corpseShotSafe;
        public string utc, unity, cpu, gpu, scene, wallCollider;
        public string coverage = "Actual twelve-pellet weapon flight, near-muzzle obstruction, visible flash/recoil, static surface scars, two nonlethal knockdowns and floor-safe recoveries, three torso-shell death, a separate head explosion, and all eight limb regions on fresh runtime-only character clones. The authored scene and player settings are never saved by this check.";
        public string timingMeaning = "Uncapped frame intervals on the reported hardware during this scripted check. Screenshot frames are excluded. This is not a lower-end hardware certification.";
        public int width, height, shots, pelletsLaunched, pelletImpacts, wallHoleDelta, activeHoles, holeCapacity,
            dropCapacity, chunkCapacity, stainCapacity, frameSamples, lightmaps;
        public float maxPelletSeparationDegrees, cameraKickDegrees, shoulderTravel, weaponKickDegrees, wallHoleSeparation;
        public double averageFrameMs, p95FrameMs;
        public List<Case> cases = new List<Case>();
        public List<string> failures = new List<string>();
    }

    private readonly Receipt receipt = new Receipt();
    private readonly List<double> frameTimes = new List<double>(16000);
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[64];
    private RetroFPSController player;
    private CharacterController playerMotor;
    private Camera eye;
    private FacilityShotgun gun;
    private FacilityHumanoid template, subject;
    private Vector3 authoredPosition, firingFeet;
    private Quaternion authoredRotation;
    private float originalEyeHeight;
    private string output;
    private bool collectingErrors, collectingFrames;
    private int skipFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(arguments, "-facilityImpactTest");
        // The old flag is an alias: its obsolete instant-hit/automatic-forearm assumptions are retired.
        if (flag < 0) flag = Array.IndexOf(arguments, "-facilityCombatTest");
        if (flag < 0) return;
        if (flag + 1 >= arguments.Length || arguments[flag + 1].StartsWith("-", StringComparison.Ordinal))
        {
            Debug.LogError("-facilityImpactTest requires an output directory.");
            Application.Quit(2);
            return;
        }
        var runner = new GameObject("Facility impact diagnostics (explicit CLI only)").AddComponent<FacilityImpactSmokeTest>();
        runner.output = arguments[flag + 1];
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        Application.logMessageReceived += OnLog;
        collectingErrors = true;
        // Flatten nested routines here so an exception always produces a receipt and a failing exit code.
        var stack = new Stack<IEnumerator>();
        stack.Push(Run());
        while (stack.Count > 0)
        {
            IEnumerator routine = stack.Peek();
            bool more;
            object current = null;
            try { more = routine.MoveNext(); if (more) current = routine.Current; }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
        collectingFrames = collectingErrors = false;
        Application.logMessageReceived -= OnLog;
        try
        {
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            receipt.frameSamples = frameTimes.Count;
            Statistics(frameTimes, out receipt.averageFrameMs, out receipt.p95FrameMs);
            receipt.success = receipt.failures.Count == 0;
            File.WriteAllText(Path.Combine(output, "impact-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Facility impact integration check " + (receipt.success ? "passed: " : "failed: ") + output);
        }
        catch (Exception exception) { Debug.LogException(exception); Application.Quit(2); yield break; }
        Application.Quit(receipt.success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        Application.runInBackground = true;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(.5f);
        player = FindFirstObjectByType<RetroFPSController>();
        gun = FindFirstObjectByType<FacilityShotgun>();
        template = FindFirstObjectByType<FacilityHumanoid>();
        if (player == null || gun == null || template == null) throw new InvalidOperationException("The authored player, shotgun and observer are required.");
        player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playerMotor = player.GetComponent<CharacterController>();
        eye = player.GetComponentInChildren<Camera>();
        if (playerMotor == null || eye == null || gun.aimCamera != eye || gun.surfaceImpacts == null)
            throw new InvalidOperationException("Missing original player capsule/camera or explicitly assigned surface-impact component.");
        if (template.HitsTaken != 0 || template.IsDead) throw new InvalidOperationException("The authored observer must start untouched.");
        originalEyeHeight = eye.transform.position.y - player.transform.position.y;
        authoredPosition = template.transform.position;
        authoredRotation = template.transform.rotation;
        receipt.utc = DateTime.UtcNow.ToString("O"); receipt.unity = Application.unityVersion;
        receipt.cpu = SystemInfo.processorType; receipt.gpu = SystemInfo.graphicsDeviceName;
        receipt.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        receipt.width = Screen.width; receipt.height = Screen.height;
        receipt.lightmaps = LightmapSettings.lightmaps.Length;
        Check(receipt.width == 1280 && receipt.height == 720, "Window did not reach 1280 x 720.");
        foreach (Renderer item in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (Material material in item.sharedMaterials)
                Check(material != null && material.shader != null && material.shader.isSupported && !material.shader.name.Contains("InternalErrorShader"), "Missing/unsupported material on " + item.name + ".");
        firingFeet = SelectClearFiringPose(template);
        PositionPlayer(firingFeet);
        Aim(template.AimPoint);
        Vector3 passivePosition = template.transform.position;
        yield return new WaitForSecondsRealtime(.5f);
        receipt.untouchedObserverPassive = template.HitsTaken == 0 && !template.IsDead && Vector3.Distance(passivePosition, template.transform.position) < .08f;
        Check(receipt.untouchedObserverPassive, "Untouched observer moved or took damage before an attack.");
        Capture("01_untouched_observer");
        // Keep an untouched, inactive authored source. Every destructive scenario gets a fresh runtime-only clone.
        template.gameObject.SetActive(false);
        collectingFrames = true;
        yield return NewSubject("Torso knockdown and recovery");
        yield return WallScenario();
        yield return TorsoScenario();
        yield return DisposeSubject();
        yield return NewSubject("Head explosion");
        yield return HeadScenario();
        yield return DisposeSubject();
        FacilityBodyRegion[] limbs = { FacilityBodyRegion.UpperArmLeft, FacilityBodyRegion.ForearmLeft, FacilityBodyRegion.UpperArmRight,
            FacilityBodyRegion.ForearmRight, FacilityBodyRegion.ThighLeft, FacilityBodyRegion.ShinLeft, FacilityBodyRegion.ThighRight, FacilityBodyRegion.ShinRight };
        foreach (FacilityBodyRegion limb in limbs)
        {
            yield return NewSubject(limb.ToString());
            yield return LimbScenario(limb);
            yield return DisposeSubject();
        }
        receipt.shots = gun.ShotsFired; receipt.pelletsLaunched = gun.PelletsLaunched; receipt.pelletImpacts = gun.PelletImpactCount;
        receipt.activeHoles = gun.surfaceImpacts.ActiveHoles; receipt.holeCapacity = gun.surfaceImpacts.Capacity;
        Check(receipt.pelletsLaunched == receipt.shots * 12, "Every accepted trigger press must launch exactly twelve pellets.");
        Check(gun.ActivePellets == 0, "Pellets survived beyond their flight lifetime.");
        receipt.marksAttached = gun.surfaceImpacts.ValidateAttachment(out string attachmentReason);
        Check(receipt.marksAttached, "A bullet scar lost its support surface: " + attachmentReason);
        Check(receipt.activeHoles <= receipt.holeCapacity, "Surface-impact count exceeded its fixed capacity.");
        yield return new WaitForSecondsRealtime(.15f);
    }

    private IEnumerator NewSubject(string label)
    {
        GameObject clone = Instantiate(template.gameObject, authoredPosition, authoredRotation, template.transform.parent);
        clone.name = "Diagnostic observer - " + label;
        subject = clone.GetComponent<FacilityHumanoid>();
        clone.SetActive(true);
        PositionPlayer(firingFeet);
        eye.transform.localPosition = new Vector3(0f, originalEyeHeight, 0f);
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        Check(subject.HitsTaken == 0 && !subject.IsDead && !subject.IsKnockedDown && subject.SeveredLimbCount == 0,
            "Fresh scenario clone inherited damaged runtime state: " + label + ".");
    }

    private IEnumerator DisposeSubject()
    {
        if (subject != null)
        {
            // Severed bodies intentionally leave the animated hierarchy. Dispose those test-only objects explicitly.
            foreach (FacilityHitRegion region in subject.hitRegions)
                if (region != null && !region.transform.IsChildOf(subject.transform)) Destroy(region.gameObject);
            Destroy(subject.gameObject);
        }
        subject = null;
        yield return null;
        yield return null;
    }

    private IEnumerator WallScenario()
    {
        Vector3 cameraPosition = eye.transform.position;
        Quaternion cameraRotation = eye.transform.rotation;
        Collider wall = FindOccludingPose(subject, out Vector3 blockedEye);
        receipt.wallCollider = wall.name;
        SetEyePose(blockedEye, Quaternion.LookRotation(subject.AimPoint - blockedEye, Vector3.up));
        Physics.SyncTransforms();
        int oldHoles = gun.surfaceImpacts.TotalImpacts;
        yield return Cooldown();
        bool fired = gun.TryFire();
        Check(fired, "Wall discharge was rejected after cooldown.");
        yield return new WaitForSecondsRealtime(.38f);
        receipt.wallBlocked = subject.HitsTaken == 0 && !subject.IsDead;
        receipt.wallHoleDelta = gun.surfaceImpacts.TotalImpacts - oldHoles;
        receipt.wallHoleSeparation = HoleSeparation();
        receipt.multipleWallHoles = receipt.wallHoleDelta > 1 && receipt.wallHoleSeparation > .01f;
        Check(receipt.wallBlocked, "Pellets passed through a verified static wall into the observer.");
        Check(receipt.multipleWallHoles, "One twelve-pellet wall shot did not leave multiple separated scars.");
        Capture("02_wall_buckshot_pattern");
        SetEyePose(cameraPosition, cameraRotation);
        Physics.SyncTransforms();
    }

    private IEnumerator TorsoScenario()
    {
        for (int shot = 1; shot <= 3; shot++)
        {
            yield return Cooldown();
            // Knockback moves the actor several metres. Re-establish a genuinely clear close firing pose,
            // rather than letting a widened distant cone turn the next torso check into a limb/head test.
            firingFeet = SelectClearFiringPose(subject);
            PositionPlayer(firingFeet);
            eye.transform.localPosition = new Vector3(0f, originalEyeHeight, 0f);
            Aim(subject.AimPoint);
            Physics.SyncTransforms();
            var record = new Case { name = "Torso discharge " + shot };
            receipt.cases.Add(record);
            int hitsBefore = subject.HitsTaken;
            int pelletsBefore = gun.PelletsLaunched;
            Vector3 direction = eye.transform.forward;
            Vector3 weaponPosition = gun.viewmodel != null && gun.viewmodel.authoredModel != null ? gun.viewmodel.authoredModel.localPosition : Vector3.zero;
            Quaternion weaponRotation = gun.viewmodel != null && gun.viewmodel.authoredModel != null ? gun.viewmodel.authoredModel.localRotation : Quaternion.identity;
            Quaternion cameraRotation = eye.transform.rotation;
            record.fired = gun.TryFire();
            Check(record.fired, record.name + " was rejected.");
            if (shot == 1)
            {
                receipt.flightDelayed = subject.HitsTaken == hitsBefore && gun.ActivePellets == 12;
                receipt.muzzleFlashVisible = gun.muzzleFlash != null && gun.muzzleFlash.activeInHierarchy;
                receipt.muzzleLightEnabled = gun.muzzleLight != null && gun.muzzleLight.enabled;
                receipt.smokeVisible = gun.muzzleSmoke != null && gun.muzzleSmoke.gameObject.activeInHierarchy;
                receipt.cameraKickDegrees = Quaternion.Angle(cameraRotation, eye.transform.rotation);
                receipt.twelveDistinctPellets = gun.PelletsLaunched - pelletsBefore == 12 && DistinctDirections(gun.LastShotDirections, out receipt.maxPelletSeparationDegrees);
                Check(receipt.flightDelayed, "Damage happened at trigger time instead of after pellet flight.");
                Check(receipt.twelveDistinctPellets, "The shell did not launch twelve distinct directions across a cone.");
                Check(receipt.muzzleFlashVisible && receipt.muzzleLightEnabled && receipt.smokeVisible, "Shot lacks visible muzzle flash, transient flash light, or smoke.");
                Check(receipt.cameraKickDegrees >= 2f, "Immediate camera kick was less than two degrees.");
                Capture("03_muzzle_flash");
            }
            yield return new WaitForSecondsRealtime(.08f);
            if (shot == 1 && gun.viewmodel != null && gun.viewmodel.authoredModel != null)
            {
                receipt.shoulderTravel = Vector3.Distance(weaponPosition, gun.viewmodel.authoredModel.localPosition);
                receipt.weaponKickDegrees = Quaternion.Angle(weaponRotation, gun.viewmodel.authoredModel.localRotation);
                Check(receipt.shoulderTravel >= .035f && receipt.weaponKickDegrees >= 3f, "The authored shotgun did not kick back and rotate visibly.");
            }
            yield return ReadFlightAndMomentum(record, direction, .14f);
            Fill(record);
            Check(record.hits == shot, record.name + " did not count once per shell; actual hits " + record.hits + ".");
            Check(record.dead == (shot == 3), "Torso health must die on the third shell.");
            Check(!subject.HeadExploded, "A center-chest shot unexpectedly exploded the head.");
            Check(record.knockedDown && record.airborne && record.backwardSpeed > .5f, record.name + " did not create a backward airborne ragdoll.");
            Capture("04_torso_" + shot + "_knockdown");
            CheckPool(record);
            if (shot < 3)
            {
                yield return WaitForRecovery(record);
                Fill(record);
                Check(record.recovered && record.grounded && !record.dead && !record.crawling, record.name + " did not recover to a supported standing capsule.");
                Check(subject.RecoveryCount >= shot, "Recovery counter did not advance after " + record.name + ".");
                Aim(subject.AimPoint);
                Capture("05_torso_" + shot + "_standing_again");
            }
        }
        int hitsBeforeCorpse = subject.HitsTaken;
        yield return Cooldown();
        Aim(subject.pelvis.position);
        int dropsBeforeCorpse = subject.gore.TotalDropsEmitted;
        bool corpseFired = gun.TryFire();
        yield return new WaitForSecondsRealtime(.35f);
        receipt.corpseShotSafe = corpseFired && subject.IsDead && subject.HitsTaken == hitsBeforeCorpse && subject.gore.TotalDropsEmitted > dropsBeforeCorpse;
        Check(receipt.corpseShotSafe, "Corpse shooting changed completed three-hit health or failed after cooldown.");
        Capture("06_torso_corpse");
    }

    private IEnumerator HeadScenario()
    {
        FacilityHitRegion region = Region(FacilityBodyRegion.Head);
        yield return Cooldown();
        RegionalAim(region);
        var record = new Case { name = "Head aimed shell", fired = gun.TryFire() };
        receipt.cases.Add(record);
        yield return new WaitForSecondsRealtime(.14f);
        Fill(record);
        Check(record.fired && record.dead && record.headExploded, "A verified head-aimed shell did not explode the head immediately.");
        Check(record.headBursts == 1, "The head explosion did not invoke its dedicated fragment burst exactly once.");
        foreach (Renderer item in subject.head.GetComponentsInChildren<Renderer>(true))
            Check(!item.enabled || !item.gameObject.activeInHierarchy, "Head renderer remained visible after explosion: " + item.name + ".");
        foreach (Collider item in subject.head.GetComponentsInChildren<Collider>(true))
            Check(!item.enabled || !item.gameObject.activeInHierarchy, "Exploded head still has a live collider.");
        CheckPool(record);
        Capture("07_head_explosion");
        yield return new WaitForSecondsRealtime(.25f);
    }

    private IEnumerator LimbScenario(FacilityBodyRegion kind)
    {
        FacilityHitRegion region = Region(kind);
        yield return Cooldown();
        RegionalAim(region);
        Vector3 direction = eye.transform.forward;
        var record = new Case { name = kind.ToString(), fired = gun.TryFire() };
        receipt.cases.Add(record);
        yield return ReadFlightAndMomentum(record, direction, .22f);
        record.severed = subject.IsRegionSevered(region);
        record.jointRemoved = region.JointSevered && region.GetComponent<CharacterJoint>() == null;
        record.descendantDetached = true;
        FacilityBodyRegion child = kind;
        if (kind == FacilityBodyRegion.UpperArmLeft) child = FacilityBodyRegion.ForearmLeft;
        if (kind == FacilityBodyRegion.UpperArmRight) child = FacilityBodyRegion.ForearmRight;
        if (kind == FacilityBodyRegion.ThighLeft) child = FacilityBodyRegion.ShinLeft;
        if (kind == FacilityBodyRegion.ThighRight) child = FacilityBodyRegion.ShinRight;
        if (child != kind) record.descendantDetached = subject.IsRegionSevered(Region(child));
        Fill(record);
        Check(record.fired && record.severed && record.jointRemoved && record.descendantDetached, "Aimed limb shell did not sever its root joint and dependent chain: " + kind + ".");
        Check(!record.dead && !record.headExploded, "A first-shell limb hit must remain recoverable: " + kind + ".");
        Check(record.knockedDown && record.airborne, "Limb shell did not knock the observer off its feet: " + kind + ".");
        CheckPool(record);
        Capture("08_" + kind + "_sever");
        yield return WaitForRecovery(record);
        Fill(record);
        bool leg = kind == FacilityBodyRegion.ThighLeft || kind == FacilityBodyRegion.ThighRight || kind == FacilityBodyRegion.ShinLeft || kind == FacilityBodyRegion.ShinRight;
        Check(record.recovered && record.grounded && !record.dead, "Severed survivor did not recover on a supported floor: " + kind + ".");
        Check(record.crawling == leg, "Recovery locomotion did not reflect intact versus severed legs: " + kind + ".");
        Check(subject.IsRegionSevered(region) && !region.transform.IsChildOf(subject.transform), "Recovery reattached or regrew a severed limb: " + kind + ".");
        Aim(subject.AimPoint);
        Capture("09_" + kind + "_" + (leg ? "crawl" : "standing"));
    }

    private IEnumerator ReadFlightAndMomentum(Case record, Vector3 direction, float duration)
    {
        float deadline = Time.realtimeSinceStartup + duration;
        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        do
        {
            if (subject != null)
            {
                record.knockedDown |= subject.IsKnockedDown || (subject.IsDead && !subject.motor.enabled);
                foreach (Rigidbody body in subject.bodies)
                {
                    if (body == null || body.isKinematic) continue;
                    // The pelvis/chest must leave their feet, rather than merely a tiny flying fragment.
                    if (body.transform != subject.pelvis && body.transform != subject.chest) continue;
                    record.backwardSpeed = Mathf.Max(record.backwardSpeed, Vector3.Dot(body.linearVelocity, horizontal));
                    record.upwardSpeed = Mathf.Max(record.upwardSpeed, body.linearVelocity.y);
                    record.airborne |= body.linearVelocity.y > .5f;
                }
            }
            yield return new WaitForFixedUpdate();
        } while (Time.realtimeSinceStartup < deadline);
    }

    private IEnumerator WaitForRecovery(Case record)
    {
        float start = Time.realtimeSinceStartup;
        while (subject != null && !subject.IsDead && (subject.IsKnockedDown || subject.IsRecovering) && Time.realtimeSinceStartup - start < 7f)
            yield return new WaitForFixedUpdate();
        record.recoverySeconds = Time.realtimeSinceStartup - start;
        yield return new WaitForSecondsRealtime(.12f);
        record.recovered = !subject.IsDead && !subject.IsKnockedDown && !subject.IsRecovering && subject.motor.enabled && subject.RecoveryCount > 0;
        record.grounded = record.recovered && SupportedCapsule(subject.motor);
    }

    private void Fill(Case record)
    {
        record.hits = subject.HitsTaken; record.recoveries = subject.RecoveryCount;
        record.severedRegions = subject.SeveredLimbCount; record.dead = subject.IsDead;
        record.crawling = subject.IsCrawling; record.headExploded = subject.HeadExploded;
        record.droplets = subject.gore.ActiveDroplets; record.chunks = subject.gore.ActiveChunks; record.stains = subject.gore.ActiveStains;
        record.bloodStreaksEmitted = subject.gore.TotalDropsEmitted; record.tissueChunksEmitted = subject.gore.TotalChunksEmitted;
        record.headBursts = subject.gore.HeadBurstCount; record.severBursts = subject.gore.SeverBurstCount;
    }

    private void CheckPool(Case record)
    {
        FacilityGorePool gore = subject.gore;
        receipt.dropCapacity = gore.DropletCapacity; receipt.chunkCapacity = gore.ChunkCapacity; receipt.stainCapacity = gore.StainCapacity;
        bool bounded = gore.ActiveDroplets <= gore.DropletCapacity && gore.ActiveChunks <= gore.ChunkCapacity && gore.ActiveStains <= gore.StainCapacity;
        receipt.boundedPools = receipt.cases.Count == 1 ? bounded : receipt.boundedPools && bounded;
        Check(bounded, "Gore pool exceeded its fixed capacity after " + record.name + ".");
        Check(record.droplets > 0 && record.chunks > 0, "Shot produced no active spray or physical chunks: " + record.name + ".");
    }

    private IEnumerator Cooldown()
    {
        // The longest preceding scenario may have already consumed the cooldown. This fixed margin keeps every shot deliberate.
        yield return new WaitForSecondsRealtime(gun.FireInterval + .04f);
    }

    private FacilityHitRegion Region(FacilityBodyRegion kind)
    {
        foreach (FacilityHitRegion item in subject.hitRegions) if (item != null && item.kind == kind) return item;
        throw new InvalidOperationException("Authored region was not assigned: " + kind + ".");
    }

    private void RegionalAim(FacilityHitRegion region)
    {
        Collider collider = region.GetComponent<Collider>();
        if (collider == null || !collider.enabled) throw new InvalidOperationException("No enabled body collider for " + region.kind + ".");
        Vector3 point = collider.bounds.center;
        Vector3 front = subject.transform.forward;
        Vector3[] approaches = { front, (front + subject.transform.right * .65f).normalized, (front - subject.transform.right * .65f).normalized };
        foreach (Vector3 approach in approaches)
        {
            Vector3 candidate = point + approach * 1.65f + Vector3.up * .12f;
            Vector3 ray = point - candidate;
            if (!FirstWeaponHit(candidate, ray.normalized, ray.magnitude + .02f, out RaycastHit first)) continue;
            if (first.collider != collider && first.collider.GetComponentInParent<FacilityHitRegion>() != region) continue;
            SetEyePose(candidate, Quaternion.LookRotation(ray, Vector3.up));
            Physics.SyncTransforms();
            return;
        }
        throw new InvalidOperationException("No unobstructed direct actual-weapon sightline to " + region.kind + ".");
    }

    private bool FirstWeaponHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit first)
    {
        // Match the weapon: locomotion capsules are not anatomical targets or bulletproof cover.
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        first = default;
        float nearest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider is CharacterController || collider.transform.IsChildOf(player.transform) || hits[i].distance >= nearest) continue;
            first = hits[i]; nearest = first.distance; found = true;
        }
        return found;
    }

    private Vector3 SelectClearFiringPose(FacilityHumanoid actor)
    {
        Vector3[] directions = { Vector3.back, Vector3.left, Vector3.right, Vector3.forward, new Vector3(-.6f, 0, -.8f), new Vector3(.6f, 0, -.8f) };
        float[] distances = { 2.5f, 2.75f, 3f, 3.5f, 4f };
        foreach (float distance in distances)
        foreach (Vector3 direction in directions)
        {
            Vector3 candidate = actor.transform.position + direction * distance;
            int count = Physics.RaycastNonAlloc(candidate + Vector3.up * 2f, Vector3.down, hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float floor = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (IsFacilitySurface(hits[i].collider) && hits[i].normal.y > .85f && Mathf.Abs(hits[i].point.y - actor.transform.position.y) < .2f)
                    floor = Mathf.Max(floor, hits[i].point.y);
            if (float.IsNegativeInfinity(floor)) continue;
            candidate.y = floor + .05f;
            float radius = playerMotor.radius - playerMotor.skinWidth * .5f;
            count = Physics.OverlapCapsuleNonAlloc(candidate + Vector3.up * radius, candidate + Vector3.up * (playerMotor.height - radius), radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            bool clear = true;
            for (int i = 0; i < count; i++) if (!overlaps[i].transform.IsChildOf(player.transform)) { clear = false; break; }
            if (!clear) continue;
            Vector3 eyePosition = candidate + Vector3.up * originalEyeHeight;
            Vector3 sight = actor.AimPoint - eyePosition;
            if (Physics.Raycast(eyePosition, sight.normalized, out RaycastHit first, sight.magnitude + .1f, ~0, QueryTriggerInteraction.Ignore) && first.collider.GetComponentInParent<FacilityHumanoid>() == actor)
                return candidate;
        }
        throw new InvalidOperationException("No supported, clear player capsule with a chest sightline within 2.5-4 m.");
    }

    private Collider FindOccludingPose(FacilityHumanoid actor, out Vector3 position)
    {
        Vector3 origin = actor.AimPoint;
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        foreach (Vector3 direction in directions)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider selected = null;
            float nearest = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < count; i++)
                if (IsFacilitySurface(hits[i].collider) && hits[i].distance < nearest)
                { selected = hits[i].collider; point = hits[i].point; nearest = hits[i].distance; }
            if (selected == null) continue;
            Vector3 candidate = point + direction * .65f;
            if ((selected.ClosestPoint(candidate) - candidate).sqrMagnitude < .000001f) continue;
            Vector3 toward = origin - candidate;
            if (Physics.Raycast(candidate, toward.normalized, out RaycastHit first, toward.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && first.collider == selected)
            { position = candidate; return selected; }
        }
        throw new InvalidOperationException("No verified static obstruction can be placed between camera and observer.");
    }

    private bool SupportedCapsule(CharacterController motor)
    {
        Vector3 feet = motor.transform.TransformPoint(motor.center) - Vector3.up * motor.height * .5f;
        int count = Physics.RaycastNonAlloc(feet + Vector3.up * .2f, Vector3.down, hits, .6f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (IsFacilitySurface(hits[i].collider) && hits[i].normal.y > .7f && Mathf.Abs(hits[i].point.y - feet.y) <= .18f) return true;
        return false;
    }

    private bool IsFacilitySurface(Collider collider)
    {
        return collider != null && !(collider is CharacterController) && collider.attachedRigidbody == null &&
            !collider.transform.IsChildOf(player.transform) && collider.GetComponentInParent<FacilityHitRegion>() == null;
    }

    private void PositionPlayer(Vector3 feet)
    {
        playerMotor.enabled = false;
        player.transform.SetPositionAndRotation(feet - Vector3.up * (playerMotor.center.y - playerMotor.height * .5f), Quaternion.identity);
        playerMotor.enabled = true;
        Physics.SyncTransforms();
        playerMotor.Move(Vector3.down * .08f);
    }

    private void Aim(Vector3 point) => SetEyePose(eye.transform.position, Quaternion.LookRotation(point - eye.transform.position, Vector3.up));

    private void SetEyePose(Vector3 position, Quaternion rotation)
    {
        // Mouse look normally owns yaw on the player root. Keep that same arrangement while
        // this explicit diagnostic disables input and places the camera between scenarios.
        player.ResetRecoil();
        player.transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        eye.transform.SetPositionAndRotation(position, rotation);
    }

    private static bool DistinctDirections(Vector3[] directions, out float separation)
    {
        separation = 0f;
        if (directions == null || directions.Length != 12) return false;
        for (int i = 0; i < directions.Length; i++)
        {
            if (Mathf.Abs(directions[i].magnitude - 1f) > .001f) return false;
            for (int j = 0; j < i; j++)
            {
                float angle = Vector3.Angle(directions[i], directions[j]);
                if (angle < .005f) return false;
                separation = Mathf.Max(separation, angle);
            }
        }
        return separation > 1f;
    }

    private static float HoleSeparation()
    {
        var points = new List<Vector3>();
        foreach (Transform item in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (item.gameObject.activeInHierarchy && item.name.StartsWith("Buckshot hole ", StringComparison.Ordinal)) points.Add(item.position);
        float largest = 0f;
        for (int i = 0; i < points.Count; i++) for (int j = 0; j < i; j++) largest = Mathf.Max(largest, Vector3.Distance(points[i], points[j]));
        return largest;
    }

    private void Check(bool condition, string failure) { if (!condition) receipt.failures.Add(failure); }

    private void Capture(string name)
    {
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(eye, request)) throw new InvalidOperationException("URP camera capture request unsupported.");
            RenderPipeline.SubmitRenderRequest(eye, request);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            skipFrames = 3;
        }
        finally { RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels); }
    }

    private void Update()
    {
        if (!collectingFrames) return;
        if (skipFrames > 0) { skipFrames--; return; }
        if (Time.unscaledDeltaTime > 0f) frameTimes.Add(Time.unscaledDeltaTime * 1000.0);
    }

    private void OnLog(string message, string trace, LogType kind)
    {
        if (collectingErrors && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace);
    }

    private static void Statistics(List<double> values, out double mean, out double p95)
    {
        mean = p95 = 0;
        if (values.Count == 0) return;
        foreach (double value in values) mean += value;
        mean /= values.Count; values.Sort();
        p95 = values[Mathf.Clamp(Mathf.CeilToInt(values.Count * .95f) - 1, 0, values.Count - 1)];
    }
}
