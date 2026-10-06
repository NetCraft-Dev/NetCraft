using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Tree;
using NetCraft.Registry;

namespace NetCraft.Commands;

//ExecutionSourceCore non-generic core of an execution source, maps to the members of vanilla ExecutionCommandSource that do not depend on T
//The engine's generic T coexists with brigadier's unconstrained S; the engine only knows the core interface, avoiding a type parameter constraint
public interface ExecutionSourceCore : PermissionSetSupplier
{
    //Callback current result callback
    CommandResultCallback Callback { get; }

    //HandleError reports an execution error; forked means fork mode where errors are only recorded, not aborted
    void HandleError(ICommandExceptionType type, IMessage message, bool forked, TraceCallbacks? tracer);

    //HandleError convenience overload for exceptions; maps to vanilla handleError(e, forked, tracer)
    void HandleError(CommandSyntaxException e, bool forked, TraceCallbacks? tracer)
        => HandleError(e.Type, e.RawMessage, forked, tracer);

    //IsSilent silent source neither errors nor results are emitted
    bool IsSilent { get; }
}

//ExecutionCommandSource command source interface of the execution engine, maps to vanilla net.minecraft.commands.ExecutionCommandSource
//Adds result callbacks/dispatcher/error reporting on top of an ordinary command source; the permission set comes from PermissionSetSupplier
//The self-referencing T constraint lets WithCallback return the concrete source type
public interface ExecutionCommandSource<T> : ExecutionSourceCore where T : ExecutionCommandSource<T>
{
    //WithCallback swaps in a new result callback and returns the derived source
    T WithCallback(CommandResultCallback callback);

    //ClearCallbacks clears callbacks; maps to vanilla clearCallbacks
    T ClearCallbacks() => WithCallback(CommandResultCallback.Empty);

    //Dispatcher the dispatcher this source belongs to
    CommandDispatcher<T> Dispatcher();
}
