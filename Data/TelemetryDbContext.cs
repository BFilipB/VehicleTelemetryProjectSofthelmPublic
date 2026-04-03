using Microsoft.EntityFrameworkCore;
using VehicleTelemetryAPI.Models;

namespace VehicleTelemetryAPI.Data;

/// <summary>
/// Database context for telemetry data using Entity Framework Core.
/// </summary>
public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet for TelemetryRecord entities.
    /// </summary>
    public DbSet<TelemetryRecord> TelemetryRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TelemetryRecord entity
        modelBuilder.Entity<TelemetryRecord>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DeviceId)
                .IsRequired();

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.EngineRPM)
                .IsRequired();

            entity.Property(e => e.FuelLevelPercentage)
                .HasPrecision(5, 2)
                .IsRequired();

            entity.Property(e => e.Latitude)
                .HasPrecision(9, 6)
                .IsRequired();

            entity.Property(e => e.Longitude)
                .HasPrecision(9, 6)
                .IsRequired();

            // Create an index on DeviceId and Timestamp for efficient queries
            entity.HasIndex(e => new { e.DeviceId, e.Timestamp })
                .IsDescending(false, true);
        });
    }
}
