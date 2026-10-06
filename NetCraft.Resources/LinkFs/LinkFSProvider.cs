using NetCraft.Util;

namespace NetCraft.Resources;

//LinkFSProvider, the access entry point for the link file system, maps to vanilla net.minecraft.server.packs.linkfs.LinkFSProvider
//Vanilla extends FileSystemProvider, here only the used behaviors are kept: read stream, list directory, read attributes, check access
public sealed class LinkFSProvider
{
    public const string Scheme = "x-mc-link";

    public string GetScheme() => Scheme;

    //NewReadChannel opens a read stream on the real file, throws when it does not exist
    public Stream NewReadChannel(LinkFSPath path)
    {
        var target = path.ToAbsolutePath().GetTargetPath();
        if (target is null) throw new FileNotFoundException(path.ToString());
        return File.OpenRead(target);
    }

    //NewDirectoryStream lists the child paths of a directory
    public IReadOnlyList<LinkFSPath> NewDirectoryStream(LinkFSPath dir, Func<LinkFSPath, bool>? filter = null)
    {
        var contents = dir.ToAbsolutePath().GetDirectoryContents();
        if (contents is null) throw new IOException($"{dir} is not a directory");
        var result = new List<LinkFSPath>();
        foreach (var child in contents.Children.Values)
        {
            if (filter is null || filter(child)) result.Add(child);
        }
        return result;
    }

    //ReadAttributes gets the placeholder attributes
    public DummyFileAttributes ReadAttributes(LinkFSPath path) => path.ToAbsolutePath().GetBasicAttributes();

    //CheckAccess, a readonly file system, reads require the path to exist and writes are always rejected
    public void CheckAccess(LinkFSPath path, bool read)
    {
        if (!read) throw new UnauthorizedAccessException("LinkFS is read-only");
        if (!path.ToAbsolutePath().Exists()) throw new FileNotFoundException(path.ToString());
    }

    public bool IsSameFile(LinkFSPath path, LinkFSPath other) => ReferenceEquals(path.FileSystem, other.FileSystem) && path.Equals(other);

    public LinkFSFileStore GetFileStore(LinkFSPath path) => path.FileSystem.Store;
}
