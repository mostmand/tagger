using Tagger.Application.Renaming;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Playback;

/// <summary>Pure queue math: no player, no I/O, no UI.</summary>
public sealed class PlaybackQueue
{
    private readonly List<Track> _items;
    private int _index;

    public PlaybackQueue(IReadOnlyList<Track> items, int startIndex = 0)
    {
        _items = [.. items];
        _index = _items.Count == 0 ? -1 : Math.Clamp(startIndex, 0, _items.Count - 1);
    }

    public IReadOnlyList<Track> Items => _items;

    public int Count => _items.Count;

    public int CurrentIndex => _index;

    public Track? Current => _index >= 0 && _index < _items.Count ? _items[_index] : null;

    public bool Contains(string filePath) =>
        _items.Any(item => PathComparison.Equals(item.FilePath, filePath));

    public bool HasNext(RepeatMode repeat) =>
        _items.Count > 0 &&
        (repeat == RepeatMode.All || _index >= 0 && _index < _items.Count - 1);

    public bool HasPrevious(RepeatMode repeat) =>
        _items.Count > 0 &&
        (repeat == RepeatMode.All || _index > 0);

    public Track? MoveNext(RepeatMode repeat)
    {
        if (_items.Count == 0)
        {
            return null;
        }

        if (_index < _items.Count - 1)
        {
            _index++;
            return Current;
        }

        if (repeat == RepeatMode.All)
        {
            _index = 0;
            return Current;
        }

        return null;
    }

    public Track? MovePrevious(RepeatMode repeat)
    {
        if (_items.Count == 0)
        {
            return null;
        }

        if (_index > 0)
        {
            _index--;
            return Current;
        }

        if (repeat == RepeatMode.All)
        {
            _index = _items.Count - 1;
            return Current;
        }

        return null;
    }

    /// <summary>Removes paths while keeping the current item selected when it survives.</summary>
    public void RemovePaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || _items.Count == 0)
        {
            return;
        }

        var removed = new HashSet<string>(paths, PathComparison.Comparer);
        var current = Current;
        var currentRemoved = current is not null && removed.Contains(current.FilePath);

        _items.RemoveAll(item => removed.Contains(item.FilePath));

        if (currentRemoved)
        {
            _index = -1;
            return;
        }

        if (current is null)
        {
            _index = _items.Count == 0 ? -1 : Math.Clamp(_index, 0, _items.Count - 1);
            return;
        }

        var newIndex = _items.FindIndex(item => PathComparison.Equals(item.FilePath, current.FilePath));
        _index = newIndex >= 0 ? newIndex : _items.Count == 0 ? -1 : Math.Clamp(_index, 0, _items.Count - 1);
    }

    /// <summary>Replaces an item after a tag write or rename.</summary>
    public void UpdateTrack(Track track, string? previousPath = null)
    {
        var matchPath = previousPath ?? track.FilePath;
        var index = _items.FindIndex(item => item.Id == track.Id);
        if (index < 0)
        {
            index = _items.FindIndex(item => PathComparison.Equals(item.FilePath, matchPath));
        }

        if (index >= 0)
        {
            _items[index] = track;
        }
    }
}
