using System.Net;
using System.Text.Json;
using VehicleTelemetryAPI.Exceptions;

namespace VehicleTelemetryAPI.Middleware;

/// <summary>
/// Middleware for handling exceptions globally
/// Improvement: Centralized error handling for consistency across all endpoints
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse { Message = exception.Message };

        return exception switch
        {
            ResourceNotFoundException notFoundEx =>
                HandleNotFoundException(context, notFoundEx, response),
            ValidationException validationEx =>
                HandleValidationException(context, validationEx, response),
            BusinessLogicException businessEx =>
                HandleBusinessLogicException(context, businessEx, response),
            DatabaseException dbEx =>
                HandleDatabaseException(context, dbEx, response),
            _ => HandleUnexpectedException(context, exception, response)
        };
    }

    private static Task HandleNotFoundException(HttpContext context, ResourceNotFoundException exception, ErrorResponse response)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        response.StatusCode = StatusCodes.Status404NotFound;
        response.Message = exception.Message;
        response.Details = $"Resource {exception.ResourceType} with ID {exception.ResourceId} not found";
        
        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task HandleValidationException(HttpContext context, ValidationException exception, ErrorResponse response)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        response.StatusCode = StatusCodes.Status400BadRequest;
        response.Message = "Validation failed";
        response.Errors = exception.Errors;
        
        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task HandleBusinessLogicException(HttpContext context, BusinessLogicException exception, ErrorResponse response)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        response.StatusCode = StatusCodes.Status400BadRequest;
        response.Message = exception.Message;
        
        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task HandleDatabaseException(HttpContext context, DatabaseException exception, ErrorResponse response)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        response.StatusCode = StatusCodes.Status500InternalServerError;
        response.Message = "Database operation failed";
        response.Details = exception.Message;
        
        return context.Response.WriteAsJsonAsync(response);
    }

    private static Task HandleUnexpectedException(HttpContext context, Exception exception, ErrorResponse response)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        response.StatusCode = StatusCodes.Status500InternalServerError;
        response.Message = "An unexpected error occurred";
        response.Details = exception.Message;
        
        return context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Standard error response format
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
