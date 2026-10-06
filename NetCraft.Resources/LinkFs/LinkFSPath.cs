using NetCraft.Util;

namespace NetCraft.Resources;

//LinkFSPath, a path in the link file system, maps to vanilla net.minecraft.server.packs.linkfs.LinkFSPath
//Vanilla implements java.nio.file.Path, here the path resolution and content query behavior is kept, represented internally as a /-separated string
public sealed class LinkFSPath
{
    private static readonly List<string> NoNames = new();
    private readonly string _name;
    private readonly LinkFSPath? _parent;
    private List<string>? _pathToRoot;
    private string? _pathString;

    public LinkFSPath(LinkFileSystem fileSystem, string name, LinkFSPath? parent, PathContents contents)
    {
        FileSystem = fileSystem;
        _name = name;
        _parent = parent;
        Contents = contents;
    }

    public LinkFileSystem FileSystem { get; }

    public PathContents Contents { get; }

    //IsAbsolute whether this is an absolute path, only a relative path is false
    public bool IsAbsolute => !Contents.IsRelative;

    //CreateRelativePath builds a relative path not attached to the file tree
    private LinkFSPath CreateRelativePath(LinkFSPath? parent, string name) => new(FileSystem, name, parent, PathContents.Relative);

    //GetRoot returns the file system root for an absolute path, a relative path has no root
    public LinkFSPath? GetRoot() => IsAbsolute ? FileSystem.RootPath : null;

    //GetFileName, the last segment name wrapped as a new relative path
    public LinkFSPath GetFileName() => CreateRelativePath(null, _name);

    public LinkFSPath? GetParent() => _parent;

    public int GetNameCount() => PathToRoot().Count;

    //GetName gets the segment name at index
    public LinkFSPath GetName(int index)
    {
        var names = PathToRoot();
        if (index < 0 || index >= names.Count) throw new ArgumentOutOfRangeException(nameof(index), $"Invalid index: {index}");
        return CreateRelativePath(null, names[index]);
    }

    //Subpath takes the segments [beginIndex,endIndex)
    public LinkFSPath Subpath(int beginIndex, int endIndex)
    {
        var names = PathToRoot();
        if (beginIndex < 0 || endIndex > names.Count || beginIndex >= endIndex) throw new ArgumentException("Invalid subpath range");
        LinkFSPath? current = null;
        for (var i = beginIndex; i < endIndex; i++) current = CreateRelativePath(current, names[i]);
        return current!;
    }

    //StartsWith checks the prefix, requires the same file system and matching absolute flag
    public bool StartsWith(LinkFSPath other)
    {
        if (other.IsAbsolute != IsAbsolute) return false;
        if (!ReferenceEquals(other.FileSystem, FileSystem)) return false;
        var thisNames = PathToRoot();
        var otherNames = other.PathToRoot();
        if (otherNames.Count > thisNames.Count) return false;
        for (var i = 0; i < otherNames.Count; i++)
        {
            if (otherNames[i] != thisNames[i]) return false;
        }
        return true;
    }

    //EndsWith checks the suffix
    public bool EndsWith(LinkFSPath other)
    {
        if (other.IsAbsolute && !IsAbsolute) return false;
        if (!ReferenceEquals(other.FileSystem, FileSystem)) return false;
        var thisNames = PathToRoot();
        var otherNames = other.PathToRoot();
        var delta = thisNames.Count - otherNames.Count;
        if (delta < 0) return false;
        for (var i = otherNames.Count - 1; i >= 0; i--)
        {
            if (otherNames[i] != thisNames[delta + i]) return false;
        }
        return true;
    }

    public LinkFSPath Normalize() => this;

    //Resolve joins paths, an absolute other is taken as is
    public LinkFSPath Resolve(LinkFSPath other) => other.IsAbsolute ? other : Resolve(other.PathToRoot());

    private LinkFSPath Resolve(List<string> names)
    {
        var current = this;
        foreach (var name in names) current = current.ResolveName(name);
        return current;
    }

    //ResolveName attaches the next segment, reuses the node when it hits the file tree
    public LinkFSPath ResolveName(string name)
    {
        if (Contents.IsRelativeOrMissing) return new LinkFSPath(FileSystem, name, this, Contents);
        if (Contents is PathContents.DirectoryContents directory)
        {
            return directory.Children.TryGetValue(name, out var child) ? child : new LinkFSPath(FileSystem, name, this, PathContents.Missing);
        }
        return new LinkFSPath(FileSystem, name, this, PathContents.Missing);
    }

    //Relativize computes the relative path, requires the same absolute flag and that this path prefixes the other
    public LinkFSPath Relativize(LinkFSPath other)
    {
        if (IsAbsolute != other.IsAbsolute) throw new ArgumentException("absolute mismatch");
        var thisNames = PathToRoot();
        var otherNames = other.PathToRoot();
        if (thisNames.Count >= otherNames.Count) throw new ArgumentException("Paths are not relative to each other");
        for (var i = 0; i < thisNames.Count; i++)
        {
            if (thisNames[i] != otherNames[i]) throw new ArgumentException("Paths are not relative to each other");
        }
        return other.Subpath(thisNames.Count, otherNames.Count);
    }

    public LinkFSPath ToAbsolutePath() => IsAbsolute ? this : FileSystem.RootPath.Resolve(this);

    public int CompareTo(LinkFSPath other) => string.CompareOrdinal(PathToString(), other.PathToString());

    public override bool Equals(object? other)
    {
        if (ReferenceEquals(other, this)) return true;
        if (other is not LinkFSPath that) return false;
        if (!ReferenceEquals(FileSystem, that.FileSystem)) return false;
        var hasRealContents = HasRealContents();
        if (hasRealContents != that.HasRealContents()) return false;
        if (hasRealContents) return ReferenceEquals(Contents, that.Contents);
        return Equals(_parent, that._parent) && _name == that._name;
    }

    public override int GetHashCode() => HasRealContents() ? Contents.GetHashCode() : _name.GetHashCode();

    public override string ToString() => PathToString();

    //Exists whether there is real content
    public bool Exists() => HasRealContents();

    //GetTargetPath the real path a file points to, null for a directory or a missing path
    public string? GetTargetPath() => Contents is PathContents.FileContents file ? file.Target : null;

    //GetDirectoryContents the directory content, null when not a directory
    public PathContents.DirectoryContents? GetDirectoryContents() => Contents as PathContents.DirectoryContents;

    //GetBasicAttributes gets the placeholder attributes, a missing path throws
    public DummyFileAttributes GetBasicAttributes()
    {
        if (Contents is PathContents.DirectoryContents) return DummyFileAttributes.Directory;
        if (Contents is PathContents.FileContents) return DummyFileAttributes.File;
        throw new FileNotFoundException(PathToString());
    }

    private bool HasRealContents() => !Contents.IsRelativeOrMissing;

    //PathToRoot collects segment names from here upward, result cached
    private List<string> PathToRoot()
    {
        if (_name.Length == 0) return NoNames;
        if (_pathToRoot is null)
        {
            var names = new List<string>();
            if (_parent is not null) names.AddRange(_parent.PathToRoot());
            names.Add(_name);
            _pathToRoot = names;
        }
        return _pathToRoot;
    }

    //PathToString an absolute path gets a leading slash, otherwise segments are concatenated directly
    private string PathToString()
    {
        if (_pathString is null)
        {
            var text = string.Join("/", PathToRoot());
            _pathString = IsAbsolute ? "/" + text : text;
        }
        return _pathString;
    }
}
