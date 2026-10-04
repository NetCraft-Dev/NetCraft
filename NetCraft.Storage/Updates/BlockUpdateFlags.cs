using NetCraft.Primitives;

namespace NetCraft.Storage.Updates;

//BlockUpdateFlags setBlock 的副作用开关 对应原版 Block.UPDATE_*
//原版没有"严格模式提前返回" 置位只让对应判定点落空 方法照样走完全程
public static class BlockUpdateFlags
{
    //变更后通知周围方块 触发邻居更新通道
    public const int Neighbours = 1;

    //变更后同步客户端
    public const int Clients = 2;

    //客户端侧抑制置脏
    public const int Invisible = 4;

    //立即置脏 26.2 服务端已无读取方 保留常量只为零值语义与原版一致
    public const int Immediate = 8;

    //调用方声明新形状已知 跳过形状更新通道
    public const int KnownShape = 16;

    //抑制掉落 只在 updateOrDestroy 里生效
    public const int SuppressDrops = 32;

    //被活塞推动 对应原版 movedByPiston 标志
    public const int MoveByPiston = 64;

    //形状更新遇到红石线时整个跳过
    public const int SkipShapeUpdateOnWire = 128;

    //跳过旧方块实体的移除副作用
    public const int SkipBlockEntitySideEffects = 256;

    //跳过 onPlace
    public const int SkipOnPlace = 512;

    //全部副作用关闭 对应原版 816 一次关掉降级 掉落 已知形状 四个判定点
    public const int SkipAllSideEffects =
        SkipBlockEntitySideEffects | SkipOnPlace | SuppressDrops | KnownShape;

    //不通知客户端且跳过方块实体副作用 对应原版 260
    public const int None = SkipBlockEntitySideEffects | Invisible;

    //邻居加客户端 对应原版 3 普通放置最常用
    public const int All = Neighbours | Clients;

    //UpdateLimitDefault 链式传播深度默认值 对应原版三参重载里补的 512
    public const int UpdateLimitDefault = 512;

    //NeighbourUpdateOrder 邻居更新遍历顺序 对应原版 NeighborUpdater.UPDATE_ORDER
    //顺序本身参与红石时序 不能改
    public static readonly Direction[] NeighbourUpdateOrder =
    {
        Direction.West, Direction.East, Direction.Down, Direction.Up, Direction.North, Direction.South,
    };

    //ShapeUpdateOrder 形状更新遍历顺序 对应原版 BlockBehaviour.UPDATE_SHAPE_ORDER
    //与邻居顺序不同 两套不能混用
    public static readonly Direction[] ShapeUpdateOrder =
    {
        Direction.West, Direction.East, Direction.North, Direction.South, Direction.Down, Direction.Up,
    };
}
