<h1>
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/brand/lockup-dark.svg">
    <img alt="Puzzle Boy" src="docs/brand/lockup.svg" height="40">
  </picture>
</h1>

**The Puzzle Boy minigame from *Shin Megami Tensei III: Nocturne*, as a standalone game.**

In Nocturne, Puzzle Boy sits deep inside a long RPG. You guide Jack-o'-Lantern through a maze by pushing blocks, turning swivel blocks and filling holes, while the Manikin Kid taunts you. It is a 3D take on Atlus's Game Boy puzzler of the same name, released in the West as Kwirk.

This project rebuilds the minigame so it runs on its own on Windows, macOS and Linux. It reads the stages, text and sounds from your own copy of *Shin Megami Tensei III Nocturne HD Remaster*.

> **Status:** early development. The original's rules are decoded and implemented, and all 20 stages can be played from your install with the original's board look, camera, timing, sound, HUD, dialogue, menu and an animated Jack-o'-Lantern. Solutions recorded in the original clear every stage in the port. See [ROADMAP.md](ROADMAP.md).

## Bring your own copy

This repository contains no Atlus code or data: no stages, text, audio, textures, models or decompiled code. The game finds your Steam install and reads them from there. Without the original game, it has nothing to show.

## Play

1. Download the archive for your system from [Releases](https://github.com/hazeliscoding/puzzle-boy/releases) and unpack it.
2. Run `PuzzleBoy` (`PuzzleBoy.exe` on Windows). It looks for the game in your Steam libraries. If no window opens, it didn't find the game: run it from a terminal with the folder that holds `smt3hd.exe`, for example `PuzzleBoy "D:\Games\smt3hd"`.

Arrow keys or WASD move, Q and E turn the view, V switches to overhead, Tab scrolls, C switches characters on bonus stages with several, F opens the menu (undo, restart, help, quit), Enter or Space confirms, Backspace cancels, and Escape quits.

The builds aren't signed, so Windows SmartScreen and macOS Gatekeeper may warn before the first start. On macOS, `xattr -dr com.apple.quarantine` on the unpacked folder lets it run. Nocturne HD has no Mac version, so a Mac needs a copy of the game folder from a PC. Nobody has played the macOS builds yet, so reports are welcome.

## How it is built

- The 50 stage files in the game's `pazzleboy` asset bundle are small binary grids. Their format is decoded and recorded in the roadmap.
- The rules come from static analysis of the game's code with [REA](https://rea.tools) and Ghidra. The analysis reads only the game's unprotected code and does not bypass its anti-tamper protection.
- Each rule becomes a test on a handmade stage, and the game is written from those tests, never from decompiler output.

## License

The code in this repository is licensed under [Apache-2.0](LICENSE).

*Shin Megami Tensei*, *Nocturne* and *Puzzle Boy* are trademarks of ATLUS and SEGA. This is a fan project. It is not affiliated with or endorsed by either company.
