# Simple Nature Pack — Integration Plan

Goal: use the Asset Store package **Low-Poly Simple Nature Pack** by JustCreate (`Assets/SimpleNaturePack`, v1.24, May 2023, Unity Asset Store: https://assetstore.unity.com/packages/3d/environments/landscapes/low-poly-simple-nature-pack-162153; licence: Standard Unity Asset Store EULA, Extension Asset) to build the forest of `Level01` as described in the GDD (§8 Environment and level design, §9.1 Visual resources, §13 Licences), without breaking the gameplay code that already exists.

Sizes: S = under half a day, M = about a day, L = several days.

## 1. What is actually in the project

Audit of the imported folder (done with the Unity MCP and by unpacking the two `.unitypackage` files, outside the repo):

| Item | Finding |
|------|---------|
| Content | 24 prefabs / 24 FBX: `Tree_01–05`, `Bush_01–03`, `Grass_01–02`, `Flowers_01–02`, `Mushroom_01–02`, `Rock_01–05`, `Stump_01`, `Branch_01`, `Ground_01–03`. One texture atlas (`NaturePackLite_Texture_01.png`) and 2 materials. |
| **Render pipeline** | The extracted folder is the **built-in (Standard shader)** variant. The project is URP 17.5, so these materials render **magenta**. The URP variant ships as `SimpleNaturePack_2020.3_URP_v1.24.unitypackage` and uses the same GUIDs for models, prefabs and materials, so only the shader differs. |
| Redundant files | Both `.unitypackage` files (HDRP and URP, 1.4 MB) sit inside `Assets/`, plus two demo scenes (`SimpleNaturePack_Demo`, `_Overview`) and their lighting settings. None of it is needed by the game. |
| Scale | Player is 1.6 m tall and jumps 1.7 m. `Tree_01` is 5.6 m, `Bush_01` 0.8 m, `Rock_01` 0.5 m high, `Stump_01` 0.8 m, `Branch_01` 0.8 m long, `Ground_01` is a 30 × 30 m tile 1.3 m thick. Rocks, stumps and branches are too small to be obstacles at scale 1. |
| Colliders | Every prefab has one: `BoxCollider` on trees, bushes, grass and stumps; `MeshCollider` on rocks, branches and ground tiles. Scenery with colliders would snag the `CharacterController`. |
| LODs | Trees, bushes, grass and stump have a `LODGroup`. Rocks, branch and ground do not. |
| Performance | One atlas and one material for all vegetation, so the SRP Batcher handles it well. |

### GDD coverage

| GDD element (§8.1, §8.3, §8.6, §9.1) | Covered by the package? | Plan |
|---|---|---|
| Trees, vegetation | Yes | `Tree_*`, `Bush_*`, `Grass_*`, `Flowers_*`, `Mushroom_*` |
| Rocks (scenery and obstacle) | Yes, scale up | `Rock_*` (x2 to x3 for obstacles) |
| Ground | Partly | `Ground_*` as visual terrain; walkable lane keeps its own flat collider |
| Logs (static obstacle) | Partly | `Stump_01` and `Branch_01` scaled / stacked |
| Rolling log (dynamic) | Partly | `Branch_01` scaled (visual only; `RollingLog` script is kept) |
| Falling rock | Yes | `Rock_04/05` scaled (visual only; `FallingRock` script is kept) |
| Mud zone | Material only | Brown URP Lit material on a flattened ground tile |
| Mountains | Partly | `Terrain_Ground_02` (a 5.6 m hill) stretched wide and high (done in phase C; fog comes in phase F) |
| **Fences** | **No** | Gap — see section 3 |
| **Bottles, cans, papers** | **No** | Gap — see section 3 |
| Recovery element (new vegetation) | Yes | Flowers / grass / bushes that grow in (Phase E) |
| Polluted look (GDD §8.6 contrast) | No | Needs a polluted material variant (Phase E) |

## 2. Phases

### Phase A — Import hygiene and URP conversion (S) — done
- [x] Commit the raw import first (assets + every `.meta`, as the project rule requires) so every teammate gets the same GUIDs before anything is changed.
- [x] Switch `SimpleNaturePack_Texture_01.mat` and `SimpleNaturePack_BG.mat` to `Universal Render Pipeline/Lit`: atlas into `_BaseMap`, smoothness 0, specular highlights off. Edit the materials in place (do not move the folder) so the GUIDs stay valid.
- [x] Deleted the two `.unitypackage` files and the `Scenes` folder (demo scenes and lighting settings) with their `.meta` files, before the first commit so they never enter history. They were not in Build Settings. The folder went from 3.0 MB to 1.2 MB (106 files).
- [x] Verified with a scene capture (tree, bush, rock, stump, flowers, mushroom): textured, no magenta. `SimpleNaturePack_BG.mat` has no texture and keeps its grey colour (0.68). The two materials are the only vendor files modified.
- **Done when:** every prefab renders correctly in `Level01`, and `git status` shows no loose `.meta` files.

### Phase B — Prefab variants and collider policy (S) — done
- [x] `Assets/Prefabs/Environment/` holds 29 prefab variants (the vendor prefabs are untouched):
  - `Scenery/Scenery_*` (21): trees, bushes, grass, flowers, mushrooms, rocks, stump and branch at scale 1, **no colliders**, layer Default.
  - `Terrain/Terrain_Ground_01–03` (3): the 30 m ground tiles, no colliders.
  - `Obstacles/Obstacle_*` (5): solid colliders, layer `Ground`, scaled so the top stays under 1.2 m (player jump is 1.7 m).
- [x] The walkable lane keeps its own flat colliders; the `Terrain_*` tiles are visual only (they are bumpy and 1.3–5.6 m thick).
- [x] Tests that keep the policy from breaking: `EnvironmentPrefabPolicyTests` (EditMode, 4 tests: scenery and terrain have no colliders, obstacles are solid on `Ground`, obstacle top at most 1.2 m) and `EnvironmentPlayModeTests` (PlayMode, 7 cases: scenery in the lane does not block the player, a solid obstacle does, and each of the 5 obstacles can be jumped over). Totals now: 13 EditMode and 14 PlayMode tests, all passing.

| Obstacle prefab | Source | Scale | Top above ground | Footprint X × Z (m) |
|---|---|---|---|---|
| `Obstacle_Rock_01` | `Rock_01` | x3 | 0.89 m | 2.6 × 2.6 |
| `Obstacle_Rock_04` | `Rock_04` | x1 | 1.12 m | 1.1 × 1.0 |
| `Obstacle_Rock_05` | `Rock_05` | x1 | 1.01 m | 2.9 × 2.2 |
| `Obstacle_Stump_01` | `Stump_01` | x1.4 | 1.14 m | 0.9 × 0.8 |
| `Obstacle_Log_01` | `Branch_01` | x5, rotated 90° on Y | 0.75 m | 0.75 × 4.1 (lies across the lane) |

- Only `Rock_04/05` and the stump are decoration-sized at scale 1; `Rock_02` (0.17 m) and `Rock_03` (0.31 m) are pebbles and are scenery only. A part of each rock is buried (pivot below the surface), so the visible height is lower than the mesh height.
- Wide obstacles (`Rock_01`, `Rock_05`) are jumpable but need a well-timed jump with no margin to spare at full run speed. Keep them for Zone 1–2, and use the narrow ones (`Rock_04`, stump) for tighter spots in Zone 3.

### Phase C — Level layout in three layers and three zones (L) — done
The side camera sits at z = −8 looking at z = 0 with FOV 45, so depth matters.

| Layer | Z range | Content | Colliders |
|-------|---------|---------|-----------|
| Gameplay lane | −1.5 to +1.5 | Path, obstacles, waste | Yes |
| Midground | +3.2 to +11 | Trees, bushes, rocks | No |
| Background | +13 to +45 | Large trees (x1.6–2.6) | No |
| Mountains | about +92 | `Terrain_Ground_02` stretched x1.8 wide and x2.2 high | No |
| Foreground | −4.6 to −2.4 | Only grass, flowers, mushrooms, pebbles | No |

- [x] **Course:** 180 m of lane, x from −12 to 168, in three contiguous 60 m sections: Zone 1 Learning (−12 to 48), Zone 2 Development (48 to 108), Zone 3 Challenge (108 to 168). The player spawns at x = −8. Invisible barriers close both ends so nobody can walk off the course.
- [x] **Lane:** flat, 3 m deep (the player's Z is locked, and the first 6 m version covered half of the screen), 4 m thick so no void shows under its edge, layer `Ground`, material `Lane_Path`. `Ground_Provisional` is gone.
- [x] **Terrain:** rows of the pack's hill tiles in front of and behind the lane, two end caps so there is ground at both ends, a row of mountains, and a flat `BaseGround` slab (`Terrain_Base` material) under everything. The slab is needed because the hill tiles are open surfaces without skirts: the seams between them showed the grey sky-ground through.
- [x] **Zone dressing (GDD §8.2):** static obstacles 3 / 4 / 7 per zone (gaps between obstacles shrink from 12 m in Zone 1 to 5–6 m in Zone 3), and scenery density rising per zone (midground trees per metre 0.25 / 0.33 / 0.42, bushes 0.35 / 0.40 / 0.50, background trees 0.20 / 0.25 / 0.32). The first 26 m of Zone 1 are completely clear (spawn area).
- [x] **Tool:** `Scripts/Editor/LevelEnvironmentBuilder.cs` (own Editor-only asmdef `ForestGuardian.Editor`), menu **Forest Guardian > Level > Build Level01 Environment**. Seeded, so the layout is reproducible; it rebuilds the `Environment` root from the tables at the top of the file, drops scenery on the hills with a raycast (temporary mesh colliders, removed afterwards), marks everything batching-static, places the player spawn, and resizes `CameraBounds` to the course plus 2 m at each end. **Running it again discards hand edits under `Environment`.**
- [x] **Hierarchy:** `Environment/{Lane, Terrain, Zone1_Learning, Zone2_Development, Zone3_Challenge}`, each zone with `Obstacles`, `Midground`, `Background`, `Foreground`.
- [x] **Provisional hazards relocated** to their zones (the art swap is still phase D): mud at x = 70 and the rolling log between x = 88 and 102 (Zone 2), the falling-rock trigger at x = 126 and the rock at x = 130 (Zone 3).
- [x] **Camera check:** rendered the game camera at the start, the middle of each zone and the end: forest on every frame, no void at either end, lane readable. About 800 renderers; LODs on trees and bushes keep the cost down (tris counted with every LOD: about 190 k).
- [x] **Tests:** `LevelLayoutPlayModeTests` (5): continuous flat lane over the whole course, colliders only on lane / barriers / obstacles, spawn on the lane near the start, camera confiner covers the course, and the obstacle count grows in every zone. Totals: 13 EditMode and 19 PlayMode, all passing.
- **Open for later:** there is no goal object at the end of the course yet (the win condition is the waste target, GDD §5.3, built in the ActionPlan "collection loop" phase). Waste placement waits for the Blender models.

### Phase D — Replace the provisional hazards with art (M) — done
- [x] **Hazard prefabs** in `Assets/Prefabs/Hazards/`; the gameplay scripts (`RollingLog`, `FallingRock`, `FallingRockTrigger`, `MudZone`, `PlayerHazard`) keep their behaviour and the art sits on `Visual` children:
  - `RollingLog_Hazard`: `Branch_01` x5 turned 90° so its axis runs along Z and it rolls along X (the camera sees its end face, like a rolling barrel). Capsule trigger along the whole log on the root (stumbles), plus a solid capsule child `Body` so the log blocks the player.
  - `FallingRock_Hazard`: a container with `Rock` (`Rock_04` x1.2 plus `FallingRock`), `WarningMarker` (the ground shadow) and `Trigger` (arms the rock, 4 m before it). The scripts' references are wired inside the prefab, so it can be dropped anywhere as a unit.
  - `MudZone_Hazard`: a 6 x 3 m wet slab (`Mud_Wet`, dark brown, matte) across the lane with four pebbles on top, and a trigger box on the root.
- [x] **Visual language, "dark means avoid":** a new `Obstacle_Atlas` material (the pack atlas tinted darker) is used by all five static obstacles and by the log and the falling rock, so they never read as scenery rocks and stumps. Waste will be the opposite: bright, saturated and never found in nature (phase F).
- [x] **`Level01`:** `Hazards_Provisional` is replaced by a `Hazards` group with one of each prefab: mud at x = 70 and rolling log between x = 88 and 102 (Zone 2), falling rock at x = 130 (Zone 3). More can be dragged in from the prefabs. The old `Hazard_Mud`, `Hazard_Log`, `Hazard_Rock` and `Ground_Provisional` materials were deleted (nothing used them); `Hazard_Marker` is kept for the warning shadow.
- [x] **Hardening:** `MudZone` now remembers the player it slowed, so the speed modifier cannot stack when Unity reports an enter without an exit (a test that toggled the `CharacterController` reproduced it: the speed stayed at 0.5 after leaving the mud).
- [x] **Tests (first coverage of the hazards):** `HazardPlayModeTests` (PlayMode, 3): mud halves the speed and releases it on exit, the rolling log stumbles the player, and the falling rock goes warning, then drop, then disappear. `EnvironmentPrefabPolicyTests` (EditMode) gained two: obstacles use the dark material, and every hazard collider is a trigger on the `Hazard` layer. Totals: 15 EditMode and 22 PlayMode, all passing.
- **Open for later:** the rolling log's cross-section is not perfectly round (the pack's branch has a nub), so it wobbles slightly as it spins; a clean cylinder log from Blender would fix it. Only one hazard of each type is placed; the real difficulty tuning (counts and timing) belongs to the playtests.

### Phase E — Polluted-to-recovered visual state (M)
GDD §6.1.4, §8.6 and §9.1 ask the player to see a zone go from polluted to recovered.
- [ ] Three material stages of the same atlas: `Nature_Polluted` (desaturated, brownish), `Nature_Partial`, `Nature_Healthy` (the package default). Swapping **shared materials** keeps the SRP Batcher working; avoid per-renderer property blocks.
- [ ] `ZoneRecovery` component (`Scripts/Environment/`): listens to `GameSession.WasteCollected`, computes progress as `collected / target`, applies the stage to the scenery of its zone and scales in the "recovery" props (flowers, grass, bushes from 0 to 1). The progress rule goes in pure C# so it can be unit-tested.
- [ ] Win state: all zones fully healthy.
- [ ] Not building a water shader or custom Shader Graph (scope, GDD §12.3). If wanted later, treat it as stretch.
- **Done when:** collecting waste visibly recovers the forest; EditMode tests cover the progress rule; the win state shows a fully green forest.

### Phase F — Lighting, atmosphere and readability (S–M) — done
- [x] **Tool:** `Scripts/Editor/LevelAtmosphereSetup.cs`, menu **Forest Guardian > Level > Apply Level01 Atmosphere**. Constants at the top, safe to run repeatedly (it updates, never deletes). The Editor asmdef now references the URP runtime assemblies.
- [x] **Sun:** warm (1, 0.95, 0.84), intensity 1.1, soft shadows at strength 0.85 (below 1 so shaded faces stay readable), yaw 330 so it comes from the camera side: lane faces are lit and shadows fall behind the player.
- [x] **Sky and ambient:** a `Level01_Sky` procedural skybox with a neutral tint (a blue tint made the shader paint a yellow band on the horizon; the tint is the scattering complement), and a trilight ambient gradient so the lighting does not depend on the sky.
- [x] **Fog:** linear, from 24 m to 150 m, in the same pale blue as the horizon. The camera is about 8 m from the lane, so the player, obstacles and hazards are never hazed; the far trees fade a little and the mountains dissolve into the sky, which gives the depth the scene was missing.
- [x] **Post-processing:** the main camera now renders post-processing with SMAA (the URP asset has MSAA off). A global volume, `Level01 Volume`, uses the new asset `Assets/Config/Level01_VolumeProfile.asset`: neutral tonemapping, contrast +8, saturation +10, bloom threshold 1 / intensity 0.15, vignette 0.15. **No** motion blur, film grain, chromatic aberration or lens distortion (GDD §11). The shared `Assets/Settings/DefaultVolumeProfile.asset` is untouched.
- [x] **Camera check** at the start and in the middle of each zone: gameplay objects crisp, background softly hazed, horizon without colour bands, lane readable. The measurement was visual only; no contrast ratios were computed.
- [x] **Tests:** `LevelAtmospherePlayModeTests` (PlayMode, 4): fog on and starting past the lane, the level uses its own volume profile with post-processing on, no disorienting effects in the profile, and the sun casts shadows. Totals: 15 EditMode and 26 PlayMode, all passing.

**Palette rule for waste (to apply when the Blender models arrive).** The rule in GDD §8.6 is "nature in greens and browns, waste clearly different". The pack does not fully respect that: it has saturated accents of its own, namely **red and orange mushroom caps, blue flowers and pink or red flowers**. So the waste palette must avoid red, orange, pink and blue, and use:

| Waste | Base colour | Why it stands out |
|---|---|---|
| Bottles | Clear cyan-white, slightly translucent | Light and cool; no flower is cyan-white |
| Cans | Silver / bright yellow | Metallic or yellow; nothing in the pack is yellow |
| Paper | Pure white | Brightest thing on screen against green and brown |

Colour alone is not enough (WCAG 1.4.1, GDD §11.3), so every collectible also gets a cue that does not depend on hue: a gentle bob and spin, an emissive rim that triggers the bloom (threshold 1), and a larger size than any flower or mushroom. Obstacles and hazards stay dark (`Obstacle_Atlas`), so the whole language is: dark means avoid, bright and moving means collect.

### Phase G — Licences, GDD inventory and QA (S)
- [x] GDD §13.1 rows (trees and vegetation, rocks, part of logs) filled with the Asset Store name, publisher, link and licence. Mountains stay "Por verificar". Still to do: read the EULA terms on redistribution, because the package now lives in a public-facing team repo.
- [ ] Editor validator (Editor asmdef): scenery colliders = 0, no renderer using an error or Standard shader, no missing scripts, lane free of scenery, `CameraBounds` covers the course.
- [ ] Re-run the 9 EditMode and 7 PlayMode tests; add one PlayMode test that walks the whole lane and reaches the goal.
- [ ] Update `CLAUDE.md` (environment folders, collider policy, vendor folders untouched) and mark this plan done.

## 3. Gaps the package does not cover

| Missing | GDD | Options (pick one per item) |
|---------|-----|------------------------------|
| Bottles, cans, papers | §8.4 (core to the game) | (a) Model them in Blender (simple low-poly shapes, no licence risk); (b) Kenney / Quaternius / Poly Pizza, checking the licence; (c) temporary primitives with a coloured material until art arrives |
| Fences | §8.3 | Kenney Nature Kit (CC0) or build from `Branch_01` rails on `Stump_01` posts |
| Mountains | §8.1 | Solved in phase C with the stretched `Terrain_Ground_02` hill tile; revisit only if they look too soft once the lighting pass (phase F) is done |
| Rolling log mesh | §9.1 | `Branch_01` scaled up, or a simple cylinder log from Blender |

Decision: waste is modelled in Blender by Elianis, later; until then the collectibles can use coloured primitives. Recommendation for the rest: waste modelled in Blender (smallest, and it keeps the "same shape language" as the pack); fences from `Branch_01`/`Stump_01` first; mountains from scaled rocks under fog.

## 4. Open questions for the team

1. ~~Asset name, publisher, link and licence~~ — answered: JustCreate, link and EULA recorded in the GDD §13.1.
2. ~~Delete the `.unitypackage` files and demo scenes~~ — done.
3. ~~Source for waste~~ — answered: the team (Elianis) will model bottles, cans and papers in Blender later. Fences are still open (proposal: `Branch_01` rails on `Stump_01` posts).

## 5. Risks

| Risk | Mitigation |
|------|-----------|
| Magenta materials reach `main` | Phase A is first; the validator in Phase G fails on Standard shaders |
| Scenery colliders snag the player | Phase B strips them; the validator counts them |
| Obstacles taller than the jump | Rule: obstacle top at most about 1.2 m; the PlayMode lane test covers it |
| Editing vendor prefabs breaks future updates | Only variants in `Assets/Prefabs/Environment`; `Assets/SimpleNaturePack` stays as imported (except the 2 materials) |
| Build rewrites shared URP settings again | Own `Level01` volume profile; check `git status` after every build |
| Scene file merge conflicts while three people edit `Level01` | One zone per prefab / sub-scene, agree who owns `Level01.unity` at a time |
| Art style mismatch (stock pack vs. Mixamo character vs. Blender waste) | Phase F palette pass; keep flat colours and no outlines for everything |

## 6. Decision log

- Use the existing extracted folder and fix its two materials in place, instead of importing the URP `.unitypackage` as a second copy (same GUIDs, risk of duplicates). *(proposed; confirm)*
- Scenery never has colliders; the walkable lane is built from explicit flat colliders. *(proposed; confirm)*
- Hazard scripts stay untouched; art goes on a `Visual` child. *(proposed; confirm)*
