namespace Yoink.Presentation.Services;

internal static class UiDispatcher
{
    internal static void Run(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }
}
