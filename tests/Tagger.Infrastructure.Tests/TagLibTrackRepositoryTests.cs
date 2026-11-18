using Tagger.Domain.Tracks;
using Tagger.Infrastructure.Metadata;
using Xunit;

namespace Tagger.Infrastructure.Tests;

public sealed class TagLibTrackRepositoryTests
{
    [Fact]
    public async Task WriteAsync_ShouldRoundTripCoreTags_WhenFileIsWritable()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "tagger-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "tone.wav");
        await System.IO.File.WriteAllBytesAsync(path, CreateSilentWav());
        var _sut = new TagLibTrackRepository();
        var tags = new TrackTags(
        [
            new(TagField.Title, "Northern Lights"),
            new(TagField.Artist, "Aurora"),
            new(TagField.Album, "Static Bloom"),
            new(TagField.Year, "2026"),
            new(TagField.Track, "2")
        ]);

        try
        {
            // Act
            await _sut.WriteAsync(path, tags, CoverArtUpdate.Keep);
            var track = await _sut.ReadAsync(path);

            // Assert
            Assert.Equal("Northern Lights", track.Tags.Get(TagField.Title));
            Assert.Equal("Aurora", track.Tags.Get(TagField.Artist));
            Assert.Equal("Static Bloom", track.Tags.Get(TagField.Album));
            Assert.Equal("2026", track.Tags.Get(TagField.Year));
            Assert.Equal("2", track.Tags.Get(TagField.Track));
            Assert.True(track.FileSizeBytes > 0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static byte[] CreateSilentWav()
    {
        const int sampleRate = 8000;
        const int seconds = 1;
        var dataLength = sampleRate * seconds * 2;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
        writer.Flush();
        return stream.ToArray();
    }
}
