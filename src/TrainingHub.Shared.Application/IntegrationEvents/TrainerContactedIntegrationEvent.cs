namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A visitor contacted a trainer — the fact, as published to consumers outside this bounded
/// context.
/// </summary>
/// <remarks>
/// The one fact in this application that no aggregate produced, and ADR 0082 argues why it is still
/// a fact rather than an intent: a visitor pressing send has already contacted the trainer, and
/// nothing about a trainer changes when they do. There is simply no aggregate whose state records
/// it, so the outbox row is the record — which is also what makes the delivery survive an SMTP
/// server that is down when the visitor was there and up ten minutes later (ADR 0002, ADR 0024).
/// <para>
/// <b>The trainer's address is deliberately absent.</b> Every other address on this vocabulary
/// travels with its fact because the fact <em>is</em> about that address — a registration, a
/// change of contact address. Here the recipient is a routing detail, so it is resolved at delivery
/// through <c>ITrainerContactQuery</c> and never written to the outbox. The visitor's address is
/// present because it is the message: it is what the trainer replies to.
/// </para>
/// </remarks>
/// <param name="TrainerId">The trainer the message is addressed to.</param>
/// <param name="TrainingId">
/// The training the visitor was reading when they wrote, or <see langword="null"/> when they wrote
/// from the trainer's own page.
/// </param>
/// <param name="SenderFirstname">The visitor's first name, as they gave it.</param>
/// <param name="SenderLastname">The visitor's last name, as they gave it.</param>
/// <param name="SenderEmailAddress">The address the visitor asks to be answered at.</param>
/// <param name="Message">What the visitor wrote.</param>
public sealed record TrainerContactedIntegrationEvent(
    Guid TrainerId,
    Guid? TrainingId,
    string SenderFirstname,
    string SenderLastname,
    string SenderEmailAddress,
    string Message) : IIntegrationEvent;
