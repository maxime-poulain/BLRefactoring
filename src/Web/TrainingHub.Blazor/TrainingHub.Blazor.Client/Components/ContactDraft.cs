namespace TrainingHub.Blazor.Client.Components;

/// <summary>
/// What the contact dialog answers: everything the visitor wrote, honeypot included.
/// </summary>
/// <remarks>
/// A client-side shape rather than the HTTP contract itself, because the dialog does not know
/// which page opened it: the training that prompted the message — when there is one — is the
/// page's knowledge, and it is added where the request is built (ADR 0082).
/// </remarks>
/// <param name="Firstname">The visitor's first name, trimmed.</param>
/// <param name="Lastname">The visitor's last name, trimmed.</param>
/// <param name="EmailAddress">Where the visitor asks to be answered, trimmed.</param>
/// <param name="Message">What they wrote, trimmed.</param>
/// <param name="Website">
/// The honeypot, exactly as submitted: <see langword="null"/> for a person, because a person never
/// sees the field. Untrimmed on purpose — what a script typed is evidence, not text.
/// </param>
/// <param name="TurnstileToken">
/// The token the Turnstile widget minted when the visitor passed its check, or
/// <see langword="null"/> when the BFF served no site key — the challenge is off, and there was
/// no widget to mint one (ADR 0083). With the challenge on, the dialog refuses to close without a
/// token; the page deposits whatever this carries for the request to spend as a header.
/// </param>
public sealed record ContactDraft(
    string Firstname,
    string Lastname,
    string EmailAddress,
    string Message,
    string? Website,
    string? TurnstileToken);
