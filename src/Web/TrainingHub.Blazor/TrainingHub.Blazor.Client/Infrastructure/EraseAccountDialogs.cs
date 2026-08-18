using MudBlazor;
using TrainingHub.Blazor.Client.Components;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Opens the erasure dialog and answers the password the caller typed, or nothing.
/// </summary>
/// <remarks>
/// One caller today, but the plumbing between a page and its dialog — the options, unwrapping the
/// result — is written beside <see cref="ReasonDialogs"/> and <see cref="ContactTrainerDialogs"/>
/// so every dialog is opened the same way. The dialog collects; the page acts on what it answers.
/// </remarks>
public static class EraseAccountDialogs
{
    /// <summary>
    /// Asks for the caller's password, and answers <see langword="null"/> when they back out.
    /// </summary>
    /// <param name="dialogService">The dialog service the page injects.</param>
    /// <param name="title">The dialog's title, worded — and localized — by the page that opens it.</param>
    public static async Task<string?> AskAsync(IDialogService dialogService, string title)
    {
        ArgumentNullException.ThrowIfNull(dialogService);

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<EraseAccountDialog>(title, options);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: string password } ? password : null;
    }
}
