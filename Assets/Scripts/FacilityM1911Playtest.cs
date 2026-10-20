using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in built-player proof: -facilityM1911Test outputDir. Never changes saved assets or the player's wallet.</summary>
public sealed class FacilityM1911Playtest : MonoBehaviour
{
    [Serializable] sealed class Receipt
    {
        public bool success;
        public string coverage = "Actual saved Sandbox M1911: first-person idle, iron sights, firing, slide travel, last-round lock and reload renders; eight-round consumption, cadence, brass, recoil and real audio; paused reload, interrupted reload, reset, wall obstruction and an anatomical headshot. Studio render clones the actual authored weapon, with hands hidden. Temporary runtime objects and an isolated account only.";
        public List<string> checks = new List<string>(), failures = new List<string>();
        public int pictures, magazineCapacity, shotsBeforeReload, casingsBeforeReload;
        public float slideRearTravel;
    }
    struct PoseSample
    {
        public Transform transform;
        public Vector3 position, scale;
        public Quaternion rotation;
        public PoseSample(Transform value) { transform = value; position = value.localPosition; rotation = value.localRotation; scale = value.localScale; }
        public bool Matches() => transform != null && Vector3.Distance(transform.localPosition, position) < .0001f &&
            Quaternion.Angle(transform.localRotation, rotation) < .01f && Vector3.Distance(transform.localScale, scale) < .0001f;
    }
    readonly Receipt receipt = new Receipt();
    FacilityGameSession session;
    FacilityWeaponInventory inventory;
    FacilityPistol pistol;
    RetroFPSController player;
    CharacterController motor;
    Camera eyes;
    string output;
    IDisposable account;
    bool collecting;
    Vector3 slideHome;
    List<PoseSample> home;
    GameObject stage;
    Material stageMaterial;
    float priorCaptureDelta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-facilityM1911Test");
        if (i < 0) return;
        if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal)) { Application.Quit(2); return; }
        var runner = new GameObject("Silver M1911 proof - CLI only").AddComponent<FacilityM1911Playtest>();
        runner.output = args[i + 1]; DontDestroyOnLoad(runner.gameObject);
    }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        Directory.CreateDirectory(output); Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
        // PNG readback must not skip the short mechanical slide cycle on the following frame.
        priorCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 120f;
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
        collecting = false; Application.logMessageReceived -= OnLog; account?.Dispose(); Time.timeScale = 1f; Time.captureDeltaTime = priorCaptureDelta; AudioListener.pause = false;
        if (stage != null) Destroy(stage); if (stageMaterial != null) Destroy(stageMaterial);
        receipt.success = receipt.failures.Count == 0;
        File.WriteAllText(Path.Combine(output, "m1911-receipt.json"), JsonUtility.ToJson(receipt, true));
        Application.Quit(receipt.success ? 0 : 1);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.6f); session = FacilityGameSession.Instance;
        Require(session != null, "The saved game title loads.");
        session.StartSandbox(); yield return new WaitForSecondsRealtime(.4f);
        session = FacilityGameSession.Instance;
        Require(session.CurrentMode == FacilityGameMode.Sandbox && session.CurrentState == FacilityGameState.Playing, "The normal Sandbox entry starts gameplay.");
        inventory = session.inventory; player = session.player; eyes = player.GetComponentInChildren<Camera>(); motor = player.GetComponent<CharacterController>();
        player.enabled = false;
        Require(inventory != null && inventory.pistol != null && inventory.pistolModel != null && inventory.Equip(FacilityWeaponKind.Pistol), "The real authored pistol equips through the normal inventory.");
        pistol = inventory.pistol; yield return new WaitForSecondsRealtime(.2f);
        Require(pistol.MagazineCapacity == 8 && pistol.RoundsRemaining == 8 && !pistol.IsReloading, "The M1911 starts with eight rounds and no active reload.");
        receipt.magazineCapacity = pistol.MagazineCapacity;
        Require(pistol.slide != null && pistol.magazine != null && pistol.supportHand != null && pistol.reloadAssembly != null &&
            pistol.hammer != null && pistol.trigger != null && pistol.ejectionPort != null && pistol.casingPrefab != null, "Slide, magazine, two-hand reload rig, hammer, trigger and brass source are saved scene references.");
        Require(pistol.reportSource != null && pistol.reportClip != null && pistol.reportClip.length > .12f && pistol.actionSource != null &&
            pistol.reloadClip != null && pistol.emptyClip != null && pistol.slideClip != null, "The saved pistol has a substantial shot report and separate reload, empty and slide audio.");
        bool silver = false; int vertices = 0;
        foreach (MeshRenderer renderer in inventory.pistolModel.GetComponentsInChildren<MeshRenderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !material.HasProperty("_Metallic") || !material.HasProperty("_BaseColor")) continue;
                Color color = material.GetColor("_BaseColor");
                if (material.GetFloat("_Metallic") >= .5f && color.grayscale > .35f && Mathf.Abs(color.r - color.b) < .18f) silver = true;
            }
            var filter = renderer.GetComponent<MeshFilter>(); if (filter != null && filter.sharedMesh != null) vertices += filter.sharedMesh.vertexCount;
        }
        Require(silver && vertices > 800, "The actual first-person asset contains detailed mesh geometry and reflective silver metal.");
        Capture("01_m1911_first_person_idle");
        Require(pistol.SetAiming(true), "The equipped M1911 enters its actual iron-sight aim mode.");
        yield return new WaitForSeconds(.5f);
        Require(pistol.IsAiming && pistol.AimBlend > .95f && pistol.viewmodel != null &&
            Mathf.Abs(pistol.viewmodel.restingPosition.x) < .01f && Mathf.Abs(pistol.viewmodel.restingPosition.y + .076f) < .012f,
            "Iron-sight aim smoothly brings the actual weapon to the centered aiming pose.");
        Capture("08_m1911_iron_sights");
        pistol.SetAiming(false); yield return new WaitForSeconds(.5f);
        Require(!pistol.IsAiming && pistol.AimBlend < .05f, "Releasing aim smoothly returns the M1911 to its hip pose.");
        slideHome = pistol.slide.localPosition; home = SampleRig();
        Vector3 modelHome = inventory.pistolModel.localPosition;
        Require(pistol.TryFire() && pistol.ShotsFired == 1 && pistol.RoundsRemaining == 7 && pistol.CasingsEjected == 1 &&
            player.CameraRecoilDegrees > 1f && Vector3.Distance(modelHome, inventory.pistolModel.localPosition) > .035f &&
            pistol.muzzleFlash != null && pistol.muzzleFlash.activeInHierarchy && pistol.reportSource.isPlaying,
            "A real shot consumes one round and produces brass, camera kick, model kick, muzzle flash and audible report.");
        Require(!pistol.TryFire() && pistol.ShotsFired == 1 && pistol.CasingsEjected == 1, "The semi-automatic interval rejects an immediate second shot without spending ammunition or brass.");
        Capture("02_m1911_firing");
        yield return new WaitForSeconds(.025f);
        receipt.slideRearTravel = Vector3.Distance(slideHome, pistol.slide.localPosition);
        Require(receipt.slideRearTravel > .015f, "The firing slide physically cycles rearward.");
        Capture("03_m1911_slide_rear");
        for (int i = 1; i < 8; i++)
        {
            yield return new WaitForSeconds(pistol.FireInterval + .025f);
            Require(pistol.TryFire() && pistol.RoundsRemaining == 7 - i, "Shot " + (i + 1) + " consumes exactly one of the original eight rounds.");
        }
        yield return new WaitForSeconds(.15f);
        receipt.shotsBeforeReload = pistol.ShotsFired; receipt.casingsBeforeReload = pistol.CasingsEjected;
        Require(pistol.RoundsRemaining == 0 && pistol.ShotsFired == 8 && pistol.CasingsEjected == 8 &&
            Vector3.Distance(slideHome, pistol.slide.localPosition) > .015f, "The eighth shot leaves the magazine empty and the slide visibly locked back, with eight spent casings.");
        Capture("04_m1911_empty_slide_lock");
        yield return new WaitForSeconds(pistol.FireInterval);
        Require(!pistol.TryFire() && pistol.ShotsFired == 8 && pistol.CasingsEjected == 8 && !pistol.muzzleFlash.activeSelf,
            "An empty trigger press cannot fire, flash or manufacture brass.");
        Require(pistol.TryReload() && pistol.IsReloading && !pistol.TryFire() && !pistol.IsAiming && !pistol.SetAiming(true), "An empty pistol starts its reload and blocks firing and aiming during it.");
        yield return new WaitForSeconds(.58f);
        Capture("05_m1911_reload_magazine_out");
        Require(!RigMatches(home), "Reload visibly moves the authored hands and magazine from their idle poses.");
        List<PoseSample> paused = SampleRig(); int rounds = pistol.RoundsRemaining, reloads = pistol.ReloadsCompleted; float time = Time.time;
        session.PauseGame(); yield return new WaitForSecondsRealtime(.32f);
        Require(pistol.IsReloading && pistol.RoundsRemaining == rounds && pistol.ReloadsCompleted == reloads &&
            Mathf.Abs(Time.time - time) < .0001f && RigMatches(paused), "The normal pause menu freezes the incomplete reload, rig poses and ammunition.");
        session.ResumeGame(); yield return new WaitForSeconds(pistol.reloadDuration + .1f);
        Require(!pistol.IsReloading && pistol.RoundsRemaining == 8 && pistol.ReloadsCompleted == 1 &&
            Vector3.Distance(slideHome, pistol.slide.localPosition) < .0001f && RigMatches(home), "Resuming finishes one reload, chambers the slide and restores the complete hand/magazine rig.");
        Require(!pistol.TryReload(), "A full magazine cannot start a redundant reload.");
        Capture("06_m1911_reload_complete");
        player.ResetRecoil();
        Require(pistol.TryFire(), "A reloaded M1911 fires again normally.");
        yield return new WaitForSeconds(pistol.FireInterval + .05f);
        Require(pistol.TryReload(), "A partly used magazine supports a tactical reload.");
        yield return new WaitForSeconds(.5f);
        Require(inventory.Equip(FacilityWeaponKind.Shotgun), "The normal inventory switches away during a tactical reload.");
        Require(!pistol.IsReloading && pistol.RoundsRemaining == 7 && pistol.ReloadsCompleted == 1 && RigMatches(home),
            "Switching weapons cancels the reload, preserves its unearned ammunition and restores every moving rig part.");
        inventory.Equip(FacilityWeaponKind.Pistol); yield return new WaitForSeconds(.2f);
        Require(pistol.RoundsRemaining == 7 && !pistol.IsReloading && RigMatches(home), "Re-equipping does not refill the magazine or retain a displaced reload pose.");
        pistol.ResetForRun();
        Require(pistol.RoundsRemaining == 8 && pistol.ShotsFired == 0 && pistol.CasingsEjected == 0 && pistol.ReloadsCompleted == 0 &&
            !pistol.IsReloading && RigMatches(home), "The normal run-reset API restores the loaded pistol, counters and rig poses.");
        yield return DamageAndObstruction();
        yield return Studio();
        Require(FacilityEconomy.Cash == 0, "The proof leaves its isolated wallet unchanged.");
    }
    IEnumerator DamageAndObstruction()
    {
        stage = new GameObject("Temporary M1911 proof stage");
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(stage.transform);
        floor.transform.position = new Vector3(100f, 49.9f, 103f); floor.transform.localScale = new Vector3(18f, .2f, 18f);
        GameObject clone = Instantiate(session.spawner.observerPrefab, stage.transform); clone.SetActive(false);
        clone.transform.SetPositionAndRotation(new Vector3(100f, 50.045f, 105f), Quaternion.Euler(0f, 180f, 0f));
        var actor = clone.GetComponent<FacilityHumanoid>(); actor.player = player.transform;
        if (actor.gore != null)
        {
            var ignored = new Collider[actor.bodyColliders.Length + 2]; Array.Copy(actor.bodyColliders, ignored, actor.bodyColliders.Length);
            ignored[ignored.Length - 2] = actor.motor; ignored[ignored.Length - 1] = motor; actor.gore.ownerColliders = ignored;
        }
        clone.SetActive(true); actor.enabled = false; if (actor.motor != null) actor.motor.enabled = false;
        foreach (Rigidbody body in actor.bodies) if (body != null) body.isKinematic = true;
        FacilityHitRegion head = null; foreach (FacilityHitRegion region in actor.hitRegions) if (region.kind == FacilityBodyRegion.Head) head = region;
        Require(head != null, "A real saved observer supplies its original anatomical head collider.");
        yield return null;
        Vector3 aim = head.GetComponent<Collider>().bounds.center;
        Pose(new Vector3(100f, 50.05f, 100f), aim); yield return null;
        Vector3 eye = eyes.transform.position, toMuzzle = pistol.muzzle.position - eye;
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "Temporary near-barrel obstruction"; block.transform.SetParent(stage.transform);
        block.transform.position = eye + toMuzzle * .65f; block.transform.localScale = Vector3.one * .055f;
        Physics.SyncTransforms();
        Vector3 closest = eye + eyes.transform.forward * Vector3.Dot(block.transform.position - eye, eyes.transform.forward);
        Require(Vector3.Distance(closest, block.transform.position) > .05f, "The obstruction is beside the central sight ray and between the camera and actual muzzle.");
        Require(pistol.TryFire() && actor.HitsTaken == 0 && !actor.HeadExploded, "A physical obstruction along the real barrel path blocks a clear camera-ray headshot.");
        Destroy(block); yield return null; yield return new WaitForSeconds(pistol.FireInterval + .05f);
        Pose(new Vector3(100f, 50.05f, 100f), aim); Physics.SyncTransforms();
        Require(pistol.TryFire() && actor.HeadExploded && actor.IsDead && actor.gore.HeadBurstCount == 1,
            "Removing that obstruction lets the actual pistol ray damage the real head, preserving original headshot gore.");
        yield return null;
        Destroy(clone);
    }
    IEnumerator Studio()
    {
        pistol.ResetForRun();
        GameObject clone = Instantiate(inventory.pistolModel.gameObject, stage.transform); clone.name = "Actual saved silver M1911 geometry - temporary studio";
        clone.transform.SetPositionAndRotation(new Vector3(105f, 51f, 103f), Quaternion.Euler(0f, 150f, 0f)); clone.SetActive(true);
        foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            if (child.name == "M1911 right hand and sleeve" || child.name == "M1911 left support hand and sleeve") child.gameObject.SetActive(false);
        foreach (Light light in clone.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (AudioSource source in clone.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
        GameObject key = new GameObject("Temporary soft studio key"); key.transform.SetParent(stage.transform); key.transform.position = new Vector3(104f, 52f, 102f);
        var lighting = key.AddComponent<Light>(); lighting.type = LightType.Point; lighting.color = new Color(.85f,.93f,1f); lighting.intensity = 9f; lighting.range = 7f;
        Bounds bounds = new Bounds(clone.transform.position, Vector3.zero); bool first = true;
        foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
        }
        yield return null;
        Vector3 centre = bounds.center;
        Capture("07_m1911_actual_model_three_quarter", centre + new Vector3(.48f, .24f, .64f), centre, 35f);
        Destroy(clone);
    }
    List<PoseSample> SampleRig()
    {
        var poses = new List<PoseSample>();
        Transform[] parts = { pistol.slide, pistol.magazine, pistol.supportHand, pistol.reloadAssembly, pistol.hammer, pistol.trigger };
        foreach (Transform part in parts) if (part != null) poses.Add(new PoseSample(part));
        return poses;
    }
    static bool RigMatches(List<PoseSample> poses) { foreach (PoseSample pose in poses) if (!pose.Matches()) return false; return true; }
    void Pose(Vector3 feet, Vector3 aim)
    {
        motor.enabled = false; player.transform.position = feet; motor.enabled = true; player.ResetRecoil();
        Vector3 flat = aim - eyes.transform.position; flat.y = 0f; player.transform.rotation = Quaternion.LookRotation(flat);
        eyes.transform.rotation = Quaternion.LookRotation(aim - eyes.transform.position); Physics.SyncTransforms();
    }
    void Capture(string name, Vector3? position = null, Vector3? aim = null, float fov = 0f)
    {
        var node = new GameObject("Temporary M1911 proof camera"); Camera camera = node.AddComponent<Camera>(); camera.CopyFrom(eyes); camera.enabled = false;
        camera.transform.SetPositionAndRotation(position ?? eyes.transform.position, position.HasValue ? Quaternion.LookRotation(aim.Value - position.Value) : eyes.transform.rotation);
        if (fov > 0f) camera.fieldOfView = fov;
        camera.aspect = 16f / 9f; var data = camera.GetUniversalAdditionalCameraData(); data.renderShadows = true; data.renderPostProcessing = false;
        Canvas canvas = session.canvas; bool wasEnabled = canvas.enabled; canvas.enabled = false;
        RenderTexture prior = RenderTexture.active; var target = new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32); target.Create();
        var pixels = new Texture2D(1600,900,TextureFormat.RGB24,false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG()); receipt.pictures++;
        }
        finally { canvas.enabled = wasEnabled; RenderTexture.active = prior; Destroy(pixels); target.Release(); Destroy(target); Destroy(node); }
    }
    void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); receipt.checks.Add(description); }
    void OnLog(string message, string trace, LogType kind)
    {
        if (collecting && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) receipt.failures.Add(message + "\n" + trace);
    }
}
