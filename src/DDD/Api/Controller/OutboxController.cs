using Microsoft.AspNetCore.Mvc;
using TrainingHub.DDD.Application.Services.OutboxServices;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Outbox;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Errors;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// The outbox's operator surface, on the layered stack: what delivery gave up on, and the one way
/// back in (ADR 0061).
/// </summary>
/// <remarks>
/// The first controller in this API that acts on no aggregate. It is nonetheless administrative in
/// the sense ADR 0051 gives the word — an authority rather than a context — so it shares the
/// administration's base class, its policy and its URL space, and differs only in the route segment
/// that says what is being administered.
/// <para>
/// Two actions, and there is deliberately no third. A poison row is the evidence of a fact this
/// system committed and then failed to tell anybody about; the honest verbs are "try again" and
/// "leave it alone", and a delete would let the mechanism destroy its own crime scene — the
/// argument ADR 0033 already makes about the retention sweep, which removes delivered rows only.
/// </para>
/// </remarks>
[Route("Administration/[controller]")]
public sealed class OutboxController(IOutboxApplicationService outboxApplicationService)
    : AdministrationControllerBase
{
    /// <summary>
    /// Lists the messages whose retry budget is spent and that are still undelivered.
    /// </summary>
    /// <remarks>
    /// Oldest fact first, which is the order the worker would have delivered them in. The payload is
    /// not published: an operator decides on what failed, when and why, and several of these events
    /// carry an address a person is reached at.
    /// </remarks>
    /// <param name="pagination">The page asked for.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with one page of poison messages.
    /// 400 Bad Request when the page is out of range.
    /// </returns>
    [HttpGet("poison")]
    [ProducesResponseType(typeof(PagedHttpResponse<PoisonedMessageHttpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedHttpResponse<PoisonedMessageHttpResponse>>> GetPoisonAsync(
        [FromQuery] PaginationHttpRequest? pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await outboxApplicationService.GetPoisonedAsync(
            pagination.ToPageRequest(), cancellationToken);

        return Ok(page.ToHttp(messages => messages.ToHttp()));
    }

    /// <summary>
    /// Hands one poison message back to the delivery worker.
    /// </summary>
    /// <remarks>
    /// Safe to press twice: the delivery ledger keeps its rows across a requeue, so the retry runs
    /// the consumers still owed and skips the ones that already succeeded (ADR 0034).
    /// </remarks>
    /// <param name="messageId">The message the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the message is back in the pool.
    /// 400 Bad Request when the identifier is empty.
    /// 404 Not Found when no such message exists.
    /// 409 Conflict when the message is still owed or was already delivered.
    /// </returns>
    [HttpPost("poison/{messageId:guid}/requeue")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RequeueAsync(
        [NotEmptyIdentifier] Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var result = await outboxApplicationService.RequeueAsync(messageId, cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == OutboxErrorCodes.NotPoison)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }
}
