using TrainingHub.Shared;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.Contact;

/// <summary>
/// Asks that a visitor's message be carried to a trainer (ADR 0082).
/// </summary>
/// <remarks>
/// The first command in this application that no aggregate answers, and the record argues why it is
/// still a command rather than a query with a side effect: it asks for something to happen, it can
/// be refused, and it answers a bare <c>Result</c> like every other one.
/// <para>
/// Everything it carries belongs to the visitor. Where the message ends up is resolved two layers
/// away, after the commit, by the only consumer allowed to read a trainer's contact address.
/// </para>
/// </remarks>
public sealed class ContactTrainerCommand : ICommand<Result>
{
    /// <summary>The trainer the message is addressed to.</summary>
    public required Guid TrainerId { get; init; }

    /// <summary>
    /// The training the visitor was reading, or <see langword="null"/> when they wrote from the
    /// trainer's own page.
    /// </summary>
    public Guid? TrainingId { get; init; }

    /// <summary>The visitor's first name, as they gave it.</summary>
    public required string SenderFirstname { get; init; }

    /// <summary>The visitor's last name, as they gave it.</summary>
    public required string SenderLastname { get; init; }

    /// <summary>The address the visitor asks to be answered at.</summary>
    public required string SenderEmailAddress { get; init; }

    /// <summary>What the visitor wrote.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Whether the honeypot came back filled, in which case nothing is sent (ADR 0082).
    /// </summary>
    /// <remarks>
    /// The verdict travels rather than the value: what a bot typed is of no interest to any layer
    /// below the boundary, and carrying the string would invite somebody to start reading it.
    /// </remarks>
    public bool LooksAutomated { get; init; }
}

/// <summary>
/// Runs <see cref="ContactTrainerCommand"/>.
/// </summary>
/// <remarks>
/// It reads before it writes, and the read is the catalog's own: a trainer the index will not show
/// a visitor is a trainer that visitor may not write to, so the profile's 404 and this one are the
/// same refusal under the same composed visibility (ADR 0062, ADR 0070).
/// <para>
/// What it writes is one outbox row. There is no aggregate to update, which is why this handler
/// takes no repository — and the <c>SaveChangesAsync</c> is its own rather than an aggregate's,
/// because nothing else in the request touched the unit of work (ADR 0002, ADR 0082).
/// </para>
/// </remarks>
public sealed class ContactTrainerCommandHandler(
    ICatalogDetailQuery catalogDetail,
    IIntegrationEventPublisher integrationEvents,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ContactTrainerCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="request">The message and the trainer it is addressed to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async ValueTask<Result> Handle(ContactTrainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A bot is answered exactly as a person is, minus the sending: telling it apart is what
        // would teach it to try again with the field left blank (ADR 0082).
        if (request.LooksAutomated)
        {
            return Result.Success();
        }

        var trainer = await catalogDetail.FindOfferedTrainerAsync(request.TrainerId, cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(
                ErrorCodes.NotFound,
                $"No offered trainer with id `{request.TrainerId}` could be found.");
        }

        await integrationEvents.PublishAsync(
            new TrainerContactedIntegrationEvent(
                request.TrainerId,
                request.TrainingId,
                request.SenderFirstname,
                request.SenderLastname,
                request.SenderEmailAddress,
                request.Message),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
