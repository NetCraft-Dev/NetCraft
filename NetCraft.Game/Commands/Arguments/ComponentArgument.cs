using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;
using UtilSyntaxException = NetCraft.Util.Parsing.Packrat.Commands.CommandSyntaxException;

namespace NetCraft.Game.Commands.Arguments;

//ComponentArgument text component argument, maps to vanilla net.minecraft.commands.arguments.ComponentArgument
//In commands, write an SNBT fragment "text" {text:"text"} [""] and it is parsed into a chat component
//Registered at network id 18 (component); the client tokenizes with the vanilla parser by the same id
public sealed class ComponentArgument : ArgumentType<Component>
{
    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "\"hello world\"", "'hello world'", "\"\"", "{text:\"hello world\"}", "[\"\"]" };

    private static readonly ComponentArgument Instance = new();

    public static ComponentArgument TextComponent() => Instance;

    public Component Parse(StringReader reader)
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
            throw ErrorInvalidComponent.CreateWithContext(reader, e.RawMessage);
        }
        reader.SetCursor(nbtReader.Cursor);
        return ComponentSerialization.FromTag(tag);
    }

    //ErrorInvalidComponent component syntax error, maps to vanilla ERROR_INVALID_COMPONENT
    private static readonly DynamicCommandExceptionType ErrorInvalidComponent =
        new(arg => new LiteralMessage($"invalid text component: {arg}"));

    //GetRawComponent gets the parsed component, maps to vanilla getRawComponent
//Vanilla also has getResolvedComponent for parsing selector placeholders; the NC component system has no selector content yet, so it is not provided
    public static Component GetRawComponent(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Component>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
