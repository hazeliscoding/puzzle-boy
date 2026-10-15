<h1 align="center">
  <img src="https://github.com/user-attachments/assets/4a3d0618-c3d1-49f7-9241-d0bd7fc07dc0" alt="Puzzle Boy: preservation port" width="640">
</h1>

<p align="center"><b>A faithful preservation port of the Puzzle Boy minigame from <i>Shin Megami Tensei III Nocturne HD Remaster</i>. It reads your own copy of the game and plays the original 20 stages.</b></p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/ec704087-0cf0-4a34-b00e-881fbe98ff8f" alt="The Manikin Kid introduces stage 1, then Jack-o'-Lantern turns the view, switches to overhead and undoes a push from the menu in stages 2 and 3" width="800">
  <br><em>Rendered by the port from a local copy of the game, at the original's speed.</em>
</p>

<table>
  <tr>
    <td align="center" width="33%">
      <a href="https://github.com/user-attachments/assets/f9d80fa1-cf8b-4e6f-b6c2-f9656f4057fc"><img src="https://github.com/user-attachments/assets/f9d80fa1-cf8b-4e6f-b6c2-f9656f4057fc" alt="The Manikin Kid explains the goal before stage 1" width="100%"></a>
      <br><sub>The Manikin Kid, before stage 1</sub>
    </td>
    <td align="center" width="33%">
      <a href="https://github.com/user-attachments/assets/1e67d749-3bdf-4d12-b87c-5fbf93592ba4"><img src="https://github.com/user-attachments/assets/1e67d749-3bdf-4d12-b87c-5fbf93592ba4" alt="The original's menu, with UNDO taking back a push" width="100%"></a>
      <br><sub>The □ menu and its undo</sub>
    </td>
    <td align="center" width="33%">
      <a href="https://github.com/user-attachments/assets/db0056d3-8456-40fe-9dba-3e1dbb45ecc9"><img src="https://github.com/user-attachments/assets/db0056d3-8456-40fe-9dba-3e1dbb45ecc9" alt="COMPLETE! after the last push of a stage" width="100%"></a>
      <br><sub>COMPLETE!</sub>
    </td>
  </tr>
  <tr>
    <td align="center" width="33%">
      <a href="https://github.com/user-attachments/assets/b1d2c8c1-5acb-447f-ba57-c1985ae90e62"><img src="https://github.com/user-attachments/assets/b1d2c8c1-5acb-447f-ba57-c1985ae90e62" alt="Stage 5, a field of swivel blocks" width="100%"></a>
      <br><sub>Stage 5: swivels</sub>
    </td>
    <td align="center" width="33%">
      <a href="https://github.com/user-attachments/assets/8caa96b6-c7f4-46e3-bb60-f33d9d85666a"><img src="https://github.com/user-attachments/assets/8caa96b6-c7f4-46e3-bb60-f33d9d85666a" alt="Stage 10, with large pushable blocks" width="100%"></a>
      <br><sub>Stage 10: big blocks</sub>
    </td>
    <td align="center" width="33%">
      <a href="https://github.com/user-attachments/assets/5e33fc24-5047-4fac-80a3-e1559a141e53"><img src="https://github.com/user-attachments/assets/5e33fc24-5047-4fac-80a3-e1559a141e53" alt="Stage 20, the last stage" width="100%"></a>
      <br><sub>Stage 20, the last one</sub>
    </td>
  </tr>
</table>

<p align="center"><sub>Every image is rendered by the port from a local copy of the game and hosted as an upload; none of it is in this repository or its downloads.</sub></p>

In Nocturne, Puzzle Boy sits deep inside a long RPG. You guide Jack-o'-Lantern through a maze by pushing blocks, turning swivel blocks and filling holes, while the Manikin Kid taunts you. It is a 3D take on Atlus's Game Boy puzzler of the same name, released in the West as Kwirk.

This project rebuilds the minigame so it runs on its own on Windows, macOS and Linux, as a record of how the original plays. It reads the stages, text and sounds from your own copy of *Shin Megami Tensei III Nocturne HD Remaster*.

> **Want more than the original 20?** [Puzzle Boy Community Edition](https://github.com/hazeliscoding/puzzle-boy-community) is the Nocturne minigame expanded: new stages, a level editor, level packs, and the bonus stages and switch key that v0.2.0 had. Its Classic theme comes from your own copy of Nocturne, and without one it plays out of the box with its bundled Default theme. Download it from its [releases](https://github.com/hazeliscoding/puzzle-boy-community/releases).

> **Status:** early development. The original's rules are decoded and implemented, and all 20 stages can be played from your install with the original's board look, camera, timing, sound, HUD, dialogue, menu and an animated Jack-o'-Lantern. Solutions recorded in the original clear every stage in the port. See [ROADMAP.md](ROADMAP.md).

## Bring your own copy

This repository contains no Atlus code or data: no stages, text, audio, textures, models or decompiled code. The game finds your Steam install and reads them from there. Without the original game, it has nothing to show.

## Play

1. Download the archive for your system from [Releases](https://github.com/hazeliscoding/puzzle-boy/releases) and unpack it.
2. Run `PuzzleBoy` (`PuzzleBoy.exe` on Windows). It looks for the game beside itself, then in your Steam libraries. If no window opens, it didn't find the game: run it from a terminal with the folder that holds `smt3hd.exe`, for example `PuzzleBoy "D:\Games\smt3hd"`.

Arrow keys or WASD move, Q and E turn the view, V switches to overhead, Tab scrolls, F opens the menu (undo, restart, help, quit), Enter or Space confirms, Backspace cancels, and Escape quits. After stage 20 the board stays cleared. v0.2.0 also played 23 bonus stages after stage 20; they now belong to Community Edition.

The builds aren't signed, so Windows SmartScreen and macOS Gatekeeper may warn before the first start. On macOS, `xattr -dr com.apple.quarantine` on the unpacked folder lets it run. Nocturne HD has no Mac version, so a Mac needs a copy of the game folder from a PC. Nobody has played the macOS builds yet, so reports are welcome.

## How it is built

- The 50 stage files in the game's `pazzleboy` asset bundle are small binary grids. Their format is decoded and recorded in the roadmap.
- The rules come from static analysis of the game's code with [REA](https://rea.tools) and Ghidra. The analysis reads only the game's unprotected code and does not bypass its anti-tamper protection.
- Each rule becomes a test on a handmade stage, and the game is written from those tests, never from decompiler output.
- The rules live in [puzzle-boy-engine](https://github.com/hazeliscoding/puzzle-boy-engine), which Community Edition shares. This repo pins it as a git submodule, so clone with `--recurse-submodules`.
- [The docs](docs/README.md) walk through all of it, a few minutes per topic: the journey, the architecture, the rules, the timing, reading the game's files, the rendering, the bonus stages (now in Community Edition) and the checks.

## License

The code in this repository is licensed under [Apache-2.0](LICENSE).

*Shin Megami Tensei*, *Nocturne* and *Puzzle Boy* are trademarks of ATLUS and SEGA. This is a fan project. It is not affiliated with or endorsed by either company.
