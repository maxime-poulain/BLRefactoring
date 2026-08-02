using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common.Results;

/// <summary>
/// Behaviour covered for <c>Result</c>.
/// </summary>
public sealed class ResultTests
{
    /// <summary>
    /// Success, match, calls on success.
    /// </summary>
    [Fact]
    public void Success_Match_CallsOnSuccess()
    {
        var result = Result.Success();

        var matched = result.Match(
            () => "success",
            errors => "failure");

        matched.Should().Be("success");
    }

    /// <summary>
    /// Success, bind, executes function.
    /// </summary>
    [Fact]
    public void Success_Bind_ExecutesFunction()
    {
        var result = Result.Success();
        var bindCalled = false;

        var bound = result.Bind(() =>
        {
            bindCalled = true;
            return Result.Success();
        });

        bindCalled.Should().BeTrue();
        bound.Match(() => true, _ => false).Should().BeTrue();
    }

    /// <summary>
    /// Success, switch, calls on success action.
    /// </summary>
    [Fact]
    public void Success_Switch_CallsOnSuccessAction()
    {
        var result = Result.Success();
        var successCalled = false;

        result.Switch(
            () => successCalled = true,
            errors => { });

        successCalled.Should().BeTrue();
    }

    /// <summary>
    /// Failure, match, calls on failure with errors.
    /// </summary>
    [Fact]
    public void Failure_Match_CallsOnFailureWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result.Failure(errors);

        var matched = result.Match(
            () => "success",
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
        var result = Result.Failure(errors);
        var bindCalled = false;

        var bound = result.Bind(() =>
        {
            bindCalled = true;
            return Result.Success();
        });

        bindCalled.Should().BeFalse();
        bound.Match(() => false, _ => true).Should().BeTrue();
    }

    /// <summary>
    /// Failure, switch, calls on failure action.
    /// </summary>
    [Fact]
    public void Failure_Switch_CallsOnFailureAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result.Failure(errors);
        var failureCalled = false;

        result.Switch(
            () => { },
            e => failureCalled = true);

        failureCalled.Should().BeTrue();
    }

    /// <summary>
    /// Match async, success, calls on success async.
    /// </summary>
    [Fact]
    public async Task MatchAsync_Success_CallsOnSuccessAsync()
    {
        var result = Result.Success();

        var matched = await result.MatchAsync(
            () => ValueTask.FromResult("success"),
            errors => ValueTask.FromResult("failure"));

        matched.Should().Be("success");
    }

    /// <summary>
    /// Match async, failure, calls on failure async.
    /// </summary>
    [Fact]
    public async Task MatchAsync_Failure_CallsOnFailureAsync()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result.Failure(errors);

        var matched = await result.MatchAsync(
            () => ValueTask.FromResult("success"),
            e => ValueTask.FromResult("failure"));

        matched.Should().Be("failure");
    }

    /// <summary>
    /// From errors, with errors, returns failure.
    /// </summary>
    [Fact]
    public void FromErrors_WithErrors_ReturnsFailure()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);

        var result = Result.FromErrors(errors);

        result.Match(() => false, _ => true).Should().BeTrue();
    }

    /// <summary>
    /// From errors, without errors, returns success.
    /// </summary>
    [Fact]
    public void FromErrors_WithoutErrors_ReturnsSuccess()
    {
        var errors = new ErrorCollection();

        var result = Result.FromErrors(errors);

        result.Match(() => true, _ => false).Should().BeTrue();
    }

    /// <summary>
    /// Implicit conversion, empty error collection, returns success.
    /// </summary>
    [Fact]
    public void ImplicitConversion_EmptyErrorCollection_ReturnsSuccess()
    {
        var errors = new ErrorCollection();

        Result result = errors;

        result.Match(() => true, _ => false).Should().BeTrue();
    }

    /// <summary>
    /// Implicit conversion, non empty error collection, returns failure.
    /// </summary>
    [Fact]
    public void ImplicitConversion_NonEmptyErrorCollection_ReturnsFailure()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);

        Result result = errors;

        result.Match(() => false, _ => true).Should().BeTrue();
    }

    /// <summary>
    /// Success async, returns success.
    /// </summary>
    [Fact]
    public async Task SuccessAsync_ReturnsSuccess()
    {
        var result = await Result.SuccessAsync();

        result.Match(() => true, _ => false).Should().BeTrue();
    }

    /// <summary>
    /// Failure async, returns failure.
    /// </summary>
    [Fact]
    public async Task FailureAsync_ReturnsFailure()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);

        var result = await Result.FailureAsync(errors);

        result.Match(() => false, _ => true).Should().BeTrue();
    }

    /// <summary>
    /// Success, tap, calls action.
    /// </summary>
    [Fact]
    public void Success_Tap_CallsAction()
    {
        var result = Result.Success();
        var tapCalled = false;

        result.Tap(() => tapCalled = true);

        tapCalled.Should().BeTrue();
    }

    /// <summary>
    /// Failure, tap, does not call action.
    /// </summary>
    [Fact]
    public void Failure_Tap_DoesNotCallAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);
        var result = Result.Failure(errors);
        var tapCalled = false;

        result.Tap(() => tapCalled = true);

        tapCalled.Should().BeFalse();
    }

    /// <summary>
    /// Tap, returns same result.
    /// </summary>
    [Fact]
    public void Tap_ReturnsSameResult()
    {
        var result = Result.Success();

        var returned = result.Tap(() => { });

        returned.Should().BeSameAs(result);
    }

    /// <summary>
    /// Success, tap error, does not call action.
    /// </summary>
    [Fact]
    public void Success_TapError_DoesNotCallAction()
    {
        var result = Result.Success();
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
        var result = Result.Failure(errors);
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
        var result = Result.Failure(errors);

        var returned = result.TapError(_ => { });

        returned.Should().BeSameAs(result);
    }
}
