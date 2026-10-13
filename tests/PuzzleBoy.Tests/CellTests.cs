using PuzzleBoy.Rules;

namespace PuzzleBoy.Tests;

public sealed class CellTests
{
    [Theory]
    [InlineData(0x0000, CellKind.Floor)]
    [InlineData(0x2000, CellKind.Wall)]
    [InlineData(0x2800, CellKind.Wall)]
    [InlineData(0x0800, CellKind.Hole)]
    [InlineData(0x607D, CellKind.Block)]
    [InlineData(0x8001, CellKind.SwivelCenter)]
    [InlineData(0xA001, CellKind.SwivelArm)]
    [InlineData(0xA800, CellKind.SwivelArm)]
    public void KindOf_reads_the_high_bits(int cell, CellKind kind) =>
        Assert.Equal(kind, Cell.KindOf((ushort)cell));

    [Fact]
    public void GroupOf_reads_the_low_byte() =>
        Assert.Equal(0x7D, Cell.GroupOf(0x607D));
}
