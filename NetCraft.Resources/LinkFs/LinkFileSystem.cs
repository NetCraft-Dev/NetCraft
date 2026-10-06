namespace NetCraft.Resources;

//LinkFileSystem, the link file system, maps to vanilla net.minecraft.server.packs.linkfs.LinkFileSystem
//Mounts several real directories into one readonly tree, use Builder to fill content then Build the instance
public sealed class LinkFileSystem
{
    public const string PathSeparator = "/";

    private LinkFileSystem(string name, DirectoryEntry rootEntry)
    {
        Store = new LinkFSFileStore(name);
        Provider = new LinkFSProvider();
        RootPath = BuildPath(rootEntry, this, "", null);
    }

    public LinkFSProvider Provider { get; }

    public LinkFSFileStore Store { get; }

    public LinkFSPath RootPath { get; }

    public bool IsReadOnly => true;

    public IReadOnlyList<LinkFSPath> RootDirectories => new[] { RootPath };

    public IReadOnlyList<LinkFSFileStore> FileStores => new[] { Store };

    public IReadOnlySet<string> SupportedFileAttributeViews { get; } = new HashSet<string> { "basic" };

    //BuildPath recursively lays out directory entries into path nodes
    private static LinkFSPath BuildPath(DirectoryEntry entry, LinkFileSystem fileSystem, string selfName, LinkFSPath? parent)
    {
        var children = new Dictionary<string, LinkFSPath>();
        var result = new LinkFSPath(fileSystem, selfName, parent, new PathContents.DirectoryContents(children));
        foreach (var pair in entry.Files)
        {
            children[pair.Key] = new LinkFSPath(fileSystem, pair.Key, result, new PathContents.FileContents(pair.Value));
        }
        foreach (var pair in entry.Children)
        {
            children[pair.Key] = BuildPath(pair.Value, fileSystem, pair.Key, result);
        }
        return result;
    }

    //GetPath builds a path, a leading slash means absolute, otherwise relative
    public LinkFSPath GetPath(string first, params string[] more)
    {
        var joined = string.Join(PathSeparator, new[] { first }.Concat(more));
        if (joined == PathSeparator) return RootPath;
        if (joined.StartsWith(PathSeparator, StringComparison.Ordinal))
        {
            var result = RootPath;
            foreach (var segment in joined[1..].Split('/'))
            {
                if (segment.Length == 0) throw new ArgumentException("Empty paths not allowed");
                result = result.ResolveName(segment);
            }
            return result;
        }
        LinkFSPath? relative = null;
        foreach (var segment in joined.Split('/'))
        {
            if (segment.Length == 0) throw new ArgumentException("Empty paths not allowed");
            relative = new LinkFSPath(this, segment, relative, PathContents.Relative);
        }
        return relative ?? throw new ArgumentException("Empty paths not allowed");
    }

    public static Builder CreateBuilder() => new();

    //Builder fills content, directories are added per segment, the file hangs under the last segment
    public sealed class Builder
    {
        private readonly DirectoryEntry _root = new();

        public Builder Put(IReadOnlyList<string> path, string name, string target)
        {
            var current = _root;
            foreach (var segment in path)
            {
                if (!current.Children.TryGetValue(segment, out var child))
                {
                    child = new DirectoryEntry();
                    current.Children[segment] = child;
                }
                current = child;
            }
            current.Files[name] = target;
            return this;
        }

        public Builder Put(IReadOnlyList<string> path, string target)
        {
            if (path.Count == 0) throw new ArgumentException("Path can't be empty", nameof(path));
            var last = path.Count - 1;
            return Put(path.Take(last).ToList(), path[last], target);
        }

        public LinkFileSystem Build(string name) => new(name, _root);
    }

    //DirectoryEntry, a build-time directory entry, only stores the mapping of child dirs and file names to real paths
    private sealed class DirectoryEntry
    {
        public Dictionary<string, DirectoryEntry> Children { get; } = new();

        public Dictionary<string, string> Files { get; } = new();
    }
}
