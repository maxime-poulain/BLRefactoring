using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Common.Results;


/// <summary>
/// Represents a result of a computation that can either be successful or result in an error.
/// This class encapsulates the Railway Oriented Programming (ROP) pattern, where computations
/// can be chained together, and errors are propagated without interrupting the flow.
/// </summary>
/// <typeparam name="TValue">The type of the value in case of a successful result.</typeparam>
public abstract class Result<TValue>
{
    /// <summary>
    /// Matches the current result to either a success or failure case, allowing for explicit handling of both outcomes.
    /// This method is central to the ROP pattern, ensuring that both success and error paths are addressed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">A function to handle the success case, taking the successful value as a parameter.</param>
    /// <param name="onFailure">A function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>The result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<IReadOnlyErrorCollection, TResult> onFailure
    );

    /// <summary>
    /// Matches the current result to either a success or failure case, allowing for explicit handling of both outcomes.
    /// This method is central to the ROP pattern, ensuring that both success and error paths are addressed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">A function to handle the success case, taking the successful value as a parameter.</param>
    /// <param name="onFailure">A function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>A Task whose result is the result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract Task<TResult> MatchAsync<TResult>(
        Func<TValue, Task<TResult>> onSuccess,
        Func<IReadOnlyErrorCollection, Task<TResult>> onFailure);

    /// <summary>
    /// Transforms the value inside a success result using the provided function.
    /// If the current result is a failure, the transformation is skipped, and the error is propagated.
    /// </summary>
    /// <typeparam name="TNewValue">The type of the new value after the transformation.</typeparam>
    /// <param name="func">A function to transform the successful value.</param>
    /// <returns>A new result containing either the transformed value or the original error.</returns>
    public abstract Result<TNewValue> Map<TNewValue>(Func<TValue, TNewValue> func);

    /// <summary>
    /// Chains computations by applying the provided function to the successful value.
    /// If the current result is a failure, the computation is skipped, and the error is propagated.
    /// </summary>
    /// <typeparam name="TNewValue">The type of the result of the new computation.</typeparam>
    /// <param name="func">A function to apply to the successful value, producing a new result.</param>
    /// <returns>A new result, which can be either a success or failure, based on the provided function and the current result state.</returns>
    public abstract Result<TNewValue> Bind<TNewValue>(Func<TValue, Result<TNewValue>> func);

    /// <summary>
    /// Chains computations by applying the provided function to the successful value.
    /// If the current result is a failure, the computation is skipped, and the error is propagated.
    /// </summary>
    /// <typeparam name="TNewValue">The type of the result of the new computation.</typeparam>
    /// <param name="func">A function to apply to the successful value, producing a new result.</param>
    /// <returns>A <see cref="Task"/> whose result contains new result, which can be either a success or failure, based on the provided function and the current result state.</returns>
    public abstract Task<Result<TNewValue>> BindAsync<TNewValue>(Func<TValue, Task<Result<TNewValue>>> func);


    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a successful computation.
    /// </summary>
    /// <typeparam name="T">The type of the successful result value.</typeparam>
    /// <param name="value">The value that resulted from the successful computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a successful computation.</returns>
    public static Result<T> Success<T>(T value) => new Result<T>.SuccessResult(value);

    /// <summary>
    /// Creates a new instance of the <see cref="Task"/> class that represents a successful computation.
    /// </summary>
    /// <typeparam name="T">The type of the successful result value.</typeparam>
    /// <param name="value">The value that resulted from the successful computation.</param>
    /// <returns>A new instance of the <see cref="Task"/> class that represents a successful computation.</returns>
    public static Task<Result<T>> SuccessAsync<T>(T value) => Task.FromResult(Success(value));

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.
    /// </summary>
    /// <param name="error">The error collection that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.</returns>
    public static Result<TValue> Failure(IReadOnlyErrorCollection error) => new FailureResult(error);

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.
    /// </summary>
    /// <param name="error">The error that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.</returns>
    public static Result<TValue> Failure(Error error) => new FailureResult(error);

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errorCode">The error code that resulted from the failed computation.</param>
    /// <param name="errorMessage">The error message that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.</returns>
    public static Result<TValue> Failure(ErrorCode errorCode, string errorMessage)
        => Failure(new Error(errorCode, errorMessage));

    /// <summary>
    /// Creates a new instance of the <see cref="Task"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errors">The error collection that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Task"/> class that represents a failed computation.</returns>
    public static Task<Result<TValue>> FailureAsync(IReadOnlyErrorCollection errors) =>
        Task.FromResult(Failure(errors));

    /// <summary>
    /// Creates a new instance of the <see cref="Task"/> class that represents a failed computation.
    /// </summary>
    /// <param name="error">The error that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Task"/> class that represents a failed computation.</returns>
    public static Task<Result<TValue>> FailureAsync(Error error) => Task.FromResult(Failure(error));

    /// <summary>
    /// Creates a new instance of the <see cref="Task"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errorCode">The error code that resulted from the failed computation.</param>
    /// <param name="errorMessage">The error message that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Task"/> class that represents a failed computation.</returns>
    public static Task<Result<TValue>> FailureAsync(ErrorCode errorCode, string errorMessage) =>
        Task.FromResult(Failure(new Error(errorCode, errorMessage)));

    private sealed class SuccessResult : Result<TValue>
    {
        private readonly TValue _value;

        public SuccessResult(TValue value)
        {
            _value = value;
        }

        public override TResult Match<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onSuccess(_value);

        public override Task<TResult> MatchAsync<TResult>(
            Func<TValue, Task<TResult>> onSuccess,
            Func<IReadOnlyErrorCollection, Task<TResult>> onFailure
        ) => onSuccess(_value);

        public override Result<TNewValue> Map<TNewValue>(Func<TValue, TNewValue> func)
            => Result<TNewValue>.Success(func(_value));

        public override Result<TNewValue> Bind<TNewValue>(Func<TValue, Result<TNewValue>> func)
            => func(_value);

        public override Task<Result<TNewValue>> BindAsync<TNewValue>(Func<TValue, Task<Result<TNewValue>>> func)
        {
            return func(_value);
        }
    }

    private sealed class FailureResult : Result<TValue>
    {
        private readonly IReadOnlyErrorCollection _error;

        public FailureResult(Error error)
        {
            _error = new ErrorCollection(new[] { error });
        }

        public FailureResult(IReadOnlyErrorCollection error)
        {
            _error = error;
        }

        public override TResult Match<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onFailure(_error);

        public override Result<TNewValue> Map<TNewValue>(Func<TValue, TNewValue> func)
            => Result<TNewValue>.Failure(_error);

        public override Result<TNewValue> Bind<TNewValue>(Func<TValue, Result<TNewValue>> func)
            => Result<TNewValue>.Failure(_error);

        public override Task<Result<TNewValue>> BindAsync<TNewValue>(Func<TValue, Task<Result<TNewValue>>> func)
            => Task.FromResult(Result<TNewValue>.Failure(_error));

        public override Task<TResult> MatchAsync<TResult>(
            Func<TValue, Task<TResult>> onSuccess,
            Func<IReadOnlyErrorCollection, Task<TResult>> onFailure)
            => onFailure(_error);
    }
}
