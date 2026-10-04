using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Context;

//SuggestionContext 建议上下文对应原版com.mojang.brigadier.context.SuggestionContext
//findSuggestionContext定位cursor所属节点供dispatcher补全
public sealed record SuggestionContext<S>(CommandContextBuilder<S> Parent, CommandNode<S> Node, int StartPos);
