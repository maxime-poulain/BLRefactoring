using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

public class Rate : ValueObject
{
    public int Value { get; }
    public Comment Comment { get; }
    public Guid AuthorId { get; }

    private Rate() { } // Private constructor for ORM or serialization

    private Rate(int value, Comment comment, Guid authorId)
    {
        Value = value;
        Comment = comment;
        AuthorId = authorId;
    }

    public static Result<Rate> Create(int value, Comment comment, Guid authorId)
    {
        var errors = new ErrorCollection();
        if (value is < 0 or > 5)
        {
            errors.Add(ErrorCode.Unspecified, "Rate value must be between 0 and 5");
        }

        if (authorId == Guid.Empty)
        {
            errors.Add(ErrorCode.Unspecified, "Author id must be provided");
        }

        if (errors.HasErrors())
        {
            return Result<Rate>.Failure(errors);
        }

        return Result<Rate>.Success(new Rate(value, comment, authorId));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Comment;
        yield return AuthorId;
    }
}
