using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//SingleRedirectModifier 单源重定向修改器委托对应原版com.mojang.brigadier.SingleRedirectModifier
//根据CommandContext返回单个source用于redirect展开
//ArgumentBuilder.redirect(target, SingleRedirectModifier)包装为返回单元素集合的RedirectModifier
public delegate S SingleRedirectModifier<S>(CommandContext<S> context);
