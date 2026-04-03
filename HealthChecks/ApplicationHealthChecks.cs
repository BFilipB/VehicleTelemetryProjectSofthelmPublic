using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.Infrastructure;

namespace VehicleTelemetryAPI.HealthChecks;

/// <summary>
/// Health check for database connectivity and performance.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly TelemetryDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(TelemetryDbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to execute a simple query
            var recordCount = await _context.TelemetryRecords.CountAsync(cancellationToken);

            _logger.LogDebug("Database health check passed. Record count: {RecordCount}", recordCount);

            return HealthCheckResult.Healthy($"Database is healthy. Contains {recordCount} records.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check failed.", ex);
        }
    }
}

/// <summary>
/// Health check for external service dependencies (cloud API).
/// Uses configurable thresholds to determine health status.
/// Requires minimum requests to make a meaningful decision.
/// </summary>
public class CloudSyncHealthCheck : IHealthCheck
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<CloudSyncHealthCheck> _logger;

    /// <summary>Success rate threshold for degraded status (70% = needs improvement)</summary>
    private const double DegradedSuccessRateThreshold = 70.0;

    /// <summary>Success rate threshold for unhealthy status (50% = critical)</summary>
    private const double UnhealthySuccessRateThreshold = 50.0;

    /// <summary>Minimum requests needed to make a health decision (avoid false positives)</summary>
    private const long MinimumRequestsForDecision = 10;

    public CloudSyncHealthCheck(IMetricsService metricsService, ILogger<CloudSyncHealthCheck> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = _metricsService.GetSnapshot();

            _logger.LogInformation(
                "Cloud sync health check - TotalRequests: {TotalRequests}, FailedRequests: {FailedRequests}, SuccessRate: {SuccessRate:F2}%",
                metrics.TotalRequests, metrics.FailedRequests, metrics.SuccessRate);

            // Not enough data to make a decision - don't fail on lack of data
            if (metrics.TotalRequests < MinimumRequestsForDecision)
            {
                _logger.LogWarning(
                    "Cloud sync has insufficient data for health decision: {TotalRequests} requests observed (minimum required: {MinimumRequests})",
                    metrics.TotalRequests, MinimumRequestsForDecision);
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Insufficient data to determine cloud sync health. Only {metrics.TotalRequests}/{MinimumRequestsForDecision} requests observed."));
            }

            // Unhealthy: Success rate critically low (< 50%)
            if (metrics.SuccessRate < UnhealthySuccessRateThreshold)
            {
                _logger.LogError(
                    "Cloud sync unhealthy: {SuccessRate:F2}% success rate (failed {FailedRequests}/{TotalRequests} requests)",
                    metrics.SuccessRate, metrics.FailedRequests, metrics.TotalRequests);
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Cloud sync service is critically unhealthy. Success rate: {metrics.SuccessRate:F2}%",
                    data: new Dictionary<string, object> 
                    { 
                        { "SuccessRate", $"{metrics.SuccessRate:F2}%" },
                        { "TotalRequests", metrics.TotalRequests },
                        { "FailedRequests", metrics.FailedRequests }
                    }));
            }

            // Degraded: Success rate concerning but not critical (50-70%)
            if (metrics.SuccessRate < DegradedSuccessRateThreshold)
            {
                _logger.LogWarning(
                    "Cloud sync degraded: {SuccessRate:F2}% success rate (failed {FailedRequests}/{TotalRequests} requests)",
                    metrics.SuccessRate, metrics.FailedRequests, metrics.TotalRequests);
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Cloud sync service is degraded. Success rate: {metrics.SuccessRate:F2}%",
                    data: new Dictionary<string, object> 
                    { 
                        { "SuccessRate", $"{metrics.SuccessRate:F2}%" },
                        { "TotalRequests", metrics.TotalRequests },
                        { "FailedRequests", metrics.FailedRequests }
                    }));
            }

            // Healthy: Success rate acceptable (>= 70%)
            _logger.LogDebug(
                "Cloud sync healthy: {SuccessRate:F2}% success rate ({SuccessfulRequests}/{TotalRequests} requests)",
                metrics.SuccessRate, metrics.TotalRequests - metrics.FailedRequests, metrics.TotalRequests);
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Cloud sync service is healthy. Success rate: {metrics.SuccessRate:F2}%",
                data: new Dictionary<string, object> 
                { 
                    { "SuccessRate", $"{metrics.SuccessRate:F2}%" },
                    { "TotalRequests", metrics.TotalRequests },
                    { "FailedRequests", metrics.FailedRequests }
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud sync health check threw an exception");
            return Task.FromResult(HealthCheckResult.Unhealthy("Cloud sync health check failed.", ex));
        }
    }
}

/// <summary>
/// Aggregate health check combining all dependencies.
/// </summary>
public class ApplicationHealthCheck : IHealthCheck
{
    private readonly ILogger<ApplicationHealthCheck> _logger;
    private readonly IEnumerable<IHealthCheck> _checks;

    public ApplicationHealthCheck(
        ILogger<ApplicationHealthCheck> logger,
        DatabaseHealthCheck databaseCheck,
        CloudSyncHealthCheck cloudSyncCheck)
    {
        _logger = logger;
        _checks = new[] { databaseCheck as IHealthCheck, cloudSyncCheck };
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HealthCheckResult>();
        var allHealthy = true;

        foreach (var check in _checks)
        {
            try
            {
                var checkContext = new HealthCheckContext();
                var result = await check.CheckHealthAsync(checkContext, cancellationToken);

                var checkName = check.GetType().Name;
                results[checkName] = result;

                if (result.Status != HealthStatus.Healthy)
                {
                    allHealthy = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running health check");
                allHealthy = false;
            }
        }

        var status = allHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        var description = allHealthy ? "All checks passed" : "One or more checks failed";

        _logger.LogDebug("Application health check completed with status: {Status}", status);

        return new HealthCheckResult(status, description, data: results.ToDictionary(x => x.Key, x => (object)x.Value));
    }
}
