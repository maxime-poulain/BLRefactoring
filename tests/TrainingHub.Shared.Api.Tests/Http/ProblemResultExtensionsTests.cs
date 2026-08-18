using System.Globalization;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Errors;
using TrainingHub.Shared.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Http;

/// <summary>
/// Behavior covered for the problem funnel's language: each refusal answered in the request's
/// culture where the catalog knows its code, and left as authored everywhere else (ADR 0089).
/// </summary>
/// <remarks>
/// Through the real localizer over the real satellite assemblies, because the lookup-or-fallback
/// split is the decision under test and a substitute would only prove that something was called.
/// The culture is set per fact and restored in the same fact.
/// </remarks>
public sealed class ProblemResultExtensionsTests
{
    /// <summary>
    /// A cataloged code, answers the requests language.
    /// </summary>
    [Fact]
    public void ACatalogedCode_AnswersTheRequestsLanguage()
    {
        var problem = ProblemFor("fr", Refusal("Trainer.AlreadySuspended", "This trainer is already suspended."));

        problem.Detail.Should().Be("Ce formateur est déjà suspendu.",
            "the funnel is the API's one translation point, and the summary follows the first entry");
        Entries(problem).Single().ErrorMessage.Should().Be("Ce formateur est déjà suspendu.");
        Entries(problem).Single().ErrorCode.Should().Be("Trainer.AlreadySuspended",
            "the code never moves — a client branching on it keeps its logic in every language");
    }

    /// <summary>
    /// An english request, reads the domains sentence verbatim.
    /// </summary>
    [Fact]
    public void AnEnglishRequest_ReadsTheDomainsSentenceVerbatim()
    {
        var problem = ProblemFor("en", Refusal("Trainer.AlreadySuspended", "This trainer is already suspended."));

        problem.Detail.Should().Be("This trainer is already suspended.",
            "the neutral entries restate the domain's sentences verbatim, so the English wire " +
            "is byte-identical to what the domain wrote");
    }

    /// <summary>
    /// A code the catalog does not know, keeps the authored sentence.
    /// </summary>
    [Fact]
    public void ACodeTheCatalogDoesNotKnow_KeepsTheAuthoredSentence()
    {
        var problem = ProblemFor("fr", Refusal("NotFound", "Training with id `42` not found."));

        problem.Detail.Should().Be("Training with id `42` not found.",
            "a miss keeps the layer's own sentence — the codes whose messages interpolate " +
            "runtime values and the codes several sentences share stay as authored (ADR 0089)");
    }

    /// <summary>
    /// Every entry is answered, not just the first.
    /// </summary>
    [Fact]
    public void EveryEntryIsAnswered_NotJustTheFirst()
    {
        var problem = ProblemFor(
            "fr",
            Refusal("Trainer.AlreadySuspended", "This trainer is already suspended."),
            Refusal("NotFound", "Trainer with id `7` not found."));

        var entries = Entries(problem);

        entries.Should().HaveCount(2, "translation rewrites sentences, never the set of refusals");
        entries[0].ErrorMessage.Should().Be("Ce formateur est déjà suspendu.");
        entries[1].ErrorMessage.Should().Be("Trainer with id `7` not found.",
            "each entry is looked up on its own, so a miss beside a hit stays as authored");
    }

    /// <summary>
    /// A host without localization, keeps every authored sentence.
    /// </summary>
    /// <remarks>
    /// The funnel resolves its catalog as an optional service on purpose: a host that never
    /// called <c>AddApiLocalization</c> must answer the sentences the layers wrote rather than
    /// throw on its first refusal. The container here registers nothing at all, which is that
    /// host at its barest.
    /// </remarks>
    [Fact]
    public void AHostWithoutLocalization_KeepsEveryAuthoredSentence()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        var controller = new Probe
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
        controller.HttpContext.Request.Path = "/trainings";

        var result = (ObjectResult)controller.Problem(
            StatusCodes.Status409Conflict,
            [Refusal("Trainer.AlreadySuspended", "This trainer is already suspended.")]);

        var problem = (ProblemDetails)result.Value!;

        problem.Detail.Should().Be("This trainer is already suspended.",
            "a missing catalog is a passthrough, never a failure");
        Entries(problem).Single().ErrorMessage.Should().Be("This trainer is already suspended.");
    }

    /// <summary>One refusal, as the application layer would have reported it.</summary>
    private static ErrorHttpResponse Refusal(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };

    /// <summary>The domainErrors extension, read back as the funnel wrote it.</summary>
    private static IReadOnlyList<ErrorHttpResponse> Entries(ProblemDetails problem) =>
        (IReadOnlyList<ErrorHttpResponse>)problem.Extensions[ProblemResultExtensions.DomainErrorsExtension]!;

    /// <summary>Runs the funnel under a culture, against the real localizer, and restores it.</summary>
    private static ProblemDetails ProblemFor(string language, params ErrorHttpResponse[] errors)
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .BuildServiceProvider();

        var controller = new Probe
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
        controller.HttpContext.Request.Path = "/trainings";

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

        try
        {
            var result = (ObjectResult)controller.Problem(StatusCodes.Status409Conflict, errors);

            return (ProblemDetails)result.Value!;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>The barest controller the extension can be exercised through.</summary>
    private sealed class Probe : ControllerBase;
}
