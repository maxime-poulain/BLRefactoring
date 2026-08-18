using System.Transactions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Development;
using TrainingHub.Shared.Infrastructure.Photos;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

// This project already has a namespace called Email — the SMTP adapter lives in it — so importing
// the value object beside it would make `Email` mean whichever using the compiler reached first.
// The alias names it once, and it names it the way the aggregate's own property does.
using ContactEmail = TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects.Email;

namespace TrainingHub.Shared.Infrastructure.Extensions;

/// <summary>
/// Fills a development database with the written catalog — in Development, when asked, and never
/// otherwise (ADR 0079).
/// </summary>
/// <remarks>
/// An empty database proves nothing. The pagination works on one page, the search matches whatever
/// was typed into it, the facets show one subject, a trainer's profile holds a single training and
/// the administration lists three rows — every screen is correct and none of them is exercised.
/// This is what puts five hundred trainings behind them.
/// <para>
/// <b>Built by the domain, not around it.</b> Every trainer goes through <c>Trainer.Create</c>,
/// every training through <c>Training.CreateAsync</c> with the same four ports the application
/// layer resolves, every portrait through the sanitizer and the object store, and every state
/// change through the aggregate's own method. A seeder writing rows directly would produce data
/// the product could not have produced, which is the one thing a development dataset must never be
/// — it would pass every screen and hide exactly the defects it exists to reveal.
/// </para>
/// <para>
/// <b>Three gates, and the third is off by default.</b> Emitting the OpenAPI document loads a host
/// for real, so the first gate is the same one the administrator's seeder carries. The second is
/// the environment. The third is a configuration key nobody sets by accident, because five hundred
/// trainings is not something to discover on a machine that was only supposed to start.
/// </para>
/// <para>
/// <b>Idempotent, and destructive of nothing.</b> The dataset names its accounts deterministically,
/// so a trainer whose username already exists is skipped whole — a completed run is a no-op, an
/// interrupted one resumes, and nothing already in the database is ever deleted or overwritten. The
/// price is that a half-written trainer, had the transaction not covered them, would be skipped
/// forever; the transaction is what makes that impossible.
/// </para>
/// <para>
/// <b>It runs before the outbox worker does.</b> Around a hundred and seventy welcome emails and
/// every search-index write are committed as facts and delivered once <c>app.Run()</c> starts the
/// worker, which drains them in batches. With Mailpit in the compose file that is where they land;
/// without an SMTP server they become poison messages and light the readiness probe, which is the
/// system behaving exactly as ADR 0033 says it should.
/// </para>
/// </remarks>
public static class DevelopmentSeedExtensions
{
    /// <summary>
    /// The configuration key that turns the seeding on. Absent means off.
    /// </summary>
    public const string EnabledConfigurationKey = "DevelopmentData:Enabled";

    /// <summary>
    /// The configuration key naming how many trainings to seed.
    /// </summary>
    /// <remarks>
    /// Tunable so that an integration test can ask for forty and finish, and so that a developer
    /// who wants a small database does not have to edit code to get one.
    /// </remarks>
    public const string TrainingsConfigurationKey = "DevelopmentData:Trainings";

    /// <summary>
    /// How many trainings are seeded when the configuration names no number.
    /// </summary>
    public const int DefaultTrainings = 500;

    /// <summary>
    /// The configuration key carrying the password every seeded account is created with.
    /// </summary>
    /// <remarks>
    /// A key rather than a constant, and for the reason the administrator's seeder already had one:
    /// a credential a host creates accounts with is configuration, and this repository had two
    /// seeders disagreeing about that. <c>Administrator:Password</c> sits in the committed
    /// Development file; <c>DevelopmentData:Password</c> now sits beside it, carrying the same word
    /// a reader will type — a fixture in plain sight, whose whole safety is the environment gate
    /// above it.
    /// <para>
    /// What it buys beyond the consistency: the word is where a developer can change it without
    /// touching source, and a released build carries no credential at all in its assemblies.
    /// Nothing about Identity is relaxed to accommodate the shipped one — it satisfies the
    /// configured policy as written, four characters with no digit, uppercase or symbol required
    /// (ADR 0051), and an architecture rule holds the committed file to that.
    /// </para>
    /// </remarks>
    public const string PasswordConfigurationKey = "DevelopmentData:Password";

    /// <summary>
    /// In Development, and only when <see cref="EnabledConfigurationKey"/> says so, creates the
    /// written catalog. Everywhere else, does nothing.
    /// </summary>
    /// <param name="app">The host whose services hold Identity, the repositories and the clock.</param>
    /// <param name="cancellationToken">Cancels the seeding.</param>
    /// <returns>A task that completes when the database holds the catalog.</returns>
    public static async Task EnsureDevelopmentCatalogAsync(
        this IHost app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Emitting the OpenAPI document loads this host for real — see OpenApiDocumentGeneration.
        // Without this, generating a client would seed whatever database the configuration points at.
        if (OpenApiDocumentGeneration.IsInProgress())
        {
            return;
        }

        var environment = app.Services.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var configuration = app.Services.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>(EnabledConfigurationKey))
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DevelopmentSeedExtensions).FullName!);

        // Reported and skipped rather than invented, exactly as the administrator's seeder treats
        // its own missing key: a credential nobody wrote down is not one to put into a database.
        var password = configuration[PasswordConfigurationKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "The development catalog was asked for, but no {Key} is configured to create its "
                + "accounts with, so nothing was seeded. Set that key, then restart.",
                PasswordConfigurationKey);
            return;
        }

        var wanted = configuration.GetValue<int?>(TrainingsConfigurationKey) ?? DefaultTrainings;

        var anchor = app.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        var dataset = DevelopmentDataset.Build(wanted, anchor);

        logger.LogInformation(
            "Seeding the development catalog: {Trainings} trainings across {Trainers} trainers. "
            + "Accounts already present are skipped.",
            wanted,
            dataset.Count);

        var created = 0;

        foreach (var seed in dataset)
        {
            if (await CreateAsync(app, seed, password, logger, cancellationToken))
            {
                created++;
            }
        }

        logger.LogInformation(
            "The development catalog is seeded: {Created} trainers created, {Skipped} already there. "
            + "Every account signs in with the password {Key} names.",
            created,
            dataset.Count - created,
            PasswordConfigurationKey);
    }

    /// <summary>
    /// Creates one trainer with everything they own, or answers <see langword="false"/> when there
    /// was nothing to do or the creation could not be completed.
    /// </summary>
    /// <remarks>
    /// One scope per trainer, so the change tracker holds one person rather than a hundred and
    /// seventy, and one transaction per trainer, so an interrupted run leaves whole people behind
    /// rather than half of one (ADR 0040). The two contexts share a connection string, so the
    /// account and the aggregate commit together without a distributed transaction.
    /// </remarks>
    private static async Task<bool> CreateAsync(
        IHost app,
        SeededTrainer seed,
        string password,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var userManager = services.GetRequiredService<UserManager<IdentityUser<Guid>>>();

        // The idempotence, and the whole of it: the username is derived from the person's name, so
        // this is the same question on every run.
        if (await userManager.FindByNameAsync(seed.Username) is not null)
        {
            return false;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        // Confirmed at birth: a seeded trainer has no mailbox to click from, and a dataset of
        // five hundred unverifiable catalogs would hide exactly the screens it exists to fill
        // (ADR 0090).
        var user = new IdentityUser<Guid> { UserName = seed.Username, Email = seed.Email, EmailConfirmed = true };

        var account = await userManager.CreateAsync(user, password);
        if (!account.Succeeded)
        {
            // Reported and skipped rather than thrown: one refused account is not a reason for a
            // host to refuse to start. The password is never written to the log.
            logger.LogWarning(
                "The seeded trainer '{Username}' could not be created: {Reasons}",
                seed.Username,
                string.Join(", ", account.Errors.Select(error => error.Description)));
            return false;
        }

        var trainer = Trainer.Create(
            TrainerId.Generate(),
            UserId.Create(user.Id),
            Value(Name.Create(seed.Firstname, seed.Lastname)),
            Value(ContactEmail.Create(seed.Email)),
            Value(Bio.Create(seed.Bio)));

        services.GetRequiredService<ITrainerRepository>().Add(trainer);

        if (seed.PortraitHue is int hue)
        {
            await AttachPortraitAsync(services, trainer, hue, cancellationToken);
        }

        // Written before the first training rather than with it, because the verification port
        // reads the database: it joins the trainer's row to the account's EmailConfirmed, and a
        // trainer that exists only in the change tracker would answer unverified — refusing the
        // whole catalog this seeder exists to create (ADR 0090). Still inside the scope: a run
        // that fails later leaves no half-seeded trainer behind.
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        var scheduled = new List<(TrainingId Id, DateTime CreatedOnUtc)>(seed.Trainings.Count);

        foreach (var planned in seed.Trainings)
        {
            var training = await CreateTrainingAsync(services, trainer.Id, planned, cancellationToken);

            if (training is null)
            {
                logger.LogWarning(
                    "'{Title}' was refused for the seeded trainer '{Username}'.",
                    planned.Title,
                    seed.Username);
                continue;
            }

            scheduled.Add((training.Id, planned.CreatedOnUtc));
        }

        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await BackdateAsync(services, scheduled, cancellationToken);

        // Last, and the order is not a preference: a suspended trainer may publish nothing, so
        // suspending before the catalog exists would leave the person with an empty one (ADR 0050).
        if (seed.SuspensionReason is not null)
        {
            trainer.Suspend(Value(SuspensionReason.Create(seed.SuspensionReason)));
            services.GetRequiredService<ITrainerRepository>().Update(trainer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        transaction.Complete();

        return true;
    }

    /// <summary>
    /// Creates one training and puts it in the state the dataset placed it in.
    /// </summary>
    /// <remarks>
    /// The four ports are resolved from the container rather than stubbed, so the seeded data is
    /// subject to the same uniqueness, capacity, standing and verification questions any caller
    /// would meet. The uniqueness and capacity questions read trainings the unit of work has not
    /// written yet — which is exactly why the dataset guarantees distinct titles per trainer
    /// rather than relying on the checker to notice. The verification question is the reason the
    /// trainer's own row is saved before this method first runs (ADR 0090).
    /// </remarks>
    private static async Task<Training?> CreateTrainingAsync(
        IServiceProvider services,
        TrainerId trainerId,
        SeededTraining planned,
        CancellationToken cancellationToken)
    {
        var creation = await Training.CreateAsync(
            TrainingId.Generate(),
            trainerId,
            Value(TrainingTitle.Create(planned.Title)),
            Value(TrainingDescription.Create(planned.Description)),
            Value(TrainingPrerequisites.Create(planned.Prerequisites)),
            Value(AcquiredSkills.Create(planned.AcquiredSkills)),
            [.. planned.Topics.Select(Filed)],
            services.GetRequiredService<IUniquenessTitleChecker>(),
            services.GetRequiredService<ITrainingCounter>(),
            services.GetRequiredService<ITrainerStanding>(),
            services.GetRequiredService<ITrainerVerification>(),
            cancellationToken);

        return creation.Match<Training?>(
            training =>
            {
                if (planned.IsWithdrawnByOwner)
                {
                    training.Unpublish();
                }
                else if (planned.WithholdingReason is not null)
                {
                    training.Withhold(Value(WithholdingReason.Create(planned.WithholdingReason)));
                }

                services.GetRequiredService<ITrainingRepository>().Add(training);

                return training;
            },
            _ => null);
    }

    /// <summary>
    /// Draws a portrait, puts it through everything a real upload goes through, and attaches it.
    /// </summary>
    /// <remarks>
    /// The same three steps and the same order as the application service (ADR 0063): the bytes are
    /// vetted, then stripped, then described from what will actually be stored. Nothing here is a
    /// shortcut around the aggregate — a drawn portrait that failed the rules would fail them here
    /// too, which is the point of not writing the row directly.
    /// </remarks>
    private static async Task AttachPortraitAsync(
        IServiceProvider services, Trainer trainer, int hue, CancellationToken cancellationToken)
    {
        var drawn = DevelopmentPortraitDrawer.Draw(hue);

        var vetted = Value(TrainerPhoto.Vet(drawn, DevelopmentPortraitDrawer.ContentType));

        var sanitized = Value(services.GetRequiredService<IPhotoSanitizer>()
            .Sanitize(drawn, vetted));

        var photo = Value(TrainerPhoto.Create(
            sanitized.Content.Span,
            sanitized.ContentType,
            services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));

        await services.GetRequiredService<ITrainerPhotoStore>()
            .StoreAsync(trainer.Id, photo, sanitized.Content, cancellationToken);

        trainer.AttachPhoto(photo);
    }

    /// <summary>
    /// Moves each training's creation stamp back to the instant the dataset scheduled it at.
    /// </summary>
    /// <remarks>
    /// <b>The one write this seeder makes outside an aggregate, and the reason is worth stating.</b>
    /// <c>CreatedOn</c> is an audit column: the interceptor stamps it from the clock on insert and
    /// explicitly refuses to let an update touch it, so there is no path through the domain or
    /// through change tracking that could place a training in the past. Without this, five hundred
    /// trainings share one second, the catalog's newest-first page is decided entirely by the
    /// identifier tiebreaker (ADR 0071), and a reader learns nothing from the one sort a visitor
    /// actually uses.
    /// <para>
    /// A bulk update rather than a tracked one, for the same reason: it goes to the database
    /// directly, so the interceptor never sees it. It runs inside the trainer's own transaction and
    /// before <c>app.Run()</c> starts the outbox worker, so the search index copies the corrected
    /// value rather than the stamped one (ADR 0059).
    /// </para>
    /// </remarks>
    private static async Task BackdateAsync(
        IServiceProvider services,
        IReadOnlyList<(TrainingId Id, DateTime CreatedOnUtc)> scheduled,
        CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<TrainingContext>();

        foreach (var (id, createdOnUtc) in scheduled)
        {
            await context.Trainings
                .Where(training => training.Id == id)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(training => training.CreatedOn, createdOnUtc),
                    cancellationToken);
        }
    }

    /// <summary>
    /// The topic that name stands for.
    /// </summary>
    /// <remarks>
    /// Total by contract rather than by hope: <c>DevelopmentCatalogTests</c> puts every name in the
    /// corpus through <c>TryFromName</c>, so a failure here means the corpus changed and the suite
    /// was not run.
    /// </remarks>
    private static Topic Filed(string name) =>
        Topic.TryFromName(name, out var topic)
            ? topic
            : throw new InvalidOperationException(
                $"The development corpus files a training under '{name}', which the domain does not admit.");

    /// <summary>
    /// The value a factory answered, or an exception naming what it refused.
    /// </summary>
    /// <remarks>
    /// The corpus is checked field by field against these same factories in a unit test that runs
    /// without a database, so reaching this throw means the suite was not run — not that a user
    /// typed something. That is why it is an exception rather than a skipped trainer: it is a defect
    /// in the corpus, and a seeder quietly working around one would hide it.
    /// </remarks>
    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(
            value => value,
            errors => throw new InvalidOperationException(
                "The development corpus holds a value the domain refuses: " +
                string.Join("; ", errors.Select(error => error.ErrorMessage))));
}
