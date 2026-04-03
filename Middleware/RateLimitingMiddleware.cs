using System.Collections.Concurrent;
using VehicleTelemetryAPI.Infrastructure;

namespace VehicleTelemetryAPI.Middleware;

/// <summary>
/// Rate limiting middleware to prevent API abuse and DDoS attacks.
/// Enterprise-level security for production APIs.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IMetricsService _metricsService;
    private const int MaxRequestsPerMinute = 100;
    private static readonly ConcurrentDictionary<string, RequestHistory> ClientRequests = new();

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IMetricsService metricsService)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);

        if (IsRateLimited(clientId))
        {
            _metricsService.RecordRateLimitEvent(true);

            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId}. Max {MaxRequests} requests per minute allowed.",
                clientId,
                MaxRequestsPerMinute);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = $"Maximum {MaxRequestsPerMinute} requests per minute allowed.",
                retryAfterSeconds = 60
            });

            return;
        }

        _metricsService.RecordRateLimitEvent(false);
        await _next(context);
    }

    private static bool IsRateLimited(string clientId)
    {
        var now = DateTime.UtcNow;

        var history = ClientRequests.AddOrUpdate(
            clientId,
            new RequestHistory { WindowStart = now, RequestCount = 1 },
            (key, existing) =>
            {
                // Reset if window has passed
                if ((now - existing.WindowStart).TotalMinutes >= 1)
                {
                    return new RequestHistory { WindowStart = now, RequestCount = 1 };
                }

                existing.RequestCount++;
                return existing;
            });

        return history.RequestCount > MaxRequestsPerMinute;
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try to get from X-Forwarded-For header (proxy/load balancer)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var forwardedIp = forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(forwardedIp))
            {
                return forwardedIp;
            }
        }

        // Fall back to remote IP address
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private class RequestHistory
    {
        public DateTime WindowStart { get; set; }
        public int RequestCount { get; set; }
    }
}
