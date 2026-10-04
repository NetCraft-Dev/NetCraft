using NetCraft.Primitives;
using StateDirection = NetCraft.Registry.Enums.Direction;

namespace NetCraft.Registry;

//DirectionConversions 状态里的朝向枚举与世界坐标方向互转
//方块属性存的是枚举形式 世界坐标运算要用 Primitives.Direction 两套类型得来回换
//放 Registry 根命名空间 免得方块文件同时 using 两个含 Direction 的命名空间时撞名
public static class DirectionConversions
{
    //ToPrimitive 枚举朝向换算成世界方向
    public static Direction ToPrimitive(this StateDirection direction) => direction switch
    {
        StateDirection.down => Direction.Down,
        StateDirection.up => Direction.Up,
        StateDirection.north => Direction.North,
        StateDirection.south => Direction.South,
        StateDirection.west => Direction.West,
        _ => Direction.East,
    };

    //ToState 世界方向换算成枚举朝向
    public static StateDirection ToState(this Direction direction) => direction.Id3D switch
    {
        Direction.DownId => StateDirection.down,
        Direction.UpId => StateDirection.up,
        Direction.NorthId => StateDirection.north,
        Direction.SouthId => StateDirection.south,
        Direction.WestId => StateDirection.west,
        _ => StateDirection.east,
    };
}
