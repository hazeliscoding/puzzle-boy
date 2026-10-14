using Microsoft.Xna.Framework.Input;
using PuzzleBoy.Rules;

namespace PuzzleBoy.Tests;

public sealed class ControlsTests
{
    // The original's default camera shows +x to the right and +y up the screen. The port once drew +y down,
    // which mirrored every stage, so these pin the keys to the original's view.
    [Theory]
    [InlineData(Keys.Up, Direction.PlusY)]
    [InlineData(Keys.W, Direction.PlusY)]
    [InlineData(Keys.Down, Direction.MinusY)]
    [InlineData(Keys.S, Direction.MinusY)]
    [InlineData(Keys.Right, Direction.PlusX)]
    [InlineData(Keys.D, Direction.PlusX)]
    [InlineData(Keys.Left, Direction.MinusX)]
    [InlineData(Keys.A, Direction.MinusX)]
    public void Each_key_moves_the_way_the_original_default_camera_shows(Keys key, Direction expected)
    {
        Assert.Equal(expected, Assert.Single(PuzzleBoyGame.Moves, m => m.Key == key).Direction);
    }
}
