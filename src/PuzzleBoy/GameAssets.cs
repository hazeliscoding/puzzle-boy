using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>Everything the game reads from the player's install at startup.</summary>
/// <param name="Stages">The stages to play in order, each with its file number.</param>
/// <param name="Atlas">The board atlas, <c>sample1</c>.</param>
/// <param name="Background">The backdrop, <c>BG_pzl</c>.</param>
/// <param name="Sounds">The sound effects and music, by clip name.</param>
/// <param name="Character">Jack-o'-Lantern.</param>
/// <param name="Ui">The HUD and menu sheets.</param>
/// <param name="Text">The font, its materials and the strings.</param>
internal sealed record GameAssets(
    IReadOnlyList<(int File, Stage Stage)> Stages, TextureData Atlas, TextureData Background,
    IReadOnlyDictionary<string, SoundData> Sounds, CharacterData Character, UiSheets Ui, TextData Text)
{
    /// <summary>The M4 bonus files played after stage 20; removed in the next commit, which restores the original 20.</summary>
    internal static IReadOnlyList<int> Bonus { get; } = [1, 30, 31, 0, 3, 9, 35, 43, 5, 6, 33, 7, 19, 47, 39, 41, 45, 34, 12, 46, 15, 11, 44];

    /// <summary>Reads every asset, opening each bundle once; <c>common</c> is large and slow to unpack.</summary>
    public static GameAssets Read(GameInstall install)
    {
        var text = install.ReadTextAssets("pazzleboy");
        var stages = OriginalStageOrder.Files.Concat(Bonus).Select(file => (file, Stage.Parse(text[OriginalStageOrder.AssetName(file)]))).ToList();
        var common = install.ReadTextures("common", "BG_pzl", "tone2", "pzlUI01", "pzlUI_font01_ja", "talkUI01");
        var english = install.ReadTextures("ui_tex_en", "pzlUI_font01_en", "buttonUI01_en_common");
        return new GameAssets(
            stages,
            install.ReadTexture("pazzleboy", "sample1"),
            common["BG_pzl"],
            install.ReadSounds("pazzleboy"),
            install.ReadCharacter(common["tone2"]),
            new UiSheets(common["pzlUI01"], english["pzlUI_font01_en"], common["pzlUI_font01_ja"],
                install.ReadTexture("ui_tex_keyboard_en", "buttonUI01_en_keyboard"), common["talkUI01"], english["buttonUI01_en_common"]),
            install.ReadText());
    }
}
