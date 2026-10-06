using NetCraft.Commands.Execution;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//InstantiatedFunction an instantiated function, maps to vanilla net.minecraft.commands.functions.InstantiatedFunction
//A macro function instantiated with arguments yields unbound actions; an ordinary function instantiates to itself
public interface InstantiatedFunction<T>
{
    //Id function identifier
    Identifier Id { get; }

    //Entries function body entries
    List<UnboundEntryAction<T>> Entries { get; }
}
