using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Tree;
using NetCraft.Registry;

namespace NetCraft.Commands;

//CommandSourceStack command source context, maps to vanilla net.minecraft.commands.CommandSourceStack
//Describes the command executor's position/permission/output receiver
//Derived types are allowed to support different command source kinds (player/console/command block)
//Implements ExecutionCommandSource to plug into the function execution engine; the permission set bridges to PermissionLevel on the registry side
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

    //SendSuccess sends a success message; derived classes may route it over the network instead
    public virtual void SendSuccess(string message)
    {
        if (AcceptsSuccess) Output?.WriteLine(message);
    }

    //SendFailure sends a failure message; derived classes may route it over the network instead
    public virtual void SendFailure(string message)
    {
        if (AcceptsFailure) Output?.WriteLine(message);
    }

    //HasPermission whether the source has the given permission level
    public bool HasPermission(int level) => PermissionLevel >= level;

    //--- ExecutionCommandSource implementation ---

    //Permissions maps the permission set by level to LevelBasedPermissionSet, a set-based version of vanilla hasPermission
    public PermissionSet Permissions
        => LevelBasedPermissionSet.ForLevel((NetCraft.Registry.PermissionLevel)PermissionLevel);

    //Callback result callback defaults to empty; maps to vanilla CommandSourceStack's callback defaulting to EMPTY
    public CommandResultCallback Callback { get; private set; } = CommandResultCallback.Empty;

    //WithCallback swaps the callback and returns a shallow copy; derived classes override to keep their own fields
    public virtual CommandSourceStack WithCallback(CommandResultCallback callback)
        => new(SenderName, PermissionLevel, Output, AcceptsSuccess, AcceptsFailure) { Callback = callback };

    //Dispatcher the dispatcher this source belongs to; the base class does not know it, derived classes wire it to the real server
    public virtual CommandDispatcher<CommandSourceStack> Dispatcher()
        => throw new NotSupportedException("This command source has no dispatcher");

    //HandleError reports an execution error to the output, matching how vanilla sends it to the source's feedback
    public void HandleError(ICommandExceptionType type, IMessage message, bool forked, TraceCallbacks? tracer)
    {
        tracer?.OnError(message.GetString());
        SendFailure(message.GetString());
    }

    //IsSilent silent source, false by default
    public bool IsSilent => false;
}
