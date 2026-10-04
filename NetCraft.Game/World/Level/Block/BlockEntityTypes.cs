using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//BlockEntityTypes 方块实体类型注册对应原版 net.minecraft.world.level.block.entity.BlockEntityTypes
//注册到 BuiltInRegistries.BLOCK_ENTITY_TYPE 注册表
//RawId 取自原版 BlockEntityTypes 静态字段声明序(furnace 起算) 客户端按序号分派故不能自行编号
//本作目前只有比较器一种方块实体 随容器等方块接入按需往下加
public static class BlockEntityTypes
{
    //FurnaceType 熔炉类型 原版 BlockEntityTypes 里 furnace 序号 0
    public sealed class FurnaceType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("furnace");
        public override int RawId => 0;
        public override BlockEntity Create(BlockPos pos) => new FurnaceBlockEntity(pos);
    }

    //DispenserType 发射器类型 原版 BlockEntityTypes 里 dispenser 序号 5
    public sealed class DispenserType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("dispenser");
        public override int RawId => 5;
        public override BlockEntity Create(BlockPos pos) => new DispenserBlockEntity(pos);
    }

    //DropperType 投掷器类型 原版 BlockEntityTypes 里 dropper 序号 6
    public sealed class DropperType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("dropper");
        public override int RawId => 6;
        public override BlockEntity Create(BlockPos pos) => new DropperBlockEntity(pos);
    }

    //ComparatorType 比较器类型 原版 BlockEntityTypes 里 comparator 序号 19
    public sealed class ComparatorType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("comparator");
        public override int RawId => 19;
        public override BlockEntity Create(BlockPos pos) => new ComparatorBlockEntity(pos);
    }

    //PistonMovingType 移动活塞类型 原版 BlockEntityTypes 里 piston 序号 11
    public sealed class PistonMovingType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("piston");
        public override int RawId => 11;
        public override BlockEntity Create(BlockPos pos) => new Piston.PistonMovingBlockEntity(pos);
    }

    //ChestType 箱子类型 原版 BlockEntityTypes 里 chest 序号 1
    public sealed class ChestType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("chest");
        public override int RawId => 1;
        public override BlockEntity Create(BlockPos pos) => new ChestBlockEntity(pos);
    }

    //BarrelType 木桶类型 原版 BlockEntityTypes 里 barrel 序号 26
    public sealed class BarrelType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("barrel");
        public override int RawId => 26;
        public override BlockEntity Create(BlockPos pos) => new BarrelBlockEntity(pos);
    }

    //SmokerType 烟熏炉类型 原版 BlockEntityTypes 里 smoker 序号 27
    public sealed class SmokerType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("smoker");
        public override int RawId => 27;
        public override BlockEntity Create(BlockPos pos) => new SmokerBlockEntity(pos);
    }

    //BlastFurnaceType 高炉类型 原版 BlockEntityTypes 里 blast_furnace 序号 28
    public sealed class BlastFurnaceType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("blast_furnace");
        public override int RawId => 28;
        public override BlockEntity Create(BlockPos pos) => new BlastFurnaceBlockEntity(pos);
    }

    //CampfireType 营火类型 原版 BlockEntityTypes 里 campfire 序号 32
    public sealed class CampfireType : BlockEntityType
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("campfire");
        public override int RawId => 32;
        public override BlockEntity Create(BlockPos pos) => new CampfireBlockEntity(pos);
    }

    public static readonly FurnaceType FURNACE = new();

    public static readonly ComparatorType COMPARATOR = new();

    public static readonly ChestType CHEST = new();

    public static readonly BarrelType BARREL = new();

    public static readonly SmokerType SMOKER = new();

    public static readonly BlastFurnaceType BLAST_FURNACE = new();

    public static readonly CampfireType CAMPFIRE = new();

    public static readonly PistonMovingType PISTON = new();

    public static readonly DispenserType DISPENSER = new();

    public static readonly DropperType DROPPER = new();

    //ByRawId 网络序号反查表 供同步包解码与客户端分派
    private static readonly Dictionary<int, BlockEntityType> ByRawId = new();

    //KeyToType 注册表键反查表 供存档 id 字段还原
    private static readonly Dictionary<string, BlockEntityType> KeyToType = new();

    //Bootstrap 注册全部内置方块实体类型 必须在注册表冻结之前调
    public static void Bootstrap()
    {
        Register(FURNACE);
        Register(COMPARATOR);
        Register(CHEST);
        Register(BARREL);
        Register(SMOKER);
        Register(BLAST_FURNACE);
        Register(CAMPFIRE);
        Register(PISTON);
        Register(DISPENSER);
        Register(DROPPER);
    }

    //ById 按网络序号取类型 未注册返回 null
    public static BlockEntityType? ById(int rawId)
        => ByRawId.TryGetValue(rawId, out var type) ? type : null;

    //ByKey 按注册表键取类型 未注册返回 null
    public static BlockEntityType? ByKey(string key)
        => KeyToType.TryGetValue(key, out var type) ? type : null;

    //Load 按存档标签还原方块实体
    //缺 id 或 id 未注册返回 null 由调用方决定是跳过还是报错
    public static BlockEntity? Load(CompoundTag tag)
    {
        if (tag.GetString("id")?.Value is not { } key) return null;
        if (!KeyToType.TryGetValue(key, out var type)) return null;
        var pos = new BlockPos(tag.GetIntOr("x", 0), tag.GetIntOr("y", 0), tag.GetIntOr("z", 0));
        var entity = type.Create(pos);
        entity.LoadCustomOnly(tag);
        return entity;
    }

    //Register 写注册表并登记两张反查表
    private static void Register(BlockEntityType type)
    {
        Registry<BlockEntityType<object>>.Register(BuiltInRegistries.BLOCK_ENTITY_TYPE, type.Id, type);
        ByRawId[type.RawId] = type;
        KeyToType[type.Id.ToString()] = type;
    }
}
