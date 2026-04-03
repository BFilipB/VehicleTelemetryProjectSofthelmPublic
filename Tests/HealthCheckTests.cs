using Xunit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using VehicleTelemetryAPI.HealthChecks;
using VehicleTelemetryAPI.Infrastructure;
using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.Models;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Tests for health check implementations
/// Verifies health check responses under various conditions
/// </summary>
public class HealthCheckTests
{
    [Fact]
    public async Task CloudSyncHealthCheck_WithHighSuccessRate_ReturnsHealthy()
    {
        // Arrange
        var mockMetricsService = new Mock<IMetricsService>();
        var mockLogger = new Mock<ILogger<CloudSyncHealthCheck>>();

        // 95% success rate (95 out of 100)
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 100,
            FailedRequests = 5
        };
        mockMetricsService.Setup(m => m.GetSnapshot()).Returns(snapshot);

        var healthCheck = new CloudSyncHealthCheck(mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CloudSyncHealthCheck_WithDegradedSuccessRate_ReturnsDegraded()
    {
        // Arrange
        var mockMetricsService = new Mock<IMetricsService>();
        var mockLogger = new Mock<ILogger<CloudSyncHealthCheck>>();

        // 60% success rate (degraded range: 50-70%)
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 50,
            FailedRequests = 20
        };
        mockMetricsService.Setup(m => m.GetSnapshot()).Returns(snapshot);

        var healthCheck = new CloudSyncHealthCheck(mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CloudSyncHealthCheck_WithLowSuccessRate_ReturnsUnhealthy()
    {
        // Arrange
        var mockMetricsService = new Mock<IMetricsService>();
        var mockLogger = new Mock<ILogger<CloudSyncHealthCheck>>();

        // 40% success rate (below 50% threshold)
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 100,
            FailedRequests = 60
        };
        mockMetricsService.Setup(m => m.GetSnapshot()).Returns(snapshot);

        var healthCheck = new CloudSyncHealthCheck(mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CloudSyncHealthCheck_WithInsufficientAttempts_ReturnsDegraded()
    {
        // Arrange
        var mockMetricsService = new Mock<IMetricsService>();
        var mockLogger = new Mock<ILogger<CloudSyncHealthCheck>>();

        // Only 5 attempts (below minimum threshold of 10)
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 5,
            FailedRequests = 0
        };
        mockMetricsService.Setup(m => m.GetSnapshot()).Returns(snapshot);

        var healthCheck = new CloudSyncHealthCheck(mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        // Should return Degraded since we don't have enough data
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("insufficient", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CloudSyncHealthCheck_WithExactlyMinimumAttempts_EvaluatesHealth()
    {
        // Arrange
        var mockMetricsService = new Mock<IMetricsService>();
        var mockLogger = new Mock<ILogger<CloudSyncHealthCheck>>();

        // Exactly 10 attempts (at minimum threshold)
        // 100% success rate (above 70%)
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 10,
            FailedRequests = 0 // 100% success
        };
        mockMetricsService.Setup(m => m.GetSnapshot()).Returns(snapshot);

        var healthCheck = new CloudSyncHealthCheck(mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert - Should evaluate and return Healthy
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CloudSyncHealthCheck_SuccessRateCalculation_IsAccurate()
    {
        // Arrange
        var mockMetricsService = new Mock<IMetricsService>();
        var mockLogger = new Mock<ILogger<CloudSyncHealthCheck>>();

        // 80% success rate (16 successful out of 20 requests)
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 20,
            FailedRequests = 4
        };
        mockMetricsService.Setup(m => m.GetSnapshot()).Returns(snapshot);

        var healthCheck = new CloudSyncHealthCheck(mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert - 80% is healthy (>70%)
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(80.0, snapshot.SuccessRate);
    }

    [Fact]
    public async Task CloudSyncHealthCheck_MetricsSnapshot_CalculatesSuccessRateCorrectly()
    {
        // Arrange - Test the MetricsSnapshot SuccessRate property
        var snapshot = new MetricsSnapshot 
        { 
            TotalRequests = 100,
            FailedRequests = 25
        };

        // Act
        var successRate = snapshot.SuccessRate;

        // Assert - (100 - 25) / 100 * 100 = 75%
        Assert.Equal(75.0, successRate);
    }
}
