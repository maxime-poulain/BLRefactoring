using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TrainingHub.Blazor.Client.Components;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Components;

/// <summary>
/// Behavior covered for the dialog the erasure asks its question through.
/// </summary>
/// <remarks>
/// The password is not a field on this dialog, it is the dialog: erasing the account is the one
/// action this API cannot undo for anyone, so the proof of intent has to be collected before the
/// page acts (ADR 0085). What is pinned here is the pair of answers this component may give — the
/// password somebody typed, or nothing at all — and never a third.
/// <para>
/// Rendered through <see cref="MudDialogProvider"/> rather than on its own, because that is the
/// only place a MudBlazor dialog exists. The page that opens it substitutes the service instead,
/// and pins the other half: that what comes back out of here is what reaches the BFF.
/// </para>
/// </remarks>
public sealed class EraseAccountDialogTests : ComponentTest
{
    /// <summary>
    /// Rendered, the password still empty, leaves the erasing button disabled.
    /// </summary>
    [Fact]
    public async Task Rendered_ThePasswordStillEmpty_LeavesTheErasingButtonDisabled()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        Erasing(dialog).HasAttribute("disabled").Should().BeTrue();
    }

    /// <summary>
    /// Rendered, says what will be erased before asking anything.
    /// </summary>
    /// <remarks>
    /// The consequence is the reason the dialog exists at all: a password field with no sentence
    /// above it asks for proof of an intent it never stated.
    /// </remarks>
    [Fact]
    public async Task Rendered_SaysWhatWillBeErased()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        dialog.Markup.Should().Contain("for good");
        dialog.Markup.Should().Contain("anyone to register with",
            "the released username and email are the half of finality that surprises people");
    }

    /// <summary>
    /// Confirming, a password typed, closes with it.
    /// </summary>
    [Fact]
    public async Task Confirming_APasswordTyped_ClosesWithIt()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();
        dialog.Find("input").Input("Password1!");

        // The button follows the field rather than the keystroke: the form validates on a later
        // render, so this waits for the state the caller would actually see.
        dialog.WaitForAssertion(() => Erasing(dialog).HasAttribute("disabled").Should().BeFalse());

        // Act
        Erasing(dialog).Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Canceled.Should().BeFalse();
        result.Data.Should().Be("Password1!",
            "the password travels as typed — the API is the judge of it, not this dialog");
    }

    /// <summary>
    /// Canceling, answers nothing at all.
    /// </summary>
    /// <remarks>
    /// Backing out is not a decision, and the page reads a canceled result as "do not erase". A
    /// dialog that closed with an empty password instead would have it send one.
    /// </remarks>
    [Fact]
    public async Task Canceling_AnswersNothingAtAll()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();
        dialog.Find("input").Input("Password1!");

        // Act
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Canceled.Should().BeTrue();
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Dialog, IDialogReference Reference)> OpenAsync()
    {
        var dialog = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var reference = await dialog.InvokeAsync(
            () => dialogService.ShowAsync<EraseAccountDialog>("Erase this account?"));

        return (dialog, reference);
    }

    /// <remarks>
    /// The wait comes before the await, and that ordering is the point: a dialog that never closes
    /// leaves <see cref="IDialogReference.Result"/> pending forever, so awaiting it first turns a
    /// failing assertion into a suite that hangs rather than one that goes red.
    /// </remarks>
    private static async Task<DialogResult> ClosedAsync(
        IRenderedComponent<MudDialogProvider> dialog, IDialogReference reference)
    {
        dialog.WaitForAssertion(() => reference.Result.IsCompleted.Should().BeTrue());

        var result = await reference.Result;

        result.Should().NotBeNull();

        return result;
    }

    private static AngleSharp.Dom.IElement Erasing(IRenderedComponent<MudDialogProvider> dialog) =>
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == "Erase account");
}
