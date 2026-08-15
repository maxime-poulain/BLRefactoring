using System.Text.Json;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Reads the sentences a problem document carries, field by field.
/// </summary>
/// <remarks>
/// <c>errors</c> is the member RFC 7807's <c>ValidationProblemDetails</c> uses for a map of field
/// name to messages; this API's own error codes travel separately, under <c>domainErrors</c>.
/// NSwag models neither, so both arrive in the extension data as <see cref="JsonElement"/>.
/// Written once because more than one page reads a refusal the same way: the per-field sentences
/// are what a person can act on ("Email 'x' is already taken."), and the title above them is what
/// remains when the document carries none.
/// </remarks>
public static class ProblemDetailsMessages
{
    /// <summary>
    /// The per-field messages of a validation problem document, or the fallback when it carries none.
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

        return [problem.Detail ?? problem.Title ?? fallback];
    }
}
