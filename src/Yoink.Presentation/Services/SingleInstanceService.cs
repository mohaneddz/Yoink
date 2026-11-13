namespace Yoink.Presentation.Services;

/// <summary>Keeps one queue owner per Windows session and lets subsequent launches show it.</summary>
public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _instance;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _listener;
    public bool IsPrimary { get; }

    public SingleInstanceService(string name = @"Local\Yoink.Desktop")
    {
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        // The lifetime of the named handle identifies the owner; no thread-affine mutex ownership is needed.
        _instance = new Mutex(false, name, out var created);
        IsPrimary = created;
    }

    public void ActivateExisting() => _activation.Set();

    public void Listen(Action activate)
    {
        _listener = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) => activate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _listener?.Unregister(null);
        _activation.Dispose();
        _instance.Dispose();
    }
}
