using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Serilog.Context;

namespace VehicleTelemetryAPI.Infrastructure;

/// <summary>
/// Factory for creating production-grade resilience policies using Polly.
/// Combines retry, circuit breaker, and bulkhead isolation patterns.
/// </summary>
public interface IResiliencePolicyFactory
{
    /// <summary>
    /// Creates a comprehensive policy with retry + circuit breaker + bulkhead.
    /// Suitable for external service calls (cloud sync, external APIs).
    /// </summary>
    IAsyncPolicy<T> CreateExternalServicePolicy<T>(
        string serviceName,
        IMetricsService metricsService);

    /// <summary>
    /// Creates a database access policy with retry logic.
    /// Suitable for transient database failures.
    /// </summary>
    IAsyncPolicy<T> CreateDatabasePolicy<T>();
}

/// <summary>
/// Polly-based resilience policy factory
/// </summary>
public class PollyResiliencePolicyFactory : IResiliencePolicyFactory
{
    private readonly ILogger<PollyResiliencePolicyFactory> _logger;

    public PollyResiliencePolicyFactory(ILogger<PollyResiliencePolicyFactory> logger)
    {
        _logger = logger;
    }

    public IAsyncPolicy<T> CreateExternalServicePolicy<T>(
        string serviceName,
        IMetricsService metricsService)
    {
        // Retry policy: exponential backoff (2s, 4s, 8s, 16s)
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .OrResult<T>(r => IsTransientFailure(r))
            .WaitAndRetryAsync(
                retryCount: 4,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Service {ServiceName} retry {RetryCount}/4 after {DelaySeconds}s",
                        serviceName, retryCount, timespan.TotalSeconds);
                });

        // Circuit breaker: opens after 3 failures, stays open for 30 seconds
        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .OrResult<T>(r => IsTransientFailure(r))
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: async (outcome, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {ServiceName}. Will retry after {Seconds}s",
                        serviceName, duration.TotalSeconds);

                    metricsService.RecordCircuitBreakerStateChange(serviceName, "Open");
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for {ServiceName}", serviceName);
                    metricsService.RecordCircuitBreakerStateChange(serviceName, "Closed");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open for {ServiceName}. Testing recovery.", serviceName);
                    metricsService.RecordCircuitBreakerStateChange(serviceName, "HalfOpen");
                });

        // Bulkhead isolation: max 10 parallel requests, queue up to 20 more
        var bulkheadPolicy = Policy.BulkheadAsync<T>(
            maxParallelization: 10,
            maxQueuingActions: 20,
            onBulkheadRejectedAsync: context =>
            {
                _logger.LogWarning(
                    "Bulkhead policy rejected request for {ServiceName}. " +
                    "Max parallel reached. Queuing or rejecting.",
                    serviceName);
                return Task.CompletedTask;
            });

        // Combine policies: bulkhead → circuit breaker → retry
        // Order matters: innermost executed first during happy path
        var combinedPolicy = Policy.WrapAsync(
            bulkheadPolicy,
            circuitBreakerPolicy,
            retryPolicy);

        _logger.LogInformation(
            "Created resilience policy for {ServiceName} with retry(4), " +
            "circuit breaker(3 failures, 30s open), bulkhead(10 parallel, 20 queue)",
            serviceName);

        return combinedPolicy;
    }

    public IAsyncPolicy<T> CreateDatabasePolicy<T>()
    {
        // Database failures are less common but deserve retry
        var retryPolicy = Policy
            .Handle<TimeoutException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("deadlock"))
            .OrResult<T>(r => IsTransientFailure(r))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Database operation retry {RetryCount}/3 after {DelayMs}ms",
                        retryCount, timespan.TotalMilliseconds);
                });

        _logger.LogInformation("Created resilience policy for database with retry(3)");
        return retryPolicy;
    }

    /// <summary>
    /// Determines if a failure is transient (should be retried)
    /// </summary>
    private static bool IsTransientFailure<T>(T result)
    {
        // This would need to be customized based on your response type
        // For now, assume all results are successful (implement logic as needed)
        return false;
    }
}
