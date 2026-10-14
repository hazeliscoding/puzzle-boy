using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>
/// The game window: plays the original's stages in order with its tick rate, move timing and follow camera
/// (ROADMAP.md, "M3 findings"). Plain boxes stand in for the original look until the rest of M3.
/// Arrow keys or WASD move, Q and E turn the view (L1/R1), V switches to overhead (○), Tab scrolls (△),
/// Z or Backspace undoes, R retries, Escape quits.
/// </summary>
public sealed class PuzzleBoyGame : Game
{
    // The input lock after a stage starts or restarts, and how long a clear holds before the next stage, in ticks.
    private const int StartLock = 2;
    private const int ClearTicks = 32;

    // The original orbits a point half a cell below the character's feet (its model sits 10 units down, the pivot 10 more).
    private const float PivotHeight = -0.5f;

    private static readonly Color[] GroupColors =
        [new(214, 92, 72), new(86, 156, 214), new(222, 186, 74), new(120, 186, 104), new(176, 112, 196), new(72, 184, 176)];

    private static readonly Color FloorColor = new(96, 96, 108);
    private static readonly Color WallColor = new(58, 60, 74);
    private static readonly Color SwivelColor = new(230, 230, 236);
    private static readonly Color JackColor = new(255, 140, 26);
    private static readonly Color WaitingColor = new(150, 96, 40);
    private static readonly Color GoalColor = new(60, 110, 255);

    // Held directions in the order the original reads them (up, down, left, right), as screen directions:
    // 0 up, 1 right, 2 down, 3 left. The camera turns them into grid directions. The original's PC keys are
    // WASD, Q/E for L1/R1, V for ○ and Tab for △ (inferred from its key tables); the arrows work too.
    internal static readonly (Keys Key, int Screen)[] Moves =
    [
        (Keys.Up, 0), (Keys.W, 0),
        (Keys.Down, 2), (Keys.S, 2),
        (Keys.Left, 3), (Keys.A, 3),
        (Keys.Right, 1), (Keys.D, 1),
    ];

    private readonly IReadOnlyList<(int File, Stage Stage)> _stages;
    private readonly ViewCamera _camera = new();
    private BasicEffect _effect = null!;
    private VertexBuffer _cube = null!;
    private KeyboardState _previousKeys;
    private Board _board = null!;
    private MoveAnimation? _animation;
    private (int X, int Y)? _scroll;
    private int _current;
    private int _lock;
    private int _clearedFor;
    private int _ticks;

    /// <summary>Opens a window on the first of <paramref name="stages"/>.</summary>
    /// <param name="stages">The stages to play in order, each with its file number. Must not be empty.</param>
    public PuzzleBoyGame(IReadOnlyList<(int File, Stage Stage)> stages)
    {
        _stages = stages;
        // The manager registers itself as a game service, which keeps it alive.
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720 };
        IsMouseVisible = true;

        // One Update is one of the original's ticks. It runs a tick per rendered frame, and its HD footage shows 30 a second.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30);
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

        _ticks++;
        _camera.Tick();
        _animation?.Tick();
        if (_animation is { FallEnds: true })
        {
            _camera.Quake();
        }

        if (_animation is { Done: true })
        {
            _animation = null;
        }

        if (_board.IsCleared)
        {
            if (++_clearedFor >= ClearTicks && _current + 1 < _stages.Count)
            {
                Load(_current + 1);
            }
        }
        else if (_lock > 0)
        {
            _lock--;
        }
        else
        {
            Play(keys);
        }

        // Developer shortcut until the menu exists: skip between stages.
        if (Pressed(keys, Keys.PageDown) || Pressed(keys, Keys.PageUp))
        {
            Load((_current + (Pressed(keys, Keys.PageDown) ? 1 : _stages.Count - 1)) % _stages.Count);
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

        var moving = Moving();
        _effect.View = _camera.View(_scroll is { } cursor ? At(cursor.X, cursor.Y, PivotHeight) : CharacterAt(ActiveIndex()) + (Vector3.Up * PivotHeight));
        _effect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60), GraphicsDevice.Viewport.AspectRatio, 0.05f, 30f);

        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                DrawCell(x, y, moving);
            }
        }

        DrawMovingParts();
        for (var i = 0; i < _board.Characters.Count; i++)
        {
            if (_board.Characters[i].Visible || _animation?.Move.Mover == i)
            {
                var color = i == ActiveIndex() ? JackColor : WaitingColor;
                DrawBox(CharacterAt(i) + (Vector3.Up * 0.4f), new Vector3(0.5f, 0.8f, 0.5f), color);
            }
        }

        // The original's goal spins 1° a tick.
        var spin = Matrix.CreateRotationY(MathHelper.ToRadians(_ticks));
        DrawBox(At(_board.Goal.X, _board.Goal.Y, 0.5f), new Vector3(0.45f), GoalColor, spin);

        base.Draw(gameTime);
    }

    private void Load(int index)
    {
        // The camera's turn and angle carry over between stages, as in the original; scroll mode doesn't.
        _current = index;
        _board = new Board(_stages[index].Stage);
        _animation = null;
        _scroll = null;
        _clearedFor = 0;
        _lock = StartLock;
    }

    /// <summary>One tick of input, read in the original's order: directions first, then the view buttons.</summary>
    private void Play(KeyboardState keys)
    {
        foreach (var (key, screen) in Moves)
        {
            if (!keys.IsKeyDown(key))
            {
                continue;
            }

            if (_scroll is { } cursor)
            {
                // In scroll mode a held direction moves the cursor one cell a tick, within the grid; Jack stays put.
                var (dx, dy) = Offset(_camera.DirectionFor(screen));
                _scroll = (Math.Clamp(cursor.X + dx, 0, Stage.Width - 1), Math.Clamp(cursor.Y + dy, 0, Stage.Height - 1));
            }
            else if (_animation is null)
            {
                var me = _board.Active;
                if (_board.Step(_camera.DirectionFor(screen)) != StepResult.Refused)
                {
                    _animation = new MoveAnimation(_board.LastMove!, (me.X, me.Y));
                }
            }

            break;
        }

        if (Pressed(keys, Keys.Q))
        {
            _camera.TurnLeft();
        }
        else if (Pressed(keys, Keys.E))
        {
            _camera.TurnRight();
        }

        if (Pressed(keys, Keys.V))
        {
            _camera.ToggleAngle();
        }

        if (Pressed(keys, Keys.Tab))
        {
            _scroll = _scroll is null ? (_board.Active.X, _board.Active.Y) : null;
        }

        // shortcut: undo and retry are keys until M3's menu, which the original reaches with □.
        if (_animation is null && (Pressed(keys, Keys.Z) || Pressed(keys, Keys.Back)))
        {
            _board.Undo();
        }

        if (_animation is null && Pressed(keys, Keys.R))
        {
            _board.Retry();
            _lock = StartLock;
        }
    }

    /// <summary>The cells of the group the current step moves, drawn on their own while it plays.</summary>
    private HashSet<(int X, int Y)> Moving() =>
        _animation is { } a && (a.Move.Result != StepResult.Walked) && (!a.Move.Fell || a.Falling) ? [.. a.Move.Landings] : [];

    private void DrawCell(int x, int y, HashSet<(int X, int Y)> moving)
    {
        var cell = _board[x, y];
        var kind = Cell.KindOf(cell);

        // A fallen block's holes stay open until it has sunk.
        var opening = moving.Contains((x, y)) && _animation!.Move.Fell;
        if (kind is not (CellKind.Wall or CellKind.Hole) && (cell & Cell.HoleFlag) == 0 && !opening)
        {
            DrawBox(At(x, y, -0.05f), new Vector3(0.96f, 0.1f, 0.96f), FloorColor);
        }

        if (moving.Contains((x, y)))
        {
            return;
        }

        if (kind == CellKind.Wall && BordersPlayArea(x, y))
        {
            DrawBox(At(x, y, 0.5f), Vector3.One, WallColor);
        }
        else if (kind is CellKind.Block or CellKind.SwivelCenter or CellKind.SwivelArm)
        {
            DrawPart(x, y, kind, Cell.GroupOf(cell), Matrix.Identity, 0);
        }
    }

    /// <summary>The pushed or turned group, slid or turned back by the part of the move still to come.</summary>
    private void DrawMovingParts()
    {
        if (_animation is not { } a || Moving().Count == 0)
        {
            return;
        }

        var move = a.Move;
        var left = 1 - a.Slide;
        foreach (var (x, y) in move.Landings)
        {
            if (move.Result == StepResult.Pushed)
            {
                var (dx, dy) = Offset(move.Direction);
                DrawPart(x - (dx * left), y - (dy * left), CellKind.Block, move.Group, Matrix.Identity, a.Sink);
                continue;
            }

            // Turn the landing back about the center, counterclockwise for a clockwise turn (x right, y up).
            var angle = -move.Turn * MathHelper.PiOver2 * left;
            var (ox, oy) = (x - move.Center.X, y - move.Center.Y);
            var (rx, ry) = ((ox * MathF.Cos(angle)) - (oy * MathF.Sin(angle)), (ox * MathF.Sin(angle)) + (oy * MathF.Cos(angle)));
            DrawPart(move.Center.X + rx, move.Center.Y + ry, Cell.KindOf(_board[x, y]), move.Group, Matrix.CreateRotationY(angle), 0);
        }
    }

    private void DrawPart(float x, float y, CellKind kind, int group, Matrix rotation, float sink)
    {
        var color = GroupColors[group % GroupColors.Length];
        switch (kind)
        {
            case CellKind.Block:
                DrawBox(At(x, y, 0.45f - sink), new Vector3(0.94f, 0.9f, 0.94f), color, rotation);
                break;
            case CellKind.SwivelCenter:
                DrawBox(At(x, y, 0.6f), new Vector3(0.4f, 1.2f, 0.4f), SwivelColor, rotation);
                break;
            case CellKind.SwivelArm:
                DrawBox(At(x, y, 0.4f), new Vector3(0.8f, 0.8f, 0.8f), color, rotation);
                break;
            default:
                break;
        }
    }

    private int ActiveIndex()
    {
        var slot = _board.Active.Slot;
        for (var i = 0; ; i++)
        {
            if (_board.Characters[i].Slot == slot)
            {
                return i;
            }
        }
    }

    /// <summary>Where character <paramref name="index"/> is drawn: sliding from its start while its move plays.</summary>
    private Vector3 CharacterAt(int index)
    {
        var c = _board.Characters[index];
        if (_animation is not { } a || a.Move.Mover != index)
        {
            return At(c.X, c.Y, 0);
        }

        return At(MathHelper.Lerp(a.From.X, c.X, a.Slide), MathHelper.Lerp(a.From.Y, c.Y, a.Slide), 0);
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

    /// <summary>
    /// The world position of grid cell (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="height"/>.
    /// Grid +y runs toward -z, away from the default camera, so it points up the screen as in the original.
    /// </summary>
    private static Vector3 At(float x, float y, float height) => new(x, height, -y);

    private static (int X, int Y) Offset(Direction direction) => direction switch
    {
        Direction.PlusY => (0, 1),
        Direction.PlusX => (1, 0),
        Direction.MinusY => (0, -1),
        _ => (-1, 0),
    };

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
