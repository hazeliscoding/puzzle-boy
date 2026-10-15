using PuzzleBoy;
using PuzzleBoy.Nocturne;

// shortcut: a missing game is reported on stderr before any window opens; an in-game message needs the fonts M3 brings.
// The bundles copied beside the executable come first, so a double-click works with the game uninstalled.
var install = (args.Length > 0 ? GameInstall.At(args[0]) : GameInstall.At(AppContext.BaseDirectory) ?? GameInstall.Find())
    ?? throw new DirectoryNotFoundException(args.Length > 0
        ? $"There's no Shin Megami Tensei III Nocturne HD Remaster at {args[0]}. Pass the folder that holds smt3hd.exe."
        : "Couldn't find Shin Megami Tensei III Nocturne HD Remaster beside PuzzleBoy or in your Steam libraries. Pass the folder that holds smt3hd.exe as the first argument.");

using var game = new PuzzleBoyGame(GameAssets.Read(install));
game.Run();
