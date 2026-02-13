namespace BLRefactoring.Blazor.Client.Models;

public static class Topic
{
    public const string Programming = "Programming";
    public const string Design = "Design";
    public const string Marketing = "Marketing";
    public const string Business = "Business";
    public const string PersonalDevelopment = "Personal Development";
    public const string Leadership = "Leadership";

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
