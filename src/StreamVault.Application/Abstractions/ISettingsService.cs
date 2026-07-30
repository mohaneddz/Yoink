using StreamVault.Domain.Settings;

namespace StreamVault.Application.Abstractions;

/// <summary>Loads and persists <see cref="AppSettings"/>.</summary>
public interface ISettingsService
{
    /// <summary>The current in-memory settings (loaded at startup).</summary>
    AppSettings Current { get; }

    event EventHandler? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
