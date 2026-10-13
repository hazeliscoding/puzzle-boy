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
- When recording a finding in `ROADMAP.md`, cite the REA Evidence or the in-game observation behind it. Keep observations, inferences and unknowns apart.

## Commands

- `dotnet build PuzzleBoy.slnx` builds everything. Warnings are errors.
- `dotnet test --solution PuzzleBoy.slnx` runs the tests. They use handmade stages only and never need the game installed.
- `dotnet format PuzzleBoy.slnx --verify-no-changes` checks the style CI will enforce.
- `dotnet run --project src/PuzzleBoy` starts the game.

## Code layout

- `src/PuzzleBoy.Rules`: stage parsing and the rules. No MonoGame and no file I/O, so the tests can drive it with plain bytes.
- `src/PuzzleBoy`: the MonoGame (DesktopGL) game. It finds the player's install and draws. It has no content pipeline; every asset is read at runtime.
- `tests/PuzzleBoy.Rules.Tests`: xunit.v3 on Microsoft Testing Platform. Test names read as sentences.

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `ci:`, `build:`, `refactor:`). Keep each commit atomic, and use a scope when it adds clarity (`feat(rules): …`). Push as you go.
- **No AI attribution** in commits or PRs. That means no `Co-Authored-By` trailers, no "Generated with" lines and no session links.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. Every check that tests for an absence needs a positive control.
- **Docs:** short and concise. Prefer editing `ROADMAP.md` over creating new planning documents.
- **Code comments:** explain why, not what.
