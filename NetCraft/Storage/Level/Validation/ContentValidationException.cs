namespace NetCraft.Storage;

//Directory validation failed, maps to vanilla ContentValidationException
//Carries the directory and all out-of-bounds links
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

    //Join the out-of-bounds links into one line, link->target
    private static string Compose(string directory, IReadOnlyList<ForbiddenSymlinkInfo> entries)
        => $"Failed to validate '{directory}'. Found forbidden symlinks: "
        + string.Join(", ", entries.Select(entry => $"{entry.Link}->{entry.Target}"));
}
