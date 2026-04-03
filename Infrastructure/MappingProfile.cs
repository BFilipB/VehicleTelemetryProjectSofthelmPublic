// Create a mapping profile for AutoMapper (currently missing)
// This would handle DTO to Model conversions

using AutoMapper;
using VehicleTelemetryAPI.DTOs;
using VehicleTelemetryAPI.Models;

namespace VehicleTelemetryAPI.Infrastructure;

/// <summary>
/// AutoMapper profile for mapping DTOs to models and vice versa.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Telemetry Record Mapping
        CreateMap<TelemetryRecordRequest, TelemetryRecord>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<TelemetryRecord, TelemetryRecordResponse>()
            .ReverseMap();
    }
}
