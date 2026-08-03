namespace TrainingHub.Blazor.Client.Models;

/// <summary>
/// The topic names the front end offers, mirroring the closed set the domain owns.
/// </summary>
public static class Topic
{
    /// <summary>
    /// The programming topic.
    /// </summary>
    public const string Programming = "Programming";

    /// <summary>
    /// The design topic.
    /// </summary>
    public const string Design = "Design";

    /// <summary>
    /// The marketing topic.
    /// </summary>
    public const string Marketing = "Marketing";

    /// <summary>
    /// The business topic.
    /// </summary>
    public const string Business = "Business";

    /// <summary>
    /// The personal development topic.
    /// </summary>
    public const string PersonalDevelopment = "Personal Development";

    /// <summary>
    /// The leadership topic.
    /// </summary>
    public const string Leadership = "Leadership";

    /// <summary>
    /// Every topic, in the order the front end offers them.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Programming,
        Design,
        Marketing,
        Business,
        PersonalDevelopment,
        Leadership
    ];
}
