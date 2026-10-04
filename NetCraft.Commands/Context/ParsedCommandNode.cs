using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Context;

//ParsedCommandNode 已解析节点对应原版com.mojang.brigadier.context.ParsedCommandNode
//记录CommandNode与解析范围供CommandContext.getNodes返回
public sealed record ParsedCommandNode<S>(CommandNode<S> Node, StringRange Range)
{
    public override string ToString() => $"{Node}@{Range}";
}
