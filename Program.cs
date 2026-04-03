using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using FluentValidation.AspNetCore;
using AutoMapper;
using VehicleTelemetryAPI.Configuration;
using VehicleTelemetryAPI.Data;
using VehicleTelemetryAPI.Services;
using VehicleTelemetryAPI.Background;
using VehicleTelemetryAPI.Middleware;
using VehicleTelemetryAPI.Infrastructure;
using Prometheus;

// Configure Serilog - Enterprise-grade structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/telemetry-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "VehicleTelemetryAPI")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentUserName()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Add services
builder.Services.AddDbContext<TelemetryDbContext>(options =>
    options.UseSqlite("Data Source=telemetry.db"));

builder.Services.AddScoped<ITelemetryRepository, TelemetryRepository>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();

// Add Prometheus metrics
builder.Services.AddSingleton<IMetricsService, PrometheusMetricsService>();

// Add Polly resilience policies
builder.Services.AddSingleton<IResiliencePolicyFactory, PollyResiliencePolicyFactory>();

// Configure cloud sync options (options pattern for runtime configuration)
builder.Services.Configure<CloudSyncOptions>(
    builder.Configuration.GetSection("CloudSync"));

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add Health Checks with dependency awareness
builder.Services.AddHealthChecks()
    .AddCheck<VehicleTelemetryAPI.HealthChecks.DatabaseHealthCheck>("database", HealthStatus.Unhealthy, new[] { "db" })
    .AddCheck<VehicleTelemetryAPI.HealthChecks.CloudSyncHealthCheck>("cloud_sync", HealthStatus.Degraded, new[] { "external" });

// Register individual health checks for aggregation
builder.Services.AddScoped<VehicleTelemetryAPI.HealthChecks.DatabaseHealthCheck>();
builder.Services.AddScoped<VehicleTelemetryAPI.HealthChecks.CloudSyncHealthCheck>();
builder.Services.AddScoped<VehicleTelemetryAPI.HealthChecks.ApplicationHealthCheck>();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Vehicle Telemetry API",
        Version = "v1",
        Description = "RESTful API for receiving, storing, and processing vehicle telemetry data"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsPolicyBuilder =>
    {
        corsPolicyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add hosted services
builder.Services.AddHostedService<CloudSyncBackgroundService>();

var app = builder.Build();

// Initialize database - create tables if they don't exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        Log.Information("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error initializing database");
        throw;
    }
}

// Configure HTTP pipeline
// Enable Swagger for all environments (Development and Production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vehicle Telemetry API V1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at the root URL
});

// Enterprise-level security and tracing middlewares
app.UseMiddleware<InputSanitizationMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// Custom middleware for exception handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health");

// Prometheus metrics endpoint
app.UseHttpMetrics();
app.MapMetrics("/metrics");

app.MapControllers();

try
{
    Log.Information("Starting Vehicle Telemetry API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
