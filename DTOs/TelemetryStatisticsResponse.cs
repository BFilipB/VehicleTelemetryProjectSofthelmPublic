namespace VehicleTelemetryAPI.DTOs;

/// <summary>
/// Response DTO for telemetry statistics
/// </summary>
public class TelemetryStatisticsResponse
{
    public Guid DeviceId { get; set; }
    public int TotalRecords { get; set; }
    public DateTime? FirstRecordTime { get; set; }
    public DateTime? LastRecordTime { get; set; }
    public decimal AverageFuelLevel { get; set; }
    public decimal MinFuelLevel { get; set; }
    public decimal MaxFuelLevel { get; set; }
    public int AverageEngineRPM { get; set; }
    public int MinEngineRPM { get; set; }
    public int MaxEngineRPM { get; set; }
}
