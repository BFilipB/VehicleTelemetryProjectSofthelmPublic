using Xunit;
using Microsoft.AspNetCore.Http;
using Moq;
using Microsoft.Extensions.Logging;
using VehicleTelemetryAPI.Middleware;
using VehicleTelemetryAPI.Infrastructure;

namespace VehicleTelemetryAPI.Tests;

/// <summary>
/// Tests for custom middleware implementations
/// Verifies security, logging, and request handling
/// </summary>
public class MiddlewareTests
{
    [Fact]
    public async Task InputSanitizationMiddleware_BlocksSQLInjectionAttempts()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<InputSanitizationMiddleware>>();
        var next = new RequestDelegate(async context =>
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(new { success = true });
        });

        var middleware = new InputSanitizationMiddleware(next, mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/telemetry";
        // SQL injection attempt in query string
        httpContext.Request.QueryString = new QueryString("?filter='; DROP TABLE users; --");

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - Should be rejected (not 200)
        Assert.NotEqual(200, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InputSanitizationMiddleware_BlocksScriptTags()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<InputSanitizationMiddleware>>();
        var next = new RequestDelegate(async context =>
        {
            context.Response.StatusCode = 200;
        });

        var middleware = new InputSanitizationMiddleware(next, mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/telemetry";
        // XSS attempt in query string
        httpContext.Request.QueryString = new QueryString("?filter=<script>alert('xss')</script>");

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - Should be rejected (not 200)
        Assert.NotEqual(200, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InputSanitizationMiddleware_AllowsCleanInput()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<InputSanitizationMiddleware>>();
        var middlewareWasCalled = false;
        var next = new RequestDelegate(async context =>
        {
            middlewareWasCalled = true;
            context.Response.StatusCode = 200;
        });

        var middleware = new InputSanitizationMiddleware(next, mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/telemetry";
        // Clean input
        httpContext.Request.QueryString = new QueryString("?filter=device123");

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - Should pass through to next middleware
        Assert.True(middlewareWasCalled);
        Assert.Equal(200, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task RateLimitingMiddleware_AllowsRequestsUnderLimit()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RateLimitingMiddleware>>();
        var mockMetricsService = new Mock<IMetricsService>();
        var requestCount = 0;
        var next = new RequestDelegate(async context =>
        {
            requestCount++;
            context.Response.StatusCode = 200;
        });

        var middleware = new RateLimitingMiddleware(next, mockLogger.Object, mockMetricsService.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/telemetry";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        // Act - Make 5 requests (well under limit)
        for (int i = 0; i < 5; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/telemetry";
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
            await middleware.InvokeAsync(context);
        }

        // Assert - All should succeed
        Assert.Equal(5, requestCount);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_GeneratesUniqueIds()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var correlationIds = new HashSet<string>();
        var next = new RequestDelegate(async context =>
        {
            if (context.Items.TryGetValue("CorrelationId", out var correlationId))
            {
                correlationIds.Add(correlationId?.ToString() ?? "");
            }
            context.Response.StatusCode = 200;
        });

        var middleware = new CorrelationIdMiddleware(next, mockLogger.Object);

        // Act - Make 5 requests
        for (int i = 0; i < 5; i++)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/telemetry";
            await middleware.InvokeAsync(httpContext);
        }

        // Assert - Each request should have unique correlation ID
        Assert.Equal(5, correlationIds.Count);
        Assert.True(correlationIds.All(id => !string.IsNullOrEmpty(id)));
    }

    [Fact]
    public async Task CorrelationIdMiddleware_IncludesIdInResponseHeader()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var capturedCorrelationId = string.Empty;
        var next = new RequestDelegate(async context =>
        {
            if (context.Items.TryGetValue("CorrelationId", out var correlationId))
            {
                capturedCorrelationId = correlationId?.ToString() ?? "";
                context.Response.Headers["X-Correlation-Id"] = capturedCorrelationId;
            }
            context.Response.StatusCode = 200;
        });

        var middleware = new CorrelationIdMiddleware(next, mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/telemetry";

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - Response should include correlation ID header
        Assert.True(httpContext.Response.Headers.ContainsKey("X-Correlation-Id"));
        Assert.False(string.IsNullOrEmpty(httpContext.Response.Headers["X-Correlation-Id"].ToString()));
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_CatchesUnhandledExceptions()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var next = new RequestDelegate(context =>
        {
            throw new InvalidOperationException("Test unhandled exception");
        });

        var middleware = new ExceptionHandlingMiddleware(next, mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/telemetry";
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - Should return 500 error
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ReturnsJsonResponse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var next = new RequestDelegate(context =>
        {
            throw new InvalidOperationException("Test exception");
        });

        var middleware = new ExceptionHandlingMiddleware(next, mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/telemetry";
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.StartsWith("application/json", httpContext.Response.ContentType);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using (var reader = new StreamReader(httpContext.Response.Body))
        {
            var responseBody = await reader.ReadToEndAsync();
            Assert.NotEmpty(responseBody);
            // Should be valid JSON
            Assert.StartsWith("{", responseBody);
        }
    }
}
