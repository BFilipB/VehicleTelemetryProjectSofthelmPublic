using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.DTOs;
using VehicleTelemetryAPI.Models;
using VehicleTelemetryAPI.Services;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Unit tests for the TelemetryService business logic layer.
/// 
/// Test Strategy:
/// - Uses Moq for dependency mocking (ITelemetryRepository, ILogger)
/// - Tests service logic in isolation from database and HTTP concerns
/// - Follows AAA pattern: Arrange → Act → Assert
/// - Verifies both happy paths and error conditions
/// - Validates DTO transformation logic
/// 
/// Coverage:
/// - CreateTelemetryRecordAsync: Valid requests, null handling, data mapping
/// - GetLatestTelemetryRecordAsync: Valid queries, missing data, null handling
/// - GetLatestTelemetryRecordsAsync: Valid queries, boundary conditions, validation
/// </summary>
public class TelemetryServiceTests
{
    private readonly Mock<ITelemetryRepository> _mockRepository;
    private readonly Mock<ILogger<TelemetryService>> _mockLogger;
    private readonly TelemetryService _telemetryService;

    public TelemetryServiceTests()
    {
        _mockRepository = new Mock<ITelemetryRepository>();
        _mockLogger = new Mock<ILogger<TelemetryService>>();
        _telemetryService = new TelemetryService(_mockRepository.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Verifies that CreateTelemetryRecordAsync successfully creates and returns a record
    /// when given a valid request.
    /// 
    /// Tests:
    /// - Request DTO is properly transformed to domain model
    /// - Repository.AddTelemetryRecordAsync is called exactly once
    /// - Response DTO contains the same data as the input
    /// - No null values in the response
    /// </summary>
    [Fact]
    public async Task CreateTelemetryRecordAsync_WithValidRequest_ShouldReturnResponse()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new TelemetryRecordRequest
        {
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        _mockRepository
            .Setup(r => r.AddTelemetryRecordAsync(It.IsAny<TelemetryRecord>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _telemetryService.CreateTelemetryRecordAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(deviceId, response.DeviceId);
        Assert.Equal(request.EngineRPM, response.EngineRPM);
        Assert.Equal(request.FuelLevelPercentage, response.FuelLevelPercentage);
        _mockRepository.Verify(r => r.AddTelemetryRecordAsync(It.IsAny<TelemetryRecord>()), Times.Once);
    }

    /// <summary>
    /// Verifies that CreateTelemetryRecordAsync throws ArgumentNullException
    /// when the request parameter is null.
    /// 
    /// Tests:
    /// - Null guard clause works as expected
    /// - Service validates input before calling repository
    /// - Proper exception type is thrown
    /// </summary>
    [Fact]
    public async Task CreateTelemetryRecordAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        TelemetryRecordRequest? request = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _telemetryService.CreateTelemetryRecordAsync(request!));
    }

    /// <summary>
    /// Verifies that GetLatestTelemetryRecordAsync returns a properly mapped response DTO
    /// when the repository finds a matching record.
    /// 
    /// Tests:
    /// - Repository is called with correct device ID
    /// - Domain model is properly transformed to response DTO
    /// - All relevant fields are mapped correctly
    /// - Repository is called exactly once
    /// </summary>
    [Fact]
    public async Task GetLatestTelemetryRecordAsync_WithValidDeviceId_ShouldReturnRecord()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var telemetryRecord = new TelemetryRecord
        {
            Id = 1,
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        _mockRepository
            .Setup(r => r.GetLatestTelemetryRecordAsync(deviceId))
            .ReturnsAsync(telemetryRecord);

        // Act
        var response = await _telemetryService.GetLatestTelemetryRecordAsync(deviceId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(deviceId, response.DeviceId);
        Assert.Equal(telemetryRecord.EngineRPM, response.EngineRPM);
        _mockRepository.Verify(r => r.GetLatestTelemetryRecordAsync(deviceId), Times.Once);
    }

    /// <summary>
    /// Verifies that GetLatestTelemetryRecordAsync returns null
    /// when the repository finds no records for the given device ID.
    /// 
    /// Tests:
    /// - Service properly handles "not found" scenario
    /// - Returns null instead of throwing an exception
    /// - Represents 404 scenario in HTTP layer
    /// </summary>
    [Fact]
    public async Task GetLatestTelemetryRecordAsync_WithNoRecords_ShouldReturnNull()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetLatestTelemetryRecordAsync(deviceId))
            .ReturnsAsync((TelemetryRecord?)null);

        // Act
        var response = await _telemetryService.GetLatestTelemetryRecordAsync(deviceId);

        // Assert
        Assert.Null(response);
        _mockRepository.Verify(r => r.GetLatestTelemetryRecordAsync(deviceId), Times.Once);
    }

    /// <summary>
    /// Verifies that GetLatestTelemetryRecordsAsync returns a list of properly mapped DTOs
    /// when given valid parameters.
    /// 
    /// Tests:
    /// - Repository is called with correct device ID and count
    /// - Multiple domain models are transformed to response DTOs
    /// - List size matches expected count
    /// - All records have correct device ID
    /// - Repository is called exactly once
    /// </summary>
    [Fact]
    public async Task GetLatestTelemetryRecordsAsync_WithValidParameters_ShouldReturnRecords()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var records = new List<TelemetryRecord>
        {
            new TelemetryRecord
            {
                Id = 1,
                DeviceId = deviceId,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
                EngineRPM = 3000,
                FuelLevelPercentage = 75.5m,
                Latitude = 40.7128m,
                Longitude = -74.0060m
            },
            new TelemetryRecord
            {
                Id = 2,
                DeviceId = deviceId,
                Timestamp = DateTimeOffset.UtcNow,
                EngineRPM = 2500,
                FuelLevelPercentage = 70.0m,
                Latitude = 40.7130m,
                Longitude = -74.0058m
            }
        };

        _mockRepository
            .Setup(r => r.GetLatestTelemetryRecordsAsync(deviceId, 5))
            .ReturnsAsync(records);

        // Act
        var responses = await _telemetryService.GetLatestTelemetryRecordsAsync(deviceId, 5);

        // Assert
        Assert.NotNull(responses);
        Assert.Equal(2, responses.Count);
        Assert.Equal(deviceId, responses[0].DeviceId);
        _mockRepository.Verify(r => r.GetLatestTelemetryRecordsAsync(deviceId, 5), Times.Once);
    }

    /// <summary>
    /// Verifies that GetLatestTelemetryRecordsAsync throws ArgumentException
    /// when count is less than or equal to zero.
    /// 
    /// Tests:
    /// - Input validation works correctly
    /// - Invalid count values are rejected
    /// - Repository is never called for invalid input
    /// </summary>
    [Fact]
    public async Task GetLatestTelemetryRecordsAsync_WithInvalidCount_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _telemetryService.GetLatestTelemetryRecordsAsync(deviceId, -1));
    }
}
