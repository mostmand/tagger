using Tagger.Application.Importing;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class FilenameImportServiceTests
{
    private readonly FilenameImportService _sut = new(new FilenamePatternParser());

    [Fact]
    public void CreatePlan_ShouldAddChanges_WhenFilenameContainsNewValues()
    {
        // Arrange
        var track = TestTracks.Create("Aurora - Northern Lights.mp3", title: "Wrong", artist: null);
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        var entry = plan.Entries[0];
        Assert.True(entry.IsValid);
        Assert.True(entry.HasChanges);
        Assert.Equal("Aurora", entry.Changes.Get(TagField.Artist));
        Assert.Equal("Northern Lights", entry.Changes.Get(TagField.Title));
    }

    [Fact]
    public void CreatePlan_ShouldNotAddChange_WhenParsedValueMatchesCurrentTag()
    {
        // Arrange
        var track = TestTracks.Create("Aurora - Northern Lights.mp3", title: "Northern Lights", artist: "Aurora");
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        Assert.False(plan.Entries[0].HasChanges);
        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void CreatePlan_ShouldReportError_WhenFilenameDoesNotMatch()
    {
        // Arrange
        var track = TestTracks.Create("Loose Demo.mp3");
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        Assert.False(plan.Entries[0].IsValid);
        Assert.Equal(1, plan.ErrorCount);
    }

    [Fact]
    public void CreatePlan_ShouldReturnEmptyPlan_WhenNoTracksProvided()
    {
        // Arrange

        // Act
        var plan = _sut.CreatePlan([], TagPattern.Parse("%title%"));

        // Assert
        Assert.Empty(plan.Entries);
        Assert.False(plan.HasChanges);
    }
}
