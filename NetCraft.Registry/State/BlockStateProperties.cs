using NetCraft.Registry.Enums;

namespace NetCraft.Registry.State;

//BlockStateProperties 常用状态属性常量集 对应原版同名类
//属性名与值序必须与 blocks.txt 给占位块生成的那一份完全一致 否则全局 BlockState id 会错位
//值序取原版实际迭代序 原版 HORIZONTAL_FACING 传 Direction.Plane.HORIZONTAL 那个集合在 26.2 按枚举序号迭代
//所以水平四向是 north south west east 而不是 NORTH EAST SOUTH WEST 显式给六个方向的属性仍按调用顺序
public static class BlockStateProperties
{
    //水平朝向 拉杆 按钮 中继器 比较器与墙火把只认水平方向
    public static readonly EnumProperty<Direction> HorizontalFacing = new("facing", new[]
    {
        Direction.north, Direction.south, Direction.west, Direction.east,
    });

    //附着面 拉杆与按钮用 floor/wall/ceiling 对应原版 ATTACH_FACE
    public static readonly EnumProperty<AttachFace> AttachFaceProperty = new("face", new[]
    {
        AttachFace.floor, AttachFace.wall, AttachFace.ceiling,
    });

    //是否处于通电态
    public static readonly BooleanProperty Powered = new("powered");

    //信号强度 0-15 测重压力板与红石线用
    public static readonly IntegerProperty Power = new("power", 0, 15);

    //是否点亮 红石火把用
    public static readonly BooleanProperty Lit = new("lit");

    //绊线钩与绊线是否已连成一条 对应原版 ATTACHED
    public static readonly BooleanProperty Attached = new("attached");

    //绊线是否已被剪断 剪断后踩上去不再触发 对应原版 DISARMED
    public static readonly BooleanProperty Disarmed = new("disarmed");

    //阳光探测器是否反向输出 反向时夜里出信号白天不出 对应原版 INVERTED
    public static readonly BooleanProperty Inverted = new("inverted");

    //音符盒音高 0-24 共两个八度 对应原版 NOTE
    public static readonly IntegerProperty Note = new("note", 0, 24);

    //音符盒乐器 值序照 blocks.txt 与 NoteBlockInstrument 的声明序
    public static readonly EnumProperty<NoteBlockInstrument> NoteBlockInstrumentProperty =
        new("instrument", new[]
        {
            NoteBlockInstrument.harp, NoteBlockInstrument.basedrum, NoteBlockInstrument.snare,
            NoteBlockInstrument.hat, NoteBlockInstrument.bass, NoteBlockInstrument.flute,
            NoteBlockInstrument.bell, NoteBlockInstrument.guitar, NoteBlockInstrument.chime,
            NoteBlockInstrument.xylophone, NoteBlockInstrument.iron_xylophone, NoteBlockInstrument.cow_bell,
            NoteBlockInstrument.didgeridoo, NoteBlockInstrument.bit, NoteBlockInstrument.banjo,
            NoteBlockInstrument.pling, NoteBlockInstrument.trumpet, NoteBlockInstrument.trumpet_exposed,
            NoteBlockInstrument.trumpet_oxidized, NoteBlockInstrument.trumpet_weathered,
            NoteBlockInstrument.zombie, NoteBlockInstrument.skeleton, NoteBlockInstrument.creeper,
            NoteBlockInstrument.dragon, NoteBlockInstrument.wither_skeleton, NoteBlockInstrument.piglin,
            NoteBlockInstrument.custom_head,
        });

    //绊线四向是否与相邻的绊线或绊线钩相连 对应原版 NORTH/EAST/SOUTH/WEST
    //红石线也有名为 north 的属性但那是三态枚举 名字相同类型不同 各自独立
    public static readonly BooleanProperty NorthConnected = new("north");
    public static readonly BooleanProperty EastConnected = new("east");
    public static readonly BooleanProperty SouthConnected = new("south");
    public static readonly BooleanProperty WestConnected = new("west");

    //铁轨形状 直道六种 值序照 blocks.txt 的 north_south..ascending_south
    //普通铁轨额外带四个弯道 对应原版 RAIL_SHAPE_STRAIGHT 与 RAIL_SHAPE
    public static readonly EnumProperty<RailShape> RailShapeStraight = new("shape", new[]
    {
        RailShape.north_south, RailShape.east_west, RailShape.ascending_east,
        RailShape.ascending_west, RailShape.ascending_north, RailShape.ascending_south,
    });

    public static readonly EnumProperty<RailShape> RailShapeAll = new("shape", new[]
    {
        RailShape.north_south, RailShape.east_west, RailShape.ascending_east,
        RailShape.ascending_west, RailShape.ascending_north, RailShape.ascending_south,
        RailShape.south_east, RailShape.south_west, RailShape.north_west, RailShape.north_east,
    });

    //中继器延时档位 1-4 档 每档 2 刻 对应原版 DELAY
    public static readonly IntegerProperty Delay = new("delay", 1, 4);

    //中继器是否被侧向输入锁住 锁住期间不翻转 对应原版 LOCKED
    public static readonly BooleanProperty Locked = new("locked");

    //比较器工作模式 值序照 blocks.txt 的 compare,subtract
    public static readonly EnumProperty<ComparatorMode> ComparatorModeProperty = new("mode", new[]
    {
        ComparatorMode.compare, ComparatorMode.subtract,
    });

    //红石线四向连接状态 值序照 blocks.txt 的 up,side,none
    //四个方向各一个属性实例 红石线的状态数就是 3^4 乘上 16 档功率
    //值数组必须声明在四个属性之前 静态字段按文本序初始化
    private static readonly RedstoneSide[] RedstoneSideValues =
    {
        RedstoneSide.up, RedstoneSide.side, RedstoneSide.none,
    };

    public static readonly EnumProperty<RedstoneSide> NorthRedstone = new("north", RedstoneSideValues);
    public static readonly EnumProperty<RedstoneSide> EastRedstone = new("east", RedstoneSideValues);
    public static readonly EnumProperty<RedstoneSide> SouthRedstone = new("south", RedstoneSideValues);
    public static readonly EnumProperty<RedstoneSide> WestRedstone = new("west", RedstoneSideValues);

    //六向朝向 观察者这类带 UP/DOWN 的方块用 值序照 blocks.txt 的 north,east,south,west,up,down
    //注意与四向的 HorizontalFacing 不是同一个实例 不能互换
    public static readonly EnumProperty<Direction> FacingProperty = new("facing", new[]
    {
        Direction.north, Direction.east, Direction.south, Direction.west,
        Direction.up, Direction.down,
    });

    //沿轴 原木与柱状方块用 值序照 blocks.txt 的 x,y,z
    public static readonly EnumProperty<Axis> AxisProperty = new("axis", new[]
    {
        Axis.x, Axis.y, Axis.z,
    });

    //tip 垂挂类方块的末端段标记 垂丝苔藓这类靠它区分末端形状 对应原版 TIP
    public static readonly BooleanProperty Tip = new("tip");

    //eye 末地传送门框架是否已插眼 对应原版 EYE
    public static readonly BooleanProperty Eye = new("eye");

    //candles 蜡烛方块插的支数 1-4 对应原版 CANDLES
    public static readonly IntegerProperty Candles = new("candles", 1, 4);

    //hanging 灯笼一类是否吊在方块下方 对应原版 HANGING
    public static readonly BooleanProperty Hanging = new("hanging");

    //layers 雪的厚度 1-8 层 对应原版 LAYERS
    public static readonly IntegerProperty Layers = new("layers", 1, 8);

    //bites 蛋糕被咬掉的份数 0-6 对应原版 BITES
    public static readonly IntegerProperty Bites = new("bites", 0, 6);

    //age 作物成熟度 各作物值域不同 同名同类型在各自方块上唯一 不会串
    public static readonly IntegerProperty Age1 = new("age", 0, 1);
    public static readonly IntegerProperty Age3 = new("age", 0, 3);
    public static readonly IntegerProperty Age7 = new("age", 0, 7);

    //hatch 海龟蛋孵化进度 0-2 对应原版 HATCH
    public static readonly IntegerProperty Hatch = new("hatch", 0, 2);

    //eggs 海龟蛋个数 1-4 对应原版 EGGS
    public static readonly IntegerProperty Eggs = new("eggs", 1, 4);

    //level 光源方块亮度 0-15 对应原版 LightBlock 的 LEVEL
    public static readonly IntegerProperty Level15 = new("level", 0, 15);

    //level 堆肥桶装填等级 0-8 第八级即满 对应原版 COMPOSTER 的 LEVEL
    public static readonly IntegerProperty LevelComposter = new("level", 0, 8);

    //distance 脚手架到支撑点的距离 0-7 越小越稳 对应原版 STABILITY_DISTANCE
    public static readonly IntegerProperty StabilityDistance = new("distance", 0, 7);

    //bottom 脚手架是否托在下方方块上 对应原版 BOTTOM
    public static readonly BooleanProperty Bottom = new("bottom");

    //stage 竹子生长阶段 0-1 对应原版 STAGE
    public static readonly IntegerProperty Stage = new("stage", 0, 1);

    //leaves 竹叶形态 值序照 blocks.txt 的 none,small,large
    public static readonly EnumProperty<BambooLeaves> BambooLeavesProperty = new("leaves", new[]
    {
        BambooLeaves.none, BambooLeaves.small, BambooLeaves.large,
    });

    //short 活塞头是否处于收缩态 对应原版 SHORT
    public static readonly BooleanProperty Short = new("short");

    //extended 活塞是否已伸出 对应原版 EXTENDED
    public static readonly BooleanProperty Extended = new("extended");

    //triggered 发射器与投掷器是否已被信号触发 对应原版 TRIGGERED
    //上升沿置真并排调度刻 下降沿清掉 标记用来避免同一次通电重复发射
    public static readonly BooleanProperty Triggered = new("triggered");

    //type 活塞头归属普通还是粘性 值序照 blocks.txt 的 normal,sticky
    public static readonly EnumProperty<PistonType> PistonTypeProperty = new("type", new[]
    {
        PistonType.normal, PistonType.sticky,
    });

    //waterlogged 方块格内是否含水 对应原版 WATERLOGGED
    public static readonly BooleanProperty Waterlogged = new("waterlogged");

    //open 门 活板门 栅栏门是否已打开 对应原版 OPEN
    public static readonly BooleanProperty Open = new("open");

    //half 活板门分上下半 值序照 blocks.txt 的 top,bottom 对应原版 HALF
    //命名空间段 Enums 的 Half 与 System.Half 同名 这里写完全限定名
    public static readonly EnumProperty<NetCraft.Registry.Enums.Half> HalfProperty = new("half", new[]
    {
        NetCraft.Registry.Enums.Half.top, NetCraft.Registry.Enums.Half.bottom,
    });

    //in_wall 栅栏门两侧是否夹在墙里 夹住时门板会压低 对应原版 IN_WALL
    public static readonly BooleanProperty InWall = new("in_wall");

    //half 门分上下两格 值序照 blocks.txt 的 upper,lower 对应原版 DOUBLE_BLOCK_HALF
    public static readonly EnumProperty<DoubleBlockHalf> DoubleBlockHalfProperty = new("half", new[]
    {
        DoubleBlockHalf.upper, DoubleBlockHalf.lower,
    });

    //hinge 门的合页在哪一侧 值序照 blocks.txt 的 left,right 对应原版 DOOR_HINGE
    public static readonly EnumProperty<DoorHingeSide> DoorHinge = new("hinge", new[]
    {
        DoorHingeSide.left, DoorHingeSide.right,
    });

    //pickles 海泡菜一格里长了几颗 1-4 对应原版 PICKLES
    public static readonly IntegerProperty Pickles = new("pickles", 1, 4);

    //type 台阶的形态 值序照 blocks.txt 的 top,bottom,double
    public static readonly EnumProperty<SlabType> SlabTypeProperty = new("type", new[]
    {
        SlabType.top, SlabType.bottom, SlabType.@double,
    });

    //type 箱子形态 单人箱与双箱的左右半 值序照 blocks.txt 的 single,left,right
    public static readonly EnumProperty<ChestType> ChestTypeProperty = new("type", new[]
    {
        ChestType.single, ChestType.left, ChestType.right,
    });
}

//RedstoneSideExtensions 红石线连接判定的辅助 对应原版 RedstoneSide.isConnected
public static class RedstoneSideExtensions
{
    //IsConnected 只要不是 none 就算连上了
    public static bool IsConnected(this RedstoneSide side) => side != RedstoneSide.none;
}
