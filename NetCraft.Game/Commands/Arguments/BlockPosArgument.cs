using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//BlockCoordinates 三段方块坐标对应原版 BlockPosArgument 解析出的坐标三元组
//与 WorldCoordinates 的差别是不支持 ^ 局部坐标 绝对段只接受整数
public sealed record BlockCoordinates(WorldCoordinate X, WorldCoordinate Y, WorldCoordinate Z)
{
    //GetBlockPos 按执行者位置求绝对坐标 相对段允许小数故统一下取整到方块格
    public BlockPos GetBlockPos(ServerCommandSource source)
    {
        var pos = source.Position;
        return new BlockPos(
            (int)Math.Floor(X.Get(pos.X)),
            (int)Math.Floor(Y.Get(pos.Y)),
            (int)Math.Floor(Z.Get(pos.Z)));
    }
}

//BlockPosArgument 方块坐标参数对应原版 net.minecraft.commands.arguments.coordinates.BlockPosArgument
//setblock 与 fill 用它定位 语法 0 0 0 或 ~ ~1 ~
public sealed class BlockPosArgument : ArgumentType<BlockCoordinates>
{
    public static readonly SimpleCommandExceptionType ErrorNotComplete =
        new(new TranslatableMessage("argument.pos3d.incomplete"));

    public static BlockPosArgument BlockPos() => new();

    public BlockCoordinates Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var x = WorldCoordinate.ParseInt(reader);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var y = WorldCoordinate.ParseInt(reader);
        if (!reader.CanRead() || reader.Peek() != ' ')
        {
            reader.SetCursor(start);
            throw ErrorNotComplete.CreateWithContext(reader);
        }
        reader.Skip();
        var z = WorldCoordinate.ParseInt(reader);
        return new BlockCoordinates(x, y, z);
    }

    //GetBlockPos 取解析结果并按执行者位置求绝对坐标
    public static BlockPos GetBlockPos(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<BlockCoordinates>(name).GetBlockPos((ServerCommandSource)context.GetSource());

    public IReadOnlyList<string> Examples => new[] { "0 0 0", "~ ~ ~", "~1 ~-2 ~3" };
}
