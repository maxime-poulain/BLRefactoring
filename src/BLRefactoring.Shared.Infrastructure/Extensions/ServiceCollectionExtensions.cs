using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BLRefactoring.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddSingleton<IEventPublisher, MediatorRDomainEventPublisher>()
            .AddTransient<ITransactionManager, TransactionManager>()
            .AddScoped<ITrainerRepository, TrainerRepository>()
            .AddScoped<ITrainingRepository, TrainingRepository>();
    }
}
