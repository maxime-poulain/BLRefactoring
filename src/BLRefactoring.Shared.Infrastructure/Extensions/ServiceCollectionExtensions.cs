using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.Repositories;
using BLRefactoring.Shared.Infrastructure.Services;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
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
            // Resolved through ITrainingRepository rather than registered a second time against
            // the same implementation: two registrations mean two instances per request, and the
            // only reason that was harmless is that both happened to share the DbContext.
            .AddScoped<IUniquenessTitleChecker>(serviceProvider =>
                (TrainingRepository)serviceProvider.GetRequiredService<ITrainingRepository>())
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
            .AddSingleton<IEmailSender, FakeEmailSender>()
            .AddSingleton<ITrainingSearchIndexer, FakeTrainingSearchIndexer>();
    }
}
