using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using Mediator;

namespace TrainingHub.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;

/// <summary>
/// Implements <see cref="ICommandDispatcher"/> using Mediator.
/// </summary>
public sealed class MediatorCommandDispatcher(IMediator mediator) : ICommandDispatcher
{
    /// <inheritdoc/>
    public ValueTask<TResult> DispatchAsync<TResult>(Shared.CQS.ICommand<TResult> command, CancellationToken cancellationToken = default) where TResult : Result
    {
        return mediator.Send(command, cancellationToken);
    }
}
