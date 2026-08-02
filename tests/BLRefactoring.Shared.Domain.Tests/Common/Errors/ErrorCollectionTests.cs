using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common.Errors;

public sealed class ErrorCollectionTests
{
    [Fact]
    public void NewErrorCollection_IsEmpty()
    {
        var collection = new ErrorCollection();

        collection.Count().Should().Be(0);
    }

    [Fact]
    public void Add_Error_IncreasesCount()
    {
        var collection = new ErrorCollection();
        var error = new Error(ErrorCodes.Unspecified, "test error");

        collection.Add(error);

        collection.Count().Should().Be(1);
    }

    [Fact]
    public void Add_ErrorCodeAndMessage_CreatesAndAddsError()
    {
        var collection = new ErrorCollection();

        collection.Add(ErrorCodes.Unspecified, "test error");

        collection.Count().Should().Be(1);
        collection.First().ErrorCode.Should().Be(ErrorCodes.Unspecified);
        collection.First().ErrorMessage.Should().Be("test error");
    }

    [Fact]
    public void AddErrors_Collection_AddsAllErrors()
    {
        var collection = new ErrorCollection();
        var errors = new List<Error>
        {
            new(ErrorCodes.Unspecified, "error 1"),
            new(ErrorCodes.NotFound, "error 2")
        };

        collection.AddErrors(errors);

        collection.Count().Should().Be(2);
    }

    [Fact]
    public void AddErrors_NullCollection_ThrowsArgumentNullException()
    {
        var collection = new ErrorCollection();

        var act = () => collection.AddErrors((IEnumerable<Error>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddErrors_FromFailureResult_AddsErrors()
    {
        var collection = new ErrorCollection();
        var errors = new ErrorCollection([new Error(ErrorCodes.Unspecified, "failure error")]);
        var failureResult = Result.Failure(errors);

        collection.AddErrors(failureResult);

        collection.Count().Should().Be(1);
        collection.First().ErrorMessage.Should().Be("failure error");
    }

    [Fact]
    public void AddErrors_FromSuccessResult_DoesNotAddErrors()
    {
        var collection = new ErrorCollection();
        var successResult = Result.Success();

        collection.AddErrors(successResult);

        collection.Count().Should().Be(0);
    }

    [Fact]
    public void ToResult_WithErrors_ReturnsFailure()
    {
        var collection = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);

        var result = collection.ToResult();

        result.ShouldBeFailure();
    }

    [Fact]
    public void ToResult_WithoutErrors_ReturnsSuccess()
    {
        var collection = new ErrorCollection();

        var result = collection.ToResult();

        result.ShouldBeSuccess();
    }

    [Fact]
    public void Match_WithErrors_CallsOnFailure()
    {
        var collection = new ErrorCollection([new Error(ErrorCodes.Unspecified, "test error")]);

        var matched = collection.Match(
            () => "success",
            errors => "failure");

        matched.Should().Be("failure");
    }

    [Fact]
    public void Match_WithoutErrors_CallsOnSuccess()
    {
        var collection = new ErrorCollection();

        var matched = collection.Match(
            () => "success",
            errors => "failure");

        matched.Should().Be("success");
    }

    [Fact]
    public void Indexer_Get_ReturnsCorrectError()
    {
        var error1 = new Error(ErrorCodes.Unspecified, "error 1");
        var error2 = new Error(ErrorCodes.NotFound, "error 2");
        var collection = new ErrorCollection([error1, error2]);

        collection[0].Should().Be(error1);
        collection[1].Should().Be(error2);
    }

    [Fact]
    public void Enumerator_IteratesAllErrors()
    {
        var error1 = new Error(ErrorCodes.Unspecified, "error 1");
        var error2 = new Error(ErrorCodes.NotFound, "error 2");
        var collection = new ErrorCollection([error1, error2]);

        var enumerated = new List<Error>();
        foreach (var error in collection)
        {
            enumerated.Add(error);
        }

        enumerated.Should().HaveCount(2);
        enumerated.Should().Contain(error1);
        enumerated.Should().Contain(error2);
    }
}
