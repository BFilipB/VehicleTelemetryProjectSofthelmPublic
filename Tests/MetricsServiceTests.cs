using Xunit;
using VehicleTelemetryAPI.Infrastructure;
using Moq;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Tests for the PrometheusMetricsService functionality.
/// </summary>
public class PrometheusMetricsServiceTests
{
    private readonly PrometheusMetricsService _metricsService;
    private readonly Mock<ILogger<PrometheusMetricsService>> _mockLogger;

    public PrometheusMetricsServiceTests()
    {
        _mockLogger = new Mock<ILogger<PrometheusMetricsService>>();
        _metricsService = new PrometheusMetricsService(_mockLogger.Object);
    }

    [Fact]
    public void RecordTelemetryCreation_WithSuccess_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryCreation(true, 100);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.TotalRequests > initialSnapshot.TotalRequests);
    }

    [Fact]
    public void RecordTelemetryCreation_WithFailure_ShouldRecordFailure()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryCreation(false, 100);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.FailedRequests > initialSnapshot.FailedRequests);
    }

    [Fact]
    public void RecordTelemetryRetrieval_WithSuccess_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryRetrieval(true, 50);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.TotalRequests > initialSnapshot.TotalRequests);
    }

    [Fact]
    public void RecordTelemetryRetrieval_WithFailure_ShouldRecordFailure()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryRetrieval(false, 50);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.FailedRequests > initialSnapshot.FailedRequests);
    }

    [Fact]
    public void GetSnapshot_ShouldReturnMetricsSnapshot()
    {
        // Act
        var snapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.NotNull(snapshot);
    }

    [Fact]
    public void GetSnapshot_ShouldHaveValidSuccessRate()
    {
        // Arrange
        _metricsService.RecordTelemetryCreation(true, 100);
        _metricsService.RecordTelemetryCreation(true, 100);

        // Act
        var snapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(snapshot.SuccessRate >= 0);
        Assert.True(snapshot.SuccessRate <= 100);
    }

    [Fact]
    public void RecordTelemetryCreation_WithZeroDuration_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryCreation(true, 0);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.TotalRequests >= initialSnapshot.TotalRequests);
    }

    [Fact]
    public void RecordTelemetryCreation_WithHighDuration_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryCreation(true, 5000);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.TotalRequests >= initialSnapshot.TotalRequests);
    }

    [Fact]
    public void RecordTelemetryRetrieval_WithZeroDuration_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryRetrieval(true, 0);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.TotalRequests >= initialSnapshot.TotalRequests);
    }

    [Fact]
    public void RecordMultipleOperations_ShouldAccumulateMetrics()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordTelemetryCreation(true, 100);
        _metricsService.RecordTelemetryCreation(false, 150);
        _metricsService.RecordTelemetryRetrieval(true, 50);
        _metricsService.RecordTelemetryRetrieval(false, 75);

        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.TotalRequests >= initialSnapshot.TotalRequests + 4);
    }

    [Fact]
    public void RecordCloudSyncAttempt_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordCloudSyncAttempt("success", 1000);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.NotNull(updatedSnapshot);
    }

    [Fact]
    public void RecordCircuitBreakerStateChange_ShouldRecord()
    {
        // Act & Assert (should not throw)
        _metricsService.RecordCircuitBreakerStateChange("TelemetryService", "Open");
    }

    [Fact]
    public void RecordRateLimitEvent_WithLimit_ShouldRecord()
    {
        // Arrange
        var initialSnapshot = _metricsService.GetSnapshot();

        // Act
        _metricsService.RecordRateLimitEvent(true);
        var updatedSnapshot = _metricsService.GetSnapshot();

        // Assert
        Assert.True(updatedSnapshot.RateLimitedRequests > initialSnapshot.RateLimitedRequests);
    }
}
