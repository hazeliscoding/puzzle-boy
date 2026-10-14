using PuzzleBoy;
using PuzzleBoy.Rules;

// shortcut: a missing game is reported on stderr before any window opens; an in-game message needs the fonts M3 brings.
var install = (args.Length > 0 ? GameInstall.At(args[0]) : GameInstall.Find())
    ?? throw new DirectoryNotFoundException(args.Length > 0
        ? $"There's no Shin Megami Tensei III Nocturne HD Remaster at {args[0]}. Pass the folder that holds smt3hd.exe."
        : "Couldn't find Shin Megami Tensei III Nocturne HD Remaster in your Steam libraries. Pass the folder that holds smt3hd.exe as the first argument.");

var assets = install.ReadTextAssets("pazzleboy");
var stages = PlayOrder.Files.Select(file => (file, Stage.Parse(assets[PlayOrder.AssetName(file)]))).ToList();

using var game = new PuzzleBoyGame(stages);
game.Run();
