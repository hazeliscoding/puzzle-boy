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
  - Swivel blocks use `0x80nn` for the center and `0xA0nn` for the arms. The code confirms this; see the M0 findings.
  - The bundle groups stages 00–29 under `pb1` and 30–49 under `pb2`.
  - Six files (30, 33, 35, 41, 44, 46) shipped with their `0x0A` bytes rewritten as `0D 0A`. Undoing that gives exactly 828 bytes and a sensible stage. The port's parser undoes it for any file longer than 828 bytes. The original never loads these files.
  - The enable bytes, not the count, pick the characters: stage 08 uses only slot 1, and stage 45 uses slots 2 and 3. Every start and goal sits on floor.

## Decisions (2026.10.14)

- **The rules engine follows the M0 findings, with three deliberate differences.** None of them can show up in a stage the original plays. Revisit them if the unused stages are ever added.
  - The character-collision check uses the real cell. The original swaps x and y, but it never plays a stage with two characters.
  - The two-cell swivel step counts only cells that belong to the swivel. The original compares group ids alone, which would also match floor if a swivel used group id 0, and no stage does.
  - A swivel turn that would reach past the grid edge is refused. The original would crash, but no swivel touches the edge.
- **Undo keeps a full copy of the board** for each push or turn, at most 30. The original stores only what changed, and the results are the same.
- **Rules tests draw their stages as text.** Columns are x and rows are y; the helper turns each drawing into real stage-file bytes, so every test also goes through the parser.
- **Play ends after stage 20.** Every video and guide returns to the arcade after stage 20, and none reaches file 00, which the code lists for levels 2 and 3. File 00 joins the unused stages in Later.
- **M2 closes with the in-game checks moved to M3.** The owner's save isn't at Puzzle Boy yet. M3 answers them from the game's code where it reaches them, since the camera and input code is in `.text`, and labels anything inferred.

## M0 findings (2026.10.14)

Read from the decompiled game code through REA. Seven readers each covered one area, and an adversarial reviewer per area re-checked every claim against the pseudocode: 111 claims, 97 confirmed, 10 refuted or corrected, 4 left uncertain. Only the confirmed and corrected claims are recorded here. The IDs are short REA Evidence IDs; the pseudocode and the full results stay in `re/`.

**Cells and coordinates**
- A cell's bits 13–15 are its type: 0 floor, 1 wall, 3 block, 4 swivel center, 5 swivel arm. Types 6 and 7 act like walls. `0x0800` is the hole flag, and the low 11 bits are the group id. Type 2 only exists at runtime: when a stage loads, the first cell of each block group becomes the group's core. A group is every cell with that id, whether or not the cells touch (`pbGameDraw.makeBlockDataBase` ev_7709df7abef9).
- File cell `20 × y + x` is cell (x, y), and the start and goal pairs are (x, y). Direction 0 is y + 1, 1 is x + 1, 2 is y − 1 and 3 is x − 1 (`pbGame.game` ev_c6e6a20a255d).

**A step** (`pbGame.hitCheck` ev_18c11f2e61c4)
- Checked in this order. A character already on the goal does nothing. A destination off the grid, another character or a hole is a bump. Then, by type: floor walks one cell, a block is a push, an arm is a swivel turn, and a swivel center or a wall is a bump.
- A bump is silent and changes nothing. Plain walking is never recorded for undo.

**Pushing and holes** (`pushCheck` ev_527a76211ad2, `moveBlock` ev_beab8ee211d5, `hallCheck` ev_acbf04db9622)
- Only the group walked into moves, one cell. Every one of its cells must land on the grid, on floor or on a cell of the same group, with no other character there. A wall, another block or a swivel refuses the push, so a block never pushes another block. Holes and the goal don't refuse a push.
- The character steps into the cell the block left.
- After the push, the block falls only if every one of its cells is over a hole. The fall fills those holes with plain floor and the block disappears. Holes the block doesn't fully cover stay holes.
- A block or arm cell resting on a hole can't be stepped into, so it can't be pushed from that cell, because the hole check comes first.

**Swivels** (`roundCheck` ev_8975615c3ad9, `roundDirCheck` ev_474426a6ef42, `roundBlock` ev_9e719443f24e)
- Pushing an arm turns the swivel exactly one quarter turn, and the pushed arm ends on the side of the center the push points to.
- The turn is refused when the character is in line with the center (same row or column), when the character's path holds a hole or a wall, or when any cell of the swivel would move onto, or sweep through, a wall, another group's cell or another character. The sweep cell is center + old offset + new offset.
- Holes and the goal never block a turn, and an arm can stop over a hole without filling it.
- The character moves 2 cells when a cell beside it, across the push, belongs to the same swivel, and 1 cell otherwise.

**Characters, goal and clear** (`deleteMoveChar` ev_34d10980d2c4, `changeCharacter` ev_9031f1f64dd0, `ClearProc` ev_a22f645b61d7)
- The goal is checked where a move ends. A character that arrives while others are still out is hidden, and control passes to the next visible slot in ascending order, wrapping around. There is no manual switch.
- The last arrival clears the stage: input locks for 31 ticks, the undo history is wiped, and the next stage loads automatically.
- There is no move counter. The only number on screen is the stage counter.

**Stages and dialogue** (`pbGame` static constructor ev_c1e5d35be978, `GetStageScriptLabel` ev_3d9eedee78a2, `pbInitGame` ev_b52ab1ade7e0, `pbGotoNewStage` ev_ee2048a293c0, `pbInitMain` ev_9981f13362fa)
- Level 1 plays 20 stage files in this order: 02, 04, 32, 08, 38, 36, 37, 48, 42, 49, 40, 20, 22, 13, 21, 24, 14, 10, 25, 26. Levels 2 and 3 play only file 00. That's 21 distinct files, and a local scan shows each has exactly one character.
- Dialogue plays before stages 1, 6, 11, 16 and 20 (labels 0–4 of `pbmsg.bf`). A dialogue can end Puzzle Boy instead of starting the stage. That it's the player choosing to quit is inferred, not confirmed.
- Progress inside a level isn't saved, so every visit starts at stage 1. Clearing a stage unlocks one achievement and clearing the last stage another, and finishing level 1 also sets a main-game event flag.

**Loading** (`pbMenu.pbLoadSystemData` ev_1fdd0ae2a9c0, `pbBackup_t.pCopy` ev_b293c2a41557)
- The path is `dds3data/pb/mapdata/data/pb1/` for stages below 30 and `pb2/` otherwise, then `pbMapNN.map.bytes`. `pCopy` reads the fixed 828-byte layout with no length check and no CR handling, and the only validation is "zero characters".
- The six damaged files are in no level table, so the original never loads them. The damage is in the shipped bundle: their TextAssets are 829–834 bytes there.

**Undo and retry** (`pbRecord.pbPushRecord` ev_e52d36ebf3b4, `pbAddRecord_char` ev_a762737382cb, `pbGame.pbPreviousRecord` ev_3dae85334430, `pbMenu.reTry` ev_c3f78d7bff36)
- Undo keeps a ring of 30 slots, and only pushes and swivel turns are recorded (a push and its fall count as one). A record holds the active character, every character's position and visibility, the moved group's cells, and the hole bits a fall filled.
- Undo returns to the moment before the last push or turn and discards any walking since. Characters hidden at the goal reappear and filled holes reopen. The 31st action overwrites the oldest slot, so 30 undos are always available. There's no redo.
- The history is wiped at stage start, on retry and on clear.

**Presentation** (`pbModel.pbInitMyCharacterDraw` ev_dc99c6b3c387, `pbChangeMotion` ev_7385ca4844c3, `pbDraw.drawBlock` ev_913d4de77fec)
- The character is demon model `0x150` (bundle `dvl0x150`), a Puzzle Boy variant of Jack-o'-Lantern: its materials are demon `0x47`'s hat, eyes and lantern, plus a puzzle shadow. Every slot uses the same model.
- In HD the walk and push motions are empty, so the model loops its first animation and turns to face each input.
- The background is `BG_pzl.png` from the `common` bundle. Board textures come from the `sample1.tga` atlas. The goal is an untextured translucent cube spinning 1° per frame, and a cell is 40 world units.
- Sounds: SE00 step, SE01 push, SE02 turn, SE09 goal, SE04 menu. Music: `JYAKYO02_BGM00`.
- Seen in gameplay video (see M2): walls are see-through with outlined edges, blocks are salmon, swivels white with a dome on the center, and the goal is cyan. The controls bar reads Move, L1/R1 Rotate, □ Menu, △ Toggle Scroll, ○ Angle. A clear shows "COMPLETE!" and an undo shows "UNDO" with a rewind mark. The camera follows the character on large stages.

**Quirks in the original**
- The character-collision check is called with x and y swapped, so another character blocks the mirrored cell. Only multi-character stages could show it, and the original plays none.
- The swivel check has no grid-edge test, and no shipped swivel touches the edge.
- Stage 36, the sixth stage played, has a swivel arm resting on a hole (`0xA802` at (8, 10)). The character can't push that arm, the swivel still turns from its other arms, and the hole stays open.

## Open decisions

To settle before M3: the brand. It must be an original mark with no Atlus logos or art, and the README gets its header once the brand exists.

## M0: Research the rules (done 2026.10.14)

- [x] Decode the stage format from the asset bundle.
- [x] Recover every method's name and address from the IL2CPP metadata and the code tables.
- [x] Analyze `GameAssembly.dll` with REA and Ghidra, and save a snapshot so later sessions skip the analysis.
- [x] Decompile the move rules: `pushCheck`, `hitCheck`, `charHitCheck`, `roundCheck`, `roundDirCheck`, `hallCheck`, `goalHitCheck` and `changeCharacter`.
- [x] Find which stages are played, in what order, and how each one picks its dialogue (`pbLevelTable`, `pLevel1` to `pLevel3`, `GetStageScriptLabel`).
- [x] Find out how the game loads the six damaged stage files (`pbBackup_t.pCopy`, `pbMenu.load`). It doesn't: they're never played.
- [x] Work out undo and records: what `pbRecord` stores, the 30-move limit, the move counter and `pbPreviousRecord`. There is no move counter.
- [x] Locate the Jack-o'-Lantern model, its motions (`STAY`, `READY`, `WALK`, `PUSH`) and the background.

**Done when** each question above has an answer backed by REA Evidence or an in-game observation, recorded as a decision here.

**Done:** every question has an answer backed by REA Evidence, recorded under "M0 findings" above. A few details can only be seen in the real game, and they're listed in M2.

## M1: Placeholder (done 2026.10.13)

- [x] Scaffold the .NET solution: the rules library, its tests and the MonoGame game. The stage parser came with it, and it reads all 50 shipped stages, including the 6 damaged files.
- [x] Find the Steam install and read the `pazzleboy` bundle at runtime. It checks the Steam libraries on Windows, macOS and Linux, and the first argument overrides the search. The bundles carry type trees, so AssetsTools.NET needs no class database.
- [x] Draw one stage from the player's install. Stages are plain lit boxes: walls only where they border the play area, holes left open, and blocks and swivels colored by group. PageUp and PageDown flip through all 50 stages until the menu exists.
- [x] Add a CI guardrail that fails if Atlus data or decompiler output lands in the repo (Unity bundles, FMOD sound banks, IL2CPP metadata, Atlus flow scripts, `.map` files outside the handmade test stages, Ghidra's generated function and type names, Il2CppDumper's address comments), with a positive control. `scripts/check-no-game-data.test.sh` plants each kind and expects a failure.

**Done when** a stage from the player's install is on screen, CI passes, and the guardrail fails on a deliberate violation.

**Done:** stages 00, 27 and 44 were drawn from the player's install, and stage 44 is a repaired file. CI passed on Linux ([run 38473585277](https://github.com/hazeliscoding/puzzle-boy/actions/runs/38473585277)). Throwaway PR #1 planted a stage file in `docs/`, and CI failed at "No game data" ([run 38473733436](https://github.com/hazeliscoding/puzzle-boy/actions/runs/38473733436)). The PR was closed without merging.

## M2: Playable rules (done 2026.10.14)

- [x] Write each M0 rule as a test on a handmade stage. There are 54 rules tests; breaking any of 9 rules on purpose (the swivel's turning sense, the fall, the sweep check, the undo limit, the two-cell step, the hole check, hiding at the goal, wiping undo on clear) fails at least one.
- [x] Implement movement, pushing, swivel blocks, holes, character switching, the goal, undo and retry (`PuzzleBoy.Rules.Board`).
- [x] Load and play the original's stages in its order: level 1's 20 stages. Play ends there, as in the original (see Decisions).
  - A local search over the port's rules proved 18 of 21 files (the 20 stages plus file 00) solvable, from 4 pushes or turns (stage 1) to 109 (file 13).
  - Files 49, 25 and 26 are larger than the search budget (6 million positions), but they use only features the solved stages exercise: multi-cell blocks, holes, and swivels with 1 to 4 arms.
  - Every swivel arm in a played stage is one cell long, so the original's long-arm quirk never applies.
  - VERIFIED: AppMedia's recorded HD solutions clear all 20 level-1 stages in the port, including 49, 25 and 26. That's 2,756 inputs: every stage clears exactly on its last input, and no input is refused. Only the default mapping clears all 20; a second mapping also clears stages 2, 4 and 16, whose routes are mirror-symmetric. AppMedia also labels each swivel turn clockwise or counter-clockwise, and on the 16 stages where the sense is visible every label matches the port's turn. A PS2 guide ([kouryaku.red](https://shinnmegamitennsei3.kouryaku.red/entry14.html)) clears every stage too. The replay is a local check in `re/research/check/`, since it reads the stages and the guides.
- [x] Confirm in the real game what the code can't show, as far as video and guides reach. The UNKNOWNs below moved to M3. Labels: **VERIFIED** (seen in the HD game or a video of it, or a recorded solution replayed in the port), **CODE** (read from the decompiled game, see M0), **DOCUMENTED** (a guide says so), **INFERRED**, **UNKNOWN**. Sources: a gameplay video of the HD release ([all 20 stages](https://www.youtube.com/watch?v=tEmsZPDJsdc), times below) and [AppMedia's input lists](https://appmedia.jp/megaten3/5551711) (HD, all 20 stages; the guide text stays in `re/`).
  - VERIFIED: the default camera shows +x to the right and +y up the screen, and Up moves toward +y. In stage 1 (0:31) the swivel's +y arm sits above its center, and AppMedia's 22 stage 1 inputs clear the port under this mapping and under none of the other 7. The port drew +y down, a mirror image, until this was fixed.
  - VERIFIED: stage 1 is file 02, and all 20 layouts match the order above; the HUD shows the stage number.
  - VERIFIED and CODE: the Manikin Kid speaks over the board before stages 1 (0:25), 6 (1:20), 11 (3:30), 16 (6:45) and 20 (9:34).
  - VERIFIED: after stage 20 (11:50) the game returns to the arcade and gives the Magatama Geis. No guide mentions more stages. CODE says levels 2 and 3 play file 00; UNKNOWN when, or whether, a player reaches them.
  - DOCUMENTED: L1/R1 rotate the view and □ opens a menu with undo, retry, help and quit (the HUD at 0:37 and a Japanese guide). INFERRED: directions follow the rotated view. A guide warns that rotating breaks its solutions, and the PS2 guide's stages 10–15 clear only under a quarter-turned mapping, as if its author had turned the view. UNKNOWN: the rotation step, and the exact mapping after a turn.
  - CODE: undo takes back the last push or turn and discards walking since then, keeps 30, and doesn't record the camera. DOCUMENTED: a wiki says undo takes back "one action that isn't a plain move", and Neoseeker says it has a limit. UNKNOWN in play.
  - UNKNOWN: whether holding a direction repeats. No guide or video shows it.

**Done when** the tests pass and, for every stage, a solution found in the original also solves the stage in the port.

**Done:** the tests pass, and recorded HD solutions clear all 20 stages in the port (above).

## M3: Presentation and release

- [ ] Answer M2's open control questions from the game's code: how directions map after an L1/R1 turn and by how much the view turns, whether holding a direction repeats, and undo in play. Label each answer as in M2.
- [ ] Camera, Jack-o'-Lantern, stage dialogue, music and sound effects.
- [ ] Menus: undo, retry, help and quit, matching the original's options.
- [ ] Release builds for Windows, macOS and Linux.
  - `release.yml` builds self-contained archives for win-x64, linux-x64, osx-arm64 and osx-x64 from Linux, and a `v*` tag publishes them. Before publishing, each build must start on its own system in CI and reach the "no game here" message: Windows, Linux and Apple Silicon macOS pass ([run 38579842875](https://github.com/hazeliscoding/puzzle-boy/actions/runs/38579842875)).
  - Played stage 1 from the owner's install: the Windows build, and the Linux build under WSL (WSLg, Mesa llvmpipe). macOS has no tester yet, and its builds are unsigned.

**Done when** the release builds start on all three systems and play stage 1 from the player's install.

## Later

- A stage editor. The original ships a developer editor (`pbEdit`).
- Custom stage packs.
- An Android build. MonoGame targets Android, and the rules library carries over as is. A phone has no Steam install, so the player copies the few bundles Puzzle Boy reads from their PC install (about 85 MB, most of it `common`) and picks them in the app. Needs touch controls: swipe to move, buttons to turn the view.
- The 30 stage files the original never plays, file 00 included, as bonus stages. They include all seven multi-character stages (05, 09, 18, 27, 43, 45, 47), so this needs a decision on the swapped collision check first.

## Not planned

- Shipping or downloading any Atlus asset.
- Circumventing Denuvo, patching the original game or modding it.
- Nocturne's other minigames.

## How we'll know it works

A solution recorded in the original game solves the same stage in the port. The same moves leave the board in the same state, and a move the original rejects, the port rejects too. The original has no move counter to compare.
