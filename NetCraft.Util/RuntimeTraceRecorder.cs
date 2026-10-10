using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using NetCraft.Logging;

namespace NetCraft.Util;

//RuntimeTraceRecorder captures this process's runtime events into a nettrace file
//A general facility rather than server business, so it sits with the rest of the shared utilities: the startup flag
//begins a capture before any business layer exists, and /debug trace drives the same recorder afterwards
//
//Goes through EventPipe, so it needs no elevation and works on every platform. Deliberately not ETW: that writes a real
//etl, but only on Windows and starting a session normally needs administrator rights
public static class RuntimeTraceRecorder
{
    //The runtime provider, whose name is a fixed string on every platform despite the Windows prefix
    private const string RuntimeProviderName = "Microsoft-Windows-DotNETRuntime";

    //The keyword set behind ClrTraceEventParser.Keywords.Default: GC, loader, JIT, exceptions, contention, threading,
    //types, JIT mapping and the app domain resource counters
    //Spelled out rather than referenced so this library needs no dependency on the trace reader, which is only needed to
    //read these captures back; the value was taken from that enum
    private const long RuntimeKeywords = 0x14C14FCCBD;

    //The sample profiler provider, the source of the CPU samples in the capture
    private const string SampleProviderName = "Microsoft-DotNETCore-SampleProfiler";

    //A capture lands in the background, so the buffer has to absorb a burst while the writer drains it
    //A dropped event leaves a hole in the trace, which costs more than the memory held here
    private const int BufferMb = 256;

    //Role the role segment in the file name, matching the crash report naming
    private const string Role = "netcraft";

    private static readonly Lock Sync = new();
    private static EventPipeSession? _session;
    //The type is spelled out: this namespace holds a Thread sub-namespace, which outranks a using alias
    private static System.Threading.Thread? _writer;
    private static string? _path;

    //IsRunning whether a capture is in progress
    public static bool IsRunning { get { lock (Sync) return _session is not null; } }

    //Start begins a capture under the given directory and reports the file being written
    //The directory is the caller's to choose: the program root is a kernel notion that this library does not carry
    //Returns null when one is already running or a capture could not start; the reason goes to the log
    public static string? Start(string directory)
    {
        lock (Sync)
        {
            if (_session is not null) return null;

            string path;
            try
            {
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, $"trace-{DateTime.Now:yyyy-MM-dd_HH.mm.ss}-{Role}.nettrace");
            }
            catch (Exception e)
            {
                Log.Warning($"[trace] the trace directory could not be prepared: {e.Message}");
                return null;
            }

            EventPipeSession session;
            try
            {
                //The runtime provider is pinned at Verbose: allocation ticks (GCAllocationTick) are a Verbose event, and at
                //Informational the capture comes back with no allocation data at all, which leaves the per-chunk allocation
                //numbers unattributable. Measured: 0 ticks at Informational, 7759 at Verbose over the same allocation load
                //The keywords were never the problem, Default already carries GC and GCHeapAndTypeNames
                //The price is a larger capture and more runtime work while it runs
                var providers = new[]
                {
                    new EventPipeProvider(RuntimeProviderName, EventLevel.Verbose, RuntimeKeywords, null),
                    new EventPipeProvider(SampleProviderName, EventLevel.Informational, 0, null),
                };

                //Rundown is what lets a reader name types and methods instead of showing raw addresses
                session = new DiagnosticsClient(Environment.ProcessId)
                    .StartEventPipeSession(providers, requestRundown: true, circularBufferMB: BufferMb);
            }
            catch (Exception e)
            {
                Log.Warning($"[trace] the capture could not be started: {e.Message}");
                return null;
            }

            _session = session;
            _path = path;
            _writer = new System.Threading.Thread(() => Drain(session, path))
                { Name = "NetCraft-TraceWriter", IsBackground = true };
            _writer.Start();

            Log.Info($"[trace] recording to {path}");
            return path;
        }
    }

    //Stop ends the running capture and reports the file it wrote; null means none was running
    public static string? Stop()
    {
        EventPipeSession? session;
        System.Threading.Thread? writer;
        string? path;
        lock (Sync)
        {
            session = _session;
            writer = _writer;
            path = _path;
            _session = null;
            _writer = null;
            _path = null;
        }

        if (session is null) return null;

        try
        {
            //Ending the session closes the event stream, which is what lets the writer reach the end of the file
            session.Stop();
            writer?.Join();
        }
        catch (Exception e)
        {
            Log.Warning($"[trace] the capture did not stop cleanly: {e.Message}");
        }
        finally
        {
            session.Dispose();
        }

        Log.Info($"[trace] written to {path}");
        return path;
    }

    //Drain writes the session's event stream into the file for as long as the session lives
    private static void Drain(EventPipeSession session, string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            session.EventStream.CopyTo(stream);
        }
        catch (Exception e)
        {
            Log.Warning($"[trace] writing the capture failed: {e.Message}");
        }
    }
}
