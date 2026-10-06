using System.Runtime.InteropServices;
using NetCraft.Logging;

namespace NetCraft.Storage;

//CopyOnWriteFileSystem, copy-on-write filesystem, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFileSystem
//At build time the base directory is laid out as a node tree; before changes files are copied into the temp directory, and finally the move operations are collected and written to disk together
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

    //Create builds the filesystem; an existing temp directory errors out, and the directory is really created after construction
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

    //Close cleans up the temp directory
    public void Close()
    {
        if (Directory.Exists(_tmpDirectory)) Directory.Delete(_tmpDirectory, true);
    }

    //ResetFileTreeToBaseFolderContent rescans the base directory and rebuilds the file tree, for tests
    public void ResetFileTreeToBaseFolderContent() => _fileTree = BuildFileTreeFrom(_baseDirectory);

    //CreateTemporaryFilePath allocates a new temp file path
    public string CreateTemporaryFilePath() => Path.Combine(_tmpDirectory, $"tmp_{Interlocked.Increment(ref _tmpFileIndex)}");

    //BuildFileTreeFrom recursively lays out the base directory as a node tree
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

    //CheckAttributes rejects symlinks and read-only paths
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

    //ToCowPath converts a real path into the virtual path in the file tree
    private static CopyOnWriteFSPath ToCowPath(string baseDirectory, string realPath, DirectoryNode fileTree)
        => fileTree.Path.Resolve(Path.GetRelativePath(baseDirectory, realPath).Replace('\\', '/'));

    //CollectMoveOperations walks the file tree and collects directories plus the two kinds of file moves
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

    //CreateDirectories creates directories one by one; the parent must already exist
    public static void CreateDirectories(IReadOnlyList<string> directories)
    {
        foreach (var directory in directories) Directory.CreateDirectory(directory);
    }

    //HardLinkFiles hard-links unchanged files to the target to save space
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

    //MoveFilesWithRetry skips already-in-place or existing entries when moving back, the rest must move
    public static void MoveFilesWithRetry(IReadOnlyList<FileMove> moves, bool overwrite = false)
    {
        foreach (var move in moves)
        {
            if (!File.Exists(move.From) && File.Exists(move.To)) continue;
            if (!File.Exists(move.From)) throw new IOException($"Not a regular file: {move.From}");
            File.Move(move.From, move.To, overwrite);
        }
    }

    //TryRevertMoves moves back files already moved away, returns the failures
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

    //CreateHardLink, cross-platform hard links with separate syscalls for Windows and Unix
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

    //Moves, the directories, copied-out files and preexisting files involved in one move
    public record Moves(List<string> Directories, List<FileMove> CopiedFiles, List<FileMove> PreexistingFiles);
}
