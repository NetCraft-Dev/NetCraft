using System.Threading;
using NetCraft.Logging;
//命名空间 Thread 与原版 rcon.thread 子包对齐 本文件要用 System.Threading.Thread 只能起别名
using ThreadType = System.Threading.Thread;

namespace NetCraft.Game.Server.Rcon.Thread;

//GenericThread 长驻线程基座对应原版 net.minecraft.server.rcon.thread.GenericThread
//running 置假后子类循环自己退出 stop 每秒 join 一次 五秒后打断
//未捕获异常记日志不带走进程 对应原版 DefaultUncaughtExceptionHandlerWithName
public abstract class GenericThread
{
    private static int _uniqueThreadId;
    private const int MaxStopWait = 5;

    private readonly object _gate = new();
    private readonly string _name;
    private ThreadType? _thread;

    protected GenericThread(string name) => _name = name;

    //ThreadName 线程显示名 供子类打日志
    protected string ThreadName => _name;

    //Running 循环继续标志
    protected volatile bool Running;

    //Start 起线程已在跑则无事发生 对应原版 start 查询线程要先起 socket 故可重写
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

    //Stop 停线程 每秒等一次 join 五秒后打断 对应原版 stop
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

    //IsRunning 是否在跑
    public bool IsRunning() => Running;

    //RunGuarded 兜住未捕获异常
    private void RunGuarded()
    {
        try
        {
            Run();
        }
        catch (ThreadInterruptedException)
        {
            //stop 打断阻塞中的循环体 正常退出路径
        }
        catch (Exception e)
        {
            Log.Error($"Uncaught exception in thread {_name} {e}");
        }
    }

    //Run 子类循环体
    protected abstract void Run();
}
