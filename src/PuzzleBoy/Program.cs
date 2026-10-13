using PuzzleBoy;
using PuzzleBoy.Rules;

// shortcut: a missing game is reported on stderr before any window opens; an in-game message needs the fonts M3 brings.
var install = (args.Length > 0 ? GameInstall.At(args[0]) : GameInstall.Find())
    ?? throw new DirectoryNotFoundException(args.Length > 0
        ? $"There's no Shin Megami Tensei III Nocturne HD Remaster at {args[0]}. Pass the folder that holds smt3hd.exe."
        : "Couldn't find Shin Megami Tensei III Nocturne HD Remaster in your Steam libraries. Pass the folder that holds smt3hd.exe as the first argument.");

var stages = install.ReadTextAssets("pazzleboy")
    .Where(asset => asset.Key.StartsWith("pbMap", StringComparison.Ordinal))
    .OrderBy(asset => asset.Key, StringComparer.Ordinal)
    .Select(asset => Stage.Parse(asset.Value))
    .ToList();

using var game = new PuzzleBoyGame(stages);
game.Run();
