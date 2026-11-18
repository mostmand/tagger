using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tagger.Desktop.ViewModels;

/// <summary>State for the full-window artwork viewer: image, zoom, and open/closed.</summary>
public sealed partial class ArtworkViewerViewModel : ViewModelBase
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;
    public const double ZoomStep = 0.25;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private byte[]? _artworkBytes;

    [ObservableProperty]
    private string _artworkSummary = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyPropertyChangedFor(nameof(CanZoomIn))]
    [NotifyPropertyChangedFor(nameof(CanZoomOut))]
    private double _zoom = 1;

    public string ZoomLabel => $"{Zoom * 100:0}%";

    public bool CanZoomIn => Zoom < MaxZoom;

    public bool CanZoomOut => Zoom > MinZoom;

    public void Open()
    {
        Zoom = 1;
        IsOpen = true;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    public void ZoomBy(double delta)
    {
        Zoom = Math.Clamp(Math.Round(Zoom + delta, 2), MinZoom, MaxZoom);
    }

    public void ResetZoom() => Zoom = 1;

    [RelayCommand]
    private void ZoomIn() => ZoomBy(ZoomStep);

    [RelayCommand]
    private void ZoomOut() => ZoomBy(-ZoomStep);

    [RelayCommand]
    private void Reset() => ResetZoom();
}
