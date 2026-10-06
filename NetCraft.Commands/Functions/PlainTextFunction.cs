using NetCraft.Commands.Execution;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//PlainTextFunction 无宏的函数对应原版 net.minecraft.commands.functions.PlainTextFunction record
//条目已编译好 实例化就是自身
public sealed record PlainTextFunction<T> : InstantiatedFunction<T>, CommandFunction<T>
   
{
    public Identifier Id { get; }

    public List<UnboundEntryAction<T>> Entries { get; }

    public PlainTextFunction(Identifier id, List<UnboundEntryAction<T>> entries)
    {
        Id = id;
        Entries = entries;
    }

    //Instantiate 无参数函数直接返回自身
    public InstantiatedFunction<T> Instantiate(CompoundTag? arguments, CommandDispatcher<T> dispatcher)
        => this;
}
