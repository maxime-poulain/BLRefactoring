using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetAdministered;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetAdministered;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Validators;

/// <summary>
/// The second guard on the administrative listings' status filter.
/// </summary>
/// <remarks>
/// The HTTP contract refuses an unknown name at model binding, and this refuses it for anything that
/// reaches <c>IQueryDispatcher</c> by another road (ADR 0046). The two are not duplicates: without
/// this one, a query built in code would carry its unknown name into
/// <c>TrainingStatus.FromName</c>, which throws by design.
/// </remarks>
public sealed class AdministeredQueryValidatorTests
{
    /// <summary>
    /// Validate, a status the domain declares, accepts it.
    /// </summary>
    [Fact]
    public void Validate_AStatusTheDomainDeclares_AcceptsIt() =>
        new GetAdministeredTrainersQueryValidator()
            .Validate(new GetAdministeredTrainersQuery { Status = "Suspended" })
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, no status at all, accepts it.
    /// </summary>
    /// <remarks>
    /// Asking for every trainer is the default, not an omission to report.
    /// </remarks>
    [Fact]
    public void Validate_NoStatusAtAll_AcceptsIt() =>
        new GetAdministeredTrainersQueryValidator()
            .Validate(new GetAdministeredTrainersQuery())
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, a status no trainer answers to, refuses it by naming the field.
    /// </summary>
    [Fact]
    public void Validate_AStatusNoTrainerAnswersTo_RefusesItByNamingTheField()
    {
        var result = new GetAdministeredTrainersQueryValidator()
            .Validate(new GetAdministeredTrainersQuery { Status = "Banished" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(GetAdministeredTrainersQuery.Status));
    }

    /// <summary>
    /// Validate, the withheld state, is accepted without the validator naming it.
    /// </summary>
    /// <remarks>
    /// The third state arrived in #98, after this shape of validator existed elsewhere. Reading the
    /// names off <c>GetStatuses()</c> is what stops a list here from being the thing that forgot
    /// (ADR 0041).
    /// </remarks>
    [Fact]
    public void Validate_TheWithheldState_IsAcceptedWithoutTheValidatorNamingIt() =>
        new GetAdministeredTrainingsQueryValidator()
            .Validate(new GetAdministeredTrainingsQuery { Status = "Withheld" })
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, a term of any shape, is not the validator's business.
    /// </summary>
    /// <remarks>
    /// Its bound belongs to the boundary, where a caller can be told which parameter was too long,
    /// and its meaning belongs to the repository, where blank is no filter. Stating it a third time
    /// here would be the duplication ADR 0043 removes.
    /// </remarks>
    [Fact]
    public void Validate_ATermOfAnyShape_IsNotTheValidatorsBusiness() =>
        new GetAdministeredTrainersQueryValidator()
            .Validate(new GetAdministeredTrainersQuery { Search = new string('x', 5_000) })
            .IsValid.Should().BeTrue();
}
