using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Extensions;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// A visitor's message reaching a trainer, end to end, without the address ever crossing the wire
/// the visitor is on (ADR 0082).
/// </summary>
/// <remarks>
/// The unit suites prove each hop — the mapping, the handler, the projection, the consumer — and
/// this proves the chain they form: a real POST becomes an outbox row, the worker delivers it, the
/// consumer resolves the <em>contact</em> address at delivery, and a real SMTP message arrives at
/// an address that never appeared in anything the visitor sent or was sent. That last property is
/// the record's whole point, and only a test that drives the visitor's side of the wire and reads
/// the trainer's mailbox can state it.
/// <para>
/// Every assertion that waits for the index polls, for the reason the search suite gives at
/// length; every recipient is unique per test, for the reason the email suite gives.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since the pipeline under test is
/// each host's own.</typeparam>
public abstract class ContactTrainerTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IMailboxSource, IServiceScopeSource
{
    /// <summary>
    /// Contacting an offered trainer, delivers to the contact address, through real SMTP.
    /// </summary>
    /// <remarks>
    /// The contact address is changed first, so the recipient asserted on can only be the address
    /// the consumer resolved at delivery — a chain that read the account's address, or one that
    /// carried the address on the fact from before the change, would deliver somewhere else and
    /// fail here (ADR 0056, ADR 0082). The subject names the training read back from the catalog,
    /// and the body names the visitor with their own address, which is all the trainer needs to
    /// answer without ever having been looked up.
    /// </remarks>
    [Fact]
    public async Task ContactingAnOfferedTrainer_DeliversToTheContactAddress_ThroughRealSmtp()
    {
        // Signed in by hand rather than through the one-call helper, for the same reason the email
        // suite is: this test needs the registration's own names to keep the profile's edit to the
        // one field under test.
        var trainer = Factory.CreateClient();
        var register = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(trainer, register)).EnsureSuccessStatusCode();

        // Verified like every caller that is not about the verification, so the create door
        // admits the training the visitor writes about (ADR 0090).
        await AuthHelper.MarkEmailVerifiedAsync(Factory, register.Username);

        var token = await AuthHelper.LoginAsync(trainer, register.Username, register.Password);
        trainer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var contactAddress = $"contact-{register.Username}@example.com";
        var entityTag = await trainer.GetETagAsync("/Trainer/me");
        (await trainer.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = register.Firstname,
            Lastname = register.Lastname,
            ContactEmail = contactAddress
        }, entityTag)).EnsureSuccessStatusCode();

        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;
        var trainingId = await PublishAsync(trainer, "A Training Worth Writing About");
        await WaitUntilOfferedAsync(trainingId);

        var visitor = Factory.CreateClient();

        var accepted = await visitor.PostAsJsonAsync(
            $"/Catalog/trainers/{me.Id}/contact",
            Message(trainingId));

        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var text = await Mailbox.WaitForEmailAsync(
            Factory, contactAddress, "Grace Hopper contacted you about “A Training Worth Writing About”");

        text.Should().Contain(
            "I would like to book this training.",
            "what the visitor wrote is the message's whole reason to exist");
        text.Should().Contain(
            "Grace Hopper <grace@example.org>",
            "the trainer decides whether to answer on exactly that");
    }

    /// <summary>
    /// The honeypot filled, is answered like any other message, and nothing ever leaves.
    /// </summary>
    /// <remarks>
    /// Both halves have to hold over the real wire, because each one has a hop no unit test sees:
    /// the 202 proves the boundary's mapping still turns the plausible-looking field into the flag
    /// the handler drops on — a mapping that lost it would deliver every bot's message with all
    /// suites green — and the empty mailbox proves the drop is a drop, not a detour. The full
    /// budget is spent watching for a message that must never come, which is what a negative
    /// proof costs (ADR 0082).
    /// </remarks>
    [Fact]
    public async Task TheHoneypotFilled_IsAnsweredLikeAnyOtherMessage_AndNothingEverLeaves()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;
        var trainingId = await PublishAsync(trainer, "A Training The Bot Was Reading");
        await WaitUntilOfferedAsync(trainingId);

        var bot = Factory.CreateClient();

        var answered = await bot.PostAsJsonAsync(
            $"/Catalog/trainers/{me.Id}/contact",
            Message(trainingId, website: "https://spam.example"));

        answered.StatusCode.Should().Be(
            HttpStatusCode.Accepted,
            "a different answer is how a bot finds out which field betrayed it");

        (await Mailbox.NothingEverArrivesAsync(
                Factory, me.ContactEmail, "Grace Hopper contacted you about “A Training The Bot Was Reading”"))
            .Should().BeTrue("a message known to be automated is dropped without a trace");
    }

    /// <summary>
    /// A trainer the catalog will not show, cannot be written to, and absence looks the same.
    /// </summary>
    /// <remarks>
    /// The write wears the profile's own visibility (ADR 0070): this trainer is registered and
    /// their row perfectly readable, but with nothing on offer their page answers nothing — and so
    /// does their letterbox, indistinguishably from an identifier that never named anyone.
    /// </remarks>
    [Fact]
    public async Task ATrainerTheCatalogWillNotShow_CannotBeWrittenTo()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var visitor = Factory.CreateClient();

        var nothingOnOffer = await visitor.PostAsJsonAsync(
            $"/Catalog/trainers/{me.Id}/contact", Message(trainingId: null));

        nothingOnOffer.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var neverExisted = await visitor.PostAsJsonAsync(
            $"/Catalog/trainers/{Guid.CreateVersion7()}/contact", Message(trainingId: null));

        neverExisted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The window on one trainer, closes against the flood, and for nobody else.
    /// </summary>
    /// <remarks>
    /// Driven against an identifier that names nobody, on purpose: the limiter sits in front of
    /// the endpoint, so the window counts requests rather than deliveries — a flood refused at the
    /// door is refused whether or not it would have found anyone — and no mail and no outbox rows
    /// are minted proving it. The last request shows the partition: a different trainer's window
    /// is untouched by this one closing (ADR 0082).
    /// </remarks>
    [Fact]
    public async Task TheWindowOnOneTrainer_ClosesAgainstTheFlood_AndForNobodyElse()
    {
        var visitor = Factory.CreateClient();
        var buried = Guid.CreateVersion7();

        for (var sent = 0; sent < ContactRateLimiting.PermitsPerWindow; sent++)
        {
            (await visitor.PostAsJsonAsync($"/Catalog/trainers/{buried}/contact", Message(trainingId: null)))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, "the window is still open");
        }

        var refused = await visitor.PostAsJsonAsync(
            $"/Catalog/trainers/{buried}/contact", Message(trainingId: null));

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.Should().BeNull(
            "the shape of the window is not something a public endpoint owes an anonymous caller");

        var someoneElse = await visitor.PostAsJsonAsync(
            $"/Catalog/trainers/{Guid.CreateVersion7()}/contact", Message(trainingId: null));

        someoneElse.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "one trainer being buried must not close anybody else's letterbox");
    }

    /// <summary>The message every fact sends, from the same visitor.</summary>
    private static ContactTrainerHttpRequest Message(Guid? trainingId, string? website = null) => new()
    {
        SenderFirstname = "Grace",
        SenderLastname = "Hopper",
        SenderEmailAddress = "grace@example.org",
        Message = "I would like to book this training.",
        TrainingId = trainingId,
        Website = website
    };

    /// <summary>Creates and publishes a training, and answers its identifier.</summary>
    private static async Task<Guid> PublishAsync(HttpClient trainer, string title)
    {
        var created = await trainer.PostAsJsonAsync("/Training", TrainingRequests.Valid(title));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Polls the catalog's search until the index holds the training.</summary>
    /// <remarks>
    /// The search rather than the contact endpoint, on purpose: waiting on the endpoint under test
    /// would make a permanently closed letterbox look like a slow index.
    /// </remarks>
    private async Task WaitUntilOfferedAsync(Guid trainingId)
    {
        var anonymous = Factory.CreateClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var page = await anonymous.GetAsync("/Catalog/trainings?page=1&pageSize=50");
            page.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await page.Content.ReadFromJsonAsync<JsonElement>();

            if (body.GetProperty("items").EnumerateArray()
                .Any(item => item.GetProperty("id").GetGuid() == trainingId))
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
