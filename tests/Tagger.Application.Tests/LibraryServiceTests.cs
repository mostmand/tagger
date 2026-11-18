using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Library;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class LibraryServiceTests
{
    private readonly IFileScanner _scanner = Substitute.For<IFileScanner>();
    private readonly ITrackReader _reader = Substitute.For<ITrackReader>();
    private readonly LibraryService _sut;

    public LibraryServiceTests()
    {
        _sut = new LibraryService(_scanner, _reader);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnTracks_WhenEveryFileCanBeRead()
    {
        // Arrange
        var first = TestTracks.Create("one.mp3");
        var second = TestTracks.Create("two.mp3");
        _scanner.FindAudioFilesAsync(Arg.Any<IEnumerable<string>>(), true, Arg.Any<CancellationToken>())
            .Returns(["one.mp3", "two.mp3"]);
        _reader.ReadAsync("one.mp3", Arg.Any<CancellationToken>()).Returns(first);
        _reader.ReadAsync("two.mp3", Arg.Any<CancellationToken>()).Returns(second);

        // Act
        var result = await _sut.LoadAsync(["music"], recursive: true);

        // Assert
        Assert.Equal(2, result.Tracks.Count);
        Assert.Empty(result.Failures);
        Assert.Equal(2, result.ScannedFileCount);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportFailure_WhenOneFileCannotBeRead()
    {
        // Arrange
        _scanner.FindAudioFilesAsync(Arg.Any<IEnumerable<string>>(), true, Arg.Any<CancellationToken>())
            .Returns(["good.mp3", "bad.mp3"]);
        _reader.ReadAsync("good.mp3", Arg.Any<CancellationToken>()).Returns(TestTracks.Create("good.mp3"));
        _reader.ReadAsync("bad.mp3", Arg.Any<CancellationToken>())
            .Returns<Task<Track>>(_ => throw new InvalidDataException("Corrupt tag"));

        // Act
        var result = await _sut.LoadAsync(["music"], recursive: true);

        // Assert
        Assert.Single(result.Tracks);
        Assert.Single(result.Failures);
        Assert.Equal("bad.mp3", result.Failures[0].Path);
        Assert.Contains("Corrupt tag", result.Failures[0].Message);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnEmpty_WhenNoPathsAreProvided()
    {
        // Arrange

        // Act
        var result = await _sut.LoadAsync([], recursive: true);

        // Assert
        Assert.Empty(result.Tracks);
        Assert.Empty(result.Failures);
        await _scanner.DidNotReceiveWithAnyArgs()
            .FindAudioFilesAsync(default!, default, default);
    }
}
