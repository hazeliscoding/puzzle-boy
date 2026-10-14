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
    // The input lock after a stage starts or restarts, in ticks.
    private const int StartLock = 2;

    // Ticks from the input that reaches the goal to the next stage: the move ends 3 ticks later, the clear starts
    // the tick after that and runs 31 times.
    private const int ClearTicks = 35;

    // The original orbits its model object, which sits 10 units (a quarter cell) below the character's position.
    private const float PivotHeight = -0.25f;

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

    private readonly GameAssets _assets;
    private readonly ViewCamera _camera = new();
    private BoardView _view = null!;
    private Hud _hud = null!;
    private Sounds _sounds = null!;
    private KeyboardState _previousKeys;
    private Board _board = null!;
    private MoveAnimation? _animation;
    private CharacterModel[] _models = [];
    private Direction?[] _facings = [];
    private (int X, int Y)? _scroll;
    private int _current;
    private int _lock;
    private int _clearedFor;
    private int _ticks;
    private int _stageTicks;
    private int _undoTicks;
    private int _scrollTicks;

    /// <summary>Opens a window on the first stage of <paramref name="assets"/>.</summary>
    /// <param name="assets">Everything read from the player's install. Its stage list must not be empty.</param>
    internal PuzzleBoyGame(GameAssets assets)
    {
        _assets = assets;

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
        _view = new BoardView(GraphicsDevice, _assets.Atlas, _assets.Background, _assets.Character);
        _hud = new Hud(GraphicsDevice, _assets.Ui);
        _sounds = new Sounds(_assets.Sounds);
        _sounds.StartMusic();
    }

    /// <inheritdoc />
    protected override void UnloadContent()
    {
        _view.Dispose();
        _hud.Dispose();
        _sounds.Dispose();
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
        _stageTicks++;
        _scrollTicks++;
        _undoTicks = Math.Max(0, _undoTicks - 1);
        _camera.Tick();
        _animation?.Tick();
        if (_animation is { FallStarts: true })
        {
            _sounds.Play(3);
        }

        if (_animation is { FallEnds: true })
        {
            _camera.Quake();
        }

        if (_animation?.ArrivesAt(_board.Goal) == true)
        {
            _sounds.Play(9);
        }

        if (_animation is { Done: true })
        {
            _animation = null;
        }

        if (_board.IsCleared)
        {
            if (++_clearedFor >= ClearTicks && _current + 1 < _assets.Stages.Count)
            {
                Load(_current + 1);
            }
        }
        else if (_lock > 0)
        {
            _lock--;
        }
        else if (_animation is null)
        {
            // The original reads no input at all while a move plays; a press made then is lost, not queued.
            Play(keys);
        }

        // Developer shortcut until the menu exists: skip between stages.
        if (Pressed(keys, Keys.PageDown) || Pressed(keys, Keys.PageUp))
        {
            Load((_current + (Pressed(keys, Keys.PageDown) ? 1 : _assets.Stages.Count - 1)) % _assets.Stages.Count);
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
        _view.Draw(_board, _animation, view, projection, _ticks, PosedModels(), _camera.Yaw);

        // COMPLETE! shows from the clear's first tick, the one after the last move ends, for 30 ticks.
        var clear = _board.IsCleared && _clearedFor is >= 4 and < 34;
        _hud.Draw(new HudState(_current + 1, clear, _undoTicks > 0, ScrollArrows(), _scrollTicks));
        base.Draw(gameTime);
    }

    private void Load(int index)
    {
        // The camera's turn and angle carry over between stages, as in the original; scroll mode doesn't.
        _current = index;
        _board = new Board(_assets.Stages[index].Stage);
        _undoTicks = 0;
        _models = [.. _board.Characters.Select(_ => new CharacterModel(_assets.Character))];
        _facings = new Direction?[_board.Characters.Count];
        _stageTicks = 0;
        _animation = null;
        _scroll = null;
        _clearedFor = 0;
        _lock = StartLock;
    }

    /// <summary>One tick of input, read in the original's order: △, ○, the directions, then L1/R1.</summary>
    private void Play(KeyboardState keys)
    {
        // shortcut: scroll mode draws no direction arrows yet; they're pzlUI sprites and come with the HUD.
        if (Pressed(keys, Keys.Tab))
        {
            _scroll = _scroll is null ? (_board.Active.X, _board.Active.Y) : null;
            _scrollTicks = 0;
            _sounds.Play(8);
        }

        if (Pressed(keys, Keys.V) && _camera.ToggleAngle())
        {
            _sounds.Play(8);
        }

        foreach (var (key, screen) in Moves)
        {
            if (!keys.IsKeyDown(key))
            {
                continue;
            }

            if (_scroll is { } cursor)
            {
                // In scroll mode a held direction moves the cursor one cell a tick, within the grid; Jack stays put.
                var (dx, dy) = _camera.DirectionFor(screen).Offset();
                _scroll = (Math.Clamp(cursor.X + dx, 0, Stage.Width - 1), Math.Clamp(cursor.Y + dy, 0, Stage.Height - 1));
            }
            else
            {
                // Jack turns to the input even when the step is refused. A bump is silent; a step, push or turn
                // plays SE00, SE01 or SE02.
                var me = _board.Active;
                var direction = _camera.DirectionFor(screen);
                _facings[_board.ActiveIndex] = direction;
                var result = _board.Step(direction);
                if (result != StepResult.Refused)
                {
                    _animation = new MoveAnimation(_board.LastMove!, (me.X, me.Y));
                    _sounds.Play(result switch { StepResult.Walked => 0, StepResult.Pushed => 1, _ => 2 });
                }
            }

            break;
        }

        if ((Pressed(keys, Keys.Q) && _camera.TurnLeft()) || (Pressed(keys, Keys.E) && _camera.TurnRight()))
        {
            _sounds.Play(8);
        }

        // shortcut: undo and retry are keys until M3's menu, which the original opens with □. Like the menu, they
        // wait for a move or a turn to end and don't work in scroll mode.
        if (_animation is not null || _camera.IsTurning || _scroll is not null)
        {
            return;
        }

        if ((Pressed(keys, Keys.Z) || Pressed(keys, Keys.Back)) && _board.Undo())
        {
            // UNDO shows for 30 ticks from the tick the board reverts.
            _undoTicks = 30;
        }
        else if (Pressed(keys, Keys.R))
        {
            _board.Retry();
            _facings = new Direction?[_board.Characters.Count];
            _stageTicks = 0;
            _lock = StartLock;
        }
    }

    /// <summary>The scroll arrows to show: one per screen direction the cursor can still move, as a bit mask.</summary>
    private int ScrollArrows()
    {
        if (_scroll is not { } cursor)
        {
            return 0;
        }

        var mask = 0;
        for (var screen = 0; screen < 4; screen++)
        {
            var (dx, dy) = _camera.DirectionFor(screen).Offset();
            if (cursor.X + dx is >= 0 and < Stage.Width && cursor.Y + dy is >= 0 and < Stage.Height)
            {
                mask |= 1 << screen;
            }
        }

        return mask;
    }

    /// <summary>
    /// The characters on the board, posed for this tick. Before a stage's first input the original leaves Jack at its
    /// starting rotation, facing grid -y toward the default camera; the idle restarts with the stage.
    /// </summary>
    private List<CharacterModel> PosedModels()
    {
        var posed = new List<CharacterModel>();
        for (var i = 0; i < _models.Length; i++)
        {
            if (_board.Characters[i].Visible || _animation?.Move.Mover == i)
            {
                var (x, y) = MoveAnimation.Where(_board, _animation, i);
                _models[i].Pose(_stageTicks, x, y, _facings[i] is { } facing ? 90 * (int)facing : 180);
                posed.Add(_models[i]);
            }
        }

        return posed;
    }

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

    private void UpdateTitle()
    {
        var status = _board.IsCleared ? (_current + 1 < _assets.Stages.Count ? " - cleared" : " - all stages cleared") : "";
        Window.Title = $"Puzzle Boy - stage {_current + 1} of {_assets.Stages.Count} (file {_assets.Stages[_current].File:D2}){status}";
    }
}
