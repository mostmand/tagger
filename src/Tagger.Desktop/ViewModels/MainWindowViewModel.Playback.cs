using CommunityToolkit.Mvvm.Input;
using Tagger.Application.Playback;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private CancellationTokenSource? _playbackSettingsSave;

    public PlayerViewModel Player { get; }

    public bool CanPlay => HasTracks;

    public async Task PlaySelectionAsync()
    {
        IReadOnlyList<TrackRowViewModel> rows = _selectedRows.Count > 0
            ? _selectedRows.ToList()
            : VisibleTracks.ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var queue = BuildVisibleQueue();
        var start = queue.IndexOf(rows[0]);
        await Player.PlayAsync(queue.Select(row => row.Track).ToList(), Math.Max(start, 0));
    }

    public Task PlayRowAsync(TrackRowViewModel row)
    {
        var queue = BuildVisibleQueue();
        var start = queue.IndexOf(row);
        return start < 0
            ? Task.CompletedTask
            : Player.PlayAsync(queue.Select(candidate => candidate.Track).ToList(), start);
    }

    public Task TogglePlaybackAsync() =>
        Player.HasTrack ? Player.TogglePlaybackAsync() : PlaySelectionAsync();

    public Task PlayNextAsync() => Player.HasTrack ? Player.NextAsync() : Task.CompletedTask;

    public Task PlayPreviousAsync() => Player.HasTrack ? Player.PreviousAsync() : Task.CompletedTask;

    [RelayCommand]
    private Task PlaySelection() => PlaySelectionAsync();

    private List<TrackRowViewModel> BuildVisibleQueue() => VisibleTracks.ToList();

    private void OnPlayerPreferencesChanged(object? sender, EventArgs e)
    {
        Settings = Settings with
        {
            PlaybackVolume = Player.Volume / 100d,
            PlaybackMuted = Player.IsMuted,
            PlaybackRepeat = Player.Repeat
        };

        _playbackSettingsSave?.Cancel();
        var save = new CancellationTokenSource();
        _playbackSettingsSave = save;
        SavePlaybackSettingsAsync(save);
    }

    private async void SavePlaybackSettingsAsync(CancellationTokenSource save)
    {
        try
        {
            await Task.Delay(400, save.Token);
            await SaveSettingsAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // A newer change won the debounce.
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not save playback settings: {exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_playbackSettingsSave, save))
            {
                _playbackSettingsSave = null;
            }

            save.Dispose();
        }
    }

    private void OnPlayerTrackChanged(object? sender, EventArgs e)
    {
        var path = Player.CurrentFilePath;
        foreach (var row in _allTracks)
        {
            row.IsCurrent = path is not null && PathComparisonEquals(row.FilePath, path);
        }
    }

    private async Task ResumePlaybackAsync(PlaybackContinuation? continuation)
    {
        if (continuation is null)
        {
            return;
        }

        var track = FindTrackByPath(continuation.FilePath);
        if (track is null)
        {
            StatusMessage += " Playback stopped.";
            return;
        }

        await Player.ResumeAfterMutationAsync(continuation, track);
    }

    private Track? FindTrackByPath(string path) =>
        _allTracks.FirstOrDefault(row => PathComparisonEquals(row.FilePath, path))?.Track;
}
