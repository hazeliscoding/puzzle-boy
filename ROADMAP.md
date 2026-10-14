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
- **C# on .NET 10 with MonoGame (DesktopGL).** It's code-first with no editor, so the whole game is plain C# that can be tested and reviewed. The rules live in their own library with no MonoGame or file I/O, and its tests are the rule spec. The game reads the player's files with AssetsTools.NET (MIT) for the LZ4HC UnityFS bundles and Fmod5Sharp (MIT) for the Vorbis-in-FSB5 audio, with NVorbis (MIT) decoding the Vorbis to PCM. Central package management pins transitive versions too, so a patched `Microsoft.Bcl.Memory` reaches Fmod5Sharp's `IndexRange`. Textures are DXT1 (the board atlas) and RGB24 (the backdrop), decoded on the CPU so every platform behaves the same.
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
- **Play ends after stage 20.** Every video and guide returns to the arcade after stage 20, and none reaches file 00, which the code lists for levels 2 and 3. File 00 joins the unused stages in Later. (M4 adds them after stage 20.)
- **Brand: Pixel Jack.** A 16×16 pixel-art pumpkin in a crooked witch's hat, inspired by SMT's Pyro Jack and drawn from scratch: no Atlus logos or art. The project is for modders and reverse engineers, not a product. The name stays Puzzle Boy, with the trademark notice in the README. The wordmark comes from Silkscreen Bold (SIL OFL 1.1), converted to paths. The palette is pumpkin `#F07F2A`/`#C9581C`, ember `#FFC94A`, hat `#2A2148` (`#4A3B91` on dark), band `#7A4FD0` (`#B497FF` on dark), ink `#1A1426`, cream `#F4EEE2` and night `#15102A`. Files are in `docs/brand/`. The window and executable icons use the dark palette, whose lighter hat reads on both light and dark title bars.
- **M2 closes with the in-game checks moved to M3.** The owner's save isn't at Puzzle Boy yet. M3 answers them from the game's code where it reaches them, since the camera and input code is in `.text`, and labels anything inferred.
- **Menu keys and the UI's open questions.** The original's keyboard keys for confirm and cancel are in protected code, so the port picks them: F opens the □ menu (inferred from the original's key tables), Enter or Space confirms, Backspace cancels, and Escape still quits at once. Text uses the face edge fitted to the footage (+0.035) rather than the material's exact value. The port has no stage fade and no mouse in the menu (re/m3-ui.md, section 10).
- **M3 closes without a macOS play test, and v0.1.0 ships.** Nobody has a Mac. The macOS builds start in CI, and the release notes say nobody has played them yet, so a Mac player can report back.
- **M4 adds the 30 unused stage files as bonus stages after stage 20.** The original's 20 stages are played first, unchanged. A local survey checked every unused file and searched for solutions with the port's `Board`:
  - observed: no file duplicates another, and none looks like a test or junk stage;
  - observed: 14 of the 23 single-character stages have solutions, and 9 hit the search cap (11, 15, 16, 17, 19, 23, 28, 29, 44);
  - observed: of the seven multi-character stages, only 09 can be finished as shipped. The search exhausts 05, 18, 27, 43, 45 and 47, because only the first character can move until it reaches the goal;
  - inferred: those six were designed for switching characters, which the shipped code lacks (M0 findings: control passes only on a goal arrival).
- **A switch key, a deliberate difference.** C hands control to the next visible character, in the order a goal arrival uses, and plays SE08 as the camera moves to it. With a switch, 05, 43 and 45 have solutions; 18, 27 and 47 hit the cap. The original has no switch to copy, so the key, the sound and the bar label are the port's.
- **The collision check keeps the real cell.** Modelling the original's swapped x and y in all three places it checks for another character changes no result in the survey.
- **Only stages with a known solution ship, easiest first.** Each bonus stage needs a solution that clears in the port. They're ordered by the pushes and turns in the solution found, so the tutorial-sized files (00, 01, 30, 31) come first.

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
- Dialogue plays before stages 1, 6, 11, 16 and 20 (labels 0–4 of `pbmsg.bf`). Stage dialogue never ends Puzzle Boy; only the quit confirmation does (corrected in M3 findings).
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

## M3 findings (2026.10.14)

Read through REA by four readers, each re-checked by an adversarial verifier: 94 claims, 66 confirmed, 17 corrected, 10 uncertain, 2 refuted. Only confirmed and corrected claims are recorded. The tick rate comes from gameplay video. The notes and pseudocode stay in `re/`.

**Ticks** (`SteamScreen.SetFrameRate` ev_a822b9618b15, `dds3DefaultMain.Update` ev_c9990f293085)
- The original runs one game tick per rendered frame, so its speed follows the frame-rate setting and the display.
- VERIFIED: the HD video runs at 30 ticks/s. Every move lasts a multiple of 8 frames of the 60 fps footage, and stage 1's five-cell walk lasts 40. The port runs at a fixed 30 ticks/s.

**Moves** (`moveChar` ev_e09bc775d13b, `moveBlock` ev_66ac7ea7f7e5, `roundBlock` ev_bbdfedee51eb, `deleteMoveBlock` ev_7fbe3f789bfc, `gameKey1` ev_5a3bbb17b4e4)
- A cell is 40 units, and cell (x, y) sits at (40x − 380, 40y − 380). A step moves the character 10 units per tick for 4 ticks, linearly. The next input is read on the 4th tick, so holding a direction walks continuously with no repeat delay.
- A push moves the block with the character over the same 4 ticks. A swivel turns 22.5° per tick for 4 ticks about its center. Both take one more tick before the next input.
- A fall sinks the block 40 units over 4 ticks. Then the hole becomes floor, SE03 plays, the pad vibrates and the camera shakes for 30 ticks.
- Facing changes at once to the input direction, even on a bump.
- Input is ignored for 2 ticks after a stage starts or restarts (`pbInitGame` ev_b52ab1ade7e0).

**Camera and controls** (`gameKey1` ev_a152ec51f1cb, `pbKeyConv.pbGetKeyAction`, `drawMyCharacter` ev_f367edf2c5b6)
- Directions are screen-relative. Each L1/R1 press turns the view exactly 90° and shifts the key mapping by a quarter turn.
  - The turn orbits the character 10° per tick for 9 ticks in the normal view, or 12 ticks in the overhead view.
  - Directions still work during a turn, while further L1, R1, ○ and □ presses wait for it to end.
- The camera follows the character, with a 60° field of view.
  - The normal view is pitched 60°, from an offset of (0, 400, −240) rotated with the view.
  - The overhead view is pitched 90°, from (0, 450, 0).
  - ○ switches between the two over 3 ticks.
- △ puts a scroll cursor on the character's cell. Held directions move the cursor and the camera one cell per tick within the grid, and arrows show which ways the cursor can still go.
- The view direction and the overhead view carry over between stages and restarts. They reset when Puzzle Boy is quit.

**Board** (`pbDraw.drawBlock` ev_913d4de77fec, `drawExit` ev_8df9c1b100d7, the UV tables in `pbDraw`'s static constructor ev_679447282679)
- Floor cells are 40-unit cubes below y = 0. Holes are empty, so the background shows through.
- Blocks and swivel cells are 40-unit cubes on top, textured from the `sample1` atlas, with white outlines on their exposed sides. The atlas is 4×2 tiles of 64 px: salmon blocks, grey swivels, and a dome on the swivel center.
- Walls have no top or body. Each side facing the play area is a floor-height band plus a 50-unit glass band at 20% opacity, outlined in grey.
- No normals are set, so the board is effectively unlit. That's inferred; the material shaders are asset data.
- The goal is a 20-unit untextured cube colored (0, 255, 200) at 30% opacity, 20 units up, spinning 1° per tick.
- The background `BG_pzl` (1920×1080) is a plane the camera code keeps facing the view.
- VERIFIED in HD footage: block and swivel outlines run along each group's outer top edges, not the bottom edges the code's numbers suggest. The wall panels are closed grey rectangles.
- VERIFIED against footage: the board is unlit. The floor in the video is within 2 levels of the atlas's floor texel.
- MEASURED: the backdrop shows 1.55 times larger than fitting a 16:9 screen, with the screen's center over image pixel (960, 372). That's from aligning `BG_pzl` with an overhead-view frame (correlation 0.95), and the port uses it for both views.
- INFERRED: the goal's color is written as 255, so blending it at 30% saturates to cyan over 70% of what's behind. That's how it glows in the footage.

**Character** (`pbInitMyCharacterDraw` ev_dc99c6b3c387, `drawMyCharacter` ev_f367edf2c5b6)
- `dvl0x150` holds two skinned meshes (24 and 8 bones), a lantern on the hand bone, eye quads, a shadow quad and 8 Mecanim clips at 30 samples/s. It's drawn at scale 40.
- VERIFIED against footage: the idle is clip `b00`, the controller's default state, at speed 1. It loops every 80 ticks, which is the footage's period, and it correlates 0.65 with three footage loops, against 0.16 or less for every other clip, facing or mirror.
- VERIFIED: Jack faces the way he moves, yaw 90° × the direction. Before a stage's first input he faces grid −y, toward the default camera. CODE: the facing turns on every accepted direction, even a bump; undo leaves it.
- INFERRED, fitted to footage: texture × a toon ramp from `tone2` (band 4), lit from screen-left at 1.1× brightness. There's no outline (VERIFIED). The shadow quad subtracts about 80% of its texture, and the lantern's warm glow is added around its hook point `h_fire`. re/m3-jack.md has the fit and the unknowns, such as whether the light turns with the view.

**HUD, menu and dialogue** (`pbStartGame` ev_84039538d4e0, `pbMenu.menuProc` ev_66d6465b0575, `pbInitGame` ev_24cd6dda05ba)
- The HUD is the `pzlUI` prefab in `common`. The stage counter, "COMPLETE!" and "UNDO" are sprites on the per-language sheet `pzlUI_font01_<lang>`, each banner shown for 30 ticks. The controls bar is the main game's button guide.
- □ opens a menu of Undo, Restart, Help and Quit. The cursor wraps. SE04 plays on open and accept, SE05 on cancel, SE06 on a cursor move and SE07 on a refusal. Undo is refused when there's no history.
- Help is one panel of 12 lines: rules and obstacles.
- Dialogue text is in the per-language bundles, shown in the main game's message window.
- Quit asks for confirmation: "I'm done for now." leaves, and "No, I'll keep trying." returns. Stage dialogue never ends Puzzle Boy. After the last stage the game ends Puzzle Boy itself, without asking.
- The only fonts are TextMeshPro SDF atlases (FOT-Matisse ProN B); no TTF or OTF is shipped.

**Sound** (`pbGame.game` ev_c6e6a20a255d, `pbStartMoveBlock`)
- SE00 step, SE01 push, SE02 turn, SE03 fall, SE08 view change or scroll toggle, SE09 goal; SE04–07 belong to the menu. A bump is silent, and every effect plays at full volume.
- The music, `JYAKYO02_BGM00`, starts with the minigame and fades out on quit. Its looping and fade length are in protected code.

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

## M3: Presentation and release (done 2026.10.14)

- [x] Answer M2's open control questions from the game's code: how directions map after an L1/R1 turn and by how much the view turns, whether holding a direction repeats, and undo in play. Label each answer as in M2.
  - CODE: directions follow the view, and each L1/R1 press turns it 90°. Holding a direction walks continuously. Undo works as M0 says. None of it has been seen in play yet. See "M3 findings".
- [x] Camera, Jack-o'-Lantern, stage dialogue, music and sound effects.
  - Done: the 30-tick clock, the move timing, the follow camera with L1/R1, the overhead view and scroll mode.
  - Done: the board's look: the atlas textures, glass walls, outlines, the goal and the backdrop, all read from the player's install.
  - Done: the sound effects (step, push, turn, fall, view change and goal) and the looping music, with the original's triggers. The menu sounds come with the menu.
  - Done: Jack-o'-Lantern from the player's install: his skinned model, idle animation, facing, toon shading, shadow and lantern glow.
  - Done: the HUD (stage counter, COMPLETE!, UNDO, the scroll arrows and the controls bar) and the Manikin Kid's talk before stages 1, 6, 11, 16 and 20, drawn with the game's own font and sheets.
- [x] Menus: undo, retry, help and quit, matching the original's options.
  - The □ menu, help page and quit question replace the Z, R and Backspace shortcuts, with SE04–07. The menu matches the HD footage at 561.75 s. Help and the choice box come from the prefab files and were never seen in play.
- [x] Release builds for Windows, macOS and Linux.
  - `release.yml` builds self-contained archives for win-x64, linux-x64, osx-arm64 and osx-x64 from Linux, and a `v*` tag publishes them. Before publishing, each build must start on its own system in CI and reach the "no game here" message: Windows, Linux and Apple Silicon macOS pass ([run 38579842875](https://github.com/hazeliscoding/puzzle-boy/actions/runs/38579842875)).
  - Played stage 1 from the owner's install: the Windows build, and the Linux build under WSL (WSLg, Mesa llvmpipe). macOS has no tester yet, and its builds are unsigned.
  - [v0.1.0](https://github.com/hazeliscoding/puzzle-boy/releases/tag/v0.1.0) published all four archives after the start checks passed on all three systems ([run 38610613537](https://github.com/hazeliscoding/puzzle-boy/actions/runs/38610613537)).

**Done when** the release builds start on all three systems and play stage 1 from the player's install.

## M4: Bonus stages

The stage files the original ships but never plays, after stage 20. See the decisions above.

- [ ] Switch key: C passes control to the next visible character. Undo still returns control to the character that made the undone push or turn. Rules tests first, drawn as text with two characters.
- [ ] Settle the unknown stages: a stronger search for the 9 capped single-character stages and for 18, 27 and 47 with the switch, run on the port's `Board`. A stage with no solution found stays out.
- [ ] Play the bonus stages after stage 20, easiest first. The bar shows the switch on multi-character stages, and PageUp/PageDown become a documented control to skip stages.

**Done when** every bonus stage that ships has a solution that clears it in the port, and the switch's rules have tests.

## Later

- A stage editor. The original ships a developer editor (`pbEdit`).
- Custom stage packs.
- Discord Rich Presence: the stage you're on ("Stage 7 of 20") and time played, with a setting to turn it off. Needs a Discord application, whose name and image come from the brand (no Atlus art), and an MIT client library such as DiscordRichPresence. It does nothing when Discord isn't running.
- An Android build. MonoGame targets Android, and the rules library carries over as is. A phone has no Steam install, so the player copies the few bundles Puzzle Boy reads from their PC install (about 85 MB, most of it `common`) and picks them in the app. Needs touch controls: swipe to move, buttons to turn the view.
- A fifth □ menu item, Bonus, that jumps to the bonus stages. Its label has no text in the game's tables.

## Not planned

- Shipping or downloading any Atlus asset.
- Circumventing Denuvo, patching the original game or modding it.
- Nocturne's other minigames.

## How we'll know it works

A solution recorded in the original game solves the same stage in the port. The same moves leave the board in the same state, and a move the original rejects, the port rejects too. The original has no move counter to compare.
