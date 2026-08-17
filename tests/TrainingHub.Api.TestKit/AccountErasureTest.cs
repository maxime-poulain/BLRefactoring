using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The erasure of ADR 0085, proven whole: one request removes the account, the trainer, their
/// catalog and their portrait, the farewell leaves by real SMTP, and every property the record
/// claims — the caller's own password as proof of intent, the sanction that does not bar the
/// door, the freed name — is a fact here rather than a sentence there.
/// </summary>
/// <remarks>
/// The transaction is the part no unit test can vouch for: the trainer's rows leave one context
/// and the account leaves another, inside one ambient scope, against real SQL Server — the
/// platform where a promotion to a distributed coordinator would throw rather than commit. The
/// after-effects ride the outbox, so the catalog and the portrait facts poll, exactly as the
/// search proofs do: eventual consistency is the property under test, not a nuisance.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class AccountErasureTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IMailboxSource, IServiceScopeSource
{
    private const string FarewellSubject = "Your TrainingHub account was erased";

    /// <summary>
    /// Erasing the account, removes the account — immediately, and for good.
    /// </summary>
    /// <remarks>
    /// The three doors tried after the 204 are the whole claim of finality: the password no
    /// longer signs in because the account is not suspended but gone, and the bearer token the
    /// caller still holds — valid for up to an hour, as ADR 0085 admits — opens onto nothing,
    /// because every row it could reach left in the same transaction.
    /// </remarks>
    [Fact]
    public async Task ErasingTheAccount_RemovesTheAccount_ImmediatelyAndForGood()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        // Act
        var erased = await EraseAsync(account.Client, account.Password);

        // Assert
        erased.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var signIn = await Factory.CreateClient().PostAsJsonAsync("/Auth/login", new LoginHttpRequest
        {
            Username = account.Username,
            Password = account.Password
        });
        signIn.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the credential died with the account it belonged to");

        var residual = await account.Client.GetAsync("/Trainer/me");
        residual.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.Forbidden],
            "the sixty-minute session the record admits outlives the account, but it opens onto nothing");
    }

    /// <summary>
    /// The wrong password, is refused on its field — and erases nothing.
    /// </summary>
    /// <remarks>
    /// The proof-of-intent clause, told from both sides: a live session is not enough to destroy
    /// an account, and the caller who mistyped keeps everything — the account signs in, the
    /// profile answers, and the refusal names the field so the dialog can say why.
    /// </remarks>
    [Fact]
    public async Task TheWrongPassword_IsRefusedOnItsField_AndErasesNothing()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        // Act
        var refused = await EraseAsync(account.Client, "not-the-password");

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("Password")[0].GetString()
            .Should().Be("The password is incorrect.",
                "only the account's owner can reach this verdict, so it is keyed to its field and said in full");

        (await AuthHelper.LoginAsync(Factory.CreateClient(), account.Username, account.Password))
            .Should().NotBeNullOrWhiteSpace("a refused erasure must leave the account exactly as it was");
        (await account.Client.GetAsync("/Trainer/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// An erased trainer's catalog, leaves public view.
    /// </summary>
    /// <remarks>
    /// The cascade's public half: the trainings' rows leave inside the erasure's transaction, and
    /// each deletion commits its own fact, which the delivery worker replays into the search
    /// index after the commit. What a visitor can find is the assertion, because it is the one
    /// place the whole chain either works or does not.
    /// </remarks>
    [Fact]
    public async Task AnErasedTrainersCatalog_LeavesPublicView()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        var created = await account.Client.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Erased Along With Its Owner"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("erased", holds: trainingId)).Should().Contain(
            trainingId, "the training must be on the shelf before its leaving can prove anything");

        // Act
        (await EraseAsync(account.Client, account.Password))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        (await WaitForCatalogAsync("erased", holdsNot: trainingId)).Should().NotContain(
            trainingId, "a catalog serving an erased trainer's trainings would be the index lying");
    }

    /// <summary>
    /// The farewell, arrives at the account's address, through real SMTP.
    /// </summary>
    /// <remarks>
    /// The fact carries the address because nothing can resolve it afterwards — the rows are
    /// gone — so this is also the proof that the address survived the erasure long enough to be
    /// written to the outbox in the same transaction (ADR 0085).
    /// </remarks>
    [Fact]
    public async Task TheFarewell_ArrivesAtTheAccountsAddress()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        // Act
        (await EraseAsync(account.Client, account.Password))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var farewell = await Mailbox.WaitForEmailAsync(Factory, account.Email, FarewellSubject);

        farewell.Should().Contain("registering anew",
            "the one thing still true after an erasure is that the freed name is its owner's to retake");
    }

    /// <summary>
    /// A freed username and address, register again.
    /// </summary>
    /// <remarks>
    /// Deletion rather than tombstoning, observed from outside: an account that was merely
    /// flagged would still hold its unique columns, and this registration would answer 409.
    /// </remarks>
    [Fact]
    public async Task AFreedUsernameAndAddress_RegisterAgain()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        (await EraseAsync(account.Client, account.Password))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var again = await AuthHelper.RegisterAsync(Factory.CreateClient(), new RegisterHttpRequest
        {
            Username = account.Username,
            Email = account.Email,
            Password = account.Password,
            ConfirmPassword = account.Password,
            Firstname = "Back",
            Lastname = "Again"
        });

        // Assert
        again.StatusCode.Should().Be(HttpStatusCode.Created,
            "erasure frees the username and the address the moment it answers, for anyone — their old owner included");
    }

    /// <summary>
    /// A suspended trainer, may still erase their account.
    /// </summary>
    /// <remarks>
    /// The amendment to ADR 0053, discharged where it matters: every other write of this
    /// trainer's surface answers 403 while the sanction lasts, and this one answers 204, because
    /// the right to leave outlives the sanction (ADR 0085). The withheld trainings die with the
    /// account — the hold governed their offering, never their existence.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainer_MayStillEraseTheirAccount()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        var profile = await account.Client.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me");
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{profile!.Id}/suspend",
            new SuspendTrainerHttpRequest { Reason = "Repeated breaches of the content policy." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var erased = await EraseAsync(account.Client, account.Password);

        // Assert
        erased.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "erasing the account is the one write a suspension does not take away");
    }

    /// <summary>
    /// The erased portrait, leaves the object store.
    /// </summary>
    /// <remarks>
    /// The collector is a post-commit consumer rather than a line in the transaction, because
    /// bytes deleted inside a scope stay deleted when the scope rolls back — and because the
    /// outbox retries what a crash between commit and cleanup would otherwise orphan forever
    /// (ADR 0085). Post-commit means polling, and the seam is the store itself: no route serves
    /// a portrait whose trainer no longer exists.
    /// </remarks>
    [Fact]
    public async Task TheErasedPortrait_LeavesTheObjectStore()
    {
        // Arrange
        var account = await RegisterSignedInAsync();

        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(Portraits.Of(TrainerPhoto.PngContentType));
        part.Headers.ContentType = new MediaTypeHeaderValue(TrainerPhoto.PngContentType);
        form.Add(part, "photo", "portrait");

        var uploaded = await account.Client.PutAsync("/Trainer/me/photo", form);
        uploaded.EnsureSuccessStatusCode();

        var profile = await uploaded.Content.ReadFromJsonAsync<TrainerHttpResponse>();
        var trainerId = TrainerId.Create(profile!.Id);
        var photoId = PhotoId.Create(profile.PhotoId!.Value);

        // Act
        (await EraseAsync(account.Client, account.Password))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        using var scope = Factory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITrainerPhotoStore>();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (await store.FetchAsync(trainerId, photoId) is not null)
        {
            (DateTime.UtcNow < deadline).Should().BeTrue(
                "the collector must delete the erased trainer's bytes once the fact is delivered");

            await Task.Delay(100);
        }
    }

    /// <summary>What the erasure needs to know about the account a fact just registered.</summary>
    private sealed record Account(HttpClient Client, string Username, string Email, string Password);

    /// <summary>Registers a fresh account and signs it in, because erasure is its owner's act.</summary>
    private async Task<Account> RegisterSignedInAsync()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();

        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new Account(client, request.Username, request.Email, request.Password);
    }

    private static Task<HttpResponseMessage> EraseAsync(HttpClient client, string password) =>
        client.PostAsJsonAsync("/Auth/erase-account", new EraseAccountHttpRequest { Password = password });

    /// <summary>
    /// Polls the public catalog until it holds — or stops holding — the training, and answers the
    /// identifiers of the page it last read. <c>CatalogSearchTest</c>'s shape, for its reason:
    /// the index is eventually consistent by design.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> WaitForCatalogAsync(
        string term,
        Guid? holds = null,
        Guid? holdsNot = null)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        var anonymous = Factory.CreateClient();
        IReadOnlyList<Guid> lastSeen = [];

        while (DateTime.UtcNow - started < timeout)
        {
            var page = await anonymous.GetAsync($"/Catalog/trainings?term={term}&page=1&pageSize=50");
            page.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await page.Content.ReadFromJsonAsync<JsonElement>();
            lastSeen = [.. body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())];

            if ((holds is null || lastSeen.Contains(holds.Value))
                && (holdsNot is null || !lastSeen.Contains(holdsNot.Value)))
            {
                return lastSeen;
            }

            await Task.Delay(100);
        }

        return lastSeen;
    }
}
