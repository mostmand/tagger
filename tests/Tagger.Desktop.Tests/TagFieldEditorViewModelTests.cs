using Tagger.Application.Library;
using Tagger.Domain.Tracks;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class TagFieldEditorViewModelTests
{
    private readonly TagFieldEditorViewModel _sut;

    public TagFieldEditorViewModelTests()
    {
        _sut = new TagFieldEditorViewModel(TagFieldCatalog.Get(TagField.Year), 2026);
    }

    [Fact]
    public void Load_ShouldShowMixedState_WhenSummaryIsMixed()
    {
        // Arrange
        var summary = new TagFieldSummary(TagField.Year, null, true, 2);

        // Act
        _sut.Load(summary);

        // Assert
        Assert.True(_sut.IsMixed);
        Assert.False(_sut.IsDirty);
        Assert.Equal("Mixed values", _sut.PlaceholderText);
    }

    [Fact]
    public void Value_ShouldBecomeDirty_WhenUserEdits()
    {
        // Arrange
        _sut.Load(new TagFieldSummary(TagField.Year, "2021", false, 1));

        // Act
        _sut.Value = "2022";

        // Assert
        Assert.True(_sut.IsDirty);
        Assert.False(_sut.IsMixed);
    }

    [Fact]
    public void Value_ShouldExposeError_WhenYearIsInvalid()
    {
        // Arrange
        _sut.Load(new TagFieldSummary(TagField.Year, null, false, 0));

        // Act
        _sut.Value = "later";

        // Assert
        Assert.NotNull(_sut.Error);
        Assert.Contains("four-digit", _sut.Error);
    }

    [Fact]
    public void MarkSaved_ShouldResetDirtyState_WhenCalled()
    {
        // Arrange
        _sut.Load(new TagFieldSummary(TagField.Year, "2020", false, 1));
        _sut.Value = "2024";

        // Act
        _sut.MarkSaved("2024");

        // Assert
        Assert.False(_sut.IsDirty);
        Assert.Null(_sut.Error);
        Assert.Equal("2024", _sut.Value);
    }
}
