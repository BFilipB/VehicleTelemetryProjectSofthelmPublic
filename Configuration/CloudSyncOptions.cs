namespace VehicleTelemetryAPI.Configuration;

/// <summary>
/// Configuration options for cloud synchronization background service.
/// Allows runtime configuration without code changes or recompilation.
/// Enterprise-grade configuration management.
/// </summary>
public class CloudSyncOptions
{
    /// <summary>
    /// Gets or sets the sync interval in seconds. Default: 60 seconds.
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the number of records to sync per device. Default: 5.
    /// </summary>
    public int RecordsPerDevicePerSync { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of retries on failure. Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the timeout in seconds for each sync operation. Default: 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether the cloud sync service is enabled. Default: true.
    /// Can be used to disable sync without redeploying.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum batch size for parallel processing. Default: 10.
    /// Limits concurrent cloud sync operations to prevent overwhelming the cloud service.
    /// </summary>
    public int MaxParallelDevices { get; set; } = 10;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <returns>Exception if configuration is invalid, null otherwise.</returns>
    public Exception? Validate()
    {
        if (SyncIntervalSeconds < 10)
            return new ArgumentException("SyncIntervalSeconds must be at least 10 seconds");

        if (RecordsPerDevicePerSync < 1)
            return new ArgumentException("RecordsPerDevicePerSync must be at least 1");

        if (MaxRetries < 0)
            return new ArgumentException("MaxRetries cannot be negative");

        if (TimeoutSeconds < 5)
            return new ArgumentException("TimeoutSeconds must be at least 5 seconds");

        if (MaxParallelDevices < 1)
            return new ArgumentException("MaxParallelDevices must be at least 1");

        return null;
    }
}
