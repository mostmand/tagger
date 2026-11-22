using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class PlayerBar : UserControl
{
    private bool _isSeeking;

    public PlayerBar()
    {
        InitializeComponent();

        // The stock Slider only seeks while its Thumb is dragged and marks the
        // pointer events handled, which bypassed the view model and left the
        // player running at the old position. Own the interaction instead so a
        // click or drag anywhere on the track scrubs.
        SeekSlider.AddHandler(PointerPressedEvent, OnSeekPointerPressed, RoutingStrategies.Tunnel, true);
        SeekSlider.AddHandler(PointerMovedEvent, OnSeekPointerMoved, RoutingStrategies.Tunnel, true);
        SeekSlider.AddHandler(PointerReleasedEvent, OnSeekPointerReleased, RoutingStrategies.Tunnel, true);
        SeekSlider.AddHandler(PointerCaptureLostEvent, OnSeekPointerCaptureLost, RoutingStrategies.Tunnel, true);
        SeekSlider.AddHandler(KeyDownEvent, OnSeekKeyDown, RoutingStrategies.Bubble, true);
    }

    private PlayerViewModel? ViewModel => DataContext as PlayerViewModel;

    private void OnSeekPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { HasTrack: true } viewModel ||
            sender is not Slider slider ||
            !e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isSeeking = true;
        slider.Focus();
        e.Pointer.Capture(slider);
        viewModel.BeginScrub();
        UpdateScrub(slider, e);
        e.Handled = true;
    }

    private void OnSeekPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isSeeking || sender is not Slider slider)
        {
            return;
        }

        UpdateScrub(slider, e);
        e.Handled = true;
    }

    private void OnSeekPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeeking || sender is not Slider slider)
        {
            return;
        }

        FinishScrub(slider);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnSeekPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isSeeking && sender is Slider slider)
        {
            FinishScrub(slider);
        }
    }

    private void OnSeekKeyDown(object? sender, KeyEventArgs e)
    {
        // Slider applies its own keyboard increment before bubbling, so seek to
        // the resulting value; otherwise the next snapshot pulls the thumb back.
        if (sender is not Slider slider ||
            ViewModel is not { HasTrack: true } viewModel ||
            e.KeyModifiers != KeyModifiers.None ||
            e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or
                Key.PageUp or Key.PageDown or Key.Home or Key.End))
        {
            return;
        }

        viewModel.SeekTo(slider.Value);
    }

    private void UpdateScrub(Slider slider, PointerEventArgs e)
    {
        // Mirror the Slider's own mapping so the Thumb lands under the pointer.
        var track = slider.GetVisualDescendants().OfType<Track>().FirstOrDefault();
        var position = track is not null ? e.GetPosition(track) : e.GetPosition(slider);
        var width = track?.Bounds.Width ?? slider.Bounds.Width;
        var thumb = track?.Thumb?.Bounds.Width ?? 0d;
        var usable = width - thumb;
        var logical = usable > 0
            ? Math.Clamp((position.X - thumb * 0.5) / usable, 0d, 1d)
            : 0d;

        slider.Value = slider.Minimum + logical * (slider.Maximum - slider.Minimum);
        ViewModel?.UpdateScrub(slider.Value);
    }

    private void FinishScrub(Slider slider)
    {
        _isSeeking = false;
        ViewModel?.EndScrub(slider.Value);
    }
}
