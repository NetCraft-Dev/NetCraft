using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Tree;
using NetCraft.Registry;

namespace NetCraft.Commands;

//CommandSourceStack 命令源上下文对应原版 net.minecraft.commands.CommandSourceStack
//描述命令执行者位置/权限/输出接收者
//允许派生以支持不同命令源类型（玩家/控制台/命令方块）
//实现 ExecutionCommandSource 接入函数执行引擎 权限集合桥到注册表侧的 PermissionLevel
public class CommandSourceStack : ExecutionCommandSource<CommandSourceStack>
{
    public string SenderName { get; }
    public int PermissionLevel { get; }
    public TextWriter Output { get; }
    public bool AcceptsSuccess { get; }
    public bool AcceptsFailure { get; }

    public CommandSourceStack(string senderName, int permissionLevel, TextWriter output, bool acceptsSuccess = true, bool acceptsFailure = true)
    {
        SenderName = senderName;
        PermissionLevel = permissionLevel;
        Output = output;
        AcceptsSuccess = acceptsSuccess;
        AcceptsFailure = acceptsFailure;
    }

    //SendSuccess 发送成功消息 派生类可改为走网络回执
    public virtual void SendSuccess(string message)
    {
        if (AcceptsSuccess) Output?.WriteLine(message);
    }

    //SendFailure 发送失败消息 派生类可改为走网络回执
    public virtual void SendFailure(string message)
    {
        if (AcceptsFailure) Output?.WriteLine(message);
    }

    //HasPermission 是否有指定权限等级
    public bool HasPermission(int level) => PermissionLevel >= level;

    //--- ExecutionCommandSource 实现 ---

    //Permissions 权限集合按等级映射到 LevelBasedPermissionSet 对应原版 hasPermission 的集合化版本
    public PermissionSet Permissions
        => LevelBasedPermissionSet.ForLevel((NetCraft.Registry.PermissionLevel)PermissionLevel);

    //Callback 结果回调默认空
    public CommandResultCallback Callback { get; private set; }

    //WithCallback 换回调返回浅拷贝 派生类重写保留自己的字段
    public virtual CommandSourceStack WithCallback(CommandResultCallback callback)
        => new(SenderName, PermissionLevel, Output, AcceptsSuccess, AcceptsFailure) { Callback = callback };

    //Dispatcher 该源所属分发器 基类不知道分发器由派生类接真实服务端
    public virtual CommandDispatcher<CommandSourceStack> Dispatcher()
        => throw new NotSupportedException("This command source has no dispatcher");

    //HandleError 执行错误上报 写到输出与原版打到源反馈一致
    public void HandleError(ICommandExceptionType type, IMessage message, bool forked, TraceCallbacks? tracer)
    {
        tracer?.OnError(message.GetString());
        SendFailure(message.GetString());
    }

    //IsSilent 静默源默认否
    public bool IsSilent => false;
}
