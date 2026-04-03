using VehicleTelemetryAPI.DTOs;

namespace VehicleTelemetryAPI.Services;

/// <summary>
/// Service interface for telemetry operations.
/// 
/// Defines the contract for business logic layer operations on telemetry data.
/// Implementations handle request validation, DTO transformation, and repository delegation.
/// All methods are asynchronous to support scalability in production environments.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Creates and stores a new telemetry record asynchronously.
    /// 
    /// Validates the incoming request, transforms the DTO to a domain model,
    /// persists it via the repository, and returns the created record as a response DTO.
    /// 
    /// Flow: Request DTO → Validation → Domain Model → Repository → Response DTO
    /// </summary>
    /// <param name="request">The telemetry record request DTO containing vehicle sensor data.</param>
    /// <returns>The created telemetry record as a response DTO with all persisted values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="ArgumentException">Thrown when request contains invalid data.</exception>
    Task<TelemetryRecordResponse> CreateTelemetryRecordAsync(TelemetryRecordRequest request);

    /// <summary>
    /// Retrieves the latest telemetry record for a device asynchronously.
    /// 
    /// Queries the repository for the most recent record (by timestamp) for the given device ID.
    /// Uses database-level ordering for performance (not in-memory sorting).
    /// </summary>
    /// <param name="deviceId">The device ID (GUID) to search for. Must not be Guid.Empty.</param>
    /// <returns>The latest telemetry record response DTO, or null if no records exist for the device.</returns>
    /// <remarks>
    /// Performance note: The repository uses AsNoTracking() and FirstOrDefault() at the DB level
    /// for optimal query performance. This is a read-only operation that doesn't modify state.
    /// </remarks>
    Task<TelemetryRecordResponse?> GetLatestTelemetryRecordAsync(Guid deviceId);

    /// <summary>
    /// Retrieves the latest N telemetry records for a device asynchronously.
    /// 
    /// Returns up to 'count' records ordered by timestamp (newest first).
    /// Useful for retrieving a time series of recent sensor data.
    /// </summary>
    /// <param name="deviceId">The device ID (GUID) to search for. Must not be Guid.Empty.</param>
    /// <param name="count">The number of records to retrieve. Must be greater than 0.</param>
    /// <returns>A list of telemetry record response DTOs, ordered by timestamp descending (newest first).</returns>
    /// <exception cref="ArgumentException">Thrown when count is less than or equal to 0.</exception>
    /// <remarks>
    /// Returns fewer records if fewer than 'count' records exist for the device.
    /// </remarks>
    Task<List<TelemetryRecordResponse>> GetLatestTelemetryRecordsAsync(Guid deviceId, int count);
}
