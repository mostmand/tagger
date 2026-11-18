namespace Tagger.Domain.Tracks;

public sealed record AudioProperties(
    TimeSpan Duration,
    int BitrateKbps,
    int SampleRate,
    int Channels,
    string Codec,
    bool IsLossless)
{
    public string DurationText
    {
        get
        {
            var totalSeconds = (int)Math.Round(Duration.TotalSeconds);
            return totalSeconds >= 3600
                ? $"{totalSeconds / 3600}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}"
                : $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }
    }

    public string BitrateText => IsLossless ? "Lossless" : $"{BitrateKbps} kbps";
}
