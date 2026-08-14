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
    /// The software architecture topic.
    /// </summary>
    public const string SoftwareArchitecture = "Software Architecture";

    /// <summary>
    /// The cloud computing topic.
    /// </summary>
    public const string CloudComputing = "Cloud Computing";

    /// <summary>
    /// The DevOps topic.
    /// </summary>
    public const string DevOps = "DevOps";

    /// <summary>
    /// The databases topic.
    /// </summary>
    public const string Databases = "Databases";

    /// <summary>
    /// The security topic.
    /// </summary>
    public const string Security = "Security";

    /// <summary>
    /// The web development topic.
    /// </summary>
    public const string WebDevelopment = "Web Development";

    /// <summary>
    /// The data and analytics topic.
    /// </summary>
    public const string DataAndAnalytics = "Data and Analytics";

    /// <summary>
    /// The testing and quality topic.
    /// </summary>
    public const string TestingAndQuality = "Testing and Quality";

    /// <summary>
    /// The project management topic.
    /// </summary>
    public const string ProjectManagement = "Project Management";

    /// <summary>
    /// The agile practices topic.
    /// </summary>
    public const string AgilePractices = "Agile Practices";

    /// <summary>
    /// Every topic, in the order the front end offers them.
    /// </summary>
    /// <remarks>
    /// A copy of a set this project cannot see: the browser references the generated client and
    /// never the domain. What keeps it honest is an architecture rule that can see both — a picker
    /// short by one subject is a subject nobody can ever file a training under, and no build, no
    /// request and no screen would say so.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } =
    [
        Programming,
        Design,
        Marketing,
        Business,
        PersonalDevelopment,
        Leadership,
        SoftwareArchitecture,
        CloudComputing,
        DevOps,
        Databases,
        Security,
        WebDevelopment,
        DataAndAnalytics,
        TestingAndQuality,
        ProjectManagement,
        AgilePractices
    ];
}
