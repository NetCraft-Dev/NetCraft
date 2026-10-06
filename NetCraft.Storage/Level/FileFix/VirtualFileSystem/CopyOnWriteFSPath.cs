namespace NetCraft.Storage;

//CopyOnWriteFSPath, a path in the copy-on-write filesystem, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFSPath
//Vanilla implements java.nio.file.Path on top of a backing filesystem; here it is represented internally as a /-separated string
public sealed class CopyOnWriteFSPath
{
    private readonly string _path;
    private readonly bool _isAbsolute;

    private CopyOnWriteFSPath(string path, CopyOnWriteFileSystem fileSystem, bool isAbsolute)
    {
        _path = path;
        FileSystem = fileSystem;
        _isAbsolute = isAbsolute;
    }

    public CopyOnWriteFileSystem FileSystem { get; }

    //Of builds a path from multiple segments; a leading slash means absolute
    public static CopyOnWriteFSPath Of(CopyOnWriteFileSystem fileSystem, string first, params string[] more)
    {
        var isAbsolute = false;
        while (first.StartsWith('/'))
        {
            isAbsolute = true;
            first = first[1..];
        }
        var segments = new List<string> { first };
        segments.AddRange(more);
        return new CopyOnWriteFSPath(Combine(segments), fileSystem, isAbsolute);
    }

    public bool IsAbsolute => _isAbsolute;

    public CopyOnWriteFSPath? GetRoot() => _isAbsolute ? FileSystem.RootPath : null;

    //IsRoot, the root path has no segment name
    public bool IsRoot() => GetNameCount() == 0;

    public CopyOnWriteFSPath? GetFileName()
    {
        if (IsRoot()) return null;
        var segments = Segments();
        return new CopyOnWriteFSPath(segments[^1], FileSystem, false);
    }

    public CopyOnWriteFSPath? GetParent()
    {
        if (IsRoot()) return null;
        var segments = Segments();
        if (segments.Count <= 1) return _isAbsolute ? FileSystem.RootPath : null;
        return new CopyOnWriteFSPath(Combine(segments.Take(segments.Count - 1)), FileSystem, _isAbsolute);
    }

    public int GetNameCount() => _path.Length == 0 && _isAbsolute ? 0 : Segments().Count;

    public CopyOnWriteFSPath GetName(int index) => new(Segments()[index], FileSystem, false);

    public CopyOnWriteFSPath Subpath(int beginIndex, int endIndex)
    {
        var segments = Segments();
        if (beginIndex < 0 || endIndex > segments.Count || beginIndex >= endIndex) throw new ArgumentException("Invalid subpath range");
        return new CopyOnWriteFSPath(Combine(segments.Skip(beginIndex).Take(endIndex - beginIndex)), FileSystem, false);
    }

    public bool StartsWith(CopyOnWriteFSPath other)
    {
        if (_isAbsolute != other._isAbsolute) return false;
        var thisSegments = Segments();
        var otherSegments = other.Segments();
        if (otherSegments.Count > thisSegments.Count) return false;
        for (var i = 0; i < otherSegments.Count; i++)
        {
            if (thisSegments[i] != otherSegments[i]) return false;
        }
        return true;
    }

    public bool EndsWith(CopyOnWriteFSPath other)
    {
        if (other._isAbsolute) return _isAbsolute && Equals(other);
        var thisSegments = Segments();
        var otherSegments = other.Segments();
        if (otherSegments.Count > thisSegments.Count) return false;
        var offset = thisSegments.Count - otherSegments.Count;
        for (var i = 0; i < otherSegments.Count; i++)
        {
            if (thisSegments[offset + i] != otherSegments[i]) return false;
        }
        return true;
    }

    //Normalize removes . and ..; on an absolute path an out-of-range .. is folded into the root
    public CopyOnWriteFSPath Normalize()
    {
        var normalized = NormalizeSegments(_path, _isAbsolute);
        if (_isAbsolute && normalized.StartsWith('.')) return FileSystem.RootPath;
        return new CopyOnWriteFSPath(normalized, FileSystem, _isAbsolute);
    }

    public CopyOnWriteFSPath Resolve(CopyOnWriteFSPath other)
    {
        if (other._isAbsolute) return other;
        return new CopyOnWriteFSPath(JoinPaths(_path, other._path), FileSystem, _isAbsolute);
    }

    public CopyOnWriteFSPath Resolve(string other) => Resolve(FileSystem.GetPath(other));

    public CopyOnWriteFSPath Resolve(string first, params string[] more)
    {
        var result = Resolve(first);
        foreach (var segment in more) result = result.Resolve(segment);
        return result;
    }

    public CopyOnWriteFSPath Relativize(CopyOnWriteFSPath other)
    {
        if (_isAbsolute != other._isAbsolute) throw new ArgumentException("'other' is different type of Path");
        var thisSegments = Segments();
        var otherSegments = other.Segments();
        var common = 0;
        while (common < thisSegments.Count && common < otherSegments.Count && thisSegments[common] == otherSegments[common]) common++;
        var parts = new List<string>();
        for (var i = common; i < thisSegments.Count; i++) parts.Add("..");
        for (var i = common; i < otherSegments.Count; i++) parts.Add(otherSegments[i]);
        return new CopyOnWriteFSPath(Combine(parts), FileSystem, false);
    }

    public CopyOnWriteFSPath ToAbsolutePath() => _isAbsolute ? this : new CopyOnWriteFSPath(_path, FileSystem, true);

    public CopyOnWriteFSPath ToRealPath() => FileSystem.Provider.GetRealPath(this);

    public int CompareTo(CopyOnWriteFSPath other) => string.CompareOrdinal(ToString(), other.ToString());

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        var other = (CopyOnWriteFSPath)obj;
        return _isAbsolute == other._isAbsolute && _path == other._path && ReferenceEquals(FileSystem, other.FileSystem);
    }

    public override int GetHashCode() => HashCode.Combine(_path, FileSystem, _isAbsolute);

    public override string ToString() => _isAbsolute ? "/" + _path : _path;

    //Segments splits the path into segments; an empty path has none
    private List<string> Segments() => _path.Length == 0 ? new List<string>() : _path.Split('/').ToList();

    //Combine joins segments with slashes and drops empty ones
    private static string Combine(IEnumerable<string> segments) => string.Join('/', segments.Where(segment => segment.Length > 0));

    private static string JoinPaths(string left, string right)
    {
        if (left.Length == 0) return right;
        if (right.Length == 0) return left;
        return left + "/" + right;
    }

    //NormalizeSegments, segment-wise normalization; leading extra .. on a relative path is kept
    private static string NormalizeSegments(string path, bool isAbsolute)
    {
        var stack = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..") stack.RemoveAt(stack.Count - 1);
                else if (!isAbsolute) stack.Add("..");
                continue;
            }
            stack.Add(segment);
        }
        return string.Join('/', stack);
    }
}
