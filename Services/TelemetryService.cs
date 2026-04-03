using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.DTOs;
using VehicleTelemetryAPI.Models;

namespace VehicleTelemetryAPI.Services;

/// <summary>
/// Service implementation for telemetry business logic operations.
/// 
/// Responsibilities:
/// - Orchestrates between controllers and data layer
/// - Transforms DTOs to domain models and vice versa
/// - Implements core business logic and validation
/// - Coordinates with repositories for data persistence
/// 
/// Pattern: Service Layer (Separation of Concerns)
/// This layer isolates business logic from HTTP concerns and data access details,
/// enabling easier testing, reusability, and maintenance.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryRepository _repository;
    private readonly ILogger<TelemetryService> _logger;

    /// <summary>
    /// Initializes a new instance of the TelemetryService.
    /// </summary>
    /// <param name="repository">The telemetry repository for data access operations.</param>
    /// <param name="logger">The logger for recording service operations and diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown if repository or logger is null.</exception>
    public TelemetryService(ITelemetryRepository repository, ILogger<TelemetryService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates and persists a new telemetry record asynchronously.
    /// 
    /// Operation flow:
    /// 1. Validate request is not null
    /// 2. Transform request DTO to domain model (TelemetryRecord)
    /// 3. Persist to database via repository
    /// 4. Log operation success
    /// 5. Transform model back to response DTO
    /// 
    /// This separation of DTOs allows the HTTP contract to evolve independently
    /// from the database schema and domain model.
    /// </summary>
    /// <param name="request">The telemetry record request DTO with sensor data.</param>
    /// <returns>The created telemetry record as a response DTO with persisted values.</returns>
    /// <exception cref="ArgumentNullException">Thrown if request is null.</exception>
    public async Task<TelemetryRecordResponse> CreateTelemetryRecordAsync(TelemetryRecordRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            // DTO → Domain Model transformation
            // The request DTO decouples the HTTP API contract from our domain model.
            // This allows the API to evolve without changing the database schema.
            var telemetryRecord = new TelemetryRecord
            {
                DeviceId = request.DeviceId,
                Timestamp = request.Timestamp,
                EngineRPM = request.EngineRPM,
                FuelLevelPercentage = request.FuelLevelPercentage,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            // Persist to database through the repository abstraction.
            // The repository handles EF Core operations, transactions, and error handling.
            await _repository.AddTelemetryRecordAsync(telemetryRecord);

            _logger.LogInformation("Telemetry record created successfully for device {DeviceId}", 
                request.DeviceId);

            // Domain Model → Response DTO transformation
            // The response DTO ensures the HTTP response is shaped according to API contract,
            // independent of how data is stored in the database.
            return MapToResponse(telemetryRecord);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Invalid argument when creating telemetry record");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating telemetry record");
            throw;
        }
    }

    /// <summary>
    /// Retrieves the latest telemetry record for a specific device asynchronously.
    /// 
    /// Queries the repository for the most recent record (ordered by timestamp descending).
    /// Returns null if no records exist for the device.
    /// 
    /// Performance considerations:
    /// - The repository uses AsNoTracking() since this is a read-only operation
    /// - Ordering happens at the database level (FirstOrDefault at DB, not in memory)
    /// - Suitable for frequent queries due to database optimization
    /// </summary>
    /// <param name="deviceId">The device ID to retrieve the latest record for.</param>
    /// <returns>The latest telemetry record as a response DTO, or null if none exists.</returns>
    public async Task<TelemetryRecordResponse?> GetLatestTelemetryRecordAsync(Guid deviceId)
    {
        try
        {
            // Query the repository for the latest record for this device
            // The repository handles the EF Core query and optimizations
            var record = await _repository.GetLatestTelemetryRecordAsync(deviceId);

            if (record == null)
            {
                _logger.LogInformation("No telemetry record found for device {DeviceId}", deviceId);
                return null;
            }

            // Transform the domain model to response DTO for HTTP response
            return MapToResponse(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest telemetry record for device {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves multiple recent telemetry records for a device asynchronously.
    /// 
    /// Returns up to 'count' records ordered by timestamp (newest first).
    /// Useful for time-series analysis, trending, or displaying recent history.
    /// 
    /// Validation:
    /// - count must be greater than 0 (throws ArgumentException otherwise)
    /// - Returns fewer records if fewer than 'count' exist for the device
    /// </summary>
    /// <param name="deviceId">The device ID to retrieve records for.</param>
    /// <param name="count">The maximum number of records to retrieve. Must be > 0.</param>
    /// <returns>A list of telemetry records (up to 'count'), ordered by timestamp descending.</returns>
    /// <exception cref="ArgumentException">Thrown if count is less than or equal to 0.</exception>
    public async Task<List<TelemetryRecordResponse>> GetLatestTelemetryRecordsAsync(Guid deviceId, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than 0", nameof(count));
        }

        try
        {
            // Query the repository for the latest 'count' records
            var records = await _repository.GetLatestTelemetryRecordsAsync(deviceId, count);

            // Transform all domain models to response DTOs
            // Using LINQ Select for functional transformation
            return records.Select(MapToResponse).ToList();
        }
        catch (ArgumentException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest telemetry records for device {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Maps a TelemetryRecord domain model to a TelemetryRecordResponse DTO.
    /// 
    /// This is a simple transformation that extracts the relevant fields from the domain model
    /// into the shape expected by HTTP clients. This separation allows:
    /// - Database schema to evolve independently of API contract
    /// - API contract to evolve independently of domain model
    /// - Clean separation of concerns between layers
    /// 
    /// Note: This is a static method because it has no dependencies on instance state,
    /// making it more testable and efficient.
    /// </summary>
    /// <param name="record">The domain model to transform.</param>
    /// <returns>A response DTO with the same data.</returns>
    private static TelemetryRecordResponse MapToResponse(TelemetryRecord record)
    {
        return new TelemetryRecordResponse
        {
            DeviceId = record.DeviceId,
            Timestamp = record.Timestamp,
            EngineRPM = record.EngineRPM,
            FuelLevelPercentage = record.FuelLevelPercentage,
            Latitude = record.Latitude,
            Longitude = record.Longitude
        };
    }
}
