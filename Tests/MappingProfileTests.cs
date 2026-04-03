using Xunit;
using AutoMapper;
using VehicleTelemetryAPI.DTOs;
using VehicleTelemetryAPI.Infrastructure;
using VehicleTelemetryAPI.Models;
using System;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Tests for AutoMapper profile configuration.
/// </summary>
public class MappingProfileTests
{
    private readonly IMapper _mapper;

    public MappingProfileTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void MappingProfile_IsValid()
    {
        // Act & Assert
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void Map_TelemetryRecord_ToTelemetryRecordResponse()
    {
        // Arrange
        var record = new TelemetryRecord
        {
            Id = 1,
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75m,
            Latitude = 40m,
            Longitude = -74m
        };

        // Act
        var response = _mapper.Map<TelemetryRecordResponse>(record);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(record.DeviceId, response.DeviceId);
        Assert.Equal(record.EngineRPM, response.EngineRPM);
        Assert.Equal(record.FuelLevelPercentage, response.FuelLevelPercentage);
    }

    [Fact]
    public void Map_TelemetryRecordRequest_ToTelemetryRecord()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 3000,
            FuelLevelPercentage = 75m,
            Latitude = 40m,
            Longitude = -74m
        };

        // Act
        var record = _mapper.Map<TelemetryRecord>(request);

        // Assert
        Assert.NotNull(record);
        Assert.Equal(request.DeviceId, record.DeviceId);
        Assert.Equal(request.EngineRPM, record.EngineRPM);
        Assert.Equal(request.FuelLevelPercentage, record.FuelLevelPercentage);
    }

    [Fact]
    public void Map_TelemetryRecordResponse_WithAllProperties()
    {
        // Arrange
        var record = new TelemetryRecord
        {
            Id = 1,
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 5000,
            FuelLevelPercentage = 50.5m,
            Latitude = 35.5m,
            Longitude = 120.5m
        };

        // Act
        var response = _mapper.Map<TelemetryRecordResponse>(record);

        // Assert
        Assert.Equal(record.Latitude, response.Latitude);
        Assert.Equal(record.Longitude, response.Longitude);
        Assert.Equal(record.Timestamp, response.Timestamp);
    }

    [Fact]
    public void Map_MultipleRecords_Independently()
    {
        // Arrange
        var record1 = new TelemetryRecord { Id = 1, DeviceId = Guid.NewGuid(), EngineRPM = 1000 };
        var record2 = new TelemetryRecord { Id = 2, DeviceId = Guid.NewGuid(), EngineRPM = 5000 };

        // Act
        var response1 = _mapper.Map<TelemetryRecordResponse>(record1);
        var response2 = _mapper.Map<TelemetryRecordResponse>(record2);

        // Assert
        Assert.NotEqual(response1.DeviceId, response2.DeviceId);
        Assert.NotEqual(response1.EngineRPM, response2.EngineRPM);
    }

    [Fact]
    public void Map_TelemetryRecordRequest_WithBoundaryValues()
    {
        // Arrange
        var request = new TelemetryRecordRequest
        {
            DeviceId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EngineRPM = 0,
            FuelLevelPercentage = 100m,
            Latitude = 90m,
            Longitude = 180m
        };

        // Act
        var record = _mapper.Map<TelemetryRecord>(request);

        // Assert
        Assert.Equal(0, record.EngineRPM);
        Assert.Equal(100m, record.FuelLevelPercentage);
        Assert.Equal(90m, record.Latitude);
        Assert.Equal(180m, record.Longitude);
    }
}
