using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//Vec2Argument 二维坐标参数对应原版 Vec2Argument
//解析 x z 两段世界坐标 缺段抛未完成错 求值时补 y 段取执行者位置
public sealed class Vec2Argument(bool centerCorrect) : ArgumentType<Coordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos2d.incomplete"));

    public static Vec2Argument Vec2() => new(true);

    public static Vec2Argument Vec2(bool centerCorrect) => new(centerCorrect);

    public Coordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        if (!reader.CanRead()) throw ErrorNotComplete.CreateWithContext(reader);
        var x = WorldCoordinate.ParseDouble(reader, centerCorrect);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseDouble(reader, centerCorrect);
        //y 段补相对 0 二维参数不带高度
        return new WorldCoordinates(x, new WorldCoordinate(true, 0.0), z);
    }

    //GetCoordinates 取解析结果
    public static Coordinates GetCoordinates(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Coordinates>(name);

    //GetVec2 取绝对坐标的 x/z 分量 对应原版 getVec2
    public static (double X, double Z) GetVec2(CommandContext<CommandSourceStack> context, string name)
    {
        var position = GetCoordinates(context, name).GetPosition((ServerCommandSource)context.GetSource());
        return (position.X, position.Z);
    }

    public IReadOnlyList<string> Examples => new[] { "0 0", "~ ~", "0.1 -0.5", "~1 ~-2" };
}
