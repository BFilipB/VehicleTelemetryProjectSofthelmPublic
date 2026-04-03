using VehicleTelemetryAPI.Models;
using VehicleTelemetryAPI.DTOs.Common;

namespace VehicleTelemetryAPI.Data;

/// <summary>
/// Repository interface for telemetry record data access operations.
/// Includes pagination, filtering, and statistical queries for production-level data access.
/// </summary>
public interface ITelemetryRepository
{
    /// <summary>
    /// Adds a new telemetry record to the database asynchronously.
    /// </summary>
    /// <param name="record">The telemetry record to add.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddTelemetryRecordAsync(TelemetryRecord record);

    /// <summary>
    /// Retrieves the latest telemetry record for a specific device asynchronously.
    /// </summary>
    /// <param name="deviceId">The device ID to search for.</param>
    /// <returns>The latest telemetry record for the device, or null if none exists.</returns>
    Task<TelemetryRecord?> GetLatestTelemetryRecordAsync(Guid deviceId);

    /// <summary>
    /// Retrieves the latest N telemetry records for a specific device asynchronously.
    /// </summary>
    /// <param name="deviceId">The device ID to search for.</param>
    /// <param name="count">The number of records to retrieve.</param>
    /// <returns>A list of the latest telemetry records for the device.</returns>
    Task<List<TelemetryRecord>> GetLatestTelemetryRecordsAsync(Guid deviceId, int count);
    
    /// <summary>
    /// Retrieves paginated telemetry records for a device with filtering support.
    /// </summary>
    /// <param name="deviceId">The device ID to search for.</param>
    /// <param name="request">Pagination and filtering parameters.</param>
    /// <returns>A paginated response containing filtered telemetry records.</returns>
    Task<PaginatedResponse<TelemetryRecord>> GetTelemetryRecordsByDeviceAsync(
        Guid deviceId,
        FilterRequest request);
    
    /// <summary>
    /// Retrieves the oldest telemetry record for a specific device.
    /// </summary>
    /// <param name="deviceId">The device ID to search for.</param>
    /// <returns>The oldest telemetry record, or null if none exists.</returns>
    Task<TelemetryRecord?> GetOldestTelemetryRecordAsync(Guid deviceId);

    /// <summary>
    /// Calculates fuel level statistics for a device.
    /// </summary>
    /// <param name="deviceId">The device ID to analyze.</param>
    /// <returns>A tuple containing average, minimum, and maximum fuel levels.</returns>
    Task<(decimal Average, decimal Min, decimal Max)> GetFuelLevelStatisticsAsync(Guid deviceId);

    /// <summary>
    /// Calculates engine RPM statistics for a device.
    /// </summary>
    /// <param name="deviceId">The device ID to analyze.</param>
    /// <returns>A tuple containing average, minimum, and maximum engine RPM values.</returns>
    Task<(int Average, int Min, int Max)> GetEngineRPMStatisticsAsync(Guid deviceId);

    /// <summary>
    /// Gets the total count of telemetry records for a specific device.
    /// </summary>
    /// <param name="deviceId">The device ID to count records for.</param>
    /// <returns>The total number of telemetry records for the device.</returns>
    Task<int> GetTotalRecordsCountAsync(Guid deviceId);
}
