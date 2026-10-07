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
//The Enums namespace segment Half clashes with System.Half, an alias distinguishes them
using Half = NetCraft.Registry.Enums.Half;

namespace NetCraft.Game.World.Level.Block;

//Trapdoors, maps to vanilla net.minecraft.world.level.block.TrapDoorBlock
//Manual open and close depends on whether the material allows bare-handed use, redstone opens and closes by signal and clicking a side picks the half from the click height
//The vanilla waterlogged reflow scheduling is omitted since this project has no fluid ticks
public static partial class Blocks
{
    public static readonly TrapDoorBlock OAK_TRAPDOOR = new("oak_trapdoor", BlockSet.Wood);
    public static readonly TrapDoorBlock IRON_TRAPDOOR = new("iron_trapdoor", BlockSet.Iron);
    public static readonly TrapDoorBlock COPPER_TRAPDOOR = new("copper_trapdoor", BlockSet.Copper);

    //RegisterTrapDoors registers trapdoors into the real block table, registry name to material tier follows vanilla Blocks.java
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
        //TrapDoorShapes the six-direction shapes rotated from a three-pixel-thick panel on the north face, maps to vanilla TrapDoorBlock.SHAPES
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
            //Iron trapdoors cannot be opened bare-handed while the other tiers can, maps to vanilla BlockSetType.canOpenByHand
            _canOpenByHand = material != BlockSet.Iron;
            (_openSound, _closeSound) = SoundsOf(material);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //Wooden trapdoor hardness 3 and iron 5, maps to the strength of each in vanilla
        public override float DestroySpeed => _material == BlockSet.Iron ? 5f : 3f;

        //Wood is mineable bare-handed, iron and copper need the correct tool
        public override bool RequiresCorrectToolForDrops
            => _material is BlockSet.Iron or BlockSet.Copper;

        //Properties states follow facing|half|open|powered|waterlogged in blocks.txt
        //The one in the table is a separately built property instance that GetValue cannot find; it must be declared here, and changing the order shifts the global state ids
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["half"] = BlockStateProperties.HalfProperty,
            ["open"] = BlockStateProperties.Open,
            ["powered"] = BlockStateProperties.Powered,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        //GetShape the panel stands toward the facing when open and hugs the top or bottom per the half when closed, maps to vanilla getShape
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
        {
            var key = state.GetValue(BlockStateProperties.Open)
                ? ToGeometry(state.GetValue(BlockStateProperties.HorizontalFacing))
                : state.GetValue(BlockStateProperties.HalfProperty) == Half.top ? Direction.Down : Direction.Up;
            return TrapDoorShapes[key];
        }

        //GetStateForPlacement the half comes from the click height when clicking a side, otherwise the top or bottom follows the clicked face
        //An existing signal beside places it already open and powered, maps to vanilla getStateForPlacement
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

        //UseOn bare-hand open and close, tiers that cannot be opened are rejected outright, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (!_canOpenByHand) return false;
            Toggle(level, pos, state, player);
            return true;
        }

        //NeighborChanged opens and closes on a signal flip and records the powered state from the new signal, maps to vanilla neighborChanged
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

        //Toggle flips the open state and plays the sound, maps to vanilla toggle
        private void Toggle(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer? player)
        {
            var updated = state.Cycle(BlockStateProperties.Open);
            level.SetBlock(pos, updated, BlockUpdateFlags.Clients);
            PlaySound(level, pos, updated.GetValue(BlockStateProperties.Open), player);
        }

        //PlaySound open and close sound, the pitch jitters between 0.9 and 1.0, maps to vanilla playSound
        private void PlaySound(ServerLevel level, BlockPos pos, bool opening, ServerPlayer? player)
        {
            var sound = opening ? _openSound : _closeSound;
            level.PlaySound(sound, SoundSource.Blocks, pos, 1f, Random.Shared.NextSingle() * 0.1f + 0.9f);
        }

        //SoundsOf the open and close sounds for a material tier, maps to trapdoorOpen/trapdoorClose of each vanilla BlockSetType tier
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

        //ToPropertyFacing converts a geometry direction into the enum member used by block properties, trapdoors only use the four horizontal ones
        private static NetCraft.Registry.Enums.Direction ToPropertyFacing(Direction direction)
        {
            if (direction == Direction.North) return NetCraft.Registry.Enums.Direction.north;
            if (direction == Direction.South) return NetCraft.Registry.Enums.Direction.south;
            if (direction == Direction.West) return NetCraft.Registry.Enums.Direction.west;
            return NetCraft.Registry.Enums.Direction.east;
        }

        //ToGeometry converts the property enum back to a geometry direction, only used to look up shape tables
        private static Direction ToGeometry(NetCraft.Registry.Enums.Direction direction) => direction switch
        {
            NetCraft.Registry.Enums.Direction.north => Direction.North,
            NetCraft.Registry.Enums.Direction.south => Direction.South,
            NetCraft.Registry.Enums.Direction.west => Direction.West,
            _ => Direction.East,
        };
    }
}
