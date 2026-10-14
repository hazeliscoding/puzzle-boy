using Microsoft.Xna.Framework;
using PuzzleBoy.Rules;

namespace PuzzleBoy;

/// <summary>
/// The original's follow camera (ROADMAP.md, "M3 findings": camera and controls), advanced one tick at a time.
/// Distances are in cells (the original's 40 units are 1 here), and grid +y runs toward -z, away from the default view.
/// </summary>
internal sealed class ViewCamera
{
    private const float TurnStep = 10f;
    private const int TurnTicks = 9;
    private const int AngleTicks = 3;

    // The original's offsets from the character: (0, 400, -240) in the normal view and (0, 450, 0) overhead.
    private const float NormalBack = 6f;
    private const float NormalHeight = 10f;
    private const float OverheadHeight = 11.25f;

    private float _yaw;
    private int _turnTicksLeft;
    private float _turnStep;
    private bool _overhead;
    private int _angle = AngleTicks;
    private int _quakeTicks;
    private float _quakeX;
    private float _quakeY;

    /// <summary>R1 presses minus L1 presses, mod 4. It turns the keys and the view together.</summary>
    public int Mode { get; private set; }

    /// <summary>True while a turn or an angle change runs; the original ignores L1, R1 and ○ until it ends.</summary>
    public bool IsBusy => _turnTicksLeft > 0 || _angle is > 0 and < AngleTicks;

    /// <summary>
    /// The grid direction for a screen direction: 0 up, 1 right, 2 down, 3 left. Up is always away from the camera,
    /// because the original's key table turns with the view: direction = (key - mode) mod 4.
    /// </summary>
    public Direction DirectionFor(int screen) => (Direction)((screen - Mode + 4) % 4);

    /// <summary>The view's yaw in degrees: 0 looks toward grid +y, and R1 adds 90 over a turn.</summary>
    public float Yaw => _yaw;

    /// <summary>L1: turns the view a quarter turn, orbiting the camera 10° a tick for 9 ticks. False while busy.</summary>
    public bool TurnLeft() => Turn(-1);

    /// <summary>R1: the opposite of <see cref="TurnLeft"/>.</summary>
    public bool TurnRight() => Turn(1);

    /// <summary>○: switches between the normal and overhead views over 3 ticks. False while busy.</summary>
    public bool ToggleAngle()
    {
        if (IsBusy)
        {
            return false;
        }

        // The first of the 3 steps shows on the press tick.
        _overhead = !_overhead;
        _angle = 1;
        return true;
    }

    /// <summary>Starts the 30-tick shake a block's fall ends with.</summary>
    public void Quake()
    {
        // The original starts from (320, 800) x 0.01 units, here in cells.
        _quakeTicks = 30;
        _quakeX = 3.2f / 40f;
        _quakeY = 8f / 40f;
    }

    /// <summary>Advances the turn, angle change and shake by one tick.</summary>
    public void Tick()
    {
        if (_turnTicksLeft > 0)
        {
            _yaw += _turnStep;
            _turnTicksLeft--;
        }

        if (_angle < AngleTicks)
        {
            _angle++;
        }

        if (_quakeTicks > 0)
        {
            _quakeX -= _quakeX / (_quakeTicks + 5);
            _quakeY -= _quakeY / (_quakeTicks + 5);
            _quakeTicks--;
        }
    }

    /// <summary>The view matrix for a camera following <paramref name="pivot"/>, the point the original orbits.</summary>
    public Matrix View(Vector3 pivot)
    {
        // How far the view has gone toward overhead: pitch steps 60, 70, 80, 90 and the position moves in thirds.
        var toward = _angle / (float)AngleTicks;
        var overhead = _overhead ? toward : 1 - toward;
        var pitch = MathHelper.ToRadians(60 + (30 * overhead));
        var yaw = MathHelper.ToRadians(_yaw);
        var away = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
        var normal = (away * NormalBack) + (Vector3.Up * NormalHeight);
        var position = pivot + Vector3.Lerp(normal, Vector3.Up * OverheadHeight, overhead);
        var forward = (-away * MathF.Cos(pitch)) - (Vector3.Up * MathF.Sin(pitch));
        var up = (-away * MathF.Sin(pitch)) + (Vector3.Up * MathF.Cos(pitch));

        // INFERRED shape: the original's shake is a random walk whose float arguments the decompiler lost; an
        // alternating shake of the same size and decay stands in.
        if (_quakeTicks > 0)
        {
            var sign = _quakeTicks % 2 == 0 ? 1 : -1;
            position += (Vector3.Cross(forward, up) * _quakeX * sign) + (Vector3.Up * _quakeY * sign);
        }

        return Matrix.CreateLookAt(position, position + forward, up);
    }

    private bool Turn(int sense)
    {
        if (IsBusy)
        {
            return false;
        }

        // R1 orbits the camera counterclockwise seen from above (from the south side to the east), L1 the other way.
        // The first of the 9 steps shows on the press tick.
        Mode = (Mode + sense + 4) % 4;
        _turnStep = sense * TurnStep;
        _yaw += _turnStep;
        _turnTicksLeft = TurnTicks - 1;
        return true;
    }
}
