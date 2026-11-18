using System.Globalization;
using TagLib;
using TagLib.Id3v2;
using Tagger.Domain.Tracks;
using TagFile = TagLib.File;
using Tag = TagLib.Tag;

namespace Tagger.Infrastructure.Metadata;

/// <summary>Maps between the canonical tag model and TagLib# tags.</summary>
public static class TagLibTagMapper
{
    public static Track ReadTrack(string filePath, Guid id)
    {
        using var file = TagFile.Create(filePath);
        var info = new FileInfo(filePath);
        var tag = file.Tag;
        var tags = ReadTags(file, tag);

        return new Track
        {
            Id = id,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
            Tags = tags,
            CoverArt = ReadArtwork(tag),
            Audio = ReadAudio(file),
            FileSizeBytes = info.Exists ? info.Length : 0,
            LastModified = info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : DateTimeOffset.MinValue
        };
    }

    public static TrackTags ReadTags(TagLib.File file, Tag tag)
    {
        var values = new List<KeyValuePair<TagField, string?>>();

        Add(values, TagField.Title, tag.Title);
        Add(values, TagField.Artist, tag.FirstPerformer);
        Add(values, TagField.Album, tag.Album);
        Add(values, TagField.AlbumArtist, tag.FirstAlbumArtist);
        Add(values, TagField.Year, tag.Year > 0 ? tag.Year.ToString(CultureInfo.InvariantCulture) : null);
        Add(values, TagField.Track, tag.Track > 0 ? tag.Track.ToString(CultureInfo.InvariantCulture) : null);
        Add(values, TagField.TrackTotal, tag.TrackCount > 0 ? tag.TrackCount.ToString(CultureInfo.InvariantCulture) : null);
        Add(values, TagField.Disc, tag.Disc > 0 ? tag.Disc.ToString(CultureInfo.InvariantCulture) : null);
        Add(values, TagField.DiscTotal, tag.DiscCount > 0 ? tag.DiscCount.ToString(CultureInfo.InvariantCulture) : null);
        Add(values, TagField.Genre, tag.FirstGenre);
        Add(values, TagField.Comment, tag.Comment);
        Add(values, TagField.Composer, tag.FirstComposer);
        Add(values, TagField.Conductor, tag.Conductor);
        Add(values, TagField.Publisher, tag.Publisher);
        Add(values, TagField.Copyright, tag.Copyright);
        Add(values, TagField.Bpm, tag.BeatsPerMinute > 0 ? tag.BeatsPerMinute.ToString(CultureInfo.InvariantCulture) : null);
        Add(values, TagField.Isrc, tag.ISRC);
        Add(values, TagField.Grouping, tag.Grouping);
        Add(values, TagField.Subtitle, tag.Subtitle);

        var id3 = file.GetTag(TagTypes.Id3v2) as TagLib.Id3v2.Tag;
        if (id3 is not null)
        {
            Add(values, TagField.Language, GetId3Text(id3, "TLAN"));
            Add(values, TagField.EncodedBy, GetId3Text(id3, "TENC"));
            Add(values, TagField.Lyricist, GetId3Text(id3, "TEXT"));
            Add(values, TagField.Mood, GetId3Text(id3, "TMOO"));
            Add(values, TagField.OriginalArtist, GetId3Text(id3, "TOPE"));
            Add(values, TagField.OriginalAlbum, GetId3Text(id3, "TOAL"));
            Add(values, TagField.OriginalYear, GetId3Text(id3, "TDOR"));
        }
        else if (file.GetTag(TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph)
        {
            Add(values, TagField.Language, xiph.GetFirstField("LANGUAGE"));
            Add(values, TagField.EncodedBy, xiph.GetFirstField("ENCODEDBY"));
            Add(values, TagField.Lyricist, xiph.GetFirstField("LYRICIST"));
            Add(values, TagField.Mood, xiph.GetFirstField("MOOD"));
            Add(values, TagField.OriginalArtist, xiph.GetFirstField("ORIGINALARTIST"));
            Add(values, TagField.OriginalAlbum, xiph.GetFirstField("ORIGINALALBUM"));
            Add(values, TagField.OriginalYear, xiph.GetFirstField("ORIGINALYEAR") ?? xiph.GetFirstField("ORIGINALDATE"));
        }

        return new TrackTags(values);
    }

    public static AudioProperties? ReadAudio(TagLib.File file)
    {
        try
        {
            var codecs = file.Properties.Codecs.OfType<ICodec>().ToList();
            var codec = codecs.FirstOrDefault()?.Description ?? "Unknown";
            var lossless = codecs.Any(x => x is ILosslessAudioCodec);

            return new AudioProperties(
                file.Properties.Duration,
                file.Properties.AudioBitrate,
                file.Properties.AudioSampleRate,
                file.Properties.AudioChannels,
                codec,
                lossless);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static CoverArt? ReadArtwork(Tag tag)
    {
        var picture = tag.Pictures.FirstOrDefault(x => x.Type == PictureType.FrontCover)
            ?? tag.Pictures.FirstOrDefault();

        return picture is null
            ? null
            : new CoverArt(picture.Data.Data, picture.MimeType, picture.Description);
    }

    public static void ApplyTags(TagLib.File file, TrackTags tags)
    {
        var tag = file.Tag;
        tag.Title = tags.Get(TagField.Title);
        tag.Performers = ToArray(tags.Get(TagField.Artist));
        tag.Album = tags.Get(TagField.Album);
        tag.AlbumArtists = ToArray(tags.Get(TagField.AlbumArtist));
        tag.Year = ToUInt(tags.Get(TagField.Year));
        tag.Track = ToUInt(tags.Get(TagField.Track));
        tag.TrackCount = ToUInt(tags.Get(TagField.TrackTotal));
        tag.Disc = ToUInt(tags.Get(TagField.Disc));
        tag.DiscCount = ToUInt(tags.Get(TagField.DiscTotal));
        tag.Genres = ToArray(tags.Get(TagField.Genre));
        tag.Comment = tags.Get(TagField.Comment);
        tag.Composers = ToArray(tags.Get(TagField.Composer));
        tag.Conductor = tags.Get(TagField.Conductor);
        tag.Publisher = tags.Get(TagField.Publisher);
        tag.Copyright = tags.Get(TagField.Copyright);
        tag.BeatsPerMinute = ToUInt(tags.Get(TagField.Bpm));
        tag.ISRC = tags.Get(TagField.Isrc);
        tag.Grouping = tags.Get(TagField.Grouping);
        tag.Subtitle = tags.Get(TagField.Subtitle);

        ApplyExtendedTags(file, tags);
    }

    public static void ApplyArtwork(Tag tag, CoverArtUpdate artwork)
    {
        if (!artwork.ShouldUpdate)
        {
            return;
        }

        var pictures = tag.Pictures
            .Where(x => x.Type != PictureType.FrontCover)
            .ToList();

        if (artwork.Artwork is not null)
        {
            pictures.Add(new Picture(artwork.Artwork.Data)
            {
                Type = PictureType.FrontCover,
                MimeType = artwork.Artwork.MimeType,
                Description = artwork.Artwork.Description ?? string.Empty
            });
        }

        tag.Pictures = pictures.ToArray();
    }

    private static void ApplyExtendedTags(TagLib.File file, TrackTags tags)
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();

        if (extension is ".mp3" or ".wav" or ".aiff" or ".aif")
        {
            if (file.GetTag(TagTypes.Id3v2, true) is not TagLib.Id3v2.Tag id3)
            {
                return;
            }

            SetId3Text(id3, "TLAN", tags.Get(TagField.Language));
            SetId3Text(id3, "TENC", tags.Get(TagField.EncodedBy));
            SetId3Text(id3, "TEXT", tags.Get(TagField.Lyricist));
            SetId3Text(id3, "TMOO", tags.Get(TagField.Mood));
            SetId3Text(id3, "TOPE", tags.Get(TagField.OriginalArtist));
            SetId3Text(id3, "TOAL", tags.Get(TagField.OriginalAlbum));
            SetId3Text(id3, "TDOR", tags.Get(TagField.OriginalYear));
        }
        else if (extension is ".flac" or ".ogg" or ".opus" or ".spx")
        {
            if (file.GetTag(TagTypes.Xiph, true) is not TagLib.Ogg.XiphComment xiph)
            {
                return;
            }

            SetXiph(xiph, "LANGUAGE", tags.Get(TagField.Language));
            SetXiph(xiph, "ENCODEDBY", tags.Get(TagField.EncodedBy));
            SetXiph(xiph, "LYRICIST", tags.Get(TagField.Lyricist));
            SetXiph(xiph, "MOOD", tags.Get(TagField.Mood));
            SetXiph(xiph, "ORIGINALARTIST", tags.Get(TagField.OriginalArtist));
            SetXiph(xiph, "ORIGINALALBUM", tags.Get(TagField.OriginalAlbum));
            SetXiph(xiph, "ORIGINALYEAR", tags.Get(TagField.OriginalYear));
        }
    }

    private static string? GetId3Text(TagLib.Id3v2.Tag id3, string frameId)
    {
        var frame = TextInformationFrame.Get(id3, ByteVector.FromString(frameId, StringType.Latin1), false);
        return frame?.Text.FirstOrDefault();
    }

    private static void SetId3Text(TagLib.Id3v2.Tag id3, string frameId, string? value)
    {
        var ident = ByteVector.FromString(frameId, StringType.Latin1);
        if (string.IsNullOrWhiteSpace(value))
        {
            id3.RemoveFrames(ident);
        }
        else
        {
            id3.SetTextFrame(ident, new string[] { value.Trim() });
        }
    }

    private static void SetXiph(TagLib.Ogg.XiphComment xiph, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            xiph.RemoveField(key);
        }
        else
        {
            xiph.SetField(key, [value.Trim()]);
        }
    }

    private static void Add(List<KeyValuePair<TagField, string?>> values, TagField field, string? value) =>
        values.Add(new KeyValuePair<TagField, string?>(field, value));

    private static string[] ToArray(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];

    private static uint ToUInt(string? value) =>
        uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
