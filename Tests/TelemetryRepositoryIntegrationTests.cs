using Xunit;
using Moq;
using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.DTOs.Common;
using VehicleTelemetryAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Integration tests for TelemetryRepository.
/// Tests repository methods with actual EF Core in-memory database.
/// </summary>
public class TelemetryRepositoryIntegrationTests
{
    private readonly TelemetryDbContext _context;
    private readonly TelemetryRepository _repository;

    public TelemetryRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TelemetryDbContext(options);
        var logger = new Mock<ILogger<TelemetryRepository>>().Object;
        _repository = new TelemetryRepository(_context, logger);
    }

    [Fact]
    public async Task AddTelemetryRecordAsync_ShouldPersistToDatabase()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var record = new TelemetryRecord
        {
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        // Act
        await _repository.AddTelemetryRecordAsync(record);

        // Assert
        var savedRecord = await _context.TelemetryRecords
            .FirstOrDefaultAsync(t => t.DeviceId == deviceId);
        Assert.NotNull(savedRecord);
        Assert.Equal(record.EngineRPM, savedRecord.EngineRPM);
    }

    [Fact]
    public async Task GetTelemetryRecordsByDeviceAsync_WithValidFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        await SeedTestData(deviceId);

        var filterRequest = new FilterRequest
        {
            PageNumber = 1,
            PageSize = 10,
            MinFuelLevel = 50,
            MaxFuelLevel = 80,
            SortBy = "fuelLevel",
            SortOrder = "asc"
        };

        // Act
        var result = await _repository.GetTelemetryRecordsByDeviceAsync(deviceId, filterRequest);

        // Assert
        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, record =>
            Assert.InRange(record.FuelLevelPercentage, 50, 80));
    }

    [Fact]
    public async Task GetFuelLevelStatisticsAsync_ShouldCalculateCorrectly()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        await SeedTestData(deviceId);

        // Act
        var (average, min, max) = await _repository.GetFuelLevelStatisticsAsync(deviceId);

        // Assert
        Assert.True(min >= 0 && min <= 100);
        Assert.True(max >= 0 && max <= 100);
        Assert.True(average >= min && average <= max);
    }

    [Fact]
    public async Task GetEngineRPMStatisticsAsync_ShouldCalculateCorrectly()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        await SeedTestData(deviceId);

        // Act
        var (average, min, max) = await _repository.GetEngineRPMStatisticsAsync(deviceId);

        // Assert
        Assert.True(min >= 0);
        Assert.True(max >= 0);
        Assert.True(average >= min && average <= max);
    }

    [Fact]
    public async Task GetTotalRecordsCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        const int expectedCount = 5;
        await SeedTestData(deviceId, count: expectedCount);

        // Act
        var count = await _repository.GetTotalRecordsCountAsync(deviceId);

        // Assert
        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public async Task GetOldestTelemetryRecordAsync_ShouldReturnFirstRecord()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var records = new List<TelemetryRecord>
        {
            new() { DeviceId = deviceId, Timestamp = now.AddHours(-2), EngineRPM = 2000, FuelLevelPercentage = 80, Latitude = 0, Longitude = 0 },
            new() { DeviceId = deviceId, Timestamp = now.AddHours(-1), EngineRPM = 2500, FuelLevelPercentage = 75, Latitude = 0, Longitude = 0 },
            new() { DeviceId = deviceId, Timestamp = now, EngineRPM = 3000, FuelLevelPercentage = 70, Latitude = 0, Longitude = 0 }
        };

        foreach (var record in records)
            _context.TelemetryRecords.Add(record);
        await _context.SaveChangesAsync();

        // Act
        var oldest = await _repository.GetOldestTelemetryRecordAsync(deviceId);

        // Assert
        Assert.NotNull(oldest);
        Assert.Equal(2000, oldest.EngineRPM);
    }

    private async Task SeedTestData(Guid deviceId, int count = 3)
    {
        var now = DateTimeOffset.UtcNow;
        var records = new List<TelemetryRecord>();

        for (int i = 0; i < count; i++)
        {
            records.Add(new TelemetryRecord
            {
                DeviceId = deviceId,
                Timestamp = now.AddMinutes(-i),
                EngineRPM = 2000 + (i * 100),
                FuelLevelPercentage = 75 - (i * 5),
                Latitude = 40.7128m + i,
                Longitude = -74.0060m - i
            });
        }

        foreach (var record in records)
            _context.TelemetryRecords.Add(record);

        await _context.SaveChangesAsync();
    }
}
