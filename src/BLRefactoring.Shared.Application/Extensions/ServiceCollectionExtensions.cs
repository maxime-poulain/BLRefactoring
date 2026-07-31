using BLRefactoring.Shared.Application.IntegrationEventHandlers;
using BLRefactoring.Shared.Application.IntegrationEvents;
using BLRefactoring.Shared.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BLRefactoring.Shared.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the handlers that react to integration events once their transaction has
    /// committed.
    /// </summary>
    /// <remarks>
    /// These live here rather than beside the outbox machinery because the dependency rule runs
    /// one way: infrastructure knows the domain, not the application. The outbox provides the
    /// queue and the dispatcher; the application says what a published fact should cause.
    /// <para>
    /// Registration is explicit rather than assembly-scanned. There are four of them, and a
    /// reader can see at a glance which facts have a consumer — worth more here than saving
    /// four lines, since a missing registration would show up only as a message that publishes
    /// successfully while doing nothing at all.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddIntegrationEventHandlers(this IServiceCollection services)
    {
        return services
            .AddScoped<IIntegrationEventHandler<TrainerRegisteredIntegrationEvent>,
                SendWelcomeEmailWhenTrainerRegisteredHandler>()
            .AddScoped<IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>,
                NotifyPreviousAddressWhenContactEmailChangedHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingPublishedIntegrationEvent>,
                IndexTrainingWhenTrainingPublishedHandler>()
            .AddScoped<IIntegrationEventHandler<TrainingRevisedIntegrationEvent>,
                IndexTrainingWhenTrainingRevisedHandler>();
    }
}
