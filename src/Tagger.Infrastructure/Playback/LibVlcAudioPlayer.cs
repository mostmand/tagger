using LibVLCSharp.Shared;
using Tagger.Application.Playback;

namespace Tagger.Infrastructure.Playback;

/// <summary>
/// Thin LibVLCSharp adapter. It translates native events into application
/// snapshots, never throws from commands, and initializes the native library
/// lazily so the app still starts when libVLC is unavailable.
/// </summary>
public sealed class LibVlcAudioPlayer : IAudioPlayer
{
    private readonly object _sync = new();
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private string? _currentPath;
    private TimeSpan _position;
    private TimeSpan _duration;
    private TimeSpan? _pendingSeek;
    private double _volume = 1d;
    private bool _muted;
    private long _lastSecond = -1;
    private bool _disposed;

    public event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    public event EventHandler<PlaybackFailure>? Failed;

    public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;

    public Task<bool> PlayAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.FromResult(false);
        }

        lock (_sync)
        {
            if (!EnsureInitialized())
            {
                return Task.FromResult(false);
            }

            try
            {
                var media = new Media(_libVlc!, request.FilePath, FromType.FromPath);
                var previous = _media;
                _media = media;
                _player!.Media = media;
                previous?.Dispose();

                _currentPath = request.FilePath;
                _position = request.StartPosition;
                _duration = TimeSpan.Zero;
                _lastSecond = -1;
                _pendingSeek = request.StartPosition > TimeSpan.Zero ? request.StartPosition : null;

                if (!_player.Play())
                {
                    RaiseFailure(request.FilePath, "This file could not be played.");
                    return Task.FromResult(false);
                }


                return Task.FromResult(true);
            }
            catch (Exception exception)
            {
                RaiseFailure(request.FilePath, VlcPlaybackMapper.MapError(exception));
                return Task.FromResult(false);
            }
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            _player?.Pause();
            RaiseSnapshot(PlaybackState.Paused);
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            _player?.Play();
            RaiseSnapshot(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            try
            {
                _player?.Stop();
            }
            catch (Exception)
            {
                // The state below remains authoritative; a stop failure is not fatal.
            }

            _pendingSeek = null;
            _position = TimeSpan.Zero;
            _lastSecond = -1;
            RaiseSnapshot(PlaybackState.Stopped);
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_sync)
        {
            var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            if (_player is null)
            {
                return;
            }

            var state = VlcPlaybackMapper.MapState(_player.State);
            if (state is PlaybackState.Opening)
            {
                _pendingSeek = clamped;
                _position = clamped;
                return;
            }

            _player.Time = (long)clamped.TotalMilliseconds;
            _position = clamped;
            RaiseSnapshot(state);
        }
    }

    public void SetVolume(double volume)
    {
        lock (_sync)
        {
            _volume = Math.Clamp(volume, 0d, 1d);
            if (_player is not null)
            {
                _player.Volume = (int)Math.Round(_volume * 100d);
            }
        }
    }

    public void SetMuted(bool muted)
    {
        lock (_sync)
        {
            _muted = muted;
            if (_player is not null)
            {
                _player.Mute = muted;
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _player?.Stop();
            }
            catch (Exception)
            {
                // Disposal must not throw.
            }

            _player?.Dispose();
            _media?.Dispose();
            _libVlc?.Dispose();
            _player = null;
            _media = null;
            _libVlc = null;
        }
    }

    private bool EnsureInitialized()
    {
        if (_player is not null)
        {
            return true;
        }

        try
        {
            if (OperatingSystem.IsLinux())
            {
                var bundle = VlcRuntimeBootstrap.FindBundle();
                if (bundle is not null && !VlcRuntimeBootstrap.TryPrepare(bundle))
                {
                    RaiseFailure(string.Empty, "The bundled libVLC runtime could not be loaded. Reinstall Tagger.");
                    return false;
                }
            }

            Core.Initialize();
            _libVlc = new LibVLC("--no-video", "--quiet");
            _player = new MediaPlayer(_libVlc)
            {
                Volume = (int)Math.Round(_volume * 100d),
                Mute = _muted
            };

            _player.Playing += OnPlaying;
            _player.Paused += (_, _) => RaiseSnapshot(PlaybackState.Paused);
            _player.Stopped += (_, _) => RaiseSnapshot(PlaybackState.Stopped);
            _player.EndReached += OnEndReached;
            _player.EncounteredError += OnEncounteredError;
            _player.TimeChanged += OnTimeChanged;
            _player.LengthChanged += OnLengthChanged;
            return true;
        }
        catch (Exception exception)
        {
            RaiseFailure(string.Empty, VlcPlaybackMapper.MapError(exception));
            return false;
        }
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        TimeSpan? pending;
        lock (_sync)
        {
            pending = _pendingSeek;
            if (pending is not null)
            {
                _position = pending.Value;
                _pendingSeek = null;
            }

            RaiseSnapshot(PlaybackState.Playing);
        }

        if (pending is { } start)
        {
            // libvlc event callbacks must not call back into the player; apply
            // the deferred seek from a worker thread.
            _ = Task.Run(() =>
            {
                lock (_sync)
                {
                    if (_player is not null)
                    {
                        _player.Time = (long)start.TotalMilliseconds;
                    }
                }
            });
        }
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        string? path;
        lock (_sync)
        {
            path = _currentPath;
            _position = _duration;
            RaiseSnapshot(PlaybackState.Ended);
        }

        if (path is not null)
        {
            PlaybackEnded?.Invoke(this, new PlaybackEndedEventArgs(path));
        }
    }

    private void OnEncounteredError(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            RaiseFailure(_currentPath ?? string.Empty, "This file could not be played.");
        }
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        lock (_sync)
        {
            var position = VlcPlaybackMapper.NormalizeTime(e.Time);
            var second = (long)position.TotalSeconds;
            if (second == _lastSecond)
            {
                return;
            }

            _lastSecond = second;
            _position = position;
            RaiseSnapshot(_player is null ? PlaybackState.Idle : VlcPlaybackMapper.MapState(_player.State));
        }
    }

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        lock (_sync)
        {
            _duration = VlcPlaybackMapper.NormalizeTime(e.Length);
            RaiseSnapshot(_player is null ? PlaybackState.Idle : VlcPlaybackMapper.MapState(_player.State));
        }
    }

    private void RaiseFailure(string filePath, string message)
    {
        var failure = new PlaybackFailure(filePath, message);
        Failed?.Invoke(this, failure);
        RaiseSnapshot(PlaybackState.Failed, failure.Message);
    }

    private void RaiseSnapshot(PlaybackState state, string? error = null)
    {
        if (_disposed)
        {
            return;
        }

        SnapshotChanged?.Invoke(
            this,
            new PlaybackSnapshot(
                state,
                _position,
                _duration,
                _volume,
                _muted,
                _currentPath,
                error));
    }
}
