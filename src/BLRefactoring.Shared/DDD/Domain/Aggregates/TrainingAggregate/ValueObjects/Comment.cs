using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

public class Comment : ValueObject
{
    public string Content { get; }

    private Comment(string content)
    {
        Content = content;
    }

    public static Result<Comment> Create(string content)
    {
        var errors = new ErrorCollection();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(ErrorCode.Unspecified, "Comment content must be provided");
        }

        if (content is not { Length: >= 5 and <= 100 })
        {
            errors.Add(ErrorCode.Unspecified, "Comment length must be between 5 and 100 characters");
        }

        if (errors.HasErrors())
        {
            return Result<Comment>.Failure(errors);
        }

        return Result<Comment>.Success(new Comment(content));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Content;
    }
}
