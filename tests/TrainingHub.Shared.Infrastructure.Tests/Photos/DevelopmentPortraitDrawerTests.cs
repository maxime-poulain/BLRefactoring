using AwesomeAssertions;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Photos;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Photos;

/// <summary>
/// The portrait a seeded trainer publishes, put through everything a real upload goes through
/// (ADR 0079).
/// </summary>
/// <remarks>
/// The seeder does not write a photo row: it draws bytes and hands them to <c>TrainerPhoto.Vet</c>,
/// the sanitizer and the aggregate, in that order (ADR 0063). So the only thing worth asserting
/// about the drawing is that the whole chain accepts it — a portrait the aggregate would refuse is
/// a seeder that throws partway through somebody's database, and the refusal would arrive with a
/// hue rather than a defect to fix.
/// </remarks>
public sealed class DevelopmentPortraitDrawerTests
{
    private readonly SkiaSharpPhotoSanitizer _sanitizer = new();

    /// <summary>
    /// Draw, any hue of the wheel, survives the whole publishing chain.
    /// </summary>
    /// <remarks>
    /// The four corners and a handful between them rather than all three hundred and sixty: what
    /// varies with the hue is the color of the pixels, and the chain has no opinion about those.
    /// The corners are there because zero and three hundred and fifty-nine are where an off-by-one
    /// in the drawing's own bounds check would show.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(359)]
    public void Draw_AnyHueOfTheWheel_SurvivesTheWholePublishingChain(int hue)
    {
        var drawn = DevelopmentPortraitDrawer.Draw(hue);

        var vetted = Unwrap(TrainerPhoto.Vet(drawn, DevelopmentPortraitDrawer.ContentType));
        vetted.Should().Be(DevelopmentPortraitDrawer.ContentType);

        var sanitized = Unwrap(_sanitizer.Sanitize(drawn, vetted));

        var photo = Unwrap(TrainerPhoto.Create(
            sanitized.Content.Span, sanitized.ContentType, DateTime.UtcNow));

        photo.ContentType.Should().Be(DevelopmentPortraitDrawer.ContentType);
        photo.ByteSize.Should().Be(sanitized.Content.Length);
    }

    /// <summary>
    /// Draw, the same hue twice, answers the same bytes.
    /// </summary>
    /// <remarks>
    /// The dataset is deterministic and this is the half of it that leaves the process: a seeder
    /// re-run against a database that already holds these people would store bytes identical to
    /// the ones there, so nothing about the object store depends on the order runs happened in.
    /// </remarks>
    [Fact]
    public void Draw_TheSameHueTwice_AnswersTheSameBytes() =>
        DevelopmentPortraitDrawer.Draw(41).Should().Equal(DevelopmentPortraitDrawer.Draw(41));

    /// <summary>
    /// Draw, two hues, answers different bytes.
    /// </summary>
    /// <remarks>
    /// Otherwise a hundred and forty portraits would be one portrait, and a page of trainers would
    /// look like a rendering defect rather than a catalog.
    /// </remarks>
    [Fact]
    public void Draw_TwoHues_AnswersDifferentBytes() =>
        DevelopmentPortraitDrawer.Draw(10).Should().NotEqual(DevelopmentPortraitDrawer.Draw(200));

    /// <summary>
    /// Draw, a hue off the wheel, is refused.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(360)]
    public void Draw_AHueOffTheWheel_IsRefused(int hue)
    {
        var draw = () => DevelopmentPortraitDrawer.Draw(hue);

        draw.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static TValue Unwrap<TValue>(Result<TValue> result) =>
        result.Match(
            value => value,
            errors => throw new InvalidOperationException(string.Join("; ",
                errors.Select(error => error.ErrorMessage))));
}
