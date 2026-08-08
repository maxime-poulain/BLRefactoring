using TrainingHub.DDD.Application.Services.TrainerServices;
using TrainingHub.DDD.Application.Services.TrainingServices;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// The administration's four decisions, on the layered stack.
/// </summary>
/// <remarks>
/// One controller for two aggregates, because what these four actions share is the authority
/// exercised rather than the resource acted on — which is what ADR 0051 says an administrator is.
/// The sub-resources are spelled out in the routes rather than in the controller name, so
/// <c>/Administration/trainers/{id}/suspend</c> still reads as an act on a trainer.
/// <para>
/// It is the one controller of this host injecting two application services, and that follows from
/// the same grouping: the layered stack names its services after the aggregate they drive, and
/// these four decisions cross two.
/// </para>
/// </remarks>
public sealed class AdministrationController(
    ITrainerApplicationService trainerApplicationService,
    ITrainingApplicationService trainingApplicationService)
    : AdministrationControllerBase
{
    /// <summary>
    /// Places a trainer under sanction: their catalogue leaves public view and cannot grow.
    /// </summary>
    /// <param name="trainerId">The trainer the route names.</param>
    /// <param name="request">The body carrying the reason.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the trainer is under sanction.
    /// 400 Bad Request when the reason is empty or too long.
    /// 404 Not Found when no such trainer exists.
    /// 409 Conflict when the trainer was already suspended.
    /// </returns>
    [HttpPost("trainers/{trainerId:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SuspendTrainerAsync(
        [NotEmptyIdentifier] Guid trainerId,
        [FromBody] SuspendTrainerHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await trainerApplicationService.SuspendAsync(trainerId, request.Reason, cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainerErrorCodes.AlreadySuspended)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Lifts a trainer's sanction: their catalogue returns exactly as they left it.
    /// </summary>
    /// <param name="trainerId">The trainer the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the sanction is lifted.
    /// 400 Bad Request on validation errors.
    /// 404 Not Found when no such trainer exists.
    /// 409 Conflict when the trainer was not under sanction.
    /// </returns>
    [HttpPost("trainers/{trainerId:guid}/reinstate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ReinstateTrainerAsync(
        [NotEmptyIdentifier] Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        var result = await trainerApplicationService.ReinstateAsync(trainerId, cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainerErrorCodes.NotSuspended)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Takes a training out of public view, where its owner cannot put it back (ADR 0052).
    /// </summary>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="request">The body carrying the reason.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the training is withheld.
    /// 400 Bad Request when the reason is empty or too long.
    /// 404 Not Found when no such training exists.
    /// 409 Conflict when the training was already withheld.
    /// </returns>
    [HttpPost("trainings/{trainingId:guid}/withhold")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> WithholdTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        [FromBody] WithholdTrainingHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await trainingApplicationService.WithholdAsync(trainingId, request.Reason, cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainingErrorCodes.AlreadyWithheld)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Lifts the interdiction on a withheld training, which lands on unpublished (ADR 0052).
    /// </summary>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the interdiction is lifted.
    /// 400 Bad Request on validation errors.
    /// 404 Not Found when no such training exists.
    /// 409 Conflict when the training was not withheld.
    /// </returns>
    [HttpPost("trainings/{trainingId:guid}/release")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ReleaseTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.ReleaseAsync(trainingId, cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainingErrorCodes.NotWithheld)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }
}
