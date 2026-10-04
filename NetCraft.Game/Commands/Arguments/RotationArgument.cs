using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//RotationArgument 朝向参数对应原版 RotationArgument
//两段 yaw pitch 各自支持 ~ 相对 z 段固定相对 0
public sealed class RotationArgument : ArgumentType<Coordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.rotation.incomplete"));

    public static RotationArgument Rotation() => new();

    public Coordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        if (!reader.CanRead())
            throw ErrorNotComplete.CreateWithContext(reader);
        //第一段输入作用 yaw 第二段作用 pitch 构造时 x 段装 pitch y 段装 yaw 对齐原版 Vec2(x=pitch,y=yaw)
        var yaw = WorldCoordinate.ParseDouble(reader, false);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var pitch = WorldCoordinate.ParseDouble(reader, false);
        return new WorldCoordinates(pitch, yaw, new WorldCoordinate(true, 0.0));
    }

    //GetRotation 取解析结果
    public static Coordinates GetRotation(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Coordinates>(name);

    public IReadOnlyList<string> Examples => new[] { "0 0", "~ ~", "~-5 ~5" };
}
