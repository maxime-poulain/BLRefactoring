using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.Outbox;
using TrainingHub.Shared.Infrastructure.Queries;
using TrainingHub.Shared.Infrastructure.Repositories;
using TrainingHub.Shared.Infrastructure.Services;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
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
            // The read side of two questions the API used to ask a repository: who owns this
            // training, and which trainer is behind this Identity user. Both answers are a handful
            // of columns; both used to cost a whole aggregate.
            .AddScoped<ITrainingOwnerQuery, TrainingOwnerQuery>()
            .AddScoped<ITrainerIdentityQuery, TrainerIdentityQuery>()
            // Scoped like the DbContext it stages rows into: the publisher must share the unit of
            // work of the save that is dispatching the domain events (ADR 0002).
            .AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>()
            // The outbox's read side (ADR 0025): the worker each host runs, the processor it
            // scopes per batch, the dispatcher that routes a fact to its consumers, and the four
            // consumers themselves — the policies that used to run inside the transaction,
            // reattached after the commit.
            .Configure<OutboxOptions>(configuration.GetSection("Outbox"))
            .AddScoped<OutboxProcessor>()
            .AddScoped<IntegrationEventDispatcher>()
            .AddScoped<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>,
                SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>,
                NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingCreatedIntegrationEvent>,
                IndexTrainingWhenTrainingCreatedIntegrationEventHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingEditedIntegrationEvent>,
                ReindexTrainingWhenTrainingEditedIntegrationEventHandler>()
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
            .AddScoped<DomainEventInterceptor>()
            // The system clock, injected so the audit stamps can be driven by a test.
            .AddSingleton(TimeProvider.System)
            .AddSingleton<AuditableEntitiesInterceptor>()
            // Called by the outbox's consumers, after the commit — the worker delivers the facts,
            // and these ports act on them (ADR 0002, ADR 0025). Still fakes that write to the log:
            // choosing a provider stays a one-line change here.
            .AddSingleton<IEmailSender, FakeEmailSender>()
            .AddSingleton<ITrainingSearchIndexer, FakeTrainingSearchIndexer>();
    }
}
