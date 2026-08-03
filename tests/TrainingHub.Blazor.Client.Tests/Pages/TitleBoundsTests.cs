using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Pages.Trainings;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behaviour covered for the bounds the training form enforces before the API sees them.
/// </summary>
/// <remarks>
/// Client-side validation here is a mirror, never the authority: it exists so a person is told
/// immediately rather than after a round trip, and the API stays the judge. A mirror is only useful
/// while it agrees with what it reflects — and the way it stops agreeing is silent. This page
/// allowed a title of 30 characters while the contract, the document and the generated client all
/// allowed 100, so it refused titles the server would have accepted. Nothing failed: too strict is
/// the direction that produces no error to read, only a user who cannot type what they meant.
/// <para>
/// The expectation is therefore not written here. It is read off
/// <see cref="CreateTrainingRequestHttp"/>, which NSwag generates from the API's own OpenAPI
/// document, which the API generates from the annotation that mirrors the domain's value object.
/// Change <c>TrainingTitle</c> and the chain moves; forget the page and this fails.
/// </para>
/// </remarks>
public sealed class TitleBoundsTests : ComponentTest
{
    /// <summary>
    /// Title bounds tests.
    /// </summary>
    public TitleBoundsTests()
    {
        Services.AddSingleton(new Mock<ITrainingClient>().Object);
    }

    /// <summary>
    /// The title field, stops where the published contract stops.
    /// </summary>
    [Fact]
    public void TheTitleField_StopsWhereThePublishedContractStops()
    {
        // Arrange
        var published = Published();

        // Act
        var page = Render<CreateTraining>();

        // Assert
        page.Find("input").GetAttribute("maxlength")
            .Should().Be(published.MaximumLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The title field, tells the visitor the same bounds it enforces.
    /// </summary>
    /// <remarks>
    /// The helper text is built from the same two constants, so this asserts that the sentence a
    /// person reads and the ceiling the field applies cannot drift apart — the failure mode being a
    /// form that says one thing and does another.
    /// </remarks>
    [Fact]
    public void TheTitleField_TellsTheVisitorTheSameBoundsItEnforces()
    {
        // Arrange
        var published = Published();

        // Act
        var page = Render<CreateTraining>();

        // Assert
        page.Markup.Should().Contain($"Between {published.MinimumLength} and {published.MaximumLength} characters");
    }

    /// <summary>
    /// The bounds the API publishes for a training title, as the generated client carries them.
    /// </summary>
    private static StringLengthAttribute Published() =>
        typeof(CreateTrainingRequestHttp)
            .GetProperty(nameof(CreateTrainingRequestHttp.Title))!
            .GetCustomAttribute<StringLengthAttribute>()
        ?? throw new InvalidOperationException(
            "The generated contract declares no length for a training title. Either the API stopped " +
            "publishing one — in which case this page's ceiling answers to nothing — or the client " +
            "was generated without data annotations. Both make this rule pass while checking nothing.");
}
