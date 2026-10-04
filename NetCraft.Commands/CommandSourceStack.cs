namespace NetCraft.Commands;

//CommandSourceStack 命令源上下文对应原版 net.minecraft.commands.CommandSourceStack
//描述命令执行者位置/权限/输出接收者
//允许派生以支持不同命令源类型（玩家/控制台/命令方块）
public class CommandSourceStack
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
}
