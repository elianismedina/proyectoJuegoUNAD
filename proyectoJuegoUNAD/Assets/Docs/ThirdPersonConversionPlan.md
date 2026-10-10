# Guardianes del Bosque — Third-Person Conversion Plan

Decided 2026-10-09 (Eli): the game moves from a 2.5D side-scroller to a **third-person 3D** game. The player walks a winding trail through the forest with the camera behind them, and the waste is no longer lying on the path: it sits off the trail, up on rocks, across streams and next to hazards, so collecting it takes a detour, a jump or a risk.

This reverses decision 6 of `ActionPlan.md` ("2.5D side-scroller"). The objective, rules and win/lose conditions of the GDD do not change: collect 10 waste, reach the goal, falling off the course loses.

## Why (design principles review)

The review against *Game Design Principles: Working Guide for Claude* found that the weakest principle was **risk and reward**: the lane had no gaps (nobody could lose), and all waste was on the line of travel (it was picked up without any decision). A trail through an open forest turns "decide the route" (GDD §6.3 core loop) into a real choice: stay on the safe trail, or step off it for the waste.

## Controls (GDD §6.7 to update)

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move (relative to the camera) | W A S D / arrows | Left stick |
| Look around (optional) | Mouse | Right stick |
| Jump | Space | South button |
| Pause | Esc | Start |

The camera recenters behind the player on its own after a short pause, so a player who never touches the mouse can still play (accessibility: GDD §11.4, simple controls by default).

## Phases

### Phase 1 — Player and camera (M)
- [x] `PlayerController` (renamed from `SideScrollerController`, same file GUID so the prefab keeps its reference): camera-relative movement on X and Z, turns toward its travel direction, keeps coyote time, jump buffer, speed modifiers and input gating by game state. `Stumble` now takes a world direction instead of an X sign.
- [x] Cursor locked while playing, released on pause, win and lose so the screens can be used with the mouse.
- [x] `ThirdPersonCameraSetup` (menu *Forest Guardian > Player > Set Up Third Person Camera*): turns the `CM Side Camera` of `Level01` into `CM Player Camera` with Orbital Follow (sphere, radius 6), Rotation Composer and Input Axis Controller (mouse / right stick), recentering behind the player after 1.5 s idle. Removes the side-view Position Composer and Confiner.
- [x] PlayMode tests: movement follows the camera's forward and right, the player faces its travel direction, moves on both axes.

### Phase 2 — Forest trail level (L)

Phases 2–4 are written but have not been run in the Editor yet: open `Level01`, run the builder, then the camera setup, save, and run the tests. The builder creates `Assets/Generated/` (two meshes) and `Assets/Materials/Stream_Water.mat`; commit them with their `.meta` files.

- [x] `LevelEnvironmentBuilder` rewritten (same menu, *Forest Guardian > Level > Build Level01 Environment*): a winding trail from a table of waypoints (Catmull-Rom), a 4 m dirt path about 281 m long, flush with a generated forest-floor mesh (no raised slab), split into the same three zones by trail distance. The first 30 m still run along +X from x = −12, so the start matches the old course. A `ForestTrail` component stores the centre line for tests and runtime code.
- [x] The playable area is a 12 m corridor around the trail plus five clearings (always on the outer side of a bend); invisible walls (`PlayAreaBounds`) behind a line of trees keep the player inside, and the corridor narrows to the path at the goal so it cannot be walked around. Trees inside the corridor get a `Trunk` capsule collider; the scenery prefabs stay collider-free.
- [x] Vertical variety: three earth banks across the corridor (0.8, 1.0 and 1.2 m), and from Zone 2 on, three streams across the trail (2.0, 2.2 and 2.5 m). Falling in loses (kill plane raised to y = −2).
- [ ] Camera Deoccluder so trees between the camera and the player do not hide them.
- [x] `JumpShadow` on the player: a disc straight below them on the ground, smaller the higher they are and hidden over a stream, so jumps can be judged in 3D (the sun's shadow falls at an angle).

### Phase 3 — Waste off the trail (M)
- [x] 12 waste items (target 10, two spare), placed by the builder: two lie on the path in Zone 1 to teach collecting; the rest are high in clearings (jump), on a rock ledge, low over the three streams, in the rolling log's run, beside the falling rock and high on the last bank.
- [ ] Navigation hint (GDD §11.5): a subtle footprint trail or sparkle pointing to the nearest waste still missing, so detours never leave a child lost.
- [x] Tests: count per zone, inside the play area, reachability from the surface below, at most two "free on the path" and only in Zone 1, collecting all and reaching the goal wins.

### Phase 4 — Hazards on the trail (M)
- [x] `RollingLog` rolls along its own local X (`travel` metres centred on where it is placed), so it can run along or across the trail.
- [x] Mud across the path (can be walked around: a choice between time and a detour); the falling rock guards a waste item.
- [x] Hazard tests read positions and directions from the scene.

### Phase 5 — Docs and cleanup (S)
- [ ] GDD §6 (controls, core loop), §8 (level structure) and `ActionPlan.md` decision 6 updated; `CLAUDE.md` updated.
- [ ] Remove `CameraBounds` (only the old side view used it) once the camera setup has been run on `Level01`.

## Risks

| Risk | Mitigation |
| --- | --- |
| Free 3D camera is harder for 10–14 year olds | Auto-recentering behind the player; mouse look is optional |
| Players get lost off the trail | Trail always visible, navigation hint, bounded play area |
| More scenery colliders cost performance | Simple capsule colliders on trunks only; leaves and bushes stay collider-free |
| Scene merge conflicts during the rebuild | Level built by an Editor tool from tables; one person edits `Level01` at a time |
