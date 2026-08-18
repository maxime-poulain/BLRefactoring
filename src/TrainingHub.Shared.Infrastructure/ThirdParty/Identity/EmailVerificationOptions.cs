namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Where the emailed verification link points.
/// </summary>
/// <remarks>
/// The API composes a link a browser will open, and the origin a browser talks to is the BFF's —
/// an address the API cannot derive from anything it knows about itself, so it is configuration,
/// exactly as <see cref="PasswordResetOptions"/> is for the reset link (ADR 0090). Two options
/// classes rather than one shared "links" section on purpose: the two credentials are separate
/// concepts end to end, and a deployment that moves one page keeps the other's address.
/// </remarks>
public sealed class EmailVerificationOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "EmailVerification";

    /// <summary>
    /// The public origin of the web application, the address the emailed link is built on.
    /// </summary>
    /// <remarks>
    /// Required, with no default on purpose: a host that guessed one would mail visitors a link
    /// into the wrong deployment, and the moment to hear about the gap is start-up rather than
    /// the first registration. The committed Development value names the BFF run from an IDE,
    /// and the compose file overrides it for the containerized stack.
    /// </remarks>
    public string LinkBaseAddress { get; set; } = null!;
}
