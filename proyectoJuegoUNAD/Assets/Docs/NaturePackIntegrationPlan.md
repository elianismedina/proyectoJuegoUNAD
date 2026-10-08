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
| **Mountains** | **No** | Gap — see section 3 |
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

### Phase C — Level layout in three layers and three zones (L)
The side camera sits at z = −8 looking at z = 0 with FOV 45, so depth matters.

| Layer | Z range | Content | Colliders |
|-------|---------|---------|-----------|
| Gameplay lane | −1 to +1 | Path, obstacles, waste | Yes |
| Midground | +3 to +10 | Trees, bushes, rocks, mushrooms | No |
| Background | +12 to +40 | Large trees, scaled rocks as mountains, fog | No |
| Foreground | −3 to −5 | Only low grass and flowers | No (must never hide the lane) |

- [ ] Replace `Ground_Provisional` with the real lane and three zone sections, sized to the real course (and resize `CameraBounds` to the course plus 2 m at each end).
- [ ] Zone dressing per GDD §8.2: **Zone 1** open, clean and sparse (few obstacles, wide spaces); **Zone 2** denser vegetation and more rocks; **Zone 3** crowded, obstacles close together.
- [ ] Editor tool `Scripts/Editor/SceneryScatter.cs` (own Editor-only asmdef): seeded random scatter inside a zone volume, so layouts are reproducible and reviewable. Static flags set for batching.
- [ ] Organise hierarchy as `Environment/Zone1|2|3/{Lane,Midground,Background}`.
- **Done when:** a full play-through from start to goal shows a forest on every frame, no empty void at either end, and the lane readable at a glance.

### Phase D — Replace the provisional hazards with art (M)
- [ ] `Hazards_Provisional` primitives become real prefabs under `Assets/Prefabs/Hazards/`. Keep the existing scripts (`RollingLog`, `FallingRock`, `FallingRockTrigger`, `MudZone`, `PlayerHazard`) unchanged: put the mesh on a `Visual` child and keep the gameplay collider on the root, sized by hand.
- [ ] Static obstacles: rocks (scaled `Rock_*`), logs (`Stump_01` / stacked `Branch_01`).
- [ ] `MudZone`: flattened ground tile with a new brown URP Lit material, visibly different from grass (GDD §9.1: "shader o material distinto").
- [ ] Obstacles must read as obstacles: darker, higher contrast than scenery rocks, and a different silhouette (GDD §8.6 and the accessibility rules in §11).
- **Done when:** every hazard type is placed with final art and still behaves as before (stumble, slow, fall).

### Phase E — Polluted-to-recovered visual state (M)
GDD §6.1.4, §8.6 and §9.1 ask the player to see a zone go from polluted to recovered.
- [ ] Three material stages of the same atlas: `Nature_Polluted` (desaturated, brownish), `Nature_Partial`, `Nature_Healthy` (the package default). Swapping **shared materials** keeps the SRP Batcher working; avoid per-renderer property blocks.
- [ ] `ZoneRecovery` component (`Scripts/Environment/`): listens to `GameSession.WasteCollected`, computes progress as `collected / target`, applies the stage to the scenery of its zone and scales in the "recovery" props (flowers, grass, bushes from 0 to 1). The progress rule goes in pure C# so it can be unit-tested.
- [ ] Win state: all zones fully healthy.
- [ ] Not building a water shader or custom Shader Graph (scope, GDD §12.3). If wanted later, treat it as stretch.
- **Done when:** collecting waste visibly recovers the forest; EditMode tests cover the progress rule; the win state shows a fully green forest.

### Phase F — Lighting, atmosphere and readability (S–M)
- [ ] Directional light, ambient colour, skybox / fog colour tuned in `Level01`. Create a **new** `Level01` volume profile instead of editing `Assets/Settings/DefaultVolumeProfile.asset`: Unity rewrites the shared defaults during builds and those edits kept leaking into commits before.
- [ ] Contrast rule (GDD §8.6): nature uses greens and browns; waste uses saturated colours that never appear in nature (blue, white, yellow, red). Obstacles and waste must also be distinguishable without colour (shape, bobbing, a sparkle on waste), per WCAG 1.4.1 in GDD §11.3.
- [ ] Check the lane against the camera at the resolutions the game targets.
- **Done when:** a first-time viewer can tell apart scenery, obstacles and waste in a still screenshot.

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
| Mountains | §8.1 | Large scaled `Rock_*` in the background behind fog; or a cone-shaped low-poly mesh |
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
