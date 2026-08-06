using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Contracts;

/// <summary>
/// Behaviour covered for <c>NotEmptyIdentifierAttribute</c>.
/// </summary>
/// <remarks>
/// The attribute exists because <c>[Required]</c> cannot refuse <see cref="Guid.Empty"/> on a
/// non-nullable <see cref="Guid"/>, and its docstring makes four separate claims about what it
/// answers. Asserted here rather than only through the API, where the empty identifier is refused
/// end to end: an integration test proves the request is answered 400, and says nothing about the
/// three cases that must pass (ADR 0046).
/// </remarks>
public sealed class NotEmptyIdentifierAttributeTests
{
    private readonly NotEmptyIdentifierAttribute _attribute = new();

    /// <summary>
    /// Is valid, the empty identifier, is refused.
    /// </summary>
    [Fact]
    public void IsValid_TheEmptyIdentifier_IsRefused() =>
        _attribute.IsValid(Guid.Empty).Should().BeFalse();

    /// <summary>
    /// Is valid, an identifier, is accepted.
    /// </summary>
    [Fact]
    public void IsValid_AnIdentifier_IsAccepted() =>
        _attribute.IsValid(Guid.NewGuid()).Should().BeTrue();

    /// <summary>
    /// Is valid, an absent value, is left to required.
    /// </summary>
    /// <remarks>
    /// Absence is a different question, and answering it here would make the two annotations report
    /// the same field twice on a nullable identifier.
    /// </remarks>
    [Fact]
    public void IsValid_AnAbsentValue_IsLeftToRequired() =>
        _attribute.IsValid(null).Should().BeTrue();

    /// <summary>
    /// Is valid, a value that is not an identifier, is not judged.
    /// </summary>
    /// <remarks>
    /// The framework convention for an attribute asked about a type it does not judge. Applying it
    /// to a string is a mistake, and one this attribute is not the right place to report.
    /// </remarks>
    [Fact]
    public void IsValid_AValueThatIsNotAnIdentifier_IsNotJudged() =>
        _attribute.IsValid("not an identifier").Should().BeTrue();

    /// <summary>
    /// Format error message, names the field.
    /// </summary>
    /// <remarks>
    /// The message reaches the caller inside a <c>ValidationProblemDetails</c> keyed by field, so a
    /// message that did not name the field would leave them the key alone to work from.
    /// </remarks>
    [Fact]
    public void FormatErrorMessage_NamesTheField() =>
        _attribute.FormatErrorMessage("trainingId")
            .Should().Contain("trainingId");
}
