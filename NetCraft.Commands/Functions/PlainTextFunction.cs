using NetCraft.Commands.Execution;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//PlainTextFunction a function without macros, maps to vanilla net.minecraft.commands.functions.PlainTextFunction record
//Entries are already compiled; instantiation returns itself
public sealed record PlainTextFunction<T> : InstantiatedFunction<T>, CommandFunction<T>
   
{
    public Identifier Id { get; }

    public List<UnboundEntryAction<T>> Entries { get; }

    public PlainTextFunction(Identifier id, List<UnboundEntryAction<T>> entries)
    {
        Id = id;
        Entries = entries;
    }

    //Instantiate a function without arguments returns itself directly
    public InstantiatedFunction<T> Instantiate(CompoundTag? arguments, CommandDispatcher<T> dispatcher)
        => this;
}
