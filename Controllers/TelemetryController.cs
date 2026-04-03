using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VehicleTelemetryAPI.DTOs;
using VehicleTelemetryAPI.Services;
using VehicleTelemetryAPI.Infrastructure;

namespace VehicleTelemetryAPI.Controllers;

/// <summary>
/// API controller for telemetry operations.
/// Tracks metrics and performance for all operations.
/// </summary>
[ApiController]
[Route("api/v1/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        ITelemetryService telemetryService,
        IMetricsService metricsService,
        ILogger<TelemetryController> logger)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Receives and stores a telemetry record from a vehicle.
    /// </summary>
    /// <param name="request">The telemetry data to store.</param>
    /// <returns>The created telemetry record with HTTP 201 Created status.</returns>
    /// <response code="201">Telemetry record created successfully.</response>
    /// <response code="400">Bad request - validation failed.</response>
    /// <response code="500">Internal server error.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TelemetryRecordResponse>> CreateTelemetryRecord(
        [FromBody] TelemetryRecordRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for telemetry record creation");
            _metricsService.RecordTelemetryCreation(false, stopwatch.ElapsedMilliseconds);
            return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors) });
        }

        try
        {
            var response = await _telemetryService.CreateTelemetryRecordAsync(request);
            stopwatch.Stop();
            _metricsService.RecordTelemetryCreation(true, stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Telemetry record created for device {DeviceId}", request.DeviceId);
            return CreatedAtAction(nameof(GetLatestTelemetryRecord), 
                new { deviceId = request.DeviceId }, response);
        }
        catch (ArgumentNullException ex)
        {
            stopwatch.Stop();
            _metricsService.RecordTelemetryCreation(false, stopwatch.ElapsedMilliseconds);
            _logger.LogError(ex, "Invalid argument when creating telemetry record");
            return BadRequest(new { error = "Invalid data provided" });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordTelemetryCreation(false, stopwatch.ElapsedMilliseconds);
            _logger.LogError(ex, "Error creating telemetry record");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An error occurred while creating the telemetry record" });
        }
    }

    /// <summary>
    /// Retrieves the latest telemetry record for a specific device.
    /// </summary>
    /// <param name="deviceId">The device ID to retrieve data for.</param>
    /// <returns>The latest telemetry record for the device.</returns>
    /// <response code="200">Telemetry record found and returned.</response>
    /// <response code="404">No telemetry record found for the device.</response>
    /// <response code="500">Internal server error.</response>
    [HttpGet("{deviceId}/latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TelemetryRecordResponse>> GetLatestTelemetryRecord(Guid deviceId)
    {
        var stopwatch = Stopwatch.StartNew();

        if (deviceId == Guid.Empty)
        {
            _logger.LogWarning("Invalid device ID provided: empty GUID");
            stopwatch.Stop();
            _metricsService.RecordTelemetryRetrieval(false, stopwatch.ElapsedMilliseconds);
            return BadRequest(new { error = "Device ID cannot be empty" });
        }

        try
        {
            var record = await _telemetryService.GetLatestTelemetryRecordAsync(deviceId);
            stopwatch.Stop();

            if (record == null)
            {
                _metricsService.RecordTelemetryRetrieval(true, stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("No telemetry record found for device {DeviceId}", deviceId);
                return NotFound(new { error = $"No telemetry records found for device {deviceId}" });
            }

            _metricsService.RecordTelemetryRetrieval(true, stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Retrieved latest telemetry record for device {DeviceId}", deviceId);
            return Ok(record);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metricsService.RecordTelemetryRetrieval(false, stopwatch.ElapsedMilliseconds);
            _logger.LogError(ex, "Error retrieving telemetry record for device {DeviceId}", deviceId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An error occurred while retrieving the telemetry record" });
        }
    }
}
