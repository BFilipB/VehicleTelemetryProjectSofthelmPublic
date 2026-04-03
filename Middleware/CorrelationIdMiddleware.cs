using System.Diagnostics;
using Serilog.Context;

namespace VehicleTelemetryAPI.Middleware;

/// <summary>
/// Middleware for adding correlation IDs to trace requests across services.
/// Enterprise-level tracing for distributed systems.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";
    private const string CorrelationIdLogPropertyName = "CorrelationId";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;

        // Get correlation ID from header or create new one
        var correlationId = ExtractOrCreateCorrelationId(context);

        // Add to context items for later retrieval
        context.Items[CorrelationIdLogPropertyName] = correlationId;

        // Add to response headers for client tracking
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            }
            return Task.CompletedTask;
        });

        // Push to Serilog context for structured logging
        using (LogContext.PushProperty(CorrelationIdLogPropertyName, correlationId))
        {
            _logger.LogInformation(
                "Request started: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            try
            {
                await _next(context);
            }
            finally
            {
                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Request completed: {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Response.StatusCode,
                    elapsed.TotalMilliseconds);
            }
        }
    }

    private string ExtractOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID is in request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationIdValues))
        {
            var correlationId = correlationIdValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }
        }

        // Use Activity.Id if available (for distributed tracing), otherwise create new ID
        var newCorrelationId = Activity.Current?.Id ?? context.TraceIdentifier;
        return newCorrelationId;
    }
}
