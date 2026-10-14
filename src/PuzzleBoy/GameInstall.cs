using System.Text.RegularExpressions;
using AssetsTools.NET.Extra;
using Microsoft.Win32;

namespace PuzzleBoy;

/// <summary>A texture from the game, every mip level as RGBA bytes in Unity's row order: the bottom row first.</summary>
/// <param name="Width">The top level's width in pixels.</param>
/// <param name="Height">The top level's height in pixels.</param>
/// <param name="Levels">The mip levels, largest first, each half the size of the one before.</param>
public sealed record TextureData(int Width, int Height, IReadOnlyList<byte[]> Levels);

/// <summary>The player's copy of Nocturne HD Remaster. Every asset the port uses is read from it.</summary>
public sealed partial class GameInstall
{
    private GameInstall(string directory) => Directory = directory;

    /// <summary>The game's folder, the one that holds <c>smt3hd.exe</c> and <c>smt3hd_Data</c>.</summary>
    public string Directory { get; }

    /// <summary>The game at <paramref name="directory"/>, or null when its Puzzle Boy bundle isn't there.</summary>
    public static GameInstall? At(string directory) =>
        File.Exists(BundlePath(directory, "pazzleboy")) ? new GameInstall(directory) : null;

    /// <summary>Looks for the game in every Steam library on this machine.</summary>
    public static GameInstall? Find() =>
        SteamRoots()
            .SelectMany(root => LibraryFolders(root).Prepend(root))
            .Select(library => At(Path.Combine(library, "steamapps", "common", "smt3hd")))
            .FirstOrDefault(install => install is not null);

    /// <summary>The library paths listed in Steam's <c>libraryfolders.vdf</c>.</summary>
    public static IEnumerable<string> LibraryPaths(string libraryFoldersVdf) =>
        PathEntry().Matches(libraryFoldersVdf).Select(match => match.Groups[1].Value.Replace(@"\\", @"\"));

    /// <summary>Every TextAsset in one of the game's bundles, by name.</summary>
    public Dictionary<string, byte[]> ReadTextAssets(string bundle)
    {
        var manager = new AssetsManager();
        try
        {
            var bundleFile = manager.LoadBundleFile(BundlePath(Directory, bundle), true);
            var assets = manager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            return assets.file.GetAssetsOfType(AssetClassID.TextAsset)
                .Select(info => manager.GetBaseField(assets, info))
                .ToDictionary(field => field["m_Name"].AsString, field => field["m_Script"].AsByteArray);
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    /// <summary>
    /// Texture2D <paramref name="name"/> from one of the game's bundles, every mip level decoded to RGBA.
    /// Throws <see cref="InvalidDataException"/> when it's missing or in a format the port doesn't read (DXT1 and RGB24 only).
    /// </summary>
    public TextureData ReadTexture(string bundle, string name)
    {
        var manager = new AssetsManager();
        try
        {
            var bundleFile = manager.LoadBundleFile(BundlePath(Directory, bundle), true);
            var assets = manager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            var texture = assets.file.GetAssetsOfType(AssetClassID.Texture2D)
                .Select(info => manager.GetBaseField(assets, info))
                .FirstOrDefault(field => field["m_Name"].AsString == name)
                ?? throw new InvalidDataException($"There's no texture {name} in {bundle}.");

            // Large textures keep their pixels in the bundle's .resS entry; small ones inline.
            var stream = texture["m_StreamData"];
            byte[] data;
            if (stream["size"].AsUInt > 0)
            {
                var file = bundleFile.file;
                file.GetFileRange(file.GetFileIndex(Path.GetFileName(stream["path"].AsString)), out var start, out _);
                file.DataReader.Position = start + (long)stream["offset"].AsULong;
                data = file.DataReader.ReadBytes((int)stream["size"].AsUInt);
            }
            else
            {
                data = texture["image data"].AsByteArray;
            }

            var (width, height, format) = (texture["m_Width"].AsInt, texture["m_Height"].AsInt, texture["m_TextureFormat"].AsInt);
            var levels = new List<byte[]>();
            var at = 0;
            for (var level = 0; level < texture["m_MipCount"].AsInt; level++)
            {
                var (w, h) = (Math.Max(1, width >> level), Math.Max(1, height >> level));
                var (pixels, size) = format switch
                {
                    10 => (TextureDecoder.Dxt1(data.AsSpan(at), w, h), TextureDecoder.Dxt1Size(w, h)),
                    3 => (TextureDecoder.Rgb24(data.AsSpan(at), w, h), w * h * 3),
                    _ => throw new InvalidDataException($"Texture {name} in {bundle} has format {format}, which the port doesn't read."),
                };
                levels.Add(pixels);
                at += size;
            }

            return new TextureData(width, height, levels);
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    private static string BundlePath(string directory, string bundle) =>
        Path.Combine(directory, "smt3hd_Data", "StreamingAssets", "PC", bundle);

    private static IEnumerable<string> LibraryFolders(string steamRoot)
    {
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        return File.Exists(vdf) ? LibraryPaths(File.ReadAllText(vdf)) : [];
    }

    private static IEnumerable<string> SteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string steamPath)
            {
                yield return steamPath;
            }

            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
        }
        else
        {
            // Native, the Debian package's symlink, and Flatpak. The game itself runs under Proton there.
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex PathEntry();
}
