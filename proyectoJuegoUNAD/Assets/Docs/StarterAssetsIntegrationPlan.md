# Starter Assets (Third Person) Integration Plan

Goal: use the imported **Starter Assets – First Person + Third Person Character Controllers** to drive our existing `Player` (`Assets/Models/Player.fbx`) in `Level01`, adapted to the GDD's **3D look with side-scroller movement**.
Language rule: all code, comments and commit messages in English.
Parent plan: `ActionPlan.md` (this replaces the custom Rigidbody movement planned for Phase 2).

## 1. What was imported (audit)

| Item | Finding | Impact |
|------|---------|--------|
| Location | `Assets/Starter Assets/` (~93 MB; `Runtime/`, `Sample/`, `Editor/`, `TutorialInfo/`) | Mostly sample content; the runtime we need is small. |
| `ThirdPersonController.cs` | Requires `CharacterController` + `PlayerInput`. Movement is **camera-relative free 3D** (`_mainCamera.transform.eulerAngles.y`), mouse/stick **look** drives a Cinemachine target (`PlayerCameraRoot`). Grounded check by sphere, gravity, jump timeout, fall timeout. | Needs a side-scroller adaptation (see §3). |
| `StarterAssetsInputs.cs` | `OnMove / OnLook / OnJump / OnSprint` via `PlayerInput` (Send Messages). | Reusable as is. |
| `StarterAssets.inputactions` | Move (WASD **and** arrow keys + left stick), Look, Jump (Space / South button), Sprint. | Already covers the GDD controls (A/D, ←/→, Space). No Pause action yet. |
| Animator | `StarterAssetsThirdPerson.controller`: parameters `Speed`, `MotionSpeed`, `Grounded`, `Jump`, `FreeFall`; states Idle/Walk/Run blend, JumpStart, InAir, JumpLand. Clips are **Humanoid** FBX. | Our `Player.fbx` is **Generic** → must become Humanoid to reuse them. |
| Cinemachine | `com.unity.cinemachine` 3.1.7 was added to `manifest.json`. Prefab `PlayerFollowCamera`. | Use it for the side camera. |
| SFX | 10 footsteps + landing WAV in `Character/Sfx` | Covers GDD `SFX_Pasos_Cesped` and `SFX_Aterrizaje`. |
| Extras | `BasicRigidBodyPush`, Mobile touch UI, FirstPerson controller, `Sample/` Playground scenes, 13 URP pipeline assets in `Runtime/Settings`, `URPWizard`, deploy menus. | Not needed for the game; some can alter project graphics settings. |
| Project state | `activeInputHandler = 1` (Input System only) → compatible. Two input assets exist: `InputSystem_Actions` (leftover drone actions such as `Altitude`) and `StarterAssets.inputactions`. | Pick one (see §2). |
| Git state | `Starter Assets/`, `Models/`, `Materials/`, `Scripts/Player/` are untracked; `manifest.json`, `packages-lock.json`, `Level01.unity` are modified. | Commit with `.meta` files (project rule). |

## 2. Decisions

| # | Decision | Recommendation |
|---|----------|----------------|
| D1 | Which controller drives the player | **Starter Assets `ThirdPersonController`, adapted.** Remove our Rigidbody `PlayerController`, because a `CharacterController` and a `Rigidbody` capsule must not coexist on the same object. |
| D2 | Modify vendor script or wrap it | **Copy it** to `Scripts/Player/SideScrollerController.cs` (our namespace, English comments) and leave `Starter Assets/` untouched so package updates stay possible. |
| D3 | Character model | **Keep our `Player.fbx`** (GDD §7.1 "uses the defined model"), set to Humanoid, reuse the Starter Assets animator and clips. Do *not* use the bundled `Armature.fbx`. |
| D4 | Input asset | Use **`StarterAssets.inputactions`** on the player's `PlayerInput`; add a `Pause` action. Delete `InputSystem_Actions` once nothing references it (drone leftovers). |
| D5 | Camera | **Cinemachine 3** `CinemachineCamera` following the player, fixed side rotation, no mouse look. |
| D6 | Package clean-up | Keep `Runtime/ThirdPersonController`, `Runtime/InputSystem`, `Runtime/Common`; remove `FirstPersonController`, `Mobile`, `Sample`, `Settings` only after the integration works. |

## 3. Required adaptations (why the stock controller is not enough)

1. **Constrain to the course plane.** Movement uses only `input.move.x`; direction is fixed to world ±X; `transform.position.z` is locked each frame (CharacterController has no axis constraints).
2. **Facing.** Replace camera-relative `_targetRotation` with yaw ±90° (smoothed) toward the movement direction.
3. **Remove look.** Delete `CameraRotation()`, mouse look, cursor locking; the camera is fixed side-on and driven by Cinemachine.
4. **Jump feel.** Add coyote time (0.2 s) and a jump buffer (0.15 s) per GDD §11.4, replacing the stock `JumpTimeout` semantics.
5. **Sprint.** Not in the GDD: disable or hide (single run speed ≈ 5 m/s).
6. **Mud.** Expose `SpeedMultiplier` (default 1) so `MudZone` can slow the player and set the `Slow` animation.
7. **Hazard hooks.** Add `Stumble()` (brief knockback + animator trigger, input lock ~0.5 s). `CharacterController` is not pushed by physics, so hazards must call it explicitly via triggers or `OnControllerColliderHit` — not rely on rigidbody forces.
8. **Kill plane / state.** Subscribe to `GameManager` state: ignore input when `Paused`, `Won` or `Lost`.
9. **Events instead of animation events only.** Keep Starter Assets' `OnFootstep` / `OnLand` animation events (they drive the SFX), but route volume through the future `AudioManager`.

## 4. Phases

### Phase A — Repository safety and cleanup (S)
- [x] Create a branch (`feature/starter-assets-player`) (branched from `elianis-medina`).
- [x] Commit, in separate commits, with their `.meta` files: (1) `Packages/manifest.json` + `packages-lock.json` (Cinemachine), (2) `Assets/Starter Assets/`, (3) `Models/`, `Materials/`, `Level01.unity`, `Scripts/Player/`.
- [x] **Do not run** `Tools > Starter Assets > Reset…`, the URP Wizard, or the deploy menus on the project (they instantiate prefabs / touch pipeline settings).
- [x] Verify the console is clean after import: 0 errors; only CS0618 deprecation warnings inside the vendor `ThirdPersonStarterAssetsDeployMenu.cs` (ignored; file is slated for removal in Phase G).
- [~] Record the package origin and license in GDD §13.1: origins are now recorded (character: Mixamo "Timmy"; controller/animations/SFX: Unity Starter Assets). **The license column still says "por verificar"**: confirm the Mixamo terms and the Starter Assets license text, and archive them with the download date.

### Phase B — Make the model Humanoid (S)
- [x] `Player.fbx` → Rig → Animation Type **Humanoid**, Avatar from this model (`PlayerAvatar`: valid, human).
- [x] Verified retargeting: Starter Assets `Stand--Idle` and `Locomotion--Run_N` render correctly on the model. (Bind pose is still the crouched run pose, but it is overridden by any animator clip.)
- [x] Material extracted to `Assets/Materials/Player_Body.mat` (URP Lit, FBX remapped to it). The FBX embeds 4 textures (Diffuse, Normal, Specular, Glossiness) that Unity had not linked; they were extracted to `Assets/Textures/Player/` and the Diffuse (base map) and Normal (as a normal map) are assigned. Specular/Glossiness are unused (URP Lit metallic workflow: metallic 0, smoothness 0.25).
- [x] The embedded `mixamo.com` clip is ignored (the Player prefab will use the Starter Assets animator).

**Done when:** the avatar is valid (green in the Avatar window) and the model plays a Starter Assets clip in a test Animator.

### Phase C — Player prefab (M)
- [x] Create `Assets/Prefabs/Player/Player.prefab`:
  - Root `Player` (tag `Player`): `CharacterController` (height ≈ 1.6, radius ≈ 0.3, center y = height/2, skin width 0.03, step offset 0.25), `PlayerInput` (actions = `StarterAssets.inputactions`, behaviour *Send Messages*), `StarterAssetsInputs`, `SideScrollerController`, `AudioSource`.
  - Child `Model` (our `Player.fbx`) with `Animator` → `StarterAssetsThirdPerson.controller` + our Avatar, root motion **off**.
  - Child `PlayerCameraRoot` at head height (Cinemachine follow target).
- [x] Removed the Rigidbody player from the scene; deleted `Scripts/Player/PlayerController.cs` and `Materials/PlayerNoFriction.asset`.
- [x] Assign footsteps/landing clips to the controller, set `GroundLayers` to the ground layer (create layers `Ground`, `Hazard`, `Collectible`).
- [x] Replace the scene's `Player` with the prefab instance on the provisional ground.

**Done when:** the player stands on `Ground_Provisional`, idles, with no console errors.

**Implementation notes (done):**
- Prefab: `Assets/Prefabs/Player/Player.prefab`. `CharacterController` height 1.6 / radius 0.3 / center y 0.8 (feet at the origin); `PlayerCameraRoot` at y 1.3.
- The `Animator` sits on the `Model` child, so a small `PlayerAnimationEvents` relay forwards the `OnFootstep` / `OnLand` animation events (Unity delivers them to the Animator's own GameObject) to the controller on the root.
- Layers created: `Ground` (7), `Hazard` (8), `Collectible` (9). `Ground_Provisional` is on `Ground`; the controller's `GroundLayers` mask is `Ground` only.
- `StarterAssetsInputs` is set to `cursorLocked = false` and `cursorInputForLook = false`.

### Phase D — Side-scroller adaptation (M)
- [x] Implement `SideScrollerController` per §3 items 1–6, 8 (copy of the stock script, trimmed). Item 8 is exposed as `InputEnabled`; `GameManager` will drive it in Phase F.
- [x] Add `Pause` action to `StarterAssets.inputactions` (Esc / Start) — consumed by the future pause menu.
- [x] Tuned: `MoveSpeed` 6 (the blend tree reaches the full Run clip at 6), `JumpHeight` 1.7, `Gravity` −20, `SpeedChangeRate` 10, `RotationSmoothTime` 0.08, coyote 0.2 s, jump buffer 0.15 s. Re-check `JumpHeight` against real obstacle heights in Phase 4.
- [x] Verified animator parameters in Play mode: `Speed`, `Grounded`, `Jump`, `FreeFall` (see results below). Walk/run blend untouched; at speed 6 the character plays the full Run clip.

**Play-mode test results (scripted, input values injected into `StarterAssetsInputs`):**

| Check | Result |
|-------|--------|
| Spawn settles on ground | y = 0.03, `Grounded` = true |
| Jump press | `Jump` param true, `Grounded` false |
| Jump apex | 1.68 m (target 1.7) |
| Mid-air press | no double jump; lands and resets `Jump` / `FreeFall` |
| Run left | yaw 270°, `Speed` = 6.00, **z = 0.000** |
| Run right past the ground edge | z stays 0.00, `FreeFall` true, y decreases (kill plane is Phase F) |
| Stop | `Speed` → 0.00 within 1 s |

Not yet tested with a physical keyboard/gamepad (values were injected), and the `Pause` action has no consumer yet.

**Done when:** A/D and ←/→ run along X, Space jumps with coyote time and buffer, the model faces travel direction, and Z never changes.

### Phase E — Camera (S)
- [x] Add `CinemachineBrain` to `Main Camera`; create a `CinemachineCamera` (Follow = `PlayerCameraRoot`, Position Composer, fixed rotation ≈ (5°, 0°, 0°), distance 8 (player ≈ 25% of screen height), damping ≈ 0.3 s, dead zone for small jumps).
- [x] Confine camera to the level (Cinemachine Confiner, or clamp X to the course bounds) so it never shows beyond the ends.
- [x] Remove the temporary fixed camera position from the previous setup.

**Implementation notes (done):**
- `Main Camera` has a `CinemachineBrain` (cut blend). `CM Side Camera`: `CinemachineCamera` (FOV 45) + `CinemachinePositionComposer` (distance 8, damping 0.5/0.3, screen position y −0.1, dead zone 0.06 × 0.25) + `CinemachineConfiner3D`. Fixed rotation (3°, 0, 0); no rotation control, no mouse look.
- Confiner volume: scene object `CameraBounds` (trigger `BoxCollider`, size 40 × 5 × 4 at (0, 3.5, −8)) → camera position limited to x ±20, y 1–6. **Provisional values for the 60 m test ground (x ±30); resize them to the real course.** At 21:9 the view half-width is ~7.7 m at distance 8, so x ±20 is safe up to a 30 m half-length with margin.
- The camera's start pose (0, 1.1, −8) is its settled pose, so there is no glide when the level starts.
- The confiner's y floor (1.0) also stops the camera from following the player down when falling off the course.

**Play-mode results:** at full run speed the camera lags ≈ 1 m (player viewport x 0.44–0.56); at both course ends it stops at x = ±20.00 with the player at viewport x 0.20 / 0.80 and always inside the frame (y 0.49–0.69 including a jump); camera z and pitch stay fixed.

**Done when:** the camera tracks the player smoothly through all three zones with no mouse input.

### Phase F — Hazard and game hooks (M, aligns with ActionPlan Phases 3–4)
- [x] `Stumble()` and speed-modifier API (`AddSpeedModifier` / `RemoveSpeedModifier`) used by `RollingLog`, `FallingRock`, `MudZone`; animator parameters/states `Slow`, `Stumble`, `Victory` added in `Assets/Animation/PlayerAnimator.controller` (copy of the Starter Assets controller). **The clips are placeholders** from Starter Assets (`Slow` = Walk, `Stumble` = Run_N_Land, `Victory` = Jump); swap the three `motion` fields for Mixamo Humanoid clips later.
- [x] `KillPlane` calls `GameManager` → `Lost`; the controller disables input on `Lost` / `Won` / `Paused` (and `PlayerPauseInput` toggles pause from the Pause action).
- [x] `Victory` animation trigger on win (zone-recovery reaction comes with `ZoneController`).
- [ ] Footstep/landing SFX routed through `AudioManager` mixer groups. *Deferred: `AudioManager` does not exist yet (ActionPlan Phase 6); the controller still plays the Starter Assets clips directly.*

**Implementation notes (done):**
- New scripts: `Gameplay/GameSession` (pure C#, tested), `GameManager`, `LevelConfig`, `KillPlane`; `Environment/PlayerHazard`, `RollingLog`, `FallingRock`, `FallingRockTrigger`, `MudZone`; `Player/PlayerPauseInput`. Scripts now compile into the `ForestGuardian` assembly.
- Stumble: input locked 0.5 s, 5 m/s knockback away from the hazard, 3 m/s hop, then 1 s of immunity so overlapping hazards cannot chain-stumble. No damage.
- Speed modifiers are a list, so overlapping mud zones multiply and always restore on exit.
- Provisional test content in `Level01` (`Hazards_Provisional`): mud x 5–11, rolling log x 14–22, falling rock landing at x −8 armed by a trigger at x −12. Replace with the real level layout.

**Tests:** 9 EditMode tests for `GameSession` pass (`Assets/Tests/EditMode`). Play-mode scripted checks, all passing:

| Check | Result |
|-------|--------|
| Mud | multiplier 0.5, `Slow` true, animator in `Slow`, speed 3.0; on exit multiplier 1, back to locomotion |
| Direct stumble | accepted; steering ignored while stumbling; second hit within 1 s rejected; accepted after immunity |
| Falling rock | trigger arms it; rock lands on the player and stumbles it |
| Animator | trigger → `Stumble` state → back to locomotion (≈ 0.6 s) |
| Pause | `Paused`, `timeScale` 0, input off; resume restores all |
| Kill plane | player below y = −10 → `Lost`, input off |
| Win | 9/10 stays Playing; 10th → `Won`, input off, `Victory` state, later `Lose()` rejected |

Not verified: the rolling log's collision (only mud/rock/direct hits were driven), the Esc key itself (`TogglePause` was called directly), and `GameManager.Restart()`.

### Phase G — Package slimming and tests (S)
- [ ] After Phases C–E pass, delete unused package folders (`FirstPersonController`, `Mobile`, `Sample`, `Runtime/Settings`, `Editor/URPWizard`) — move/delete asset and `.meta` together, inside Unity.
- [ ] PlayMode tests: player grounded on spawn; Space leaves ground; falling below the kill plane raises `Lost`; Z stays constant over 2 s of input.
- [ ] Final check: clean build opens `MainMenu` → `Level01`, player controllable, no missing scripts or references.

## 5. Risks

| Risk | Mitigation |
|------|------------|
| Generic → Humanoid retarget distorts the Mixamo mesh | Do Phase B first; fall back to Mixamo Idle/Run/Jump clips on the Generic rig if mapping fails |
| Cinemachine 3 prefab/API mismatch with stock `PlayerFollowCamera` | Build our own `CinemachineCamera` instead of using the prefab |
| `CharacterController` ignores rigidbody hazards | Triggers and explicit `Stumble()` calls; no physics-force knockback |
| Stock script silently re-adds look/cursor lock | Our copy drops those methods; cursor stays free for the pause menu |
| Two input assets cause rebinding confusion | D4: one asset only; remove the drone actions |
| 93 MB of unused sample content bloats the repo | Phase G; remove before the first push of `Starter Assets/` if the team agrees |
| Missing `.meta` files break GUIDs for teammates | Project rule: commit every asset with its `.meta` |

## 6. Decision log

_(Confirm before starting Phase A.)_

- D1 Use adapted `ThirdPersonController` and drop the Rigidbody controller: ☐
- D3 Keep `Player.fbx` as Humanoid (not the bundled Armature): ☐
- D4 Single input asset (`StarterAssets.inputactions`): ☐
- D6 Delete unused Starter Assets folders after integration: ☐
