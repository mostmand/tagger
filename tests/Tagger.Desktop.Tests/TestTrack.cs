using Tagger.Domain.Tracks;

namespace Tagger.Desktop.Tests;

internal static class TestTrack
{
    public static Track Create(
        string fileName = "song.mp3",
        string? title = "Song",
        string? artist = "Artist",
        string? album = null,
        string? year = null,
        CoverArt? artwork = null,
        string folder = "music")
    {
        var path = Path.Combine(folder, fileName);
        return new Track
        {
            Id = Guid.NewGuid(),
            FilePath = path,
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
            Tags = new TrackTags(
            [
                new(TagField.Title, title),
                new(TagField.Artist, artist),
                new(TagField.Album, album),
                new(TagField.Year, year)
            ]),
            CoverArt = artwork,
            Audio = new AudioProperties(TimeSpan.FromMinutes(3), 320, 44100, 2, "MP3", false),
            FileSizeBytes = 1024,
            LastModified = DateTimeOffset.UnixEpoch
        };
    }
}
