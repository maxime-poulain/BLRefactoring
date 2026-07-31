using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common.Results;

public class ResultOfTTests
{
    [Fact]
    public void Success_Match_CallsOnSuccessWithValue()
    {
        var result = Result<string>.Success("hello");

        var matched = result.Match(
            value => $"success: {value}",
            errors => "failure");

        matched.Should().Be("success: hello");
    }

    [Fact]
    public void Success_Bind_AppliesFunction()
    {
        var result = Result<string>.Success("hello");

        var bound = result.Bind(value => Result<int>.Success(value.Length));

        bound.Match(
            value => value,
            _ => -1).Should().Be(5);
    }

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

    [Fact]
    public void Failure_Match_CallsOnFailureWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);

        var matched = result.Match(
            value => "success",
            e => $"failure: {e.First().ErrorMessage}");

        matched.Should().Be("failure: test error");
    }

    [Fact]
    public void Failure_Bind_PropagatesErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
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

    [Fact]
    public void Failure_Switch_CallsOnFailureAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        var failureCalled = false;

        result.Switch(
            value => { },
            e => failureCalled = true);

        failureCalled.Should().BeTrue();
    }

    [Fact]
    public async Task MatchAsync_Success_CallsOnSuccessAsync()
    {
        var result = Result<string>.Success("hello");

        var matched = await result.MatchAsync(
            value => ValueTask.FromResult($"success: {value}"),
            errors => ValueTask.FromResult("failure"));

        matched.Should().Be("success: hello");
    }

    [Fact]
    public async Task MatchAsync_Failure_CallsOnFailureAsync()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);

        var matched = await result.MatchAsync(
            value => ValueTask.FromResult("success"),
            e => ValueTask.FromResult("failure"));

        matched.Should().Be("failure");
    }

    [Fact]
    public async Task SuccessAsync_ReturnsCompletedTask()
    {
        var result = await Result<string>.SuccessAsync("hello");

        result.Match(
            value => value,
            _ => "failure").Should().Be("hello");
    }

    [Fact]
    public void FailureFromError_ContainsExpectedError()
    {
        var error = new Error(ErrorCode.Unspecified, "specific error");

        var result = Result<string>.Failure(error);

        result.Match(
            value => "success",
            errors => errors.First().ErrorMessage).Should().Be("specific error");
    }

    [Fact]
    public void FailureFromErrorCode_ContainsExpectedErrorCode()
    {
        var result = Result<string>.Failure(ErrorCode.NotFound, "not found");

        result.Match(
            value => (ErrorCode?)null,
            errors => errors.First().ErrorCode).Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public void Success_Tap_CallsActionWithValue()
    {
        var result = Result<string>.Success("hello");
        string? capturedValue = null;

        result.Tap(value => capturedValue = value);

        capturedValue.Should().Be("hello");
    }

    [Fact]
    public void Failure_Tap_DoesNotCallAction()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        var tapCalled = false;

        result.Tap(_ => tapCalled = true);

        tapCalled.Should().BeFalse();
    }

    [Fact]
    public void Tap_ReturnsSameResult()
    {
        var result = Result<string>.Success("hello");

        var returned = result.Tap(_ => { });

        returned.Should().BeSameAs(result);
    }

    [Fact]
    public void Success_TapError_DoesNotCallAction()
    {
        var result = Result<string>.Success("hello");
        var tapErrorCalled = false;

        result.TapError(_ => tapErrorCalled = true);

        tapErrorCalled.Should().BeFalse();
    }

    [Fact]
    public void Failure_TapError_CallsActionWithErrors()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);
        IReadOnlyErrorCollection? capturedErrors = null;

        result.TapError(errs => capturedErrors = errs);

        capturedErrors.Should().NotBeNull();
        capturedErrors!.First().ErrorMessage.Should().Be("test error");
    }

    [Fact]
    public void TapError_ReturnsSameResult()
    {
        var errors = new ErrorCollection([new Error(ErrorCode.Unspecified, "test error")]);
        var result = Result<string>.Failure(errors);

        var returned = result.TapError(_ => { });

        returned.Should().BeSameAs(result);
    }
}
