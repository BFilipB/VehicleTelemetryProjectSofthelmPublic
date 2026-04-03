using System.Diagnostics;
using Prometheus;

namespace VehicleTelemetryAPI.Infrastructure;

/// <summary>
/// Metrics collection service for monitoring API performance.
/// Uses Prometheus format for compatibility with standard monitoring stacks.
/// </summary>
public interface IMetricsService
{
    /// <summary>Record a telemetry record creation attempt</summary>
    void RecordTelemetryCreation(bool success, long durationMs);

    /// <summary>Record a telemetry record retrieval attempt</summary>
    void RecordTelemetryRetrieval(bool success, long durationMs);

    /// <summary>Record a cloud sync attempt</summary>
    void RecordCloudSyncAttempt(string status, long durationMs);

    /// <summary>Record when circuit breaker state changes</summary>
    void RecordCircuitBreakerStateChange(string serviceName, string state);

    /// <summary>Record rate limiting events</summary>
    void RecordRateLimitEvent(bool limited);

    /// <summary>Get current metric values for health checks</summary>
    MetricsSnapshot GetSnapshot();
}

/// <summary>
/// Represents current metrics snapshot for health monitoring
/// </summary>
public class MetricsSnapshot
{
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public double SuccessRate => TotalRequests > 0 ? (double)(TotalRequests - FailedRequests) / TotalRequests * 100 : 100;
    public long RateLimitedRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
}

/// <summary>
/// Prometheus-based metrics implementation
/// </summary>
public class PrometheusMetricsService : IMetricsService
{
    private readonly Counter _telemetryCreatedTotal;
    private readonly Counter _telemetryCreatedErrorsTotal;
    private readonly Histogram _telemetryCreationDuration;

    private readonly Counter _telemetryRetrievedTotal;
    private readonly Counter _telemetryRetrievedErrorsTotal;
    private readonly Histogram _telemetryRetrievalDuration;

    private readonly Counter _cloudSyncAttemptsTotal;
    private readonly Gauge _cloudSyncSuccessRate;
    private readonly Histogram _cloudSyncDuration;

    private readonly Gauge _circuitBreakerState;
    private readonly Counter _rateLimitEventsTotal;

    private readonly ILogger<PrometheusMetricsService> _logger;

    // For snapshots
    private long _totalRequests;
    private long _totalErrors;
    private long _totalRateLimited;
    private readonly Queue<long> _recentDurations = new(capacity: 100);

    public PrometheusMetricsService(ILogger<PrometheusMetricsService> logger)
    {
        _logger = logger;

        // Telemetry Creation Metrics
        _telemetryCreatedTotal = Metrics.CreateCounter(
            "telemetry_created_total",
            "Total telemetry records created",
            new CounterConfiguration { LabelNames = new[] { "status" } });

        _telemetryCreatedErrorsTotal = Metrics.CreateCounter(
            "telemetry_creation_errors_total",
            "Total telemetry creation errors");

        _telemetryCreationDuration = Metrics.CreateHistogram(
            "telemetry_creation_duration_ms",
            "Time taken to create telemetry record (ms)",
            new HistogramConfiguration { Buckets = new double[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000 } });

        // Telemetry Retrieval Metrics
        _telemetryRetrievedTotal = Metrics.CreateCounter(
            "telemetry_retrieved_total",
            "Total telemetry records retrieved",
            new CounterConfiguration { LabelNames = new[] { "status" } });

        _telemetryRetrievedErrorsTotal = Metrics.CreateCounter(
            "telemetry_retrieval_errors_total",
            "Total telemetry retrieval errors");

        _telemetryRetrievalDuration = Metrics.CreateHistogram(
            "telemetry_retrieval_duration_ms",
            "Time taken to retrieve telemetry record (ms)",
            new HistogramConfiguration { Buckets = new double[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000 } });

        // Cloud Sync Metrics
        _cloudSyncAttemptsTotal = Metrics.CreateCounter(
            "cloud_sync_attempts_total",
            "Total cloud sync attempts",
            new CounterConfiguration { LabelNames = new[] { "status" } });

        _cloudSyncSuccessRate = Metrics.CreateGauge(
            "cloud_sync_success_rate",
            "Cloud sync success rate percentage");

        _cloudSyncDuration = Metrics.CreateHistogram(
            "cloud_sync_duration_ms",
            "Time taken for cloud sync (ms)",
            new HistogramConfiguration { Buckets = new double[] { 100, 500, 1000, 5000, 10000 } });

        // Circuit Breaker Metrics
        _circuitBreakerState = Metrics.CreateGauge(
            "circuit_breaker_state",
            "Circuit breaker state (0=closed, 1=open, 2=half-open)",
            new GaugeConfiguration { LabelNames = new[] { "service" } });

        // Rate Limiting Metrics
        _rateLimitEventsTotal = Metrics.CreateCounter(
            "rate_limit_events_total",
            "Total rate limiting events",
            new CounterConfiguration { LabelNames = new[] { "action" } });

        _logger.LogInformation("Prometheus metrics initialized");
    }

    public void RecordTelemetryCreation(bool success, long durationMs)
    {
        _telemetryCreatedTotal.WithLabels(success ? "success" : "failure").Inc();

        if (!success)
        {
            _telemetryCreatedErrorsTotal.Inc();
            _totalErrors++;
        }

        _telemetryCreationDuration.Observe(durationMs);
        _totalRequests++;
        _recentDurations.Enqueue(durationMs);

        if (_recentDurations.Count > 100)
            _recentDurations.Dequeue();

        _logger.LogDebug("Telemetry creation recorded: success={Success}, duration={Duration}ms", success, durationMs);
    }

    public void RecordTelemetryRetrieval(bool success, long durationMs)
    {
        _telemetryRetrievedTotal.WithLabels(success ? "success" : "failure").Inc();

        if (!success)
        {
            _telemetryRetrievedErrorsTotal.Inc();
            _totalErrors++;
        }

        _telemetryRetrievalDuration.Observe(durationMs);
        _totalRequests++;
        _recentDurations.Enqueue(durationMs);

        if (_recentDurations.Count > 100)
            _recentDurations.Dequeue();

        _logger.LogDebug("Telemetry retrieval recorded: success={Success}, duration={Duration}ms", success, durationMs);
    }

    public void RecordCloudSyncAttempt(string status, long durationMs)
    {
        _cloudSyncAttemptsTotal.WithLabels(status).Inc();
        _cloudSyncDuration.Observe(durationMs);

        // Increment total requests counter for health check decision
        _totalRequests++;

        // Track errors for success rate calculation
        if (status != "success")
        {
            _totalErrors++;
        }

        _logger.LogDebug("Cloud sync recorded: status={Status}, duration={Duration}ms, TotalRequests={TotalRequests}", 
            status, durationMs, _totalRequests);
    }

    public void RecordCircuitBreakerStateChange(string serviceName, string state)
    {
        var stateValue = state switch
        {
            "Closed" => 0,
            "Open" => 1,
            "HalfOpen" => 2,
            _ => 0
        };

        _circuitBreakerState.WithLabels(serviceName).Set(stateValue);
        _logger.LogWarning("Circuit breaker state changed: service={Service}, state={State}", serviceName, state);
    }

    public void RecordRateLimitEvent(bool limited)
    {
        _rateLimitEventsTotal.WithLabels(limited ? "limited" : "allowed").Inc();

        if (limited)
            _totalRateLimited++;

        _logger.LogDebug("Rate limit event recorded: limited={Limited}", limited);
    }

    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            TotalRequests = _totalRequests,
            FailedRequests = _totalErrors,
            RateLimitedRequests = _totalRateLimited,
            AverageResponseTimeMs = _recentDurations.Count > 0 ? _recentDurations.Average() : 0
        };
    }
}
