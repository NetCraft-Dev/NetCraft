namespace NetCraft.Storage;

//目录校验对应原版DirectoryValidator
//把目录里指向允许列表之外的符号链接挑出来
public sealed class DirectoryValidator
{
    private readonly PathAllowList _symlinkTargetAllowList;

    public DirectoryValidator(PathAllowList symlinkTargetAllowList) => _symlinkTargetAllowList = symlinkTargetAllowList;

    //目标不在允许列表里就记一条
    public void ValidateSymlink(string path, List<ForbiddenSymlinkInfo> issues)
    {
        var target = ResolveLinkTarget(path) ?? throw new IOException($"Not a symbolic link: {path}");
        if (!_symlinkTargetAllowList.Matches(target))
            issues.Add(new ForbiddenSymlinkInfo(path, target));
    }

    public List<ForbiddenSymlinkInfo> ValidateSymlink(string path)
    {
        var issues = new List<ForbiddenSymlinkInfo>();
        ValidateSymlink(path, issues);
        return issues;
    }

    //校验整个目录
    //顶层自己就是链接时，allowTopSymlink 为真就顺着目标往下走，否则只查这一层
    public List<ForbiddenSymlinkInfo> ValidateDirectory(string directory, bool allowTopSymlink)
    {
        var issues = new List<ForbiddenSymlinkInfo>();
        if (!Exists(directory))
            return issues;
        if (File.Exists(directory))
            throw new IOException($"Path {directory} is not a directory");
        var target = ResolveLinkTarget(directory);
        if (target is not null)
        {
            if (!allowTopSymlink)
            {
                ValidateSymlink(directory, issues);
                return issues;
            }
            directory = target;
        }
        ValidateKnownDirectory(directory, issues);
        return issues;
    }

    //递归走一遍，目录与文件都看一眼
    //不跟着链接往下钻，链接成环会转不出来
    public void ValidateKnownDirectory(string directory, List<ForbiddenSymlinkInfo> issues)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                if (ResolveLinkTarget(entry) is not null)
                {
                    ValidateSymlink(entry, issues);
                    continue;
                }
                if (Directory.Exists(entry))
                    pending.Push(entry);
            }
        }
    }

    //路径在不在，断掉的链接也算在
    private static bool Exists(string path)
        => File.Exists(path) || Directory.Exists(path) || ResolveLinkTarget(path) is not null;

    //取链接目标，不是链接给null
    private static string? ResolveLinkTarget(string path)
    {
        var file = new FileInfo(path);
        return file.LinkTarget ?? new DirectoryInfo(path).LinkTarget;
    }
}
