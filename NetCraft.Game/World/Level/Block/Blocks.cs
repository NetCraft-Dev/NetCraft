using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.World.Level.Block;

//Blocks built-in block constants, maps to vanilla net.minecraft.world.level.block.Blocks
//Registered into the BuiltInRegistries.BLOCK registry
//Real blocks are extended as needed, the rest use placeholder blocks to align with the vanilla registration order
//The network chunk palette uses global BlockState ids and the client expands them in vanilla order, so the state counts must match vanilla
public static partial class Blocks
{
    //AirBlock air block, invisible, non-colliding and does not attenuate light
    public sealed class AirBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("air");
        //Air does not occlude light, the occlusion shape is treated as empty
        public override bool CanOcclude => false;
        //Air has no shape, sky light passes straight through with 0 attenuation
        //The default implementation looks at the visual shape, but air's visual shape is not set separately here and would count as a full block, so this is explicitly corrected
        public override bool PropagatesSkylightDown(BlockState state) => true;
        //Air can be replaced by any placement
        public override bool CanBeReplaced => true;
        //Air satisfies isAir, the criterion surface rules use to detect empty columns
        public override bool IsAir => true;
        //Air does not take part in collisions
        public override bool HasCollision => false;
    }

    //StoneBlock stone block, a basic building material
    public sealed class StoneBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("stone");
        //Vanilla stone hardness 1.5 needs a pickaxe, 150 ticks bare-handed
        public override float DestroySpeed => 1.5f;
        public override bool RequiresCorrectToolForDrops => true;
    }

    //DirtBlock dirt block
    public sealed class DirtBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("dirt");
        //Vanilla dirt hardness 0.5, mineable bare-handed in 15 ticks
        public override float DestroySpeed => 0.5f;
    }

    //GrassBlock grass block, the snowy property takes 2 state slots matching vanilla
    public sealed class GrassBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("grass_block");
        //Vanilla grass hardness 0.6, drops dirt
        public override float DestroySpeed => 0.6f;
        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["snowy"] = new BooleanProperty("snowy") };
    }

    //WaterBlock water block, level 0-15, 16 states total, 0 is the source
    //The bidirectional mapping between level and fluid state is in LiquidBlock, only water's own block properties are here
    public sealed class WaterBlock : LiquidBlock
    {
        public WaterBlock() : base(Material.Fluids.Water) { }

        public override Identifier Id => Identifier.WithDefaultNamespace("water");
        //Water does not occlude light and the occlusion shape is empty, but sky light still does not pass through, attenuation is 1 per block
        public override bool CanOcclude => false;
        //Water can be replaced by block placement, matching the vanilla fluid canBeReplaced semantics
        public override bool CanBeReplaced => true;
        //Fluids do not take part in collisions; walking into water slows you via fluid drag
        public override bool HasCollision => false;
    }

    //LavaBlock lava, level 0-15, 16 states total, 0 is the source
    public sealed class LavaBlock : LiquidBlock
    {
        public LavaBlock() : base(Material.Fluids.Lava) { }

        public override Identifier Id => Identifier.WithDefaultNamespace("lava");
        public override int LightEmission => 15;
        //Lava does not occlude light, attenuating 1 per block like water
        public override bool CanOcclude => false;
        //Fluids do not take part in collisions
        public override bool HasCollision => false;
    }

    //BedrockBlock bedrock hardness -1, unbreakable, maps to vanilla destroyTime(-1)
    public sealed class BedrockBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("bedrock");
        public override float DestroySpeed => -1f;
        public override bool RequiresCorrectToolForDrops => true;
    }

    //BlockSet wood and metal tiers shared by doors, trapdoors and fence gates, decides bare-handed opening and open/close sounds, maps to vanilla BlockSetType
    public enum BlockSet
    {
        Wood,
        Iron,
        Copper,
        Cherry,
        Bamboo,
        NetherWood,
    }

    //PlaceholderBlock placeholder block only used to fill registry slots and align vanilla block ids and state counts
    //Carries dummy properties to expand the same number of states as vanilla; until the real block is implemented it does not take part in world generation
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

    //Water's source fluid state, its block fallback is a water block with level=0
    //Must be deferred to first access; static field initialization would build the water block state before block registration and scramble the global state ids
    public static FluidState WaterFluidState => _waterFluidState ??= Material.Fluids.Water.DefaultFluidState;
    //Lava's source fluid state, its block fallback is a lava block with level=0
    public static FluidState LavaFluidState => _lavaFluidState ??= Material.Fluids.Lava.DefaultFluidState;

    private static FluidState? _waterFluidState;
    private static FluidState? _lavaFluidState;

    //CaveAir cave air, used for carver-excavated holes to distinguish it from surface air
    public static BlockBehaviour CaveAir => Lookup("cave_air");

    //Mycelium mycelium block; carving it out must recompute the top texture together with the dirt below
    public static BlockBehaviour Mycelium => Lookup("mycelium");

    //Lookup returns a registered block by registry name, falls back to air before the block table has bootstrapped
    private static BlockBehaviour Lookup(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path)) as BlockBehaviour ?? AIR;

    //Bootstrap registers all blocks in vanilla order
    //Both the order and each block's state count align with vanilla 26.2, the client decodes the palette with the same global BlockState ids
    //The implemented ones use real classes, the rest become placeholder blocks from the embedded block table, see BlockTable
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
        //Redstone components are numerous and registered in a separate table, see Blocks.Redstone.cs
        RegisterRedstone(real);
        //Piston family, see Blocks.Piston.cs
        RegisterPiston(real);
        //Dispenser and dropper, see Blocks.Dispenser.cs
        RegisterDispenser(real);
        //Tripwire hooks and tripwire, see Blocks.Tripwire.cs
        RegisterTripwire(real);
        //Note block, see Blocks.NoteBlock.cs
        RegisterNoteBlock(real);
        //The four rail types, see Blocks.Rail.cs
        RegisterRails(real);
        //Trapdoors, see Blocks.TrapDoor.cs
        RegisterTrapDoors(real);
        //Fence gates, see Blocks.FenceGate.cs
        RegisterFenceGates(real);
        //Doors, see Blocks.Door.cs
        RegisterDoors(real);
        //Vegetation blocks, see Blocks.Vegetation.cs
        RegisterVegetation(real);
        //Crop blocks, see Blocks.Crops.cs
        RegisterCrops(real);
        //Slab blocks, see Blocks.Slabs.cs
        RegisterSlabs(real);
        //Decoration and functional blocks, see Blocks.Decoration.cs
        RegisterDecoration(real);
        //Container blocks, see Blocks.Containers.cs
        RegisterContainers(real);
        //Crafting blocks, see Blocks.Crafting.cs
        RegisterCrafting(real);
        //Smelting blocks, see Blocks.Furnace.cs
        RegisterFurnaces(real);
        //The extension sections of the table carry the default state overrides from vanilla registerDefaultState, the placement facing category and light occlusion
        //Real block classes are injected too, so placeholders and implementations do not end up with two different sets of default states
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
            //Note block base instrument table, see BlockInstruments
            block.ApplyInstrument(BlockInstruments.Lookup(id.Path));
            Register(block);
        }
    }

    //Register registers a block into the BLOCK registry and triggers state building
    private static void Register(BlockBehaviour block)
    {
        //Accessing DefaultBlockState triggers BlockStateDefinition building
        _ = block.DefaultBlockState;
        Registry<NetCraft.Registry.Block>.Register(BuiltInRegistries.BLOCK, block.Id, block);
        //Synced into the container factory; the chunk save codec depends on the default block, saving a chunk would throw without registration
        PalettedContainerFactory.Default.RegisterBlock(block);
    }
}
