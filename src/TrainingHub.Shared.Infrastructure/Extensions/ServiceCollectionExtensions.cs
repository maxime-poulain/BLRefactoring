using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.Outbox;
using TrainingHub.Shared.Infrastructure.Photos;
using TrainingHub.Shared.Infrastructure.Queries;
using TrainingHub.Shared.Infrastructure.Repositories;
using TrainingHub.Shared.Infrastructure.Search;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TrainingHub.Shared.Infrastructure.Extensions;

/// <summary>
/// Registers the infrastructure layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database session, the repositories and the unit of work.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Every outbox knob is checked positive before the host serves anything, in the mold of
        // SmtpOptions and ObjectStorageOptions: fail at start-up rather than on the first drain.
        // The defaults all pass, so a host with no section keeps starting (ADR 0033).
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(
                outbox => outbox.PollInterval > TimeSpan.Zero,
                $"'{OutboxOptions.SectionName}:{nameof(OutboxOptions.PollInterval)}' must be positive.")
            .Validate(
                outbox => outbox.BatchSize > 0,
                $"'{OutboxOptions.SectionName}:{nameof(OutboxOptions.BatchSize)}' must be positive.")
            .Validate(
                outbox => outbox.MaxAttempts > 0,
                $"'{OutboxOptions.SectionName}:{nameof(OutboxOptions.MaxAttempts)}' must be positive.")
            .Validate(
                outbox => outbox.LeaseDuration > TimeSpan.Zero,
                $"'{OutboxOptions.SectionName}:{nameof(OutboxOptions.LeaseDuration)}' must be positive.")
            .Validate(
                outbox => outbox.RetryDelay > TimeSpan.Zero,
                $"'{OutboxOptions.SectionName}:{nameof(OutboxOptions.RetryDelay)}' must be positive.")
            .Validate(
                outbox => outbox.RetentionPeriod > TimeSpan.Zero,
                $"'{OutboxOptions.SectionName}:{nameof(OutboxOptions.RetentionPeriod)}' must be positive.")
            .ValidateOnStart();

        // The reset link's base address is required and checked whole at start-up, in the same
        // mold: a host that cannot say where its web front door is would mail visitors a link
        // into nowhere (ADR 0084).
        services.AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .Validate(
                reset => !string.IsNullOrWhiteSpace(reset.LinkBaseAddress),
                $"Missing configuration value '{PasswordResetOptions.SectionName}:{nameof(PasswordResetOptions.LinkBaseAddress)}'.")
            .Validate(
                reset => Uri.TryCreate(reset.LinkBaseAddress, UriKind.Absolute, out _),
                $"'{PasswordResetOptions.SectionName}:{nameof(PasswordResetOptions.LinkBaseAddress)}' must be an absolute URL.")
            .ValidateOnStart();

        return services
            .AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<ITrainerRepository, TrainerRepository>()
            .AddScoped<ITrainingRepository, TrainingRepository>()
            // Resolved through ITrainingRepository rather than registered a second time against
            // the same implementation: two registrations mean two instances per request, and the
            // only reason that was harmless is that both happened to share the DbContext.
            .AddScoped<IUniquenessTitleChecker>(serviceProvider =>
                (TrainingRepository)serviceProvider.GetRequiredService<ITrainingRepository>())
            .AddScoped<ITrainingCounter>(serviceProvider =>
                (TrainingRepository)serviceProvider.GetRequiredService<ITrainingRepository>())
            // The standing port answers about a trainer, so it is the trainer's repository that
            // implements it — resolved through ITrainerRepository for the same reason as above.
            .AddScoped<ITrainerStanding>(serviceProvider =>
                (TrainerRepository)serviceProvider.GetRequiredService<ITrainerRepository>())
            // The read side of six questions that used to cost a whole aggregate, or would have:
            // who owns this training, which trainer is behind this Identity user, where a trainer
            // is reachable as a person, where they are reachable as a professional, what a page of
            // trainers is called, and whether a trainer is under suspension. Each answer is a
            // handful of columns, and none of these ports can write — which is what a post-commit
            // consumer may hold (ADR 0056). Two of them are read on the authorization path, before
            // any use case runs (ADR 0053).
            //
            // The fourth and the third are deliberately not one port: an account address is a
            // credential and a contact address is something a trainer published, and keeping them
            // apart here is what keeps a sanction and a visitor's message from ever reaching the
            // wrong one (ADR 0082).
            .AddScoped<ITrainingOwnerQuery, TrainingOwnerQuery>()
            .AddScoped<ITrainerIdentityQuery, TrainerIdentityQuery>()
            .AddScoped<ITrainerAccountQuery, TrainerAccountQuery>()
            .AddScoped<ITrainerContactQuery, TrainerContactQuery>()
            .AddScoped<ITrainerNamesQuery, TrainerNamesQuery>()
            .AddScoped<ITrainerStandingQuery, TrainerStandingQuery>()
            // The recovery credential's store (ADR 0084) — a writing port in the search indexer's
            // mold rather than a seventh read port: the consumer that issues a reset link and the
            // API flow that redeems one both write the Identity store, through this port's own
            // save and never through the unit of work.
            .AddScoped<IPasswordResetTokenStore, PasswordResetTokenStore>()
            // Scoped like the DbContext it stages rows into: the publisher must share the unit of
            // work of the save that is dispatching the domain events (ADR 0002).
            .AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>()
            // The outbox's read side (ADR 0025, hardened per ADR 0033): the worker each host
            // runs, the processor it scopes per batch, the dispatcher that routes a fact to its
            // consumers, and the seventeen consumers themselves — the policies that used to run
            // inside the transaction, reattached after the commit, the six that answer a sanction
            // (ADR 0056), the one that carries a visitor's message to a trainer (ADR 0082), and
            // the two that guard an account's recovery (ADR 0084).
            // The options bind above, validated.
            .AddScoped<OutboxProcessor>()
            .AddScoped<IntegrationEventDispatcher>()
            .AddScoped<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>,
                SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>,
                NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerSuspendedIntegrationEvent>,
                SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerSuspendedIntegrationEvent>,
                HideCatalogWhenTrainerSuspendedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerReinstatedIntegrationEvent>,
                SendReinstatementNoticeWhenTrainerReinstatedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerReinstatedIntegrationEvent>,
                ShowCatalogWhenTrainerReinstatedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerContactedIntegrationEvent>,
                SendContactMessageWhenTrainerContactedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingCreatedIntegrationEvent>,
                IndexTrainingWhenTrainingCreatedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingEditedIntegrationEvent>,
                ReindexTrainingWhenTrainingEditedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingTransferredIntegrationEvent>,
                ReindexTrainingWhenTrainingTransferredIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingPublishedIntegrationEvent>,
                IndexTrainingWhenTrainingPublishedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingUnpublishedIntegrationEvent>,
                RemoveTrainingFromIndexWhenTrainingUnpublishedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingWithheldIntegrationEvent>,
                SendWithholdingNoticeWhenTrainingWithheldIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingWithheldIntegrationEvent>,
                RemoveTrainingFromIndexWhenTrainingWithheldIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingDeletedIntegrationEvent>,
                RemoveTrainingFromIndexWhenTrainingDeletedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<PasswordResetRequestedIntegrationEvent>,
                SendPasswordResetLinkWhenPasswordResetRequestedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<PasswordChangedIntegrationEvent>,
                SendPasswordChangedNoticeWhenPasswordChangedIntegrationEventHandler>()
            .AddHostedService<OutboxDeliveryWorker>()
            .AddDbContext<TrainingContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(configuration.GetConnectionString("TrainingContext"))
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<DomainEventInterceptor>(),
                        serviceProvider.GetRequiredService<AuditableEntitiesInterceptor>());

                // Parameter values (emails, names, bios…) only ever reach the
                // logs in Development; production logs stay free of personal data.
                if (serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                }
            })
            .AddObjectStorage(configuration)
            // The email port's real adapter (ADR 0031), called by the outbox's consumers after the
            // commit — the worker delivers the facts, and this port acts on them (ADR 0002, ADR 0025).
            .AddEmailDelivery(configuration)
            .AddScoped<DomainEventInterceptor>()
            // The system clock, injected so the audit stamps can be driven by a test.
            .AddSingleton(TimeProvider.System)
            .AddSingleton<AuditableEntitiesInterceptor>()
            // The search port's real adapter (ADR 0059), called by the same consumers after the
            // same commit. Scoped rather than singleton, now that the index is two tables of this
            // database and the adapter holds the session that writes them.
            // The one decoder in the product (ADR 0063). Singleton because it holds nothing: every
            // call allocates its own bitmaps and disposes them, and the type has no field at all.
            .AddSingleton<IPhotoSanitizer, SkiaSharpPhotoSanitizer>()
            .AddScoped<ITrainingSearchIndexer, TrainingSearchIndexer>()
            .AddScoped<ITrainingSearchQuery, TrainingSearchQuery>()
            // The catalog's read by identifier: visibility from the index, content from the write
            // model, and the two never confused (ADR 0062).
            .AddScoped<ICatalogDetailQuery, CatalogDetailQuery>()
            // The outbox's operator surface (ADR 0061). Scoped for the same reason: it reads and
            // writes the delivery table through the request's own session.
            .AddScoped<IOutboxOperations, OutboxOperations>();
    }
}
