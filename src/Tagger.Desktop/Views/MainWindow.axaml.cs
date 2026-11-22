using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private bool _syncingSelection;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPlaybackKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectionChanged -= OnViewModelSelectionChanged;
            _viewModel.VisibleTracksRebuilt -= OnVisibleTracksRebuilt;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SelectionChanged += OnViewModelSelectionChanged;
            _viewModel.VisibleTracksRebuilt += OnVisibleTracksRebuilt;
        }

        base.OnDataContextChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsFocused)
        {
            SearchBox.Text = string.Empty;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SearchText = string.Empty;
            }

            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OnVisibleTracksRebuilt(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var selected = _viewModel.SelectedRows
            .Where(row => _viewModel.VisibleTracks.Contains(row))
            .ToList();

        if (selected.Count == 0 && _viewModel.VisibleTracks.Count > 0)
        {
            TrackTree.SelectedItem = _viewModel.VisibleTracks[0];
            return;
        }

        SyncSelection(selected);
    }

    private void OnViewModelSelectionChanged(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        SyncSelection(_viewModel.SelectedRows
            .Where(row => _viewModel.VisibleTracks.Contains(row))
            .ToList());
    }

    private void SyncSelection(IReadOnlyList<TrackRowViewModel> rows)
    {
        if (_syncingSelection || TrackTree.SelectedItems is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            TrackTree.SelectedItems.Clear();
            foreach (var row in rows)
            {
                TrackTree.SelectedItems.Add(row);
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnPlaybackKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null || IsTextInputFocused())
        {
            return;
        }

        if (e.Key == Key.Space &&
            _viewModel.Player.ShowBar &&
            !IsSpaceConsumedByFocus())
        {
            _ = _viewModel.TogglePlaybackAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && TrackTree.IsKeyboardFocusWithin)
        {
            _ = _viewModel.PlaySelectionAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Right && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = _viewModel.PlayNextAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = _viewModel.PlayPreviousAsync();
            e.Handled = true;
        }
    }

    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        // The TreeViewItem captures the pointer, so the gesture is raised on its
        // content presenter and never bubbles through the row template.
        if (_viewModel is not null &&
            e.Source is Control { DataContext: TrackRowViewModel row })
        {
            _ = _viewModel.PlayRowAsync(row);
            e.Handled = true;
        }
    }

    private bool IsTextInputFocused() =>
        (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()) is TextBox;

    /// <summary>
    /// Space is a global play shortcut only when the focused control does not use it
    /// itself. Buttons, sliders, combo boxes, and menu items must keep their own
    /// keyboard activation.
    /// </summary>
    private bool IsSpaceConsumedByFocus() =>
        (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()) is
            TextBox or Button or Slider or ComboBox or MenuItem;

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || _viewModel is null)
        {
            return;
        }

        var nodes = TrackTree.SelectedItems?
            .OfType<LibraryNodeViewModel>()
            .ToList() ?? [];

        _viewModel.SelectTreeNodes(nodes);
    }
}
