# Facility exploration prototype

Open **Assets/Scenes/FacilityPrototype.unity** and press Play. The Windows build is **Builds/FacilityPrototype/FacilityPrototype.exe**; keep its adjacent data and runtime files together.

The shotgun and staring humanoid encounter are now installed. See **COMBAT_PROTOTYPE.md** for controls, gore behaviour, authoring, and current combat verification. Area-identification and wayfinding signs have been removed.

Start/pause/death menus, a Tab character browser, placeable observers/zombies and a three-hit player HP display are installed. See **MENUS_AND_SPAWNING.md** for controls, editing and verification. Ordinary launch now opens the title menu.

The old ventilation/duct hiss has been replaced by a quiet low drone. Shotgun and character voices also use the new horror sound pass; see **AUDIO_DESIGN.md** and **AUDIO_CREDITS.md**.

WASD moves, Shift sprints, Space jumps, and the mouse looks. Escape releases the cursor; clicking captures it again. The route checks use walking and sprinting without jumping. The existing player rig and weapon bob/sway are retained.

## Editing and repairing construction

The facility is saved Unity geometry grouped into Arrival, Transit, Factory, Research, Maintenance, and Service Return. Move objects and tune materials or lights directly in the editor. Play, recompilation, and Inspector changes do not regenerate the facility.

**Tools > Facility > Repair Construction and Lighting** repairs the open exploration scene. It first saves a timestamped **Assets/Scenes/ConstructionRecovery_*.unity** copy and stops if that backup fails. It replaces the generated area groups and **Lighting and Sound**, preserving the Player and unrelated scene objects. Manual edits inside the replaced groups are overwritten; their previous state remains in the recovery scene. The repair also clears obsolete lightmaps and updates shared facility materials and bake settings. Run **Tools > Facility > Bake Lighting** afterward, wait for completion, and save before reviewing illumination.

**Tools > Facility > Create Exploration Scene** creates a separate editable snapshot from the open scene's player rig, with a recovery copy first. Later invocations use unique scene names and update the enabled startup scene in Build Settings. The original working rig is also preserved in **Assets/Scenes/PrototypeRecovery_20261017.unity**. MainScene retains the original procedural prototype; its legacy generators are disabled in the facility copy. The corrected atmosphere filename retains its asset GUID.

The circuit climbs the factory stairs to the 3 m mezzanine and research lab, descends through maintenance, and returns through the service passage. The factory-maintenance door and ramp forecourt provide a ground-level shortcut. The maintenance ramp occupies **X -14 to -10 m, Z 23 to 29 m**, rising **3 m over a 6 m run**. The complete facility occupies X -18 to 18 m and Z -5 to 43 m.

## Lighting and materials

The cleanup uses mounted fixture housings and supported rails with base plates. The inspected scene contains **17 fixture housings, 16 baked lights, and no realtime lights**. Emergency lenses breathe through renderer colour changes; they do not vary world-light intensity. Structural geometry and substantial props cast baked shadows; small trim and fixture pieces do not. Light probes illuminate the moving viewmodel.

Industrial surfaces use matte materials with specular highlights and environment reflections disabled. Small trim is excluded from baked shadowing and lightmap charts. Bake ambient occlusion is disabled. Current bake settings are nondirectional 1024 px lightmaps, **24 texels/metre, 8 px padding, 64 direct samples, 256 indirect samples, 64 environment samples**, and two indirect bounces. The cleanup bake produced **eight atlases and 78 probes**, exceeding the initial four-atlas target to improve shadow and corner quality. Major geometry or light edits require a fresh bake.

Meshes, materials, low-resolution textures, WAV clips, profiles, lighting settings, and the Facility Low pipeline live under **Assets/Generated/Facility**. Normal scene creation reuses existing assets. The repair deliberately updates the shared industrial material and bake settings for this cleanup.

Facility Low uses Forward rendering, shared materials, static batching, 720p, and no realtime shadows, SSAO, HDR, or antialiasing. Fog runs from 7 to 32 m. Tune fixture placement, baked illumination, and ambient colour before increasing fog or contrast. The lower-end desktop 720p/60 FPS target still needs testing on representative hardware.

## Physical checks and diagnostics

- **Tools > Facility > Validate Exploration Scene** checks saved references, meshes, materials, profiles, audio wiring, and light budgets.
- **Tools > Facility > Check Walking Routes** drives the actual CharacterController around the circuit and shortcut in both directions without jumping, checking grounding, capsule clearance, and at least 2.2 m headroom. It restores the Player afterward; run outside Play mode.
- `FacilityValidation.InspectConstruction(scene)` checks fixture support, rail/base-plate contact, guard thickness and collision alignment, and full-width doorway/turning clearance.
- `FacilityValidation.TraverseApproaches(scene)` tests offsets, doorway edges, landings, and the west ramp approach at 6 and 10 m/s in both directions, restoring the Player afterward.

The editor inbox **.codex/facility-command.txt** accepts explicit local commands including `polish`, `construction`, `approaches`, `validate`, `traverse`, `save-reopen`, `bake`, `capture`, `playtest`, `build`, and `status`. Receipts are written to **.codex/facility-result.json**. An absent command file performs no work. Use the menu commands for ordinary authoring.

The optional standalone argument `-facilitySmokeTest "C:\absolute\output-folder"` captures six views, samples warmed frame timing, checks audio/material/lightmap state, writes a JSON receipt, and quits. Without that argument it creates no smoke-test object. Standalone timing describes the machine and viewpoints tested; editor traversal supplies separate collision coverage.

## Construction cleanup milestone — October 17, 2026, before combat

| Check | Current result |
|---|---|
| Unity / URP | 6000.3.21f1 / 17.3.0 |
| Construction and clearance | Passed: 17 fixture housings, 27 rail posts, 19 base plates, 18 solid rails, and 234 capsule/headroom samples |
| Physical approaches | Passed: 144 routes, 468 checkpoints, 1,913.9 m, and 47,089 CharacterController.Move steps at walking/sprinting speeds; Player restored |
| Final lighting bake and visual review | Passed: eight lightmaps, 78 probes; fourteen rendered views reviewed, including ceiling mounts, stair feet, rail joins, ramp approaches and corners |
| Final save / reopen | Passed: 420 renderers, 174 shared meshes, 11 environment/sign materials, 128 solid colliders; all saved references valid |
| Final repeated Play | Passed: two cycles, 511 facility objects in each, all four ambient loops playing, no duplicate generation |
| Final Windows build and standalone diagnostics | Passed: non-development Windows x64 build; visible-window 1280 x 720 run exited 0, six captured views, 429 runtime renderers, four playing loops, one listener, no missing shaders or log warnings/errors |
| Visible-window frame intervals | 0.404 ms mean / 0.923 ms 95th percentile, 44,580 warmed samples on Ryzen 7 9700X / RTX 5070; GPU timestamps unavailable |

Current construction and approach receipts are **.codex/construction-receipt.json** and **.codex/approaches-receipt.json**. Final bake, reopen, Play, and build receipts are **.codex/bake-polished.json**, **.codex/reopen-polished.json**, **.codex/playtest-polished.json**, and **.codex/build-polished.json**. The fourteen editor views are in **.codex/previews**; final standalone captures, log, and receipt are in **.codex/standalone-final**. Earlier artifacts describe previous scene revisions.

Frame intervals were uncapped and include present/wait. They describe this small environment on the development PC, not a lower-end hardware guarantee or a combat benchmark.
