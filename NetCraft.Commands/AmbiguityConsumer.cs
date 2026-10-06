using NetCraft.Commands.Tree;

namespace NetCraft.Commands;

//AmbiguityConsumer delegate maps to vanilla com.mojang.brigadier.AmbiguityConsumer
//findAmbiguities walks sibling nodes, testing example overlap and reporting ambiguities through the callback
public delegate void AmbiguityConsumer<S>(CommandNode<S> parent, CommandNode<S> child, CommandNode<S> sibling, ICollection<string> inputs);
