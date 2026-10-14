# Bonus stages

> [!TIP]
> **TL;DR:** The game ships 50 stage files and plays 20. The other 30 come after stage 20 in the port, but only the ones a search has solved. Six of the seven stages with several Jack-o'-Lanterns can't be finished under the original's rules, because only the first character can move until it reaches the goal. The port adds a switch key, C, which makes three of them winnable so far.

**Read time:** 4 minutes · **Status:** M4, in progress · **Code:** `PlayOrder.Bonus`, `Board.Switch`

## The 50 files

```mermaid
pie title The 50 stage files, as of Oct 14
    "Played by the original" : 20
    "Unused, solved as shipped" : 15
    "Unused, solved with the switch" : 3
    "Unused, not settled yet" : 12
```

- The original's code lists file 00 for "levels 2 and 3", but every video and guide ends after stage 20, so 00 counts as unused here.
- None of the 30 is a duplicate or junk. Every start and goal is on floor, and no goal is walled off. A few are tutorial-sized, at 3 or 4 pushes.
- They include all seven multi-character stages, and the six files damaged by the line-ending conversion.

## How the survey worked

- A local search explored each stage by pushes and turns, treating walking anywhere Jack can reach as free.
- Every candidate move ran on the port's real `Board`, built from the real file, so a solution can't rely on a rule the port doesn't have.
- Breadth-first search finds the fewest pushes. A* takes over when the space gets big, and its counts are upper bounds.
- A stage is called **unwinnable** only when the search runs out of positions. One that hits the search budget is **not settled**, not lost.
- Every solution is replayed input by input on a fresh board before anything ships.

## Why six stages are stuck

- In the original, control passes to the next character only when one reaches the goal. Nothing else switches.
- So character 1 has to get home alone, using only its own pushes, before anyone else can move.
- The search ran out of positions on six stages: 05, 18, 27, 43, 45 and 47. Modeling the original's swapped collision check changes nothing.
- File 05 shows why. One push can only be made from a dead-end alcove that the block itself seals off, so somebody has to wait in that alcove while the other character brings the block over.

> [!NOTE]
> **INFERRED:** these stages look designed for a switch that the shipped code doesn't have. With one, 05, 43 and 45 have solutions. 18, 27 and 47 haven't been settled yet.

## The switch

- **C** passes control to the next visible character, in the same order a goal arrival uses.
- It isn't recorded for undo, so undo still hands control back to whoever made the undone push or turn.
- It plays the view-change sound, because the camera moves to the new character.
- On the controls bar it shows only while two or more characters are out. Its label, "Switch Character", is the game's own guide text.
- It's a deliberate difference from the original, logged in ROADMAP's M4 decisions.

## Order

- The bonus stages are ordered easiest first, by the pushes and turns in the solution found, with the file number breaking ties. The tutorial-sized ones come first.
- On Oct 14 that was 18 stages, which play as stages 21 to 38. The list grows as M4's search settles more.

## Where to look

- `src/PuzzleBoy.Rules/PlayOrder.cs`: `Bonus`, the list and its order.
- `src/PuzzleBoy.Rules/Board.cs`: `Switch`.
- `tests/PuzzleBoy.Tests/SwitchTests.cs` and `PlayOrderTests.cs`.
