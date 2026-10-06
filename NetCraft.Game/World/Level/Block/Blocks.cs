using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.World.Level.Block;

//Blocks 内置方块常量对应原版 net.minecraft.world.level.block.Blocks
//注册到 BuiltInRegistries.BLOCK 注册表
//真实方块按需扩展 其余用占位块对齐原版注册顺序
//网络区块 palette 用全局 BlockState id 客户端按原版顺序展开 因此状态数必须与原版一致
public static partial class Blocks
{
    //AirBlock 空气方块不可见不可碰撞不衰减光
    public sealed class AirBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("air");
        //空气不遮挡光线 遮挡形状按空处理
        public override bool CanOcclude => false;
        //空气本身没有形状 天光直接穿过 减光 0
        //默认实现看视觉形状 而本作 air 的视觉形状还没单独给过 会算成整块 这里显式掰回来
        public override bool PropagatesSkylightDown(BlockState state) => true;
        //空气可被任意放置行为替换
        public override bool CanBeReplaced => true;
        //空气即 isAir 地表规则判断空列的判据
        public override bool IsAir => true;
        //空气不参与碰撞
        public override bool HasCollision => false;
    }

    //StoneBlock 石头方块基础建筑材料
    public sealed class StoneBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("stone");
        //原版石头硬度 1.5 需要镐子 空手挖要 150 刻
        public override float DestroySpeed => 1.5f;
        public override bool RequiresCorrectToolForDrops => true;
    }

    //DirtBlock 泥土方块
    public sealed class DirtBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("dirt");
        //原版泥土硬度 0.5 空手可挖 15 刻
        public override float DestroySpeed => 0.5f;
    }

    //GrassBlock 草方块 snowy 属性占 2 个状态位对齐原版
    public sealed class GrassBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("grass_block");
        //原版草方块硬度 0.6 掉落走泥土
        public override float DestroySpeed => 0.6f;
        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["snowy"] = new BooleanProperty("snowy") };
    }

    //WaterBlock 水方块 level 0-15 共 16 个状态 0 为源
    //level 与流体状态的双向映射在 LiquidBlock 里 这里只留水自身的方块属性
    public sealed class WaterBlock : LiquidBlock
    {
        public WaterBlock() : base(Material.Fluids.Water) { }

        public override Identifier Id => Identifier.WithDefaultNamespace("water");
        //水不遮挡光线 遮挡形状按空处理 天光又透不过去 衰减落在每格 1
        public override bool CanOcclude => false;
        //水可被方块放置替换 原版流体 canBeReplaced 语义
        public override bool CanBeReplaced => true;
        //流体不参与碰撞 走进水里不改速度靠的是流体阻力
        public override bool HasCollision => false;
    }

    //LavaBlock 岩浆 level 0-15 共 16 个状态 0 为源
    public sealed class LavaBlock : LiquidBlock
    {
        public LavaBlock() : base(Material.Fluids.Lava) { }

        public override Identifier Id => Identifier.WithDefaultNamespace("lava");
        public override int LightEmission => 15;
        //岩浆不遮挡光线 同水按每格 1 衰减
        public override bool CanOcclude => false;
        //流体不参与碰撞
        public override bool HasCollision => false;
    }

    //BedrockBlock 基岩硬度 -1 不可破坏 对应原版 destroyTime(-1)
    public sealed class BedrockBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("bedrock");
        public override float DestroySpeed => -1f;
        public override bool RequiresCorrectToolForDrops => true;
    }

    //BlockSet 木料与金属档 门 活板门 栅栏门共用 决定能否徒手开与开合音效 对应原版 BlockSetType
    public enum BlockSet
    {
        Wood,
        Iron,
        Copper,
        Cherry,
        Bamboo,
        NetherWood,
    }

    //PlaceholderBlock 占位方块仅用于填充注册表位置对齐原版 block id 与状态数
    //挂哑属性展开与原版相同数量的状态真实方块未实现前不参与世界生成
    public sealed class PlaceholderBlock : BlockBehaviour
    {
        private readonly string _name;
        private readonly IDictionary<string, PropertyBase> _properties;

        public PlaceholderBlock(string name, params PropertyBase[] properties)
        {
            _name = name;
            _properties = properties.ToDictionary(p => p.Name);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);
        public override IDictionary<string, PropertyBase> Properties => _properties;
    }

    public static readonly AirBlock AIR = new();
    public static readonly StoneBlock STONE = new();
    public static readonly DirtBlock DIRT = new();
    public static readonly GrassBlock GRASS_BLOCK = new();
    public static readonly WaterBlock WATER = new();
    public static readonly LavaBlock LAVA = new();
    public static readonly BedrockBlock BEDROCK = new();

    //水的流体源态 回退成方块时是 level=0 的水方块
    //必须延迟到首次访问 静态字段初始化会抢在方块注册之前构建水方块状态打乱全局状态 id
    public static FluidState WaterFluidState => _waterFluidState ??= Material.Fluids.Water.DefaultFluidState;
    //岩浆的流体源态 回退成方块时是 level=0 的岩浆方块
    public static FluidState LavaFluidState => _lavaFluidState ??= Material.Fluids.Lava.DefaultFluidState;

    private static FluidState? _waterFluidState;
    private static FluidState? _lavaFluidState;

    //CaveAir 洞穴空气 雕刻挖出的洞用它区分于地表空气
    public static BlockBehaviour CaveAir => Lookup("cave_air");

    //Mycelium 菌丝方块 雕刻挖到它要连同下面的泥土一起重算顶面材质
    public static BlockBehaviour Mycelium => Lookup("mycelium");

    //Lookup 按注册名取已注册方块 方块表跑 Bootstrap 之前退回空气
    private static BlockBehaviour Lookup(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path)) as BlockBehaviour ?? AIR;

    //Bootstrap 按原版顺序注册全部方块
    //顺序与每个方块的状态数都对齐原版 26.2 客户端按同一套全局 BlockState id 解码 palette
    //有实现的那几个用真实类 其余按内嵌方块表生成占位方块 表见 BlockTable
    public static void Bootstrap()
    {
        var real = new Dictionary<string, BlockBehaviour>(StringComparer.Ordinal)
        {
            ["air"] = AIR,
            ["stone"] = STONE,
            ["dirt"] = DIRT,
            ["grass_block"] = GRASS_BLOCK,
            ["water"] = WATER,
            ["lava"] = LAVA,
            ["bedrock"] = BEDROCK
        };
        //红石元件数量多 单独一张表登记 见 Blocks.Redstone.cs
        RegisterRedstone(real);
        //活塞系列 见 Blocks.Piston.cs
        RegisterPiston(real);
        //发射器与投掷器 见 Blocks.Dispenser.cs
        RegisterDispenser(real);
        //绊线钩与绊线 见 Blocks.Tripwire.cs
        RegisterTripwire(real);
        //音符盒 见 Blocks.NoteBlock.cs
        RegisterNoteBlock(real);
        //铁轨四种 见 Blocks.Rail.cs
        RegisterRails(real);
        //活板门 见 Blocks.TrapDoor.cs
        RegisterTrapDoors(real);
        //栅栏门 见 Blocks.FenceGate.cs
        RegisterFenceGates(real);
        //门 见 Blocks.Door.cs
        RegisterDoors(real);
        //植被类方块 见 Blocks.Vegetation.cs
        RegisterVegetation(real);
        //作物类方块 见 Blocks.Crops.cs
        RegisterCrops(real);
        //台阶类方块 见 Blocks.Slabs.cs
        RegisterSlabs(real);
        //装饰与功能类方块 见 Blocks.Decoration.cs
        RegisterDecoration(real);
        //容器类方块 见 Blocks.Containers.cs
        RegisterContainers(real);
        //合成类方块 见 Blocks.Crafting.cs
        RegisterCrafting(real);
        //熔炼类方块 见 Blocks.Furnace.cs
        RegisterFurnaces(real);
        //表里扩展段是原版 registerDefaultState 给的默认状态覆盖 放置朝向类别与是否遮挡光线
        //真实方块类也一并注入 免得占位块与实现类两套默认状态各自为政
        foreach (var (id, properties, defaults, placement, canOcclude, pushReaction, conductor)
            in BlockTable.Load())
        {
            var block = real.TryGetValue(id.Path, out var implemented)
                ? implemented
                : new PlaceholderBlock(id.Path, properties);
            block.ApplyTableProperties(properties);
            block.ApplyTableDefaults(defaults);
            block.ApplyTablePlacement(PlacementKindExtensions.ParsePlacementKind(placement));
            block.ApplyTableOcclusion(canOcclude);
            block.ApplyTablePushReaction(pushReaction ?? NetCraft.Registry.Enums.PushReaction.normal);
            block.ApplyTableRedstoneConductor(conductor);
            //音符盒底座音色表 见 BlockInstruments
            block.ApplyInstrument(BlockInstruments.Lookup(id.Path));
            Register(block);
        }
    }

    //Register 注册方块到 BLOCK 注册表触发状态构建
    private static void Register(BlockBehaviour block)
    {
        //访问 DefaultBlockState 触发 BlockStateDefinition 构建
        _ = block.DefaultBlockState;
        Registry<NetCraft.Registry.Block>.Register(BuiltInRegistries.BLOCK, block.Id, block);
        //同步到容器工厂 区块落盘 codec 依赖默认方块 不注册时保存区块会抛异常
        PalettedContainerFactory.Default.RegisterBlock(block);
    }
}
