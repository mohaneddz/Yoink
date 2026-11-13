using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Yoink.Infrastructure.Media;

namespace Yoink.Presentation.Services;

/// <summary>Loads images off the UI thread, sharing decoded images and persistent thumbnail files.</summary>
public static class CachedImage
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ImageFileCache Files = new(Client,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yoink", "cache", "images"));
    private static readonly ConcurrentDictionary<string, (Lazy<Task<BitmapSource?>> Image, DateTimeOffset Created)> Memory = new();

    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(string), typeof(CachedImage), new PropertyMetadata(null, OnSourceChanged));

    public static void SetSource(DependencyObject element, string? value) => element.SetValue(SourceProperty, value);
    public static string? GetSource(DependencyObject element) => (string?)element.GetValue(SourceProperty);

    private static async void OnSourceChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not Image image) return;
        image.Source = null;
        if (args.NewValue is not string url || string.IsNullOrWhiteSpace(url)) return;
        try
        {
            if (Memory.TryGetValue(url, out var previous) && previous.Image.IsValueCreated
                && previous.Image.Value.IsCompletedSuccessfully && previous.Image.Value.Result is null
                && previous.Created < DateTimeOffset.UtcNow.AddSeconds(-30))
                ((ICollection<KeyValuePair<string, (Lazy<Task<BitmapSource?>> Image, DateTimeOffset Created)>>)Memory).Remove(new(url, previous));
            var entry = Memory.GetOrAdd(url, key => (new Lazy<Task<BitmapSource?>>(() => Task.Run(() => LoadAsync(key))), DateTimeOffset.UtcNow));
            var bitmap = await entry.Image.Value;
            // A recycled row may now represent a different item.
            if (GetSource(image) == url) image.Source = bitmap;
            foreach (var old in Memory.Where(pair => pair.Value.Image.IsValueCreated && pair.Value.Image.Value.IsCompleted)
                         .OrderByDescending(pair => pair.Value.Created).Skip(128).ToList())
                Memory.TryRemove(old.Key, out _);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException or FormatException or System.Runtime.InteropServices.COMException)
        {
            Memory.TryRemove(url, out _);
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile) Files.Invalidate(url);
        }
    }

    private static async Task<BitmapSource?> LoadAsync(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath : await Files.GetAsync(url).ConfigureAwait(false);
        if (path is null) return null;
        using var input = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 480;
        bitmap.StreamSource = input;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
