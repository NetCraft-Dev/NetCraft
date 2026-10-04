using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Game.Server;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Game.World.Phys.Collision;
using Direction = NetCraft.Primitives.Direction;
//命名空间段 Enums 的 Half 与 System.Half 同名 用别名区分
using Half = NetCraft.Registry.Enums.Half;

namespace NetCraft.Game.World.Level.Block;

//活板门 对应原版 net.minecraft.world.level.block.TrapDoorBlock
//手动开合看材质能否徒手开 红石按信号开合 点到侧面时按点击高度定上下半
//原版的含水回流调度本作没有流体刻 略去
public static partial class Blocks
{
    public static readonly TrapDoorBlock OAK_TRAPDOOR = new("oak_trapdoor", BlockSet.Wood);
    public static readonly TrapDoorBlock IRON_TRAPDOOR = new("iron_trapdoor", BlockSet.Iron);
    public static readonly TrapDoorBlock COPPER_TRAPDOOR = new("copper_trapdoor", BlockSet.Copper);

    //RegisterTrapDoors 活板门登记进真实方块表 注册名到材质档照原版 Blocks.java
    private static void RegisterTrapDoors(Dictionary<string, BlockBehaviour> real)
    {
        RegisterTrapDoor(real, OAK_TRAPDOOR);
        RegisterTrapDoor(real, new TrapDoorBlock("spruce_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("birch_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("jungle_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("acacia_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("dark_oak_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("pale_oak_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("mangrove_trapdoor", BlockSet.Wood));
        RegisterTrapDoor(real, new TrapDoorBlock("cherry_trapdoor", BlockSet.Cherry));
        RegisterTrapDoor(real, new TrapDoorBlock("bamboo_trapdoor", BlockSet.Bamboo));
        RegisterTrapDoor(real, new TrapDoorBlock("crimson_trapdoor", BlockSet.NetherWood));
        RegisterTrapDoor(real, new TrapDoorBlock("warped_trapdoor", BlockSet.NetherWood));
        RegisterTrapDoor(real, IRON_TRAPDOOR);
        RegisterTrapDoor(real, COPPER_TRAPDOOR);
        RegisterTrapDoor(real, new TrapDoorBlock("exposed_copper_trapdoor", BlockSet.Copper));
        RegisterTrapDoor(real, new TrapDoorBlock("weathered_copper_trapdoor", BlockSet.Copper));
        RegisterTrapDoor(real, new TrapDoorBlock("oxidized_copper_trapdoor", BlockSet.Copper));
        RegisterTrapDoor(real, new TrapDoorBlock("waxed_copper_trapdoor", BlockSet.Copper));
        RegisterTrapDoor(real, new TrapDoorBlock("waxed_exposed_copper_trapdoor", BlockSet.Copper));
        RegisterTrapDoor(real, new TrapDoorBlock("waxed_weathered_copper_trapdoor", BlockSet.Copper));
        RegisterTrapDoor(real, new TrapDoorBlock("waxed_oxidized_copper_trapdoor", BlockSet.Copper));
    }

    private static void RegisterTrapDoor(Dictionary<string, BlockBehaviour> real, TrapDoorBlock block)
        => real[block.Id.Path] = block;

    public sealed class TrapDoorBlock : BlockBehaviour
    {
        //TrapDoorShapes 贴北面那条三像素厚的板转出的六向形状 对应原版 TrapDoorBlock.SHAPES
        private static readonly Dictionary<Direction, VoxelShape> TrapDoorShapes =
            NetCraft.Primitives.Phys.Shapes.RotateAll(NetCraft.Registry.Block.BoxZ(16.0, 13.0, 16.0));

        private readonly string _name;
        private readonly BlockSet _material;
        private readonly bool _canOpenByHand;
        private readonly SoundEvent _openSound;
        private readonly SoundEvent _closeSound;

        public TrapDoorBlock(string name, BlockSet material)
        {
            _name = name;
            _material = material;
            //铁活板门不能徒手开 其余材质都可以 对应原版 BlockSetType.canOpenByHand
            _canOpenByHand = material != BlockSet.Iron;
            (_openSound, _closeSound) = SoundsOf(material);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //木质活板门硬度 3 铁质 5 对应原版二者 strength
        public override float DestroySpeed => _material == BlockSet.Iron ? 5f : 3f;

        //木质空手可挖 铁与铜要正确工具
        public override bool RequiresCorrectToolForDrops
            => _material is BlockSet.Iron or BlockSet.Copper;

        //Properties 状态按 blocks.txt 的 facing|half|open|powered|waterlogged 走
        //表里那份是另建的属性实例 GetValue 取不到 必须自己声明 顺序变了全局状态 id 会错位
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["half"] = BlockStateProperties.HalfProperty,
            ["open"] = BlockStateProperties.Open,
            ["powered"] = BlockStateProperties.Powered,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        //GetShape 开着时板子立向朝向那面 关着时按上下半贴顶或贴底 对应原版 getShape
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
        {
            var key = state.GetValue(BlockStateProperties.Open)
                ? ToGeometry(state.GetValue(BlockStateProperties.HorizontalFacing))
                : state.GetValue(BlockStateProperties.HalfProperty) == Half.top ? Direction.Down : Direction.Up;
            return TrapDoorShapes[key];
        }

        //GetStateForPlacement 点到侧面时按点击高度定上下半 否则按点击面贴顶或贴底
        //落位时旁边已有信号则直接落成打开且通电 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection, Vec3 hitLocal)
        {
            var state = face.IsHorizontal
                ? DefaultBlockState
                    .SetValue(BlockStateProperties.HorizontalFacing, ToPropertyFacing(face))
                    .SetValue(BlockStateProperties.HalfProperty, hitLocal.Y > 0.5 ? Half.top : Half.bottom)
                : DefaultBlockState
                    .SetValue(BlockStateProperties.HorizontalFacing, ToPropertyFacing(horizontalFacing.Opposite))
                    .SetValue(BlockStateProperties.HalfProperty, face == Direction.Up ? Half.bottom : Half.top);
            if (!level.HasNeighborSignal(pos)) return state;
            return state.SetValue(BlockStateProperties.Open, true).SetValue(BlockStateProperties.Powered, true);
        }

        //UseOn 徒手开合 打不开的材质直接不受理 对应原版 useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (!_canOpenByHand) return false;
            Toggle(level, pos, state, player);
            return true;
        }

        //NeighborChanged 信号翻转时跟着开关并按新信号记通电态 对应原版 neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var signal = level.HasNeighborSignal(pos);
            if (signal == state.GetValue(BlockStateProperties.Powered)) return;
            if (state.GetValue(BlockStateProperties.Open) != signal)
            {
                state = state.SetValue(BlockStateProperties.Open, signal);
                PlaySound(level, pos, signal, null);
            }
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, signal),
                BlockUpdateFlags.Clients);
        }

        //Toggle 翻一下开合位并出声 对应原版 toggle
        private void Toggle(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer? player)
        {
            var updated = state.Cycle(BlockStateProperties.Open);
            level.SetBlock(pos, updated, BlockUpdateFlags.Clients);
            PlaySound(level, pos, updated.GetValue(BlockStateProperties.Open), player);
        }

        //PlaySound 开合音效 音高在 0.9 到 1.0 之间抖动 对应原版 playSound
        private void PlaySound(ServerLevel level, BlockPos pos, bool opening, ServerPlayer? player)
        {
            var sound = opening ? _openSound : _closeSound;
            level.PlaySound(sound, SoundSource.Blocks, pos, 1f, Random.Shared.NextSingle() * 0.1f + 0.9f);
        }

        //SoundsOf 材质档对应的开合音效 对应原版 BlockSetType 各档的 trapdoorOpen/trapdoorClose
        private static (SoundEvent Open, SoundEvent Close) SoundsOf(BlockSet material) => material switch
        {
            BlockSet.Iron => (SoundEvents.IronTrapdoorOpen, SoundEvents.IronTrapdoorClose),
            BlockSet.Copper => (SoundEvents.CopperTrapdoorOpen, SoundEvents.CopperTrapdoorClose),
            BlockSet.Cherry => (SoundEvents.CherryWoodTrapdoorOpen, SoundEvents.CherryWoodTrapdoorClose),
            BlockSet.Bamboo => (SoundEvents.BambooWoodTrapdoorOpen, SoundEvents.BambooWoodTrapdoorClose),
            BlockSet.NetherWood =>
                (SoundEvents.NetherWoodTrapdoorOpen, SoundEvents.NetherWoodTrapdoorClose),
            _ => (SoundEvents.WoodenTrapdoorOpen, SoundEvents.WoodenTrapdoorClose),
        };

        //ToPropertyFacing 几何方向折成方块属性用的枚举成员 活板门只会用到水平四个
        private static NetCraft.Registry.Enums.Direction ToPropertyFacing(Direction direction)
        {
            if (direction == Direction.North) return NetCraft.Registry.Enums.Direction.north;
            if (direction == Direction.South) return NetCraft.Registry.Enums.Direction.south;
            if (direction == Direction.West) return NetCraft.Registry.Enums.Direction.west;
            return NetCraft.Registry.Enums.Direction.east;
        }

        //ToGeometry 属性枚举折回几何方向 只用来查形状表
        private static Direction ToGeometry(NetCraft.Registry.Enums.Direction direction) => direction switch
        {
            NetCraft.Registry.Enums.Direction.north => Direction.North,
            NetCraft.Registry.Enums.Direction.south => Direction.South,
            NetCraft.Registry.Enums.Direction.west => Direction.West,
            _ => Direction.East,
        };
    }
}
