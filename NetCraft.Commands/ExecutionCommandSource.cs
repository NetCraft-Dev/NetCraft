using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Tree;
using NetCraft.Registry;

namespace NetCraft.Commands;

//ExecutionSourceCore 执行源的非泛型核心对应原版 ExecutionCommandSource 里不依赖 T 的那部分成员
//执行引擎的泛型 T 与 brigadier 的无约束 S 共存 引擎内部只认核心接口 免掉类型参数约束
public interface ExecutionSourceCore : PermissionSetSupplier
{
    //Callback 当前结果回调
    CommandResultCallback Callback { get; }

    //HandleError 执行错误上报 forked 表示分叉模式错误只记录不中断
    void HandleError(ICommandExceptionType type, IMessage message, bool forked, TraceCallbacks? tracer);

    //HandleError 异常便捷入口对应原版 handleError(e, forked, tracer)
    void HandleError(CommandSyntaxException e, bool forked, TraceCallbacks? tracer)
        => HandleError(e.Type, e.RawMessage, forked, tracer);

    //IsSilent 静默源错误与回执都不外发
    bool IsSilent { get; }
}

//ExecutionCommandSource 执行引擎的命令源接口对应原版 net.minecraft.commands.ExecutionCommandSource
//在普通命令源之上补齐结果回调/分发器/错误上报 权限集合来自 PermissionSetSupplier
//T 自约束让 WithCallback 能返回具体源类型
public interface ExecutionCommandSource<T> : ExecutionSourceCore where T : ExecutionCommandSource<T>
{
    //WithCallback 换上新的结果回调返回派生源
    T WithCallback(CommandResultCallback callback);

    //ClearCallbacks 清空回调对应原版 clearCallbacks
    T ClearCallbacks() => WithCallback(CommandResultCallback.Empty);

    //Dispatcher 该源所属分发器
    CommandDispatcher<T> Dispatcher();
}
