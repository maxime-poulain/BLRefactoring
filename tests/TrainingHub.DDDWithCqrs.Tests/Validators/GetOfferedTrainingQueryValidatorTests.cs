using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOffered;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Validators;

/// <summary>
/// The second guard on the catalog detail's identifier.
/// </summary>
/// <remarks>
/// The route constraint refuses anything that is not a GUID, and this refuses the one GUID that
/// names nothing, for whatever reaches <c>IQueryDispatcher</c> by another road (ADR 0046).
/// </remarks>
public sealed class GetOfferedTrainingQueryValidatorTests
{
    /// <summary>
    /// Validate, an identifier that names something, accepts it.
    /// </summary>
    [Fact]
    public void Validate_AnIdentifierThatNamesSomething_AcceptsIt() =>
        new GetOfferedTrainingQueryValidator()
            .Validate(new GetOfferedTrainingQuery(Guid.CreateVersion7()))
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, the empty identifier, refuses it by naming the field.
    /// </summary>
    /// <remarks>
    /// Refused rather than answered with a 404: an empty identifier is a malformed message, and
    /// answering "no such training" would say the message was well formed and the training absent.
    /// </remarks>
    [Fact]
    public void Validate_TheEmptyIdentifier_RefusesItByNamingTheField()
    {
        var result = new GetOfferedTrainingQueryValidator()
            .Validate(new GetOfferedTrainingQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(GetOfferedTrainingQuery.TrainingId));
    }
}
