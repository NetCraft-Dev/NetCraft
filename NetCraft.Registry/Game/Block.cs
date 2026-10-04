using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;

namespace NetCraft.Registry;

//Block 抽象基类对应原版 net.minecraft.world.level.block.Block
//原版继承 BlockBehaviour 此处简化为抽象类持有 Id 与默认 BlockState
//子类按需重写 Id 与 DefaultBlockState 提供具体方块定义
public abstract class Block
{
    //Id 方块的注册名子类必须实现
    public abstract Identifier Id { get; }

    //DefaultBlockState 方块的默认状态子类必须实现
    public abstract BlockState DefaultBlockState { get; }

    //AllStates 方块全部可能状态按注册顺序排列
    //网络 palette 全局 id 需要全量状态默认仅默认状态由行为子类重写
    public virtual IReadOnlyList<BlockState> AllStates => new[] { DefaultBlockState };

    //LightEmission 方块自身发光等级 0-15
    public virtual int LightEmission => 0;

    //GetLightEmission 按状态算的发光等级 对应原版 lightLevel
    //默认取不随状态变的 LightEmission 红石灯这类点亮才发光的方块覆写它
    public virtual int GetLightEmission(BlockState state) => LightEmission;

    //CanOcclude 方块是否遮挡光线 对应原版 Properties.noOcclusion 设的 canOcclude
    //玻璃 铁栏杆 栅栏 门 台阶这类都关掉 关掉后方块位置上的遮挡形状按空处理
    public virtual bool CanOcclude => true;

    //UseShapeForLightOcclusion 遮挡形状是否跟着状态形状走 对应原版 useShapeForLightOcclusion
    //只有显式声明的方块为真 与 IsEmptyShape 一起决定要不要做面级遮挡比较
    public virtual bool UseShapeForLightOcclusion => false;

    //PushReaction 被活塞推动时的反应 对应原版 Properties.pushReaction 默认 normal
    //值由内嵌方块表的 push= 段注入 表里没写的就是 normal
    public virtual NetCraft.Registry.Enums.PushReaction PushReaction
        => NetCraft.Registry.Enums.PushReaction.normal;

    //GetOcclusionShape 遮挡判定用的形状 对应原版 getOcclusionShape
    //原版取 state.getShape(空世界视图) BlockBehaviour 会覆写成真实形状 这里的兜底只给不走它的实现
    public virtual VoxelShape GetOcclusionShape(BlockState state) => Shapes.Block();

    //SolidRender 遮挡形状是否占满整格 对应原版 solidRender
    public bool SolidRender(BlockState state)
        => IsShapeFullBlock(CanOcclude ? GetOcclusionShape(state) : Shapes.Empty());

    //GetLightDampening 该状态对光的衰减 对应原版 getLightDampening
    //整格实心扣满 15 天光能垂直穿透的扣 0 其余扣 1
    //原版不是按方块给常数而是按状态形状算 玻璃与台阶这类不完整形状的衰减靠它才对
    public virtual int GetLightDampening(BlockState state)
        => SolidRender(state) ? 15 : (PropagatesSkylightDown(state) ? 0 : 1);

    //PropagatesSkylightDown 天光能否垂直穿过该状态 对应原版 propagatesSkylightDown
    //原版默认看视觉形状是否占满整格并且该处没有流体
    public virtual bool PropagatesSkylightDown(BlockState state)
        => !IsShapeFullBlock(GetOcclusionShape(state)) && state.FluidState.IsEmpty;

    //IsAir 是否为空气方块 对应原版 BlockState.isAir 地表规则与放置判定靠它
    public virtual bool IsAir => false;

    //RandomTicks 是否参与随机刻 对应原版 Properties.randomTicks 默认关闭
    //放在基类是因为区段计数在 Storage 层算 那一层只认得到 Block
    public virtual bool RandomTicks => false;

    //Friction 方块表面摩擦系数 决定实体落在这格上时的水平阻力 对应原版 getFriction
    //默认 0.6 冰一类的滑面重写为 0.98
    public virtual float Friction => 0.6f;

    //GetFluidState 取该状态的流体状态 非流体方块返回空对应原版 getFluidState
    public virtual FluidState GetFluidState(BlockState state) => FluidState.Empty;

    //IsShapeFullBlock 形状是否占满整格 对应原版 isShapeFullBlock
    //原版对结果带 512 容量弱键缓存 形状实例基本被方块持有且反复使用 这里直接算
    public static bool IsShapeFullBlock(VoxelShape shape)
        => !Shapes.JoinIsNotEmpty(Shapes.Block(), shape, BooleanOps.NotSame);

    //IsFaceFull 形状的某一面是否占满整格面 对应原版 isFaceFull
    public static bool IsFaceFull(VoxelShape shape, Direction direction)
        => IsShapeFullBlock(shape.GetFaceShape(direction));

    //Box 按 1/16 像素坐标造盒 对应原版 Block.box
    public static VoxelShape Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        => Shapes.Box(minX / 16.0, minY / 16.0, minZ / 16.0, maxX / 16.0, maxY / 16.0, maxZ / 16.0);

    //Column 居中柱体 四参版可分别给两个水平尺寸 对应原版 Block.column
    public static VoxelShape Column(double sizeXZ, double minY, double maxY)
        => Column(sizeXZ, sizeXZ, minY, maxY);

    public static VoxelShape Column(double sizeX, double sizeZ, double minY, double maxY)
        => Box(8.0 - sizeX / 2.0, minY, 8.0 - sizeZ / 2.0, 8.0 + sizeX / 2.0, maxY, 8.0 + sizeZ / 2.0);

    //Cube 三轴居中且等高的方块 对应原版 Block.cube
    public static VoxelShape Cube(double size) => Cube(size, size, size);

    public static VoxelShape Cube(double sizeX, double sizeY, double sizeZ)
    {
        var halfY = sizeY / 2.0;
        return Column(sizeX, sizeZ, 8.0 - halfY, 8.0 + halfY);
    }

    //BoxZ 沿 Z 轴定两端 X 方向居中 对应原版 Block.boxZ
    public static VoxelShape BoxZ(double sizeXY, double minZ, double maxZ)
        => BoxZ(sizeXY, sizeXY, minZ, maxZ);

    public static VoxelShape BoxZ(double sizeX, double sizeY, double minZ, double maxZ)
    {
        var halfY = sizeY / 2.0;
        return BoxZ(sizeX, 8.0 - halfY, 8.0 + halfY, minZ, maxZ);
    }

    public static VoxelShape BoxZ(double sizeX, double minY, double maxY, double minZ, double maxZ)
        => Box(8.0 - sizeX / 2.0, minY, minZ, 8.0 + sizeX / 2.0, maxY, maxZ);

    //Boxes 按 0..endInclusive 的序号造一组形状 对应原版 Block.boxes
    //雪花层 作物 蜡烛这类每级形状都不同又无规律 逐级算出来存表
    public static VoxelShape[] Boxes(int endInclusive, Func<int, VoxelShape> factory)
    {
        var shapes = new VoxelShape[endInclusive + 1];
        for (var i = 0; i <= endInclusive; i++) shapes[i] = factory(i);
        return shapes;
    }
}
