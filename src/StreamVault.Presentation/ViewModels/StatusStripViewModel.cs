using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamVault.Presentation.ViewModels;

public enum NetworkState
{
    Stable,
    Unstable,
    Offline
}

/// <summary>Backs the bottom operational status strip shown on every primary page (spec section 9).</summary>
public partial class StatusStripViewModel : ObservableObject
{
    [ObservableProperty]
    private int _activeDownloads;

    [ObservableProperty]
    private double _totalSpeedMbps;

    [ObservableProperty]
    private int _filesInQueue;

    [ObservableProperty]
    private double _storageFreeTb;

    [ObservableProperty]
    private NetworkState _network = NetworkState.Stable;

    public string TotalSpeedText => $"{TotalSpeedMbps:0.0} MB/s";

    public string StorageFreeText => $"{StorageFreeTb:0.00} TB FREE";

    public string NetworkText => Network switch
    {
        NetworkState.Stable => "STABLE",
        NetworkState.Unstable => "UNSTABLE",
        _ => "OFFLINE"
    };

    partial void OnTotalSpeedMbpsChanged(double value) => OnPropertyChanged(nameof(TotalSpeedText));
    partial void OnStorageFreeTbChanged(double value) => OnPropertyChanged(nameof(StorageFreeText));
    partial void OnNetworkChanged(NetworkState value) => OnPropertyChanged(nameof(NetworkText));
}
