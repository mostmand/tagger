using Tagger.Application.Renaming;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Playback;

/// <summary>
/// Owns the queue and the playback state machine. All commands are serialized so
/// rapid double-clicks cannot open two files at once.
/// </summary>
public sealed class PlaybackService : IPlaybackService
{
    private static readonly TimeSpan RestartThreshold = TimeSpan.FromSeconds(3);

    private readonly IAudioPlayer _player;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PlaybackQueue _queue = new([]);
    private PlaybackSnapshot _snapshot = PlaybackSnapshot.Initial;
    private Track? _currentTrack;
    private RepeatMode _repeat = RepeatMode.Off;
    private bool _acceptPlayerEvents;
    private int _startGeneration;
    private bool _disposed;

    public PlaybackService(IAudioPlayer player)
    {
        _player = player;
        _player.SnapshotChanged += OnPlayerSnapshot;
        _player.Failed += OnPlayerFailed;
        _player.PlaybackEnded += OnPlayerEnded;
    }

    public PlaybackSnapshot Snapshot => _snapshot;

    public Track? CurrentTrack => _currentTrack;

    public RepeatMode Repeat => _repeat;

    public bool CanGoNext => _queue.HasNext(_repeat);

    public bool CanGoPrevious => _queue.HasPrevious(_repeat);

    public int QueueIndex => _queue.CurrentIndex;

    public int QueueCount => _queue.Count;

    public event EventHandler<PlaybackSnapshot>? StateChanged;

    public event EventHandler? CurrentTrackChanged;

    public event EventHandler<PlaybackFailure>? Failed;

    public async Task PlayAsync(
        IReadOnlyList<Track> queue,
        int startIndex,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _queue = new PlaybackQueue(queue, startIndex);
            SetCurrentTrack(_queue.Current);

            if (_currentTrack is null)
            {
                _acceptPlayerEvents = false;
                _startGeneration++;
                try
                {
                    _player.Stop();
                }
                catch (Exception)
                {
                    // State below is authoritative.
                }

                Publish(PlaybackState.Idle, resetPosition: true, duration: TimeSpan.Zero);
                return;
            }

            await StartCurrentAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ToggleAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (_snapshot.State)
            {
                case PlaybackState.Playing:
                    _player.Pause();
                    Publish(PlaybackState.Paused);
                    break;
                case PlaybackState.Paused:
                    _player.Resume();
                    Publish(PlaybackState.Playing);
                    break;
                case PlaybackState.Opening:
                    break;
                default:
                    if (_currentTrack is not null)
                    {
                        await StartCurrentAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentTrack is null)
            {
                return;
            }

            var next = _queue.MoveNext(_repeat);
            if (next is null)
            {
                StopCore();
                return;
            }

            SetCurrentTrack(next);
            await StartCurrentAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentTrack is null)
            {
                return;
            }

            if (_snapshot.Position > RestartThreshold)
            {
                Seek(TimeSpan.Zero);
                return;
            }

            var previous = _queue.MovePrevious(_repeat);
            if (previous is null)
            {
                Seek(TimeSpan.Zero);
                return;
            }

            SetCurrentTrack(previous);
            await StartCurrentAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        _acceptPlayerEvents = false;
        _startGeneration++;
        try
        {
            _player.Stop();
        }
        catch (Exception)
        {
            // A native stop failure must not crash the UI; the state below is authoritative.
        }

        Publish(PlaybackState.Stopped, resetPosition: true);
    }

    public void Seek(TimeSpan position)
    {
        if (_snapshot.State is PlaybackState.Idle or PlaybackState.Stopped)
        {
            return;
        }

        var maximum = _snapshot.Duration > TimeSpan.Zero ? _snapshot.Duration : TimeSpan.MaxValue;
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position > maximum ? maximum : position;

        _player.Seek(clamped);
        Publish(_snapshot.State, position: clamped);
    }

    public void SetVolume(double volume)
    {
        var clamped = Math.Clamp(volume, 0d, 1d);
        _player.SetVolume(clamped);
        _snapshot = _snapshot with { Volume = clamped };
        StateChanged?.Invoke(this, _snapshot);
    }

    public void SetMuted(bool muted)
    {
        _player.SetMuted(muted);
        _snapshot = _snapshot with { IsMuted = muted };
        StateChanged?.Invoke(this, _snapshot);
    }

    public void SetRepeat(RepeatMode repeat)
    {
        _repeat = repeat;
    }

    public void RemoveFromQueue(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        var current = _currentTrack;
        var removesCurrent = current is not null &&
            paths.Any(path => PathComparison.Equals(path, current.FilePath));

        _queue.RemovePaths(paths);

        if (removesCurrent)
        {
            _acceptPlayerEvents = false;
            try
            {
                _player.Stop();
            }
            catch (Exception)
            {
                // State below is authoritative.
            }

            SetCurrentTrack(null);
            Publish(PlaybackState.Stopped, resetPosition: true);
            return;
        }

        if (current is not null)
        {
            SetCurrentTrack(_queue.Current);
        }

        _snapshot = _snapshot with { FilePath = _currentTrack?.FilePath };
        StateChanged?.Invoke(this, _snapshot);
    }

    public void UpdateTrack(Track track, string? previousPath = null)
    {
        var wasCurrent = _currentTrack is not null &&
            (_currentTrack.Id == track.Id ||
             PathComparison.Equals(previousPath ?? track.FilePath, _currentTrack.FilePath));

        _queue.UpdateTrack(track, previousPath);

        if (wasCurrent)
        {
            SetCurrentTrack(track);
            _snapshot = _snapshot with { FilePath = track.FilePath };
            StateChanged?.Invoke(this, _snapshot);
        }
    }

    public PlaybackContinuation? PrepareForMutation(IReadOnlyList<string> paths)
    {
        var current = _currentTrack;
        if (current is null ||
            _snapshot.State is not (PlaybackState.Playing or PlaybackState.Paused or PlaybackState.Opening) ||
            !paths.Any(path => PathComparison.Equals(path, current.FilePath)))
        {
            return null;
        }

        var continuation = new PlaybackContinuation(
            current.Id,
            current.FilePath,
            _snapshot.Position,
            _snapshot.State == PlaybackState.Playing,
            _queue.CurrentIndex);

        Stop();
        return continuation;
    }

    public async Task ResumeAfterMutationAsync(
        PlaybackContinuation continuation,
        Track? updatedTrack = null,
        CancellationToken cancellationToken = default)
    {
        if (!continuation.WasPlaying)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (updatedTrack is not null)
            {
                _queue.UpdateTrack(updatedTrack, continuation.FilePath);
                if (_currentTrack is not null &&
                    PathComparison.Equals(_currentTrack.FilePath, continuation.FilePath))
                {
                    SetCurrentTrack(updatedTrack);
                }
            }

            var track = _queue.Current;
            if (track is null)
            {
                return;
            }

            SetCurrentTrack(track);
            await StartCurrentAsync(continuation.Position, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _acceptPlayerEvents = false;
        _player.SnapshotChanged -= OnPlayerSnapshot;
        _player.Failed -= OnPlayerFailed;
        _player.PlaybackEnded -= OnPlayerEnded;
        _player.Dispose();
        _gate.Dispose();
    }

    private async Task StartCurrentAsync(TimeSpan position, CancellationToken cancellationToken)
    {
        var track = _currentTrack;
        if (track is null)
        {
            Publish(PlaybackState.Idle, resetPosition: true);
            return;
        }

        _acceptPlayerEvents = true;
        var generation = ++_startGeneration;
        Publish(
            PlaybackState.Opening,
            position: position,
            duration: track.Audio?.Duration ?? TimeSpan.Zero,
            resetPosition: position == TimeSpan.Zero);

        bool accepted;
        try
        {
            accepted = await _player
                .PlayAsync(new PlaybackRequest(track.FilePath, position, track.DisplayTitle), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Publish(PlaybackState.Failed, error: exception.Message);
            return;
        }

        if (!accepted)
        {
            if (_snapshot.State != PlaybackState.Failed)
            {
                Publish(
                    PlaybackState.Failed,
                    error: _snapshot.Error ?? "Playback is unavailable. Install or bundle libVLC and try again.");
            }

            return;
        }

        if (!_acceptPlayerEvents)
        {
            return;
        }

        if (generation != _startGeneration)
        {
            return;
        }

        Publish(PlaybackState.Playing, position: position);
    }

    private void OnPlayerSnapshot(object? sender, PlaybackSnapshot snapshot)
    {
        if (_disposed || !_acceptPlayerEvents)
        {
            return;
        }

        // libVLC emits Stopped after EndReached. The end handler owns the
        // transition, so a late stop must not turn a natural end into a stop.
        if (snapshot.State == PlaybackState.Stopped && _currentTrack is not null)
        {
            return;
        }

        if (_currentTrack is not null &&
            snapshot.FilePath is not null &&
            !PathComparison.Equals(snapshot.FilePath, _currentTrack.FilePath))
        {
            return;
        }

        _snapshot = snapshot with { FilePath = _currentTrack?.FilePath ?? snapshot.FilePath };
        StateChanged?.Invoke(this, _snapshot);
    }

    private void OnPlayerFailed(object? sender, PlaybackFailure failure)
    {
        if (_disposed)
        {
            return;
        }

        if (_currentTrack is not null &&
            !PathComparison.Equals(failure.FilePath, _currentTrack.FilePath))
        {
            return;
        }

        _snapshot = _snapshot with
        {
            State = PlaybackState.Failed,
            FilePath = _currentTrack?.FilePath ?? failure.FilePath,
            Error = failure.Message
        };

        Failed?.Invoke(this, failure);
        StateChanged?.Invoke(this, _snapshot);
    }

    private void OnPlayerEnded(object? sender, PlaybackEndedEventArgs ended)
    {
        var generation = _startGeneration;
        _ = Task.Run(() => HandleEndedAsync(ended.FilePath, generation));
    }

    internal Task HandleEndedAsync(string filePath) =>
        HandleEndedAsync(filePath, _startGeneration);

    private async Task HandleEndedAsync(string filePath, long generation)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed ||
                !_acceptPlayerEvents ||
                generation != _startGeneration ||
                _currentTrack is null ||
                !PathComparison.Equals(filePath, _currentTrack.FilePath))
            {
                return;
            }

            if (_repeat == RepeatMode.One)
            {
                await StartCurrentAsync(TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var next = _queue.MoveNext(_repeat);
            if (next is null)
            {
                _acceptPlayerEvents = false;
                Publish(PlaybackState.Ended, position: _snapshot.Duration);
                return;
            }

            SetCurrentTrack(next);
            await StartCurrentAsync(TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void StopCore()
    {
        _acceptPlayerEvents = false;
        _startGeneration++;
        try
        {
            _player.Stop();
        }
        catch (Exception)
        {
            // State below is authoritative.
        }

        Publish(PlaybackState.Stopped, resetPosition: true);
    }

    private void SetCurrentTrack(Track? track)
    {
        if (ReferenceEquals(track, _currentTrack))
        {
            return;
        }

        _currentTrack = track;
        CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Publish(
        PlaybackState state,
        TimeSpan? position = null,
        TimeSpan? duration = null,
        string? error = null,
        bool resetPosition = false)
    {
        _snapshot = _snapshot with
        {
            State = state,
            Position = resetPosition ? TimeSpan.Zero : position ?? _snapshot.Position,
            Duration = duration ?? _snapshot.Duration,
            FilePath = _currentTrack?.FilePath,
            Error = error
        };

        StateChanged?.Invoke(this, _snapshot);
    }
}
