using MudBlazor;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// What happened to an administrative action, as far as the page needs to know.
/// </summary>
public enum AdministrationOutcome
{
    /// <summary>The API accepted it.</summary>
    Done,

    /// <summary>The API refused it, and said why.</summary>
    Refused,

    /// <summary>Nothing was reached, so nothing is known about the state of anything.</summary>
    Unreachable
}

/// <summary>
/// The sentences one administrative action can answer with, written whole by the page.
/// </summary>
/// <remarks>
/// Whole sentences rather than a verb and a subject spliced into templates: a slot that is
/// grammatical in English survives no other language, so each page hands over localized copy and
/// <see cref="AdministrationFailures"/> only decides which sentence a failure earns (ADR 0089).
/// </remarks>
/// <param name="Success">What to say when the API accepted it.</param>
/// <param name="Gone">What to say when the row disappeared under the list.</param>
/// <param name="Rejected">The fallback when a problem document carries no readable sentence.</param>
/// <param name="NoLongerAllowed">What to say when the caller's authority has lapsed.</param>
/// <param name="CouldNot">What to say when nothing was reached, with the advice to retry.</param>
/// <param name="WentWrong">What to say when the failure is this application's own.</param>
public sealed record AdministrationCopy(
    string Success,
    string Gone,
    string Rejected,
    string NoLongerAllowed,
    string CouldNot,
    string WentWrong);

/// <summary>
/// The one error-handling body the two administration pages share.
/// </summary>
/// <remarks>
/// <para>
/// The four administrative actions fail in exactly the same four ways — <c>400</c> on a reason the
/// domain refuses, <c>404</c> when the row is gone, <c>409</c> when the state is already the one
/// being asked for, <c>403</c> when the caller's authority has lapsed — and eight copies of the
/// same catch stack would drift apart at precisely the two that matter. It lives here rather than
/// on a base page because the pages share no markup, and because Sonar measures duplication in
/// <c>src/Web/</c> like anywhere else.
/// </para>
/// <para>
/// The copy rule this enforces is the one the trainer's own pages already follow: a problem
/// document's <c>Detail</c> is shown as written, because the server is the authority on its own
/// refusal; everything else gets a sentence of ours and a line in the console.
/// <c>ApiException.Message</c> embeds the raw response body and the generator's own wording, so it
/// never reaches a screen.
/// </para>
/// </remarks>
public static class AdministrationFailures
{
    private const int StatusForbidden = 403;
    private const int StatusNotFound = 404;

    /// <summary>
    /// Runs an administrative action and says what became of it.
    /// </summary>
    /// <param name="snackbar">Where the outcome is told.</param>
    /// <param name="action">The call to make.</param>
    /// <param name="copy">The sentences to tell it with, localized by the page.</param>
    public static async Task<AdministrationOutcome> RunAsync(
        ISnackbar snackbar,
        Func<Task> action,
        AdministrationCopy copy)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(copy);

        try
        {
            await action();
            snackbar.Add(copy.Success, Severity.Success);
            return AdministrationOutcome.Done;
        }
        catch (ApiException<ProblemDetails> exception) when (exception.StatusCode == StatusNotFound)
        {
            // Gone between this list being read and the button being pressed. Not an error the
            // administrator caused, and the list is simply stale.
            snackbar.Add(copy.Gone, Severity.Info);
            return AdministrationOutcome.Refused;
        }
        catch (ApiException<ProblemDetails> exception)
        {
            // A 409 lands here too — the state was already the one being asked for — and the
            // server's own sentences say so better than anything this page could invent. So does
            // the 400 a reason the domain refuses produces, whichever envelope carried it.
            foreach (var message in ProblemDetailsMessages.Read(exception.Result, copy.Rejected))
            {
                snackbar.Add(message, Severity.Error);
            }

            return AdministrationOutcome.Refused;
        }
        catch (ApiException exception) when (exception.StatusCode == StatusForbidden)
        {
            // Reachable even here: this page was reached with the administrator role, and the role
            // can have been taken away since. A 403 carries no body, so there is nothing to read
            // out of it.
            Console.Error.WriteLine(exception);
            snackbar.Add(copy.NoLongerAllowed, Severity.Error);
            return AdministrationOutcome.Refused;
        }
        catch (ApiException exception)
        {
            Console.Error.WriteLine(exception);
            snackbar.Add(copy.CouldNot, Severity.Error);
            return AdministrationOutcome.Unreachable;
        }
        catch (Exception exception)
        {
            // Whatever this is, it is ours. A NullReferenceException must not become interface copy.
            Console.Error.WriteLine(exception);
            snackbar.Add(copy.WentWrong, Severity.Error);
            return AdministrationOutcome.Unreachable;
        }
    }

    /// <summary>
    /// Reports a failed read, which has nothing to undo and nothing to report but itself.
    /// </summary>
    /// <param name="snackbar">Where the outcome is told.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="rejected">The fallback when a problem document carries no readable sentence.</param>
    /// <param name="couldNotRetry">What to say when nothing was reached, retry advice included —
    /// one sentence rather than a suffix appended here, for the reason the record above gives.</param>
    /// <param name="wentWrong">What to say when the failure is this application's own.</param>
    public static void Report(
        ISnackbar snackbar,
        Exception exception,
        string rejected,
        string couldNotRetry,
        string wentWrong)
    {
        ArgumentNullException.ThrowIfNull(snackbar);

        switch (exception)
        {
            case ApiException<ProblemDetails> refused:
                foreach (var message in ProblemDetailsMessages.Read(refused.Result, rejected))
                {
                    snackbar.Add(message, Severity.Error);
                }

                break;

            case ApiException:
                Console.Error.WriteLine(exception);
                snackbar.Add(couldNotRetry, Severity.Error);
                break;

            default:
                Console.Error.WriteLine(exception);
                snackbar.Add(wentWrong, Severity.Error);
                break;
        }
    }
}
