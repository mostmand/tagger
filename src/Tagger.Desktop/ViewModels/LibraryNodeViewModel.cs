using CommunityToolkit.Mvvm.ComponentModel;

namespace Tagger.Desktop.ViewModels;

/// <summary>A node in the library tree: either a folder or a track.</summary>
public abstract partial class LibraryNodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isExpanded = true;
}
