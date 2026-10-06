namespace NetCraft.Commands;

//CommandResultCallback maps to vanilla net.minecraft.commands.CommandResultCallback
//The execution chain uses /return to pass values back up the Frame callback chain
public interface CommandResultCallback
{
    //OnResult result callback; when success is false result is always 0
    void OnResult(bool success, int result);

    //OnSuccess reports only the success value
    void OnSuccess(int result) => OnResult(true, result);

    //OnFailure reports only failure
    void OnFailure() => OnResult(false, 0);

    //Chain chains two callbacks, short-circuiting on an empty callback; maps to vanilla chain
    public static CommandResultCallback Chain(CommandResultCallback first, CommandResultCallback second)
    {
        if (ReferenceEquals(first, Empty)) return second;
        if (ReferenceEquals(second, Empty)) return first;
        return new ChainedCallback(first, second);
    }

    //Empty always-empty callback
    public static readonly CommandResultCallback Empty = new EmptyCallback();

    private sealed class EmptyCallback : CommandResultCallback
    {
        public void OnResult(bool success, int result)
        {
        }

        public override string ToString() => "<empty>";
    }

    private sealed class ChainedCallback : CommandResultCallback
    {
        private readonly CommandResultCallback _first;
        private readonly CommandResultCallback _second;

        public ChainedCallback(CommandResultCallback first, CommandResultCallback second)
        {
            _first = first;
            _second = second;
        }

        public void OnResult(bool success, int result)
        {
            _first.OnResult(success, result);
            _second.OnResult(success, result);
        }
    }
}
