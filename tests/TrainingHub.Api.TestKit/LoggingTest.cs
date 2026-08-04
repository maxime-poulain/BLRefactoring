using System.Net.Http.Headers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Holds the logging pipeline to what it promises: the provider bridge stays fed, and every line
/// names its caller.
/// </summary>
/// <remarks>
/// <para>
/// The bridge proof exists for a regression that makes no test fail on its own.
/// <c>AddApiLogging</c> passes <c>writeToProviders: true</c>; with the default <c>false</c>,
/// Serilog still logs everywhere it is configured to, the hosts look healthy — and every
/// <c>ILoggerProvider</c> attached by anyone else goes quiet, including the recording provider
/// this suite relies on to say <em>why</em> a request answered 500. So one test writes an error
/// through the host's own logger factory and demands it back from the recording.
/// </para>
/// <para>
/// The enrichment proofs read the file sink rather than the recording, because they are about the
/// rendered line: the recording keeps formatted error messages, while the caller is a property
/// that only the output template makes visible. The fixture owns the directory the sink writes
/// to, so the reads race with nobody. Deliberately not an <c>IntegrationTest</c>: the log file
/// accumulates across the fixture on purpose, and each proof finds its own lines by a marker
/// nobody else writes.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since the pipeline under test
/// is each host's own.</typeparam>
public abstract class LoggingTest<TFactory>(TFactory factory)
    where TFactory : IServiceScopeSource, IServerErrorSource, IHttpClientSource, ILogFileSource
{
    /// <summary>
    /// An error logged by the host, reaches the recording provider, through serilog.
    /// </summary>
    [Fact]
    public void AnErrorLoggedByTheHost_ReachesTheRecordingProvider_ThroughSerilog()
    {
        var probe = $"Logging bridge probe {Guid.NewGuid():N}";

        using var scope = factory.CreateScope();

        // The factory the host itself resolves — Serilog's, once AddApiLogging has run. Logging
        // through it takes the exact path a real failure takes, which is the path under test.
        scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType().FullName!)
            .LogError("{Probe}", probe);

        factory.ServerErrors
            .Should().Contain(
                error => error.Contains(probe, StringComparison.Ordinal),
                "Serilog must keep feeding the providers registered beside it " +
                "(writeToProviders: true), or a 500's cause is silently thrown away");
    }

    /// <summary>
    /// Every line of a signed request, names the caller.
    /// </summary>
    /// <remarks>
    /// The route carries a fresh identifier, so the request's lines — the per-request summary and
    /// whatever EF logged underneath it — are found by that marker and by nothing else. Asserting
    /// on <em>all</em> of them is the requirement itself: the caller is ambient, not something a
    /// writer remembered to mention.
    /// </remarks>
    [Fact]
    public async Task EveryLineOfASignedRequest_NamesTheCaller()
    {
        var client = factory.CreateClient();
        var registration = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, registration)).EnsureSuccessStatusCode();
        var token = await AuthHelper.LoginAsync(client, registration.Username, registration.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var marker = Guid.NewGuid();
        _ = await client.GetAsync(new Uri($"/Training/{marker}", UriKind.Relative));

        var lines = await LinesContainingAsync(marker.ToString());

        lines.Should().OnlyContain(
            line => line.Contains($"[{registration.Username}]", StringComparison.Ordinal),
            "every line written while a signed request runs must carry its caller");
    }

    /// <summary>
    /// A request nobody signed, is stamped anonymous, not with the previous caller.
    /// </summary>
    /// <remarks>
    /// A signed request runs first on purpose: the failure this guards against is ambient state
    /// leaking from one request into the next, and a leak needs something to leak.
    /// </remarks>
    [Fact]
    public async Task ARequestNobodySigned_IsStampedAnonymous_NotWithThePreviousCaller()
    {
        var signedClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
        _ = await signedClient.GetAsync(new Uri($"/Training/{Guid.NewGuid()}", UriKind.Relative));

        var marker = Guid.NewGuid();
        _ = await factory.CreateClient().GetAsync(new Uri($"/Training/{marker}", UriKind.Relative));

        var lines = await LinesContainingAsync(marker.ToString());

        lines.Should().OnlyContain(
            line => line.Contains("[Anonymous]", StringComparison.Ordinal),
            "an unsigned request is Anonymous, whoever the pipeline served just before");
    }

    /// <summary>
    /// A line written outside any request, says system.
    /// </summary>
    [Fact]
    public async Task ALineWrittenOutsideAnyRequest_SaysSystem()
    {
        var marker = $"system probe {Guid.NewGuid():N}";

        using var scope = factory.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType().FullName!)
            .LogInformation("{Marker}", marker);

        var lines = await LinesContainingAsync(marker);

        lines.Should().OnlyContain(
            line => line.Contains("[System]", StringComparison.Ordinal),
            "a line with no request behind it belongs to the host itself, and says so");
    }

    /// <summary>
    /// The rendered lines holding a marker, waited for briefly because the sink flushes on its own
    /// cadence, and asserted non-empty — a proof that matched nothing proved nothing.
    /// </summary>
    private async Task<IReadOnlyList<string>> LinesContainingAsync(string marker)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        List<string> lines;

        while (true)
        {
            lines = [.. RenderedLines().Where(line => line.Contains(marker, StringComparison.Ordinal))];

            if (lines.Count > 0 || DateTime.UtcNow > deadline)
            {
                break;
            }

            await Task.Delay(50);
        }

        lines.Should().NotBeEmpty(
            $"the file sink should have written at least one line holding '{marker}' — " +
            "if none arrived, the sink itself is broken and every other assertion is vacuous");

        return lines;
    }

    private IEnumerable<string> RenderedLines() =>
        Directory.Exists(factory.LogDirectory)
            ? Directory.EnumerateFiles(factory.LogDirectory, "*.log").SelectMany(SharedReadLines)
            : [];

    /// <summary>Reads a file the sink still holds open, so sharing must allow the writer.</summary>
    private static IEnumerable<string> SharedReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
