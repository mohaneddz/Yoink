using Microsoft.EntityFrameworkCore;
using Yoink.Application.Abstractions;
using Yoink.Domain.Entities;

namespace Yoink.Infrastructure.Persistence;

/// <summary>EF-backed history store. Uses a context factory so concurrent downloads can write safely.</summary>
public sealed class HistoryRepository : IHistoryRepository
{
    private readonly IDbContextFactory<YoinkDbContext> _factory;

    public HistoryRepository(IDbContextFactory<YoinkDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<DownloadHistoryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.History.AsNoTracking().OrderByDescending(x => x.Timestamp).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(DownloadHistoryEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.History.Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.Library.Where(item => db.History.Any(history =>
            (history.SourceUrl != "" && history.SourceUrl == item.SourceUrl)
            || (history.OutputPath != "" && history.OutputPath == item.FilePath)))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.History.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.Library.Where(item => db.History.Any(history => history.Id == id &&
            ((history.SourceUrl != "" && history.SourceUrl == item.SourceUrl)
            || (history.OutputPath != "" && history.OutputPath == item.FilePath))))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.History.Where(x => x.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>EF-backed library store.</summary>
public sealed class LibraryRepository : ILibraryRepository
{
    private readonly IDbContextFactory<YoinkDbContext> _factory;

    public LibraryRepository(IDbContextFactory<YoinkDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<LibraryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Library.AsNoTracking().OrderByDescending(x => x.DownloadedAt).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<LibraryEntry?> FindByPathAsync(string path, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Library.AsNoTracking().FirstOrDefaultAsync(x => x.FilePath == path, ct).ConfigureAwait(false);
    }

    public async Task UpsertAsync(LibraryEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.Library.FirstOrDefaultAsync(x => x.FilePath == entry.FilePath, ct).ConfigureAwait(false);
        if (existing is null)
        {
            db.Library.Add(entry);
        }
        else
        {
            entry.Id = existing.Id;
            db.Entry(existing).CurrentValues.SetValues(entry);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Library.Where(x => x.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>EF-backed saved-channel store.</summary>
public sealed class SavedChannelRepository : ISavedChannelRepository
{
    private readonly IDbContextFactory<YoinkDbContext> _factory;

    public SavedChannelRepository(IDbContextFactory<YoinkDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<SavedChannelEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Channels.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(SavedChannelEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Channels.Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SavedChannelEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Channels.Update(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Channels.Where(x => x.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>EF-backed queue store, so in-progress/queued jobs survive an app restart.</summary>
public sealed class QueueRepository : IQueueRepository
{
    private readonly IDbContextFactory<YoinkDbContext> _factory;

    public QueueRepository(IDbContextFactory<YoinkDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<QueueJobEntity>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Queue.AsNoTracking().OrderBy(x => x.Order).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> UpsertAsync(QueueJobEntity entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        if (entry.Id == 0)
        {
            db.Queue.Add(entry);
        }
        else
        {
            db.Queue.Update(entry);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return entry.Id;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Queue.Where(x => x.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
