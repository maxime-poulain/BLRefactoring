using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.Outbox;
using BLRefactoring.Shared.Infrastructure.Repositories;
using BLRefactoring.Shared.Infrastructure.Services;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BLRefactoring.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<ITrainerRepository, TrainerRepository>()
            .AddScoped<ITrainingRepository, TrainingRepository>()
            .AddScoped<IUniquenessTitleChecker, TrainingRepository>()
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
            .AddScoped<DomainEventInterceptor>()
            .AddSingleton<AuditableEntitiesInterceptor>()
            .AddScoped<ITokenService, TokenService>()
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .AddSingleton<IEmailSender, FakeEmailSender>()
            .AddSingleton<ITrainingSearchIndexer, FakeTrainingSearchIndexer>()
            .AddOutbox();
    }

    /// <summary>
    /// Registers the outbox: the writer, the dispatcher and the handlers it feeds, plus the
    /// background service that drains the queue.
    /// </summary>
    /// <remarks>
    /// The handlers themselves are not registered here: they live in the application layer,
    /// which infrastructure does not reference. <see cref="AddIntegrationEventHandlers"/> in
    /// that layer wires them, and <see cref="IntegrationEventDispatcher"/> resolves them from
    /// the container by runtime type.
    /// </remarks>
    private static IServiceCollection AddOutbox(this IServiceCollection services)
    {
        return services
            .AddSingleton(TimeProvider.System)
            .AddScoped<IOutbox, Outbox.Outbox>()
            .AddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>()
            .AddHostedService<OutboxProcessor>();
    }
}
