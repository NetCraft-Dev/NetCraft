using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//ResultConsumer delegate maps to vanilla com.mojang.brigadier.ResultConsumer
//Called back when command execution completes with success, failure and result code
public delegate void ResultConsumer<S>(CommandContext<S> context, bool success, int result);
