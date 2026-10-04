using NetCraft.Commands.Tree;

namespace NetCraft.Commands;

//AmbiguityConsumer 歧义消费者委托对应原版com.mojang.brigadier.AmbiguityConsumer
//findAmbiguities遍历兄弟节点测试examples重叠时回调报告歧义
public delegate void AmbiguityConsumer<S>(CommandNode<S> parent, CommandNode<S> child, CommandNode<S> sibling, ICollection<string> inputs);
