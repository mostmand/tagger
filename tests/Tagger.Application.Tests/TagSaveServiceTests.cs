using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Editing;
using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class TagSaveServiceTests
{
    private readonly ITrackWriter _writer = Substitute.For<ITrackWriter>();
    private readonly IChangeJournal _journal = Substitute.For<IChangeJournal>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly TagSaveService _sut;
    private readonly DateTimeOffset _now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    public TagSaveServiceTests()
    {
        _clock.UtcNow.Returns(_now);
        _sut = new TagSaveService(_writer, _journal, _clock);
    }

    [Fact]
    public async Task ApplyAsync_ShouldWriteTagsAndRecordJournal_WhenRequestHasChanges()
    {
        // Arrange
        var track = TestTracks.Create(title: "Old");
        var request = new TagEditRequest(
            track,
            TagChangeSet.From([new TagChange(TagField.Title, "New")]));

        // Act
        var result = await _sut.ApplyAsync([request], "Edit tags");

        // Assert
        await _writer.Received(1).WriteAsync(
            track.FilePath,
            Arg.Is<TrackTags>(x => x.Get(TagField.Title) == "New"),
            CoverArtUpdate.Keep,
            Arg.Any<CancellationToken>());
        await _journal.Received(1).RecordAsync(
            Arg.Is<ChangeTransaction>(x => x.Summary == "Edit tags" && x.TagWrites.Count == 1),
            Arg.Any<CancellationToken>());
        Assert.Single(result.UpdatedTracks);
        Assert.Equal(_now, result.UpdatedTracks[0].LastModified);
        Assert.NotNull(result.Transaction);
    }

    [Fact]
    public async Task ApplyAsync_ShouldSkipWriter_WhenRequestHasNoChanges()
    {
        // Arrange
        var request = new TagEditRequest(TestTracks.Create(), TagChangeSet.Empty);

        // Act
        var result = await _sut.ApplyAsync([request], "Nothing");

        // Assert
        await _writer.DidNotReceiveWithAnyArgs().WriteAsync(default!, default!, default, default);
        Assert.Empty(result.UpdatedTracks);
        Assert.Null(result.Transaction);
    }

    [Fact]
    public async Task ApplyAsync_ShouldReturnFailure_WhenWriterThrows()
    {
        // Arrange
        var track = TestTracks.Create();
        var request = new TagEditRequest(
            track,
            TagChangeSet.From([new TagChange(TagField.Title, "New")]));
        _writer.WriteAsync(Arg.Any<string>(), Arg.Any<TrackTags>(), Arg.Any<CoverArtUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Disk full")));

        // Act
        var result = await _sut.ApplyAsync([request], "Edit tags");

        // Assert
        Assert.Empty(result.UpdatedTracks);
        Assert.Single(result.Failures);
        Assert.Contains("Disk full", result.Failures[0].Message);
        await _journal.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task ApplyAsync_ShouldRecordArtworkChange_WhenCoverArtIsRemoved()
    {
        // Arrange
        var artwork = new CoverArt([1, 2, 3], "image/png");
        var track = TestTracks.Create(artwork: artwork);
        var request = new TagEditRequest(track, TagChangeSet.Empty, CoverArtUpdate.Remove);

        // Act
        var result = await _sut.ApplyAsync([request], "Remove artwork");

        // Assert
        await _writer.Received(1).WriteAsync(
            track.FilePath,
            track.Tags,
            CoverArtUpdate.Remove,
            Arg.Any<CancellationToken>());
        await _journal.Received(1).RecordAsync(
            Arg.Is<ChangeTransaction>(x => x.TagWrites[0].BeforeArtwork == artwork && x.TagWrites[0].AfterArtwork == null),
            Arg.Any<CancellationToken>());
        Assert.Null(result.UpdatedTracks[0].CoverArt);
    }
}
