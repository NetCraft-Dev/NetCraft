using System.Runtime.InteropServices;
using NetCraft.Logging;

namespace NetCraft.Storage;

//CopyOnWriteFileSystem 写时复制文件系统 对应原版 net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFileSystem
//构建时把基础目录铺成一棵节点树 改动前先把文件复制进临时目录 最后收集搬运操作统一落盘
public sealed class CopyOnWriteFileSystem
{
    private readonly string _baseDirectory;
    private readonly string _tmpDirectory;
    private readonly Func<string, bool> _skippedPaths;
    private readonly CopyOnWriteFSPath _rootPath;
    private int _tmpFileIndex;
    private DirectoryNode _fileTree;

    private CopyOnWriteFileSystem(string name, string baseDirectory, string tmpDirectory, Func<string, bool> skippedPaths)
    {
        _baseDirectory = baseDirectory;
        _tmpDirectory = tmpDirectory;
        _skippedPaths = skippedPaths;
        Provider = new CopyOnWriteFSProvider(this);
        Store = new CopyOnWriteFileStore(name, this);
        _rootPath = GetPath("/");
        _fileTree = BuildFileTreeFrom(baseDirectory);
    }

    public CopyOnWriteFSProvider Provider { get; }

    public CopyOnWriteFileStore Store { get; }

    public CopyOnWriteFSPath RootPath => _rootPath;

    public DirectoryNode FileTree => _fileTree;

    public string BaseDirectory => _baseDirectory;

    public string TmpDirectory => _tmpDirectory;

    public bool IsReadOnly => false;

    public IReadOnlyList<CopyOnWriteFSPath> RootDirectories => new[] { _rootPath };

    public IReadOnlyList<CopyOnWriteFileStore> FileStores => new[] { Store };

    public IReadOnlySet<string> SupportedFileAttributeViews { get; } = new HashSet<string> { "basic" };

    //Create 建文件系统 临时目录已存在直接报错 建完再真正落目录
    public static CopyOnWriteFileSystem Create(string name, string baseDirectory, string tmpDirectory, Func<string, bool> skippedPaths)
    {
        if (Directory.Exists(tmpDirectory) || File.Exists(tmpDirectory))
        {
            throw new CowFSCreationException($"Temporary directory already exists: {tmpDirectory}");
        }
        var fileSystem = new CopyOnWriteFileSystem(name, baseDirectory, tmpDirectory, skippedPaths);
        Directory.CreateDirectory(tmpDirectory);
        return fileSystem;
    }

    public CopyOnWriteFSPath GetPath(string first, params string[] more) => CopyOnWriteFSPath.Of(this, first, more);

    public string GetSeparator() => Path.DirectorySeparatorChar.ToString();

    //Close 清掉临时目录
    public void Close()
    {
        if (Directory.Exists(_tmpDirectory)) Directory.Delete(_tmpDirectory, true);
    }

    //ResetFileTreeToBaseFolderContent 重扫基础目录重建文件树 测试用
    public void ResetFileTreeToBaseFolderContent() => _fileTree = BuildFileTreeFrom(_baseDirectory);

    //CreateTemporaryFilePath 分配一个新的临时文件路径
    public string CreateTemporaryFilePath() => Path.Combine(_tmpDirectory, $"tmp_{Interlocked.Increment(ref _tmpFileIndex)}");

    //BuildFileTreeFrom 递归把基础目录铺成节点树
    private DirectoryNode BuildFileTreeFrom(string baseDirectory)
    {
        var fileTree = new DirectoryNode(_rootPath);
        Walk(baseDirectory, baseDirectory, fileTree);
        return fileTree;
    }

    private void Walk(string directory, string baseDirectory, DirectoryNode fileTree)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            CheckAttributes(entry);
            if (_skippedPaths(entry)) continue;
            var cowPath = ToCowPath(baseDirectory, entry, fileTree);
            var parentPath = cowPath.GetParent() ?? throw new CowFSCreationException($"Path has no parent: {cowPath}");
            var parent = fileTree.DirectoryByPath(parentPath);
            if (Directory.Exists(entry))
            {
                parent.AddChild(new DirectoryNode(cowPath));
                Walk(entry, baseDirectory, fileTree);
            }
            else
            {
                parent.AddChild(new FileNode(cowPath, entry, false));
            }
        }
    }

    //CheckAttributes 符号链接与只读路径都不接受
    private static void CheckAttributes(string realPath)
    {
        var attributes = File.GetAttributes(realPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new CowFSSymlinkException($"Cannot build copy-on-write file system when symlink is present: {realPath}");
        }
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            throw new CowFSCreationException($"Cannot build copy-on-write file system, missing write access for file: {realPath}");
        }
    }

    //ToCowPath 真实路径换算成文件树里的虚拟路径
    private static CopyOnWriteFSPath ToCowPath(string baseDirectory, string realPath, DirectoryNode fileTree)
        => fileTree.Path.Resolve(Path.GetRelativePath(baseDirectory, realPath).Replace('\\', '/'));

    //CollectMoveOperations 遍历文件树收集目录与两类文件搬运
    public Moves CollectMoveOperations(string outPath)
    {
        var result = new Moves(new List<string>(), new List<FileMove>(), new List<FileMove>());
        CollectMoveOperations(outPath, _fileTree, result);
        return result;
    }

    private static void CollectMoveOperations(string outPath, DirectoryNode folder, Moves result)
    {
        foreach (var child in folder.Children)
        {
            var target = Path.Combine(outPath, child.Name ?? string.Empty);
            switch (child)
            {
                case FileNode file:
                    var move = new FileMove(file.StoragePath, target);
                    if (file.IsCopy) result.CopiedFiles.Add(move);
                    else result.PreexistingFiles.Add(move);
                    break;
                case DirectoryNode directory:
                    result.Directories.Add(target);
                    CollectMoveOperations(target, directory, result);
                    break;
            }
        }
    }

    //CreateDirectories 逐个建目录 父目录必须已存在
    public static void CreateDirectories(IReadOnlyList<string> directories)
    {
        foreach (var directory in directories) Directory.CreateDirectory(directory);
    }

    //HardLinkFiles 未改动的文件硬链接到目标位置省空间
    public static void HardLinkFiles(IReadOnlyList<FileMove> moves)
    {
        foreach (var move in moves)
        {
            if (File.Exists(move.To)) continue;
            if (!File.Exists(move.From)) throw new InvalidOperationException($"Not a regular file: {move.From}");
            CreateHardLink(move.To, move.From);
        }
    }

    public static void MoveFiles(IReadOnlyList<FileMove> moves)
    {
        foreach (var move in moves) File.Move(move.From, move.To);
    }

    //MoveFilesWithRetry 搬回去时跳过已就位或已存在的 其余必须搬
    public static void MoveFilesWithRetry(IReadOnlyList<FileMove> moves, bool overwrite = false)
    {
        foreach (var move in moves)
        {
            if (!File.Exists(move.From) && File.Exists(move.To)) continue;
            if (!File.Exists(move.From)) throw new IOException($"Not a regular file: {move.From}");
            File.Move(move.From, move.To, overwrite);
        }
    }

    //TryRevertMoves 把已搬走的文件搬回去 返回失败的项
    public static IReadOnlyList<FileMove> TryRevertMoves(IReadOnlyList<FileMove> moves, bool overwrite = false)
    {
        var failed = new List<FileMove>();
        foreach (var move in moves)
        {
            if (!File.Exists(move.To) && File.Exists(move.From)) continue;
            if (File.Exists(move.To))
            {
                if (SafeMoveFile(move.To, move.From, overwrite))
                {
                    Log.Info($"Reverted move from {move.From} to {move.To}");
                    continue;
                }
                Log.Error($"Failed to revert move from {move.From} to {move.To}");
                failed.Add(move);
                continue;
            }
            Log.Error($"Skipping reverting move from {move.From} to {move.To} as it's not a file");
            failed.Add(move);
        }
        Log.Info(failed.Count == 0 ? "Successfully reverted back to previous world state" : "Completed reverting with errors");
        return failed;
    }

    private static bool SafeMoveFile(string from, string to, bool overwrite)
    {
        try
        {
            File.Move(from, to, overwrite);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    //CreateHardLink 跨平台硬链接 Windows 与 Unix 各走一套系统调用
    private static void CreateHardLink(string target, string source)
    {
        var success = OperatingSystem.IsWindows()
            ? CreateHardLinkWindows(target, source, IntPtr.Zero)
            : CreateHardLinkUnix(source, target) == 0;
        if (!success) throw new IOException($"Failed to create hard link {target} -> {source}");
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkWindows(string newLink, string existingFile, IntPtr securityAttributes);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLinkUnix(string existingPath, string newPath);

    //Moves 一次搬运涉及的目录 复制出来的文件 以及原有文件
    public record Moves(List<string> Directories, List<FileMove> CopiedFiles, List<FileMove> PreexistingFiles);
}
