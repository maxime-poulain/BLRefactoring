using System.Text.Json;
using AwesomeAssertions;
using BLRefactoring.Shared.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BLRefactoring.Shared.Api.Tests.Errors;

/// <summary>
/// The last handler in the chain, and the only one nothing was proving.
/// </summary>
/// <remarks>
/// The integration suite appeared to cover it: a test posted malformed JSON and asserted
/// <c>BeOneOf(BadRequest, InternalServerError)</c>. Malformed JSON is rejected by
/// <c>[ApiController]</c> at model binding, so the answer is always 400 and this handler was never
/// entered — the <c>BeOneOf</c> made the test pass without it. Which mattered: this is the handler
/// that used to answer a bare quoted string, and the one that decides what an exception is allowed
/// to tell a caller.
/// <para>
/// Asserted here rather than over HTTP because reaching it end-to-end means an endpoint that
/// throws, and adding one to a production host to satisfy a test is a worse trade than testing the
/// handler directly. What the integration suites still prove is that it is registered.
/// </para>
/// </remarks>
public class UnhandledExceptionHandlerTests
{
    private static (HttpContext Context, MemoryStream Body) ContextWithRecordedBody()
    {
        var services = new ServiceCollection();
        services.AddProblemDetails();
        services.AddLogging();

        var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/Training";
        context.Response.Body = body;

        return (context, body);
    }

    private static UnhandledExceptionHandler CreateSut(HttpContext context) =>
        new(context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(),
            NullLogger<UnhandledExceptionHandler>.Instance);

    [Fact]
    public async Task Handle_AnyException_Answers500WithAProblemDocument()
    {
        var (context, body) = ContextWithRecordedBody();
        var sut = CreateSut(context);

        var handled = await sut.TryHandleAsync(
            context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.Should().BeTrue("nothing follows this handler, so declining would leave the caller with no body at all");
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        body.Position = 0;
        var problem = JsonDocument.Parse(body).RootElement;

        problem.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        problem.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task Handle_AnyException_TellsTheCallerNothingAboutTheServer()
    {
        var (context, body) = ContextWithRecordedBody();
        var sut = CreateSut(context);

        var exception = new InvalidOperationException(
            "Invalid column name 'TrainerId' on table dbo.Trainings; connection string was Server=prod;");

        await sut.TryHandleAsync(context, exception, CancellationToken.None);

        body.Position = 0;
        var raw = new StreamReader(body).ReadToEnd();

        // The exception, the method and the path go to the log; none of them go to the caller.
        // A message like the one above is exactly why: it names a table, a column and a server.
        raw.Should().NotContain("TrainerId")
            .And.NotContain("connection string")
            .And.NotContain("StackTrace")
            .And.NotContain("BLRefactoring.");
    }
}
