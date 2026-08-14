using System.Net;
using System.Text;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TrainingHub.Blazor.Client.Components;
using TrainingHub.Blazor.Client.Tests.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Components;

/// <summary>
/// Behavior covered for the dialog both public pages ask a visitor's message through.
/// </summary>
/// <remarks>
/// What is pinned here is the pair of answers this component may give — a draft somebody wrote, or
/// nothing at all — the honeypot's whole design: present, invisible, and carried verbatim when
/// something fills it (ADR 0082) — and the Turnstile widget's: rendered with the key the BFF
/// serves, and refusing to close without the token it mints (ADR 0083). Rendered through
/// <see cref="MudDialogProvider"/> rather than on its own, because that is the only place a
/// MudBlazor dialog exists. The pages that open it substitute the service instead, and pin the
/// other half: that what comes back out of here is what reaches the API.
/// </remarks>
public sealed class ContactTrainerDialogTests : ComponentTest
{
    private const string TrainerName = "Ada Lovelace";

    private const string Verb = "Send";

    private const string SiteKey = "site-key-from-the-bff";

    private const string MintedToken = "token-the-widget-minted";

    private readonly StubHttpClientFactory _bff;

    private readonly Bunit.JSRuntimeInvocationHandler<string> _getResponse;

    private string? _servedSiteKey = SiteKey;

    /// <summary>
    /// Wires what the widget needs: the BFF answering the site key — replaceable with a null one,
    /// which is the challenge's off state — and the two interop calls the dialog makes:
    /// <c>turnstile.render</c> answering a widget identity, and <c>turnstile.getResponse</c>
    /// answering a minted token a fact can replace with an empty one.
    /// </summary>
    public ContactTrainerDialogTests()
    {
        _bff = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                _servedSiteKey is null
                    ? """{"siteKey":null}"""
                    : $$"""{"siteKey":"{{_servedSiteKey}}"}""",
                Encoding.UTF8,
                "application/json")
        });
        Services.AddSingleton<IHttpClientFactory>(_bff);

        JSInterop.Setup<string>("turnstile.render", _ => true).SetResult("widget-1");
        _getResponse = JSInterop.Setup<string>("turnstile.getResponse", _ => true);
        _getResponse.SetResult(MintedToken);
    }

    /// <summary>
    /// Rendered, nothing written yet, leaves the sending button disabled.
    /// </summary>
    /// <remarks>
    /// The first thing a visitor sees, and the cheapest place to refuse: a dialog that opened
    /// ready to send invites the empty message rather than preventing it.
    /// </remarks>
    [Fact]
    public async Task Rendered_NothingWrittenYet_LeavesTheSendingButtonDisabled()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        Sending(dialog).HasAttribute("disabled").Should().BeTrue();
    }

    /// <summary>
    /// Rendered, says who the message goes to.
    /// </summary>
    /// <remarks>
    /// The requirement this dialog exists under: the address is withheld, so the name is what
    /// tells a visitor their message reaches the right person (ADR 0082). A form that collected
    /// text toward nobody in particular would be the address leak's silent twin — a message sent
    /// blind.
    /// </remarks>
    [Fact]
    public async Task Rendered_SaysWhoTheMessageGoesTo()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        dialog.Markup.Should().Contain(TrainerName);
    }

    /// <summary>
    /// Confirming, everything written, closes with what was typed and nothing around it.
    /// </summary>
    /// <remarks>
    /// The surrounding whitespace goes because the text is quoted to a person in an email, and
    /// because the trimmed text is what the page sends: the contract's bounds are judged on what
    /// travels, so the dialog answers exactly that. The honeypot left alone answers
    /// <see langword="null"/>, which is what the contract's absent value is.
    /// </remarks>
    [Fact]
    public async Task Confirming_EverythingWritten_ClosesWithWhatWasTyped()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();

        WriteVisibleFields(dialog,
            firstname: "  Grace ",
            lastname: " Hopper ",
            email: " grace@example.org ",
            message: "  I would like to book this training for my team.  ");

        // The button follows the fields rather than the keystroke: the form validates on a later
        // render, so this waits for the state the visitor would actually see.
        dialog.WaitForAssertion(() => Sending(dialog).HasAttribute("disabled").Should().BeFalse());

        // Act
        Sending(dialog).Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Canceled.Should().BeFalse();
        result.Data.Should().BeEquivalentTo(new ContactDraft(
            "Grace",
            "Hopper",
            "grace@example.org",
            "I would like to book this training for my team.",
            Website: null,
            MintedToken));
    }

    /// <summary>
    /// Rendered, asks the BFF for the site key and renders the widget with it.
    /// </summary>
    /// <remarks>
    /// The WebAssembly application carries no configuration of its own (ADR 0035), so the key can
    /// only arrive this way: fetched from the BFF's endpoint and handed to
    /// <c>turnstile.render</c> — explicitly, because the loader script scanned the page long
    /// before this dialog existed (ADR 0083).
    /// </remarks>
    [Fact]
    public async Task Rendered_AsksTheBffForTheSiteKey_AndRendersTheWidgetWithIt()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        dialog.WaitForAssertion(() =>
        {
            _bff.Requests.Should().ContainSingle()
                .Which.RequestUri!.AbsolutePath.Should().Be("/bff/turnstile");

            var rendered = JSInterop.VerifyInvoke("turnstile.render");
            rendered.Arguments[1]!.ToString().Should().Contain(SiteKey);
        });
    }

    /// <summary>
    /// Rendered, no site key served, renders no widget and sends without a token.
    /// </summary>
    /// <remarks>
    /// The challenge's off state, end to end through the dialog (ADR 0083): the BFF answered a
    /// null key because no pair is configured, so there is no widget to render, no token to
    /// demand, and the draft honestly carries none — the BFF forwards it without judging.
    /// </remarks>
    [Fact]
    public async Task Rendered_NoSiteKeyServed_RendersNoWidgetAndSendsWithoutAToken()
    {
        // Arrange
        _servedSiteKey = null;

        var (dialog, reference) = await OpenAsync();

        WriteVisibleFields(dialog,
            firstname: "Grace",
            lastname: "Hopper",
            email: "grace@example.org",
            message: "I would like to book this training.");

        dialog.WaitForAssertion(() => Sending(dialog).HasAttribute("disabled").Should().BeFalse());

        // Act
        Sending(dialog).Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Data.Should().BeOfType<ContactDraft>()
            .Which.TurnstileToken.Should().BeNull();
        JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "turnstile.render")
            .Should().BeEmpty("a null key means there is no challenge to render");
    }

    /// <summary>
    /// Confirming, the check not done, refuses to close and says what is missing.
    /// </summary>
    /// <remarks>
    /// An empty answer from the widget means the check is unsolved — or never came up — and a
    /// dialog that closed anyway would hand the page a draft the BFF is certain to refuse. The
    /// refusal happens here, worded for the visitor, with everything they typed kept.
    /// </remarks>
    [Fact]
    public async Task Confirming_TheCheckNotDone_RefusesToCloseAndSaysWhatIsMissing()
    {
        // Arrange
        _getResponse.SetResult(string.Empty);

        var (dialog, reference) = await OpenAsync();

        WriteVisibleFields(dialog,
            firstname: "Grace",
            lastname: "Hopper",
            email: "grace@example.org",
            message: "I would like to book this training.");

        dialog.WaitForAssertion(() => Sending(dialog).HasAttribute("disabled").Should().BeFalse());

        // Act
        Sending(dialog).Click();

        // Assert
        dialog.WaitForAssertion(() =>
            dialog.Markup.Should().Contain("Complete the check above to send your message."));
        reference.Result.IsCompleted.Should().BeFalse();
    }

    /// <summary>
    /// Writing a message shorter than the floor, refuses it and names the floor.
    /// </summary>
    /// <remarks>
    /// Ten characters is what the contract publishes, so refusing here spares a round trip rather
    /// than inventing a rule — and the floor is measured on the trimmed text, because the trimmed
    /// text is what is sent: nine letters padded with spaces would pass a raw count and still
    /// arrive below the contract's own.
    /// </remarks>
    [Fact]
    public async Task WritingAMessageShorterThanTheFloor_RefusesItAndNamesTheFloor()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();

        // Act
        WriteVisibleFields(dialog,
            firstname: "Grace",
            lastname: "Hopper",
            email: "grace@example.org",
            message: "Hello.   ");

        // Assert
        dialog.WaitForAssertion(() =>
            dialog.Markup.Should().Contain("A message needs at least 10 characters"));

        Sending(dialog).HasAttribute("disabled").Should().BeTrue();
        reference.Result.IsCompleted.Should().BeFalse();
    }

    /// <summary>
    /// The honeypot, stays out of sight and out of the tab order.
    /// </summary>
    /// <remarks>
    /// The two halves of the design in one assertion: the field must exist for a form-filling
    /// script to find, and no person may ever reach it — not by eye, not by Tab, not through a
    /// screen reader. Breaking either half quietly turns every visitor into a bot or every bot
    /// into a visitor (ADR 0082).
    /// </remarks>
    [Fact]
    public async Task TheHoneypot_StaysOutOfSightAndOutOfTheTabOrder()
    {
        // Act
        var (dialog, _) = await OpenAsync();

        // Assert
        var honeypot = dialog.Find("input[name=website]");

        honeypot.GetAttribute("tabindex").Should().Be("-1");
        honeypot.GetAttribute("aria-hidden").Should().Be("true");
    }

    /// <summary>
    /// Confirming, the honeypot filled, carries it in the draft verbatim.
    /// </summary>
    /// <remarks>
    /// The dialog does not judge the honeypot, and must not: a request that carries one is
    /// answered exactly as a good one is — accepted, and dropped without sending — because a
    /// different answer is how a bot finds out. That decision is the server's (ADR 0082), so this
    /// component's whole duty is to pass the evidence along untouched.
    /// </remarks>
    [Fact]
    public async Task Confirming_TheHoneypotFilled_CarriesItInTheDraftVerbatim()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();

        WriteVisibleFields(dialog,
            firstname: "Grace",
            lastname: "Hopper",
            email: "grace@example.org",
            message: "I would like to book this training.");

        dialog.Find("input[name=website]").Input("https://spam.example ");

        dialog.WaitForAssertion(() => Sending(dialog).HasAttribute("disabled").Should().BeFalse());

        // Act
        Sending(dialog).Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Data.Should().BeOfType<ContactDraft>()
            .Which.Website.Should().Be("https://spam.example ");
    }

    /// <summary>
    /// Canceling, answers nothing at all.
    /// </summary>
    /// <remarks>
    /// Backing out is not a decision, and the pages read a canceled result as "do not send". A
    /// dialog that closed with a draft instead would have them send one.
    /// </remarks>
    [Fact]
    public async Task Canceling_AnswersNothingAtAll()
    {
        // Arrange
        var (dialog, reference) = await OpenAsync();

        WriteVisibleFields(dialog,
            firstname: "Grace",
            lastname: "Hopper",
            email: "grace@example.org",
            message: "I would like to book this training.");

        // Act
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        // Assert
        var result = await ClosedAsync(dialog, reference);

        result.Canceled.Should().BeTrue();
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Dialog, IDialogReference Reference)> OpenAsync()
    {
        var dialog = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<ContactTrainerDialog>
        {
            { component => component.TrainerName, TrainerName }
        };

        var reference = await dialog.InvokeAsync(
            () => dialogService.ShowAsync<ContactTrainerDialog>($"Contact {TrainerName}", parameters));

        return (dialog, reference);
    }

    /// <remarks>
    /// The wait comes before the await, and that ordering is the point: a dialog that never closes
    /// leaves <see cref="IDialogReference.Result"/> pending forever, so awaiting it first turns a
    /// failing assertion into a suite that hangs rather than one that goes red.
    /// </remarks>
    private static async Task<DialogResult> ClosedAsync(
        IRenderedComponent<MudDialogProvider> dialog, IDialogReference reference)
    {
        dialog.WaitForAssertion(() => reference.Result.IsCompleted.Should().BeTrue());

        var result = await reference.Result;

        result.Should().NotBeNull();

        return result;
    }

    /// <summary>
    /// Fills the four fields a person can see, leaving the honeypot to the one fact about it.
    /// </summary>
    /// <remarks>
    /// By position rather than by label, the way the sign-in and registration suites read their
    /// forms: the honeypot is excluded by the name it wears for the bots, so this helper stays
    /// honest about which inputs a person is offered.
    /// </remarks>
    private static void WriteVisibleFields(
        IRenderedComponent<MudDialogProvider> dialog,
        string firstname, string lastname, string email, string message)
    {
        var fields = dialog.FindAll("input")
            .Where(input => input.GetAttribute("name") != "website")
            .ToList();

        fields[0].Input(firstname);
        fields[1].Input(lastname);
        fields[2].Input(email);

        // The immediate event rather than the change one: the fields validate as they are typed,
        // which is what makes the sending button follow along.
        dialog.Find("textarea").Input(message);
    }

    private static AngleSharp.Dom.IElement Sending(IRenderedComponent<MudDialogProvider> dialog) =>
        dialog.FindAll("button").Single(button => button.TextContent.Trim() == Verb);
}
