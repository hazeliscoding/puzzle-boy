namespace PuzzleBoy.Rules;

public enum CellKind
{
    Floor,
    Wall,
    Hole,
    Block,
    SwivelCenter,
    SwivelArm,
}

public static class Cell
{
    /// <summary>What a raw cell value holds. Swivel parts and blocks outrank the hole flag beneath them.</summary>
    /// <remarks>Inferred from the shipped stages' layouts; M0's decompile confirms or corrects it.</remarks>
    public static CellKind KindOf(ushort cell) => (cell & 0xF000) switch
    {
        0x8000 => CellKind.SwivelCenter,
        0xA000 => CellKind.SwivelArm,
        0x6000 => CellKind.Block,
        0x2000 => CellKind.Wall,
        _ when (cell & 0x0800) != 0 => CellKind.Hole,
        _ => CellKind.Floor,
    };

    /// <summary>The block or swivel group a cell belongs to.</summary>
    public static int GroupOf(ushort cell) => cell & 0xFF;
}
