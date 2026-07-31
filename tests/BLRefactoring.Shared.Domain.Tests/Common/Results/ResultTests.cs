using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common.Results;

public class ResultTests
{
    [Fact]
    public void Success_Match_CallsOnSuccess()
    {
        var result = Result.Success();

        var matched = result.Match(
            () => "success",
            errors => "failure");

        matched.Should().Be("success");
    }

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

    [Fact]
    public void Failure_Match_CallsOnFailureWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result.Failure(errors);

        var matched = result.Match(
            () => "success",
            e => $"failure: {e.First().ErrorMessage}");

        matched.Should().Be("failure: test error");
    }

    [Fact]
    public void Failure_Bind_PropagatesErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
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

    [Fact]
    public void Failure_Switch_CallsOnFailureAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result.Failure(errors);
        var failureCalled = false;

        result.Switch(
            () => { },
            e => failureCalled = true);

        failureCalled.Should().BeTrue();
    }

    [Fact]
    public async Task MatchAsync_Success_CallsOnSuccessAsync()
    {
        var result = Result.Success();

        var matched = await result.MatchAsync(
            () => ValueTask.FromResult("success"),
            errors => ValueTask.FromResult("failure"));

        matched.Should().Be("success");
    }

    [Fact]
    public async Task MatchAsync_Failure_CallsOnFailureAsync()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result.Failure(errors);

        var matched = await result.MatchAsync(
            () => ValueTask.FromResult("success"),
            e => ValueTask.FromResult("failure"));

        matched.Should().Be("failure");
    }

    [Fact]
    public void FromErrors_WithErrors_ReturnsFailure()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);

        var result = Result.FromErrors(errors);

        result.Match(() => false, _ => true).Should().BeTrue();
    }

    [Fact]
    public void FromErrors_WithoutErrors_ReturnsSuccess()
    {
        var errors = new ErrorCollection();

        var result = Result.FromErrors(errors);

        result.Match(() => true, _ => false).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_EmptyErrorCollection_ReturnsSuccess()
    {
        var errors = new ErrorCollection();

        Result result = errors;

        result.Match(() => true, _ => false).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_NonEmptyErrorCollection_ReturnsFailure()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);

        Result result = errors;

        result.Match(() => false, _ => true).Should().BeTrue();
    }

    [Fact]
    public async Task SuccessAsync_ReturnsSuccess()
    {
        var result = await Result.SuccessAsync();

        result.Match(() => true, _ => false).Should().BeTrue();
    }

    [Fact]
    public async Task FailureAsync_ReturnsFailure()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);

        var result = await Result.FailureAsync(errors);

        result.Match(() => false, _ => true).Should().BeTrue();
    }

    [Fact]
    public void Success_Tap_CallsAction()
    {
        var result = Result.Success();
        var tapCalled = false;

        result.Tap(() => tapCalled = true);

        tapCalled.Should().BeTrue();
    }

    [Fact]
    public void Failure_Tap_DoesNotCallAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result.Failure(errors);
        var tapCalled = false;

        result.Tap(() => tapCalled = true);

        tapCalled.Should().BeFalse();
    }

    [Fact]
    public void Tap_ReturnsSameResult()
    {
        var result = Result.Success();

        var returned = result.Tap(() => { });

        returned.Should().BeSameAs(result);
    }

    [Fact]
    public void Success_TapError_DoesNotCallAction()
    {
        var result = Result.Success();
        var tapErrorCalled = false;

        result.TapError(_ => tapErrorCalled = true);

        tapErrorCalled.Should().BeFalse();
    }

    [Fact]
    public void Failure_TapError_CallsActionWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result.Failure(errors);
        IReadOnlyErrorCollection? capturedErrors = null;

        result.TapError(errs => capturedErrors = errs);

        capturedErrors.Should().NotBeNull();
        capturedErrors!.First().ErrorMessage.Should().Be("test error");
    }

    [Fact]
    public void TapError_ReturnsSameResult()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result.Failure(errors);

        var returned = result.TapError(_ => { });

        returned.Should().BeSameAs(result);
    }
}
