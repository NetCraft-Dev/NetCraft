using NetCraft.Commands.Execution;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//InstantiatedFunction 已实例化的函数对应原版 net.minecraft.commands.functions.InstantiatedFunction
//宏函数带参实例化后产出一条条未绑定动作 普通函数实例化就是自身
public interface InstantiatedFunction<T>
{
    //Id 函数标识
    Identifier Id { get; }

    //Entries 函数体条目
    List<UnboundEntryAction<T>> Entries { get; }
}
