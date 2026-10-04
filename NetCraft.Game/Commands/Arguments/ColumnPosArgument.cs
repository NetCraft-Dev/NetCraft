using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ColumnCoordinates 两段水平坐标对应原版 ColumnPosArgument 解析出的列坐标
//与 BlockCoordinates 的差别是只取水平两段 不支持局部坐标
public sealed record ColumnCoordinates(WorldCoordinate X, WorldCoordinate Z)
{
    //GetColumn 按执行者位置求绝对方块坐标 调用方再用它换算区块坐标
    public (int X, int Z) GetColumn(ServerCommandSource source)
    {
        var pos = source.Position;
        return ((int)Math.Floor(X.Get(pos.X)), (int)Math.Floor(Z.Get(pos.Z)));
    }
}

//ColumnPosArgument 列坐标参数对应原版 net.minecraft.commands.arguments.coordinates.ColumnPosArgument
//forceload 用它定位区块列 语法 0 0 或 ~ ~1
public sealed class ColumnPosArgument : ArgumentType<ColumnCoordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos2d.incomplete"));

    public static ColumnPosArgument ColumnPos() => new();

    public ColumnCoordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var x = WorldCoordinate.ParseInt(reader);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseInt(reader);
        return new ColumnCoordinates(x, z);
    }

    //GetColumn 取解析结果并按执行者位置求绝对方块坐标
    public static (int X, int Z) GetColumn(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<ColumnCoordinates>(name).GetColumn((ServerCommandSource)context.GetSource());

    public IReadOnlyList<string> Examples => new[] { "0 0", "~ ~", "~1 ~-2" };
}
