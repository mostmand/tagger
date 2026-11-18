using Tagger.Domain.Searching;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class TrackSorterTests
{
    private readonly List<Track> _tracks;

    public TrackSorterTests()
    {
        _tracks =
        [
            Create("track 10.mp3", "Zulu"),
            Create("track 2.mp3", "Alpha"),
            Create("track 1.mp3", "Bravo")
        ];
    }

    [Fact]
    public void Sort_ShouldOrderNumerically_WhenFileNamesContainNumbers()
    {
        // Arrange
        var _sut = TrackSorter.Sort;

        // Act
        var result = _sut(_tracks, TrackSortField.FileName);

        // Assert
        Assert.Equal(["track 1.mp3", "track 2.mp3", "track 10.mp3"], result.Select(x => x.FileName));
    }

    [Fact]
    public void Sort_ShouldOrderByTitle_WhenTitleFieldRequested()
    {
        // Arrange
        var _sut = TrackSorter.Sort;

        // Act
        var result = _sut(_tracks, TrackSortField.Title);

        // Assert
        Assert.Equal(["Alpha", "Bravo", "Zulu"], result.Select(x => x.DisplayTitle));
    }

    [Fact]
    public void Sort_ShouldReverseOrder_WhenDescending()
    {
        // Arrange
        var _sut = TrackSorter.Sort;

        // Act
        var result = _sut(_tracks, TrackSortField.FileName, true);

        // Assert
        Assert.Equal("track 10.mp3", result[0].FileName);
    }

    [Fact]
    public void Compare_ShouldPreferShorterNumber_WhenLeadingZerosDiffer()
    {
        // Arrange
        var _sut = NaturalStringComparer.Instance;

        // Act
        var result = _sut.Compare("track 02", "track 2");

        // Assert
        Assert.Equal(0, result);
    }

    private static Track Create(string fileName, string title) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = Path.Combine("music", fileName),
        FileName = fileName,
        Extension = "mp3",
        Tags = new TrackTags([new(TagField.Title, title)])
    };
}
