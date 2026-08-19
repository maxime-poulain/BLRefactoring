using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Infrastructure.Email;

namespace TrainingHub.Shared.Infrastructure.Extensions;

/// <summary>
/// Registers the SMTP adapter behind the application's email port.
/// </summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="SmtpOptions"/> and wires the SMTP adapter behind
    /// <see cref="IEmailSender"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration carrying the <c>Smtp</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// No hosted bootstrapper, unlike the object store: SMTP has nothing to provision, and a host
    /// must be able to start while the mail server is down — an unsendable email is what the
    /// outbox's retry budget exists for, whereas an unreachable object store sits on the serving
    /// path itself.
    /// </remarks>
    public static IServiceCollection AddEmailDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            // Fail at start-up rather than on the first delivery. A relay this host cannot name is
            // a deployment mistake, and the moment to hear about it is before the outbox starts
            // spending its retry budget on it.
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Host),
                $"Missing configuration value '{SmtpOptions.SectionName}:Host'.")
            .Validate(
                options => options.Port is >= 1 and <= 65535,
                $"Configuration value '{SmtpOptions.SectionName}:Port' must be between 1 and 65535.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SenderAddress),
                $"Missing configuration value '{SmtpOptions.SectionName}:SenderAddress'.")
            .Validate(
                options => string.IsNullOrEmpty(options.Username) == string.IsNullOrEmpty(options.Password),
                $"'{SmtpOptions.SectionName}:Username' and '{SmtpOptions.SectionName}:Password' travel as a pair or not at all.")
            .ValidateOnStart();

        return services
            .AddSingleton<IEmailSender, SmtpEmailSender>()
            // The presenter half of the same mechanics (ADR 0090, every notice since ADR 0091):
            // the consumer hands facts to this port and mails back whatever prose it answers.
            // Transient because the localizer it holds is, and it holds nothing else.
            .AddTransient<INotificationComposer, NotificationComposer>()
            .AddSingleton<IEmailTransportReachability, SmtpReachability>();
    }
}
