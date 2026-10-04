using NetCraft.Util;

namespace NetCraft.Resources;

//LinkFSPath 链接文件系统里的路径 对应原版 net.minecraft.server.packs.linkfs.LinkFSPath
//原版实现 java.nio.file.Path 这里保留路径解析与内容查询行为 内部用 / 分隔的字符串表示
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

    //IsAbsolute 是否绝对路径 只有相对路径才是 false
    public bool IsAbsolute => !Contents.IsRelative;

    //CreateRelativePath 造一个不挂在文件树上的相对路径
    private LinkFSPath CreateRelativePath(LinkFSPath? parent, string name) => new(FileSystem, name, parent, PathContents.Relative);

    //GetRoot 绝对路径返回文件系统根 相对路径没有根
    public LinkFSPath? GetRoot() => IsAbsolute ? FileSystem.RootPath : null;

    //GetFileName 末段名字 包成新的相对路径
    public LinkFSPath GetFileName() => CreateRelativePath(null, _name);

    public LinkFSPath? GetParent() => _parent;

    public int GetNameCount() => PathToRoot().Count;

    //GetName 取第 index 段名字
    public LinkFSPath GetName(int index)
    {
        var names = PathToRoot();
        if (index < 0 || index >= names.Count) throw new ArgumentOutOfRangeException(nameof(index), $"Invalid index: {index}");
        return CreateRelativePath(null, names[index]);
    }

    //Subpath 截取 [beginIndex,endIndex) 段
    public LinkFSPath Subpath(int beginIndex, int endIndex)
    {
        var names = PathToRoot();
        if (beginIndex < 0 || endIndex > names.Count || beginIndex >= endIndex) throw new ArgumentException("Invalid subpath range");
        LinkFSPath? current = null;
        for (var i = beginIndex; i < endIndex; i++) current = CreateRelativePath(current, names[i]);
        return current!;
    }

    //StartsWith 判断前缀 要求同文件系统且绝对属性一致
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

    //EndsWith 判断后缀
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

    //Resolve 拼接路径 对方绝对时直接采用
    public LinkFSPath Resolve(LinkFSPath other) => other.IsAbsolute ? other : Resolve(other.PathToRoot());

    private LinkFSPath Resolve(List<string> names)
    {
        var current = this;
        foreach (var name in names) current = current.ResolveName(name);
        return current;
    }

    //ResolveName 挂下一段 命中文件树就复用节点
    public LinkFSPath ResolveName(string name)
    {
        if (Contents.IsRelativeOrMissing) return new LinkFSPath(FileSystem, name, this, Contents);
        if (Contents is PathContents.DirectoryContents directory)
        {
            return directory.Children.TryGetValue(name, out var child) ? child : new LinkFSPath(FileSystem, name, this, PathContents.Missing);
        }
        return new LinkFSPath(FileSystem, name, this, PathContents.Missing);
    }

    //Relativize 求相对路径 要求同一绝对属性且本路径是对方前缀
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

    //Exists 是否有真实内容
    public bool Exists() => HasRealContents();

    //GetTargetPath 文件指向的真实路径 目录或不存在返回空
    public string? GetTargetPath() => Contents is PathContents.FileContents file ? file.Target : null;

    //GetDirectoryContents 目录内容 非目录返回空
    public PathContents.DirectoryContents? GetDirectoryContents() => Contents as PathContents.DirectoryContents;

    //GetBasicAttributes 取占位属性 不存在的路径抛异常
    public DummyFileAttributes GetBasicAttributes()
    {
        if (Contents is PathContents.DirectoryContents) return DummyFileAttributes.Directory;
        if (Contents is PathContents.FileContents) return DummyFileAttributes.File;
        throw new FileNotFoundException(PathToString());
    }

    private bool HasRealContents() => !Contents.IsRelativeOrMissing;

    //PathToRoot 从自身往上收集各段名字 结果缓存
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

    //PathToString 绝对路径带前缀斜杠 其余直接拼接
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
