using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TrainingHub.Blazor.Client.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// Behavior covered for the error-handling body the administration pages share.
/// </summary>
/// <remarks>
/// Exercised directly rather than through a page, because the mapping is the decision: each way an
/// administrative call can fail earns exactly one of the sentences the page handed over, whole and
/// already localized (ADR 0089). The pages' own suites hold that the right copy is handed in; this
/// one holds that the right sentence comes out for each failure.
/// </remarks>
public sealed class AdministrationFailuresTests : ComponentTest
{
    private static readonly AdministrationCopy Copy = new(
        Success: "The trainer was suspended.",
        Gone: "That trainer no longer exists.",
        Rejected: "The request was rejected.",
        NoLongerAllowed: "Your account is no longer allowed to do this.",
        CouldNot: "The trainer could not be suspended. Try again in a moment.",
        WentWrong: "Something went wrong; the trainer was not suspended.");

    /// <summary>
    /// The action succeeded, says so, and answers done.
    /// </summary>
    [Fact]
    public async Task TheActionSucceeded_SaysSo_AndAnswersDone()
    {
        var outcome = await AdministrationFailures.RunAsync(Snackbar(), () => Task.CompletedTask, Copy);

        outcome.Should().Be(AdministrationOutcome.Done);
        Shown().Should().ContainSingle()
            .Which.Should().Match<Snackbar>(snackbar =>
                snackbar.Message == Copy.Success && snackbar.Severity == Severity.Success);
    }

    /// <summary>
    /// The row vanished under the list, says so gently rather than as an error.
    /// </summary>
    [Fact]
    public async Task TheRowVanishedUnderTheList_SaysSoGently_RatherThanAsAnError()
    {
        var outcome = await AdministrationFailures.RunAsync(
            Snackbar(),
            () => throw ApiExceptions.Refused(404, "The trainer was not found."),
            Copy);

        outcome.Should().Be(AdministrationOutcome.Refused);
        Shown().Should().ContainSingle()
            .Which.Should().Match<Snackbar>(snackbar =>
                snackbar.Message == Copy.Gone && snackbar.Severity == Severity.Info,
                "a row deleted between the read and the click is staleness, not a mistake");
    }

    /// <summary>
    /// The callers authority lapsed, says that, since a 403 carries no body to read.
    /// </summary>
    [Fact]
    public async Task TheCallersAuthorityLapsed_SaysThat_SinceA403CarriesNoBodyToRead()
    {
        var outcome = await AdministrationFailures.RunAsync(
            Snackbar(),
            () => throw ApiExceptions.Bare(403),
            Copy);

        outcome.Should().Be(AdministrationOutcome.Refused);
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be(Copy.NoLongerAllowed);
    }

    /// <summary>
    /// Nothing was reached, advises a retry, and does not show the generators own sentence.
    /// </summary>
    [Fact]
    public async Task NothingWasReached_AdvisesARetry_AndDoesNotShowTheGeneratorsOwnSentence()
    {
        var outcome = await AdministrationFailures.RunAsync(
            Snackbar(),
            () => throw ApiExceptions.Unreachable(),
            Copy);

        outcome.Should().Be(AdministrationOutcome.Unreachable);
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be(Copy.CouldNot);
    }

    /// <summary>
    /// The failure was this applications own, does not turn the exception into interface copy.
    /// </summary>
    [Fact]
    public async Task TheFailureWasThisApplicationsOwn_DoesNotTurnTheExceptionIntoInterfaceCopy()
    {
        var outcome = await AdministrationFailures.RunAsync(
            Snackbar(),
            () => throw new InvalidOperationException("a stack detail nobody should read on screen"),
            Copy);

        outcome.Should().Be(AdministrationOutcome.Unreachable);
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be(Copy.WentWrong);
    }

    /// <summary>
    /// A failed read that was this applications own, reports the went-wrong sentence.
    /// </summary>
    [Fact]
    public void AFailedReadThatWasThisApplicationsOwn_ReportsTheWentWrongSentence()
    {
        AdministrationFailures.Report(
            Snackbar(),
            new InvalidOperationException("a stack detail nobody should read on screen"),
            rejected: "The request was rejected.",
            couldNotRetry: "The list could not be read. Try again in a moment.",
            wentWrong: "Something went wrong reading the list.");

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Something went wrong reading the list.");
    }

    /// <summary>The real snackbar service, so the sentence a person reads is what is asserted.</summary>
    private ISnackbar Snackbar() => Services.GetRequiredService<ISnackbar>();
}
