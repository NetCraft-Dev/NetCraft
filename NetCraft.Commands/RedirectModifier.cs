using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//RedirectModifier delegate maps to vanilla com.mojang.brigadier.RedirectModifier
//Returns multiple sources from a CommandContext for fork expansion
public delegate ICollection<S> RedirectModifier<S>(CommandContext<S> context);
