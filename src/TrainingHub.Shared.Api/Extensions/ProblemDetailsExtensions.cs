using TrainingHub.Shared.Api.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// One error format for both hosts: RFC 7807 <c>ProblemDetails</c>, whatever failed.
/// </summary>
/// <remarks>
/// Shared for the same reason CORS, identity and optimistic concurrency are: the two hosts
/// advertise the same REST API, and error handling is exactly the kind of plumbing that diverges
/// when each host declares its own. It already had — one host caught unhandled exceptions and the
/// other did not.
/// </remarks>
public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Registers the problem-details service and the exception handlers, in the order they are
    /// tried.
    /// </summary>
    /// <remarks>
    /// Order is registration order, and it matters: the validation handler must be asked before
    /// the catch-all, or a validation failure is answered with a 500. Expressing it as a sequence
    /// of registrations rather than as nested middlewares is the point of moving to
    /// <c>IExceptionHandler</c>.
    /// </remarks>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        return services
            .AddProblemDetails()
            .AddExceptionHandler<ValidationExceptionHandler>()
            .AddExceptionHandler<UnhandledExceptionHandler>();
    }

    /// <summary>
    /// Puts the exception handlers in the pipeline.
    /// </summary>
    /// <remarks>
    /// First, so everything downstream is covered — authentication, authorization, routing and the
    /// actions alike. Registered after the authentication middlewares, as it used to be, anything
    /// thrown while authenticating escapes to the host.
    /// </remarks>
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseExceptionHandler();
    }
}
