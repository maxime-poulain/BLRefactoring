using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Outbox.Requeue;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Validators;

/// <summary>
/// The second guard on the message a requeue names.
/// </summary>
/// <remarks>
/// The HTTP contract refuses the empty identifier at model binding, and this refuses it for anything
/// that reaches <c>ICommandDispatcher</c> by another road (ADR 0046). Not a duplicate: the
/// application layer never assumes the boundary checked first, and that argument does not weaken
/// because the message names a row rather than an aggregate.
/// </remarks>
public sealed class RequeueOutboxMessageCommandValidatorTests
{
    /// <summary>
    /// Validate, a message that names nothing, refuses it.
    /// </summary>
    [Fact]
    public void Validate_AMessageThatNamesNothing_RefusesIt() =>
        new RequeueOutboxMessageCommandValidator()
            .Validate(new RequeueOutboxMessageCommand(Guid.Empty))
            .IsValid.Should().BeFalse();

    /// <summary>
    /// Validate, a message that names something, accepts it.
    /// </summary>
    /// <remarks>
    /// Whether the row exists, and whether it is poison, are the port's answers rather than this
    /// one's: a validator that went looking would be asking a database a question about shape.
    /// </remarks>
    [Fact]
    public void Validate_AMessageThatNamesSomething_AcceptsIt() =>
        new RequeueOutboxMessageCommandValidator()
            .Validate(new RequeueOutboxMessageCommand(Guid.CreateVersion7()))
            .IsValid.Should().BeTrue();
}
