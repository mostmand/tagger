using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

/// <summary>Full-window artwork overlay with wheel zoom, drag panning, and scrollbars.</summary>
public partial class ArtworkViewerView : UserControl
{
    private static readonly PixelPoint CursorHotSpot = new(16, 16);
    private static Cursor? _grabCursor;
    private static Cursor? _grabbingCursor;

    private bool _isPanning;
    private Point _panOrigin;
    private Vector _panStartOffset;

    public ArtworkViewerView()
    {
        InitializeComponent();
        AddHandler(PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is ArtworkViewerViewModel previous)
        {
            previous.PropertyChanged -= OnViewerPropertyChanged;
        }

        base.OnDataContextChanged(e);

        if (DataContext is ArtworkViewerViewModel current)
        {
            current.PropertyChanged += OnViewerPropertyChanged;
        }
    }

    private static Cursor GrabCursor => _grabCursor ??= CreateCursor("cursor-grab.png");

    private static Cursor GrabbingCursor => _grabbingCursor ??= CreateCursor("cursor-grabbing.png");

    private static Cursor CreateCursor(string fileName) =>
        new(new Bitmap(AssetLoader.Open(new Uri($"avares://tagger/Assets/{fileName}"))), CursorHotSpot);

    private void OnViewerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ArtworkViewerViewModel.IsOpen))
        {
            _isPanning = false;
            if (DataContext is ArtworkViewerViewModel { IsOpen: true })
            {
                ArtworkScrollViewer.Offset = default;
                Dispatcher.UIThread.Post(() => CloseArtworkButton.Focus());
            }
        }

        if (e.PropertyName is nameof(ArtworkViewerViewModel.IsOpen) or nameof(ArtworkViewerViewModel.Zoom))
        {
            UpdateCursor();
        }
    }

    private void UpdateCursor()
    {
        var canPan = DataContext is ArtworkViewerViewModel { IsOpen: true, Zoom: > 1 };
        ArtworkCanvas.Cursor = canPan ? GrabCursor : Cursor.Default;
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not ArtworkViewerViewModel { IsOpen: true } viewer)
        {
            return;
        }

        viewer.ZoomBy(e.Delta.Y > 0 ? ArtworkViewerViewModel.ZoomStep : -ArtworkViewerViewModel.ZoomStep);
        e.Handled = true;
    }

    private void OnArtworkPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ArtworkViewerViewModel { IsOpen: true, Zoom: > 1 })
        {
            return;
        }

        var point = e.GetCurrentPoint(ArtworkScrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _panOrigin = point.Position;
        _panStartOffset = ArtworkScrollViewer.Offset;
        ArtworkCanvas.Cursor = GrabbingCursor;
        e.Pointer.Capture(ArtworkCanvas);
        e.Handled = true;
    }

    private void OnArtworkPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var point = e.GetCurrentPoint(ArtworkScrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
        {
            EndPan(e.Pointer);
            return;
        }

        var delta = point.Position - _panOrigin;
        ArtworkScrollViewer.Offset = new Vector(
            _panStartOffset.X - delta.X,
            _panStartOffset.Y - delta.Y);
        e.Handled = true;
    }

    private void OnArtworkPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        EndPan(e.Pointer);
        e.Handled = true;
    }

    private void OnArtworkPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
        UpdateCursor();
    }

    private void EndPan(IPointer pointer)
    {
        _isPanning = false;
        pointer.Capture(null);
        UpdateCursor();
    }
}
