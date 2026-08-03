using Bunit;
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

        Services.AddMudServices();
    }

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
