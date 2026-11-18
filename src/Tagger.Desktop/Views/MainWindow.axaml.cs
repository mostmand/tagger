using Avalonia.Controls;
using Avalonia.Input;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private bool _syncingSelection;

    public MainWindow()
    {
        InitializeComponent();
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
