using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Components;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages.Profile;
using TrainingHub.Blazor.Client.Pages.Trainings;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// What the trainer's own space shows and refuses while a suspension lasts (ADR 0053, ADR 0057).
/// </summary>
/// <remarks>
/// Presence and disabled-ness are asserted together on every control, and that pairing is the whole
/// point: ADR 0053's verification clause asked for the controls to be absent, and ADR 0057 amends it
/// precisely because a control that is gone teaches nothing — it is indistinguishable from a defect,
/// from a permission the trainer never had, and from a product that simply does not do that. A fact
/// that only asserted "disabled" would pass against a page that removed them.
/// </remarks>
public sealed class SuspendedTrainerTests : ComponentTest
{
    private const string Motive = "Repeated breaches of the content policy.";

    private readonly Mock<ITrainerClient> _trainerClient = new();
    private readonly Mock<ITrainingClient> _trainingClient = new();

    /// <summary>Suspended trainer tests.</summary>
    public SuspendedTrainerTests()
    {
        Standing
            .Setup(source => source.GetAsync())
            .ReturnsAsync(new TrainerStanding(IsSuspended: true, Motive));

        Services.AddSingleton(_trainerClient.Object);
        Services.AddSingleton(_trainingClient.Object);

        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ReturnsAsync(new SwaggerResponse<TrainerHttpResponse>(
                200,
                new Dictionary<string, IEnumerable<string>>(),
                new TrainerHttpResponse
                {
                    Id = Guid.NewGuid(),
                    Firstname = "Ada",
                    Lastname = "Lovelace",
                    ContactEmail = "ada@example.com",
                    Status = "Suspended",
                    SuspensionReason = Motive
                }));

        _trainingClient
            .Setup(client => client.GetMineAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new PagedHttpResponseOfTrainingHttpResponse
            {
                Items = [Training()],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                TotalPages = 1,
                HasNextPage = false,
                HasPreviousPage = false
            });
    }

    /// <summary>
    /// The banner, a suspension in force, says the reason in the administrator's own words.
    /// </summary>
    [Fact]
    public void TheBanner_ASuspensionInForce_SaysTheReasonInTheAdministratorsOwnWords()
    {
        // Act
        var banner = Render<SuspensionBanner>();

        // Assert
        banner.Markup.Should().Contain(Motive);
        banner.Markup.Should().Contain("cannot change anything");
    }

    /// <summary>
    /// The banner, nothing in force, renders nothing at all.
    /// </summary>
    /// <remarks>
    /// It sits above every routed page, so a banner that rendered an empty shell would put a gap at
    /// the top of the application for everybody.
    /// </remarks>
    [Fact]
    public void TheBanner_NothingInForce_RendersNothingAtAll()
    {
        // Arrange
        Standing.Setup(source => source.GetAsync()).ReturnsAsync(TrainerStanding.Active);

        // Act
        var banner = Render<SuspensionBanner>();

        // Assert
        banner.Markup.Trim().Should().BeEmpty();
    }

    /// <summary>
    /// The profile, a suspension in force, keeps its write controls and disables them.
    /// </summary>
    [Fact]
    public void TheProfile_ASuspensionInForce_KeepsItsWriteControlsAndDisablesThem()
    {
        // Act
        var page = Render<Profile>();

        // Assert
        var save = page.WaitForElement("button:contains('Save Changes')");
        save.HasAttribute("disabled").Should().BeTrue();
    }

    /// <summary>
    /// The catalogue, a suspension in force, keeps its write controls and disables them.
    /// </summary>
    /// <remarks>
    /// Asserted on every button the page renders rather than on a chosen one: what the sanction
    /// takes away is the writing, not one endpoint, and a page that disabled three of four would
    /// pass a narrower fact.
    /// </remarks>
    [Fact]
    public void TheCatalogue_ASuspensionInForce_KeepsItsWriteControlsAndDisablesThem()
    {
        // Act
        var page = Render<Trainings>();

        // Assert
        var create = page.WaitForElement("a:contains('Create Training'), button:contains('Create Training')");
        create.HasAttribute("disabled").Should().BeTrue();
    }

    private static TrainingHttpResponse Training() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Domain-Driven Design",
        TrainerId = Guid.NewGuid(),
        Topics = ["Backend"],
        Description = "A description long enough to be valid.",
        Prerequisites = "None to speak of.",
        AcquiredSkills = "Modelling a domain.",
        Status = "Published"
    };
}
