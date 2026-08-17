using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// The one reader of both problem-document envelopes this API publishes (ADR 0004, ADR 0012).
/// </summary>
/// <remarks>
/// The payloads here are the wire truth, not inventions: the validation document is what model
/// binding answers for an empty title, and the business document is what a duplicate title
/// answers — <c>domainErrors</c> carrying every code, <c>detail</c> repeating only the first.
/// </remarks>
public sealed class ProblemDetailsMessagesTests
{
    /// <summary>
    /// Read, a validation document, answers every field message.
    /// </summary>
    [Fact]
    public void Read_AValidationDocument_AnswersEveryFieldMessage()
    {
        var problem = new ProblemDetails
        {
            Title = "One or more validation errors occurred.",
            Status = 400
        };

        problem.AdditionalProperties["errors"] = JsonSerializer.SerializeToElement(
            new Dictionary<string, string[]>
            {
                ["Title"] =
                [
                    "The Title field is required.",
                    "The field Title must be a string with a minimum length of 5 and a maximum length of 100."
                ],
                ["Description"] = ["The Description field is required."]
            });

        ProblemDetailsMessages.Read(problem, "fallback").Should().Equal(
            "The Title field is required.",
            "The field Title must be a string with a minimum length of 5 and a maximum length of 100.",
            "The Description field is required.");
    }

    /// <summary>
    /// Read, a business document, answers every domain error rather than the first alone.
    /// </summary>
    [Fact]
    public void Read_ABusinessDocument_AnswersEveryDomainError()
    {
        var problem = new ProblemDetails
        {
            Status = 409,
            Detail = "A training with the same title already exists for this trainer."
        };

        problem.AdditionalProperties["domainErrors"] = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                errorMessage = "A training with the same title already exists for this trainer.",
                errorCode = "Training.DuplicateTitle"
            },
            new
            {
                errorMessage = "The catalog is full for this trainer.",
                errorCode = "Trainer.CatalogFull"
            }
        });

        ProblemDetailsMessages.Read(problem, "fallback").Should().Equal(
            "A training with the same title already exists for this trainer.",
            "The catalog is full for this trainer.");
    }

    /// <summary>
    /// Read, a document with empty extensions, answers the detail.
    /// </summary>
    [Fact]
    public void Read_ADocumentWithEmptyExtensions_AnswersTheDetail()
    {
        var problem = new ProblemDetails { Detail = "The one sentence the document leads with." };

        problem.AdditionalProperties["errors"] = JsonSerializer.SerializeToElement(
            new Dictionary<string, string[]>());
        problem.AdditionalProperties["domainErrors"] = JsonSerializer.SerializeToElement(
            Array.Empty<object>());

        ProblemDetailsMessages.Read(problem, "fallback")
            .Should().Equal("The one sentence the document leads with.");
    }

    /// <summary>
    /// Read, a document with only a title, answers the title.
    /// </summary>
    [Fact]
    public void Read_ADocumentWithOnlyATitle_AnswersTheTitle() =>
        ProblemDetailsMessages.Read(new ProblemDetails { Title = "Conflict" }, "fallback")
            .Should().Equal("Conflict");

    /// <summary>
    /// Read, a document saying nothing, answers the fallback.
    /// </summary>
    [Fact]
    public void Read_ADocumentSayingNothing_AnswersTheFallback() =>
        ProblemDetailsMessages.Read(new ProblemDetails(), "The request was rejected.")
            .Should().Equal("The request was rejected.");
}
