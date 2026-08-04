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
            // The read side of two questions the API used to ask a repository: who owns this
            // training, and which trainer is behind this Identity user. Both answers are a handful
            // of columns; both used to cost a whole aggregate.
            .AddScoped<ITrainingOwnerQuery, TrainingOwnerQuery>()
            .AddScoped<ITrainerIdentityQuery, TrainerIdentityQuery>()
            // Scoped like the DbContext it stages rows into: the publisher must share the unit of
            // work of the save that is dispatching the domain events (ADR 0002).
            .AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>()
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
            // Nothing calls these two ports since the outbox landed: the handlers that used them
            // now record facts instead of acting (ADR 0002). They stay registered because the
            // delivery worker — the outbox's read side, still owed — is what will call them, after
            // the commit this time.
            .AddSingleton<IEmailSender, FakeEmailSender>()
            .AddSingleton<ITrainingSearchIndexer, FakeTrainingSearchIndexer>();
    }
}
