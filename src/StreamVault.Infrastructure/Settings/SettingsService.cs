using System.Text.Json;
using Microsoft.Extensions.Logging;
using StreamVault.Application.Abstractions;
using StreamVault.Domain.Settings;

namespace StreamVault.Infrastructure.Settings;

/// <summary>Persists <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%\StreamVault.</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamVault");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Current = new AppSettings();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(_path))
            {
                await using var stream = File.OpenRead(_path);
                var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct).ConfigureAwait(false);
                if (loaded is not null)
                {
                    Current = loaded;
                }
            }
            else
            {
                await SaveInternalAsync(Current, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings; using defaults.");
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Current = settings;
        await SaveInternalAsync(settings, ct).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveInternalAsync(AppSettings settings, CancellationToken ct)
    {
        try
        {
            await using var stream = File.Create(_path);
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings.");
        }
    }
}
