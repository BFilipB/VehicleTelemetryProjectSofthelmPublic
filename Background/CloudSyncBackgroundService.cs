using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog.Context;
using VehicleTelemetryAPI.Configuration;
using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.Infrastructure;
using VehicleTelemetryAPI.Models;

namespace VehicleTelemetryAPI.Background;

/// <summary>
/// Enterprise-grade background service that syncs telemetry data to the cloud.
/// Features:
/// - Configurable via CloudSyncOptions
/// - Production-grade resilience patterns (retry, circuit breaker, bulkhead)
/// - Correlation ID tracking for distributed tracing
/// - Comprehensive metrics and monitoring
/// - Graceful error handling and logging
/// - Feature flagging support
/// </summary>
public class CloudSyncBackgroundService : BackgroundService
{
    private readonly ILogger<CloudSyncBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<CloudSyncOptions> _options;
    private readonly IMetricsService _metricsService;
    private readonly IResiliencePolicyFactory _policyFactory;

    public CloudSyncBackgroundService(
        ILogger<CloudSyncBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptionsMonitor<CloudSyncOptions> options,
        IMetricsService metricsService,
        IResiliencePolicyFactory policyFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _policyFactory = policyFactory ?? throw new ArgumentNullException(nameof(policyFactory));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validate configuration on startup
        var validationError = _options.CurrentValue.Validate();
        if (validationError != null)
        {
            _logger.LogError(validationError, "Invalid CloudSyncOptions configuration");
            return;
        }

        // Check if feature is enabled
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Cloud sync background service is disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "Cloud sync background service started with interval: {IntervalSeconds}s, " +
            "records per device: {RecordsPerDevice}, max retries: {MaxRetries}",
            _options.CurrentValue.SyncIntervalSeconds,
            _options.CurrentValue.RecordsPerDevicePerSync,
            _options.CurrentValue.MaxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Generate unique correlation ID for this sync cycle
                var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("Operation", "CloudSync"))
                {
                    var stopwatch = Stopwatch.StartNew();

                    await SyncDataToCloudAsync(stoppingToken);

                    stopwatch.Stop();

                    _logger.LogInformation(
                        "Cloud sync cycle completed in {ElapsedMilliseconds}ms",
                        stopwatch.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cloud sync background service cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in cloud sync background service");
            }

            // Wait for configured interval before next sync
            var delay = TimeSpan.FromSeconds(_options.CurrentValue.SyncIntervalSeconds);
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("Cloud sync background service stopped");
    }

    private async Task SyncDataToCloudAsync(CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITelemetryRepository>();
            var context = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            // Get all unique device IDs that have telemetry data
            var deviceIds = await context.TelemetryRecords
                .Select(t => t.DeviceId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (deviceIds.Count == 0)
            {
                _logger.LogInformation("No devices with telemetry data found for cloud sync");
                return;
            }

            _logger.LogInformation(
                "Starting cloud sync for {DeviceCount} devices",
                deviceIds.Count);

            // Process devices in parallel batches to improve performance
            var successCount = 0;
            var failureCount = 0;

            var batchSize = _options.CurrentValue.MaxParallelDevices;
            for (int i = 0; i < deviceIds.Count; i += batchSize)
            {
                var batch = deviceIds.Skip(i).Take(batchSize).ToList();

                var tasks = batch.Select(async deviceId =>
                {
                    try
                    {
                        await SyncDeviceAsync(
                            deviceId,
                            repository,
                            cancellationToken);
                        Interlocked.Increment(ref successCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to sync device {DeviceId}",
                            deviceId);
                        Interlocked.Increment(ref failureCount);
                    }
                });

                await Task.WhenAll(tasks);
            }

            _logger.LogInformation(
                "Cloud sync completed: {SuccessCount} successful, {FailureCount} failed",
                successCount,
                failureCount);
        }
    }

    private async Task SyncDeviceAsync(
        Guid deviceId,
        ITelemetryRepository repository,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using (LogContext.PushProperty("DeviceId", deviceId))
            {
                var records = await repository.GetLatestTelemetryRecordsAsync(
                    deviceId,
                    _options.CurrentValue.RecordsPerDevicePerSync);

                if (!records.Any())
                {
                    _logger.LogDebug("No records to sync for device");
                    return;
                }

                // Create comprehensive resilience policy (retry + circuit breaker + bulkhead)
                var syncPolicy = _policyFactory.CreateExternalServicePolicy<bool>("CloudSyncAPI", _metricsService);

                // Execute the cloud sync with production-grade resilience policy
                var success = await syncPolicy.ExecuteAsync(
                    async () =>
                    {
                        await SimulateCloudSyncAsync(deviceId, records, cancellationToken);
                        return true;
                    });

                stopwatch.Stop();

                _metricsService.RecordCloudSyncAttempt(
                    success ? "success" : "failure",
                    stopwatch.ElapsedMilliseconds);

                _logger.LogInformation(
                    "Successfully synced {RecordCount} records to cloud in {ElapsedMilliseconds}ms. " +
                    "Timestamps: {Timestamps}",
                    records.Count,
                    stopwatch.ElapsedMilliseconds,
                    string.Join(", ", records.Select(r => r.Timestamp.ToString("O"))));
            }
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            _logger.LogError(
                "Cloud sync timeout after {TimeoutSeconds}s (elapsed: {ElapsedMilliseconds}ms)",
                _options.CurrentValue.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task SimulateCloudSyncAsync(
        Guid deviceId,
        List<TelemetryRecord> records,
        CancellationToken cancellationToken)
    {
        // Simulate cloud API call with configurable timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.CurrentValue.TimeoutSeconds));

        // In a real implementation, this would be an actual HTTP call to your cloud service
        await Task.Delay(100, cts.Token); // Simulate network call

        _logger.LogDebug(
            "Simulated cloud sync for device {DeviceId} with {RecordCount} records",
            deviceId,
            records.Count);
    }
}
