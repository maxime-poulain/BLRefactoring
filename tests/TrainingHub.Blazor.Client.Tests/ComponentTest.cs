using Bunit;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests;

/// <summary>
/// The setup every component test here needs, and the disposal one of them insisted on.
/// </summary>
/// <remarks>
/// Loose interop because MudBlazor reaches for the browser on render — scroll listeners, popovers,
/// resize observers — and none of the decisions under test are about the DOM.
/// <para>
/// The asynchronous disposal is not tidiness. <c>MudChip</c> resolves a key-interception service
/// that implements <see cref="IAsyncDisposable"/> and not <see cref="IDisposable"/>, and disposing
/// the container synchronously throws rather than skipping it — so a test that rendered a chip
/// failed after its assertions had already passed, which is the most confusing way for a suite to
/// go red. Implementing <see cref="IAsyncLifetime"/> is what makes xUnit unwind this context the
/// way the container asks to be unwound.
/// </para>
/// </remarks>
public abstract class ComponentTest : BunitContext, IAsyncLifetime
{
    /// <summary>Component test.</summary>
    protected ComponentTest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The default second is generous on an idle machine and a coin flip when every suite runs
        // at once: a menu that opens through a popover needs two renders, and the CI runner owes
        // neither of them a schedule. Five seconds changes nothing a passing test can observe.
        DefaultWaitTimeout = TimeSpan.FromSeconds(5);

        Services.AddMudServices();

        // The catalog's pages hand their prerendered answer to the interactive pass through
        // PersistentComponentState (ADR 0072). Here there is no prerender, so the store starts
        // empty and every page simply fetches — which is exactly the browser's cold path.
        this.AddBunitPersistentComponentState();

        // Every page of the trainer's space now asks where its caller stands, and the layout's
        // banner asks the same source (ADR 0057). Active by default, so a suite that is not about
        // the sanction says nothing about it; the suites that are override this.
        Standing.Setup(source => source.GetAsync()).ReturnsAsync(TrainerStanding.Active);
        Services.AddSingleton(Standing.Object);

        // The user menu asks the same read for the caller's portrait address (ADR 0074). No photo
        // by default — the initials path — so a suite that is not about the face says nothing
        // about it; the facts that are override this.
        Portrait.Setup(source => source.FindOwnPortraitAsync()).ReturnsAsync((string?)null);
        Services.AddSingleton(Portrait.Object);

        // The real accessor rather than a mock, because both public pages inject it and the one
        // fact about it reads what was deposited (ADR 0083). Registered here the way the browser
        // registers it: one instance, shared by the page and the handler.
        Services.AddSingleton<TurnstileTokenAccessor>();

        // The real localizer over the real satellite assemblies, because the layout and the
        // selector inject it and a mock would only prove that some indexer was called
        // (ADR 0088). The runner's culture is English-or-invariant, so every fact that is not
        // about language reads the neutral words it always read.
        Services.AddLocalization();

        // The selector's client, inert by default: a suite that is not about language says
        // nothing about it, and the facts that are override this.
        Services.AddSingleton(CultureClient.Object);

        // The verification banner's resend, inert by default: the layout renders the banner on
        // every authorized page, so the client must resolve everywhere even though only the
        // verification facts ever make it answer anything (ADR 0090).
        Services.AddSingleton(AuthClient.Object);
    }

    /// <summary>The culture client the selector calls, substituted so a fact can watch the choice.</summary>
    protected Mock<IBffCultureClient> CultureClient { get; } = new();

    /// <summary>The standing every page reads, substituted so a suite can suspend its caller.</summary>
    protected Mock<ITrainerStandingSource> Standing { get; } = new();

    /// <summary>The portrait the user menu shows, substituted so a fact can give its caller a face.</summary>
    protected Mock<ITrainerPortraitSource> Portrait { get; } = new();

    /// <summary>The auth client the verification banner resends through, substituted so a fact
    /// can watch the ask — or refuse it with the window's 429 (ADR 0090).</summary>
    protected Mock<TrainingHub.GeneratedClients.IAuthClient> AuthClient { get; } = new();

    /// <summary>The messages the page raised, in the order it raised them.</summary>
    /// <remarks>
    /// The real MudBlazor service rather than a mock: what is worth asserting is the sentence a
    /// person reads and its severity, and a mock would only prove that some method was called.
    /// </remarks>
    protected IEnumerable<Snackbar> Shown() =>
        Services.GetRequiredService<ISnackbar>().ShownSnackbars;

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
