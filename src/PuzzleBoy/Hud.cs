using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PuzzleBoy;

/// <summary>The original's 1920x1080 UI layout scaled uniformly into the window and centred (re/m3-ui.md, section 1).</summary>
internal static class UiLayout
{
    /// <summary>The layout's scale and the offset that centres it in a window of <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
    public static (float Scale, Vector2 Offset) Fit(int width, int height)
    {
        var scale = Math.Min(width / 1920f, height / 1080f);
        return (scale, new Vector2((width - (1920 * scale)) / 2, (height - (1080 * scale)) / 2));
    }
}

/// <summary>What the HUD shows this tick.</summary>
/// <param name="Stage">The stage number, 1 to 20.</param>
/// <param name="Clear">True while COMPLETE! shows.</param>
/// <param name="Undo">True while UNDO shows.</param>
/// <param name="Arrows">The scroll arrows to show, one bit per screen direction: 1 up, 2 right, 4 down, 8 left.</param>
/// <param name="ScrollTicks">Ticks since scroll mode began, for the arrows' bob.</param>
internal readonly record struct HudState(int Stage, bool Clear, bool Undo, int Arrows, int ScrollTicks);

/// <summary>The UI sheets the HUD and menus draw from, read from the player's install.</summary>
/// <param name="Sheet"><c>pzlUI01</c> from <c>common</c>: plates, glows, arrows, the menu and help.</param>
/// <param name="Words"><c>pzlUI_font01_en</c> from <c>ui_tex_en</c>: STAGE, COMPLETE! and UNDO.</param>
/// <param name="Digits"><c>pzlUI_font01_ja</c> from <c>common</c>, whose stage digits the original draws.</param>
internal sealed record UiSheets(TextureData Sheet, TextureData Words, TextureData Digits);

/// <summary>
/// The stage counter, COMPLETE!, UNDO and the scroll arrows, at the original's layout (re/m3-ui.md, section 4).
/// Sprite rects are in sheet texels from the bottom-left, as Unity stores them; the sheets keep Unity's row order on the
/// GPU, so each sprite is drawn flipped.
/// </summary>
internal sealed class Hud : IDisposable
{
    // The scroll arrows: rest centre, sheet rect on pzlUI01, and the way each bobs toward the centre (layout y is down).
    private static readonly (Vector2 Centre, Rectangle Sprite, Vector2 Inward)[] ScrollArrows =
    [
        (new(960, 84), new(1152, 808, 72, 72), new(0, 1)),
        (new(1774, 518), new(1380, 808, 72, 72), new(-1, 0)),
        (new(960, 952), new(1228, 808, 72, 72), new(0, -1)),
        (new(146, 518), new(1304, 808, 72, 72), new(1, 0)),
    ];

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _sheet;
    private readonly Texture2D _words;
    private readonly Texture2D _digits;
    private (float Scale, Vector2 Offset) _fit;

    /// <summary>Uploads the sheets.</summary>
    public Hud(GraphicsDevice device, UiSheets sheets)
    {
        _device = device;
        _sprites = new SpriteBatch(device);
        _sheet = BoardView.Upload(device, sheets.Sheet);
        _words = BoardView.Upload(device, sheets.Words);
        _digits = BoardView.Upload(device, sheets.Digits);
    }

    /// <summary>The stage number as the counter's two digits: the tens are blank below 10.</summary>
    public static (int? Tens, int Ones) Digits(int stage) => (stage >= 10 ? stage / 10 : null, stage % 10);

    /// <summary>
    /// The scroll arrows' bob at <paramref name="seconds"/> into its half-second cycle: how far toward the centre, in
    /// layout units, and the glow's alpha. Both follow the original's three-key Hermite curves.
    /// </summary>
    public static (float Offset, float Glow) Bob(float seconds)
    {
        seconds %= 0.5f;
        return seconds < 0.25f
            ? (Hermite(0, 0, 24, 96, seconds / 0.25f, 0.25f), Hermite(0, 0, 0.5f, 2, seconds / 0.25f, 0.25f))
            : (Hermite(24, -96, 0, 0, (seconds - 0.25f) / 0.25f, 0.25f), Hermite(0.5f, -2, 0, 0, (seconds - 0.25f) / 0.25f, 0.25f));
    }

    /// <summary>Draws the HUD over the scene.</summary>
    public void Draw(HudState state)
    {
        _fit = UiLayout.Fit(_device.Viewport.Width, _device.Viewport.Height);

        // The stage counter: plate, glow, the word STAGE, then the digits.
        Begin(BlendState.NonPremultiplied);
        Sprite(_sheet, new(772, 1096, 372, 68), new(1494, 56, 372, 68));
        Swap(BlendState.Additive);
        Sprite(_sheet, new(632, 1096, 136, 124), new(1712, 30, 136, 124));
        Swap(BlendState.NonPremultiplied);
        Sprite(_words, new(1120, 144, 208, 44), new(1528, 68, 208, 44));
        var (tens, ones) = Digits(state.Stage);
        if (tens is { } t)
        {
            Sprite(_digits, Digit(t), new(1736, 60, 48, 60));
        }

        Sprite(_digits, Digit(ones), new(1774, 60, 48, 60));

        if (state.Undo)
        {
            Sprite(_words, new(1120, 68, 448, 72), new(736, 552, 448, 72));
        }

        // The arrows: a half-transparent base, then an animated additive glow.
        var (offset, glow) = Bob(state.ScrollTicks / 30f);
        for (var i = 0; i < ScrollArrows.Length; i++)
        {
            if ((state.Arrows & (1 << i)) != 0)
            {
                var (centre, sprite, inward) = ScrollArrows[i];
                Sprite(_sheet, sprite, Square(centre + (inward * offset)), 0.502f);
            }
        }

        Swap(BlendState.Additive);
        for (var i = 0; i < ScrollArrows.Length; i++)
        {
            if ((state.Arrows & (1 << i)) != 0)
            {
                var (centre, sprite, inward) = ScrollArrows[i];
                Sprite(_sheet, sprite, Square(centre + (inward * offset)), glow);
            }
        }

        if (state.Clear)
        {
            Swap(BlendState.NonPremultiplied);
            Sprite(_words, new(4, 4, 1112, 224), new(424, 444, 1112, 224));
            Swap(BlendState.Additive);
            Sprite(_words, new(4, 232, 1112, 224), new(424, 444, 1112, 224));
        }

        _sprites.End();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sprites.Dispose();
        _sheet.Dispose();
        _words.Dispose();
        _digits.Dispose();
    }

    /// <summary>A cubic Hermite segment from p0 (slope m0) to p1 (slope m1) over <paramref name="length"/> seconds, at fraction <paramref name="s"/>.</summary>
    private static float Hermite(float p0, float m0, float p1, float m1, float s, float length)
    {
        var (s2, s3) = (s * s, s * s * s);
        return (((2 * s3) - (3 * s2) + 1) * p0) + ((s3 - (2 * s2) + s) * length * m0) + (((-2 * s3) + (3 * s2)) * p1) + ((s3 - s2) * length * m1);
    }

    /// <summary>Digit d's 48x60 sprite on the digit sheet: 1 to 9 run from x 1120, and 0 sits at x 1552.</summary>
    private static Rectangle Digit(int d) => new(d == 0 ? 1552 : 1120 + (48 * (d - 1)), 4, 48, 60);

    private static RectangleF Square(Vector2 centre) => new(centre.X - 36, centre.Y - 36, 72, 72);

    private void Begin(BlendState blend) =>
        _sprites.Begin(SpriteSortMode.Deferred, blend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

    private void Swap(BlendState blend)
    {
        _sprites.End();
        Begin(blend);
    }

    /// <summary>Stretches a sheet rect (Unity's, from the bottom-left) over a layout rect, at <paramref name="alpha"/>.</summary>
    private void Sprite(Texture2D sheet, Rectangle unity, RectangleF layout, float alpha = 1)
    {
        var position = _fit.Offset + (new Vector2(layout.X, layout.Y) * _fit.Scale);
        var scale = new Vector2(layout.Width / unity.Width, layout.Height / unity.Height) * _fit.Scale;
        // Fading a straight-alpha sprite scales its alpha only; the additive glows take the same alpha as their strength.
        _sprites.Draw(sheet, position, unity, new Color(Color.White, alpha), 0, Vector2.Zero, scale, SpriteEffects.FlipVertically, 0);
    }

    /// <summary>A rectangle in layout units, which can be fractional.</summary>
    private readonly record struct RectangleF(float X, float Y, float Width, float Height);
}
