namespace TrainingHub.Shared.Infrastructure.Development;

/// <summary>
/// One trainer the dataset asks the seeder to create, with their account, their profile and
/// everything they own.
/// </summary>
/// <remarks>
/// <see cref="Username"/> is the identity of this record across runs, and that is what makes the
/// seeder idempotent: it is derived from the person's name alone, so the same dataset always asks
/// for the same accounts, and a trainer whose account already exists is skipped whole. Nothing is
/// ever deleted to make room for one (ADR 0079).
/// </remarks>
/// <param name="Username">The account name, folded to what Identity's character set admits.</param>
/// <param name="Email">The account address, derived from the username so it is unique too.</param>
/// <param name="Firstname">The given name, accents and all.</param>
/// <param name="Lastname">The family name, accents and all.</param>
/// <param name="Bio">What the trainer says about themselves.</param>
/// <param name="ExpertiseArea">The area they were drawn from. Never shown to a visitor.</param>
/// <param name="PortraitHue">The hue their generated portrait is drawn in, or <see langword="null"/> when they publish none.</param>
/// <param name="SuspensionReason">Why the administration suspended them, or <see langword="null"/>.</param>
/// <param name="Trainings">Everything they own, between one and five of them.</param>
public sealed record SeededTrainer(
    string Username,
    string Email,
    string Firstname,
    string Lastname,
    string Bio,
    string ExpertiseArea,
    int? PortraitHue,
    string? SuspensionReason,
    IReadOnlyList<SeededTraining> Trainings);
