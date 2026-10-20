using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Local, explicit command inbox for validating this prototype in the running editor.</summary>
[InitializeOnLoad]
public static class FacilityEditorAutomation
{
    static readonly string DirectoryPath = Path.GetFullPath(".codex");
    static readonly string Inbox = Path.Combine(DirectoryPath, "facility-command.txt");
    static readonly string Result = Path.Combine(DirectoryPath, "facility-result.json");
    static double nextPoll;
    static string command;
    static bool baking;
    static int playCycles = SessionState.GetInt("Facility.PlayCycles", 0);
    static int baselineObjects = SessionState.GetInt("Facility.Baseline", 0);
    static double checkPlayAt;
    static readonly StringBuilder playReport = new StringBuilder(SessionState.GetString("Facility.PlayReport", ""));

    [Serializable] sealed class Receipt { public string command; public bool success; public string detail; }

    static FacilityEditorAutomation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayStateChanged;
        Lightmapping.bakeCompleted += BakeCompleted;
        if (playCycles > 0) checkPlayAt = EditorApplication.timeSinceStartup + 3;
    }

    static void Reply(bool success, string detail)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Result, JsonUtility.ToJson(new Receipt { command = command, success = success, detail = detail }, true));
        if (success) Debug.Log("Facility automation: " + detail); else Debug.LogError("Facility automation: " + detail);
    }

    static void Tick()
    {
        if (playCycles > 0 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= checkPlayAt)
        {
            command = "playtest";
            try
            {
                FacilityAmbientEffects effects = FacilitySceneGenerator.FindInScene<FacilityAmbientEffects>(SceneManager.GetActiveScene());
                if (effects == null) throw new InvalidOperationException("No ambient effects in Play mode.");
                GameObject root = effects.transform.root.gameObject;
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                if (count != baselineObjects) throw new InvalidOperationException("Facility objects changed during Play: " + count + " vs " + baselineObjects);
                foreach (AudioSource source in effects.loopingSources)
                    if (source == null || !source.isPlaying) throw new InvalidOperationException("Ambient loop did not start: " +
                        (source == null ? "missing source" : source.name + "; enabled=" + source.enabled + "; active=" + source.gameObject.activeInHierarchy +
                        "; clip=" + (source.clip == null ? "missing" : source.clip.name) + "; listenerPaused=" + AudioListener.pause + "; dsp=" + AudioSettings.dspTime));
                playReport.AppendLine("Play cycle " + playCycles + ": " + count + " facility objects; all ambient loops playing.");
                SessionState.SetString("Facility.PlayReport", playReport.ToString());
                checkPlayAt = double.MaxValue;
                EditorApplication.isPlaying = false;
            }
            catch (Exception error)
            {
                playCycles = 0;
                SessionState.SetInt("Facility.PlayCycles", 0);
                EditorApplication.isPlaying = false;
                Reply(false, error.ToString());
            }
            return;
        }
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .25;
        if (!File.Exists(Inbox)) return;
        if (Lightmapping.isRunning || playCycles > 0) return;
        command = File.ReadAllText(Inbox).Trim();
        File.Delete(Inbox);
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            switch (command)
            {
                case "m1911": Reply(true, FacilityM1911Authoring.Install()); break;
                case "m1911-capture": FacilityM1911Authoring.Capture(scene); Reply(true,"Saved silver M1911 studio previews."); break;
                case "sandbox-elite": Reply(true, FacilitySandboxEliteButton.Install()); break;
                case "elite-swat": Reply(true, FacilityEliteEncounterAuthoring.Install()); break;
                case "validate-elite": Reply(true, FacilityEliteEncounterAuthoring.Validate(scene)); break;
                case "characters": Reply(true, FacilityCharacterCastAuthoring.Install()); break;
                case "validate-characters": Reply(true, FacilityCharacterCastAuthoring.Validate(scene)); break;
                case "character-capture": FacilityCharacterCastAuthoring.Capture(scene); Reply(true, "Saved stylized cast previews in .codex/character-previews."); break;
                case "create":
                    FacilitySceneGenerator.CreateExplorationScene();
                    Reply(true, SceneManager.GetActiveScene().path);
                    break;
                case "install-combat": FacilityCombatInstaller.Install(); Reply(true, "Shotgun and subject installed; area labels removed; scene and assets saved."); break;
                case "install-menus": FacilityMenuInstaller.Install(); Reply(true, "Menus, health, character prefabs and baked walking routes installed and saved."); break;
                case "install-story": FacilityStoryInstaller.Install(); Reply(true, "Story and Sandbox scenes, arsenal, mission interface and persistent navigation installed and saved. Story lighting needs a bake."); break;
                case "upgrade-heist": FacilityHeistInstaller.Install(); Reply(true, "Heist props, closed doors, trunk drill, forty-five-second vault, police waves, Mono font and typing audio saved. Rebake story lighting."); break;
                case "windows-stable": FacilityAssetLibrary.ConfigureWindowsStability(); AssetDatabase.SaveAssets(); Reply(true, "Windows builds now use Direct3D 11 with graphics jobs disabled."); break;
                case "restart-editor-dx11":
                    if (!EditorSceneManager.SaveOpenScenes()) throw new IOException("Scene save failed; editor restart stopped.");
                    AssetDatabase.SaveAssets();
                    string projectDirectory = System.IO.Directory.GetParent(Application.dataPath).FullName;
                    Reply(true, "Saved scenes and assets; restarting the editor with Direct3D 11.");
                    EditorApplication.delayCall += () => EditorApplication.OpenProject(projectDirectory, "-force-d3d11");
                    break;
                case "finish-heist-props":
                    FacilityGameSession heistSession = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
                    string heistReport = FacilityHeistUpgrade.FinishAppliedProps(heistSession, heistSession.storyDirector);
                    FacilityStoryInstaller.RebuildNavigation(scene);
                    AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save finished story props.");
                    Reply(true, heistReport); break;
                case "story-touchup": FacilityStoryInstaller.TouchUp(); Reply(true, "Counter approach cleared; story navigation rebuilt and saved."); break;
                case "reception-repair": FacilityReceptionAuthoring.Repair(); Reply(true, "Reception drawers face staff; solid walnut front hides pistol; staff-side pickup required. Scene and navigation saved; rebake lighting."); break;
                case "bank-polish": FacilityBankAuthoring.Install(); Reply(true, "Bank menus, alarm warning, health bar, expressions, depth-tested police labels, supported rooftop sign, entrance ground and door navigation saved. Rebake bank lighting."); break;
                case "validate-bank": Reply(true, FacilityBankValidation.Validate(scene)); break;
                case "desktop-menus": FacilityDesktopMenuAuthoring.Install(); Reply(true,"Dark desktop menus, functional window close buttons and mechanical menu clicks saved in both enabled scenes."); break;
                case "upgrade-progression": Reply(true, FacilityProgressionAuthoring.Install()); break;
                case "hellcat": Reply(true, FacilityHellcatAuthoring.Install()); break;
                case "hellcat-capture": FacilityHellcatValidation.Capture(scene); Reply(true, "Saved five Challenger photographs in .codex/hellcat-previews."); break;
                case "validate-hellcat": Reply(true, FacilityHellcatValidation.Validate(scene)); break;
                case "hellcat-shadows": Reply(true, FacilityHellcatAuthoring.FinishShadowRendering()); break;
                case "car-seat": Reply(true, FacilityVehicleDepartureAuthoring.Install()); break;
                case "validate-car-seat": Reply(true, FacilityVehicleDepartureAuthoring.Validate(scene)); break;
                case "car-landscape": Reply(true, FacilityLandscapeAuthoring.Install()); break;
                case "landscape-capture": Reply(true, FacilityLandscapeAuthoring.Capture(scene)); break;
                case "desktop-capture": FacilityDesktopMenuAuthoring.Capture(); Reply(true,"Six desktop menu previews saved in .codex/desktop-previews."); break;
                case "bank-office-door": FacilityBankAuthoring.RepairOfficeDoor(); Reply(true,"Office door rehinged on north jamb; opens against rear wall; saved navigation rebuilt."); break;
                case "story-lights": FacilityStoryInstaller.TouchUpLights(); Reply(true, "Mounted lights moved clear of beams; office fixture added and parking light adjusted. Rebake required."); break;
                case "story-materials": FacilityStoryInstaller.FinishMaterials(); Reply(true, "Sedan silhouette and bullion materials adjusted and saved."); break;
                case "finish-mode-panels":
                    string originalScene = scene.path;
                    foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes.Where(item => item.enabled))
                    {
                        Scene menuScene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                        FacilityGameSession menuSession = FacilitySceneGenerator.FindInScene<FacilityGameSession>(menuScene);
                        if (menuSession == null) continue;
                        FacilityModeUIAuthoring.FinishPanels(menuSession);
                        EditorSceneManager.MarkSceneDirty(menuScene);
                        if (!EditorSceneManager.SaveScene(menuScene)) throw new IOException("Could not save menu backdrop in " + entry.path);
                    }
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                    Reply(true, "Opaque mode and completion backdrops saved in the enabled scenes."); break;
                case "open-story":
                    FacilityGameSession storySession = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
                    string storyName = storySession.storySceneNames[0];
                    EditorSceneManager.OpenScene("Assets/Scenes/" + storyName + ".unity", OpenSceneMode.Single);
                    Reply(true, SceneManager.GetActiveScene().path); break;
                case "open-sandbox":
                    EditorSceneManager.OpenScene("Assets/Scenes/FacilityPrototype.unity", OpenSceneMode.Single);
                    Reply(true, SceneManager.GetActiveScene().path); break;
                case "validate-story": Reply(true, FacilityStoryValidation.Validate(scene)); break;
                case "story-traverse": Reply(true, FacilityStoryValidation.Traverse(scene)); break;
                case "story-capture": FacilityStoryValidation.Capture(scene); Reply(true, "Saved story previews in .codex/story-previews."); break;
                case "audio": FacilityAudioAuthoring.Apply(); Reply(true, "Horror audio assets applied to the current scene and both live spawn prefabs; saved with recovery copies."); break;
                case "pump-audio": FacilityAudioAuthoring.ApplyPump(); Reply(true, "Heavy recorded pump sound assigned and saved; playback aligned with the start of the pump stroke."); break;
                case "validate-menus": Reply(true, FacilityMenuInstaller.Validate(scene)); break;
                case "validate": Reply(true, FacilityValidation.Validate(scene)); break;
                case "traverse": Reply(true, FacilityValidation.Traverse(scene)); break;
                case "polish": FacilityConstructionRepair.Repair(); Reply(true, "Construction repaired and saved; lighting requires a fresh bake."); break;
                case "finish-trim": FacilityConstructionRepair.FinishNonBakedTrim(scene); Reply(true, "Non-baked rail feet fitted to treads and ramp; baked lighting preserved."); break;
                case "construction": Reply(true, FacilityValidation.InspectConstruction(scene)); break;
                case "approaches": Reply(true, FacilityValidation.TraverseApproaches(scene)); break;
                case "update-layout":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode first.");
                    GameObject facility = null;
                    foreach (GameObject candidate in scene.GetRootGameObjects())
                        if (candidate.name == FacilitySceneGenerator.RootName && candidate.activeInHierarchy) facility = candidate;
                    if (facility == null) throw new InvalidOperationException("No active facility root.");
                    Undo.IncrementCurrentGroup();
                    int undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Update initial facility architecture");
                    for (int i = facility.transform.childCount - 1; i >= 0; i--)
                    {
                        GameObject child = facility.transform.GetChild(i).gameObject;
                        if (FacilitySceneGenerator.IsGeneratedArea(child.name)) Undo.DestroyObjectImmediate(child);
                    }
                    new FacilityLayoutBuilder(facility.transform, new FacilityAssetLibrary()).Build();
                    foreach (Transform area in facility.transform)
                        if (FacilitySceneGenerator.IsGeneratedArea(area.name)) Undo.RegisterCreatedObjectUndo(area.gameObject, "Update facility area");
                    Undo.CollapseUndoOperations(undoGroup);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Reply(true, "Updated the initial generated architecture; preserved rig, lighting, and audio.");
                    break;
                case "save-reopen":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode first.");
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed.");
                    scene = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                    Reply(true, FacilityValidation.Validate(scene));
                    break;
                case "bake":
                    baking = true;
                    FacilitySceneGenerator.BakeLighting();
                    Reply(true, "Lighting bake started.");
                    break;
                case "build":
                    Directory.CreateDirectory("Builds/FacilityPrototype");
                    EditorSceneManager.SaveOpenScenes();
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray(), locationPathName = "Builds/FacilityPrototype/FacilityPrototype.exe",
                        target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                    });
                    if (report.summary.result != BuildResult.Succeeded)
                        throw new InvalidOperationException("Build " + report.summary.result + "; errors=" + report.summary.totalErrors + "\n" +
                            string.Join("\n", report.steps.SelectMany(step => step.messages).Where(message =>
                                message.type == LogType.Error || message.type == LogType.Exception).Select(message => message.content)));
                    if (File.Exists("Assets/Fonts/JetBrainsMono/OFL.txt"))
                    {
                        Directory.CreateDirectory("Builds/FacilityPrototype/ThirdPartyNotices");
                        File.Copy("Assets/Fonts/JetBrainsMono/OFL.txt", "Builds/FacilityPrototype/ThirdPartyNotices/JetBrainsMono-OFL.txt", true);
                    }
                    Reply(true, "Windows build succeeded: " + report.summary.totalSize + " bytes, " + report.summary.totalTime);
                    break;
                case "playtest":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Already in Play mode.");
                    EditorSceneManager.SaveOpenScenes();
                    FacilityAmbientEffects ambient = FacilitySceneGenerator.FindInScene<FacilityAmbientEffects>(scene);
                    if (ambient == null) throw new InvalidOperationException("The scene has no active FacilityAmbientEffects.");
                    baselineObjects = ambient.transform.root.GetComponentsInChildren<Transform>(true).Length;
                    playReport.Clear();
                    SessionState.SetInt("Facility.Baseline", baselineObjects);
                    SessionState.SetString("Facility.PlayReport", "");
                    playCycles = 1;
                    SessionState.SetInt("Facility.PlayCycles", 1);
                    checkPlayAt = double.MaxValue;
                    EditorApplication.isPlaying = true;
                    Reply(true, "Two Play cycles started.");
                    break;
                case "capture": CaptureViews(scene); Reply(true, "Saved facility previews in .codex/previews."); break;
                case "capture-combat": CaptureViews(scene, true); Reply(true, "Saved shotgun and observer previews in .codex/previews."); break;
                case "fix-signs":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode first.");
                    FacilityAmbientEffects sceneAmbience = FacilitySceneGenerator.FindInScene<FacilityAmbientEffects>(scene);
                    GameObject signRoot = sceneAmbience.transform.root.gameObject;
                    var signAssets = new FacilityAssetLibrary();
                    FacilitySceneGenerator.ConfigureSignFont(signRoot, signAssets);
                    foreach (TextMesh text in signRoot.GetComponentsInChildren<TextMesh>())
                    {
                        MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
                        textRenderer.sharedMaterial = signAssets.Material("SignText");
                        Transform backing = text.transform.parent.Find("Painted Steel Backing");
                        if (backing == null) continue;
                        Vector3 size = backing.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                        Bounds textBounds = textRenderer.localBounds;
                        if (textBounds.size.x <= 0 || textBounds.size.y <= 0) continue;
                        float fit = Mathf.Min(size.x * .9f / textBounds.size.x, size.y * .75f / textBounds.size.y);
                        text.transform.localScale = Vector3.one * fit;
                        text.transform.localPosition = new Vector3(-textBounds.center.x * fit, -textBounds.center.y * fit, -.035f);
                    }
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Reply(true, "Assigned depth-tested signage and fitted lettering; baked architecture unchanged.");
                    break;
                case "status":
                    Reply(true, "Scene=" + scene.path + "; playing=" + EditorApplication.isPlaying +
                          "; lightmaps=" + LightmapSettings.lightmaps.Length + "; probes=" +
                          (LightmapSettings.lightProbes == null ? 0 : LightmapSettings.lightProbes.count) +
                          "; renderer=" + SystemInfo.graphicsDeviceType + "; baking=" + Lightmapping.isRunning);
                    break;
                case "edit-mode":
                    EditorApplication.isPlaying = false;
                    Reply(true, "Leaving Play mode for saved scene authoring.");
                    break;
                case "refresh":
                    Reply(true, "Importing project assets.");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    break;
                case "compile":
                    UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                    Reply(true, "Explicit script compilation requested.");
                    break;
                case "probe-report":
                    Camera probeCamera = FacilitySceneGenerator.FindInScene<RetroFPSController>(scene).GetComponentInChildren<Camera>();
                    Vector3 probePosition = probeCamera.transform.position + probeCamera.transform.forward * .8f;
                    LightProbes.GetInterpolatedProbe(probePosition, null, out SphericalHarmonicsL2 harmonics);
                    var directions = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
                    var irradiance = new Color[6];
                    harmonics.Evaluate(directions, irradiance);
                    var probeReport = new StringBuilder("Interpolated probe at " + probePosition + ": ");
                    for (int i = 0; i < directions.Length; i++) probeReport.Append(directions[i] + "=" + irradiance[i] + "; ");
                    Reply(true, probeReport.ToString());
                    break;
                case "fix-viewmodel":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode first.");
                    FacilitySceneGenerator.ConfigureViewmodel(scene, new FacilityAssetLibrary());
                    RetroViewmodel authoredViewmodel = FacilitySceneGenerator.FindInScene<RetroFPSController>(scene).GetComponentInChildren<RetroViewmodel>();
                    foreach (Material weaponMaterial in new[] { authoredViewmodel.darkMaterial, authoredViewmodel.steelMaterial, authoredViewmodel.accentMaterial })
                    {
                        weaponMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        weaponMaterial.EnableKeyword("_EMISSION");
                        EditorUtility.SetDirty(weaponMaterial);
                    }
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Reply(true, "Assigned saved, probe-lit viewmodel materials with a modest readability floor.");
                    break;
                default: throw new InvalidOperationException("Unknown facility command: " + command);
            }
        }
        catch (Exception error) { baking = false; Reply(false, error.ToString()); }
    }

    static void BakeCompleted()
    {
        if (!baking) return;
        baking = false;
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        command = "bake";
        Reply(true, "Lighting bake completed; lightmaps=" + LightmapSettings.lightmaps.Length + "; probes=" +
              (LightmapSettings.lightProbes == null ? 0 : LightmapSettings.lightProbes.count));
    }

    static void PlayStateChanged(PlayModeStateChange state)
    {
        if (playCycles == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // The explicit editor diagnostic must keep advancing while a standalone test window
            // or the IDE has focus. This changes only the diagnostic Play session.
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            // This explicit diagnostic checks live gameplay loops; ordinary Play still opens the title menu.
            FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(SceneManager.GetActiveScene());
            if (session != null) session.StartSandbox();
            checkPlayAt = EditorApplication.timeSinceStartup + 3;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (playCycles == 1)
        {
            playCycles = 2;
            SessionState.SetInt("Facility.PlayCycles", 2);
            checkPlayAt = double.MaxValue;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
        else
        {
            playCycles = 0;
            SessionState.SetInt("Facility.PlayCycles", 0);
            command = "playtest";
            Reply(true, playReport.ToString() + "Returned to Edit mode without generated duplicates.");
        }
    }

    static void CaptureViews(Scene scene, bool combatOnly = false)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use editor capture outside Play mode.");
        RetroFPSController player = FacilitySceneGenerator.FindInScene<RetroFPSController>(scene);
        Camera camera = player.GetComponentInChildren<Camera>();
        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        RenderTexture originalTarget = camera.targetTexture;
        RenderTexture active = RenderTexture.active;
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "previews"));
        try
        {
            if (combatOnly)
            {
                FacilityHumanoid subject = FacilitySceneGenerator.FindInScene<FacilityHumanoid>(scene);
                if (subject == null) throw new InvalidOperationException("No authored subject to capture.");
                Vector3 origin = subject.transform.position;
                Vector3 front = subject.transform.forward;
                Capture(camera, texture, "15_shotgun_observer", origin + front * 3.1f + Vector3.up * 1.65f,
                    origin + Vector3.up * 1.35f);
                RetroViewmodel motion = player.GetComponentInChildren<RetroViewmodel>();
                GameObject weaponModel = motion != null && motion.authoredModel != null ? motion.authoredModel.gameObject : null;
                bool modelActive = weaponModel != null && weaponModel.activeSelf;
                try
                {
                    if (weaponModel != null) weaponModel.SetActive(false);
                    Capture(camera, texture, "16_observer_close", origin + front * 1.65f + Vector3.up * 1.56f,
                        origin + Vector3.up * 1.43f);
                    Capture(camera, texture, "17_observer_full", origin + front * 2.8f + Vector3.up * 1.1f,
                        origin + Vector3.up * .93f);
                }
                finally { if (weaponModel != null) weaponModel.SetActive(modelActive); }
                return;
            }
            Capture(camera, texture, "01_arrival", new Vector3(1.5f, 1.65f, -3), new Vector3(0, 1.6f, 7));
            Capture(camera, texture, "02_factory", new Vector3(6, 1.65f, 18.5f), new Vector3(7, 3f, 28));
            Capture(camera, texture, "03_mezzanine", new Vector3(-3.5f, 4.65f, 31), new Vector3(10, 2.2f, 26));
            Capture(camera, texture, "04_research", new Vector3(0, 4.65f, 37), new Vector3(-10, 4.2f, 39));
            Capture(camera, texture, "05_maintenance", new Vector3(-8, 1.65f, 22), new Vector3(-15, 2f, 15));
            Capture(camera, texture, "06_service", new Vector3(-12, 1.65f, 8), new Vector3(-12, 1.65f, 0));
            Capture(camera, texture, "07_stair_feet", new Vector3(-3, 1.65f, 18), new Vector3(0, 1.3f, 22));
            Capture(camera, texture, "08_landing_joins", new Vector3(0, 4.65f, 28), new Vector3(1.5f, 3.5f, 30));
            Capture(camera, texture, "09_ramp_approach", new Vector3(-7, 1.65f, 22), new Vector3(-12, 1.6f, 24));
            Capture(camera, texture, "10_arrival_fixture", new Vector3(2, 1.65f, 0), new Vector3(0, 4, 1.5f));
            Capture(camera, texture, "11_transit_corner", new Vector3(0, 1.65f, 10), new Vector3(6, 3, 12.5f));
            Capture(camera, texture, "12_factory_roof", new Vector3(4, 1.65f, 21), new Vector3(13, 7, 28));
            Capture(camera, texture, "13_arrival_furniture", new Vector3(0, 1.65f, 0), new Vector3(0, 1, -4.5f));
            Capture(camera, texture, "14_ramp_top", new Vector3(-12, 4.65f, 30), new Vector3(-12, 2.8f, 25));
        }
        finally
        {
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            camera.targetTexture = originalTarget;
            RenderTexture.active = active;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void Capture(Camera camera, RenderTexture target, string name, Vector3 eye, Vector3 look)
    {
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(DirectoryPath, "previews", name + ".png"), pixels.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(pixels);
    }
}
