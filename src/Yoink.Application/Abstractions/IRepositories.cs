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
