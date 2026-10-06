using NetCraft.Registry;

namespace NetCraft.Commands.Execution;

//TraceCallbacks execution trace callbacks, maps to vanilla net.minecraft.commands.execution.TraceCallbacks
///Debug tools like /debug function trace use it to get traces of command entry/return and function calls
public interface TraceCallbacks : IDisposable
{
    //OnCommand command started
    void OnCommand(int depth, string command);

    //OnReturn command returned
    void OnReturn(int depth, string command, int result);

    //OnError command errored
    void OnError(string message);

    //OnCall function called
    void OnCall(int depth, Identifier functionId, int size);
}
