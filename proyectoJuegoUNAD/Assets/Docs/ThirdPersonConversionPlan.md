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
- [ ] `ForestTrailBuilder` (replaces `LevelEnvironmentBuilder`): a winding trail from a table of waypoints, about 4 m wide and 250 m long, flush with the forest floor (no raised slab), split into the same three zones (Learning, Development, Challenge).
- [ ] The playable area is the trail plus side clearings; dense trees and rocks with trunk colliders, and invisible walls behind them, keep the player inside. Scenery that can be touched gets colliders (this replaces the "scenery never collides" rule of the side-scroller).
- [ ] Vertical variety: low banks and rock steps (≤ 1.2 m, jumpable), and from Zone 2 on, streams and ravines that cross the trail (≤ 2.5 m, jumpable with coyote time). Falling in loses (kill plane raised to y = -3).
- [ ] Camera Deoccluder so trees between the camera and the player do not hide them.

### Phase 3 — Waste off the trail (M)
- [ ] 12 waste items (target 10, two spare): at most 3 lie on the trail (Zone 1, to teach collecting). The rest are in side clearings, on top of rocks and stumps, across a stream, beside the rolling log and under the falling rock.
- [ ] Navigation hint (GDD §11.5): a subtle footprint trail or sparkle pointing to the nearest waste still missing, so detours never leave a child lost.
- [ ] Tests: count per zone, reachability (height from the surface below), at most 3 "on the trail at foot height", collecting all and reaching the goal wins.

### Phase 4 — Hazards on the trail (M)
- [ ] `RollingLog` rolls between two points in any direction (down a slope across the trail), not only along X.
- [ ] Mud patches on the trail and in clearings; the falling rock guards a waste item.
- [ ] Hazard tests updated to the trail positions.

### Phase 5 — Docs and cleanup (S)
- [ ] GDD §6 (controls, core loop), §8 (level structure) and `ActionPlan.md` decision 6 updated; `CLAUDE.md` updated.
- [ ] Remove `LevelEnvironmentBuilder`, `CameraBounds` and the side-view tests once the trail level replaces them.

## Risks

| Risk | Mitigation |
| --- | --- |
| Free 3D camera is harder for 10–14 year olds | Auto-recentering behind the player; mouse look is optional |
| Players get lost off the trail | Trail always visible, navigation hint, bounded play area |
| More scenery colliders cost performance | Simple capsule colliders on trunks only; leaves and bushes stay collider-free |
| Scene merge conflicts during the rebuild | Level built by an Editor tool from tables; one person edits `Level01` at a time |
