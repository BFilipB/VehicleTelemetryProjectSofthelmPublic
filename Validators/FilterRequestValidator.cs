using FluentValidation;
using VehicleTelemetryAPI.DTOs.Common;

namespace VehicleTelemetryAPI.Validators;

/// <summary>
/// Validator for FilterRequest DTOs.
/// Ensures pagination and filtering parameters are valid.
/// </summary>
public class FilterRequestValidator : AbstractValidator<FilterRequest>
{
    private readonly string[] _validSortFields = { "timestamp", "fuelLevel", "engineRPM" };
    private readonly string[] _validSortOrders = { "asc", "desc" };

    public FilterRequestValidator()
    {
        // PageNumber validation
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0");

        // PageSize validation
        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("PageSize must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize cannot exceed 100 items per page");

        // SortBy validation
        RuleFor(x => x.SortBy)
            .Must(sortBy => _validSortFields.Contains(sortBy?.ToLower() ?? "timestamp"))
            .WithMessage($"SortBy must be one of: {string.Join(", ", _validSortFields)}");

        // SortOrder validation
        RuleFor(x => x.SortOrder)
            .Must(sortOrder => _validSortOrders.Contains(sortOrder?.ToLower() ?? "asc"))
            .WithMessage($"SortOrder must be one of: {string.Join(", ", _validSortOrders)}");

        // Fuel level filtering validation
        RuleFor(x => x.MinFuelLevel)
            .InclusiveBetween(0, 100)
            .When(x => x.MinFuelLevel.HasValue)
            .WithMessage("MinFuelLevel must be between 0 and 100");

        RuleFor(x => x.MaxFuelLevel)
            .InclusiveBetween(0, 100)
            .When(x => x.MaxFuelLevel.HasValue)
            .WithMessage("MaxFuelLevel must be between 0 and 100");

        RuleFor(x => x)
            .Must(x => !x.MinFuelLevel.HasValue || !x.MaxFuelLevel.HasValue || x.MinFuelLevel <= x.MaxFuelLevel)
            .WithMessage("MinFuelLevel must be less than or equal to MaxFuelLevel");

        // Engine RPM filtering validation
        RuleFor(x => x.MinEngineRPM)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinEngineRPM.HasValue)
            .WithMessage("MinEngineRPM must be non-negative");

        RuleFor(x => x.MaxEngineRPM)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxEngineRPM.HasValue)
            .WithMessage("MaxEngineRPM must be non-negative");

        RuleFor(x => x)
            .Must(x => !x.MinEngineRPM.HasValue || !x.MaxEngineRPM.HasValue || x.MinEngineRPM <= x.MaxEngineRPM)
            .WithMessage("MinEngineRPM must be less than or equal to MaxEngineRPM");
    }
}
