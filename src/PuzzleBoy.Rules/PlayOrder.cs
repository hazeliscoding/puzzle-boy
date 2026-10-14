namespace PuzzleBoy.Rules;

/// <summary>The stage files the original plays, in its order (ROADMAP.md, "M0 findings": stages and dialogue).</summary>
public static class PlayOrder
{
    /// <summary>
    /// Level 1's 20 stages, then file 00, the single stage of levels 2 and 3. The other 29 files, including the
    /// six damaged ones and every multi-character stage, are never played.
    /// </summary>
    public static IReadOnlyList<int> Files { get; } = [2, 4, 32, 8, 38, 36, 37, 48, 42, 49, 40, 20, 22, 13, 21, 24, 14, 10, 25, 26, 0];

    /// <summary>The TextAsset name of stage file <paramref name="file"/> in the <c>pazzleboy</c> bundle.</summary>
    public static string AssetName(int file) => $"pbMap{file:D2}.map";
}
