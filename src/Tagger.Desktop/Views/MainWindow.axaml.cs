using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.VisibleTracks.CollectionChanged -= OnVisibleTracksChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.VisibleTracks.CollectionChanged += OnVisibleTracksChanged;
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

    private void OnVisibleTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (TrackList.SelectedItems is { Count: > 0 })
        {
            return;
        }

        if (TrackList.ItemCount > 0)
        {
            TrackList.SelectedIndex = 0;
        }
    }

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var rows = TrackList.SelectedItems?
            .OfType<TrackRowViewModel>()
            .ToList() ?? [];

        viewModel.SetSelection(rows);
    }
}
