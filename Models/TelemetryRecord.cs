namespace VehicleTelemetryAPI.Models;

/// <summary>
/// Represents telemetry data received from a vehicle's sensors.
/// </summary>
public class TelemetryRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for the vehicle/sensor.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// When the data was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Engine RPM (Revolutions Per Minute).
    /// </summary>
    public int EngineRPM { get; set; }

    /// <summary>
    /// Fuel level as a percentage (0-100).
    /// </summary>
    public decimal FuelLevelPercentage { get; set; }

    /// <summary>
    /// Latitude coordinate.
    /// </summary>
    public decimal Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate.
    /// </summary>
    public decimal Longitude { get; set; }
}
