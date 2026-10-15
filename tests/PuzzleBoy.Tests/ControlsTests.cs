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
        Assert.Equal(expected, new ViewCamera().DirectionFor(ScreenOf(key)));
    }

    // The original's key table after one R1 and after one L1 (ROADMAP.md, "M3 findings": camera and controls).
    [Theory]
    [InlineData(Keys.Up, Direction.MinusX, Direction.PlusX)]
    [InlineData(Keys.Right, Direction.PlusY, Direction.MinusY)]
    [InlineData(Keys.Down, Direction.PlusX, Direction.MinusX)]
    [InlineData(Keys.Left, Direction.MinusY, Direction.PlusY)]
    public void Turning_the_view_turns_the_keys_with_it(Keys key, Direction afterR1, Direction afterL1)
    {
        var right = new ViewCamera();
        right.TurnRight();
        var left = new ViewCamera();
        left.TurnLeft();

        Assert.Equal(afterR1, right.DirectionFor(ScreenOf(key)));
        Assert.Equal(afterL1, left.DirectionFor(ScreenOf(key)));
    }

    [Fact]
    public void Directions_are_read_up_down_left_right_like_the_original()
    {
        Assert.Equal([0, 0, 2, 2, 3, 3, 1, 1], PuzzleBoyGame.Moves.Select(m => m.Screen));
    }

    [Fact]
    public void The_game_reads_only_the_original_keys_and_the_menu_keys_as_presses()
    {
        Assert.Equal([Keys.Tab, Keys.V, Keys.Q, Keys.E, Keys.F, Keys.Enter, Keys.Space, Keys.Back], PuzzleBoyGame.Presses);
    }

    // The switch and stage skipping are Community Edition's; the original has neither.
    [Theory]
    [InlineData(Keys.C)]
    [InlineData(Keys.PageUp)]
    [InlineData(Keys.PageDown)]
    public void The_preservation_game_has_no_switch_or_stage_skip_key(Keys key)
    {
        Assert.DoesNotContain(key, PuzzleBoyGame.Presses);
        Assert.DoesNotContain(key, PuzzleBoyGame.Moves.Select(m => m.Key));
    }

    private static int ScreenOf(Keys key) => Assert.Single(PuzzleBoyGame.Moves, m => m.Key == key).Screen;
}
