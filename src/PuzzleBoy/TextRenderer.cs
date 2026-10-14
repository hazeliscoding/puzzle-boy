using Microsoft.Xna.Framework;

namespace PuzzleBoy;

/// <summary>A glyph of the TextMeshPro font, in font units at the font's point size (1 unit = 1 atlas texel there).</summary>
/// <param name="Width">The metric box's width.</param>
/// <param name="Height">The metric box's height.</param>
/// <param name="BearingX">From the pen to the box's left edge.</param>
/// <param name="BearingY">From the baseline up to the box's top.</param>
/// <param name="Advance">How far the pen moves.</param>
/// <param name="Rect">The glyph's rect in the atlas, in texels from the bottom-left as Unity stores it.</param>
internal readonly record struct FontGlyph(float Width, float Height, float BearingX, float BearingY, float Advance, Rectangle Rect);

/// <summary>The game's TextMeshPro SDF font: glyphs and the distance-field atlas, one byte per texel, rows bottom-up.</summary>
/// <param name="PointSize">The size the glyph metrics are given at.</param>
/// <param name="Ascent">The ascent line, in font units.</param>
/// <param name="Descent">The descent line, in font units (negative).</param>
/// <param name="Glyphs">Glyphs by character.</param>
/// <param name="AtlasWidth">The atlas's width in texels.</param>
/// <param name="AtlasHeight">The atlas's height in texels.</param>
/// <param name="Atlas">The atlas: 0.5 + distance / 22 per texel as a byte, positive inside.</param>
internal sealed record FontData(float PointSize, float Ascent, float Descent, IReadOnlyDictionary<char, FontGlyph> Glyphs, int AtlasWidth, int AtlasHeight, byte[] Atlas);

/// <summary>A TextMeshPro distance-field material: colours and the floats that place its edges.</summary>
/// <param name="Face">The face colour, straight alpha.</param>
/// <param name="Outline">The outline colour.</param>
/// <param name="Underlay">The underlay colour; its alpha is the underlay's strength.</param>
/// <param name="FaceDilate">TMP's <c>_FaceDilate</c>.</param>
/// <param name="OutlineWidth">TMP's <c>_OutlineWidth</c>.</param>
/// <param name="UnderlayDilate">TMP's <c>_UnderlayDilate</c>.</param>
/// <param name="ScaleRatioA">TMP's <c>_ScaleRatioA</c>.</param>
/// <param name="ScaleRatioC">TMP's <c>_ScaleRatioC</c>.</param>
/// <param name="GradientScale">TMP's <c>_GradientScale</c>: the atlas's distance range in texels.</param>
internal sealed record TextMaterial(
    Vector4 Face, Vector4 Outline, Vector4 Underlay, float FaceDilate, float OutlineWidth, float UnderlayDilate,
    float ScaleRatioA, float ScaleRatioC, float GradientScale)
{
    // shortcut: FIT. The footage's text is slightly thinner than these floats give; raising the face edge by 0.035
    // matches it best at 720p (re/m3-ui.md, 3.2). Revisit with a 1080p capture.
    private const float FaceFit = 0.035f;

    private float Weight => FaceDilate * ScaleRatioA / 2;

    /// <summary>The atlas value where the face's coverage is half, as the material sets it (no fit).</summary>
    public float FaceEdge => 0.5f - Weight - (OutlineWidth * ScaleRatioA / 2);

    /// <summary>Where the colour turns from face to outline.</summary>
    public float ColourEdge => 0.5f - Weight + (OutlineWidth * ScaleRatioA / 2);

    /// <summary>Where the underlay's coverage is half.</summary>
    public float UnderlayEdge => 0.5f - Weight - (UnderlayDilate * ScaleRatioC / 2);

    /// <summary>The face edge the port draws with, thinned to match the footage.</summary>
    public float DrawnFaceEdge => FaceEdge + FaceFit;
}

/// <summary>A line of text baked to premultiplied RGBA at whole-pixel position (<paramref name="X"/>, <paramref name="Y"/>).</summary>
/// <param name="X">The image's left edge in window pixels.</param>
/// <param name="Y">The image's top edge in window pixels.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Rgba">Premultiplied RGBA, 4 bytes per pixel, rows top-down.</param>
internal sealed record BakedText(int X, int Y, int Width, int Height, byte[] Rgba);

/// <summary>
/// Lays out and draws text the way TextMeshPro's distance-field shader does, on the CPU, since the port has no custom
/// shaders (re/m3-ui.md, section 3). Positions are in the 1920x1080 layout; baking turns them into window pixels.
/// </summary>
internal static class TextRenderer
{
    // TMP's padding around each glyph quad, in font units, for these materials.
    private const float Padding = 5.1f;

    /// <summary>The width of <paramref name="text"/> at <paramref name="size"/>, in layout units: the sum of advances.</summary>
    public static float Advance(FontData font, string text, float size) =>
        text.Sum(c => font.Glyphs.TryGetValue(c, out var g) ? g.Advance : 0) * size / font.PointSize;

    /// <summary>The baseline for text middle-aligned in a box whose centre is <paramref name="centre"/>.</summary>
    public static float MiddleBaseline(FontData font, float centre, float size) => centre + ((font.Ascent + font.Descent) / 2 * size / font.PointSize);

    /// <summary>Face coverage at atlas value <paramref name="c"/>: TMP's linear clamp around the edge, over 1/<paramref name="scale"/>.</summary>
    public static float Coverage(float c, float edge, float scale) => Math.Clamp(((c - edge) * scale) + 0.5f, 0, 1);

    /// <summary>
    /// Splits text into characters, each with the material its <c>&lt;COLOR n&gt;</c> tag chose (starting with
    /// <paramref name="initial"/>). Tags take no width. Characters the font lacks are kept; drawing skips them.
    /// </summary>
    public static List<(char Char, int Material)> Runs(string text, int initial = 0)
    {
        var runs = new List<(char, int)>();
        var material = initial;
        for (var i = 0; i < text.Length; i++)
        {
            if (text.AsSpan(i).StartsWith("<COLOR ") && text.IndexOf('>', i) is var close and > 0 && int.TryParse(text.AsSpan(i + 7, close - i - 7), out var n))
            {
                material = n;
                i = close;
                continue;
            }

            runs.Add((text[i], material));
        }

        return runs;
    }

    /// <summary>
    /// Bakes one line at <paramref name="size"/> with its pen starting at layout x <paramref name="penX"/> on layout
    /// baseline <paramref name="baseline"/>, for a window whose layout scale and offset are <paramref name="fit"/>.
    /// Glyphs are composited in order with premultiplied "over", since their quads overlap.
    /// </summary>
    public static BakedText Bake(FontData font, IReadOnlyList<TextMaterial> materials, IReadOnlyList<(char Char, int Material)> line,
        float size, float penX, float baseline, (float Scale, Vector2 Offset) fit)
    {
        var s = size / font.PointSize * fit.Scale;
        var pen = fit.Offset.X + (penX * fit.Scale);
        var base0 = fit.Offset.Y + (baseline * fit.Scale);
        var quads = new List<(FontGlyph Glyph, TextMaterial Material, float X, float Y, float W, float H)>();
        foreach (var (c, m) in line)
        {
            if (!font.Glyphs.TryGetValue(c, out var g))
            {
                continue;
            }

            if (g.Rect.Width > 0)
            {
                quads.Add((g, materials[m], pen + ((g.BearingX - Padding) * s), base0 - ((g.BearingY + Padding) * s), (g.Width + (2 * Padding)) * s, (g.Height + (2 * Padding)) * s));
            }

            pen += g.Advance * s;
        }

        if (quads.Count == 0)
        {
            return new BakedText(0, 0, 0, 0, []);
        }

        var (x0, y0) = ((int)MathF.Floor(quads.Min(q => q.X)), (int)MathF.Floor(quads.Min(q => q.Y)));
        var (x1, y1) = ((int)MathF.Ceiling(quads.Max(q => q.X + q.W)), (int)MathF.Ceiling(quads.Max(q => q.Y + q.H)));
        var (width, height) = (x1 - x0, y1 - y0);
        var image = new Vector4[width * height];
        foreach (var (g, m, qx, qy, qw, qh) in quads)
        {
            var (uw, vh) = (g.Rect.Width + (2 * Padding), g.Rect.Height + (2 * Padding));

            // Every edge ramps over sqrt(2) screen pixels: TMP's scale under the UI's orthographic projection.
            var scale = MathF.Sqrt(2) * m.GradientScale * (qw / uw);
            var outlineMix = MathF.Sqrt(MathF.Min(1, m.OutlineWidth * m.ScaleRatioA * scale));
            for (var py = (int)MathF.Floor(qy); py < (int)MathF.Ceiling(qy + qh); py++)
            {
                for (var px = (int)MathF.Floor(qx); px < (int)MathF.Ceiling(qx + qw); px++)
                {
                    var (fx, fy) = ((px + 0.5f - qx) / qw, (py + 0.5f - qy) / qh);
                    if (fx is < 0 or >= 1 || fy is < 0 or >= 1)
                    {
                        continue;
                    }

                    // The atlas rows run bottom-up, so v counts down from the padded rect's top.
                    var c = Sample(font, g.Rect.X - Padding + (fx * uw) - 0.5f, g.Rect.Y + g.Rect.Height + Padding - (fy * vh) - 0.5f);
                    var fa = Coverage(c, m.DrawnFaceEdge, scale);
                    var mix = Math.Clamp(((m.ColourEdge - c) * scale) + 0.5f, 0, 1) * outlineMix;
                    var ua = Coverage(c, m.UnderlayEdge, scale) * m.Underlay.W * (1 - fa);
                    var colour = (Vector3.Lerp(new Vector3(m.Face.X, m.Face.Y, m.Face.Z), new Vector3(m.Outline.X, m.Outline.Y, m.Outline.Z), mix) * fa)
                        + (new Vector3(m.Underlay.X, m.Underlay.Y, m.Underlay.Z) * ua);
                    var src = new Vector4(colour, fa + ua);
                    ref var dst = ref image[((py - y0) * width) + (px - x0)];
                    dst = src + (dst * (1 - src.W));
                }
            }
        }

        var rgba = new byte[width * height * 4];
        for (var i = 0; i < image.Length; i++)
        {
            var p = Vector4.Clamp(image[i], Vector4.Zero, Vector4.One) * 255;
            (rgba[4 * i], rgba[(4 * i) + 1], rgba[(4 * i) + 2], rgba[(4 * i) + 3]) = ((byte)MathF.Round(p.X), (byte)MathF.Round(p.Y), (byte)MathF.Round(p.Z), (byte)MathF.Round(p.W));
        }

        return new BakedText(x0, y0, width, height, rgba);
    }

    /// <summary>The atlas value at texel position (u, v), bilinear, as 0 to 1.</summary>
    private static float Sample(FontData font, float u, float v)
    {
        u = Math.Clamp(u, 0, font.AtlasWidth - 1);
        v = Math.Clamp(v, 0, font.AtlasHeight - 1);
        var (x, y) = ((int)u, (int)v);
        var (x1, y1) = (Math.Min(x + 1, font.AtlasWidth - 1), Math.Min(y + 1, font.AtlasHeight - 1));
        var (fx, fy) = (u - x, v - y);
        float At(int ax, int ay) => font.Atlas[(ay * font.AtlasWidth) + ax];
        return ((((At(x, y) * (1 - fx)) + (At(x1, y) * fx)) * (1 - fy)) + (((At(x, y1) * (1 - fx)) + (At(x1, y1) * fx)) * fy)) / 255f;
    }
}
