namespace NetCraft.Resources;

//LinkFileSystem 链接文件系统 对应原版 net.minecraft.server.packs.linkfs.LinkFileSystem
//把若干真实目录挂成一棵只读树 用 Builder 填内容再 Build 出实例
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

    //BuildPath 递归把目录条目铺成路径节点
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

    //GetPath 拼出路径 以斜杠开头当绝对 否则当相对
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

    //Builder 填内容 目录按段自动补 文件挂在末段名下
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

    //DirectoryEntry 构建期的目录条目 只存子目录与文件名到真实路径的映射
    private sealed class DirectoryEntry
    {
        public Dictionary<string, DirectoryEntry> Children { get; } = new();

        public Dictionary<string, string> Files { get; } = new();
    }
}
