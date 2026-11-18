using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class TrackTagsTests
{
    private readonly TrackTags _sut;

    public TrackTagsTests()
    {
        _sut = new TrackTags(
        [
            new KeyValuePair<TagField, string?>(TagField.Title, "  Northern Lights  "),
            new KeyValuePair<TagField, string?>(TagField.Artist, "Aurora"),
            new KeyValuePair<TagField, string?>(TagField.Album, "   ")
        ]);
    }

    [Fact]
    public void Get_ShouldTrimAndIgnoreWhitespace_WhenValuesAreLoaded()
    {
        // Arrange

        // Act
        var title = _sut.Get(TagField.Title);
        var album = _sut.Get(TagField.Album);

        // Assert
        Assert.Equal("Northern Lights", title);
        Assert.Null(album);
        Assert.Equal(2, _sut.Count);
    }

    [Fact]
    public void With_ShouldRemoveField_WhenValueIsWhitespace()
    {
        // Arrange
        var current = _sut;

        // Act
        var result = current.With(TagField.Artist, " ");

        // Assert
        Assert.False(result.Contains(TagField.Artist));
        Assert.True(current.Contains(TagField.Artist));
    }

    [Fact]
    public void WithMany_ShouldApplyEveryChange_WhenChangesAreProvided()
    {
        // Arrange
        var changes = new[]
        {
            new TagChange(TagField.Title, "Midnight Run"),
            new TagChange(TagField.Genre, "Synthwave")
        };

        // Act
        var result = _sut.WithMany(changes);

        // Assert
        Assert.Equal("Midnight Run", result.Get(TagField.Title));
        Assert.Equal("Synthwave", result.Get(TagField.Genre));
        Assert.Equal("Aurora", result.Get(TagField.Artist));
    }

    [Fact]
    public void Equals_ShouldBeTrue_WhenAllPairsMatch()
    {
        // Arrange
        var other = new TrackTags(
        [
            new KeyValuePair<TagField, string?>(TagField.Artist, "Aurora"),
            new KeyValuePair<TagField, string?>(TagField.Title, "Northern Lights")
        ]);

        // Act
        var equal = _sut.Equals(other);

        // Assert
        Assert.True(equal);
    }
}
