using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Context;

//SuggestionContext maps to vanilla com.mojang.brigadier.context.SuggestionContext
//findSuggestionContext locates the node the cursor belongs to for dispatcher completion
public sealed record SuggestionContext<S>(CommandContextBuilder<S> Parent, CommandNode<S> Node, int StartPos);
