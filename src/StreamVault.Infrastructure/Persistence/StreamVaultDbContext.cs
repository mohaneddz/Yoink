using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StreamVault.Domain.Entities;

namespace StreamVault.Infrastructure.Persistence;

/// <summary>EF Core context backing history, library, and saved-channel persistence (SQLite).</summary>
public sealed class StreamVaultDbContext : DbContext
{
    public StreamVaultDbContext(DbContextOptions<StreamVaultDbContext> options) : base(options)
    {
    }

    public DbSet<DownloadHistoryEntry> History => Set<DownloadHistoryEntry>();
    public DbSet<LibraryEntry> Library => Set<LibraryEntry>();
    public DbSet<SavedChannelEntry> Channels => Set<SavedChannelEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite cannot order/compare DateTimeOffset; store the UTC instant as DateTime and round-trip.
        var dto = new ValueConverter<DateTimeOffset, DateTime>(
            v => v.UtcDateTime,
            v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc), TimeSpan.Zero));
        var dtoNullable = new ValueConverter<DateTimeOffset?, DateTime?>(
            v => v.HasValue ? v.Value.UtcDateTime : null,
            v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc), TimeSpan.Zero) : null);

        modelBuilder.Entity<DownloadHistoryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Timestamp).HasConversion(dto);
            e.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<LibraryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DownloadedAt).HasConversion(dto);
            e.HasIndex(x => x.FilePath).IsUnique();
        });

        modelBuilder.Entity<SavedChannelEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AddedAt).HasConversion(dto);
            e.Property(x => x.LastCheckedAt).HasConversion(dtoNullable);
            e.HasIndex(x => x.ChannelUrl).IsUnique();
        });
    }
}
