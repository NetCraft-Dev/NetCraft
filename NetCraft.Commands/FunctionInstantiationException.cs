namespace NetCraft.Commands;

//FunctionInstantiationException 函数实例化失败对应原版 net.minecraft.commands.FunctionInstantiationException
//宏函数缺参数或宏行编译失败时抛出 调用方按函数级吞掉
public sealed class FunctionInstantiationException : Exception
{
    public FunctionInstantiationException(string message) : base(message)
    {
    }
}
