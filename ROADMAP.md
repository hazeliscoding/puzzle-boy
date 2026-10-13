# Roadmap

Puzzle Boy is a standalone, cross-platform rebuild of the Puzzle Boy minigame from *Shin Megami Tensei III Nocturne HD Remaster*. This file tracks the decisions already made, what gets built and in what order.

## Decisions (2026.10.13)

- **Bring your own copy.** The repo never contains Atlus data or code: no stages, text, audio, textures, models, metadata dumps, method tables or decompiler output. The game reads its assets from the player's install. Research output lives in `re/`, which git ignores.
- **Target:** the Steam release of *Nocturne HD Remaster* (Unity 2019.4, IL2CPP). Stages, dialogue, music and sounds are in `smt3hd_Data/StreamingAssets/PC/pazzleboy`. Translated text is in `pazzleboy_<lang>`, for de, en, es, fr, it, ja, ko and zh.
- **No circumvention.** `GameAssembly.dll` is protected by Denuvo Anti-Tamper. We analyze only its unprotected `.text` section, statically. There are no runtime memory dumps and no de-obfuscation. All 225 Puzzle Boy methods are in `.text`. If a function we need leads into protected code, we answer that question by playing the real game instead.
- **Toolchain:** REA with Ghidra 12.1.4 and JDK 21. REA has no IL2CPP support, so method names come from the game's IL2CPP metadata and reach Ghidra through an REA seed file. Il2CppDumper fails on this binary, because the protector renamed the sections that hold IL2CPP's registration tables. The tables use the v24.3 layout, although the metadata reports v24.2.
- **The game code is the PS2 original, converted to C#.** The original names survive in the metadata (`pbGame.pushCheck`, `pb_H.pbBackup_t`, the `dds3` engine calls), which makes the decompiled functions readable.
- **Rules are written as tests.** Each rule becomes a test on a handmade stage, and the code is written from the tests. Pseudocode is never pasted or transliterated into the source.
- **3D like the original.** The stage is drawn as 3D blocks, with the original's rotating camera and view mode. Rules come first, with plain cubes standing in until M3 adds Jack-o'-Lantern and the original look.
- **C# on .NET 10 with MonoGame (DesktopGL).** It's code-first with no editor, so the whole game is plain C# that can be tested and reviewed. The rules live in their own library with no MonoGame or file I/O, and its tests are the rule spec. The game reads the player's files with AssetsTools.NET (MIT) for the LZ4HC UnityFS bundles and Fmod5Sharp (MIT) for the Vorbis-in-FSB5 audio. Textures are DXT1.
- **Stage format** (observed). A stage file is 828 bytes:
  - an int32 character count (1–4) and 4 enable bytes;
  - four int16 x,y start positions, then an int16 x,y goal;
  - a 20×20 grid of little-endian uint16 cells: `0x0000` floor, `0x2000` wall, `0x0800` hole, `0x60nn` a cell of block group `nn`.
  - Swivel blocks use `0x80nn` for the center and `0xA0nn` for the arms. That reading is inferred from the layouts and still needs the code to confirm it.
  - The bundle groups stages 00–29 under `pb1` and 30–49 under `pb2`.
  - Six files (30, 33, 35, 41, 44, 46) shipped with their `0x0A` bytes rewritten as `0D 0A`. Undoing that gives exactly 828 bytes and a sensible stage. The port's parser undoes it for any file longer than 828 bytes. Whether the original does the same is still an M0 question.
  - The enable bytes, not the count, pick the characters: stage 08 uses only slot 1, and stage 45 uses slots 2 and 3. Every start and goal sits on floor.

## Open decisions

To settle before M3: the brand. It must be an original mark with no Atlus logos or art, and the README gets its header once the brand exists.

## M0: Research the rules (current)

- [x] Decode the stage format from the asset bundle.
- [x] Recover every method's name and address from the IL2CPP metadata and the code tables.
- [ ] Analyze `GameAssembly.dll` with REA and Ghidra, and save a snapshot so later sessions skip the analysis.
- [ ] Decompile the move rules: `pushCheck`, `hitCheck`, `charHitCheck`, `roundCheck`, `roundDirCheck`, `hallCheck`, `goalHitCheck` and `changeCharacter`.
- [ ] Find which stages are played, in what order, and how each one picks its dialogue (`pbLevelTable`, `pLevel1` to `pLevel3`, `GetStageScriptLabel`).
- [ ] Find out how the game loads the six damaged stage files (`pbBackup_t.pCopy`, `pbMenu.load`).
- [ ] Work out undo and records: what `pbRecord` stores, the 30-move limit, the move counter and `pbPreviousRecord`.
- [ ] Locate the Jack-o'-Lantern model, its motions (`STAY`, `READY`, `WALK`, `PUSH`) and the background.

**Done when** each question above has an answer backed by REA Evidence or an in-game observation, recorded as a decision here.

## M1: Placeholder

- [x] Scaffold the .NET solution: the rules library, its tests and the MonoGame game. The stage parser came with it, and it reads all 50 shipped stages, including the 6 damaged files.
- [x] Find the Steam install and read the `pazzleboy` bundle at runtime. It checks the Steam libraries on Windows, macOS and Linux, and the first argument overrides the search. The bundles carry type trees, so AssetsTools.NET needs no class database.
- [x] Draw one stage from the player's install. Stages are plain lit boxes: walls only where they border the play area, holes left open, and blocks and swivels colored by group. PageUp and PageDown flip through all 50 stages until the menu exists.
- [ ] Add a CI guardrail that fails if Atlus data or decompiler output lands in the repo (Unity bundles, `.map` files outside the handmade test stages, Ghidra's generated function and type names, Il2CppDumper's address comments), with a positive control.

**Done when** a stage from the player's install is on screen, CI passes, and the guardrail fails on a deliberate violation.

## M2: Playable rules

- [ ] Write each M0 rule as a test on a handmade stage.
- [ ] Implement movement, pushing, swivel blocks, holes, character switching, the goal, undo and retry.
- [ ] Load and play every stage the original plays.

**Done when** the tests pass and, for every stage, a solution found in the original also solves the stage in the port.

## M3: Presentation and release

- [ ] Camera, Jack-o'-Lantern, stage dialogue, music and sound effects.
- [ ] Menus: undo, retry, help and quit, matching the original's options.
- [ ] Release builds for Windows, macOS and Linux.

**Done when** the release builds start on all three systems and play stage 1 from the player's install.

## Later

- A stage editor. The original ships a developer editor (`pbEdit`).
- Custom stage packs.

## Not planned

- Shipping or downloading any Atlus asset.
- Circumventing Denuvo, patching the original game or modding it.
- Nocturne's other minigames.

## How we'll know it works

A solution recorded in the original game solves the same stage in the port, with the same move count. A move the original rejects, the port rejects too.
