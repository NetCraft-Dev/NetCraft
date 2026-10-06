namespace NetCraft.Storage;

//Directory validation, maps to vanilla DirectoryValidator
//Picks out symlinks in the directory that point outside the allow list
public sealed class DirectoryValidator
{
    private readonly PathAllowList _symlinkTargetAllowList;

    public DirectoryValidator(PathAllowList symlinkTargetAllowList) => _symlinkTargetAllowList = symlinkTargetAllowList;

    //Record an entry when the target is not in the allow list
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

    //Validate the whole directory
    //When the top itself is a symlink, follow the target down if allowTopSymlink is true, otherwise check only this level
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

    //Walk recursively, checking both directories and files
    //Do not descend through links; a link cycle would never terminate
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

    //Whether the path exists; a broken link also counts as present
    private static bool Exists(string path)
        => File.Exists(path) || Directory.Exists(path) || ResolveLinkTarget(path) is not null;

    //Get the link target; null when not a link
    private static string? ResolveLinkTarget(string path)
    {
        var file = new FileInfo(path);
        return file.LinkTarget ?? new DirectoryInfo(path).LinkTarget;
    }
}
