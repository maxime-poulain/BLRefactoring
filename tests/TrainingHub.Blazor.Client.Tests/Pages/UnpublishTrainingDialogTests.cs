using AwesomeAssertions;
using Bunit;
using TrainingHub.Blazor.Client.Pages.Trainings;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the dialog that withdraws a training from public view.
/// </summary>
/// <remarks>
/// The trainings page substitutes <see cref="IDialogService"/> and pins what it does with the two
/// answers; until here nobody rendered the dialog itself, so its copy — rewritten by ADR 0050 to
/// stop calling a reversible act irreversible — and the two results it may close with were held by
/// nothing. Rendered through <see cref="MudDialogProvider"/>, because that is the only place a
/// MudBlazor dialog exists.
/// </remarks>
public sealed class UnpublishTrainingDialogTests : ComponentTest
{
    private const string Title = "Domain-Driven Design";

    /// <summary>
    /// Rendered, names the training and promises the withdrawal is reversible.
    /// </summary>
    /// <remarks>
    /// The copy replaced "this action cannot be undone", which was true of the deletion this dialog
    /// used to confirm and is the opposite of what withdrawing means (ADR 0050). A trainer who
    /// hesitated over an irreversible button should not hesitate over this one.
    /// </remarks>
    [Fact]
    public async Task Rendered_NamesTheTraining_AndPromisesTheWithdrawalIsReversible()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        dialog.Markup.Should().Contain(Title, "the trainer confirms a title, not an abstraction");
        dialog.Markup.Should().Contain("publish it again at any time",
            "withdrawing is reversible, and saying so is the point (ADR 0050)");
        dialog.Markup.Should().NotContain("cannot be undone",
            "that sentence belonged to the deletion this dialog no longer confirms");
    }

    /// <summary>
    /// Confirming, closes with an affirmative answer.
    /// </summary>
    [Fact]
    public async Task Confirming_ClosesWithAnAffirmativeAnswer()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();

        // Act
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == "Unpublish").Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Canceled.Should().BeFalse();
        result.Data.Should().Be(true, "the page reads this as permission to call the API");
    }

    /// <summary>
    /// Canceling, answers a canceled result.
    /// </summary>
    [Fact]
    public async Task Canceling_AnswersACanceledResult()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();

        // Act
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Canceled.Should().BeTrue("backing out must not read as permission");
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Dialog, IDialogReference Reference)> OpenAsync()
    {
        var dialog = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<UnpublishTrainingDialog>
        {
            { component => component.TrainingTitle, Title }
        };

        var reference = await dialog.InvokeAsync(
            () => dialogService.ShowAsync<UnpublishTrainingDialog>("Unpublish training", parameters));

        return (dialog, reference);
    }

    /// <remarks>
    /// The wait comes before the await, as in <c>ReasonDialogTests</c>: a dialog that never closes
    /// leaves the reference pending forever, and a hang is the worst way for a suite to go red.
    /// </remarks>
    private static async Task<DialogResult> ClosedAsync(
        IRenderedComponent<MudDialogProvider> dialog, IDialogReference reference)
    {
        dialog.WaitForAssertion(() => reference.Result.IsCompleted.Should().BeTrue());

        var result = await reference.Result;

        result.Should().NotBeNull();

        return result;
    }
}
