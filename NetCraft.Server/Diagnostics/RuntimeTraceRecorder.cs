using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;
using NetCraft.Game.Server;
using NetCraft.Logging;

namespace NetCraft.Server.Diagnostics;

//RuntimeTraceRecorder captures this process's runtime events into a nettrace file, backing /debug trace on and off
//Goes through EventPipe the same way GcEventMonitor does, so it needs no elevation and works on every platform
//Deliberately not ETW: that would write a real etl but is Windows-only and starting a session normally needs admin
public sealed class RuntimeTraceRecorder : ServerTraceControl
{
    //The sample profiler provider, the source of the CPU samples in the capture
    private const string SampleProviderName = "Microsoft-DotNETCore-SampleProfiler";

    //A capture lands in the background, so the buffer has to absorb a burst while the writer drains it
    //A dropped event leaves a hole in the trace, which costs more than the memory held here
    private const int BufferMb = 256;

    //NameSegment the role segment in the file name, matching CrashHandler's report naming
    private const string NameSegment = "netcraft";

    private readonly Lock _sync = new();
    private EventPipeSession? _session;
    private Thread? _writer;
    private string? _path;

    //Start begins a capture, reporting the file being written; null means one is already running or it could not start
    public override string? Start()
    {
        lock (_sync)
        {
            if (_session is not null) return null;

            string path;
            try
            {
                Directory.CreateDirectory(AppPaths.TracesDir);
                path = Path.Combine(AppPaths.TracesDir,
                    $"trace-{DateTime.Now:yyyy-MM-dd_HH.mm.ss}-{NameSegment}.nettrace");
            }
            catch (Exception e)
            {
                Log.Warning($"[trace] the trace directory could not be prepared: {e.Message}");
                return null;
            }

            EventPipeSession session;
            try
            {
                var providers = new[]
                {
                    //The runtime provider over the whole default keyword set, which covers GC, loader, JIT, exceptions,
                    //contention and threading; the sample profiler then adds the CPU samples on top
                    new EventPipeProvider(ClrTraceEventParser.ProviderName, EventLevel.Informational,
                        (long)ClrTraceEventParser.Keywords.Default, null),
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
            _writer = new Thread(() => Drain(session, path)) { Name = "NetCraft-TraceWriter", IsBackground = true };
            _writer.Start();
            return path;
        }
    }

    //Stop ends the running capture and reports the file it wrote; null means none was running
    public override string? Stop()
    {
        EventPipeSession? session;
        Thread? writer;
        string? path;
        lock (_sync)
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
