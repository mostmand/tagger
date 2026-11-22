using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Playback;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;

namespace Tagger.Desktop.ViewModels;

/// <summary>Projects the playback service into bar state and marshals events to the UI thread.</summary>
public sealed partial class PlayerViewModel : ViewModelBase, IDisposable
{
    private readonly IPlaybackService _playback;
    private readonly IUiDispatcher _dispatcher;
    private bool _isScrubbing;
    private bool _suppressPreferences;
    private string? _currentFilePath;
    private bool _disposed;

    [ObservableProperty]
    private bool _hasTrack;

    [ObservableProperty]
    private bool _showBar;

    [ObservableProperty]
    private string _title = "Nothing playing";

    [ObservableProperty]
    private string _subtitle = "Double-click a track or press Space";

    [ObservableProperty]
    private byte[]? _artworkBytes;

    [ObservableProperty]
    private bool _hasArtwork;

    [ObservableProperty]
    private string _positionText = "0:00";

    [ObservableProperty]
    private string _durationText = "0:00";

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private double _volume = 80d;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isOpening;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private bool _canGoPrevious;

    [ObservableProperty]
    private RepeatMode _repeat = RepeatMode.Off;

    [ObservableProperty]
    private int _queuePosition;

    [ObservableProperty]
    private int _queueLength;

    [ObservableProperty]
    private string? _errorMessage;

    public PlayerViewModel(IPlaybackService playback, IUiDispatcher dispatcher)
    {
        _playback = playback;
        _dispatcher = dispatcher;

        _playback.StateChanged += OnStateChanged;
        _playback.CurrentTrackChanged += OnCurrentTrackChanged;
        _playback.Failed += OnFailed;

        var snapshot = _playback.Snapshot;
        _volume = Math.Clamp(snapshot.Volume, 0d, 1d) * 100d;
        _isMuted = snapshot.IsMuted;
        _repeat = _playback.Repeat;
        _playback.SetVolume(_volume / 100d);
        _playback.SetMuted(_isMuted);
        _playback.SetRepeat(_repeat);
        ApplyCurrentTrack();
        ApplySnapshot(snapshot);
    }

    public event EventHandler? PreferencesChanged;

    public event EventHandler<string>? PlaybackFailed;

    public event EventHandler? CurrentTrackChanged;

    public string? CurrentFilePath => _currentFilePath;

    public string PlayPauseGlyph => IsOpening ? "…" : IsPlaying ? "⏸" : "▶";

    public string PlayPauseTooltip => IsPlaying ? "Pause (Space)" : "Play (Space)";

    public string MuteTooltip => IsMuted ? "Unmute" : "Mute";

    public string RepeatLabel => Repeat switch
    {
        RepeatMode.All => "All",
        RepeatMode.One => "One",
        _ => "Off"
    };

    public string RepeatTooltip => Repeat switch
    {
        RepeatMode.Off => "Repeat off — click to repeat the queue",
        RepeatMode.All => "Repeat queue — click to repeat one track",
        _ => "Repeat one — click to turn repeat off"
    };

    public bool IsRepeatActive => Repeat != RepeatMode.Off;

    public string QueueText => HasTrack ? $"{QueuePosition} / {QueueLength}" : string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Task PlayAsync(
        IReadOnlyList<Track> queue,
        int startIndex,
        CancellationToken cancellationToken = default) =>
        RunAsync(() => _playback.PlayAsync(queue, startIndex, cancellationToken));

    public Task TogglePlaybackAsync() => RunAsync(() => _playback.ToggleAsync());

    public Task NextAsync() => RunAsync(() => _playback.NextAsync());

    public Task PreviousAsync() => RunAsync(() => _playback.PreviousAsync());

    public void StopPlayback() => _playback.Stop();

    public void RemoveFromQueue(IReadOnlyList<string> paths) => _playback.RemoveFromQueue(paths);

    public void UpdateTrack(Track track, string? previousPath = null) =>
        _playback.UpdateTrack(track, previousPath);

    public PlaybackContinuation? PrepareForMutation(IReadOnlyList<string> paths) =>
        _playback.PrepareForMutation(paths);

    public Task ResumeAfterMutationAsync(
        PlaybackContinuation continuation,
        Track? updatedTrack = null,
        CancellationToken cancellationToken = default) =>
        _playback.ResumeAfterMutationAsync(continuation, updatedTrack, cancellationToken);

    public void ApplyPreferences(double volume, bool muted, RepeatMode repeat)
    {
        _suppressPreferences = true;
        try
        {
            Volume = Math.Clamp(volume, 0d, 1d) * 100d;
            IsMuted = muted;
            Repeat = repeat;
        }
        finally
        {
            _suppressPreferences = false;
        }
    }

    /// <summary>Freezes player-driven position updates while the user drags the seek bar.</summary>
    public void BeginScrub() => _isScrubbing = true;

    /// <summary>Reflects the pointer position while scrubbing without committing a seek.</summary>
    public void UpdateScrub(double seconds)
    {
        if (!_isScrubbing)
        {
            return;
        }

        var maximum = DurationSeconds > 0 ? DurationSeconds : seconds;
        var clamped = Math.Clamp(seconds, 0d, maximum);
        PositionSeconds = clamped;
        PositionText = FormatTime(clamped);
    }

    public void EndScrub(double seconds)
    {
        _isScrubbing = false;
        SeekTo(seconds);
    }

    public void SeekTo(double seconds)
    {
        if (!HasTrack)
        {
            return;
        }

        var maximum = DurationSeconds > 0 ? DurationSeconds : seconds;
        var clamped = Math.Clamp(seconds, 0d, maximum);
        _playback.Seek(TimeSpan.FromSeconds(clamped));
        PositionSeconds = clamped;
        PositionText = FormatTime(clamped);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _playback.StateChanged -= OnStateChanged;
        _playback.CurrentTrackChanged -= OnCurrentTrackChanged;
        _playback.Failed -= OnFailed;
        _playback.Stop();
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(PlayPauseTooltip));
    }

    partial void OnIsOpeningChanged(bool value) => OnPropertyChanged(nameof(PlayPauseGlyph));

    partial void OnHasTrackChanged(bool value) => OnPropertyChanged(nameof(QueueText));

    partial void OnQueuePositionChanged(int value) => OnPropertyChanged(nameof(QueueText));

    partial void OnQueueLengthChanged(int value) => OnPropertyChanged(nameof(QueueText));

    partial void OnRepeatChanged(RepeatMode value)
    {
        OnPropertyChanged(nameof(RepeatLabel));
        OnPropertyChanged(nameof(RepeatTooltip));
        OnPropertyChanged(nameof(IsRepeatActive));
        _playback.SetRepeat(value);
        CanGoNext = _playback.CanGoNext;
        CanGoPrevious = _playback.CanGoPrevious;
        RaisePreferencesChanged();
    }

    partial void OnVolumeChanged(double value)
    {
        if (!double.IsFinite(value))
        {
            return;
        }

        var clamped = Math.Clamp(value, 0d, 100d);
        if (Math.Abs(clamped - value) > double.Epsilon)
        {
            Volume = clamped;
            return;
        }

        _playback.SetVolume(clamped / 100d);
        RaisePreferencesChanged();
    }

    partial void OnIsMutedChanged(bool value)
    {
        OnPropertyChanged(nameof(MuteTooltip));
        _playback.SetMuted(value);
        RaisePreferencesChanged();
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void RaisePreferencesChanged()
    {
        if (!_suppressPreferences)
        {
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyCurrentTrack()
    {
        var track = _playback.CurrentTrack;

        HasTrack = track is not null;
        Title = track?.DisplayTitle ?? "Nothing playing";
        Subtitle = track is null
            ? "Double-click a track or press Space"
            : BuildSubtitle(track);
        ArtworkBytes = track?.CoverArt?.Data;
        HasArtwork = ArtworkBytes is { Length: > 0 };
        QueuePosition = _playback.QueueIndex >= 0 ? _playback.QueueIndex + 1 : 0;
        QueueLength = _playback.QueueCount;
        _currentFilePath = track?.FilePath;
        CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySnapshot(PlaybackSnapshot snapshot)
    {
        IsPlaying = snapshot.State == PlaybackState.Playing;
        IsOpening = snapshot.State == PlaybackState.Opening;
        CanGoNext = _playback.CanGoNext;
        CanGoPrevious = _playback.CanGoPrevious;
        QueuePosition = _playback.QueueIndex >= 0 ? _playback.QueueIndex + 1 : 0;
        QueueLength = _playback.QueueCount;

        if (!_isScrubbing)
        {
            PositionSeconds = Math.Max(0d, snapshot.Position.TotalSeconds);
            PositionText = FormatTime(PositionSeconds);
        }

        DurationSeconds = Math.Max(0d, snapshot.Duration.TotalSeconds);
        DurationText = FormatTime(DurationSeconds);

        if (snapshot.State == PlaybackState.Failed)
        {
            ErrorMessage = snapshot.Error ?? "This file could not be played.";
        }
        else if (snapshot.State is PlaybackState.Idle or PlaybackState.Stopped or PlaybackState.Ended)
        {
            ErrorMessage = null;
        }
    }

    private void OnStateChanged(object? sender, PlaybackSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.Post(() => ApplySnapshot(snapshot));
    }

    private void OnCurrentTrackChanged(object? sender, EventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.Post(() =>
        {
            ApplyCurrentTrack();
            ApplySnapshot(_playback.Snapshot);
        });
    }

    private void OnFailed(object? sender, PlaybackFailure failure)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.Post(() =>
        {
            ErrorMessage = failure.Message;
            PlaybackFailed?.Invoke(this, failure.Message);
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private static string BuildSubtitle(Track track)
    {
        var parts = new[] { track.DisplayArtist, track.DisplayAlbum }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length > 0 ? string.Join(" · ", parts) : track.FileName;
    }

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds))
        {
            return "0:00";
        }

        var value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }
}
