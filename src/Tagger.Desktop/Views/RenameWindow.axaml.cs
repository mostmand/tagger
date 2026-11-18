using Avalonia.Controls;
using Avalonia.Interactivity;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class RenameWindow : Window
{
    public RenameWindow()
    {
        InitializeComponent();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnApply(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RenameViewModel viewModel && await viewModel.ApplyAsync())
        {
            Close(true);
        }
    }
}
