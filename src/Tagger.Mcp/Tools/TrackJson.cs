using System.Text.Json.Nodes;
using Tagger.Domain.Tracks;

namespace Tagger.Mcp.Tools;

internal static class TrackJson
{
    public static JsonObject Track(Tagger.Domain.Tracks.Track track) => new()
    {
        ["path"] = track.FilePath,
        ["fileName"] = track.FileName,
        ["format"] = track.Format,
        ["duration"] = track.Audio?.DurationText,
        ["bitrate"] = track.Audio?.BitrateKbps,
        ["lossless"] = track.Audio?.IsLossless,
        ["tags"] = Tags(track.Tags),
        ["hasArtwork"] = track.CoverArt is not null
    };

    public static JsonObject Tags(TrackTags tags)
    {
        var result = new JsonObject();
        foreach (var info in TagFieldCatalog.All)
        {
            var value = tags.Get(info.Field);
            if (value is not null)
            {
                result[info.Token] = value;
            }
        }

        return result;
    }

    public static JsonArray Failures(IEnumerable<Application.Library.TrackOperationFailure> failures)
    {
        var array = new JsonArray();
        foreach (var failure in failures)
        {
            array.Add(new JsonObject
            {
                ["path"] = failure.Path,
                ["message"] = failure.Message
            });
        }

        return array;
    }
}
