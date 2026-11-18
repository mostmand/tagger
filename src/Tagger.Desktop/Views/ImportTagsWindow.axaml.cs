using Avalonia.Controls;
using Avalonia.Interactivity;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class ImportTagsWindow : Window
{
    public ImportTagsWindow()
    {
        InitializeComponent();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnApply(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImportTagsViewModel viewModel && await viewModel.ApplyAsync())
        {
            Close(true);
        }
    }
}
