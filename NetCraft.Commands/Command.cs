using NetCraft.Commands.Context;

namespace NetCraft.Commands;

//Command callback delegate maps to vanilla com.mojang.brigadier.Command
//Runs on a CommandContext and returns an int result code; SingleSuccess means success
public delegate int Command<S>(CommandContext<S> context);

//CommandConstants holds the SingleSuccess constant since C# delegates cannot
public static class CommandConstants
{
    public const int SingleSuccess = 1;
}
