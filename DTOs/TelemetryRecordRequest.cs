using System.ComponentModel.DataAnnotations;

namespace VehicleTelemetryAPI.DTOs;

/// <summary>
/// Data Transfer Object for creating telemetry records.
/// </summary>
public class TelemetryRecordRequest
{
    /// <summary>
    /// Unique identifier for the vehicle/sensor.
    /// </summary>
    [Required(ErrorMessage = "DeviceId is required.")]
    public Guid DeviceId { get; set; }

    /// <summary>
    /// When the data was recorded.
    /// </summary>
    [Required(ErrorMessage = "Timestamp is required.")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Engine RPM (Revolutions Per Minute).
    /// </summary>
    [Required(ErrorMessage = "EngineRPM is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "EngineRPM must be a non-negative value.")]
    public int EngineRPM { get; set; }

    /// <summary>
    /// Fuel level as a percentage (0-100).
    /// </summary>
    [Required(ErrorMessage = "FuelLevelPercentage is required.")]
    [Range(0, 100, ErrorMessage = "FuelLevelPercentage must be between 0 and 100.")]
    public decimal FuelLevelPercentage { get; set; }

    /// <summary>
    /// Latitude coordinate.
    /// </summary>
    [Required(ErrorMessage = "Latitude is required.")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate.
    /// </summary>
    [Required(ErrorMessage = "Longitude is required.")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal Longitude { get; set; }
}
