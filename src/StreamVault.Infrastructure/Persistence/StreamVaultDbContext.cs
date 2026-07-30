using Microsoft.EntityFrameworkCore;
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
        modelBuilder.Entity<DownloadHistoryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<LibraryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FilePath).IsUnique();
        });

        modelBuilder.Entity<SavedChannelEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ChannelUrl).IsUnique();
        });
    }
}
