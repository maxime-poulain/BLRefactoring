using MediatR;

namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Marker interface for queries.
/// </summary>
public interface IQuery : IBaseRequest
{
}

/// <summary>
/// Represents a query that returns data.
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public interface IQuery<out TResponse> : IQuery, IRequest<TResponse>
{
}

/// <summary>
/// Represents the handler for a query that returns data.
/// </summary>
/// <typeparam name="TQuery"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
