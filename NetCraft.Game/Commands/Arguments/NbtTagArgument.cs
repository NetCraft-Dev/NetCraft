using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;
using UtilSyntaxException = NetCraft.Util.Parsing.Packrat.Commands.CommandSyntaxException;

namespace NetCraft.Game.Commands.Arguments;

//NbtTagArgument any NBT tag argument, maps to vanilla net.minecraft.commands.arguments.NbtTagArgument
//Written in commands as SNBT fragments such as 0 0.0 {} {foo=bar}, complementary to CompoundTagArgument which only accepts compound tags
//Registered at network id 22 (nbt_tag); the client tokenizes with the vanilla parser by the same id
public sealed class NbtTagArgument : ArgumentType<Tag>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "0", "0.0", "{}", "{foo=bar}" };

    private static readonly NbtTagArgument Instance = new();

    public static NbtTagArgument NbtTag() => Instance;

    public Tag Parse(StringReader reader)
    {
        //The SNBT parser has its own cursor; parse with it and write the position back to the command reader
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        Tag tag;
        try
        {
            tag = TagParser<Tag>.ParseTagAsArgument(nbtReader);
        }
        catch (UtilSyntaxException e)
        {
            throw ErrorInvalidTag.CreateWithContext(reader, e.RawMessage);
        }
        reader.SetCursor(nbtReader.Cursor);
        return tag;
    }

    //ErrorInvalidTag tag syntax error, maps to vanilla ERROR_INVALID_TYPE
    private static readonly DynamicCommandExceptionType ErrorInvalidTag =
        new(arg => new LiteralMessage($"invalid NBT tag: {arg}"));

    //GetNbtTag gets the parsed tag
    public static Tag GetNbtTag(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Tag>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
