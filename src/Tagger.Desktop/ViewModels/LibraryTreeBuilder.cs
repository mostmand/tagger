using Tagger.Application.Renaming;
using Tagger.Domain.Searching;

namespace Tagger.Desktop.ViewModels;

/// <summary>Groups tracks into a folder hierarchy rooted at the loaded library folders.</summary>
public sealed class LibraryTreeBuilder
{
    public IReadOnlyList<FolderNodeViewModel> Build(
        IReadOnlyList<TrackRowViewModel> tracks,
        IReadOnlyList<string> roots,
        TrackSortField sortField,
        bool sortDescending,
        IReadOnlyDictionary<string, bool>? expansionStates = null)
    {
        var rootNodes = new Dictionary<string, MutableFolder>(PathComparison.Comparer);

        foreach (var track in tracks)
        {
            var root = FindRoot(track.Folder, roots) ?? track.Folder;
            if (!rootNodes.TryGetValue(root, out var node))
            {
                node = new MutableFolder(root, FolderName(root));
                rootNodes[root] = node;
            }

            var relative = Path.GetRelativePath(root, track.Folder);
            if (relative != "." && !Path.IsPathRooted(relative))
            {
                foreach (var segment in relative.Split(
                             [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    node = node.GetOrAddChild(segment);
                }
            }

            node.Tracks.Add(track);
        }

        return OrderRoots(rootNodes, roots)
            .Select(node => ToViewModel(node, sortField, sortDescending, expansionStates))
            .ToList();
    }

    private static FolderNodeViewModel ToViewModel(
        MutableFolder folder,
        TrackSortField sortField,
        bool sortDescending,
        IReadOnlyDictionary<string, bool>? expansionStates)
    {
        var childFolders = folder.Folders.Values
            .OrderBy(x => x.Name, NaturalStringComparer.Instance)
            .Select(x => ToViewModel(x, sortField, sortDescending, expansionStates))
            .ToList();

        var byId = folder.Tracks.ToDictionary(x => x.Id);
        var sortedTracks = TrackSorter
            .Sort(folder.Tracks.Select(x => x.Track), sortField, sortDescending)
            .Select(x => byId[x.Id])
            .ToList();

        var children = childFolders
            .Cast<LibraryNodeViewModel>()
            .Concat(sortedTracks)
            .ToList();

        var node = new FolderNodeViewModel(folder.Path, folder.Name, children, sortedTracks);
        if (expansionStates is not null &&
            expansionStates.TryGetValue(folder.Path, out var expanded))
        {
            node.IsExpanded = expanded;
        }

        return node;
    }

    private static IEnumerable<MutableFolder> OrderRoots(
        IReadOnlyDictionary<string, MutableFolder> nodes,
        IReadOnlyList<string> roots)
    {
        var emitted = new HashSet<string>(PathComparison.Comparer);
        foreach (var root in roots)
        {
            if (nodes.TryGetValue(root, out var node) && emitted.Add(root))
            {
                yield return node;
            }
        }

        foreach (var node in nodes.Values.OrderBy(x => x.Path, NaturalStringComparer.Instance))
        {
            if (emitted.Add(node.Path))
            {
                yield return node;
            }
        }
    }

    private static string? FindRoot(string directory, IReadOnlyList<string> roots)
    {
        string? best = null;
        foreach (var root in roots)
        {
            if (!PathComparison.Comparer.Equals(directory, root) && !IsUnder(directory, root))
            {
                continue;
            }

            if (best is null || root.Length > best.Length)
            {
                best = root;
            }
        }

        return best;
    }

    private static bool IsUnder(string directory, string root) =>
        directory.StartsWith(
            Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            PathComparison.Comparison);

    private static string FolderName(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private sealed class MutableFolder(string path, string name)
    {
        public string Path { get; } = path;

        public string Name { get; } = name;

        public Dictionary<string, MutableFolder> Folders { get; } = new(PathComparison.Comparer);

        public List<TrackRowViewModel> Tracks { get; } = [];

        public MutableFolder GetOrAddChild(string segment)
        {
            if (Folders.TryGetValue(segment, out var existing))
            {
                return existing;
            }

            var child = new MutableFolder(System.IO.Path.Combine(Path, segment), segment);
            Folders[segment] = child;
            return child;
        }
    }
}
