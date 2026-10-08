# Guardianes del Bosque — Development Action Plan

Source: `GuardianesdelBosqueGDD.md`. Engine: Unity 6 (6000.5.6f1), URP, Input System.
**Language rule:** all code, identifiers, comments, commit messages and technical docs are written in English. Player-facing strings (HUD, win/lose messages) stay in Spanish per the GDD, but live in one localization-ready place (see Phase 1) so code never contains hard-coded Spanish.

## 0. Scope summary

One level, three zones (Learning → Development → Challenge), one playable character (the Forest Guardian). The player runs, jumps and auto-collects waste (eight types: bottles, cans, paper, plastic bags, juice boxes, disposable cups, glass jars, batteries) to reach a configurable target (default 10). Win: target reached. Lose: falling off the course. Pause menu: Continue / Restart / Quit. 3D low-poly, no health, lives, timer, combat, inventory or waste sorting.

## 1. GDD conflicts to resolve before building

The GDD contradicts itself in several places. Decide these first; the plan assumes the **recommended** answer.

| # | Conflict | Where | Recommendation |
|---|----------|-------|----------------|
| 1 | Waste sorting / recycling containers are excluded (§5.4, §8.4, §12.2) but still appear in audio (`SFX_Clasificacion_Correcta`, `UI_Error` "sorting error") and the Level sketch (§1: "fewer than 3 sorting errors", 6 levels). | §1, §9.2 vs §5.4, §12.2 | **Confirmed out of scope for now.** Do not build sorting, containers, `SFX_Clasificacion_Correcta`, the `UI_Error` sorting cue, or the 6-level table; keep as "future work". |
| 2 | "Rolling log", "falling rock", "mud zone", `Stumble`/`Slow` animations are listed (§7, §9) but §12.1 lists only static logs/rocks/fences. | §7.3, §7.4, §9.1 vs §8.3, §12.1 | **Confirmed in scope.** Rolling logs, falling rocks and mud are core (Phase 4); `Slow` and `Stumble` animations are core (Phase 5). |
| 3 | Win target: 15 (§1) vs 10 (§5). | §1 vs §5.1 | **10**, as a serialized parameter. |
| 4 | "Pollution indicator" bar (§9.3, §10) vs HUD with only counter + pause (§10.2, §12.1). | §9.3 vs §12.1 | Per-zone progress bar is **optional**; ship counter + pause first. |
| 5 | Accessibility extras (color-blind filters, remappable keys, text-to-speech, adaptive music) are large. | §9.2, §11 | Split: coyote time, large pickup radius, contrast, redundant feedback are **core**; remapping is **should-have**; color-blind filters and TTS are **stretch**. |
| 6 | Movement is described as horizontal (A/D, ←/→) in a 3D low-poly world. | §6 | **Confirmed:** 2.5D side-scroller — 3D art, movement on a single axis, side camera. |
| 7 | "Falls off the course after hitting an obstacle" is vague. | §4.4, §5.2 | Lose = player Y below a kill-plane threshold. Obstacles only block; they never damage. |
| 8 | Existing repo code is a different game ("Smog Buster" drone). | repo | Treat as reference only; do not reuse `DroneController`. Remove or archive it once Guardianes is on `main`. |

## 2. Technical architecture

Folder layout under `proyectoJuegoUNAD/Assets/Scripts/`:

```
Player/        PlayerController, PlayerAnimation, CoyoteTime
Camera/        SideScrollCamera
Gameplay/      GameManager, WasteCollector, Collectible, KillPlane, ZoneController
Obstacles/     Obstacle markers (static only in core scope)
UI/            HudController, PauseMenu, EndScreen
Audio/         AudioManager, ZoneAmbience
Config/        LevelConfig (ScriptableObject)
Editor/        Level validation + scene setup tools
```

Key design points:

- **`LevelConfig` ScriptableObject:** `targetWaste` (10), per-zone waste counts, kill-plane Y, coyote time (0.2 s), pickup radius. Everything the GDD calls "adjustable" lives here.
- **`GameManager`** (state machine: `Playing`, `Paused`, `Won`, `Lost`): owns the collected count, raises C# events (`OnWasteCollected`, `OnZoneRecovered`, `OnWon`, `OnLost`). UI, audio and zones subscribe; no direct references from gameplay to UI.
- **`Collectible`:** trigger collider (oversized for accessibility) → notifies `GameManager` → plays VFX/SFX → disables itself. One prefab base with eight art variants (bottle, can, paper, bag, juice box, cup, jar, battery) and identical behaviour.
- **`ZoneController`:** knows its waste count; on completion fires `OnZoneRecovered` and swaps contaminated visuals for recovered ones (simple object toggles, per GDD §6.1.4).
- **`PlayerController`:** `Rigidbody` or `CharacterController` (pick one in Phase 2 and stay with it), Input System actions `Move` and `Jump`, coyote-time and jump-buffer, animator parameters `Speed`, `IsGrounded`.
- **Input:** extend `Assets/InputSystem_Actions.inputactions` with A/D, ←/→, Space, Pause (Esc). Keep the action map rebindable from day one so remapping is cheap later.
- **Strings:** a small `Strings` static class or table for Spanish UI text; code uses keys only.
- **Testing:** EditMode tests for `GameManager` state transitions and win/lose logic (pure C#, no scene); PlayMode smoke test that loads `Level01`, collects N collectibles and asserts the win event.

## 3. Phases

Each phase ends with something playable and a commit. Estimates are relative effort for a small team (S ≈ 1–2 days, M ≈ 3–5, L ≈ 1+ week).

### Phase 0 — Decisions and repo hygiene (S)
- [ ] Record any remaining open conflicts (§1 items 4, 5, 7) in the decision log.
- [ ] Clean the working tree: ~2,200 tracked files show as deleted on disk. Decide whether the drone/city-pack assets are intentionally removed, then commit that deletion (or `git restore`) as its own commit.
- [ ] Stop tracking `Library/` at the repo root (`git rm -r --cached Library Logs UserSettings`).
- [ ] Commit `Docs/`, `Scenes/MainMenu.unity`, `Scenes/Level01.unity` with their `.meta` files.
- [ ] Agree on branch workflow (feature branches → PR to `main`) and file ownership per person (design / programming / art) to avoid scene merge conflicts. Prefab-first, one person per scene at a time.
- [ ] Set Unity Smart Merge / force text serialization (Project Settings > Editor).

**Done when:** `git status` is clean and `main` opens in Unity without errors.

### Phase 1 — Project skeleton (S)
- [ ] Create the folder structure above and the assembly definition(s) if desired.
- [ ] `LevelConfig` asset with default values; `GameManager` with the state machine and events; EditMode tests for it.
- [ ] Spanish UI strings table.
- [ ] Input actions: Move, Jump, Pause.

**Done when:** tests pass; `GameManager` can be driven from a test and reports win/lose correctly.

### Phase 2 — Greybox level and player movement (M)
- [ ] Greybox `Level01` with three zones using primitives/ProBuilder; fixed side camera path.
- [ ] `PlayerController`: run, jump, ground check, coyote time (0.2 s), jump buffer; tune feel in a dedicated test strip.
- [ ] `SideScrollCamera` follow with smoothing and bounds.
- [ ] Kill-plane → `OnLost` → restart.

**Done when:** a placeholder capsule can traverse all three zones and falling resets the level.

### Phase 3 — Collection loop and win/lose (M)
- [x] `Collectible` prefab with enlarged trigger, auto-collect, event to `GameManager`: `Scripts/Gameplay/Collectible.cs` and `Assets/Prefabs/Collectibles/` (base `Collectible` plus one variant per waste type, `Collectible_Bottle` … `Collectible_Battery`). The model bobs and spins on a `Visual` child; `Collectible.Collected` is the hook for VFX/SFX. Tests: `CollectiblePrefabPolicyTests` (EditMode, 3) and `CollectiblePlayModeTests` (PlayMode, 2).
- [ ] HUD counter `Residuos: X/10` bound to `OnWasteCollected`.
- [ ] Win and lose screens with Restart / Play again.
- [ ] Place the 10+ collectibles per the zone budget (e.g. 3 / 3 / 4) via a `LevelConfig`-driven validator that warns if placed count < target.

**Done when:** the full GDD core loop works end to end with placeholder art.

### Phase 4 — Obstacles, hazards, zones, pause (L)
- [ ] Static obstacle prefabs: log, rock, fence with correct colliders; jump-height validation (an editor script that checks every obstacle is clearable at max jump height).
- [ ] Dynamic hazards (all single-axis, deterministic and telegraphed so they stay fair for ages 10–14):
  - `RollingLog`: rolls along the course on a fixed path/speed; contact triggers `Stumble` (brief knockback, no damage) and is lethal only if it pushes the player off the course.
  - `FallingRock`: spawns from a marked trigger with a ground-shadow warning before impact; same `Stumble` rule.
  - `MudZone`: trigger volume that multiplies move speed (config value, e.g. 0.5) and sets the `Slow` animator state while inside; restores speed on exit.
  - Tunable parameters live in `LevelConfig` or per-prefab serialized fields; hazards pause with `Time.timeScale`.
- [ ] Extend the jump-clearance validator to cover hazard timing gaps.
- [ ] Zone difficulty pass: Zone 1 wide and gentle, Zone 2 mixed, Zone 3 dense with elevated collectibles.
- [ ] `ZoneController`: recovery event, unlocks the next segment (gate removed) and toggles contaminated → recovered visuals.
- [ ] Pause menu (Continue / Restart / Quit), `Time.timeScale` handling, Esc and on-screen button.

**Done when:** each zone is completable and the pause menu works at any moment, including after win/lose.

### Phase 5 — Art pass: character, environment, UI (L)
- [ ] Source and register assets (Kenney, Mixamo, Asset Store, OpenGameArt). **Fill the license table in GDD §13.1 as each asset is added** — do not defer.
- [ ] Character with Idle / Run / Jump / Victory / Slow / Stumble animations and an Animator Controller (parameters from Phase 2).
- [ ] Trees, vegetation, rocks, mountains, fences, logs; recovered-zone props (new vegetation, clean water).
- [x] Waste models: the team's own low-poly models in Blender (`Coleccionables.blend`, collection `Residuos`): `Waste_Bottle`, `Waste_Can`, `Waste_Paper`, `Waste_Bag`, `Waste_JuiceBox`, `Waste_Cup`, `Waste_Jar`, `Waste_Battery`. Each has a body material and an emissive `Rim` material for the bloom, flat shading and a centred pivot. Still to do: export to FBX (leave out the hidden default `Cube`) and set the emission again in Unity, since FBX import usually drops it.
- [ ] URP lighting, low-poly materials, fog/tint that shifts from polluted to clean per zone.
- [ ] HUD icons, pause icon, win/lose panels; legible sans-serif font (e.g. Atkinson Hyperlegible, check license); contrast ≥ 4.5:1.

**Done when:** the level looks consistent, and polluted vs recovered state reads at a glance.

### Phase 6 — Audio and feedback (M)
- [ ] `AudioManager` (mixer groups: Music, Ambience, SFX, UI; volume saved in `PlayerPrefs`).
- [ ] SFX: footsteps, jump, land, collect, zone-recovery stinger, UI click, pause.
- [ ] Ambience: polluted vs clean loops crossfaded by zone state.
- [ ] Music: base loop first; the string and wind layers are optional (stems enabled as waste is collected).
- [ ] Collection VFX (particle burst + `+1` pop on HUD). Every cue has a visual twin and vice versa (GDD §11.3).

**Done when:** every player action has both audio and visual feedback.

### Phase 7 — Accessibility and stretch features (M, scope-controlled)
Must-have (finish before release):
- [ ] Coyote time and oversized pickup radius (already built; expose in a settings asset).
- [ ] Redundant feedback audit; contrast check on all HUD text.
- [ ] Navigation hint (footprint line or arrow) toward the next waste cluster.

Should-have:
- [ ] Key remapping screen using Input System rebinding.
- [ ] Short instruction text on Zone 1.

Stretch (cut first if schedule slips):
- [ ] Color-blind filter options; text-to-speech.
- [ ] Per-zone pollution bar.
- [ ] Dynamic music layers.

### Phase 8 — Main menu, polish, QA, delivery (M)
- [ ] `MainMenu` scene: Play / Quit, scene loading with Build Settings order (`MainMenu`, `Level01`).
- [ ] Playtests with 3+ people close to the target age range; log issues in GitHub Issues.
- [ ] Balance: waste positions, jump distances, camera framing.
- [ ] Profile on target PC spec; keep draw calls low (static batching, GPU instancing on low-poly assets).
- [ ] Windows build; smoke-test on a clean machine.
- [ ] Final license table, credits screen/readme, delivery notes.

**Done when:** a clean build runs from the menu through win and lose without errors.

## 4. Suggested role split

| Role | Primary focus |
|------|---------------|
| Level designer (Elianis) | Greybox, zone pacing, obstacle/waste placement, playtests |
| Game programmer (Jhon) | Phases 1–4, 6 code, tests, build |
| Art / audio (Geordany or shared) | Asset sourcing, Phase 5 and 6 content, license log |

Adjust to the team's real availability.

## 5. Risks

| Risk | Mitigation |
|------|------------|
| Scope creep from GDD stretch items | Treat §1 and Phase 7 tiers as binding; cut stretch first (hazards are now core, so cut Phase 7 items before them) |
| Scene merge conflicts | Prefabs for everything; one editor per scene; text serialization |
| Dynamic hazards add tuning and collision complexity | Build static obstacles first, add hazards one at a time with playtests |
| Asset licensing gaps | Log every asset in §13.1 the day it is imported |
| Jump difficulty too high for ages 10–14 | Coyote time, generous colliders, early playtests |
| Existing repo state (deleted files, committed `Library/`) | Phase 0 cleanup before any new feature work |

## 6. Decision log

_(Fill in during Phase 0.)_

- Waste sorting: out of scope for now (revisit after the core game ships) — ☑ confirmed
- Target waste count: 10 — ☑ confirmed
- Waste types: eight (bottles, cans, paper, plastic bags, juice boxes, disposable cups, glass jars, batteries), all with the same behaviour; GDD §6.2, §8.4 and §13.1 updated — ☑ confirmed
- Movement model: 3D look with side-scroller movement — ☑ confirmed
- Rolling logs, falling rocks, mud: ☑ in scope (core, Phase 4–5)
