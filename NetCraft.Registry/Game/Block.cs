using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;

namespace NetCraft.Registry;

//Block abstract base class, maps to vanilla net.minecraft.world.level.block.Block
//Vanilla extends BlockBehaviour; simplified here to an abstract class holding Id and the default BlockState
//Subclasses override Id and DefaultBlockState as needed to provide concrete block definitions
public abstract class Block
{
    //Id the block's registry name, must be implemented by subclasses
    public abstract Identifier Id { get; }

    //DefaultBlockState the block's default state, must be implemented by subclasses
    public abstract BlockState DefaultBlockState { get; }

    //AllStates all possible states of the block in registration order
    //The network palette global id needs every state; by default only the default state, overridden by behavior subclasses
    public virtual IReadOnlyList<BlockState> AllStates => new[] { DefaultBlockState };

    //LightEmission the block's own light emission 0-15
    public virtual int LightEmission => 0;

    //GetLightEmission per-state light emission, maps to vanilla lightLevel
    //Defaults to the state-independent LightEmission; blocks that only emit light when lit, like redstone lamps, override it
    public virtual int GetLightEmission(BlockState state) => LightEmission;

    //CanOcclude whether the block occludes light, maps to canOcclude set by vanilla Properties.noOcclusion
    //Glass, iron bars, fences, doors and slabs turn it off; when off, the occlusion shape at the block position is treated as empty
    public virtual bool CanOcclude => true;

    //UseShapeForLightOcclusion whether the occlusion shape follows the state shape, maps to vanilla useShapeForLightOcclusion
    //True only for explicitly declared blocks; together with IsEmptyShape it decides whether face-level occlusion comparison is done
    public virtual bool UseShapeForLightOcclusion => false;

    //PushReaction the reaction when pushed by a piston, maps to vanilla Properties.pushReaction, default normal
    //The value is injected from the push= section of the embedded block table; anything not listed there is normal
    public virtual NetCraft.Registry.Enums.PushReaction PushReaction
        => NetCraft.Registry.Enums.PushReaction.normal;

    //GetOcclusionShape the shape used for occlusion checks, maps to vanilla getOcclusionShape
    //Vanilla takes state.getShape(empty world view) and BlockBehaviour overrides it with the real shape; the fallback here only serves implementations that do not go through it
    public virtual VoxelShape GetOcclusionShape(BlockState state) => Shapes.Block();

    //SolidRender whether the occlusion shape fills the whole block, maps to vanilla solidRender
    public bool SolidRender(BlockState state)
        => IsShapeFullBlock(CanOcclude ? GetOcclusionShape(state) : Shapes.Empty());

    //GetLightDampening the light attenuation of this state, maps to vanilla getLightDampening
    //A full solid block attenuates 15, one that lets skylight pass straight down attenuates 0, and the rest attenuate 1
    //Vanilla does not use a per-block constant but computes from the state shape; this is what makes attenuation correct for incomplete shapes like glass and slabs
    public virtual int GetLightDampening(BlockState state)
        => SolidRender(state) ? 15 : (PropagatesSkylightDown(state) ? 0 : 1);

    //PropagatesSkylightDown whether skylight can pass straight down through this state, maps to vanilla propagatesSkylightDown
    //Vanilla by default checks whether the visual shape fills the whole block and there is no fluid there
    public virtual bool PropagatesSkylightDown(BlockState state)
        => !IsShapeFullBlock(GetOcclusionShape(state)) && state.FluidState.IsEmpty;

    //IsAir whether it is an air block, maps to vanilla BlockState.isAir; surface rules and placement checks rely on it
    public virtual bool IsAir => false;

    //RandomTicks whether it takes part in random ticks, maps to vanilla Properties.randomTicks, off by default
    //Placed on the base class because section counting is done in the Storage layer, which only knows about Block
    public virtual bool RandomTicks => false;

    //Friction the block surface friction, determining horizontal resistance when an entity lands on it, maps to vanilla getFriction
    //Defaults to 0.6; slippery surfaces like ice override it to 0.98
    public virtual float Friction => 0.6f;

    //GetFluidState gets the fluid state of this state; non-fluid blocks return empty, maps to vanilla getFluidState
    public virtual FluidState GetFluidState(BlockState state) => FluidState.Empty;

    //IsShapeFullBlock whether the shape fills the whole block, maps to vanilla isShapeFullBlock
    //Vanilla caches the result with a 512-capacity weak-key cache; shape instances are basically held by blocks and reused repeatedly, so it is computed directly here
    public static bool IsShapeFullBlock(VoxelShape shape)
        => !Shapes.JoinIsNotEmpty(Shapes.Block(), shape, BooleanOps.NotSame);

    //IsFaceFull whether one face of the shape fills the whole block face, maps to vanilla isFaceFull
    public static bool IsFaceFull(VoxelShape shape, Direction direction)
        => IsShapeFullBlock(shape.GetFaceShape(direction));

    //Box builds a box from 1/16 pixel coordinates, maps to vanilla Block.box
    public static VoxelShape Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        => Shapes.Box(minX / 16.0, minY / 16.0, minZ / 16.0, maxX / 16.0, maxY / 16.0, maxZ / 16.0);

    //Column centered column; the four-argument version takes the two horizontal sizes separately, maps to vanilla Block.column
    public static VoxelShape Column(double sizeXZ, double minY, double maxY)
        => Column(sizeXZ, sizeXZ, minY, maxY);

    public static VoxelShape Column(double sizeX, double sizeZ, double minY, double maxY)
        => Box(8.0 - sizeX / 2.0, minY, 8.0 - sizeZ / 2.0, 8.0 + sizeX / 2.0, maxY, 8.0 + sizeZ / 2.0);

    //Cube a cube centered on all three axes, maps to vanilla Block.cube
    public static VoxelShape Cube(double size) => Cube(size, size, size);

    public static VoxelShape Cube(double sizeX, double sizeY, double sizeZ)
    {
        var halfY = sizeY / 2.0;
        return Column(sizeX, sizeZ, 8.0 - halfY, 8.0 + halfY);
    }

    //BoxZ spans Z with X centered, maps to vanilla Block.boxZ
    public static VoxelShape BoxZ(double sizeXY, double minZ, double maxZ)
        => BoxZ(sizeXY, sizeXY, minZ, maxZ);

    public static VoxelShape BoxZ(double sizeX, double sizeY, double minZ, double maxZ)
    {
        var halfY = sizeY / 2.0;
        return BoxZ(sizeX, 8.0 - halfY, 8.0 + halfY, minZ, maxZ);
    }

    public static VoxelShape BoxZ(double sizeX, double minY, double maxY, double minZ, double maxZ)
        => Box(8.0 - sizeX / 2.0, minY, minZ, 8.0 + sizeX / 2.0, maxY, maxZ);

    //Boxes builds a set of shapes for indexes 0..endInclusive, maps to vanilla Block.boxes
    //Snow layers, crops and candles each have a different, irregular shape per level, so they are computed level by level into a table
    public static VoxelShape[] Boxes(int endInclusive, Func<int, VoxelShape> factory)
    {
        var shapes = new VoxelShape[endInclusive + 1];
        for (var i = 0; i <= endInclusive; i++) shapes[i] = factory(i);
        return shapes;
    }
}
