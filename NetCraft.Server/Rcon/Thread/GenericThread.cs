using System.Threading;
using NetCraft.Logging;
//The Thread namespace aligns with the vanilla rcon.thread subpackage, this file needs System.Threading.Thread so an alias is required
using ThreadType = System.Threading.Thread;

namespace NetCraft.Game.Server.Rcon.Thread;

//GenericThread, base for long-lived threads, maps to vanilla net.minecraft.server.rcon.thread.GenericThread
//After running is set false the subclass loop exits itself, stop joins once per second and interrupts after five seconds
//Uncaught exceptions are logged and do not take down the process, maps to vanilla DefaultUncaughtExceptionHandlerWithName
public abstract class GenericThread
{
    private static int _uniqueThreadId;
    private const int MaxStopWait = 5;

    private readonly object _gate = new();
    private readonly string _name;
    private ThreadType? _thread;

    protected GenericThread(string name) => _name = name;

    //ThreadName the thread display name, for subclasses to log
    protected string ThreadName => _name;

    //Running loop continuation flag
    protected volatile bool Running;

    //Start starts the thread, nothing happens if already running, maps to vanilla start, overridable since the query thread must open a socket first
    public virtual bool Start()
    {
        lock (_gate)
        {
            if (Running) return true;
            Running = true;
            _thread = new ThreadType(RunGuarded)
            {
                Name = $"{_name} #{Interlocked.Increment(ref _uniqueThreadId)}",
                IsBackground = true,
            };
            _thread.Start();
            Log.Info($"Thread {_name} started");
            return true;
        }
    }

    //Stop stops the thread, joins once per second and interrupts after five seconds, maps to vanilla stop
    public virtual void Stop()
    {
        lock (_gate)
        {
            Running = false;
            var thread = _thread;
            if (thread is null) return;
            var waited = 0;
            while (thread.IsAlive)
            {
                thread.Join(1000);
                if (++waited >= MaxStopWait)
                {
                    Log.Warning($"Waited {waited} seconds attempting force stop!");
                    continue;
                }
                if (!thread.IsAlive) continue;
                Log.Warning($"Thread {_name} ({thread.ThreadState}) failed to exit after {waited} second(s)");
                thread.Interrupt();
            }
            Log.Info($"Thread {_name} stopped");
            _thread = null;
        }
    }

    //IsRunning whether it is running
    public bool IsRunning() => Running;

    //RunGuarded catches uncaught exceptions
    private void RunGuarded()
    {
        try
        {
            Run();
        }
        catch (ThreadInterruptedException)
        {
            //stop interrupts the blocked loop body, the normal exit path
        }
        catch (Exception e)
        {
            Log.Error($"Uncaught exception in thread {_name} {e}");
        }
    }

    //Run the subclass loop body
    protected abstract void Run();
}
