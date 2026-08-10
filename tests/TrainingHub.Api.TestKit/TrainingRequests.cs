using TrainingHub.Shared.Api.Contracts.Trainings;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The valid training request every suite starts from.
/// </summary>
/// <remarks>
/// It existed five times, under five names — <c>CreateValidTrainingRequest</c>, <c>ValidCreation</c>
/// and three separate <c>Creation</c> methods — with the same five field values in each. That is
/// the shape of duplication that survives review: each copy is small, obviously correct, and only
/// wrong once the contract gains a required field and four of the five are found by the compiler
/// while the fifth is found by a reader.
/// </remarks>
public static class TrainingRequests
{
    /// <summary>
    /// A creation request that passes every data annotation and every domain rule.
    /// </summary>
    /// <param name="title">The title, which is the field a test usually needs to vary — it is
    /// what the uniqueness rule is about.</param>
    /// <param name="topics">The topics, for the facts about shelves; one default topic otherwise
    /// (ADR 0069).</param>
    public static CreateTrainingHttpRequest Valid(
        string title = "Valid training title",
        List<string>? topics = null) => new()
        {
            Title = title,
            Description = "A valid training description for integration testing",
            Prerequisites = "Basic programming knowledge required",
            AcquiredSkills = "Advanced design patterns mastery",
            Topics = topics ?? ["Programming"]
        };

    /// <summary>
    /// An edition request that passes every data annotation and every domain rule.
    /// </summary>
    public static EditTrainingHttpRequest ValidEdition(string title = "Edited training title") => new()
    {
        Title = title,
        Description = "An edited training description for integration testing",
        Prerequisites = "Basic programming knowledge required",
        AcquiredSkills = "Advanced design patterns mastery",
        Topics = ["Programming"]
    };
}
