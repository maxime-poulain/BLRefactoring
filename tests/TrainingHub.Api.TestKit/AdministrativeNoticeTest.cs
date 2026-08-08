using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The notices a sanction produces, end to end and on both hosts (ADR 0056).
/// </summary>
/// <remarks>
/// What the unit tests cannot prove is exactly what this chain is made of: an administrative
/// endpoint commits a state change, a domain event becomes an outbox row inside that transaction,
/// a background worker picks it up after the commit, a consumer looks the trainer's account up
/// across two databases, and MailKit puts a message on the wire. Every one of those is real here.
/// <para>
/// The address is the fact worth the most. Every trainer below moves their published contact
/// address somewhere else before the sanction lands, so an implementation reading
/// <c>Trainer.ContactEmail</c> would send to a mailbox nobody is watching — and would pass every
/// test that did not bother to make the two differ.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class AdministrativeNoticeTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource, IMailboxSource
{
    /// <summary>
    /// Suspending a trainer, sends the reason to the account rather than to the contact address.
    /// </summary>
    [Fact]
    public async Task SuspendingATrainer_SendsTheReason_ToTheAccountRatherThanTheContactAddress()
    {
        var trainer = await RegisterTrainerAsync();
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{trainer.Id}/suspend",
            new SuspendTrainerHttpRequest { Reason = "Repeated breaches of the content policy." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var text = await Mailbox.WaitForEmailAsync(
            Factory, trainer.AccountAddress, "Your trainer account has been suspended");

        text.Should().Contain(
            "Repeated breaches of the content policy.",
            "a sanction the product never explains is one the trainer discovers by failing to do something");

        text.Should().NotContain(
            trainer.ContactAddress,
            "the notice is addressed to the person who signs in, not to the address their profile publishes");
    }

    /// <summary>
    /// Reinstating a trainer, tells the same account the sanction is over.
    /// </summary>
    /// <remarks>
    /// The half a lifting needs and the half that is easiest to leave out: a trainer told they were
    /// sanctioned and never told it ended finds out by trying something and seeing it work.
    /// </remarks>
    [Fact]
    public async Task ReinstatingATrainer_TellsTheSameAccount_TheSanctionIsOver()
    {
        var trainer = await RegisterTrainerAsync();
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{trainer.Id}/suspend",
            new SuspendTrainerHttpRequest { Reason = "Under review." }))
            .EnsureSuccessStatusCode();

        (await administrator.PostAsync($"/Administration/trainers/{trainer.Id}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Mailbox.WaitForEmailAsync(
            Factory, trainer.AccountAddress, "Your trainer account has been reinstated");
    }

    /// <summary>
    /// Withholding a training, names it and its reason to its owner.
    /// </summary>
    [Fact]
    public async Task WithholdingATraining_NamesItAndItsReason_ToItsOwner()
    {
        var trainer = await RegisterTrainerAsync();
        var title = $"Advanced domain modelling {Guid.NewGuid():N}";
        var trainingId = await CreateTrainingAsync(trainer.Client, title);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = "Reported for misleading claims." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var text = await Mailbox.WaitForEmailAsync(
            Factory, trainer.AccountAddress, "One of your trainings has been withheld");

        text.Should().Contain(title, "an owner with several trainings has to be told which one");
        text.Should().Contain("Reported for misleading claims.");
    }

    /// <summary>
    /// Releasing a training, notifies nobody.
    /// </summary>
    /// <remarks>
    /// The executable half of ADR 0052's asymmetry, and the only one there is: withholding earns a
    /// fact because consumers wait on it, releasing earns none because the training lands where it
    /// was not indexed and nobody was listening. Everything else in this suite would still pass if
    /// a release quietly published a fact of its own.
    /// <para>
    /// It costs the full delivery budget, spent once, because a message that has not arrived yet
    /// and one that never will look identical at every earlier instant.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ReleasingATraining_NotifiesNobody()
    {
        var trainer = await RegisterTrainerAsync();
        var trainingId = await CreateTrainingAsync(trainer.Client, $"A training {Guid.NewGuid():N}");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = "Taken down for this proof." }))
            .EnsureSuccessStatusCode();

        // Waited for first, so the release below is not racing a delivery that was already owed.
        await Mailbox.WaitForEmailAsync(
            Factory, trainer.AccountAddress, "One of your trainings has been withheld");

        (await administrator.PostAsync($"/Administration/trainings/{trainingId}/release", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Mailbox.NothingEverArrivesAsync(
            Factory, trainer.AccountAddress, "One of your trainings has been released"))
            .Should().BeTrue("a release has no consumer, so it commits no fact and sends no message");
    }

    /// <summary>
    /// A trainer whose account address and published contact address are deliberately different.
    /// </summary>
    /// <param name="Id">The trainer's identifier.</param>
    /// <param name="AccountAddress">The address the account signs in with — where notices go.</param>
    /// <param name="ContactAddress">The address the profile publishes — where they must not.</param>
    /// <param name="Client">A client authenticated as this trainer.</param>
    private sealed record RegisteredTrainer(
        Guid Id, string AccountAddress, string ContactAddress, HttpClient Client);

    private async Task<RegisteredTrainer> RegisterTrainerAsync()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // The whole point of the fixture: registration makes the two addresses equal, and this
        // moves them apart so that reading the wrong one is observable.
        var contactAddress = $"published-{request.Username}@example.com";
        var entityTag = await client.GetETagAsync("/Trainer/me");
        (await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = contactAddress
        }, entityTag)).EnsureSuccessStatusCode();

        var profile = await client.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me");

        return new RegisteredTrainer(profile!.Id, request.Email, contactAddress, client);
    }

    private static async Task<Guid> CreateTrainingAsync(HttpClient client, string title)
    {
        var created = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid(title));
        created.EnsureSuccessStatusCode();

        return await created.Content.ReadFromJsonAsync<Guid>();
    }
}
