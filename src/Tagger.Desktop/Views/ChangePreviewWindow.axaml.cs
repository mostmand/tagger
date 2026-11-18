using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Tagger.Desktop.Views;

public partial class ChangePreviewWindow : Window
{
    public ChangePreviewWindow()
    {
        InitializeComponent();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private void OnApply(object? sender, RoutedEventArgs e) => Close(true);
}
