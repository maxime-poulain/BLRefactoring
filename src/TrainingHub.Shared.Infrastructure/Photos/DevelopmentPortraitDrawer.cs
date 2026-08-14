using SkiaSharp;

namespace TrainingHub.Shared.Infrastructure.Photos;

/// <summary>
/// Draws the portrait a seeded trainer publishes.
/// </summary>
/// <remarks>
/// A development catalog whose profiles are all initials tells a reader nothing about what the
/// product looks like with faces in it, and no stock photograph can be committed to this repository
/// without somebody owning its license. So the portraits are drawn: an abstract composition in one
/// hue, distinct per person, honest about being generated (ADR 0079).
/// <para>
/// <b>It lives here because of where imaging is allowed to live.</b>
/// <c>OnlyTheInfrastructure_DecodesAnImage</c> holds the image codec to this project (ADR 0063),
/// and a seeder in the API layer reaching for SkiaSharp would break that line for a convenience.
/// The seeder asks this for bytes instead, which is a call returning a <c>byte[]</c> and carries no
/// imaging type across the boundary.
/// </para>
/// <para>
/// Geometry rather than initials, and that is a portability decision rather than a taste one. The
/// Linux native assets this project requests are the dependency-free build, which ships no
/// fontconfig — asking Skia for a typeface inside the container answers with whatever it can find,
/// which on a runtime image is frequently nothing. A portrait that renders on a developer's machine
/// and comes out blank in Docker would be worse than one that never carried text.
/// </para>
/// </remarks>
public static class DevelopmentPortraitDrawer
{
    /// <summary>
    /// The side of the square portrait, in pixels.
    /// </summary>
    /// <remarks>
    /// Under the sanitizer's own bound of 1024, so that what is stored is what was drawn and the
    /// generated data does not silently exercise the resize path on every single trainer.
    /// </remarks>
    public const int Side = 512;

    /// <summary>
    /// The media type the drawn bytes carry.
    /// </summary>
    public const string ContentType = "image/png";

    /// <summary>
    /// Draws a portrait in the given hue.
    /// </summary>
    /// <param name="hue">The hue, in degrees, between 0 and 359.</param>
    /// <returns>The PNG bytes, ready to be sanitized like any upload.</returns>
    /// <remarks>
    /// Deterministic: the same hue always draws the same image, so re-running the seeder against a
    /// database that already holds these people would store bytes identical to the ones there.
    /// </remarks>
    public static byte[] Draw(int hue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hue, 359);

        using var surface = SKSurface.Create(new SKImageInfo(Side, Side));
        var canvas = surface.Canvas;

        Ground(canvas, hue);
        Arcs(canvas, hue);

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }

    /// <summary>
    /// The diagonal wash the composition stands on.
    /// </summary>
    private static void Ground(SKCanvas canvas, int hue)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(Side, Side),
            [SKColor.FromHsl(hue, 42, 34), SKColor.FromHsl((hue + 38) % 360, 46, 58)],
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint { Shader = shader };

        canvas.DrawRect(SKRect.Create(Side, Side), paint);
    }

    /// <summary>
    /// Three translucent rings, placed from the hue so that two people never share a composition.
    /// </summary>
    private static void Arcs(SKCanvas canvas, int hue)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Side / 14f,
            Color = SKColor.FromHsl((hue + 180) % 360, 30, 88).WithAlpha(70)
        };

        for (var ring = 0; ring < 3; ring++)
        {
            var drift = (hue + (ring * 120)) % 360 / 360f;

            canvas.DrawCircle(
                Side * (0.28f + (drift * 0.44f)),
                Side * (0.72f - (drift * 0.44f)),
                Side * (0.16f + (ring * 0.11f)),
                paint);
        }
    }
}
