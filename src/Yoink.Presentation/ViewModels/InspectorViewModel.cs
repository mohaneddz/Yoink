using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yoink.Presentation.ViewModels;

/// <summary>Hosts the contextual right-hand inspector panel (spec section 9): pin/close, arbitrary content.</summary>
public partial class InspectorViewModel : ObservableObject
{
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
