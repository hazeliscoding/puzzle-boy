namespace PuzzleBoy;

/// <summary>The stage files the original plays, in its order (ROADMAP.md, "M0 findings": stages and dialogue).</summary>
internal static class OriginalStageOrder
{
    /// <summary>
    /// Level 1's 20 stages. The code lists file 00 as the single stage of levels 2 and 3, but play ends after
    /// stage 20 in every video and guide. The other 30 files, including the six damaged ones and every
    /// multi-character stage, are never played.
    /// </summary>
    public static IReadOnlyList<int> Files { get; } = [2, 4, 32, 8, 38, 36, 37, 48, 42, 49, 40, 20, 22, 13, 21, 24, 14, 10, 25, 26];

    /// <summary>The TextAsset name of stage file <paramref name="file"/> in the <c>pazzleboy</c> bundle.</summary>
    public static string AssetName(int file) => $"pbMap{file:D2}.map";
}
