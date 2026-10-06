using NetCraft.Registry;

namespace NetCraft.Commands.Execution;

//TraceCallbacks 执行追踪回调对应原版 net.minecraft.commands.execution.TraceCallbacks
///debug function trace 之类靠它拿命令进入返回与函数调用的轨迹
public interface TraceCallbacks : IDisposable
{
    //OnCommand 命令开始
    void OnCommand(int depth, string command);

    //OnReturn 命令返回
    void OnReturn(int depth, string command, int result);

    //OnError 命令出错
    void OnError(string message);

    //OnCall 函数调用
    void OnCall(int depth, Identifier functionId, int size);
}
