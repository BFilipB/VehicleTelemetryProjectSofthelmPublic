using FluentValidation;
using VehicleTelemetryAPI.DTOs;

namespace VehicleTelemetryAPI.Validators;

/// <summary>
/// Validator for TelemetryRecordRequest DTOs.
/// Implements comprehensive validation rules for telemetry data submissions.
/// </summary>
public class TelemetryRecordRequestValidator : AbstractValidator<TelemetryRecordRequest>
{
    public TelemetryRecordRequestValidator()
    {
        // DeviceId validation
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required and cannot be empty")
            .Must(id => id != Guid.Empty)
            .WithMessage("DeviceId must be a valid GUID");

        // Timestamp validation
        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required")
            .Must(timestamp => timestamp <= DateTimeOffset.UtcNow.AddSeconds(1))
            .WithMessage("Timestamp cannot be in the future")
            .Must(timestamp => timestamp > DateTimeOffset.UtcNow.AddYears(-10))
            .WithMessage("Timestamp cannot be more than 10 years in the past");
    // Note: 1-second buffer allows for network latency and clock skew

        // EngineRPM validation
        RuleFor(x => x.EngineRPM)
            .GreaterThanOrEqualTo(0)
            .WithMessage("EngineRPM must be a non-negative value")
            .LessThanOrEqualTo(10000)
            .WithMessage("EngineRPM must not exceed 10000 RPM");

        // FuelLevelPercentage validation
        RuleFor(x => x.FuelLevelPercentage)
            .InclusiveBetween(0, 100)
            .WithMessage("FuelLevelPercentage must be between 0 and 100");

        // Latitude validation
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        // Longitude validation
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");
    }
}
