using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PuzzleBoy.Rendering;
using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>
/// The game window: plays the original's stages in order with its tick rate, move timing, follow camera and board look
/// (ROADMAP.md, "M3 findings"). Arrow keys or WASD move, Q and E turn the view (L1/R1), V switches to overhead (○),
/// Tab scrolls (△), F opens the menu (□), Enter or Space confirms, Backspace cancels, Escape quits.
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

    // Every key the game reads as a press; Pressed refuses any other, so a test can see the whole set. The port's
    // switch (C) and stage skipping (PageUp/PageDown) belong to Community Edition, not to this port.
    internal static readonly Keys[] Presses = [Keys.Tab, Keys.V, Keys.Q, Keys.E, Keys.F, Keys.Enter, Keys.Space, Keys.Back];

    private readonly GameAssets _assets;
    private readonly ViewCamera _camera = new();
    private BoardView _view = null!;
    private Hud _hud = null!;
    private Sounds _sounds = null!;
    private KeyboardState _previousKeys;
    private Board _board = null!;
    private MoveAnimation? _animation;
    private Dialogue? _dialogue;
    private readonly Menu _menu = new();
    private readonly KeyRepeat _up = new();
    private readonly KeyRepeat _down = new();
    private bool _menuOpen;
    private bool _helpOpen;
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
    private int _barTicks;

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
        _hud = new Hud(GraphicsDevice, _assets.Ui, _assets.Text);
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
        _barTicks++;
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

        // The pad layer counts held keys every tick, whatever reads them, so the repeat runs on even when nothing listens.
        var up = _up.Fires(keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W));
        var down = _down.Fires(keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S));
        if (_dialogue is { } dialogue)
        {
            Talk(dialogue, keys, up ? -1 : down ? 1 : 0);
        }
        else if (_board.IsCleared)
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
        else if (_helpOpen)
        {
            // Confirm, cancel or □ closes the rules.
            if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space) || Pressed(keys, Keys.Back) || Pressed(keys, Keys.F))
            {
                _helpOpen = false;
                _sounds.Play(5);
            }
        }
        else if (_menuOpen)
        {
            Choose(keys, up ? -1 : down ? 1 : 0);
        }
        else if (_animation is null)
        {
            // The original reads no input at all while a move plays; a press made then is lost, not queued.
            Play(keys);
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
        // The controls bar hides while the Manikin Kid talks and shows the menu's or help's keys while they're up.
        var bar = _dialogue is not null ? BarSet.None : _helpOpen ? BarSet.Help : _menuOpen ? BarSet.Menu : _scroll is null ? BarSet.Play : BarSet.Scroll;
        _hud.Draw(new HudState(_current + 1, clear, _undoTicks > 0, ScrollArrows(), _scrollTicks, bar, _barTicks));
        if (_menuOpen)
        {
            _hud.DrawMenu(_menu);
        }

        if (_helpOpen)
        {
            _hud.DrawHelp();
        }

        if (_dialogue is not null)
        {
            _hud.DrawDialogue(_dialogue);
        }

        base.Draw(gameTime);
    }

    private void Load(int index)
    {
        // The camera's turn and angle carry over between stages, as in the original; scroll mode doesn't.
        _current = index;
        _board = new Board(_assets.Stages[index].Stage);
        _dialogue = StageDialogue(index + 1);
        _undoTicks = 0;

        // The bar turns off at a stage change and fades back in.
        _barTicks = 0;
        _models = [.. _board.Characters.Select(_ => new CharacterModel(_assets.Character))];
        _facings = new Direction?[_board.Characters.Count];
        _stageTicks = 0;
        _animation = null;
        _scroll = null;
        _menuOpen = false;
        _helpOpen = false;
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

        // □ opens the menu only in play: not in scroll mode, and not once a move or a turn has started this tick.
        if (Pressed(keys, Keys.F) && _animation is null && !_camera.IsTurning && _scroll is null)
        {
            _menuOpen = true;
            _menu.Reset();
            _sounds.Play(4);
        }
    }

    /// <summary>
    /// One tick of the Manikin Kid's window: confirm turns his pages, and a question's choice moves by
    /// <paramref name="move"/>. Answering the quit question with its first option ends the game.
    /// </summary>
    private void Talk(Dialogue dialogue, KeyboardState keys, int move)
    {
        dialogue.Tick();

        // INFERRED: the choice box plays the menu's sounds; the footage never shows it.
        if (dialogue.ShowsChoices && Pressed(keys, Keys.Back))
        {
            dialogue.Cancel();
            _sounds.Play(5);
        }
        else if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space))
        {
            if (dialogue.ShowsChoices)
            {
                _sounds.Play(4);
            }

            dialogue.Confirm();
        }
        else if (dialogue.ShowsChoices && move != 0)
        {
            dialogue.MoveChoice(move);
            _sounds.Play(6);
        }

        if (dialogue.Finished)
        {
            _dialogue = null;
            _barTicks = 0;

            // shortcut: the original fades the music out before Puzzle Boy ends; the port closes at once.
            if (dialogue.Choices is not null && dialogue.Choice == 0)
            {
                Exit();
            }
        }
    }

    /// <summary>One tick of the open □ menu (re/m3-ui.md, section 6); its cursor moves by <paramref name="move"/>.</summary>
    private void Choose(KeyboardState keys, int move)
    {
        // Cancel wins when it lands on the same tick as confirm.
        if (Pressed(keys, Keys.Back) || Pressed(keys, Keys.F))
        {
            _menuOpen = false;
            _sounds.Play(5);
        }
        else if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space))
        {
            Run(_menu.Cursor);
        }
        else if (move != 0)
        {
            _menu.Move(move);
            _sounds.Play(6);
        }
    }

    /// <summary>Runs a menu item, or buzzes when it can't run (Undo with no history).</summary>
    private void Run(MenuItem item)
    {
        if (item == MenuItem.Undo && !_board.Undo())
        {
            _sounds.Play(7);
            return;
        }

        _sounds.Play(4);
        _menuOpen = item == MenuItem.Undo;
        switch (item)
        {
            case MenuItem.Undo:
                // UNDO shows for 30 ticks from the tick the board reverts, and the menu stays open.
                _undoTicks = 30;
                break;
            case MenuItem.Restart:
                _board.Retry();

                // INFERRED: the original's restart reruns its stage start, so the Manikin Kid talks again.
                _dialogue = StageDialogue(_current + 1);
                _barTicks = 0;
                _facings = new Direction?[_board.Characters.Count];
                _stageTicks = 0;
                _lock = StartLock;
                break;
            case MenuItem.Help:
                _helpOpen = true;
                break;
            case MenuItem.Quit:
                var text = _assets.Text.Strings;
                _dialogue = new Dialogue([text["PBMESSAGE_L0007"]], text["PBMESSAGE_L0008"].Split('\n'));
                break;
        }
    }

    /// <summary>The Manikin Kid's pages for <paramref name="stage"/>, or null on a stage he doesn't talk over.</summary>
    private Dialogue? StageDialogue(int stage) =>
        Dialogue.Stages.TryGetValue(stage, out var pages) ? new Dialogue(pages.Select(id => _assets.Text.Strings[id])) : null;

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

    private bool Pressed(KeyboardState keys, Keys key) => Array.IndexOf(Presses, key) >= 0
        ? keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key)
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not one of the keys in Presses.");

    private void UpdateTitle()
    {
        var status = _board.IsCleared ? (_current + 1 < _assets.Stages.Count ? " - cleared" : " - all stages cleared") : "";
        Window.Title = $"Puzzle Boy - stage {_current + 1} of {_assets.Stages.Count} (file {_assets.Stages[_current].File:D2}){status}";
    }
}
