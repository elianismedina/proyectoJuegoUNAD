# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**proyectoJuegoUNAD** is a video game development project created as part of a multimedia engineering degree at UNAD (Universidad Nacional Abierta y a Distancia). It is the developer's first game project, with interests in level design, programming, photography, and animation.

- **Engine:** Unity 6 (version 6000.5.6f1)
- **Language:** C#
- **Project Lead:** Elianis Manuel Medina (Level Designer, Cali, Valle del Cauca)

## Repository Structure

```
proyectoJuegoUNAD/
├── proyecto Juego UNAD/          # Main Unity project directory
│   ├── Assets/                   # Game assets and scripts
│   │   ├── Scenes/              # Game scenes
│   │   ├── Scripts/             # C# scripts (organized by functionality)
│   │   └── TutorialInfo/        # Tutorial and readme assets
│   ├── Packages/                # Package manifest for dependencies
│   ├── ProjectSettings/         # Unity project configuration
│   ├── Assembly-CSharp.csproj   # Main C# project file
│   └── Assembly-CSharp-Editor.csproj
├── Elianis/                      # Developer's personal workspace
├── README.md                     # Project introduction
└── .git/                         # Git repository
```

**Note:** The git repository root is at the top level; the actual Unity project is in the `proyecto Juego UNAD/` subdirectory.

## Getting Started

### Opening the Project in Unity

1. Install Unity version 6.0.5 or later (currently 6000.5.6f1)
2. Open Unity Hub and add the project: Point to `proyecto Juego UNAD/` directory
3. Open the project in Unity 6

### Project Organization

The project follows standard Unity conventions:
- **Assets/Scenes/** — Game scenes (currently SampleScene.unity)
- **Assets/Scripts/** — C# gameplay scripts (will be organized as development progresses)
- **Assets/TutorialInfo/** — Tutorial content and Readme asset
- **ProjectSettings/** — Unity configuration (graphics, physics, input mappings, etc.)
- **Packages/manifest.json** — Lists all dependencies

### Key Dependencies

- **Universal Render Pipeline (URP)** — Modern rendering system (v17.5.0)
- **Input System** — Modern input handling (v1.20.0)
- **Visual Scripting** — Visual programming support (v1.9.11)
- **Timeline** — Animation and cutscene support (v1.8.12)
- **AI Navigation** — Pathfinding and NPC movement (v2.0.14)
- **Test Framework** — Unit testing (v1.7.0)

## Development Workflow

### C# Scripts

All game logic resides in C# scripts under `Assets/Scripts/`. When adding new scripts:
- Organize by feature or system (e.g., `Player/`, `Enemies/`, `UI/`, `Gameplay/`)
- Each script should have a single responsibility
- Use meaningful class and method names

### Scene Management

- Primary scene: `Assets/Scenes/SampleScene.unity`
- Create new scenes as needed for different levels or game states
- Configure scene load order in **Project Settings > Editor > Scene Load Order**

### Building & Testing

- **Play in Editor:** Press Play in Unity Editor (Ctrl+P or Cmd+P)
- **Build:** File > Build and Run (or Game > Build Settings > Build)
- **Test Framework:** Run tests via Window > General > Test Runner

## Input System Configuration

Input mappings are defined in `Assets/InputSystem_Actions.inputactions`. This modern Input System supports:
- Keyboard, mouse, and gamepad input
- Rebindable controls
- Context-sensitive input handling

## Known Patterns & Architecture

- The project is in early stage; architecture will evolve as gameplay mechanics are developed
- Use the tutorial assets in TutorialInfo as reference for Readme and project documentation
- Visual Scripting is available for rapid prototyping of game logic if needed

## .gitignore Notes

The `.gitignore` file excludes:
- Build artifacts and temporary Unity folders (`Library/`, `Temp/`, `Logs/`, `Obj/`, `Builds/`)
- IDE and editor caches (`.vs/`, `*.csproj`, `*.sln`, `*.suo`, `*.user`)
- Platform-specific binaries (`.apk`, `.aab`, `.app`)
- Generated files (crashes, addressables, visual scripting generated code)

Commit only:
- `Assets/` (scripts, scenes, prefabs, sprites, sounds, etc.)
- `ProjectSettings/` (project configuration)
- `Packages/manifest.json` (dependencies)
- Source files (*.cs, *.unity, *.prefab, etc.)

Do NOT commit: Library, Temp, Logs, Obj, Build, .vs, or user-specific files.
