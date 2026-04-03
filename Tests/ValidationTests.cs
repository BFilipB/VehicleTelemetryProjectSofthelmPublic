using Xunit;
using FluentValidation;
using VehicleTelemetryAPI.DTOs;
using VehicleTelemetryAPI.DTOs.Common;
using VehicleTelemetryAPI.Validators;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Tests for input validation rules.
/// </summary>
public class ValidationTests
{
    private readonly TelemetryRecordRequestValidator _telemetryValidator;
    private readonly FilterRequestValidator _filterValidator;

    public ValidationTests()
    {
        _telemetryValidator = new TelemetryRecordRequestValidator();
        _filterValidator = new FilterRequestValidator();
    }

    #region TelemetryRecordRequest Validation

    [Fact]
    public void ValidateTelemetryRecord_WithValidData_ShouldPass()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
    }

    [Fact]
    public void ValidateTelemetryRecord_WithEmptyDeviceId_ShouldFail()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DeviceId");
    }

    [Fact]
    public void ValidateTelemetryRecord_WithFutureTimestamp_ShouldFail()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow.AddHours(1),
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public void ValidateTelemetryRecord_WithInvalidFuelLevel_ShouldFail()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 150m, // Invalid: > 100
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FuelLevelPercentage");
    }

    [Fact]
    public void ValidateTelemetryRecord_WithInvalidLatitude_ShouldFail()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 95m, // Invalid: > 90
            Longitude = -74.0060m
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Latitude");
    }

    [Fact]
    public void ValidateTelemetryRecord_WithInvalidLongitude_ShouldFail()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = 200m // Invalid: > 180
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Longitude");
    }

    [Fact]
    public void ValidateTelemetryRecord_WithNegativeEngineRPM_ShouldFail()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = -100,
            FuelLevelPercentage = 75.5m,
            Latitude = 40.7128m,
            Longitude = -74.0060m
        };

        // Act
        var result = _telemetryValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EngineRPM");
    }

    #endregion

    #region FilterRequest Validation

    [Fact]
    public void ValidateFilterRequest_WithValidData_ShouldPass()
    {
        // Arrange
        var request = new FilterRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SortBy = "timestamp",
            SortOrder = "asc",
            MinFuelLevel = 20,
            MaxFuelLevel = 80
        };

        // Act
        var result = _filterValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
    }

    [Fact]
    public void ValidateFilterRequest_WithInvalidSortBy_ShouldFail()
    {
        // Arrange
        var request = new FilterRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SortBy = "invalidField", // Invalid: not a valid sort field
            SortOrder = "asc"
        };

        // Act
        var result = _filterValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SortBy");
    }

    [Fact]
    public void ValidateFilterRequest_WithInvalidSortOrder_ShouldFail()
    {
        // Arrange
        var request = new FilterRequest
        {
            PageNumber = 1,
            PageSize = 10,
            SortBy = "timestamp",
            SortOrder = "invalid" // Invalid: must be "asc" or "desc"
        };

        // Act
        var result = _filterValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SortOrder");
    }

    [Fact]
    public void ValidateFilterRequest_WithMinFuelLevelGreaterThanMax_ShouldFail()
    {
        // Arrange
        var request = new FilterRequest
        {
            PageNumber = 1,
            PageSize = 10,
            MinFuelLevel = 80,
            MaxFuelLevel = 20 // Invalid: max < min
        };

        // Act
        var result = _filterValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateFilterRequest_WithMinEngineRPMGreaterThanMax_ShouldFail()
    {
        // Arrange
        var request = new FilterRequest
        {
            PageNumber = 1,
            PageSize = 10,
            MinEngineRPM = 5000,
            MaxEngineRPM = 2000 // Invalid: max < min
        };

        // Act
        var result = _filterValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion
}
