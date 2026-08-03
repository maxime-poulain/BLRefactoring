using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// Gives a test access to what the host logged while it was failing.
/// </summary>
/// <remarks>
/// A 500 is deliberately opaque to a caller — <c>UnhandledExceptionHandler</c> answers "the request
/// could not be processed" and puts the exception in the log, which is the right thing to publish
/// and the wrong thing to debug from. In this suite the host is in the same process as the test, so
/// the log is right there; without this it was being thrown away, and a failing assertion said
/// `500` and nothing else. That costs one full CI run per hypothesis.
/// </remarks>
public interface IServerErrorSource
{
    /// <summary>
    /// Everything the host has logged at <see cref="LogLevel.Error"/> or above, oldest first.
    /// </summary>
    IReadOnlyList<string> ServerErrors { get; }
}

/// <summary>
/// An <see cref="ILoggerProvider"/> that keeps errors where a test can read them.
/// </summary>
/// <param name="sink">Where the entries go.</param>
internal sealed class RecordingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, sink);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Errors only. Everything below is the host narrating a successful request, and a test that
        // has to read a thousand lines to find one is no better off than a test that read none.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);

            // The exception, not just its message: the interesting part of a storage failure is
            // usually two frames down, in whatever the SDK wrapped.
            sink.Enqueue(exception is null ? $"{category}: {message}" : $"{category}: {message}\n{exception}");
        }
    }
}
