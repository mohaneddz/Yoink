using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yoink.Presentation.ViewModels;

/// <summary>Hosts the contextual right-hand inspector panel (spec section 9): pin/close, arbitrary content.</summary>
public partial class InspectorViewModel : ObservableObject
{
    public InspectorViewModel(Yoink.Application.Abstractions.ISettingsService? settings = null)
    {
        if (settings is not null)
            settings.Changed += (_, _) => Yoink.Presentation.Services.UiDispatcher.Run(() =>
            {
                if (settings.Current.HidePrivateVideos)
                {
                    IsOpen = false;
                    Content = null;
                }
            });
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private object? _content;

    public void Show(object content)
    {
        Content = content;
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        if (IsPinned)
        {
            return;
        }

        IsOpen = false;
        Content = null;
    }

    [RelayCommand]
    private void TogglePin() => IsPinned = !IsPinned;
}
