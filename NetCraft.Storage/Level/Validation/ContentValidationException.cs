namespace NetCraft.Storage;

//目录校验没过对应原版ContentValidationException
//带上目录与所有越界的链接
public sealed class ContentValidationException : Exception
{
    public ContentValidationException(string directory, IReadOnlyList<ForbiddenSymlinkInfo> entries)
        : base(Compose(directory, entries))
    {
        Directory = directory;
        Entries = entries;
    }

    public string Directory { get; }

    public IReadOnlyList<ForbiddenSymlinkInfo> Entries { get; }

    //把越界的链接拼成一行 链接->目标
    private static string Compose(string directory, IReadOnlyList<ForbiddenSymlinkInfo> entries)
        => $"Failed to validate '{directory}'. Found forbidden symlinks: "
        + string.Join(", ", entries.Select(entry => $"{entry.Link}->{entry.Target}"));
}
