namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents an event publisher that is responsible for
/// dispatching and publishing <see cref="IDomainEvent"/>
/// of entities implementing the <see cref="IHasDomainEvents"/> interface.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Asynchronously publishes a collection of <see cref="IDomainEvent"/>.
    /// </summary>
    /// <param name="havingDomainEvents">A collection of <see cref="IDomainEvent"/> to be published.</param>
    /// <param name="cancellationToken">A CancellationToken used to signal cancellation of the operation.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task PublishAsync(IHasDomainEvents[] havingDomainEvents, CancellationToken cancellationToken);
}
