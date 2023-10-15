using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Common.Results;

internal static class ResultExtensions
{
    /// <summary>
    /// Returns a value indicating whether this error collection contains any errors.
    /// </summary>
    /// <returns><see langword="true"/> if this error collection contains errors; otherwise, <see langword="false"/>.</returns>
    internal static bool HasErrors(this IReadOnlyErrorCollection errors)
        => errors.Any();
}
