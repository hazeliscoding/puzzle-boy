using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>
/// The game window: plays the original's stages in order with plain boxes standing in for the original look,
/// which arrives in M3. Arrow keys or WASD move, Z or Backspace undoes, R retries, Escape quits.
/// </summary>
public sealed class PuzzleBoyGame : Game
{
    private const double ClearPause = 0.6;

    private static readonly Color[] GroupColors =
        [new(214, 92, 72), new(86, 156, 214), new(222, 186, 74), new(120, 186, 104), new(176, 112, 196), new(72, 184, 176)];

    private static readonly Color FloorColor = new(96, 96, 108);
    private static readonly Color WallColor = new(58, 60, 74);
    private static readonly Color SwivelColor = new(230, 230, 236);
    private static readonly Color JackColor = new(255, 140, 26);
    private static readonly Color WaitingColor = new(150, 96, 40);
    private static readonly Color GoalColor = new(60, 110, 255);

    // The camera looks from +z toward -z, so +y on the grid is toward the bottom of the screen.
    private static readonly (Keys Key, Direction Direction)[] Moves =
    [
        (Keys.Down, Direction.PlusY), (Keys.S, Direction.PlusY),
        (Keys.Right, Direction.PlusX), (Keys.D, Direction.PlusX),
        (Keys.Up, Direction.MinusY), (Keys.W, Direction.MinusY),
        (Keys.Left, Direction.MinusX), (Keys.A, Direction.MinusX),
    ];

    private readonly IReadOnlyList<(int File, Stage Stage)> _stages;
    private BasicEffect _effect = null!;
    private VertexBuffer _cube = null!;
    private KeyboardState _previousKeys;
    private Board _board = null!;
    private int _current;
    private double _clearedFor;

    /// <summary>Opens a window on the first of <paramref name="stages"/>.</summary>
    /// <param name="stages">The stages to play in order, each with its file number. Must not be empty.</param>
    public PuzzleBoyGame(IReadOnlyList<(int File, Stage Stage)> stages)
    {
        _stages = stages;
        // The manager registers itself as a game service, which keeps it alive.
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720 };
        IsMouseVisible = true;
        Load(0);
    }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _effect = new BasicEffect(GraphicsDevice);
        _effect.EnableDefaultLighting();
        _cube = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), 36, BufferUsage.WriteOnly);
        _cube.SetData(CubeVertices());
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        if (keys.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        // shortcut: one step per key press; the original repeats while a direction is held (an M2 in-game check).
        foreach (var (key, direction) in Moves)
        {
            if (Pressed(keys, key))
            {
                _board.Step(direction);
                break;
            }
        }

        if (Pressed(keys, Keys.Z) || Pressed(keys, Keys.Back))
        {
            _board.Undo();
        }

        if (Pressed(keys, Keys.R))
        {
            _board.Retry();
        }

        // Developer shortcut until the menu exists: skip between stages.
        if (Pressed(keys, Keys.PageDown) || Pressed(keys, Keys.PageUp))
        {
            Load((_current + (Pressed(keys, Keys.PageDown) ? 1 : _stages.Count - 1)) % _stages.Count);
        }

        _clearedFor = _board.IsCleared ? _clearedFor + gameTime.ElapsedGameTime.TotalSeconds : 0;
        if (_clearedFor > ClearPause && _current + 1 < _stages.Count)
        {
            Load(_current + 1);
        }

        UpdateTitle();
        _previousKeys = keys;
        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.SetVertexBuffer(_cube);

        AimCamera();
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                DrawCell(x, y);
            }
        }

        foreach (var character in _board.Characters.Where(c => c.Visible))
        {
            var color = character == _board.Active ? JackColor : WaitingColor;
            DrawBox(new Vector3(character.X, 0.4f, character.Y), new Vector3(0.5f, 0.8f, 0.5f), color);
        }

        var spin = Matrix.CreateRotationY((float)gameTime.TotalGameTime.TotalSeconds * 2f);
        DrawBox(new Vector3(_board.Goal.X, 0.5f, _board.Goal.Y), new Vector3(0.45f), GoalColor, spin);

        base.Draw(gameTime);
    }

    private void Load(int index)
    {
        _current = index;
        _board = new Board(_stages[index].Stage);
        _clearedFor = 0;
    }

    private void DrawCell(int x, int y)
    {
        var cell = _board[x, y];
        var kind = Cell.KindOf(cell);
        var group = GroupColors[Cell.GroupOf(cell) % GroupColors.Length];
        if (kind is not (CellKind.Wall or CellKind.Hole) && (cell & Cell.HoleFlag) == 0)
        {
            DrawBox(new Vector3(x, -0.05f, y), new Vector3(0.96f, 0.1f, 0.96f), FloorColor);
        }

        switch (kind)
        {
            case CellKind.Wall when BordersPlayArea(x, y):
                DrawBox(new Vector3(x, 0.5f, y), Vector3.One, WallColor);
                break;
            case CellKind.Block:
                DrawBox(new Vector3(x, 0.45f, y), new Vector3(0.94f, 0.9f, 0.94f), group);
                break;
            case CellKind.SwivelCenter:
                DrawBox(new Vector3(x, 0.6f, y), new Vector3(0.4f, 1.2f, 0.4f), SwivelColor);
                break;
            case CellKind.SwivelArm:
                DrawBox(new Vector3(x, 0.4f, y), new Vector3(0.8f, 0.8f, 0.8f), group);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Whether a wall touches the play area, counting diagonals. The rest of the 20x20 grid is wall too, and drawing it would bury the stage.
    /// </summary>
    private bool BordersPlayArea(int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var (nx, ny) = (x + dx, y + dy);
                if (nx is >= 0 and < Stage.Width && ny is >= 0 and < Stage.Height && Cell.KindOf(_board[nx, ny]) != CellKind.Wall)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Looks down at the play area from the front, far enough back to fit its larger side.</summary>
    private void AimCamera()
    {
        var open = Enumerable.Range(0, Stage.Width * Stage.Height)
            .Select(i => (X: i % Stage.Width, Y: i / Stage.Width))
            .Where(p => Cell.KindOf(_board[p.X, p.Y]) != CellKind.Wall)
            .ToList();
        var (minX, maxX, minY, maxY) = (open.Min(p => p.X), open.Max(p => p.X), open.Min(p => p.Y), open.Max(p => p.Y));
        var target = new Vector3((minX + maxX) / 2f, 0, (minY + maxY) / 2f);
        var distance = (Math.Max(maxX - minX, maxY - minY) * 1.1f) + 6f;

        _effect.View = Matrix.CreateLookAt(target + new Vector3(0, distance * 0.8f, distance * 0.65f), target, Vector3.Up);
        _effect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f, 200f);
    }

    /// <summary>Draws the unit cube scaled, optionally rotated about its center, then moved to <paramref name="center"/>.</summary>
    private void DrawBox(Vector3 center, Vector3 size, Color color, Matrix? rotation = null)
    {
        _effect.World = Matrix.CreateScale(size) * (rotation ?? Matrix.Identity) * Matrix.CreateTranslation(center);
        _effect.DiffuseColor = color.ToVector3();
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);
        }
    }

    /// <summary>
    /// A unit cube centered on the origin, as 12 triangles with face normals for lighting.
    /// Faces aren't consistently wound, so drawing uses <see cref="RasterizerState.CullNone"/>.
    /// </summary>
    private static VertexPositionNormalTexture[] CubeVertices()
    {
        var vertices = new List<VertexPositionNormalTexture>();
        foreach (var normal in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
        {
            var side = new Vector3(normal.Y, normal.Z, normal.X);
            var up = Vector3.Cross(normal, side);
            Vector3[] corners = [normal - side - up, normal - side + up, normal + side + up, normal + side - up];
            foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
            {
                vertices.Add(new VertexPositionNormalTexture(corners[i] * 0.5f, normal, Vector2.Zero));
            }
        }

        return [.. vertices];
    }

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

    private void UpdateTitle()
    {
        var status = _board.IsCleared ? (_current + 1 < _stages.Count ? " - cleared" : " - all stages cleared") : "";
        Window.Title = $"Puzzle Boy - stage {_current + 1} of {_stages.Count} (file {_stages[_current].File:D2}){status}";
    }
}
