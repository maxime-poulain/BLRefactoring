using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Tests.Helpers;

/// <summary>
/// Result assertion extensions.
/// </summary>
public static class ResultAssertionExtensions
{
    /// <summary>
    /// Should be success.
    /// </summary>
    public static void ShouldBeSuccess(this Result result)
    {
        result.Match(
            () => true,
            errors =>
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.ToString()));
                throw new Xunit.Sdk.XunitException(
                    $"Expected Result to be Success, but was Failure with errors: {errorMessages}");
            });
    }

    /// <summary>
    /// Should be failure.
    /// </summary>
    public static IReadOnlyErrorCollection ShouldBeFailure(this Result result)
    {
        IReadOnlyErrorCollection? captured = null;
        result.Switch(
            () => throw new Xunit.Sdk.XunitException("Expected Result to be Failure, but was Success."),
            errors => captured = errors);
        return captured!;
    }

    /// <summary>
    /// Should contain error.
    /// </summary>
    public static void ShouldContainError(this Result result, ErrorCode code)
    {
        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == code,
            $"Expected errors to contain ErrorCode '{code}'");
    }

    /// <summary>
    /// Should be success.
    /// </summary>
    public static T ShouldBeSuccess<T>(this Result<T> result)
    {
        T? captured = default;
        result.Switch(
            value => captured = value,
            errors =>
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.ToString()));
                throw new Xunit.Sdk.XunitException(
                    $"Expected Result<{typeof(T).Name}> to be Success, but was Failure with errors: {errorMessages}");
            });
        return captured!;
    }

    /// <summary>
    /// Should be failure.
    /// </summary>
    public static IReadOnlyErrorCollection ShouldBeFailure<T>(this Result<T> result)
    {
        IReadOnlyErrorCollection? captured = null;
        result.Switch(
            _ => throw new Xunit.Sdk.XunitException(
                $"Expected Result<{typeof(T).Name}> to be Failure, but was Success."),
            errors => captured = errors);
        return captured!;
    }

    /// <summary>
    /// Should contain error.
    /// </summary>
    public static void ShouldContainError<T>(this Result<T> result, ErrorCode code)
    {
        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == code,
            $"Expected errors to contain ErrorCode '{code}'");
    }
}
