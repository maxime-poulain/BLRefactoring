using AwesomeAssertions;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common.Results;

/// <summary>
/// Behavior covered for <c>ResultOfT</c>.
/// </summary>
public sealed class ResultOfTTests
{
    /// <summary>
    /// Success, match, calls on success with value.
    /// </summary>
    [Fact]
    public void Success_Match_CallsOnSuccessWithValue()
    {
        var result = Result<string>.Success("hello");

        var matched = result.Match(
            value => $"success: {value}",
            errors => "failure");

        matched.Should().Be("success: hello");
    }

    /// <summary>
    /// Success, bind, applies function.
    /// </summary>
    [Fact]
    public void Success_Bind_AppliesFunction()
    {
        var result = Result<string>.Success("hello");

        var bound = result.Bind(value => Result<int>.Success(value.Length));

        bound.Match(
            value => value,
            _ => -1).Should().Be(5);
    }

    /// <summary>
    /// Success, switch, calls on success action with value.
    /// </summary>
    [Fact]
    public void Success_Switch_CallsOnSuccessActionWithValue()
    {
        var result = Result<string>.Success("hello");
        string? capturedValue = null;

        result.Switch(
            value => capturedValue = value,
            errors => { });

        capturedValue.Should().Be("hello");
    }

    /// <summary>
    /// Failure, match, calls on failure with errors.
    /// </summary>
    [Fact]
    public void Failure_Match_CallsOnFailureWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);

        var matched = result.Match(
            value => "success",
            e => $"failure: {e.First().ErrorMessage}");

        matched.Should().Be("failure: test error");
    }

    /// <summary>
    /// Failure, bind, propagates errors.
    /// </summary>
    [Fact]
    public void Failure_Bind_PropagatesErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        var bindCalled = false;

        var bound = result.Bind(value =>
        {
            bindCalled = true;
            return Result<int>.Success(value.Length);
        });

        bindCalled.Should().BeFalse();
        bound.Match(_ => false, propagated => propagated.SequenceEqual(errors)).Should().BeTrue();
    }

    /// <summary>
    /// Failure, switch, calls on failure action.
    /// </summary>
    [Fact]
    public void Failure_Switch_CallsOnFailureAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        var failureCalled = false;

        result.Switch(
            value => { },
            e => failureCalled = true);

        failureCalled.Should().BeTrue();
    }

    /// <summary>
    /// Match async, success, calls on success async.
    /// </summary>
    [Fact]
    public async Task MatchAsync_Success_CallsOnSuccessAsync()
    {
        var result = Result<string>.Success("hello");

        var matched = await result.MatchAsync(
            value => ValueTask.FromResult($"success: {value}"),
            errors => ValueTask.FromResult("failure"));

        matched.Should().Be("success: hello");
    }

    /// <summary>
    /// Match async, failure, calls on failure async.
    /// </summary>
    [Fact]
    public async Task MatchAsync_Failure_CallsOnFailureAsync()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);

        var matched = await result.MatchAsync(
            value => ValueTask.FromResult("success"),
            e => ValueTask.FromResult("failure"));

        matched.Should().Be("failure");
    }

    /// <summary>
    /// Success async, returns completed task.
    /// </summary>
    [Fact]
    public async Task SuccessAsync_ReturnsCompletedTask()
    {
        var result = await Result<string>.SuccessAsync("hello");

        result.Match(
            value => value,
            _ => "failure").Should().Be("hello");
    }

    /// <summary>
    /// Failure from error, contains expected error.
    /// </summary>
    [Fact]
    public void FailureFromError_ContainsExpectedError()
    {
        var error = new Error(ErrorCodes.Unspecified, "specific error");

        var result = Result<string>.Failure(error);

        result.Match(
            value => "success",
            errors => errors.First().ErrorMessage).Should().Be("specific error");
    }

    /// <summary>
    /// Failure from error code, contains expected error code.
    /// </summary>
    [Fact]
    public void FailureFromErrorCode_ContainsExpectedErrorCode()
    {
        var result = Result<string>.Failure(ErrorCodes.NotFound, "not found");

        result.Match<ErrorCode?>(
            value => null,
            errors => errors.First().ErrorCode).Should().Be(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Success, tap, calls action with value.
    /// </summary>
    [Fact]
    public void Success_Tap_CallsActionWithValue()
    {
        var result = Result<string>.Success("hello");
        string? capturedValue = null;

        result.Tap(value => capturedValue = value);

        capturedValue.Should().Be("hello");
    }

    /// <summary>
    /// Failure, tap, does not call action.
    /// </summary>
    [Fact]
    public void Failure_Tap_DoesNotCallAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        var tapCalled = false;

        result.Tap(_ => tapCalled = true);

        tapCalled.Should().BeFalse();
    }

    /// <summary>
    /// Tap, returns same result.
    /// </summary>
    [Fact]
    public void Tap_ReturnsSameResult()
    {
        var result = Result<string>.Success("hello");

        var returned = result.Tap(_ => { });

        returned.Should().BeSameAs(result);
    }

    /// <summary>
    /// Success, tap error, does not call action.
    /// </summary>
    [Fact]
    public void Success_TapError_DoesNotCallAction()
    {
        var result = Result<string>.Success("hello");
        var tapErrorCalled = false;

        result.TapError(_ => tapErrorCalled = true);

        tapErrorCalled.Should().BeFalse();
    }

    /// <summary>
    /// Failure, tap error, calls action with errors.
    /// </summary>
    [Fact]
    public void Failure_TapError_CallsActionWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        IReadOnlyErrorCollection? capturedErrors = null;

        result.TapError(errs => capturedErrors = errs);

        capturedErrors.Should().NotBeNull();
        capturedErrors!.First().ErrorMessage.Should().Be("test error");
    }

    /// <summary>
    /// Tap error, returns same result.
    /// </summary>
    [Fact]
    public void TapError_ReturnsSameResult()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);

        var returned = result.TapError(_ => { });

        returned.Should().BeSameAs(result);
    }
}
