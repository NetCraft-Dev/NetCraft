using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//SingleRedirectModifier delegate maps to vanilla com.mojang.brigadier.SingleRedirectModifier
//Returns a single source from a CommandContext for redirect expansion
//ArgumentBuilder.redirect(target, SingleRedirectModifier) wraps it into a RedirectModifier returning a single-element collection
public delegate S SingleRedirectModifier<S>(CommandContext<S> context);
