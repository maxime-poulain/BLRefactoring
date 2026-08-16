namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Where the emailed reset link points.
/// </summary>
/// <remarks>
/// The API composes a link a browser will open, and the origin a browser talks to is the BFF's —
/// an address the API cannot derive from anything it knows about itself, so it is configuration,
/// like the relay mail leaves through (ADR 0084). It lives beside the store that mints the token
/// rather than in the application layer, in the mold of <c>SmtpOptions</c>: the consumer that
/// writes the email receives a finished link through the port and holds no options of its own.
/// </remarks>
public sealed class PasswordResetOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "PasswordReset";

    /// <summary>
    /// The public origin of the web application, the address the emailed link is built on.
    /// </summary>
    /// <remarks>
    /// Required, with no default on purpose: a host that guessed one would mail visitors a link
    /// into the wrong deployment, and the moment to hear about the gap is start-up rather than
    /// the first forgotten password. The committed Development value names the BFF run from an
    /// IDE, and the compose file overrides it for the containerized stack.
    /// </remarks>
    public string LinkBaseAddress { get; set; } = null!;
}
