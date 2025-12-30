using System.Runtime.CompilerServices;
using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Common.Results;

/// <summary>
/// Represents a result of a computation that can either be successful or result in an error.
/// This class encapsulates the Railway Oriented Programming (ROP) pattern, where computations
/// can be chained together, and errors are propagated without interrupting the flow.
/// Unlike the <see cref="Result{TValue}"/> class, this class does not hold a value in case of a successful result.
/// </summary>
public abstract class Result
{
    /// <summary>
    /// Chains computations by applying the provided function.
    /// If the current result is a failure, the computation is skipped, and the error is propagated.
    /// </summary>
    /// <param name="func">A function to apply, producing a new result.</param>
    /// <returns>A new result, which can be either a success or failure, based on the provided function and the current result state.</returns>
    public abstract Result Bind(Func<Result> func);

    /// <summary>
    /// Matches the current result to either a success or failure case, allowing for explicit handling of both outcomes.
    /// This method is central to the ROP pattern, ensuring that both success and error paths are addressed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">A function to handle the success case.</param>
    /// <param name="onFailure">A function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>The result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<IReadOnlyErrorCollection, TResult> onFailure
    );

    /// <summary>
    /// Executes one of the provided actions based on whether the result is a success or a failure.
    /// </summary>
    /// <param name="onSuccess">An action to execute if the result is a success.</param>
    /// <param name="onFailure">An action to execute if the result is a failure, taking the error collection as a parameter.</param>
    public abstract void Switch(Action onSuccess, Action<IReadOnlyErrorCollection> onFailure);

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a successful computation.
    /// </summary>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a successful computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Success() => new SuccessResult();

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a successful computation.
    /// </summary>
    /// <returns>A new instance of the <see cref="ValueTask"/> class that represents a successful computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result> SuccessAsync() => ValueTask.FromResult(Success());

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed computation.
    /// </summary>
    /// <param name="error">The error collection that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(IReadOnlyErrorCollection error) => new FailureResult(error);

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errorCode">The error code that resulted from the failed computation.</param>
    /// <param name="errorMessage">The error message that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(ErrorCode errorCode, string errorMessage)
    {
        var errorCollection = new ErrorCollection([new Error(errorCode, errorMessage)]);
        return Failure(errorCollection);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class from the given error collection.
    /// </summary>
    /// <param name="errors">The error collection.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed computation if there are errors, or a successful computation otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result FromErrors(IReadOnlyErrorCollection errors)
        => errors.HasErrors() ? Failure(errors) : Success();

    /// <summary>
    /// Creates a new instance of the <see cref="ValueTask"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errors">The error collection that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="ValueTask"/> class that represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result> FailureAsync(IReadOnlyErrorCollection errors)
        => ValueTask.FromResult(Failure(errors));

    /// <summary>
    /// Implicitly converts an <see cref="ErrorCollection"/> to a <see cref="Result"/>.
    /// </summary>
    /// <param name="errors">The error collection to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result(ErrorCollection errors) => FromErrors(errors);

    private sealed class SuccessResult : Result
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Match<TResult>(
            Func<TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onSuccess();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Switch(Action onSuccess, Action<IReadOnlyErrorCollection> onFailure)
            => onSuccess();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Result Bind(Func<Result> func)
            => func();
    }

    private sealed class FailureResult(IReadOnlyErrorCollection error) : Result
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Match<TResult>(
            Func<TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onFailure(error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Switch(Action onSuccess, Action<IReadOnlyErrorCollection> onFailure)
            => onFailure(error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Result Bind(Func<Result> func)
            => this;
    }
}
