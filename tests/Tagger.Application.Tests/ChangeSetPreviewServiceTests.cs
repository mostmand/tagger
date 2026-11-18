using Tagger.Application.Editing;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class ChangeSetPreviewServiceTests
{
    private readonly ChangeSetPreviewService _sut = new();

    [Fact]
    public void Create_ShouldReturnOneDiff_WhenOnlyOneFieldChanges()
    {
        // Arrange
        var track = TestTracks.Create(title: "Old title");
        var changes = TagChangeSet.From([new TagChange(TagField.Title, "New title")]);

        // Act
        var result = _sut.Create([track], changes);

        // Assert
        var preview = result.Tracks[0];
        Assert.True(preview.HasChanges);
        Assert.Single(preview.Diffs);
        Assert.Equal("Old title", preview.Diffs[0].Before);
        Assert.Equal("New title", preview.Diffs[0].After);
        Assert.Equal("New title", preview.NewTags.Get(TagField.Title));
    }

    [Fact]
    public void Create_ShouldIgnoreUnchangedValue_WhenPatchMatchesCurrentTag()
    {
        // Arrange
        var track = TestTracks.Create(title: "Same");
        var changes = TagChangeSet.From([new TagChange(TagField.Title, "Same")]);

        // Act
        var result = _sut.Create([track], changes);

        // Assert
        Assert.False(result.HasChanges);
        Assert.Empty(result.Tracks[0].Diffs);
    }

    [Fact]
    public void Create_ShouldMarkArtworkChanged_WhenArtworkIsRemoved()
    {
        // Arrange
        var track = TestTracks.Create(artwork: new CoverArt([1, 2, 3], "image/png"));

        // Act
        var result = _sut.Create([track], TagChangeSet.Empty, CoverArtUpdate.Remove);

        // Assert
        Assert.True(result.Tracks[0].CoverArtChanged);
        Assert.True(result.Tracks[0].HasChanges);
    }

    [Fact]
    public void Create_ShouldReportTrackCounts_WhenMultipleTracksSelected()
    {
        // Arrange
        var tracks = new[] { TestTracks.Create("one.mp3", title: "One"), TestTracks.Create("two.mp3", title: "Two") };
        var changes = TagChangeSet.From([new TagChange(TagField.Album, "New Album")]);

        // Act
        var result = _sut.Create(tracks, changes);

        // Assert
        Assert.Equal(2, result.ChangedTrackCount);
        Assert.Equal(2, result.ChangedFieldCount);
    }
}
