using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Nbt;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//CompoundTagArgument SNBT compound tag argument, maps to vanilla CompoundTagArgument
//Written as {...} in commands, used by commands such as summon to pass an entity's initial NBT
//Registered at network id 21 (nbt_compound_tag); the client tokenizes with the vanilla parser by the same id, with matching syntax and behavior
public sealed class CompoundTagArgument : ArgumentType<CompoundTag>
{
    //Instance parameterless singleton, consistent with the factory style of other argument types
    public static readonly CompoundTagArgument Instance = new();

    public static CompoundTagArgument CompoundTag() => Instance;

    public CompoundTag Parse(StringReader reader)
    {
        //The SNBT parser has its own cursor; parse with it and write the position back to the command reader
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        var tag = TagParser<Tag>.ParseCompoundAsArgument(nbtReader);
        reader.SetCursor(nbtReader.Cursor);
        return tag;
    }

    //GetCompoundTag gets the parsed compound tag
    public static CompoundTag GetCompoundTag(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<CompoundTag>(name);
}
