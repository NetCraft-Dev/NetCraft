using System.Collections;
using System.Globalization;
using System.IO.Compression;
using NetCraft.Logging;

namespace NetCraft.Util.EventLog;

//Event log directory, maps to vanilla EventLogDirectory
//Filename is yyyyMMdd-N plus extension; old files can be compressed to gz or cleaned by retention days
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

    //Only recognizes yyyyMMdd-N files with this directory's extension, everything else is ignored
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

    //The day's sequence continues, picking one not yet taken
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

    //Opens the original exclusively, compresses to gz then truncates and deletes; gives up if someone else holds it
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

    //Reads the original in blocks and writes into the gz
    private static void WriteCompressed(Stream source, string target)
    {
        using var output = new GZipStream(File.Create(target), CompressionLevel.Optimal);
        var buffer = new byte[CompressBufferSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            output.Write(buffer, 0, read);
    }

    //A file listing of the directory, can bulk-compress or clean up
    public sealed class FileList : IEnumerable<IEventLogFile>
    {
        private readonly List<IEventLogFile> _files;

        internal FileList(List<IEventLogFile> files) => _files = new List<IEventLogFile>(files);

        //Deletes files whose date plus retention days is not later than today
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

        //Compresses all uncompressed files to gz
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

    //Filename identifier, maps to vanilla EventLogDirectory.FileId
    public sealed record FileId(DateOnly Date, int Index)
    {
        private const string DateFormat = "yyyyMMdd";

        //Returns null on an unrecognized format
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

    //Log file, either uncompressed or compressed
    public interface IEventLogFile
    {
        string Path { get; }

        FileId Id { get; }

        //Reads as text, returns null if the file is gone
        TextReader? OpenReader();

        //Compresses to gz, returns as-is if already gz
        CompressedFile Compress();
    }

    //Uncompressed log file
    public sealed record RawFile(string Path, FileId Id) : IEventLogFile
    {
        //The writer uses it to append events
        public FileStream OpenChannel() => new(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        public TextReader? OpenReader() => File.Exists(Path) ? new StreamReader(Path) : null;

        public CompressedFile Compress()
        {
            var compressed = Path + CompressedExtension;
            TryCompress(Path, compressed);
            return new CompressedFile(compressed, Id);
        }
    }

    //Compressed log file
    public sealed record CompressedFile(string Path, FileId Id) : IEventLogFile
    {
        public TextReader? OpenReader()
            => File.Exists(Path)
                ? new StreamReader(new GZipStream(File.OpenRead(Path), CompressionMode.Decompress))
                : null;

        public CompressedFile Compress() => this;
    }
}
