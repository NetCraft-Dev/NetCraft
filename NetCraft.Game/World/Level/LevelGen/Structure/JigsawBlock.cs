using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using GameDirection = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawBlock 拼图方块语义辅助 对应原版 net.minecraft.world.level.block.JigsawBlock
//项目还没注册 jigsaw 方块 这里只抽出连接判定需要的静态语义 接入方块时直接搬进方块类即可
public static class JigsawBlock
{
    //OrientationPropertyName 朝向属性名 对应原版 JigsawBlock.ORIENTATION
    public const string OrientationPropertyName = "orientation";

    //GetFrontFacing 取正面朝向 对应原版 getFrontFacing
    //朝向属性缺失或值非法时退回方块默认朝向朝北
    public static GameDirection GetFrontFacing(BlockState state)
        => GetOrientation(state) is { } orientation ? FrontOf(orientation) : GameDirection.North;

    //GetTopFacing 取顶面朝向 对应原版 getTopFacing
    public static GameDirection GetTopFacing(BlockState state)
        => GetOrientation(state) is { } orientation ? TopOf(orientation) : GameDirection.Up;

    //CanAttach 判定两个拼图方块能不能接上 对应原版 canAttach
    //三个条件同时成立：源正面是目标正面的反面 关节可滚动或顶面朝向一致 源的 target 等于目标的 name
    public static bool CanAttach(StructureTemplate.JigsawBlockInfo source, StructureTemplate.JigsawBlockInfo target)
    {
        var sourceFront = GetFrontFacing(source.Info.State);
        var targetFront = GetFrontFacing(target.Info.State);
        var sourceTop = GetTopFacing(source.Info.State);
        var targetTop = GetTopFacing(target.Info.State);
        var rollable = source.JointType == JointType.Rollable;
        return sourceFront == targetFront.Opposite
            && (rollable || sourceTop == targetTop)
            && source.Target == target.Name;
    }

    //GetOrientation 读朝向属性值 没有该属性时返回 null
    private static FrontAndTop? GetOrientation(BlockState state)
    {
        foreach (var entry in state.GetValues())
        {
            if (entry.Property.Name != OrientationPropertyName || entry.Value is not FrontAndTop orientation) continue;
            return orientation;
        }
        return null;
    }

    //FrontOf 取朝向组合的正面 对应原版 FrontAndTop.getFront 枚举名的前半段
    public static GameDirection FrontOf(FrontAndTop orientation) => orientation switch
    {
        FrontAndTop.down_east or FrontAndTop.down_north or FrontAndTop.down_south or FrontAndTop.down_west
            => GameDirection.Down,
        FrontAndTop.up_east or FrontAndTop.up_north or FrontAndTop.up_south or FrontAndTop.up_west
            => GameDirection.Up,
        FrontAndTop.west_up => GameDirection.West,
        FrontAndTop.east_up => GameDirection.East,
        FrontAndTop.north_up => GameDirection.North,
        _ => GameDirection.South,
    };

    //TopOf 取朝向组合的顶面 对应原版 FrontAndTop.getTop 枚举名的后半段
    public static GameDirection TopOf(FrontAndTop orientation) => orientation switch
    {
        FrontAndTop.west_up or FrontAndTop.east_up or FrontAndTop.north_up or FrontAndTop.south_up
            => GameDirection.Up,
        FrontAndTop.down_east or FrontAndTop.up_east => GameDirection.East,
        FrontAndTop.down_north or FrontAndTop.up_north => GameDirection.North,
        FrontAndTop.down_south or FrontAndTop.up_south => GameDirection.South,
        _ => GameDirection.West,
    };

    //FromFrontAndTop 由正面与顶面反查朝向组合 对应原版 FrontAndTop.fromFrontAndTop
    public static FrontAndTop FromFrontAndTop(GameDirection front, GameDirection top)
    {
        foreach (var candidate in Enum.GetValues<FrontAndTop>())
            if (FrontOf(candidate) == front && TopOf(candidate) == top) return candidate;
        throw new ArgumentException($"没有正面 {front} 顶面 {top} 的朝向组合");
    }
}
