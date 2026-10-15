namespace PuzzleBoy.Tests;

public sealed class PlayOrderTests
{
    [Fact]
    public void Bonus_stages_are_unused_files_each_played_once()
    {
        Assert.Empty(GameAssets.Bonus.Intersect(OriginalStageOrder.Files));
        Assert.Equal(GameAssets.Bonus.Count, GameAssets.Bonus.Distinct().Count());
        Assert.All(GameAssets.Bonus, file => Assert.InRange(file, 0, 49));
    }
}
