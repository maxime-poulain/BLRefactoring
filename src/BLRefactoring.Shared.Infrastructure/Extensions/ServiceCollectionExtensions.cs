using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.Repositories;
using BLRefactoring.Shared.Infrastructure.Services;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                options.UseSqlServer(configuration.GetConnectionString("TrainingContext"))
                    .EnableSensitiveDataLogging()
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<DomainEventInterceptor>(),
                        serviceProvider.GetRequiredService<AuditableEntitiesInterceptor>()))
            .AddScoped<DomainEventInterceptor>()
            .AddSingleton<AuditableEntitiesInterceptor>()
            .AddScoped<ITokenService, TokenService>()
            .AddScoped<ICurrentUserService, CurrentUserService>();
    }
}
