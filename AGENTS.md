# AGENTS.md

These are the working rules for agents in this repo. Puzzle Boy is a standalone, cross-platform rebuild of the Puzzle Boy minigame from *Shin Megami Tensei III Nocturne HD Remaster*, licensed Apache-2.0.

## Sources of truth

- `README.md`: the pitch and the bring-your-own-copy promise.
- `ROADMAP.md`: decisions already made, the milestones, and what is out of scope. Work from the next unchecked item and record new decisions there. Don't build past the current milestone without asking.

## Hard rules

- **No Atlus data in the repo.** That covers stage files, text, audio, textures, models, asset bundles, metadata dumps, method address tables and decompiler output. Research output goes in `re/`, which git ignores. Test stages are handmade.
- **No decompiled code in the source.** Turn what the decompiler shows into a test on a handmade stage, then write the code from the test. Don't paste or transliterate pseudocode.
- **No circumvention.** `GameAssembly.dll` is protected by Denuvo Anti-Tamper. Analyze only its unprotected `.text` section, statically. Don't dump the running game's memory, de-obfuscate protected code or patch the game. If a function leads into protected code, stop and answer the question by playing the real game.
- **No secrets or private paths** in committed files.

## Reverse engineering

- Use REA with Ghidra for native analysis. Its MCP server needs `GHIDRA_INSTALL_DIR` (Ghidra 12.1.x), `JAVA_HOME` (JDK 21), a large `GHIDRA_HEADLESS_MAXMEM`, a long `REA_GHIDRA_STARTUP_TIMEOUT_MS`, and `REA_GHIDRA_SEED_FILE` pointing at `re/ghidra-seeds.tsv`.
- Seeded Ghidra function names are `Type__Method`, such as `pbGame__pushCheck`. `re/methods.tsv` maps each method to its RVA and section.
- Open `GameAssembly.dll` with the saved snapshot in `re/ghidra/`. Don't re-run the full analysis.
- The full analysis needs most of a 32 GB machine. On the first try, Java reached 11.4 GB private memory after 52 minutes and left 0.1 GB of RAM free. Run it only when the owner's machine is otherwise idle, launched detached so it outlives the agent session. Never start it while the owner is working.
- When recording a finding in `ROADMAP.md`, cite the REA Evidence or the in-game observation behind it. Keep observations, inferences and unknowns apart.

## Commands

- `dotnet build PuzzleBoy.slnx` builds everything. Warnings are errors.
- `dotnet test --solution PuzzleBoy.slnx` runs the tests. They use handmade stages only and never need the game installed.
- `dotnet format PuzzleBoy.slnx --verify-no-changes` checks the style CI will enforce.
- `dotnet run --project src/PuzzleBoy` starts the game, finding the install through Steam. `dotnet run --project src/PuzzleBoy -- <game folder>` points it at a copy elsewhere. Arrow keys or WASD move (hold to keep walking), Q and E turn the view (L1/R1), V switches to overhead (○), Tab scrolls (△), C switches characters on multi-character stages, F opens the menu with undo, restart, help and quit (□), Enter or Space confirms, Backspace cancels, PageUp/PageDown skip stages, Escape quits. The game runs at the original's 30 ticks a second.
- `scripts/check-no-game-data.sh` fails if tracked files hold Atlus data or decompiler output. `scripts/check-no-game-data.test.sh` is its positive control: it plants each kind in a scratch repo and expects a failure. When you add a new kind, add both a check and a planted case.

## Code layout

- `src/PuzzleBoy.Rules`: stage parsing (`Stage`, `Cell`), the rules (`Board`) and the original's play order (`PlayOrder`). No MonoGame and no file I/O, so the tests can drive it with plain bytes.
- Rules tests draw handmade stages as text with `tests/PuzzleBoy.Tests/Ascii.cs`; its summary has the legend. A rule change starts with a drawing of the case.
- `src/PuzzleBoy`: the MonoGame (DesktopGL) game. `GameInstall` finds the player's Steam install, or takes the game folder as the first argument, and reads bundles with AssetsTools.NET. It has no content pipeline; every asset is read at runtime.
- `tests/PuzzleBoy.Tests`: xunit.v3 on Microsoft Testing Platform, covering both projects. Test names read as sentences.

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `ci:`, `build:`, `refactor:`). Keep each commit atomic, and use a scope when it adds clarity (`feat(rules): …`). Push as you go.
- **No AI attribution** in commits or PRs. That means no `Co-Authored-By` trailers, no "Generated with" lines and no session links.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. Every check that tests for an absence needs a positive control.
- **Docs:** short and concise. Prefer editing `ROADMAP.md` over creating new planning documents.
- **Documentation:** every public type and member has an XML doc comment that says what it's for and any contract a caller can't see from the signature: units, ranges, what's null, what throws. Overrides use `<inheritdoc />`. The build fails on a missing one (CS1591); tests are exempt. Give a private helper a summary when its purpose isn't obvious from its name.
- **Comments in code:** explain why, not what: a constraint, a quirk of the original game, a workaround and its cause. Mark a known limit as `shortcut: <the limit>, <when to upgrade>`.
- **Pragmatic code:** the smallest change that fully works. No abstractions, options or layers for later, and nothing the current roadmap item doesn't need.
