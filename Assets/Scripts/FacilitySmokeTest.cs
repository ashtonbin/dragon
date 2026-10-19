using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Developer-only standalone check: -facilitySmokeTest "absolute output directory".</summary>
/// <remarks>No runner is created without the flag. Circuit traversal is covered by the editor validator.</remarks>
public sealed class FacilitySmokeTest : MonoBehaviour
{
    [Serializable] private sealed class View
    {
        public string name;
        public Vector3 eye, look;
        public float floor, meanLuminance;
        public View(string name, Vector3 eye, Vector3 look, float floor)
        { this.name = name; this.eye = eye; this.look = look; this.floor = floor; }
    }
    [Serializable] private sealed class Receipt
    {
        public bool success;
        public string utc, unity, device, gpu, cpu, scene;
        public string timingMeaning = "Warmed uncapped frame intervals include present/wait; GPU timing is optional. Hardware-specific, not lower-end certification.";
        public string traversal = "Six grounded viewpoints only; full no-jump circuit and shortcut traversal use the editor validator receipt.";
        public int width, height, frameSamples, gpuSamples, bakedLights, liveLights, lightmaps, renderers, audioSources, playingLoops, listeners;
        public double averageFrameMs, p95FrameMs, averageGpuMs, p95GpuMs;
        public View[] views;
        public List<string> failures = new List<string>();
    }
    private readonly Receipt receipt = new Receipt();
    private readonly List<double> frameTimes = new List<double>(12000), gpuTimes = new List<double>(12000);
    private readonly FrameTiming[] timing = new FrameTiming[1];
    private string output;
    private bool collectingErrors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-facilitySmokeTest");
        if (flag < 0) return;
        if (flag + 1 >= args.Length || args[flag + 1].StartsWith("-", StringComparison.Ordinal))
        { Debug.LogError("-facilitySmokeTest requires an output directory."); Application.Quit(2); return; }
        var runner = new GameObject("Facility standalone diagnostics").AddComponent<FacilitySmokeTest>();
        runner.output = args[flag + 1];
        DontDestroyOnLoad(runner.gameObject);
    }

    private IEnumerator Start()
    {
        // The component also remains inert if it was accidentally attached in the Inspector.
        if (string.IsNullOrEmpty(output)) { enabled = false; yield break; }
        Application.logMessageReceived += OnLog;
        collectingErrors = true;
        IEnumerator run = Run();
        while (true)
        {
            bool more;
            try { more = run.MoveNext(); }
            catch (Exception exception) { receipt.failures.Add(exception.ToString()); break; }
            if (!more) break;
            yield return run.Current;
        }
        collectingErrors = false;
        Application.logMessageReceived -= OnLog;
        try
        {
            Directory.CreateDirectory(output);
            receipt.frameSamples = frameTimes.Count;
            receipt.gpuSamples = gpuTimes.Count;
            Statistics(frameTimes, out receipt.averageFrameMs, out receipt.p95FrameMs);
            Statistics(gpuTimes, out receipt.averageGpuMs, out receipt.p95GpuMs);
            receipt.success = receipt.failures.Count == 0;
            File.WriteAllText(Path.Combine(output, "smoke-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Facility smoke test " + (receipt.success ? "passed: " : "failed: ") + output);
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
        yield return null;
        RetroFPSController player = FindFirstObjectByType<RetroFPSController>();
        if (player == null) throw new InvalidOperationException("Player controller is missing.");
        player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CharacterController controller = player.GetComponent<CharacterController>();
        Camera camera = player.GetComponentInChildren<Camera>();
        if (camera == null || controller == null || !controller.enabled)
            throw new InvalidOperationException("The original Player requires its Camera and enabled CharacterController.");
        receipt.utc = DateTime.UtcNow.ToString("O"); receipt.unity = Application.unityVersion;
        receipt.device = SystemInfo.deviceModel; receipt.gpu = SystemInfo.graphicsDeviceName; receipt.cpu = SystemInfo.processorType;
        receipt.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        receipt.width = Screen.width; receipt.height = Screen.height;
        if (Screen.width != 1280 || Screen.height != 720) receipt.failures.Add("Player window did not reach 1280x720.");
        receipt.views = new[] {
            new View("01_arrival", new Vector3(1.5f,1.65f,-3), new Vector3(0,1.6f,7), 0),
            new View("02_factory", new Vector3(6,1.65f,18.5f), new Vector3(7,3,28), 0),
            new View("03_mezzanine", new Vector3(-3.5f,4.65f,31), new Vector3(10,2.2f,26), 3),
            new View("04_research", new Vector3(0,4.65f,37), new Vector3(-10,4.2f,39), 3),
            new View("05_maintenance", new Vector3(-8,1.65f,22), new Vector3(-15,2,15), 0),
            new View("06_service", new Vector3(-12,1.65f,8), new Vector3(-12,1.65f,0), 0) };
        yield return new WaitForSecondsRealtime(2f);
        InspectScene();
        foreach (View view in receipt.views)
        {
            Vector3 foot = new Vector3(view.eye.x, view.floor + 0.03f, view.eye.z);
            player.transform.position = foot - Vector3.up * (controller.center.y - controller.height * 0.5f);
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            controller.Move(Vector3.down * 0.1f);
            bool grounded = false;
            foreach (RaycastHit hit in Physics.RaycastAll(foot + Vector3.up * 0.5f, Vector3.down, 1f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != controller && !hit.collider.transform.IsChildOf(player.transform) && Mathf.Abs(hit.point.y - view.floor) < 0.2f) grounded = true;
            if (!grounded) receipt.failures.Add("No expected walking surface beneath " + view.name + ".");
            Quaternion rotation = Quaternion.LookRotation(view.look - camera.transform.position);
            camera.transform.rotation = rotation;
            yield return null; // Exclude teleport, probe refresh, and prior capture costs from sampling.
            yield return null;
            double end = Time.realtimeSinceStartupAsDouble + 3.0;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                camera.transform.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.unscaledTime * 0.7f) * 6f, Vector3.up) * rotation;
                controller.Move(Vector3.down * (Time.unscaledDeltaTime * 2f));
                FrameTimingManager.CaptureFrameTimings();
                if (Time.unscaledDeltaTime > 0f) frameTimes.Add(Time.unscaledDeltaTime * 1000.0);
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0)
                    gpuTimes.Add(timing[0].gpuFrameTime);
                yield return null;
            }
            camera.transform.rotation = rotation;
            Capture(camera, view);
        }
        if (frameTimes.Count < 60) receipt.failures.Add("Insufficient warmed frame samples.");
    }

    private void InspectScene()
    {
        receipt.lightmaps = LightmapSettings.lightmaps.Length;
        if (receipt.lightmaps == 0) receipt.failures.Add("No baked lightmaps are loaded.");
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked || light.bakingOutput.isBaked) receipt.bakedLights++;
            else if (light.isActiveAndEnabled) receipt.liveLights++;
        }
        if (receipt.bakedLights > 16 || receipt.liveLights > 2) receipt.failures.Add("Lighting exceeds 16 baked / 2 live lights.");
        foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            receipt.renderers++;
            MeshFilter mesh = renderer.GetComponent<MeshFilter>();
            if ((mesh == null || mesh.sharedMesh == null) && renderer.GetComponent<TextMesh>() == null)
                receipt.failures.Add("Missing mesh: " + renderer.name);
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name.Contains("InternalErrorShader"))
                    receipt.failures.Add("Missing or unsupported material/shader: " + renderer.name);
        }
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            receipt.audioSources++;
            if (source.clip == null) receipt.failures.Add("Audio source has no saved clip: " + source.name);
            if (source.loop && source.isPlaying) receipt.playingLoops++;
            else if (source.loop) receipt.failures.Add("Ambient loop is not playing: " + source.name);
        }
        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener.isActiveAndEnabled) receipt.listeners++;
        if (receipt.listeners != 1) receipt.failures.Add("Expected one active AudioListener.");
        if (receipt.playingLoops < 3) receipt.failures.Add("Expected at least three playing ambient loops.");
    }

    private void Capture(Camera camera, View view)
    {
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP camera capture request is unsupported.");
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            Color32[] colors = pixels.GetPixels32();
            double sum = 0;
            for (int i = 0; i < colors.Length; i += 16) sum += (colors[i].r * 0.2126 + colors[i].g * 0.7152 + colors[i].b * 0.0722) / 255.0;
            view.meanLuminance = (float)(sum / ((colors.Length + 15) / 16));
            if (view.meanLuminance < 0.002f) receipt.failures.Add("Capture is almost entirely black: " + view.name);
            File.WriteAllBytes(Path.Combine(output, view.name + ".png"), pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels); }
    }

    private void OnLog(string message, string stackTrace, LogType type)
    {
        if (collectingErrors && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            receipt.failures.Add(message + "\n" + stackTrace);
    }
    private static void Statistics(List<double> values, out double mean, out double p95)
    {
        mean = 0; p95 = 0;
        if (values.Count == 0) return;
        foreach (double value in values) mean += value;
        mean /= values.Count; values.Sort(); p95 = values[Mathf.Clamp(Mathf.CeilToInt(values.Count * 0.95f) - 1, 0, values.Count - 1)];
    }
}
