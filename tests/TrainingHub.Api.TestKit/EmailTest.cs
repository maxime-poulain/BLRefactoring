using System.Net.Http.Headers;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves the email path end to end: a fact committed by the API leaves the host as a real SMTP
/// message and arrives addressed, titled and worded as its handler composed it.
/// </summary>
/// <remarks>
/// The messages are read back through the mail server's own HTTP API rather than a substituted
/// port, because delivery is exactly what a substitute would assume: that MailKit connects, that
/// the options point somewhere real, that the sender the deployment configured is the one on the
/// wire. Every recipient below is unique per test — the registration helper mints them — so the
/// mailbox accumulating across a fixture's lifetime cannot make one test read another's message.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since the wiring under test is
/// each host's own.</typeparam>
public abstract class EmailTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IMailboxSource
{
    /// <summary>
    /// Registering a trainer, delivers the welcome email, through real SMTP.
    /// </summary>
    [Fact]
    public async Task RegisteringATrainer_DeliversTheWelcomeEmail_ThroughRealSmtp()
    {
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(Factory.CreateClient(), request);
        response.EnsureSuccessStatusCode();

        var text = await Mailbox.WaitForEmailAsync(Factory, request.Email, "Welcome aboard!");

        text.Should().Contain(
            $"{request.Firstname} {request.Lastname}",
            "the handler addresses the trainer by the name the fact carried");
    }

    /// <summary>
    /// Changing the contact email, warns the address that lost it.
    /// </summary>
    [Fact]
    public async Task ChangingTheContactEmail_WarnsTheAddressThatLostIt()
    {
        // Signed in by hand rather than through the one-call helper, because this test needs to
        // keep the original address: it is the recipient the warning must go to.
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();
        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newAddress = $"changed-{request.Username}@example.com";
        var entityTag = await client.GetETagAsync("/Trainer/me");
        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = newAddress
        }, entityTag);
        response.EnsureSuccessStatusCode();

        var text = await Mailbox.WaitForEmailAsync(
            Factory, request.Email, "Your contact email address was changed");

        text.Should().Contain(
            newAddress,
            "the warning tells the previous owner where their profile now points");
    }
}
