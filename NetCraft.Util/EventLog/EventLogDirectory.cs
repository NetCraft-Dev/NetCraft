using System.Collections;
using System.Globalization;
using System.IO.Compression;
using NetCraft.Logging;

namespace NetCraft.Util.EventLog;

//事件日志目录对应原版EventLogDirectory
//文件名是 yyyyMMdd-N 加扩展名，旧文件可以压成gz或按保留天数清掉
public sealed class EventLogDirectory
{
    private const int CompressBufferSize = 4096;

    private const string CompressedExtension = ".gz";

    private readonly string _root;

    private readonly string _extension;

    private EventLogDirectory(string root, string extension)
    {
        _root = root;
        _extension = extension;
    }

    public static EventLogDirectory Open(string root, string extension)
    {
        Directory.CreateDirectory(root);
        return new EventLogDirectory(root, extension);
    }

    public FileList ListFiles()
    {
        var files = new List<IEventLogFile>();
        foreach (var path in Directory.EnumerateFiles(_root))
        {
            var file = ParseFile(path);
            if (file is not null)
                files.Add(file);
        }
        return new FileList(files);
    }

    //只认 yyyyMMdd-N 加本目录扩展名的文件，其余一律无视
    private IEventLogFile? ParseFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var extensionIndex = fileName.IndexOf('.');
        if (extensionIndex == -1)
            return null;
        var id = FileId.Parse(fileName[..extensionIndex]);
        if (id is null)
            return null;
        var extension = fileName[extensionIndex..];
        if (extension == _extension)
            return new RawFile(path, id);
        if (extension == _extension + CompressedExtension)
            return new CompressedFile(path, id);
        return null;
    }

    //当天序号接着往后排，挑一个还没被占的
    public RawFile CreateNewFile(DateOnly date)
    {
        var index = 1;
        var used = ListFiles().Ids();
        FileId id;
        while (used.Contains(id = new FileId(date, index++)))
        {
        }
        var file = new RawFile(Path.Combine(_root, id.ToFileName(_extension)), id);
        using (File.Create(file.Path))
        {
        }
        return file;
    }

    //独占打开原文件压成gz再截断删除，别人拿着的时候直接放弃
    private static void TryCompress(string raw, string compressed)
    {
        if (File.Exists(compressed))
            throw new IOException($"Compressed target file already exists: {compressed}");
        using (var channel = new FileStream(raw, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            WriteCompressed(channel, compressed);
            channel.SetLength(0);
        }
        File.Delete(raw);
    }

    //按块读原文件写进gz
    private static void WriteCompressed(Stream source, string target)
    {
        using var output = new GZipStream(File.Create(target), CompressionLevel.Optimal);
        var buffer = new byte[CompressBufferSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            output.Write(buffer, 0, read);
    }

    //目录里的一份文件清单，可以整批压缩或清理
    public sealed class FileList : IEnumerable<IEventLogFile>
    {
        private readonly List<IEventLogFile> _files;

        internal FileList(List<IEventLogFile> files) => _files = new List<IEventLogFile>(files);

        //日期加保留天数不晚于今天就删掉
        public FileList Prune(DateOnly date, int expiryDays)
        {
            _files.RemoveAll(file =>
            {
                if (date < file.Id.Date.AddDays(expiryDays))
                    return false;
                try
                {
                    File.Delete(file.Path);
                    return true;
                }
                catch (IOException error)
                {
                    Log.Warning($"Failed to delete expired event log file: {file.Path} {error.Message}");
                    return false;
                }
            });
            return this;
        }

        //把还没压过的全压成gz
        public FileList CompressAll()
        {
            for (var index = 0; index < _files.Count; index++)
            {
                try
                {
                    _files[index] = _files[index].Compress();
                }
                catch (IOException error)
                {
                    Log.Warning($"Failed to compress event log file: {_files[index].Path} {error.Message}");
                }
            }
            return this;
        }

        public IEnumerator<IEventLogFile> GetEnumerator() => _files.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<IEventLogFile> Stream() => _files;

        public HashSet<FileId> Ids() => _files.Select(file => file.Id).ToHashSet();
    }

    //文件名标识对应原版EventLogDirectory.FileId
    public sealed record FileId(DateOnly Date, int Index)
    {
        private const string DateFormat = "yyyyMMdd";

        //认不出格式给null
        public static FileId? Parse(string name)
        {
            var separator = name.IndexOf('-');
            if (separator == -1)
                return null;
            var date = name[..separator];
            var index = name[(separator + 1)..];
            if (!DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return null;
            if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                return null;
            return new FileId(parsed, number);
        }

        public override string ToString() => Date.ToString(DateFormat, CultureInfo.InvariantCulture) + "-" + Index;

        public string ToFileName(string extension) => ToString() + extension;
    }

    //日志文件，未压缩或已压缩两种
    public interface IEventLogFile
    {
        string Path { get; }

        FileId Id { get; }

        //按文本读，文件不在了给null
        TextReader? OpenReader();

        //压成gz，已经是gz的原样返回
        CompressedFile Compress();
    }

    //未压缩的日志文件
    public sealed record RawFile(string Path, FileId Id) : IEventLogFile
    {
        //写入方拿它追加事件
        public FileStream OpenChannel() => new(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        public TextReader? OpenReader() => File.Exists(Path) ? new StreamReader(Path) : null;

        public CompressedFile Compress()
        {
            var compressed = Path + CompressedExtension;
            TryCompress(Path, compressed);
            return new CompressedFile(compressed, Id);
        }
    }

    //已压缩的日志文件
    public sealed record CompressedFile(string Path, FileId Id) : IEventLogFile
    {
        public TextReader? OpenReader()
            => File.Exists(Path)
                ? new StreamReader(new GZipStream(File.OpenRead(Path), CompressionMode.Decompress))
                : null;

        public CompressedFile Compress() => this;
    }
}
