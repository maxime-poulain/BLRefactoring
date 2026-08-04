using Serilog;
using Serilog.Events;

namespace TrainingHub.Shared.Api.Logging;

/// <summary>
/// Everything a deployment tunes about what the API hosts log, and where.
/// </summary>
/// <remarks>
/// Bound and validated by <c>LoggingExtensions</c>, not read ad hoc: a typo in a level name or an
/// empty path fails the host at start-up instead of surfacing as a log that quietly never appears.
/// The defaults are a working configuration on their own — a host with no
/// <see cref="SectionName"/> section logs to the console and to daily files under
/// <c>logs/</c> — so the section only needs to say what differs. See ADR 0026.
/// </remarks>
public sealed class ApiLoggingOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    /// <remarks>
    /// Deliberately neither <c>Logging</c> — the framework's own section, whose filter rules stop
    /// applying the moment Serilog replaces the logger factory — nor <c>Serilog</c>, the section
    /// <c>ReadFrom.Configuration</c> would read and ADR 0026 rejects. A name of its own keeps
    /// either mechanism from half-applying by accident.
    /// </remarks>
    public const string SectionName = "ApiLogging";

    /// <summary>
    /// Where the rolling file sink writes, relative to the host's working directory.
    /// </summary>
    /// <remarks>
    /// Serilog inserts the rolling period before the extension — the default writes
    /// <c>logs/traininghub-20260804.log</c> — so the value ends with the separator on purpose.
    /// </remarks>
    public string Path { get; set; } = "logs/traininghub-.log";

    /// <summary>
    /// How often the file sink starts a new file.
    /// </summary>
    public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;

    /// <summary>
    /// How many rolled files are kept before the oldest is deleted.
    /// </summary>
    /// <remarks>
    /// Retention is not only disk hygiene: in Development EF Core logs command parameters
    /// (<c>EnableSensitiveDataLogging</c>), so a file that never expires is personal data that
    /// never expires. A month covers any debugging session that could plausibly need history.
    /// </remarks>
    public int RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// The level below which nothing is logged anywhere.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Per-namespace overrides of <see cref="MinimumLevel"/>, keyed by source-context prefix.
    /// </summary>
    /// <remarks>
    /// The default restates what the hosts' old <c>Logging</c> section said — the framework's
    /// per-request narration is noise at Information — and <c>UseApiLogging</c> puts back the one
    /// line per request worth keeping.
    /// </remarks>
    public Dictionary<string, LogEventLevel> LevelOverrides { get; set; } = new(StringComparer.Ordinal)
    {
        ["Microsoft.AspNetCore"] = LogEventLevel.Warning,
    };

    /// <summary>
    /// Whether the file sink is attached at all.
    /// </summary>
    /// <remarks>
    /// Off where files are the wrong medium: a platform that captures stdout has no use for a
    /// second copy on an ephemeral disk, and the integration suite turns it off because its hosts
    /// run in Development — where the sensitive-data logging above would persist test data to disk
    /// — and because parallel fixtures would race for the same relative path.
    /// </remarks>
    public bool WriteToFile { get; set; } = true;
}
