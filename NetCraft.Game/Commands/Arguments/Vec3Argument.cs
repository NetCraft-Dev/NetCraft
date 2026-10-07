using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//Vec3Argument 3D coordinate argument, maps to vanilla Vec3Argument
//A ^ prefix goes through local coordinates, otherwise world coordinates; centerCorrect controls block center correction
public sealed class Vec3Argument(bool centerCorrect) : ArgumentType<Coordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos3d.incomplete"));

    public static readonly SimpleCommandExceptionType ErrorMixedType =
        new(new TranslatableMessage("argument.pos.mixed"));

    public static Vec3Argument Vec3() => new(true);
    public static Vec3Argument Vec3(bool centerCorrect) => new(centerCorrect);

    public Coordinates Parse(StringReader reader)
    {
        if (reader.CanRead() && reader.Peek() == '^')
            return LocalCoordinates.Parse(reader);
        return WorldCoordinates.ParseDouble(reader, centerCorrect);
    }

    //GetCoordinates gets the parse result
    public static Coordinates GetCoordinates(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Coordinates>(name);

    //GetVec3 resolves the absolute coordinate from the executor's position
    public static Vec3 GetVec3(CommandContext<CommandSourceStack> context, string name)
        => GetCoordinates(context, name).GetPosition((ServerCommandSource)context.GetSource());

    public IReadOnlyList<string> Examples => new[] { "0 0 0", "~ ~ ~", "^ ^ ^", "^1 ^ ^-5", "0.1 -0.5 .9", "~0.5 ~1 ~-5" };
}
