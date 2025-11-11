using System;

namespace Parlot.Tracing;

/// <summary>
/// Provides static methods for enabling parser tracing.
/// </summary>
public static class ParserTracing
{
    /// <summary>
    /// Starts tracing for the specified parse context.
    /// </summary>
    /// <param name="context">The parse context to trace.</param>
    /// <param name="options">Optional tracing configuration.</param>
    /// <returns>A disposable scope that stops tracing when disposed.</returns>
    public static TracingScope Start(Fluent.ParseContext context, TracingOptions? options = null)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var tracer = new FirefoxProfilerTracer(options);
        return new TracingScope(context, tracer);
    }
}

/// <summary>
/// Represents a tracing session that can be disposed to stop tracing and retrieve results.
/// </summary>
public sealed class TracingScope : IDisposable
{
    private readonly Fluent.ParseContext _context;
    private readonly FirefoxProfilerTracer _tracer;
    private bool _disposed;

    internal TracingScope(Fluent.ParseContext context, FirefoxProfilerTracer tracer)
    {
        _context = context;
        _tracer = tracer;
        _context.Tracer = tracer;
    }

    /// <summary>
    /// Gets the Speedscope JSON export of the collected trace data.
    /// </summary>
    /// <returns>JSON string compatible with Speedscope (https://www.speedscope.app/).</returns>
    public string GetSpeedscopeJson()
    {
        return _tracer.ExportSpeedscope();
    }

    /// <summary>
    /// Gets the Firefox Profiler JSON export of the collected trace data.
    /// </summary>
    /// <returns>JSON string compatible with Firefox Profiler.</returns>
    [Obsolete("Firefox Profiler format has compatibility issues. Use GetSpeedscopeJson() instead.")]
    public string GetFirefoxProfilerJson()
    {
        return _tracer.ExportFirefoxProfiler();
    }

    /// <summary>
    /// Stops tracing and removes the tracer from the parse context.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _context.Tracer = null;
            _disposed = true;
        }
    }
}
