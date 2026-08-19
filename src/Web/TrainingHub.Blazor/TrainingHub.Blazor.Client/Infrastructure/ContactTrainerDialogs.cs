using MudBlazor;
using TrainingHub.Blazor.Client.Components;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Opens the contact dialog and answers what the visitor wrote, or nothing.
/// </summary>
/// <remarks>
/// Both public pages ask the same question in the same shape, so the plumbing between a page and
/// <see cref="ContactTrainerDialog"/> — the parameter bag, the options, unwrapping the result — is
/// written once, the way <see cref="ReasonDialogs"/> writes it for the sanctions. What differs
/// between the two callers is only whose page the visitor was reading, and that is an argument.
/// </remarks>
public static class ContactTrainerDialogs
{
    /// <summary>
    /// Asks for a message, and answers <see langword="null"/> when the visitor backs out.
    /// </summary>
    /// <param name="dialogService">The dialog service the page injects.</param>
    /// <param name="trainerName">The person the message goes to, shown before anything is asked.</param>
    /// <param name="title">The dialog's title, worded — and localized — by the page that opens it.</param>
    public static async Task<ContactDraft?> AskAsync(
        IDialogService dialogService, string trainerName, string title)
    {
        ArgumentNullException.ThrowIfNull(dialogService);

        var parameters = new DialogParameters<ContactTrainerDialog>
        {
            { dialog => dialog.TrainerName, trainerName }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            // Stated rather than inherited: the provider's own is nullable and unset, so a
            // dialog that says nothing answers Escape however the library decided this
            // release. No test here can press that key (ADR 0093).
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<ContactTrainerDialog>(title, parameters, options);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: ContactDraft draft } ? draft : null;
    }
}
