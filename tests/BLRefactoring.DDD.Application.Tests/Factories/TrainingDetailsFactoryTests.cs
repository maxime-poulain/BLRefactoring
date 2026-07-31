using AwesomeAssertions;
using BLRefactoring.Shared.Application.Factories;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.Factories;

/// <summary>
/// Turning primitives into value objects — including resolving topic names against
/// the closed set the domain owns — is the application layer's job, so this is where
/// the accumulation of validation errors is covered.
/// </summary>
public class TrainingDetailsFactoryTests
{
    [Fact]
    public void Create_ValidInput_ReturnsTheValueObjects()
    {
        var result = TrainingDetailsFactory.Create(
            "Valid Training Title",
            "A valid training description for testing purposes",
            "Basic programming knowledge required",
            "Advanced design patterns mastery",
            ["Programming", "Design"]);

        var details = result.ShouldBeSuccess();
        details.Title.Value.Should().Be("Valid Training Title");
        details.Topics.Should().HaveCount(2);
        details.Topics.Should().Contain(Topic.Programming);
        details.Topics.Should().Contain(Topic.Design);
    }

    [Fact]
    public void Create_InvalidTitle_ReturnsFailure()
    {
        var result = TrainingDetailsFactory.Create(
            "ab", "A valid description", "Valid prerequisites", "Valid skills", ["Programming"]);

        result.ShouldBeFailure();
    }

    [Fact]
    public void Create_SeveralInvalidInputs_ReportsThemAllAtOnce()
    {
        var result = TrainingDetailsFactory.Create("ab", "", "", "", ["Programming"]);

        var errors = result.ShouldBeFailure();
        errors.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Create_UnknownTopic_ReturnsInvalidTopicFailure()
    {
        var result = TrainingDetailsFactory.Create(
            "Valid Training Title",
            "A valid training description for testing purposes",
            "Basic programming knowledge required",
            "Advanced design patterns mastery",
            ["Underwater Basket Weaving"]);

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCode.InvalidTopic);
    }

    [Fact]
    public void Create_UnknownTopic_IsAValidationErrorNotAnException()
    {
        // The closed set of topics belongs to the domain, but an unknown name is
        // user input like any other — it must never surface as an exception.
        var act = () => TrainingDetailsFactory.Create(
            "Valid Training Title", "A valid description", "Valid prerequisites", "Valid skills",
            ["Not A Real Topic"]);

        act.Should().NotThrow();
    }

    [Fact]
    public void Create_NullTopicNames_ThrowsArgumentNullException()
    {
        var act = () => TrainingDetailsFactory.Create(
            "Valid Training Title", "A valid description", "Valid prerequisites", "Valid skills", null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
