using Yoink.Domain.Entities;

namespace Yoink.Application.Abstractions;

public interface IHistoryRepository
{
    Task<IReadOnlyList<DownloadHistoryEntry>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(DownloadHistoryEntry entry, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ILibraryRepository
{
    Task<IReadOnlyList<LibraryEntry>> GetAllAsync(CancellationToken ct = default);
    Task<LibraryEntry?> FindByPathAsync(string path, CancellationToken ct = default);
    Task UpsertAsync(LibraryEntry entry, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ISavedChannelRepository
{
    Task<IReadOnlyList<SavedChannelEntry>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(SavedChannelEntry entry, CancellationToken ct = default);
    Task UpdateAsync(SavedChannelEntry entry, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>Persists the download queue so it survives an app restart (spec §7.2, §20).</summary>
public interface IQueueRepository
{
    Task<IReadOnlyList<QueueJobEntity>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Inserts a new row (when <see cref="QueueJobEntity.Id"/> is 0) or updates the existing one, returning its id.</summary>
    Task<int> UpsertAsync(QueueJobEntity entry, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
