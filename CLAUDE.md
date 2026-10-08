# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**proyectoJuegoUNAD** is a Unity 6 game project for a multimedia engineering degree at UNAD (Universidad Nacional Abierta y a Distancia). It is a team repo: `README.md` holds per-member profiles (Elianis Manuel Medina – Level Designer; Jhon – Game Programmer; Geordany Giraldo Arenas), with their photos in `Elianis/`, `Jhon/`, `Geordany Giraldo Arenas/`. Docs and UI text are in Spanish.

## Repository Layout (read this first — it is non-obvious)

The git root contains **more than one Unity project**, and the real game is *not* at the root:

| Path | What it is |
|------|------------|
| `proyectoJuegoUNAD/` | **The active game project.** Open this folder in Unity Hub. Editor 6000.5.6f1, URP 17.5.0, Input System 1.20.0. |
| `Assets/`, `Packages/`, `ProjectSettings/` (repo root) | A second, near-empty Unity project (single `Assets/Proyecto.unity`, editor 6000.4.7f1). Not the game. |
| `Library/`, `Logs/`, `UserSettings/` (repo root) | Unity cache dirs. **Roughly 765 `Library/` files are committed to git** despite `.gitignore` — don't edit them, and avoid adding more. |

Older notes referring to `proyecto Juego UNAD/` are stale; that directory no longer exists.

## Game Design / Code Architecture

Two designs coexist in history, so check which one a task targets:

- **Committed code (HEAD): "Smog Buster" drone game.** Under `proyectoJuegoUNAD/Assets/Scripts/`:
  - `Player/DroneController.cs` — Rigidbody-based drone (Input System; momentum, vertical thrust, altitude-boundary spring, cosmetic tilt on a `DroneModel` child, a beam with energy, planting cooldown, `DroneState` enum). Expects child objects named `DroneModel` and `BeamOrigin`.
  - `Player/CameraFollow.cs`, `Player/DroneHUD.cs`
  - `Environment/SmogZone.cs`, `Environment/PlantingZone.cs` — zones the drone clears/plants in.
  - `Gameplay/GameManager.cs` — singleton; counts zones via `FindObjectsByType` in `Start`, zones call `NotifySmogCleared` / `NotifyTreePlanted`, drives the TextMeshPro HUD and win panel.
  - `Editor/SceneSetup.cs` — menu **Smog Buster > Setup Scene** builds layers, ground, UI canvas, GameManager and placeholder zones. Run it instead of hand-building scenes.
  - Art: `Assets/Models/UAV2_Fbx` (drone), `Assets/POLYGON city pack` (third-party city prefabs/scene), `Assets/Materials`.
- **Newer direction (untracked in working tree): "Guardianes del Bosque"** — educational 3D low-poly forest platformer about waste sorting (identify → collect → classify → recycle → restore), for ages 10–14. The GDD is `proyectoJuegoUNAD/Assets/Docs/GuardianesdelBosqueGDD.md`; new scenes `Assets/Scenes/MainMenu.unity` and `Level01.unity`.

**Working-tree caveat:** at the time of writing, the working tree has ~2,200 tracked files deleted on disk (the drone scripts, models, and city pack) while `HEAD` still contains them, plus untracked Level01/MainMenu/Docs. Run `git status` before assuming a file exists, and do not `git add -A`/commit blindly. Recover deleted files with `git show HEAD:<path>` or `git restore <path>`.

## Commands

There is no CLI build, lint, or test script. Everything runs through the Unity Editor:

- **Open:** Unity Hub → add `proyectoJuegoUNAD/` → Unity 6000.5.6f1.
- **Play:** Play button in the Editor.
- **Build:** File > Build Profiles (Build Settings).
- **Tests:** Window > General > Test Runner (Test Framework package; no tests written yet). Headless, if needed: `Unity.exe -batchmode -projectPath proyectoJuegoUNAD -runTests -testPlatform EditMode -quit`.
- **Unity MCP** (`mcp__unity-mcp__*`) is configured and can read console logs, run editor commands, and capture the scene/camera — useful for verifying changes without leaving the CLI.

## Conventions

- New scripts go under `Assets/Scripts/<Feature>/` (`Player`, `Environment`, `Gameplay`, `UI`, `Editor`), one responsibility per script.
- Input goes through the Input System (`Assets/InputSystem_Actions.inputactions`), not the legacy `Input` class.
- **`.meta` files must ALWAYS be committed, in the same commit as their asset (or folder).** Each `.meta` holds the asset's GUID; if it is missing, every teammate's Unity generates a different GUID, scene/prefab references break, and merges corrupt the project. Never add `*.meta` to `.gitignore`, never delete a `.meta` by hand, and move/rename assets inside Unity (or move the asset and its `.meta` together). When creating files outside the Editor (scripts, docs), the `.meta` appears once Unity next focuses — commit it before pushing.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vs/`, or generated `*.csproj`/`*.sln`.
- Active branch for this developer is `elianis-medina`; `main` is the PR target.
