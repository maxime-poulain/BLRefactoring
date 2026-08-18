using TrainingHub.DDDWithCqrs.Api.Mappings;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Erase;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace TrainingHub.DDDWithCqrs.Api.Controller;

/// <summary>
/// Account endpoints of the CQRS stack. The registration, login, recovery and erasure flows
/// live in <see cref="AuthControllerBase"/>; this controller only supplies how the trainer is
/// created and erased, through the command dispatcher.
/// </summary>
public sealed class AuthController(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager,
    ITokenService tokenService,
    IPasswordRecoveryService passwordRecovery,
    IEmailVerificationService emailVerification,
    IIntegrationEventPublisher integrationEventPublisher,
    ICommandDispatcher commandDispatcher)
    : AuthControllerBase(
        userManager, signInManager, tokenService, passwordRecovery, emailVerification, integrationEventPublisher)
{
    /// <inheritdoc />
    /// <remarks>
    /// The command reports success and nothing more, so the identifier comes from the command
    /// itself — it generates one when it is built, exactly as <c>CreateTrainingCommand</c> does for
    /// the identifier that endpoint publishes in <c>Location</c>.
    /// </remarks>
    protected override async Task<Result<Guid>> CreateTrainerAsync(
        RegisterHttpRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var command = request.ToCommand(userId);

        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);

        return result.Match<Result<Guid>>(
            () => Result<Guid>.Success(command.TrainerId),
            Result<Guid>.Failure);
    }

    /// <inheritdoc />
    protected override async Task<Result> EraseTrainerAsync(CancellationToken cancellationToken = default) =>
        await commandDispatcher.DispatchAsync(new EraseCurrentTrainerCommand(), cancellationToken);
}
