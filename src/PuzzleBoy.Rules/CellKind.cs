namespace PuzzleBoy.Rules;

/// <summary>What occupies a cell of the stage grid.</summary>
public enum CellKind
{
    /// <summary>Open floor a character can walk on.</summary>
    Floor,

    /// <summary>A fixed wall. The grid outside the play area is all wall.</summary>
    Wall,

    /// <summary>A hole. Characters can't cross it until a block fills it.</summary>
    Hole,

    /// <summary>Part of a pushable block. Cells with the same group form one block.</summary>
    Block,

    /// <summary>The axis a swivel block turns around.</summary>
    SwivelCenter,

    /// <summary>An arm of a swivel block. It turns with the center of the same group.</summary>
    SwivelArm,
}

/// <summary>Reads the raw 16-bit cell values of a <see cref="Stage"/>.</summary>
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
