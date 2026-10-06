using System.Formats.Tar;
using ZstdSharp;

namespace NetCraft.Logging;

//LogArchive packs historical log files into a single tar.zst
//Logs are highly repetitive text, so keeping them raw wastes space; the originals are deleted after packing
//leaving only the current session's .log plus one archive per past session
internal static class LogArchive
{
    //Extension archive file extension
    internal const string Extension = ".tar.zst";

    //SearchPattern matches the same set as Extension, used by the retention sweep
    internal const string SearchPattern = "*" + Extension;

    //Level zstd compression level
    //Log text is redundant enough that level 3 already gets most of the size back, anything higher just burns CPU
    private const int Level = 3;

    //Archive packs every .log currently in the directory and deletes the originals
    //A missing compression library or a full disk is treated as "not archived"
    //archiving must never block the log sink itself
    public static void Archive(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        var logs = Directory.GetFiles(directory, "*.log");
        if (logs.Length == 0)
            return;

        var archive = Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{Extension}");
        //Packs into a staging file first so a half-written archive can never pass for a complete one
        var staging = archive + ".tmp";
        try
        {
            Pack(logs, staging);
            File.Move(staging, archive, overwrite: true);
        }
        catch
        {
            TryDelete(staging);
            return;
        }

        foreach (var log in logs)
            TryDelete(log);
    }

    //Pack writes a batch of logs into one zstd-compressed tar
    private static void Pack(string[] logs, string staging)
    {
        using var file = File.Create(staging);
        using var zstd = new CompressionStream(file, Level);
        //leaveOpen lets the compression stream finish; the tar end blocks must be compressed before the file closes
        using var tar = new TarWriter(zstd, TarEntryFormat.Pax, leaveOpen: true);
        foreach (var log in logs)
            tar.WriteEntry(log, Path.GetFileName(log));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
