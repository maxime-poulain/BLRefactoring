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
/// <typeparam name="TResult"></typeparam>
public interface IQuery<out TResult> : IQuery, IRequest<TResult>
{
}

/// <summary>
/// Represents the handler for a query that returns data.
/// </summary>
/// <typeparam name="TQuery"></typeparam>
/// <typeparam name="TResult"></typeparam>
public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
}
