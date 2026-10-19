# Menus and character sandbox

Open `Assets/Scenes/FacilityPrototype.unity` and press Play, or launch `Builds/FacilityPrototype/FacilityPrototype.exe` with its adjacent files. The startup menu displays **the game of all time**, Start and Quit. Its subdued industrial background is editable uGUI geometry.

| Control | Action |
|---|---|
| Start | Choose Story or Sandbox; see STORY_PROTOTYPE.md for the mission |
| Tab | Open/close the character browser |
| Observer / Zombie card | Choose a character and enter placement mode |
| Mouse + WASD during placement | Aim the preview and move to a suitable floor |
| Left click during placement | Place one character at the valid green preview |
| Right click or Escape during placement | Cancel placement |
| Escape during ordinary play | Pause; Escape again resumes |
| Restart | Reload the facility, restore HP and remove runtime spawns/effects |
| Quit | Exit the Windows game; stop Play mode in the editor |

The browser uses a category sidebar, character cards, placement hints and a count of spawned characters. The world continues while browsing. Placement suppresses the shotgun; the click that places a character cannot fire it. Floor slope, navigation, headroom, capsule overlap, proximity to the player, edge support and distance are checked. Invalid surfaces cannot place a character. Eight runtime characters are allowed at once, including their corpses; restart clears them. The original observer in the factory is separate from that limit.

The Observer remains passive until attacked. The Zombie is a distinct, ashen, wounded worker with a slack face and raised arms. It automatically pursues the current player using saved navigation data and attacks through a clear melee sightline every 1.15 seconds. It retains the shotgun damage, head destruction, limb severing, ragdoll and recovery systems. Injured zombies with leg damage crawl.

The player starts at **3 / 3 HP**. Each successful attack removes one HP, flashes red, and updates the small bar at the top left. A shared 0.9-second invulnerability window prevents several enemies from spending all three HP in one frame. The third hit opens the death menu. Start, pause and death block movement and weapon input; pause/death also stop simulation and pause world audio. Restart starts a fresh playable session directly.

## Editing

`Facility Game Systems` contains `FacilityGameSession`, `FacilitySpawnController`, the saved navigation surface, Canvas and EventSystem. UI panels and controls are ordinary scene objects. Health is on the original Player. Player movement speeds, capsule dimensions and shotgun behavior remain intact.

`Tools > Facility > Install Menus and Spawn Sandbox` first saves a timestamped `MenuRecovery_*.unity` copy, then replaces its own systems root and creates saved observer/zombie prefabs and navigation assets under `Assets/Generated/Interface`. The zombie uses shared materials under `Assets/Generated/Combat/Zombie`. The installer preserves architecture, lighting, the original observer and unrelated authored content. Installation replaces edits inside the systems root; use the recovery copy to recover them. Repeated installation creates separate prefab/navigation snapshots. Ordinary Play and compilation do not regenerate menus or navigation.

After geometry changes, rerun installation to rebuild navigation; lighting changes still follow the existing bake workflow. The navigation radius includes clearance beyond the NPC capsule and its skin. Slow NPC motors use a zero minimum movement threshold so high frame rates do not discard their walking steps.

`Tools > Facility > Validate Menus and Spawn Assets` checks the authored wiring and persistent assets. The explicit standalone flag `-facilityMenuTest <absolute output directory>` exercises menus, placement, a passive observer, zombie navigation around a carved obstacle, natural attacks/death, pause, two restart paths and Quit dispatch. It captures the completed screen including overlay UI and writes `menu-receipt.json`; no test runner is created in ordinary play. Existing combat/environment diagnostics bypass the title screen only when their explicit command-line flags are present.

## Verification

The final Windows check and ten GUI captures are in `.codex/menu-verified`. It verifies startup/Start, blocked movement and firing in menus, safe placement with wall/void rejection, observer passivity, a zombie routing around a temporary carved obstacle, three natural attacks and death, red flash, shared hit cooldown, a shotgun headshot on a separately spawned zombie, live ambient loops, pause/resume, both restart paths and real Quit dispatch. Restart removes spawned characters and restores three HP.

Saved-reference and navigation checks passed, including thirteen complete links around the facility, stairs and ramp: `.codex/menu-navigation.json`. Save/reopen and two gameplay Play cycles passed; the facility retained 469 authored objects and four playing ambient loops in each cycle. Receipts are `.codex/menu-reopen.json`, `.codex/menu-playtest.json` and `.codex/menu-build.json`. The existing environment bake remains eight lightmaps and 78 probes. These are functional checks on the development PC, not a lower-end performance certification.
