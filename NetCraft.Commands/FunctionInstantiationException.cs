namespace NetCraft.Commands;

//FunctionInstantiationException thrown when a function fails to instantiate, maps to vanilla net.minecraft.commands.FunctionInstantiationException
//Thrown when a macro function is missing arguments or a macro line fails to compile; callers swallow it at the function level
public sealed class FunctionInstantiationException : Exception
{
    public FunctionInstantiationException(string message) : base(message)
    {
    }
}
