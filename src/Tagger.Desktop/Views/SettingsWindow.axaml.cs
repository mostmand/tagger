using Avalonia.Controls;
using Avalonia.Interactivity;
using Tagger.Desktop.ViewModels;

namespace Tagger.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && await viewModel.SaveAsync())
        {
            Close(true);
        }
    }
}
