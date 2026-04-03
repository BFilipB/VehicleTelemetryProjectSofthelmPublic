using Microsoft.EntityFrameworkCore;
using VehicleTelemetryAPI.Models;
using VehicleTelemetryAPI.DTOs.Common;

namespace VehicleTelemetryAPI.Data;

/// <summary>
/// Repository implementation for telemetry record data access operations.
/// 
/// Responsibilities:
/// - Abstracts Entity Framework Core operations
/// - Provides clean data access interface for the service layer
/// - Implements query optimizations (AsNoTracking, FirstOrDefaultAsync, etc.)
/// - Handles database exceptions and logging
/// - Coordinates with TelemetryDbContext for persistence
/// 
/// Pattern: Repository Pattern
/// This pattern creates an abstraction between the data mapping layer and business logic layer,
/// centralizing all database access in one place for easier testing and maintenance.
/// 
/// Performance Considerations:
/// - Uses AsNoTracking() for read-only queries (no entity tracking overhead)
/// - Executes ordering and filtering at database level (not in-memory)
/// - Uses FirstOrDefault/Take at DB level instead of loading all data then filtering in memory
/// </summary>
public class TelemetryRepository : ITelemetryRepository
{
    private readonly TelemetryDbContext _context;
    private readonly ILogger<TelemetryRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TelemetryRepository.
    /// </summary>
    /// <param name="context">The Entity Framework Core database context.</param>
    /// <param name="logger">The logger for recording data access operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public TelemetryRepository(TelemetryDbContext context, ILogger<TelemetryRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds a new telemetry record to the database asynchronously.
    /// 
    /// Operation flow:
    /// 1. Validate record is not null
    /// 2. Add record to DbSet
    /// 3. Call SaveChangesAsync() to persist to database
    /// 4. Log the operation
    /// 
    /// Note: The database will assign the Id field on successful persistence.
    /// </summary>
    /// <param name="record">The telemetry record to persist.</param>
    /// <exception cref="ArgumentNullException">Thrown if record is null.</exception>
    /// <exception cref="DbUpdateException">Thrown if database write fails.</exception>
    public async Task AddTelemetryRecordAsync(TelemetryRecord record)
    {
        if (record == null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        try
        {
            // Add the record to the DbSet (in-memory change tracking)
            _context.TelemetryRecords.Add(record);

            // Persist changes to the database asynchronously
            // SaveChangesAsync() executes the INSERT statement and assigns the Id
            await _context.SaveChangesAsync();

            _logger.LogInformation("Telemetry record added for device {DeviceId} at {Timestamp}", 
                record.DeviceId, record.Timestamp);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while adding telemetry record for device {DeviceId}", 
                record.DeviceId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the latest (most recent by timestamp) telemetry record for a device asynchronously.
    /// 
    /// Query optimization:
    /// - Uses AsNoTracking() because this is a read-only query
    ///   (no need for EF Core to track the entity for changes)
    /// - Orders by Timestamp descending and uses FirstOrDefaultAsync()
    ///   (filtering/ordering happens at DB level, not in-memory)
    /// - Returns null if no records exist for the device
    /// 
    /// Suitable for frequent queries due to database optimization.
    /// </summary>
    /// <param name="deviceId">The device ID (GUID) to search for.</param>
    /// <returns>The latest telemetry record for the device, or null if none exists.</returns>
    public async Task<TelemetryRecord?> GetLatestTelemetryRecordAsync(Guid deviceId)
    {
        try
        {
            // Performance optimization: Use AsNoTracking for read-only queries
            // Query optimized to work across all databases:
            // 1. Filter by DeviceId at database level (efficient)
            // 2. Load to memory (required for DateTimeOffset ordering on some databases)
            // 3. Get latest record
            var records = await _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId)
                .AsNoTracking()
                .ToListAsync();  // Load to memory

            // Order in memory (works with DateTimeOffset on all databases)
            var latestRecord = records
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefault();

            if (latestRecord != null)
            {
                _logger.LogInformation("Retrieved latest telemetry record for device {DeviceId}", deviceId);
            }
            else
            {
                _logger.LogInformation("No telemetry records found for device {DeviceId}", deviceId);
            }

            return latestRecord;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving latest telemetry record for device {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the latest N telemetry records for a device asynchronously.
    /// 
    /// Query optimization:
    /// - Uses AsNoTracking() for read-only query optimization
    /// - Uses Take() at database level (not loading all then filtering in memory)
    /// - Orders by timestamp descending (newest first)
    /// 
    /// Returns fewer records if fewer than 'count' exist for the device.
    /// </summary>
    /// <param name="deviceId">The device ID to search for.</param>
    /// <param name="count">The maximum number of records to retrieve. Must be > 0.</param>
    /// <returns>A list of telemetry records (up to 'count'), ordered by timestamp descending.</returns>
    /// <exception cref="ArgumentException">Thrown if count is less than or equal to 0.</exception>
    public async Task<List<TelemetryRecord>> GetLatestTelemetryRecordsAsync(Guid deviceId, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than 0", nameof(count));
        }

        try
        {
            // Query optimization: Filter at DB level, order in memory
            // Works across all databases (SQLite, SQL Server, PostgreSQL, MySQL, etc.)
            // 
            // Strategy:
            // 1. Filter by DeviceId at database level (most expensive operation)
            // 2. Load filtered records to memory (required for DateTimeOffset sorting)
            // 3. Sort and take N records in memory (fast for typical use cases)
            var records = await _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId)
                .AsNoTracking()
                .ToListAsync();  // Load filtered records to memory

            // Order in memory by timestamp descending (DateTimeOffset works perfectly here)
            var orderedRecords = records
                .OrderByDescending(t => t.Timestamp)
                .Take(count)
                .ToList();

            _logger.LogInformation("Retrieved {Count} latest telemetry records for device {DeviceId}",
                orderedRecords.Count, deviceId);

            return orderedRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving latest telemetry records for device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<PaginatedResponse<TelemetryRecord>> GetTelemetryRecordsByDeviceAsync(
        Guid deviceId,
        FilterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            var query = _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId);

            // Apply filters
            if (request.MinFuelLevel.HasValue)
            {
                query = query.Where(t => t.FuelLevelPercentage >= request.MinFuelLevel.Value);
            }

            if (request.MaxFuelLevel.HasValue)
            {
                query = query.Where(t => t.FuelLevelPercentage <= request.MaxFuelLevel.Value);
            }

            if (request.MinEngineRPM.HasValue)
            {
                query = query.Where(t => t.EngineRPM >= request.MinEngineRPM.Value);
            }

            if (request.MaxEngineRPM.HasValue)
            {
                query = query.Where(t => t.EngineRPM <= request.MaxEngineRPM.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Load to memory first to avoid SQLite DateTime ordering issues
            var allRecords = await query.ToListAsync();

            // Apply sorting in memory
            var sortedRecords = request.SortBy switch
            {
                "timestamp" => request.SortOrder == "asc" 
                    ? allRecords.OrderBy(t => t.Timestamp).ToList()
                    : allRecords.OrderByDescending(t => t.Timestamp).ToList(),
                "fuelLevel" => request.SortOrder == "asc"
                    ? allRecords.OrderBy(t => t.FuelLevelPercentage).ToList()
                    : allRecords.OrderByDescending(t => t.FuelLevelPercentage).ToList(),
                "engineRPM" => request.SortOrder == "asc"
                    ? allRecords.OrderBy(t => t.EngineRPM).ToList()
                    : allRecords.OrderByDescending(t => t.EngineRPM).ToList(),
                _ => allRecords.OrderByDescending(t => t.Timestamp).ToList() // Default
            };

            // Apply pagination
            var pageSize = Math.Max(1, Math.Min(request.PageSize, 100)); // Limit to 100 records per page
            var skip = (request.PageNumber - 1) * pageSize;

            var records = sortedRecords
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            _logger.LogInformation(
                "Retrieved paginated telemetry records for device {DeviceId}: Page {PageNumber}, Size {PageSize}",
                deviceId, request.PageNumber, pageSize);

            return new PaginatedResponse<TelemetryRecord>
            {
                Data = records,
                PageNumber = request.PageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving paginated telemetry records for device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<TelemetryRecord?> GetOldestTelemetryRecordAsync(Guid deviceId)
    {
        try
        {
            var record = await _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId)
                .OrderBy(t => t.Timestamp)
                .FirstOrDefaultAsync();

            return record;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error while retrieving oldest telemetry record for device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<(decimal Average, decimal Min, decimal Max)> GetFuelLevelStatisticsAsync(Guid deviceId)
    {
        try
        {
            var statistics = await _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId)
                .GroupBy(t => t.DeviceId)
                .Select(g => new
                {
                    Average = g.Average(t => t.FuelLevelPercentage),
                    Min = g.Min(t => t.FuelLevelPercentage),
                    Max = g.Max(t => t.FuelLevelPercentage)
                })
                .FirstOrDefaultAsync();

            if (statistics == null)
            {
                _logger.LogInformation("No telemetry records found for fuel level statistics, device {DeviceId}", deviceId);
                return (0, 0, 0);
            }

            _logger.LogInformation(
                "Retrieved fuel level statistics for device {DeviceId}: Avg={Avg}, Min={Min}, Max={Max}",
                deviceId, statistics.Average, statistics.Min, statistics.Max);

            return ((decimal)statistics.Average, statistics.Min, statistics.Max);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error while calculating fuel level statistics for device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<(int Average, int Min, int Max)> GetEngineRPMStatisticsAsync(Guid deviceId)
    {
        try
        {
            var statistics = await _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId)
                .GroupBy(t => t.DeviceId)
                .Select(g => new
                {
                    Average = (int)g.Average(t => t.EngineRPM),
                    Min = g.Min(t => t.EngineRPM),
                    Max = g.Max(t => t.EngineRPM)
                })
                .FirstOrDefaultAsync();

            if (statistics == null)
            {
                _logger.LogInformation("No telemetry records found for engine RPM statistics, device {DeviceId}", deviceId);
                return (0, 0, 0);
            }

            _logger.LogInformation(
                "Retrieved engine RPM statistics for device {DeviceId}: Avg={Avg}, Min={Min}, Max={Max}",
                deviceId, statistics.Average, statistics.Min, statistics.Max);

            return (statistics.Average, statistics.Min, statistics.Max);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error while calculating engine RPM statistics for device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<int> GetTotalRecordsCountAsync(Guid deviceId)
    {
        try
        {
            var count = await _context.TelemetryRecords
                .Where(t => t.DeviceId == deviceId)
                .CountAsync();

            _logger.LogInformation("Retrieved total records count for device {DeviceId}: {Count}", deviceId, count);
            return count;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error while counting telemetry records for device {DeviceId}", deviceId);
            throw;
        }
    }
}
