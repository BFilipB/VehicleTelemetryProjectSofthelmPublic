using System.Text.RegularExpressions;

namespace VehicleTelemetryAPI.Middleware;

/// <summary>
/// Input sanitization middleware to prevent injection attacks (SQL, XSS, etc).
/// Enterprise-level security for protecting against common web vulnerabilities.
/// </summary>
public class InputSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputSanitizationMiddleware> _logger;

    // Common SQL injection patterns
    private static readonly string[] SqlInjectionPatterns = new[]
    {
        @"(\b(DROP|DELETE|INSERT|UPDATE|EXEC|EXECUTE|SELECT|UNION|ALTER|CREATE)\b)",
        @"(--|;|\/\*|\*\/|xp_|sp_)",
        @"(\bOR\b.*=.*)",
        @"(\b1\s*=\s*1\b)"
    };

    // Common XSS patterns
    private static readonly string[] XssPatterns = new[]
    {
        @"<script[^>]*>.*?</script>",
        @"on\w+\s*=",
        @"javascript:",
        @"<iframe",
        @"<object",
        @"<embed"
    };

    public InputSanitizationMiddleware(RequestDelegate next, ILogger<InputSanitizationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check request body for POST/PUT/PATCH
        if (context.Request.Method != HttpMethods.Get &&
            context.Request.Method != HttpMethods.Head &&
            context.Request.Method != HttpMethods.Delete)
        {
            // Enable buffering to re-read request body
            context.Request.EnableBuffering();

            // Read body without disposing the underlying stream
            var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            if (ContainsSuspiciousPatterns(body))
            {
                _logger.LogWarning(
                    "Suspicious input detected in request to {Path} from {ClientIp}",
                    context.Request.Path,
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Invalid input detected",
                    message = "Your request contains patterns that may indicate a security attack."
                });

                return;
            }

            // Reset body position for next middleware
            context.Request.Body.Position = 0;
        }

        // Check URL parameters for suspicious patterns
        var queryString = context.Request.QueryString.Value ?? "";
        if (ContainsSuspiciousPatterns(queryString))
        {
            _logger.LogWarning(
                "Suspicious pattern detected in query parameters for {Path} from {ClientIp}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid input detected",
                message = "Your request contains patterns that may indicate a security attack."
            });

            return;
        }

        await _next(context);
    }

    private bool ContainsSuspiciousPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var lowerInput = input.ToLowerInvariant();

        // Check SQL injection patterns
        foreach (var pattern in SqlInjectionPatterns)
        {
            if (Regex.IsMatch(lowerInput, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        // Check XSS patterns
        foreach (var pattern in XssPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        // Check for null bytes (common in buffer overflow attempts)
        if (input.Contains('\0'))
        {
            return true;
        }

        return false;
    }
}
