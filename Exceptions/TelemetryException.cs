namespace VehicleTelemetryAPI.Exceptions;

/// <summary>
/// Base exception for the Vehicle Telemetry API
/// </summary>
public class TelemetryException : Exception
{
    public TelemetryException(string message) : base(message) { }
    public TelemetryException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a requested resource is not found
/// </summary>
public class ResourceNotFoundException : TelemetryException
{
    public string? ResourceType { get; }
    public object? ResourceId { get; }

    public ResourceNotFoundException(string message) : base(message) { }

    public ResourceNotFoundException(string resourceType, object resourceId) 
        : base($"{resourceType} with ID {resourceId} not found")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : TelemetryException
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(Dictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }
}

/// <summary>
/// Exception thrown when business rule is violated
/// </summary>
public class BusinessLogicException : TelemetryException
{
    public BusinessLogicException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when database operation fails
/// </summary>
public class DatabaseException : TelemetryException
{
    public DatabaseException(string message) : base(message) { }
    public DatabaseException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when cache operation fails
/// </summary>
public class CacheException : TelemetryException
{
    public CacheException(string message) : base(message) { }
    public CacheException(string message, Exception innerException)
        : base(message, innerException) { }
}
