using System.Text.Json;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Reads the sentences a problem document carries, whichever envelope it arrived in.
/// </summary>
/// <remarks>
/// This API publishes two: a model-binding refusal is <c>ValidationProblemDetails</c> — the
/// per-field messages under <c>errors</c>, no <c>detail</c> — while a business refusal carries
/// <c>domainErrors</c>, the list of <c>errorCode</c>/<c>errorMessage</c> pairs the domain raised,
/// of which <c>detail</c> repeats only the first (ADR 0004, ADR 0012). NSwag models neither, so
/// both arrive in the extension data as <see cref="JsonElement"/>. Written once because every page
/// reads a refusal the same way: the sentences are what a person can act on ("Email 'x' is
/// already taken."), and the title above them is what remains when the document carries none.
/// </remarks>
public static class ProblemDetailsMessages
{
    /// <summary>
    /// The messages of a problem document — per field, per domain error, or the one sentence the
    /// document leads with — or the fallback when it carries none of them.
    /// </summary>
    /// <param name="problem">The problem document the API answered.</param>
    /// <param name="fallback">What to say when the document names nothing better itself.</param>
    public static IEnumerable<string> Read(ProblemDetails problem, string fallback)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.AdditionalProperties.TryGetValue("errors", out var raw)
            && raw is JsonElement { ValueKind: JsonValueKind.Object } fields)
        {
            var messages = fields
                .EnumerateObject()
                .SelectMany(field => field.Value.EnumerateArray())
                .Select(message => message.GetString())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!)
                .ToList();

            if (messages.Count > 0)
            {
                return messages;
            }
        }

        if (problem.AdditionalProperties.TryGetValue("domainErrors", out var extension)
            && extension is JsonElement { ValueKind: JsonValueKind.Array } domainErrors)
        {
            var messages = domainErrors
                .EnumerateArray()
                .Where(error => error.ValueKind == JsonValueKind.Object)
                .Select(error => error.TryGetProperty("errorMessage", out var message)
                    ? message.GetString()
                    : null)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!)
                .ToList();

            if (messages.Count > 0)
            {
                return messages;
            }
        }

        return [problem.Detail ?? problem.Title ?? fallback];
    }
}
