using NetCraft.Util;

namespace NetCraft.Resources;

//LinkFSProvider 链接文件系统的访问入口 对应原版 net.minecraft.server.packs.linkfs.LinkFSProvider
//原版继承 FileSystemProvider 这里只保留读流 列目录 读属性 查访问这些会被用到的行为
public sealed class LinkFSProvider
{
    public const string Scheme = "x-mc-link";

    public string GetScheme() => Scheme;

    //NewReadChannel 打开真实文件读流 不存在抛异常
    public Stream NewReadChannel(LinkFSPath path)
    {
        var target = path.ToAbsolutePath().GetTargetPath();
        if (target is null) throw new FileNotFoundException(path.ToString());
        return File.OpenRead(target);
    }

    //NewDirectoryStream 列出目录下的子路径
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

    //ReadAttributes 取占位属性
    public DummyFileAttributes ReadAttributes(LinkFSPath path) => path.ToAbsolutePath().GetBasicAttributes();

    //CheckAccess 只读文件系统 读要求路径存在 写一律拒绝
    public void CheckAccess(LinkFSPath path, bool read)
    {
        if (!read) throw new UnauthorizedAccessException("LinkFS is read-only");
        if (!path.ToAbsolutePath().Exists()) throw new FileNotFoundException(path.ToString());
    }

    public bool IsSameFile(LinkFSPath path, LinkFSPath other) => ReferenceEquals(path.FileSystem, other.FileSystem) && path.Equals(other);

    public LinkFSFileStore GetFileStore(LinkFSPath path) => path.FileSystem.Store;
}
