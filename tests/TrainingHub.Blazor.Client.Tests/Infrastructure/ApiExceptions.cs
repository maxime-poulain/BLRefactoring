using System.Text.Json;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// The exceptions a generated client throws, built once for every page test.
/// </summary>
/// <remarks>
/// Five test files used to declare their own, in three different argument styles, and the shapes
/// had already drifted. The wire truth these encode: a status declared with a
/// <c>ProblemDetails</c> body arrives as <see cref="ApiException{TResult}"/>, its extensions —
/// <c>errors</c> for a validation refusal, <c>domainErrors</c> for a business one — as
/// <see cref="JsonElement"/> in the extension data; anything else arrives as the bare
/// <see cref="ApiException"/> whose <c>Message</c> is the generator's own sentence.
/// </remarks>
internal static class ApiExceptions
{
    /// <summary>A refusal whose problem document leads with one sentence.</summary>
    /// <param name="statusCode">The status the API answered.</param>
    /// <param name="detail">The document's <c>detail</c>.</param>
    public static ApiException<ProblemDetails> Refused(int statusCode, string detail) => new(
        "refused",
        statusCode,
        response: null,
        new Dictionary<string, IEnumerable<string>>(),
        new ProblemDetails { Detail = detail },
        innerException: null);

    /// <summary>A business refusal carrying its domain errors, the way the API writes them.</summary>
    /// <param name="statusCode">The status the API answered.</param>
    /// <param name="messages">The <c>errorMessage</c> of each domain error, in order.</param>
    public static ApiException<ProblemDetails> RefusedWithDomainErrors(
        int statusCode,
        params string[] messages)
    {
        var problem = new ProblemDetails { Detail = messages.FirstOrDefault() };

        problem.AdditionalProperties["domainErrors"] = JsonSerializer.SerializeToElement(
            messages.Select(message => new { errorCode = "Test.Refused", errorMessage = message }));

        return new ApiException<ProblemDetails>(
            "refused",
            statusCode,
            response: null,
            new Dictionary<string, IEnumerable<string>>(),
            problem,
            innerException: null);
    }

    /// <summary>A validation refusal keyed by field, the way model binding writes one.</summary>
    /// <param name="fields">The field map: each name, and the messages refusing it.</param>
    public static ApiException<ProblemDetails> RefusedWithFieldErrors(
        params (string Field, string[] Messages)[] fields)
    {
        var problem = new ProblemDetails { Title = "One or more validation errors occurred." };

        problem.AdditionalProperties["errors"] = JsonSerializer.SerializeToElement(
            fields.ToDictionary(field => field.Field, field => field.Messages));

        return new ApiException<ProblemDetails>(
            "refused",
            400,
            response: null,
            new Dictionary<string, IEnumerable<string>>(),
            problem,
            innerException: null);
    }

    /// <summary>A bodiless refusal or outage — the bare exception the generator throws.</summary>
    /// <param name="statusCode">The status the API answered.</param>
    public static ApiException Bare(int statusCode) => new(
        $"The HTTP status code of the response was not expected ({statusCode}).",
        statusCode,
        response: null,
        new Dictionary<string, IEnumerable<string>>(),
        innerException: null);

    /// <summary>The 503 nothing answered.</summary>
    public static ApiException Unreachable() => Bare(503);

    /// <summary>The bare 404 an undeclared body arrives as.</summary>
    public static ApiException NotFound() => Bare(404);
}
