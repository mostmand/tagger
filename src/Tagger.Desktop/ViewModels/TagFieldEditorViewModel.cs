using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Library;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed partial class TagFieldEditorViewModel : ViewModelBase
{
    private readonly int _currentYear;
    private bool _loading;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isMixed;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _error;

    public TagFieldEditorViewModel(TagFieldInfo info, int currentYear)
    {
        Info = info;
        _currentYear = currentYear;
    }

    public TagFieldInfo Info { get; }

    public TagField Field => Info.Field;

    public string Label => Info.DisplayName;

    public string Hint => Info.Hint ?? string.Empty;

    public bool IsNumeric => Info.IsNumeric;

    public string PlaceholderText => IsMixed ? "Mixed values" : Hint;

    public void Load(TagFieldSummary summary)
    {
        _loading = true;
        Value = summary.CommonValue ?? string.Empty;
        IsMixed = summary.IsMixed;
        IsDirty = false;
        Error = null;
        _loading = false;
        OnPropertyChanged(nameof(PlaceholderText));
    }

    public void MarkSaved(string? value)
    {
        _loading = true;
        Value = value ?? string.Empty;
        IsMixed = false;
        IsDirty = false;
        Error = null;
        _loading = false;
        OnPropertyChanged(nameof(PlaceholderText));
    }

    partial void OnValueChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        IsMixed = false;
        IsDirty = true;
        Error = TagValidator.ValidateField(Field, value, _currentYear);
        OnPropertyChanged(nameof(PlaceholderText));
    }

    partial void OnIsMixedChanged(bool value) => OnPropertyChanged(nameof(PlaceholderText));
}
