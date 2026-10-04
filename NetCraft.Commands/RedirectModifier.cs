using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//RedirectModifier 重定向修改器委托对应原版com.mojang.brigadier.RedirectModifier
//根据CommandContext返回多个source用于fork展开
public delegate ICollection<S> RedirectModifier<S>(CommandContext<S> context);
