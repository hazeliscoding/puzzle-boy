using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>Everything the game reads from the player's install at startup.</summary>
/// <param name="Stages">The stages to play in order, each with its file number.</param>
/// <param name="Atlas">The board atlas, <c>sample1</c>.</param>
/// <param name="Background">The backdrop, <c>BG_pzl</c>.</param>
/// <param name="Sounds">The sound effects and music, by clip name.</param>
/// <param name="Character">Jack-o'-Lantern.</param>
/// <param name="Ui">The HUD and menu sheets.</param>
internal sealed record GameAssets(
    IReadOnlyList<(int File, Stage Stage)> Stages, TextureData Atlas, TextureData Background,
    IReadOnlyDictionary<string, SoundData> Sounds, CharacterData Character, UiSheets Ui)
{
    /// <summary>Reads every asset, opening each bundle once; <c>common</c> is large and slow to unpack.</summary>
    public static GameAssets Read(GameInstall install)
    {
        var text = install.ReadTextAssets("pazzleboy");
        var stages = PlayOrder.Files.Select(file => (file, Stage.Parse(text[PlayOrder.AssetName(file)]))).ToList();
        var common = install.ReadTextures("common", "BG_pzl", "tone2", "pzlUI01", "pzlUI_font01_ja");
        return new GameAssets(
            stages,
            install.ReadTexture("pazzleboy", "sample1"),
            common["BG_pzl"],
            install.ReadSounds("pazzleboy"),
            install.ReadCharacter(common["tone2"]),
            new UiSheets(common["pzlUI01"], install.ReadTexture("ui_tex_en", "pzlUI_font01_en"), common["pzlUI_font01_ja"]));
    }
}
