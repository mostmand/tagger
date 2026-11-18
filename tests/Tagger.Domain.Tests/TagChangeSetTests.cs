using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class TagChangeSetTests
{
    private readonly TagChangeSet _sut = new();

    [Fact]
    public void Add_ShouldTrackField_WhenValueIsProvided()
    {
        // Arrange

        // Act
        _sut.Add(TagField.Title, "Northern Lights");
        _sut.Add(TagField.Album, null);

        // Assert
        Assert.Equal(2, _sut.Count);
        Assert.Equal("Northern Lights", _sut.Get(TagField.Title));
        Assert.Null(_sut.Get(TagField.Album));
        Assert.True(_sut.Contains(TagField.Album));
    }

    [Fact]
    public void Remove_ShouldDropField_WhenCalled()
    {
        // Arrange
        _sut.Add(TagField.Title, "Northern Lights");

        // Act
        _sut.Remove(TagField.Title);

        // Assert
        Assert.True(_sut.IsEmpty);
        Assert.False(_sut.Contains(TagField.Title));
    }

    [Fact]
    public void Clone_ShouldCopyChanges_WhenCalled()
    {
        // Arrange
        _sut.Add(TagField.Artist, "Aurora");

        // Act
        var clone = _sut.Clone();
        clone.Add(TagField.Album, "Static Bloom");

        // Assert
        Assert.Equal("Aurora", clone.Get(TagField.Artist));
        Assert.Null(_sut.Get(TagField.Album));
    }
}

public sealed class TagFieldCatalogTests
{
    [Fact]
    public void TryGetByToken_ShouldResolveKnownToken_WhenCaseDiffers()
    {
        // Arrange
        var _sut = TagFieldCatalog.TryGetByToken;

        // Act
        var found = _sut("ALBUMARTIST", out var info);

        // Assert
        Assert.True(found);
        Assert.Equal(TagField.AlbumArtist, info.Field);
    }

    [Fact]
    public void All_ShouldContainEveryEnumValue_WhenCatalogIsBuilt()
    {
        // Arrange
        var _sut = TagFieldCatalog.All;

        // Act
        var missing = Enum.GetValues<TagField>().Except(_sut.Select(x => x.Field)).ToList();

        // Assert
        Assert.Empty(missing);
    }
}

public sealed class AudioPropertiesTests
{
    [Fact]
    public void DurationText_ShouldFormatMinutesAndSeconds_WhenUnderAnHour()
    {
        // Arrange
        var _sut = new AudioProperties(TimeSpan.FromSeconds(125), 320, 44100, 2, "MP3", false);

        // Act
        var text = _sut.DurationText;

        // Assert
        Assert.Equal("2:05", text);
    }

    [Fact]
    public void BitrateText_ShouldSayLossless_WhenLosslessCodec()
    {
        // Arrange
        var _sut = new AudioProperties(TimeSpan.FromMinutes(3), 1000, 44100, 2, "FLAC", true);

        // Act
        var text = _sut.BitrateText;

        // Assert
        Assert.Equal("Lossless", text);
    }
}
