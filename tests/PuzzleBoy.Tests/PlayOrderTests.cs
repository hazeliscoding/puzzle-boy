using PuzzleBoy.Rules;

namespace PuzzleBoy.Tests;

public sealed class PlayOrderTests
{
    [Fact]
    public void Bonus_stages_are_unused_files_each_played_once()
    {
        Assert.Empty(PlayOrder.Bonus.Intersect(PlayOrder.Files));
        Assert.Equal(PlayOrder.Bonus.Count, PlayOrder.Bonus.Distinct().Count());
        Assert.All(PlayOrder.Bonus, file => Assert.InRange(file, 0, 49));
    }
}
