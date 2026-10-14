using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>
/// The game window: plays the original's stages in order with its tick rate, move timing, follow camera and board look
/// (ROADMAP.md, "M3 findings"). Arrow keys or WASD move, Q and E turn the view (L1/R1), V switches to overhead (○),
/// Tab scrolls (△), Z or Backspace undoes, R retries, Escape quits.
/// </summary>
public sealed class PuzzleBoyGame : Game
{
    // The input lock after a stage starts or restarts, and how long a clear holds before the next stage, in ticks.
    private const int StartLock = 2;
    private const int ClearTicks = 32;

    // The original orbits a point half a cell below the character's feet (its model sits 10 units down, the pivot 10 more).
    private const float PivotHeight = -0.5f;

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
    private readonly TextureData _atlas;
    private readonly TextureData _background;
    private readonly ViewCamera _camera = new();
    private BoardView _view = null!;
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
    /// <param name="atlas">The board atlas, <c>sample1</c> from the <c>pazzleboy</c> bundle.</param>
    /// <param name="background">The backdrop, <c>BG_pzl</c> from the <c>common</c> bundle.</param>
    public PuzzleBoyGame(IReadOnlyList<(int File, Stage Stage)> stages, TextureData atlas, TextureData background)
    {
        _stages = stages;
        _atlas = atlas;
        _background = background;

        // The manager registers itself as a game service, which keeps it alive.
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720 };
        IsMouseVisible = true;

        // One Update is one of the original's ticks. It runs a tick per rendered frame, and its HD footage shows 30 a second.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30);
        Load(0);
    }

    /// <inheritdoc />
    protected override void LoadContent() => _view = new BoardView(GraphicsDevice, _atlas, _background);

    /// <inheritdoc />
    protected override void UnloadContent() => _view.Dispose();

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
        var (px, py) = _scroll is { } cursor ? cursor : MoveAnimation.Where(_board, _animation, _board.ActiveIndex);
        var view = _camera.View(new Vector3(px, PivotHeight, -py));
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60), GraphicsDevice.Viewport.AspectRatio, 0.05f, 30f);
        _view.Draw(_board, _animation, view, projection, _ticks);
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

    private static (int X, int Y) Offset(Direction direction) => direction switch
    {
        Direction.PlusY => (0, 1),
        Direction.PlusX => (1, 0),
        Direction.MinusY => (0, -1),
        _ => (-1, 0),
    };

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

    private void UpdateTitle()
    {
        var status = _board.IsCleared ? (_current + 1 < _stages.Count ? " - cleared" : " - all stages cleared") : "";
        Window.Title = $"Puzzle Boy - stage {_current + 1} of {_stages.Count} (file {_stages[_current].File:D2}){status}";
    }
}
